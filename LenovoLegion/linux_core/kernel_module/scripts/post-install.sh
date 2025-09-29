#!/bin/bash
# DKMS Post-install script for Legion Laptop Kernel Module
# Executed after successful module compilation and installation

# Set up permissions and create necessary device nodes

MODULE_NAME="legion_laptop_16irx9"
SYSFS_PATH="/sys/kernel/$MODULE_NAME"

# Create sysfs symlinks for easier access
if [ -d "$SYSFS_PATH" ]; then
    # Create convenient symlinks
    ln -sf "$SYSFS_PATH" "/sys/class/legion-laptop" 2>/dev/null || true
fi

# Set appropriate permissions for hardware access
if [ -d "$SYSFS_PATH" ]; then
    # Allow legion group to access hardware controls
    if ! getent group legion >/dev/null 2>&1; then
        groupadd -r legion 2>/dev/null || true
    fi

    # Set group ownership
    chgrp -R legion "$SYSFS_PATH" 2>/dev/null || true

    # Set permissions for safe hardware access
    chmod -R g+r "$SYSFS_PATH" 2>/dev/null || true

    # Allow write access to specific control files
    for control_file in performance_mode fan_mode rgb_mode; do
        if [ -f "$SYSFS_PATH/$control_file" ]; then
            chmod g+w "$SYSFS_PATH/$control_file" 2>/dev/null || true
        fi
    done
fi

# Create udev rules for automatic permissions
cat > /etc/udev/rules.d/99-legion-laptop.rules << 'EOF'
# Legion Laptop Hardware Access Rules
# Allows legion group members to control hardware

SUBSYSTEM=="platform", DRIVER=="legion_laptop_16irx9", GROUP="legion", MODE="0664"
KERNEL=="legion_laptop_16irx9", GROUP="legion", MODE="0664"

# EC register access
SUBSYSTEM=="platform", ATTRS{driver}=="legion_laptop_16irx9", GROUP="legion", MODE="0664"
EOF

# Reload udev rules
udevadm control --reload-rules 2>/dev/null || true
udevadm trigger 2>/dev/null || true

# Log successful installation
logger "Legion laptop kernel module installed successfully" || true

exit 0