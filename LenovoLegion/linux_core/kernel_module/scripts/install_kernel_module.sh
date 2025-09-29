#!/bin/bash
# Automated Legion Kernel Module Installation Script
# Handles DKMS installation, dependency checking, and system integration

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
MODULE_NAME="legion-laptop"
MODULE_VERSION="6.0.0"
KERNEL_MODULE="legion_laptop_16irx9"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULE_DIR="$(dirname "$SCRIPT_DIR")"

# Logging
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if running as root
check_root() {
    if [ "$EUID" -ne 0 ]; then
        log_error "This script must be run as root (use sudo)"
        exit 1
    fi
}

# Detect distribution
detect_distribution() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        DISTRO="$ID"
        VERSION="$VERSION_ID"
        log_info "Detected distribution: $PRETTY_NAME"
    else
        log_error "Cannot detect Linux distribution"
        exit 1
    fi
}

# Install system dependencies
install_dependencies() {
    log_info "Installing system dependencies..."

    case "$DISTRO" in
        ubuntu|debian)
            apt update
            apt install -y dkms build-essential linux-headers-$(uname -r) \
                          linux-headers-generic bc kmod cpio flex cpio \
                          libssl-dev libelf-dev
            ;;
        fedora|centos|rhel)
            if command -v dnf &> /dev/null; then
                dnf install -y dkms kernel-devel kernel-headers gcc make \
                              bc elfutils-libelf-devel openssl-devel
            else
                yum install -y dkms kernel-devel kernel-headers gcc make \
                              bc elfutils-libelf-devel openssl-devel
            fi
            ;;
        arch|manjaro)
            pacman -S --noconfirm dkms linux-headers base-devel bc kmod cpio \
                                 pahole openssl libelf
            ;;
        opensuse*)
            zypper install -y dkms kernel-default-devel gcc make bc \
                             libopenssl-devel libelf-devel
            ;;
        *)
            log_warning "Unsupported distribution: $DISTRO"
            log_info "Please install the following packages manually:"
            log_info "  - dkms"
            log_info "  - kernel headers for $(uname -r)"
            log_info "  - build-essential/development tools"
            read -p "Continue anyway? (y/N): " -n 1 -r
            echo
            if [[ ! $REPLY =~ ^[Yy]$ ]]; then
                exit 1
            fi
            ;;
    esac

    log_success "Dependencies installed successfully"
}

# Check hardware compatibility
check_hardware() {
    log_info "Checking hardware compatibility..."

    # Check for Legion laptop
    if dmidecode -s system-product-name | grep -qi "legion"; then
        PRODUCT_NAME=$(dmidecode -s system-product-name)
        log_success "Legion laptop detected: $PRODUCT_NAME"

        # Check for specific Gen 9 model
        if echo "$PRODUCT_NAME" | grep -qi "16IRX9"; then
            log_success "Legion Slim 7i Gen 9 (16IRX9) detected - full compatibility"
        else
            log_warning "Different Legion model detected - limited compatibility expected"
        fi
    else
        log_warning "Non-Legion laptop detected - module may not function correctly"
        read -p "Continue installation? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi

    # Check for Intel processor
    if lscpu | grep -qi "intel"; then
        CPU_MODEL=$(lscpu | grep "Model name" | cut -d':' -f2 | xargs)
        log_success "Intel CPU detected: $CPU_MODEL"
    else
        log_warning "Non-Intel CPU detected - some features may not work"
    fi

    # Check for NVIDIA GPU
    if lspci | grep -qi "nvidia"; then
        GPU_MODEL=$(lspci | grep -i nvidia | head -n1 | cut -d':' -f3 | xargs)
        log_success "NVIDIA GPU detected: $GPU_MODEL"
    else
        log_info "No NVIDIA GPU detected - GPU features will be disabled"
    fi
}

# Check for existing installation
check_existing() {
    log_info "Checking for existing installation..."

    if dkms status | grep -q "$MODULE_NAME"; then
        log_warning "Existing Legion kernel module found"
        read -p "Remove existing installation? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            remove_existing
        else
            log_error "Cannot proceed with existing installation"
            exit 1
        fi
    fi

    if lsmod | grep -q "$KERNEL_MODULE"; then
        log_warning "Legion kernel module is currently loaded"
        log_info "Removing existing module..."
        modprobe -r "$KERNEL_MODULE" || true
    fi
}

# Remove existing installation
remove_existing() {
    log_info "Removing existing installation..."

    # Remove from DKMS
    dkms remove "$MODULE_NAME/$MODULE_VERSION" --all 2>/dev/null || true

    # Remove module directory
    rm -rf "/usr/src/$MODULE_NAME-$MODULE_VERSION" 2>/dev/null || true

    # Remove from modules
    modprobe -r "$KERNEL_MODULE" 2>/dev/null || true

    log_success "Existing installation removed"
}

# Install kernel module
install_module() {
    log_info "Installing Legion kernel module..."

    # Copy source to DKMS location
    DKMS_DIR="/usr/src/$MODULE_NAME-$MODULE_VERSION"
    mkdir -p "$DKMS_DIR"
    cp -r "$MODULE_DIR"/* "$DKMS_DIR/"

    # Set permissions
    chmod 755 "$DKMS_DIR"
    chmod 644 "$DKMS_DIR"/*.c "$DKMS_DIR"/*.h "$DKMS_DIR"/Makefile "$DKMS_DIR"/dkms.conf 2>/dev/null || true
    chmod 755 "$DKMS_DIR"/scripts/*.sh 2>/dev/null || true

    # Add to DKMS
    log_info "Adding module to DKMS..."
    dkms add "$MODULE_NAME/$MODULE_VERSION"

    # Build module
    log_info "Building kernel module (this may take a few minutes)..."
    if dkms build "$MODULE_NAME/$MODULE_VERSION"; then
        log_success "Module built successfully"
    else
        log_error "Module build failed"
        log_info "Check build logs: /var/lib/dkms/$MODULE_NAME/$MODULE_VERSION/build/make.log"
        exit 1
    fi

    # Install module
    log_info "Installing kernel module..."
    if dkms install "$MODULE_NAME/$MODULE_VERSION"; then
        log_success "Module installed successfully"
    else
        log_error "Module installation failed"
        exit 1
    fi
}

# Load and test module
test_module() {
    log_info "Loading and testing kernel module..."

    # Load module
    if modprobe "$KERNEL_MODULE"; then
        log_success "Module loaded successfully"
    else
        log_error "Failed to load module"
        exit 1
    fi

    # Test basic functionality
    if [ -d "/sys/kernel/$KERNEL_MODULE" ]; then
        log_success "Module sysfs interface created"
    else
        log_warning "Module sysfs interface not found - limited functionality"
    fi

    # Check dmesg for errors
    if dmesg | tail -20 | grep -i error | grep -i legion; then
        log_warning "Errors found in kernel log - check dmesg"
    else
        log_success "No errors found in kernel log"
    fi
}

# Configure module autoload
configure_autoload() {
    log_info "Configuring module autoload..."

    # Create modprobe configuration
    cat > /etc/modprobe.d/legion-laptop.conf << EOF
# Legion Laptop Kernel Module Configuration
# Automatically load legion_laptop_16irx9 module

# Module options
options legion_laptop_16irx9 debug=0 force_load=0

# Module aliases for automatic loading
alias pci:v00008086d*sv000017AAsd*bc03sc*i* legion_laptop_16irx9
EOF

    # Add to modules-load.d
    echo "$KERNEL_MODULE" > /etc/modules-load.d/legion-laptop.conf

    # Update initramfs
    log_info "Updating initramfs..."
    if command -v update-initramfs &> /dev/null; then
        update-initramfs -u
    elif command -v dracut &> /dev/null; then
        dracut -f
    elif command -v mkinitcpio &> /dev/null; then
        mkinitcpio -P
    else
        log_warning "Could not update initramfs automatically"
    fi

    log_success "Module autoload configured"
}

# Create systemd service
create_service() {
    log_info "Creating systemd service..."

    cat > /etc/systemd/system/legion-laptop.service << EOF
[Unit]
Description=Legion Laptop Hardware Support
After=multi-user.target
Wants=multi-user.target

[Service]
Type=oneshot
RemainAfterExit=yes
ExecStart=/sbin/modprobe legion_laptop_16irx9
ExecStop=/sbin/modprobe -r legion_laptop_16irx9
TimeoutSec=30

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable legion-laptop.service

    log_success "Systemd service created and enabled"
}

# Print final status
print_status() {
    log_success "Legion Kernel Module Installation Complete!"
    echo
    echo "=== Installation Summary ==="
    echo "Module Name: $KERNEL_MODULE"
    echo "Version: $MODULE_VERSION"
    echo "DKMS Status: $(dkms status $MODULE_NAME 2>/dev/null || echo 'Unknown')"
    echo "Module Loaded: $(lsmod | grep -q $KERNEL_MODULE && echo 'Yes' || echo 'No')"
    echo "Sysfs Interface: $([ -d /sys/kernel/$KERNEL_MODULE ] && echo 'Available' || echo 'Not Available')"
    echo
    echo "=== Usage ==="
    echo "Check status: systemctl status legion-laptop"
    echo "Module info: modinfo $KERNEL_MODULE"
    echo "Hardware access: ls /sys/kernel/$KERNEL_MODULE/"
    echo
    echo "=== Troubleshooting ==="
    echo "View logs: journalctl -u legion-laptop"
    echo "Check dmesg: dmesg | grep legion"
    echo "DKMS status: dkms status"
    echo
    log_info "Reboot recommended to ensure all components are properly loaded"
}

# Error handling
cleanup() {
    if [ $? -ne 0 ]; then
        log_error "Installation failed!"
        log_info "Cleaning up partial installation..."
        dkms remove "$MODULE_NAME/$MODULE_VERSION" --all 2>/dev/null || true
        rm -rf "/usr/src/$MODULE_NAME-$MODULE_VERSION" 2>/dev/null || true
        modprobe -r "$KERNEL_MODULE" 2>/dev/null || true
    fi
}

trap cleanup EXIT

# Main installation flow
main() {
    echo "=================================================="
    echo "Legion Toolkit - Kernel Module Installation"
    echo "Version: $MODULE_VERSION"
    echo "Target: Legion Slim 7i Gen 9 (16IRX9)"
    echo "=================================================="
    echo

    check_root
    detect_distribution
    check_hardware
    install_dependencies
    check_existing
    install_module
    test_module
    configure_autoload
    create_service
    print_status

    log_success "Installation completed successfully!"
}

# Handle command line arguments
case "${1:-install}" in
    install)
        main
        ;;
    remove)
        log_info "Removing Legion kernel module..."
        systemctl disable legion-laptop.service 2>/dev/null || true
        rm -f /etc/systemd/system/legion-laptop.service
        systemctl daemon-reload
        remove_existing
        rm -f /etc/modprobe.d/legion-laptop.conf
        rm -f /etc/modules-load.d/legion-laptop.conf
        log_success "Legion kernel module removed"
        ;;
    status)
        echo "=== Legion Kernel Module Status ==="
        echo "DKMS Status: $(dkms status $MODULE_NAME 2>/dev/null || echo 'Not installed')"
        echo "Module Loaded: $(lsmod | grep -q $KERNEL_MODULE && echo 'Yes' || echo 'No')"
        echo "Service Status: $(systemctl is-active legion-laptop 2>/dev/null || echo 'inactive')"
        ;;
    *)
        echo "Usage: $0 [install|remove|status]"
        echo "  install - Install the Legion kernel module (default)"
        echo "  remove  - Remove the Legion kernel module"
        echo "  status  - Show installation status"
        exit 1
        ;;
esac