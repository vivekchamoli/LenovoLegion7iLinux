#!/bin/bash
# Legion Toolkit Uninstallation Script

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Installation paths
INSTALL_DIR="/opt/legion-toolkit"
BIN_DIR="/usr/local/bin"
CONFIG_DIR="$HOME/.config/legion-toolkit"
DESKTOP_FILE="/usr/share/applications/legion-toolkit.desktop"
SERVICE_FILE="$HOME/.config/systemd/user/legion-toolkit.service"
MODULE_CONF="/etc/modules-load.d/legion-toolkit.conf"

print_header() {
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}  Legion Toolkit Uninstallation Script${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo
}

print_error() {
    echo -e "${RED}Error: $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}Warning: $1${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

stop_service() {
    echo "Stopping Legion Toolkit service..."

    if [ -f "$SERVICE_FILE" ]; then
        systemctl --user stop legion-toolkit.service 2>/dev/null || true
        systemctl --user disable legion-toolkit.service 2>/dev/null || true
        rm -f "$SERVICE_FILE"
        systemctl --user daemon-reload
        print_success "Service stopped and removed"
    else
        print_warning "Service file not found"
    fi
}

remove_application() {
    echo "Removing application files..."

    # Remove installation directory
    if [ -d "$INSTALL_DIR" ]; then
        sudo rm -rf "$INSTALL_DIR"
        print_success "Application files removed"
    else
        print_warning "Installation directory not found"
    fi

    # Remove symbolic link
    if [ -L "$BIN_DIR/legion-toolkit" ]; then
        sudo rm -f "$BIN_DIR/legion-toolkit"
        print_success "Command-line interface removed"
    fi

    # Remove desktop file
    if [ -f "$DESKTOP_FILE" ]; then
        sudo rm -f "$DESKTOP_FILE"

        # Update desktop database
        if command -v update-desktop-database &> /dev/null; then
            sudo update-desktop-database /usr/share/applications 2>/dev/null || true
        fi

        print_success "Desktop entry removed"
    fi
}

remove_icons() {
    echo "Removing application icons..."

    # Remove PNG icons
    for size in 16 32 48 64 128 256 512; do
        ICON_FILE="/usr/share/icons/hicolor/${size}x${size}/apps/legion-toolkit.png"
        if [ -f "$ICON_FILE" ]; then
            sudo rm -f "$ICON_FILE"
            print_success "Removed ${size}x${size} icon"
        fi
    done

    # Remove SVG icon
    SVG_ICON="/usr/share/icons/hicolor/scalable/apps/legion-toolkit.svg"
    if [ -f "$SVG_ICON" ]; then
        sudo rm -f "$SVG_ICON"
        print_success "Removed SVG icon"
    fi

    # Update icon cache
    if command -v gtk-update-icon-cache &> /dev/null; then
        sudo gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
    fi

    print_success "Icons removed"
}

remove_man_page() {
    echo "Removing man page..."

    MAN_FILE="/usr/share/man/man1/legion-toolkit.1.gz"
    if [ -f "$MAN_FILE" ]; then
        sudo rm -f "$MAN_FILE"

        # Update man database
        if command -v mandb &> /dev/null; then
            sudo mandb -q 2>/dev/null || true
        fi

        print_success "Man page removed"
    else
        print_warning "Man page not found"
    fi
}

remove_kernel_modules() {
    echo "Removing kernel module configuration..."

    if [ -f "$MODULE_CONF" ]; then
        sudo rm -f "$MODULE_CONF"
        print_success "Kernel module configuration removed"
    fi
}

backup_config() {
    echo "Backing up configuration..."

    if [ -d "$CONFIG_DIR" ]; then
        BACKUP_DIR="$HOME/.config/legion-toolkit-backup-$(date +%Y%m%d-%H%M%S)"
        cp -r "$CONFIG_DIR" "$BACKUP_DIR"
        print_success "Configuration backed up to $BACKUP_DIR"
    else
        print_warning "No configuration found to backup"
    fi
}

remove_config() {
    if [ -d "$CONFIG_DIR" ]; then
        rm -rf "$CONFIG_DIR"
        print_success "Configuration removed"
    fi
}

main() {
    print_header

    echo "This will uninstall Legion Toolkit from your system."
    read -p "Continue? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Uninstallation cancelled."
        exit 0
    fi

    echo

    # Stop running services
    stop_service

    # Backup configuration
    backup_config

    # Remove application
    remove_application

    # Remove icons
    remove_icons

    # Remove man page
    remove_man_page

    # Remove kernel module config
    remove_kernel_modules

    # Ask about configuration
    echo
    read -p "Remove configuration files? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        remove_config
    else
        print_warning "Configuration files preserved in $CONFIG_DIR"
    fi

    echo
    echo -e "${GREEN}Uninstallation completed successfully!${NC}"

    if [ -d "$BACKUP_DIR" ]; then
        echo "Your configuration has been backed up to:"
        echo "  $BACKUP_DIR"
    fi
}

# Check if Legion Toolkit is running
if pgrep -f "LenovoLegionToolkit.Avalonia" > /dev/null; then
    echo "Legion Toolkit is currently running."
    read -p "Stop it and continue? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        pkill -f "LenovoLegionToolkit.Avalonia" || true
        sleep 2
    else
        echo "Please close Legion Toolkit manually and run this script again."
        exit 1
    fi
fi

main