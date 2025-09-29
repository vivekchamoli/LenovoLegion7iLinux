#!/bin/bash
# DKMS post-remove script for Legion Laptop module

set -e

MODULE_NAME="legion_laptop_16irx9"
MODULE_VERSION="6.0.0"

echo "Legion Laptop Module Post-Remove Script"
echo "Module: $MODULE_NAME v$MODULE_VERSION"

# Unload module if loaded
if lsmod | grep -q "$MODULE_NAME"; then
    echo "Unloading $MODULE_NAME module..."
    modprobe -r "$MODULE_NAME" || {
        echo "Warning: Failed to unload module"
    }
fi

# Remove sysfs symlink
SYSFS_LINK="/sys/kernel/legion_laptop"
if [ -L "$SYSFS_LINK" ]; then
    echo "Removing sysfs symlink..."
    rm -f "$SYSFS_LINK"
    echo "Symlink removed: $SYSFS_LINK"
fi

# Clean up configuration files (optional - comment out to preserve settings)
# UDEV_RULES_FILE="/etc/udev/rules.d/99-legion-laptop.rules"
# MODPROBE_CONF="/etc/modprobe.d/legion-laptop.conf"
# MODULES_LOAD_CONF="/etc/modules-load.d/legion-laptop.conf"

# if [ -f "$UDEV_RULES_FILE" ]; then
#     echo "Removing udev rules..."
#     rm -f "$UDEV_RULES_FILE"
#     udevadm control --reload-rules
#     udevadm trigger
# fi

# if [ -f "$MODPROBE_CONF" ]; then
#     echo "Removing modprobe configuration..."
#     rm -f "$MODPROBE_CONF"
# fi

# if [ -f "$MODULES_LOAD_CONF" ]; then
#     echo "Removing modules-load configuration..."
#     rm -f "$MODULES_LOAD_CONF"
# fi

# Note: We don't remove the legion group as other applications might use it

echo "Legion Laptop module removal completed!"
echo ""
echo "Note: Configuration files have been preserved."
echo "To completely remove all traces:"
echo "  sudo rm -f /etc/udev/rules.d/99-legion-laptop.rules"
echo "  sudo rm -f /etc/modprobe.d/legion-laptop.conf"
echo "  sudo rm -f /etc/modules-load.d/legion-laptop.conf"
echo "  sudo groupdel legion"

exit 0