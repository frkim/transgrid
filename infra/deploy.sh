#!/bin/bash
#
# RNE Operational Plans Export - Azure Deployment Script
#
# This script deploys all Azure resources for the RNE Export demo including:
# - Storage Account with Blob, Table, and File shares
# - Container Apps Environment with SFTP servers (primary and backup)
# - Logic Apps Standard for workflow orchestration
# - Azure Functions for JSON to XML transformation
# - Application Insights for monitoring
#
# Usage:
#   ./deploy.sh --resource-group "rg-transgrid-demo" --location "westeurope"
#   ./deploy.sh -g "rg-transgrid-prod" -l "westeurope" -e "prod" --allowed-ips "10.0.0.0/8,192.168.0.0/16"
#

set -e

# Default values
ENVIRONMENT="dev"
SFTP_PASSWORD=""
ALLOWED_IP_RANGES=""
OPS_API_ENDPOINT="http://localhost:5000/graphql"
SKIP_SSH_KEY_GENERATION=false
WHAT_IF=false

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Print functions
info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
    exit 1
}

# Help message
show_help() {
    cat << EOF
Usage: $(basename "$0") [OPTIONS]

Deploy the RNE Operational Plans Export infrastructure to Azure.

Required:
  -g, --resource-group NAME    Azure Resource Group name
  -l, --location LOCATION      Azure region (e.g., westeurope)

Optional:
  -e, --environment ENV        Environment name: dev, test, prod (default: dev)
  -p, --password PASSWORD      SFTP password (will prompt if not provided)
  --allowed-ips IPS            Comma-separated list of allowed IP ranges (CIDR)
  --ops-api-endpoint URL       Operations API endpoint (default: http://localhost:5000/graphql)
  --skip-ssh-keys              Skip SSH key generation
  --what-if                    Show what would be deployed without deploying
  -h, --help                   Show this help message

Examples:
  $(basename "$0") -g "rg-transgrid-demo" -l "westeurope"
  $(basename "$0") -g "rg-transgrid-prod" -l "westeurope" -e "prod" --allowed-ips "10.0.0.0/8"
EOF
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -g|--resource-group)
            RESOURCE_GROUP="$2"
            shift 2
            ;;
        -l|--location)
            LOCATION="$2"
            shift 2
            ;;
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -p|--password)
            SFTP_PASSWORD="$2"
            shift 2
            ;;
        --allowed-ips)
            ALLOWED_IP_RANGES="$2"
            shift 2
            ;;
        --ops-api-endpoint)
            OPS_API_ENDPOINT="$2"
            shift 2
            ;;
        --skip-ssh-keys)
            SKIP_SSH_KEY_GENERATION=true
            shift
            ;;
        --what-if)
            WHAT_IF=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            error "Unknown option: $1"
            ;;
    esac
done

# Validate required parameters
if [[ -z "$RESOURCE_GROUP" ]]; then
    error "Resource group is required. Use -g or --resource-group"
fi

if [[ -z "$LOCATION" ]]; then
    error "Location is required. Use -l or --location"
fi

# Header
echo "=============================================="
echo "RNE Operational Plans Export - Azure Deployment"
echo "=============================================="
echo ""
info "Resource Group: $RESOURCE_GROUP"
info "Location: $LOCATION"
info "Environment: $ENVIRONMENT"
echo ""

# Check Azure CLI
info "Checking Azure CLI..."
if ! command -v az &> /dev/null; then
    error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecli"
fi
AZ_VERSION=$(az version --query '"azure-cli"' -o tsv)
info "Azure CLI version: $AZ_VERSION"

# Check Bicep
info "Checking Bicep CLI..."
if ! az bicep version &> /dev/null; then
    info "Installing Bicep CLI..."
    az bicep install
fi
info "Bicep ready"

# Check login status
info "Checking Azure login status..."
if ! az account show &> /dev/null; then
    info "Please log in to Azure..."
    az login
fi
ACCOUNT_NAME=$(az account show --query "user.name" -o tsv)
SUBSCRIPTION=$(az account show --query "name" -o tsv)
info "Logged in as: $ACCOUNT_NAME"
info "Subscription: $SUBSCRIPTION"

# Prompt for SFTP password if not provided
if [[ -z "$SFTP_PASSWORD" ]]; then
    echo ""
    read -s -p "Enter SFTP password: " SFTP_PASSWORD
    echo ""
fi

# Create resource group if it doesn't exist
echo ""
info "Ensuring resource group exists..."
RG_EXISTS=$(az group exists --name "$RESOURCE_GROUP")
if [[ "$RG_EXISTS" == "false" ]]; then
    if [[ "$WHAT_IF" == "false" ]]; then
        az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none
        success "Created resource group: $RESOURCE_GROUP"
    else
        info "[WHAT-IF] Would create resource group: $RESOURCE_GROUP"
    fi
else
    info "Resource group already exists: $RESOURCE_GROUP"
fi

# Generate SSH keys if needed
SSH_KEY_DIR="$SCRIPT_DIR/ssh-keys"
if [[ "$SKIP_SSH_KEY_GENERATION" == "false" ]]; then
    echo ""
    info "Generating SSH host keys..."
    
    mkdir -p "$SSH_KEY_DIR"
    
    ED25519_KEY="$SSH_KEY_DIR/ssh_host_ed25519_key"
    RSA_KEY="$SSH_KEY_DIR/ssh_host_rsa_key"
    
    if [[ ! -f "$ED25519_KEY" ]]; then
        if [[ "$WHAT_IF" == "false" ]]; then
            ssh-keygen -t ed25519 -f "$ED25519_KEY" -N '' -q
            success "Generated ED25519 host key"
        else
            info "[WHAT-IF] Would generate ED25519 host key"
        fi
    else
        info "ED25519 key already exists"
    fi
    
    if [[ ! -f "$RSA_KEY" ]]; then
        if [[ "$WHAT_IF" == "false" ]]; then
            ssh-keygen -t rsa -b 4096 -f "$RSA_KEY" -N '' -q
            success "Generated RSA host key"
        else
            info "[WHAT-IF] Would generate RSA host key"
        fi
    else
        info "RSA key already exists"
    fi
fi

# Deploy Bicep template
echo ""
info "Deploying infrastructure..."
info "This may take 10-15 minutes..."

DEPLOYMENT_NAME="rne-export-$ENVIRONMENT-$(date +%Y%m%d%H%M%S)"

DEPLOY_PARAMS=(
    --name "$DEPLOYMENT_NAME"
    --resource-group "$RESOURCE_GROUP"
    --template-file "$SCRIPT_DIR/main.bicep"
    --parameters "environment=$ENVIRONMENT"
    --parameters "location=$LOCATION"
    --parameters "sftpPassword=$SFTP_PASSWORD"
    --parameters "opsApiEndpoint=$OPS_API_ENDPOINT"
)

if [[ -n "$ALLOWED_IP_RANGES" ]]; then
    # Convert comma-separated to JSON array
    IP_ARRAY=$(echo "$ALLOWED_IP_RANGES" | sed 's/,/","/g' | sed 's/^/["/' | sed 's/$/"]/')
    DEPLOY_PARAMS+=(--parameters "allowedSftpIpRanges=$IP_ARRAY")
fi

if [[ "$WHAT_IF" == "true" ]]; then
    DEPLOY_PARAMS+=(--what-if)
fi

DEPLOY_PARAMS+=(--output json)

DEPLOYMENT_RESULT=$(az deployment group create "${DEPLOY_PARAMS[@]}")

if [[ $? -ne 0 ]]; then
    error "Deployment failed!"
fi

success "Deployment completed successfully!"

# Extract outputs
if [[ "$WHAT_IF" == "false" ]]; then
    STORAGE_ACCOUNT=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.storageAccountName.value')
    BLOB_CONTAINER=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.blobContainerName.value')
    PRIMARY_SFTP=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.primarySftpEndpoint.value')
    BACKUP_SFTP=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.backupSftpEndpoint.value')
    PRIMARY_SFTP_FQDN=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.primarySftpFqdn.value')
    FUNCTION_APP=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.functionAppName.value')
    FUNCTION_ENDPOINT=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.functionEndpoint.value')
    LOGIC_APP=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.logicAppName.value')
    LOGIC_APP_HOST=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.logicAppHostname.value')
    APP_INSIGHTS=$(echo "$DEPLOYMENT_RESULT" | jq -r '.properties.outputs.appInsightsName.value')

    echo ""
    echo "=============================================="
    echo "Deployment Outputs"
    echo "=============================================="
    echo ""
    info "Storage Account: $STORAGE_ACCOUNT"
    info "Blob Container: $BLOB_CONTAINER"
    echo ""
    info "Primary SFTP Endpoint: $PRIMARY_SFTP"
    info "Backup SFTP Endpoint: $BACKUP_SFTP"
    info "SFTP Username: rneuser"
    echo ""
    info "Function App: $FUNCTION_APP"
    info "Function Endpoint: $FUNCTION_ENDPOINT"
    echo ""
    info "Logic App: $LOGIC_APP"
    info "Logic App URL: https://$LOGIC_APP_HOST"
    echo ""
    info "Application Insights: $APP_INSIGHTS"

    # Upload SSH keys to Azure Files
    if [[ "$SKIP_SSH_KEY_GENERATION" == "false" ]] && [[ -d "$SSH_KEY_DIR" ]]; then
        echo ""
        info "Uploading SSH keys to Azure Files..."
        
        STORAGE_KEY=$(az storage account keys list --account-name "$STORAGE_ACCOUNT" --query "[0].value" -o tsv)
        
        # Upload to sshkeys share
        for KEY_FILE in "$SSH_KEY_DIR"/*; do
            if [[ -f "$KEY_FILE" ]]; then
                FILENAME=$(basename "$KEY_FILE")
                az storage file upload \
                    --account-name "$STORAGE_ACCOUNT" \
                    --account-key "$STORAGE_KEY" \
                    --share-name "sshkeys" \
                    --source "$KEY_FILE" \
                    --path "$FILENAME" \
                    --output none
                success "Uploaded: $FILENAME"
            fi
        done
        
        # Upload copykeys.sh to scripts share
        COPYKEYS_SCRIPT="$SCRIPT_DIR/scripts/copykeys.sh"
        if [[ -f "$COPYKEYS_SCRIPT" ]]; then
            az storage file upload \
                --account-name "$STORAGE_ACCOUNT" \
                --account-key "$STORAGE_KEY" \
                --share-name "scripts" \
                --source "$COPYKEYS_SCRIPT" \
                --path "copykeys.sh" \
                --output none
            success "Uploaded: copykeys.sh"
        fi
        
        # Upload to backup shares
        for KEY_FILE in "$SSH_KEY_DIR"/*; do
            if [[ -f "$KEY_FILE" ]]; then
                FILENAME=$(basename "$KEY_FILE")
                az storage file upload \
                    --account-name "$STORAGE_ACCOUNT" \
                    --account-key "$STORAGE_KEY" \
                    --share-name "sshkeys-backup" \
                    --source "$KEY_FILE" \
                    --path "$FILENAME" \
                    --output none
            fi
        done
        
        if [[ -f "$COPYKEYS_SCRIPT" ]]; then
            az storage file upload \
                --account-name "$STORAGE_ACCOUNT" \
                --account-key "$STORAGE_KEY" \
                --share-name "scripts-backup" \
                --source "$COPYKEYS_SCRIPT" \
                --path "copykeys.sh" \
                --output none
        fi
        
        success "SSH keys uploaded to Azure Files shares"
    fi

    echo ""
    echo "=============================================="
    echo "Next Steps"
    echo "=============================================="
    echo ""
    info "1. Deploy Azure Function code:"
    echo "   func azure functionapp publish $FUNCTION_APP"
    echo ""
    info "2. Import Logic App workflows:"
    echo "   - Open Logic App in Azure Portal"
    echo "   - Create new workflows from workflow definitions"
    echo "   - Configure API connections (Blob, SFTP, Table)"
    echo ""
    info "3. Test SFTP connection:"
    echo "   sftp -P 22 rneuser@$PRIMARY_SFTP_FQDN"
    echo ""
    info "4. Run the mock server locally:"
    echo "   cd ../sources/server/Transgrid.MockServer"
    echo "   dotnet run"
    echo ""
fi

# Clean up password from environment
unset SFTP_PASSWORD
