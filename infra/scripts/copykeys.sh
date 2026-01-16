#!/bin/bash
# SSH Key Persistence Script for atmoz/sftp container
# This script copies SSH host keys from Azure Files mount to /etc/ssh
# preventing the "REMOTE HOST IDENTIFICATION HAS CHANGED" warning
# 
# Mount this script to /etc/sftp.d/copykeys.sh (read-only)
# Mount SSH keys to /etc/sftpkeys (read-only)
#
# Reference: https://charbelnemnom.com/deploy-sftp-service-on-azure-container-apps/

# Copy SSH host keys from mounted Azure Files share to /etc/ssh
cp /etc/sftpkeys/ssh_host_* /etc/ssh/

# Set correct permissions on private keys
chmod 600 /etc/ssh/ssh_host_ed25519_key 2>/dev/null || true
chmod 600 /etc/ssh/ssh_host_rsa_key 2>/dev/null || true

# Set correct permissions on public keys  
chmod 644 /etc/ssh/ssh_host_ed25519_key.pub 2>/dev/null || true
chmod 644 /etc/ssh/ssh_host_rsa_key.pub 2>/dev/null || true

echo "[copykeys.sh] SSH host keys copied from /etc/sftpkeys to /etc/ssh"
