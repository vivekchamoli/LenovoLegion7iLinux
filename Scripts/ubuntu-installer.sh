#!/bin/bash
# Legion Toolkit One-Click Ubuntu Installer
# Downloads and installs the latest version

set -e

# Configuration
GITHUB_USER="vivekchamoli"
GITHUB_REPO="LenovoLegion7i"
REPO_URL="https://github.com/${GITHUB_USER}/${GITHUB_REPO}"
INSTALL_DIR="/tmp/legion-toolkit-install"
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

# Banner
echo -e "${BLUE}"
cat << "EOF"
╔═══════════════════════════════════════════════════════╗
║                                                       ║
║            LEGION TOOLKIT UBUNTU INSTALLER            ║
║         Comprehensive Lenovo Legion Control           ║
║                                                       ║
╚═══════════════════════════════════════════════════════╝
EOF
echo -e "${NC}"

# Check if running as root
if [ "$EUID" -eq 0 ]; then
   echo -e "${RED}✗ Please do not run this installer as root${NC}"
   echo "Run as normal user: ./ubuntu-installer.sh"
   exit 1
fi

# Check Ubuntu version
if [ -f /etc/os-release ]; then
    . /etc/os-release
    if [[ "$ID" != "ubuntu" && "$ID_LIKE" != *"ubuntu"* && "$ID" != "debian" ]]; then
        echo -e "${YELLOW}⚠ Warning: This installer is designed for Ubuntu/Debian${NC}"
        read -p "Continue anyway? (y/N): " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
    fi
else
    echo -e "${YELLOW}⚠ Cannot detect OS version${NC}"
fi

echo -e "${GREEN}▶ Checking system requirements...${NC}"

# Check hardware
if ! grep -qi "lenovo" /sys/devices/virtual/dmi/id/sys_vendor 2>/dev/null; then
    echo -e "${YELLOW}⚠ This doesn't appear to be a Lenovo system${NC}"
    read -p "Continue anyway? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Install dependencies
echo -e "${GREEN}▶ Installing dependencies...${NC}"
sudo apt-get update
sudo apt-get install -y \
    wget \
    curl \
    libx11-6 \
    libice6 \
    libsm6 \
    libfontconfig1 \
    acpi \
    acpid \
    pciutils \
    usbutils \
    lm-sensors \
    fancontrol

# Install .NET if not present
if ! command -v dotnet &> /dev/null; then
    echo -e "${GREEN}▶ Installing .NET Runtime...${NC}"
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
    chmod +x dotnet-install.sh
    ./dotnet-install.sh --runtime dotnet --channel 8.0
    rm dotnet-install.sh
    export PATH="$HOME/.dotnet:$PATH"
fi

# Create installation directory
echo -e "${GREEN}▶ Preparing installation...${NC}"
rm -rf "$INSTALL_DIR"
mkdir -p "$INSTALL_DIR"
cd "$INSTALL_DIR"

# Download method selection
echo ""
echo "Select installation method:"
echo "  1) Download latest release (recommended)"
echo "  2) Build from source"
echo "  3) Install from local package"
read -p "Choice [1-3]: " -r INSTALL_METHOD

case $INSTALL_METHOD in
    1)
        echo -e "${GREEN}▶ Downloading latest release...${NC}"
        # Get latest release URL
        DOWNLOAD_URL="${REPO_URL}/releases/latest/download/legion-toolkit-ubuntu.tar.gz"

        if ! wget -q --spider "$DOWNLOAD_URL" 2>/dev/null; then
            echo -e "${YELLOW}⚠ Using local build instead...${NC}"
            # Fallback to build from source
            INSTALL_METHOD=2
        else
            wget -O legion-toolkit.tar.gz "$DOWNLOAD_URL"
            tar -xzf legion-toolkit.tar.gz
            cd legion-toolkit-ubuntu-*
        fi
        ;;

    2)
        echo -e "${GREEN}▶ Building from source...${NC}"
        # Clone repository
        git clone "${REPO_URL}.git" || {
            echo -e "${RED}✗ Failed to clone repository${NC}"
            exit 1
        }
        cd LenovoLegion7iToolkit

        # Build
        cd LenovoLegionToolkit.Avalonia
        dotnet publish \
            -c Release \
            -r linux-x64 \
            --self-contained true \
            -p:PublishSingleFile=true \
            -o ../build

        cd ../build
        ;;

    3)
        echo -e "${YELLOW}▶ Enter path to .deb package:${NC}"
        read -r PACKAGE_PATH
        if [ ! -f "$PACKAGE_PATH" ]; then
            echo -e "${RED}✗ Package not found${NC}"
            exit 1
        fi
        cp "$PACKAGE_PATH" .
        ;;

    *)
        echo -e "${RED}✗ Invalid choice${NC}"
        exit 1
        ;;
esac

# Install the package
echo -e "${GREEN}▶ Installing Legion Toolkit...${NC}"

if [ -f *.deb ]; then
    sudo dpkg -i *.deb || {
        echo -e "${YELLOW}▶ Fixing dependencies...${NC}"
        sudo apt-get install -f -y
        sudo dpkg -i *.deb
    }
elif [ -f LegionToolkit ]; then
    # Manual installation from built binary
    sudo mkdir -p /usr/local/bin
    sudo cp LegionToolkit /usr/local/bin/legion-toolkit
    sudo chmod +x /usr/local/bin/legion-toolkit

    # Create desktop file
    sudo tee /usr/share/applications/legion-toolkit.desktop > /dev/null << EOF
[Desktop Entry]
Name=Legion Toolkit
Comment=Control Lenovo Legion laptop features
Exec=/usr/local/bin/legion-toolkit
Icon=legion-toolkit
Terminal=false
Type=Application
Categories=System;Settings;
EOF
fi

# Check for kernel module
echo -e "${GREEN}▶ Checking kernel module...${NC}"
if ! lsmod | grep -q legion_laptop; then
    echo -e "${YELLOW}⚠ Legion laptop kernel module not detected${NC}"
    echo ""
    echo "Would you like to install it now? (recommended)"
    read -p "Install legion-laptop module? (Y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        # Try to install from repository first
        if sudo apt-get install -y legion-laptop-dkms 2>/dev/null; then
            echo -e "${GREEN}✓ Kernel module installed${NC}"
        else
            echo -e "${YELLOW}Building kernel module from source...${NC}"
            cd /tmp
            git clone https://github.com/johnfanv2/LenovoLegion5LinuxSupport.git
            cd LenovoLegion5LinuxSupport/kernel_module
            make
            sudo make install
            sudo modprobe legion-laptop
        fi
    fi
fi

# Configure sensors
echo -e "${GREEN}▶ Configuring sensors...${NC}"
sudo sensors-detect --auto

# Setup user groups
echo -e "${GREEN}▶ Setting up user permissions...${NC}"
sudo usermod -a -G video,input "$USER"

# Enable services
echo -e "${GREEN}▶ Configuring services...${NC}"
if systemctl list-unit-files | grep -q legion-toolkit; then
    sudo systemctl daemon-reload
    sudo systemctl enable legion-toolkit
    echo "Legion Toolkit service enabled"
fi

# Cleanup
echo -e "${GREEN}▶ Cleaning up...${NC}"
cd /
rm -rf "$INSTALL_DIR"

# Final message
echo ""
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Legion Toolkit installation complete!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo ""
echo "Available commands:"
echo "  • GUI Application:  ${BLUE}legion-toolkit-gui${NC}"
echo "  • Command Line:     ${BLUE}legion-toolkit --help${NC}"
echo "  • Start Service:    ${BLUE}sudo systemctl start legion-toolkit${NC}"
echo ""
echo -e "${YELLOW}Note: Log out and back in for group changes to take effect${NC}"
echo ""
echo "Quick actions:"
echo "  Set quiet mode:       legion-toolkit power set quiet"
echo "  Enable conservation:  legion-toolkit battery conservation on"
echo "  RGB control:          legion-toolkit rgb set 00FF00"
echo ""
echo "For more information: ${BLUE}man legion-toolkit${NC}"

# Offer to start GUI
echo ""
read -p "Would you like to start Legion Toolkit now? (Y/n): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Nn]$ ]]; then
    nohup legion-toolkit-gui > /dev/null 2>&1 &
    echo -e "${GREEN}✓ Legion Toolkit started${NC}"
fi