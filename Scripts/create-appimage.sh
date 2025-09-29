#!/bin/bash

# Create AppImage for Legion Toolkit
# This script creates a portable AppImage that can run on most Linux distributions

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Legion Toolkit AppImage Builder${NC}"
echo "================================"

# Check if running in correct directory
if [ ! -f "LenovoLegionToolkit.Avalonia/LenovoLegionToolkit.Avalonia.csproj" ]; then
    echo -e "${RED}Error: Please run this script from the root of the repository${NC}"
    exit 1
fi

# Variables
APP_NAME="LegionToolkit"
APP_VERSION="3.0.0"
ARCH="x86_64"
BUILD_DIR="build/AppDir"
OUTPUT_DIR="release"

# Clean previous builds
echo -e "${YELLOW}Cleaning previous builds...${NC}"
rm -rf $BUILD_DIR
rm -rf $OUTPUT_DIR
mkdir -p $BUILD_DIR
mkdir -p $OUTPUT_DIR

# Build the application
echo -e "${YELLOW}Building Legion Toolkit...${NC}"
cd LenovoLegionToolkit.Avalonia
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ../$BUILD_DIR/usr/bin
cd ..

# Create desktop entry
echo -e "${YELLOW}Creating desktop entry...${NC}"
cat > $BUILD_DIR/LegionToolkit.desktop << EOF
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Comment=Comprehensive system management for Lenovo Legion laptops
Exec=LegionToolkit
Icon=LegionToolkit
Categories=System;Settings;
Terminal=false
StartupNotify=true
EOF

# Create AppRun script
echo -e "${YELLOW}Creating AppRun script...${NC}"
cat > $BUILD_DIR/AppRun << 'EOF'
#!/bin/bash
SELF=$(readlink -f "$0")
HERE="${SELF%/*}"
export PATH="${HERE}/usr/bin:${HERE}/usr/sbin:${HERE}/usr/games:${HERE}/bin:${HERE}/sbin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${HERE}/usr/lib/x86_64-linux-gnu:${HERE}/usr/lib64:${HERE}/lib:${HERE}/lib/x86_64-linux-gnu:${HERE}/lib64:${LD_LIBRARY_PATH}"

# Check if running with required permissions
if [ "$EUID" -ne 0 ]; then
    echo "Legion Toolkit requires root privileges for hardware control."
    echo "Please run with: sudo $0"

    # Try to use pkexec if available
    if command -v pkexec > /dev/null 2>&1; then
        pkexec "${HERE}/usr/bin/LegionToolkit" "$@"
    else
        echo "You can also install 'pkexec' for graphical sudo prompt."
        exit 1
    fi
else
    exec "${HERE}/usr/bin/LegionToolkit" "$@"
fi
EOF
chmod +x $BUILD_DIR/AppRun

# Create icon (placeholder - you should add actual icon)
echo -e "${YELLOW}Creating application icon...${NC}"
cat > $BUILD_DIR/LegionToolkit.svg << 'EOF'
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <rect width="256" height="256" fill="#1a1a1a"/>
  <text x="128" y="128" font-family="Arial" font-size="48" font-weight="bold"
        text-anchor="middle" dominant-baseline="middle" fill="#ff6b6b">LT</text>
  <text x="128" y="180" font-family="Arial" font-size="16"
        text-anchor="middle" fill="#ffffff">Legion Toolkit</text>
</svg>
EOF

# Copy icon to multiple sizes
mkdir -p $BUILD_DIR/usr/share/icons/hicolor/256x256/apps
cp $BUILD_DIR/LegionToolkit.svg $BUILD_DIR/usr/share/icons/hicolor/256x256/apps/

# Download AppImage tools if not present
echo -e "${YELLOW}Downloading AppImage tools...${NC}"
if [ ! -f "appimagetool-x86_64.AppImage" ]; then
    wget -q "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x appimagetool-x86_64.AppImage
fi

# Create the AppImage
echo -e "${YELLOW}Creating AppImage...${NC}"
ARCH=$ARCH ./appimagetool-x86_64.AppImage $BUILD_DIR $OUTPUT_DIR/${APP_NAME}-${APP_VERSION}-${ARCH}.AppImage

# Create installation script
echo -e "${YELLOW}Creating installation script...${NC}"
cat > $OUTPUT_DIR/install.sh << 'EOF'
#!/bin/bash

# Legion Toolkit Installer for Linux

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}Legion Toolkit Installer${NC}"
echo "========================"

# Check if running as root
if [ "$EUID" -eq 0 ]; then
   echo -e "${RED}Please do not run this installer as root${NC}"
   exit 1
fi

# Find the AppImage
APPIMAGE=$(find . -name "LegionToolkit-*.AppImage" | head -n1)
if [ -z "$APPIMAGE" ]; then
    echo -e "${RED}Error: AppImage not found${NC}"
    exit 1
fi

# Installation directories
INSTALL_DIR="$HOME/.local/share/LegionToolkit"
BIN_DIR="$HOME/.local/bin"
DESKTOP_DIR="$HOME/.local/share/applications"
ICON_DIR="$HOME/.local/share/icons"

# Create directories
echo -e "${YELLOW}Creating directories...${NC}"
mkdir -p "$INSTALL_DIR"
mkdir -p "$BIN_DIR"
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICON_DIR"

# Copy AppImage
echo -e "${YELLOW}Installing Legion Toolkit...${NC}"
cp "$APPIMAGE" "$INSTALL_DIR/LegionToolkit.AppImage"
chmod +x "$INSTALL_DIR/LegionToolkit.AppImage"

# Create launcher script
cat > "$BIN_DIR/legion-toolkit" << SCRIPT
#!/bin/bash
exec "$INSTALL_DIR/LegionToolkit.AppImage" "\$@"
SCRIPT
chmod +x "$BIN_DIR/legion-toolkit"

# Extract and install icon
echo -e "${YELLOW}Installing icon...${NC}"
"$INSTALL_DIR/LegionToolkit.AppImage" --appimage-extract LegionToolkit.svg > /dev/null 2>&1 || true
if [ -f "squashfs-root/LegionToolkit.svg" ]; then
    cp "squashfs-root/LegionToolkit.svg" "$ICON_DIR/"
    rm -rf squashfs-root
fi

# Create desktop entry
echo -e "${YELLOW}Creating desktop entry...${NC}"
cat > "$DESKTOP_DIR/legion-toolkit.desktop" << DESKTOP
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Comment=Comprehensive system management for Lenovo Legion laptops
Exec=$BIN_DIR/legion-toolkit
Icon=$ICON_DIR/LegionToolkit.svg
Categories=System;Settings;
Terminal=false
StartupNotify=true
DESKTOP

# Update desktop database
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database "$DESKTOP_DIR" 2>/dev/null || true
fi

# Add to PATH if not already there
if [[ ":$PATH:" != *":$BIN_DIR:"* ]]; then
    echo -e "${YELLOW}Adding $BIN_DIR to PATH...${NC}"
    echo "export PATH=\"\$PATH:$BIN_DIR\"" >> "$HOME/.bashrc"
    echo -e "${YELLOW}Please run: source ~/.bashrc${NC}"
fi

echo -e "${GREEN}Installation complete!${NC}"
echo ""
echo "You can now run Legion Toolkit using:"
echo "  - Desktop launcher: Look for 'Legion Toolkit' in your application menu"
echo "  - Terminal: legion-toolkit"
echo ""
echo -e "${YELLOW}Note: Legion Toolkit requires root privileges for hardware control.${NC}"
echo "It will prompt for your password when needed."
EOF
chmod +x $OUTPUT_DIR/install.sh

# Create uninstall script
echo -e "${YELLOW}Creating uninstall script...${NC}"
cat > $OUTPUT_DIR/uninstall.sh << 'EOF'
#!/bin/bash

# Legion Toolkit Uninstaller

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${RED}Legion Toolkit Uninstaller${NC}"
echo "=========================="
echo ""
read -p "Are you sure you want to uninstall Legion Toolkit? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Uninstall cancelled."
    exit 0
fi

echo -e "${YELLOW}Removing Legion Toolkit...${NC}"

# Remove files
rm -f "$HOME/.local/bin/legion-toolkit"
rm -rf "$HOME/.local/share/LegionToolkit"
rm -f "$HOME/.local/share/applications/legion-toolkit.desktop"
rm -f "$HOME/.local/share/icons/LegionToolkit.svg"

# Update desktop database
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
fi

echo -e "${GREEN}Legion Toolkit has been uninstalled.${NC}"
EOF
chmod +x $OUTPUT_DIR/uninstall.sh

echo ""
echo -e "${GREEN}Build complete!${NC}"
echo "Output files:"
echo "  - AppImage: $OUTPUT_DIR/${APP_NAME}-${APP_VERSION}-${ARCH}.AppImage"
echo "  - Installer: $OUTPUT_DIR/install.sh"
echo "  - Uninstaller: $OUTPUT_DIR/uninstall.sh"
echo ""
echo "To install, run: ./release/install.sh"