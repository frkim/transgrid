// Global functions for data management

async function generateNewData() {
    if (!confirm('Are you sure you want to generate new data? This will add new records to existing data.')) {
        return;
    }
    
    try {
        const response = await fetch('/api/DataManagement/generate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        
        if (response.ok) {
            showNotification('success', 'New data generated successfully!');
            setTimeout(() => location.reload(), 1000);
        } else {
            showNotification('error', 'Failed to generate new data');
        }
    } catch (error) {
        console.error('Error generating data:', error);
        showNotification('error', 'Error generating new data');
    }
}

async function resetData() {
    if (!confirm('Are you sure you want to reset data to baseline? This will delete all current data.')) {
        return;
    }
    
    try {
        const response = await fetch('/api/DataManagement/reset', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        
        if (response.ok) {
            showNotification('success', 'Data reset to baseline successfully!');
            setTimeout(() => location.reload(), 1000);
        } else {
            showNotification('error', 'Failed to reset data');
        }
    } catch (error) {
        console.error('Error resetting data:', error);
        showNotification('error', 'Error resetting data');
    }
}

async function updateData() {
    const periodSelect = document.getElementById('updatePeriodSelect');
    const periodMinutes = parseInt(periodSelect.value);
    
    if (!confirm(`Update data with ${periodSelect.options[periodSelect.selectedIndex].text} period?`)) {
        return;
    }
    
    try {
        const response = await fetch('/api/DataManagement/update', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ periodMinutes: periodMinutes })
        });
        
        if (response.ok) {
            showNotification('success', `Data updated with ${periodSelect.options[periodSelect.selectedIndex].text} period!`);
            setTimeout(() => location.reload(), 1000);
        } else {
            showNotification('error', 'Failed to update data');
        }
    } catch (error) {
        console.error('Error updating data:', error);
        showNotification('error', 'Error updating data');
    }
}

function setPeriodAndUpdate(minutes) {
    event.preventDefault();
    const periodSelect = document.getElementById('updatePeriodSelect');
    periodSelect.value = minutes;
    updateData();
}

function showNotification(type, message) {
    // Create a toast notification
    const toastContainer = document.getElementById('toastContainer') || createToastContainer();
    
    const toast = document.createElement('div');
    toast.className = `toast align-items-center text-white bg-${type === 'success' ? 'success' : 'danger'} border-0`;
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');
    
    // Build the toast content safely without using innerHTML for dynamic text
    const wrapper = document.createElement('div');
    wrapper.className = 'd-flex';
    
    const body = document.createElement('div');
    body.className = 'toast-body';
    
    const icon = document.createElement('i');
    icon.classList.add('bi');
    icon.classList.add(type === 'success' ? 'bi-check-circle' : 'bi-exclamation-circle');
    body.appendChild(icon);
    
    // Add a space and the message text safely using textContent
    body.appendChild(document.createTextNode(' ' + message));
    
    const closeButton = document.createElement('button');
    closeButton.type = 'button';
    closeButton.className = 'btn-close btn-close-white me-2 m-auto';
    closeButton.setAttribute('data-bs-dismiss', 'toast');
    
    wrapper.appendChild(body);
    wrapper.appendChild(closeButton);
    toast.appendChild(wrapper);
    
    toastContainer.appendChild(toast);
    const bsToast = new bootstrap.Toast(toast);
    bsToast.show();
    
    // Remove toast after it's hidden
    toast.addEventListener('hidden.bs.toast', () => {
        toast.remove();
    });
}

function createToastContainer() {
    const container = document.createElement('div');
    container.id = 'toastContainer';
    container.className = 'toast-container position-fixed top-0 end-0 p-3';
    container.style.zIndex = '9999';
    document.body.appendChild(container);
    return container;
}

// Utility function to format dates
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric'
    });
}

function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}

// Add active class to current nav item
document.addEventListener('DOMContentLoaded', function() {
    const currentPath = window.location.pathname;
    document.querySelectorAll('.navbar-nav .nav-link').forEach(link => {
        if (link.getAttribute('href') === currentPath) {
            link.classList.add('active');
        }
    });
});
