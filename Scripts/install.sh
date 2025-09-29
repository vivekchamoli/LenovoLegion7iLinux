#!/bin/bash
# Legion Toolkit Installation Script for Linux
# Supports: Ubuntu, Debian, Fedora, Arch Linux

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Installation paths
INSTALL_DIR="/opt/legion-toolkit"
BIN_DIR="/usr/local/bin"
CONFIG_DIR="$HOME/.config/legion-toolkit"
DESKTOP_FILE="/usr/share/applications/legion-toolkit.desktop"
SERVICE_FILE="$HOME/.config/systemd/user/legion-toolkit.service"

# Functions
print_header() {
    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}  Legion Toolkit Installation Script${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo
}

print_error() {
    echo -e "${RED}Error: $1${NC}"
    exit 1
}

print_warning() {
    echo -e "${YELLOW}Warning: $1${NC}"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

detect_distro() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS=$ID
        VER=$VERSION_ID
    else
        print_error "Cannot detect Linux distribution"
    fi
}

check_requirements() {
    echo "Checking system requirements..."

    # Check if running as root
    if [ "$EUID" -eq 0 ]; then
        print_warning "Running as root. Will install system-wide."
    fi

    # Check for required commands
    local missing_deps=""

    for cmd in git make gcc; do
        if ! command -v $cmd &> /dev/null; then
            missing_deps="$missing_deps $cmd"
        fi
    done

    if [ ! -z "$missing_deps" ]; then
        print_error "Missing required dependencies:$missing_deps"
    fi

    # Check for systemd
    if ! command -v systemctl &> /dev/null; then
        print_warning "systemd not found. Service auto-start will not be available."
    fi

    # Check for Legion hardware
    if [ ! -d /sys/bus/platform/drivers/legion_laptop ]; then
        print_warning "Legion kernel module not detected. Some features may not work."
        echo "You may need to install the legion-laptop kernel module first."
        read -p "Continue anyway? (y/n) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 0
        fi
    fi

    print_success "System requirements check passed"
}

install_dependencies() {
    echo "Installing dependencies..."

    case "$OS" in
        ubuntu|debian)
            sudo apt-get update
            sudo apt-get install -y \
                libx11-dev \
                libxi-dev \
                libgl1-mesa-glx \
                libglib2.0-0 \
                libfontconfig1 \
                libnotify-bin \
                acpi \
                sensors-detect \
                i2c-tools
            ;;
        fedora|rhel|centos)
            sudo dnf install -y \
                libX11-devel \
                libXi-devel \
                mesa-libGL \
                glib2 \
                fontconfig \
                libnotify \
                acpi \
                lm_sensors \
                i2c-tools
            ;;
        arch|manjaro)
            sudo pacman -S --needed \
                libx11 \
                libxi \
                mesa \
                glib2 \
                fontconfig \
                libnotify \
                acpi \
                lm_sensors \
                i2c-tools
            ;;
        *)
            print_warning "Unknown distribution. Please install dependencies manually."
            ;;
    esac

    print_success "Dependencies installed"
}

build_application() {
    echo "Building Legion Toolkit..."

    cd "$(dirname "$0")/.."

    # Clean previous builds
    rm -rf bin/Release

    # Build the application
    dotnet publish LenovoLegionToolkit.Avalonia/LenovoLegionToolkit.Avalonia.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o bin/Release/linux-x64

    if [ ! -f "bin/Release/linux-x64/LenovoLegionToolkit.Avalonia" ]; then
        print_error "Build failed. Application binary not found."
    fi

    print_success "Application built successfully"
}

install_application() {
    echo "Installing Legion Toolkit..."

    # Create installation directory
    sudo mkdir -p "$INSTALL_DIR"

    # Copy application files
    sudo cp -r bin/Release/linux-x64/* "$INSTALL_DIR/"
    sudo chmod +x "$INSTALL_DIR/LenovoLegionToolkit.Avalonia"

    # Create symbolic link for CLI
    sudo ln -sf "$INSTALL_DIR/LenovoLegionToolkit.Avalonia" "$BIN_DIR/legion-toolkit"

    # Create config directory
    mkdir -p "$CONFIG_DIR"

    # Copy default configuration if it doesn't exist
    if [ ! -f "$CONFIG_DIR/settings.json" ]; then
        cat > "$CONFIG_DIR/settings.json" << EOF
{
    "version": "1.0.0",
    "theme": "dark",
    "startMinimized": false,
    "enableSystemTray": true,
    "monitoring": {
        "enabled": true,
        "interval": 5
    },
    "notifications": {
        "enabled": true,
        "temperatureAlerts": true,
        "batteryAlerts": true
    }
}
EOF
    fi

    print_success "Application installed to $INSTALL_DIR"
}

install_desktop_file() {
    echo "Creating desktop entry..."

    # Check if desktop file exists in the source
    DESKTOP_SOURCE="$(dirname "$0")/../LenovoLegionToolkit.Avalonia/Resources/legion-toolkit.desktop"

    if [ -f "$DESKTOP_SOURCE" ]; then
        # Copy the desktop file from resources
        sudo cp "$DESKTOP_SOURCE" "$DESKTOP_FILE"
        # Update the Exec path
        sudo sed -i "s|/opt/legion-toolkit/LegionToolkit|$INSTALL_DIR/LenovoLegionToolkit.Avalonia|g" "$DESKTOP_FILE"
    else
        # Create desktop file if not found
        sudo tee "$DESKTOP_FILE" > /dev/null << EOF
[Desktop Entry]
Name=Legion Toolkit
GenericName=System Control Panel
Comment=Control panel for Lenovo Legion laptops
Exec=$INSTALL_DIR/LenovoLegionToolkit.Avalonia
Icon=legion-toolkit
Terminal=false
Type=Application
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;hardware;performance;battery;thermal;rgb;
StartupNotify=true
StartupWMClass=LenovoLegionToolkit.Avalonia
EOF
    fi

    # Update desktop database
    if command -v update-desktop-database &> /dev/null; then
        sudo update-desktop-database /usr/share/applications 2>/dev/null || true
    fi

    print_success "Desktop entry created"
}

install_icons() {
    echo "Installing application icons..."

    ICON_SOURCE_DIR="$(dirname "$0")/../LenovoLegionToolkit.Avalonia/Resources/icons"

    if [ -d "$ICON_SOURCE_DIR" ]; then
        # Install PNG icons in various sizes
        for size in 16 32 48 64 128 256 512; do
            ICON_DIR="/usr/share/icons/hicolor/${size}x${size}/apps"
            if [ -f "$ICON_SOURCE_DIR/${size}x${size}/legion-toolkit.png" ]; then
                sudo mkdir -p "$ICON_DIR"
                sudo cp "$ICON_SOURCE_DIR/${size}x${size}/legion-toolkit.png" "$ICON_DIR/"
                print_success "Installed ${size}x${size} icon"
            fi
        done

        # Install SVG icon
        if [ -f "$ICON_SOURCE_DIR/legion-toolkit.svg" ]; then
            sudo mkdir -p "/usr/share/icons/hicolor/scalable/apps"
            sudo cp "$ICON_SOURCE_DIR/legion-toolkit.svg" "/usr/share/icons/hicolor/scalable/apps/"
            print_success "Installed SVG icon"
        fi

        # Update icon cache
        if command -v gtk-update-icon-cache &> /dev/null; then
            sudo gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
        fi

        # Also copy an icon to the installation directory as fallback
        if [ -f "$ICON_SOURCE_DIR/256x256/legion-toolkit.png" ]; then
            sudo cp "$ICON_SOURCE_DIR/256x256/legion-toolkit.png" "$INSTALL_DIR/icon.png"
        elif [ -f "$ICON_SOURCE_DIR/legion-toolkit.svg" ]; then
            sudo cp "$ICON_SOURCE_DIR/legion-toolkit.svg" "$INSTALL_DIR/icon.svg"
        fi
    else
        print_warning "Icons directory not found. Icons will not be installed."
    fi

    print_success "Icons installation completed"
}

install_man_page() {
    echo "Installing man page..."

    MAN_SOURCE="$(dirname "$0")/../LenovoLegionToolkit.Avalonia/Resources/man/legion-toolkit.1"
    MAN_DIR="/usr/share/man/man1"

    if [ -f "$MAN_SOURCE" ]; then
        sudo mkdir -p "$MAN_DIR"
        sudo cp "$MAN_SOURCE" "$MAN_DIR/"
        sudo gzip -f "$MAN_DIR/legion-toolkit.1"

        # Update man database
        if command -v mandb &> /dev/null; then
            sudo mandb -q 2>/dev/null || true
        fi

        print_success "Man page installed"
    else
        print_warning "Man page not found. Manual will not be installed."
    fi
}

install_systemd_service() {
    echo "Installing systemd service..."

    # Create user service directory
    mkdir -p "$(dirname "$SERVICE_FILE")"

    # Create service file
    cat > "$SERVICE_FILE" << EOF
[Unit]
Description=Legion Toolkit
After=graphical-session.target

[Service]
Type=simple
ExecStart=$INSTALL_DIR/LenovoLegionToolkit.Avalonia --daemon
Restart=on-failure
RestartSec=10

[Install]
WantedBy=default.target
EOF

    # Reload systemd
    systemctl --user daemon-reload

    print_success "Systemd service installed"

    read -p "Enable auto-start on login? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        systemctl --user enable legion-toolkit.service
        print_success "Auto-start enabled"
    fi
}

setup_kernel_modules() {
    echo "Setting up kernel modules..."

    # Load required modules
    sudo modprobe i2c-dev 2>/dev/null || true
    sudo modprobe i2c-i801 2>/dev/null || true

    # Make modules load on boot
    if [ -d /etc/modules-load.d ]; then
        echo "i2c-dev" | sudo tee /etc/modules-load.d/legion-toolkit.conf > /dev/null
        echo "i2c-i801" | sudo tee -a /etc/modules-load.d/legion-toolkit.conf > /dev/null
    fi

    # Add user to i2c group for DDC/CI access
    if getent group i2c > /dev/null; then
        sudo usermod -a -G i2c $USER
        print_success "Added user to i2c group (logout required to take effect)"
    fi

    print_success "Kernel modules configured"
}

post_install() {
    echo
    echo -e "${GREEN}Installation completed successfully!${NC}"
    echo
    echo "You can now:"
    echo "  • Run 'legion-toolkit' from terminal"
    echo "  • Launch Legion Toolkit from your application menu"
    echo "  • Use 'legion-toolkit --help' for CLI options"
    echo

    if [ -f "$SERVICE_FILE" ]; then
        echo "To start the daemon now:"
        echo "  systemctl --user start legion-toolkit.service"
        echo
    fi

    echo "Please logout and login again for all changes to take effect."

    read -p "Start Legion Toolkit now? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        "$INSTALL_DIR/LenovoLegionToolkit.Avalonia" &
        print_success "Legion Toolkit started"
    fi
}

# Main installation flow
main() {
    print_header

    detect_distro
    echo "Detected: $OS $VER"
    echo

    check_requirements
    install_dependencies
    build_application
    install_application
    install_icons
    install_desktop_file
    install_man_page
    install_systemd_service
    setup_kernel_modules
    post_install
}

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK not found. Please install .NET 8.0 SDK first."
fi

# Run main installation
main