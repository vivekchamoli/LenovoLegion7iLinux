#!/bin/bash
# Legion Toolkit for Linux - One-Line Installer
# Usage: curl -sSL https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7iLinux/main/install.sh | sudo bash

set -e

echo "============================================="
echo "🚀 Legion Toolkit for Linux - Quick Install"
echo "============================================="

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}❌ Please run with sudo${NC}"
    echo "Usage: curl -sSL https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7iLinux/main/install.sh | sudo bash"
    exit 1
fi

# Get the original user (the one who ran sudo)
ORIGINAL_USER="${SUDO_USER:-$USER}"
ORIGINAL_HOME=$(eval echo ~$ORIGINAL_USER)

echo -e "${BLUE}🔍 Installing for user: $ORIGINAL_USER${NC}"

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64) DOWNLOAD_ARCH="x64" ;;
    aarch64) DOWNLOAD_ARCH="arm64" ;;
    *) echo -e "${RED}❌ Unsupported architecture: $ARCH${NC}"; exit 1 ;;
esac

echo -e "${GREEN}✅ Detected architecture: $ARCH${NC}"

# Create temporary directory
TEMP_DIR=$(mktemp -d)
cd "$TEMP_DIR"

echo -e "${YELLOW}📥 Downloading Legion Toolkit...${NC}"

# Download latest release
RELEASE_URL="https://api.github.com/repos/vivekchamoli/LenovoLegion7iLinux/releases/latest"
DOWNLOAD_URL=$(curl -s "$RELEASE_URL" | grep -o '"browser_download_url": "[^"]*\.deb"' | cut -d'"' -f4 | head -1)

if [ -z "$DOWNLOAD_URL" ]; then
    echo -e "${RED}❌ Failed to get download URL${NC}"
    echo -e "${YELLOW}⚠️  Falling back to direct installation...${NC}"

    # Fallback: Clone and build
    if ! command -v dotnet &> /dev/null; then
        echo -e "${YELLOW}📦 Installing .NET 8 SDK...${NC}"

        # Install .NET based on distro
        if command -v apt &> /dev/null; then
            apt update
            apt install -y dotnet-sdk-8.0
        elif command -v dnf &> /dev/null; then
            dnf install -y dotnet-sdk-8.0
        elif command -v pacman &> /dev/null; then
            pacman -S --noconfirm dotnet-sdk
        else
            echo -e "${RED}❌ Please install .NET 8 SDK manually${NC}"
            exit 1
        fi
    fi

    echo -e "${YELLOW}📥 Cloning repository...${NC}"
    git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
    cd LenovoLegion7iLinux/LenovoLegionToolkit.Avalonia

    echo -e "${YELLOW}🔨 Building application...${NC}"
    ./build-linux-complete.sh

    echo -e "${YELLOW}📦 Installing from build...${NC}"
    cd publish
    ./install-system.sh
else
    echo -e "${YELLOW}📥 Downloading package...${NC}"
    wget -q "$DOWNLOAD_URL" -O legion-toolkit.deb

    echo -e "${YELLOW}📦 Installing package...${NC}"
    dpkg -i legion-toolkit.deb || apt-get install -f -y
fi

# Add user to legion group
echo -e "${YELLOW}👥 Adding $ORIGINAL_USER to legion group...${NC}"
usermod -a -G legion "$ORIGINAL_USER"

# Load legion module if available
echo -e "${YELLOW}🔧 Loading legion-laptop module...${NC}"
modprobe legion-laptop 2>/dev/null || echo -e "${YELLOW}⚠️  legion-laptop module not available${NC}"

# Cleanup
cd /
rm -rf "$TEMP_DIR"

echo ""
echo -e "${GREEN}✅ Installation completed successfully!${NC}"
echo ""
echo -e "${YELLOW}📋 Next Steps:${NC}"
echo "   1. Log out and log back in (or restart)"
echo "   2. Launch: legion-toolkit"
echo ""
echo -e "${YELLOW}💡 Quick Start:${NC}"
echo "   • GUI: Search 'Legion Toolkit' in applications"
echo "   • CLI: legion-toolkit --help"
echo "   • Status: legion-toolkit status"
echo ""
echo -e "${YELLOW}🔧 Hardware Setup:${NC}"
echo "   • Ensure legion-laptop module is loaded"
echo "   • Check permissions: groups $ORIGINAL_USER"
echo ""
echo -e "${BLUE}📖 Documentation: https://github.com/vivekchamoli/LenovoLegion7iLinux${NC}"
echo -e "${GREEN}🎉 Enjoy your Legion Toolkit for Linux!${NC}"