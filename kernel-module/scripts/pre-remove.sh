#!/bin/bash
# Enhanced Legion Laptop Kernel Module Pre-Remove Script
# Author: Vivek Chamoli
# Version: 2.0.0

set -e

MODULE_NAME="legion_laptop_enhanced"
MODULE_VERSION="2.0.0"
KERNEL_VERSION="$1"

echo "=== Legion Enhanced Kernel Module Pre-Remove ==="
echo "Module: $MODULE_NAME v$MODULE_VERSION"
echo "Kernel: $KERNEL_VERSION"

# Function to log messages
log() {
    echo "[$(date '+%Y-%m-%d %H:%M:%S')] $1"
}

# Function to check if module is loaded
is_module_loaded() {
    lsmod | grep -q "^$1 "
}

# Function to safely unload module
unload_module() {
    local module="$1"
    if is_module_loaded "$module"; then
        log "Unloading module: $module"

        # Try graceful unload first
        if rmmod "$module" 2>/dev/null; then
            log "✓ Module unloaded successfully"
            return 0
        else
            log "⚠ Could not unload module gracefully"

            # Check if module is in use
            local usage=$(lsmod | grep "^$module " | awk '{print $3}')
            if [ "$usage" != "0" ]; then
                log "Module is in use (usage count: $usage)"
                log "Attempting to stop dependent services"

                # Stop Legion Toolkit if running
                if systemctl is-active --quiet legion-toolkit 2>/dev/null; then
                    log "Stopping Legion Toolkit service"
                    systemctl stop legion-toolkit || true
                fi

                # Wait and try again
                sleep 2
                if rmmod "$module" 2>/dev/null; then
                    log "✓ Module unloaded after stopping services"
                    return 0
                else
                    log "✗ Could not unload module - it may require a reboot"
                    return 1
                fi
            else
                log "✗ Module unload failed for unknown reason"
                return 1
            fi
        fi
    else
        log "Module $module is not loaded"
        return 0
    fi
}

# Function to remove udev rules
remove_udev_rules() {
    local udev_file="/etc/udev/rules.d/99-legion-enhanced.rules"

    if [ -f "$udev_file" ]; then
        log "Removing udev rules: $udev_file"
        rm -f "$udev_file"

        # Reload udev rules
        if command -v udevadm >/dev/null 2>&1; then
            log "Reloading udev rules"
            udevadm control --reload-rules
        fi
    fi
}

# Function to remove module configuration
remove_module_config() {
    local modules_load_file="/etc/modules-load.d/legion-enhanced.conf"
    local modprobe_file="/etc/modprobe.d/legion-enhanced.conf"

    log "Removing module configuration files"

    if [ -f "$modules_load_file" ]; then
        rm -f "$modules_load_file"
        log "Removed: $modules_load_file"
    fi

    if [ -f "$modprobe_file" ]; then
        rm -f "$modprobe_file"
        log "Removed: $modprobe_file"
    fi
}

# Function to restore original module
restore_original_module() {
    local backup_path="/lib/modules/$KERNEL_VERSION/kernel/drivers/platform/x86/legion-laptop.ko.backup"
    local orig_module_path="/lib/modules/$KERNEL_VERSION/kernel/drivers/platform/x86/legion-laptop.ko"

    if [ -f "$backup_path" ]; then
        log "Restoring original legion-laptop module"
        cp "$backup_path" "$orig_module_path"
        rm -f "$backup_path"
        log "✓ Original module restored"
    else
        log "No backup of original module found"
    fi
}

# Function to clean up module files
cleanup_module_files() {
    local module_path="/lib/modules/$KERNEL_VERSION/extra/$MODULE_NAME.ko"

    if [ -f "$module_path" ]; then
        log "Removing module file: $module_path"
        rm -f "$module_path"
    fi

    # Remove empty extra directory if it exists
    local extra_dir="/lib/modules/$KERNEL_VERSION/extra"
    if [ -d "$extra_dir" ] && [ -z "$(ls -A "$extra_dir")" ]; then
        rmdir "$extra_dir" 2>/dev/null || true
    fi
}

# Function to show removal status
show_removal_status() {
    log "Checking removal status..."

    # Check if module is still loaded
    if is_module_loaded "$MODULE_NAME"; then
        log "⚠ Module is still loaded - reboot required for complete removal"
    else
        log "✓ Module successfully unloaded"
    fi

    # Check if files are removed
    local module_path="/lib/modules/$KERNEL_VERSION/extra/$MODULE_NAME.ko"
    if [ ! -f "$module_path" ]; then
        log "✓ Module files removed"
    else
        log "⚠ Some module files may still exist"
    fi
}

# Main removal process
main() {
    log "Starting pre-remove process"

    # Unload the module
    if ! unload_module "$MODULE_NAME"; then
        log "⚠ Module unload failed - continuing with file cleanup"
    fi

    # Remove configuration
    remove_module_config
    remove_udev_rules

    # Clean up files
    cleanup_module_files

    # Restore original module if available
    restore_original_module

    # Update module dependencies
    log "Updating module dependencies"
    depmod -a "$KERNEL_VERSION"

    # Show removal status
    show_removal_status

    log "=== Pre-remove completed ==="
    echo ""
    echo "Legion Enhanced Kernel Module removal process completed."
    if is_module_loaded "$MODULE_NAME"; then
        echo "⚠ A reboot is required to complete the removal process."
    else
        echo "✓ Module has been successfully removed."
    fi
}

# Run main function
main "$@"