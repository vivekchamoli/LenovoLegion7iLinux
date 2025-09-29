#!/bin/bash
# Legion Toolkit Ubuntu Package Builder
# Builds .deb package and AppImage for Ubuntu distribution

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BUILD_DIR="$PROJECT_ROOT/build-ubuntu"
VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT_ROOT/LenovoLegionToolkit.Avalonia/LenovoLegionToolkit.Avalonia.csproj" || echo "3.0.0")

echo -e "${BLUE}╔════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║     Legion Toolkit Ubuntu Package Builder v$VERSION    ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════╝${NC}"

# Function to print section headers
print_section() {
    echo -e "\n${GREEN}▶ $1${NC}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
}

# Function to print error and exit
error_exit() {
    echo -e "${RED}✗ Error: $1${NC}" >&2
    exit 1
}

# Function to print success message
success() {
    echo -e "${GREEN}✓ $1${NC}"
}

# Check prerequisites
print_section "Checking Prerequisites"

# Check if running on Linux
if [[ "$OSTYPE" != "linux-gnu"* ]]; then
    error_exit "This script must be run on Linux"
fi

# Check for required tools
REQUIRED_TOOLS=("dotnet" "dpkg-deb" "fakeroot" "lintian")
for tool in "${REQUIRED_TOOLS[@]}"; do
    if ! command -v "$tool" &> /dev/null; then
        echo -e "${YELLOW}⚠ $tool is not installed${NC}"
        echo "Installing $tool..."
        case $tool in
            dotnet)
                wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
                chmod +x dotnet-install.sh
                ./dotnet-install.sh --channel 8.0
                rm dotnet-install.sh
                ;;
            *)
                sudo apt-get update
                sudo apt-get install -y "$tool"
                ;;
        esac
    else
        success "$tool is installed"
    fi
done

# Clean previous builds
print_section "Cleaning Previous Builds"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
success "Build directory prepared"

# Build the application
print_section "Building Legion Toolkit"
cd "$PROJECT_ROOT/LenovoLegionToolkit.Avalonia"

echo "Publishing for linux-x64..."
dotnet publish \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    -o "$BUILD_DIR/publish" || error_exit "Build failed"

success "Application built successfully"

# Create .deb package structure
print_section "Creating Debian Package"

DEB_DIR="$BUILD_DIR/legion-toolkit_${VERSION}_amd64"
mkdir -p "$DEB_DIR/DEBIAN"
mkdir -p "$DEB_DIR/usr/bin"
mkdir -p "$DEB_DIR/usr/share/applications"
mkdir -p "$DEB_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$DEB_DIR/usr/share/man/man1"
mkdir -p "$DEB_DIR/usr/share/doc/legion-toolkit"
mkdir -p "$DEB_DIR/etc/systemd/system"
mkdir -p "$DEB_DIR/etc/xdg/autostart"
mkdir -p "$DEB_DIR/usr/lib/legion-toolkit"

# Copy binary
cp "$BUILD_DIR/publish/LegionToolkit" "$DEB_DIR/usr/bin/legion-toolkit"
chmod +x "$DEB_DIR/usr/bin/legion-toolkit"

# Create wrapper script for GUI
cat > "$DEB_DIR/usr/bin/legion-toolkit-gui" << 'EOF'
#!/bin/bash
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export AVALONIA_SCREEN_SCALE_FACTOR_OVERRIDE=1
exec /usr/bin/legion-toolkit "$@"
EOF
chmod +x "$DEB_DIR/usr/bin/legion-toolkit-gui"

# Copy resources
cp -r "$PROJECT_ROOT/LenovoLegionToolkit.Avalonia/Assets"/* "$DEB_DIR/usr/share/icons/hicolor/256x256/apps/" 2>/dev/null || true
cp "$PROJECT_ROOT/publish/legion-toolkit.man" "$DEB_DIR/usr/share/man/man1/legion-toolkit.1" 2>/dev/null || true

# Create desktop file
cat > "$DEB_DIR/usr/share/applications/legion-toolkit.desktop" << EOF
[Desktop Entry]
Name=Legion Toolkit
Comment=Control Lenovo Legion laptop features
Exec=/usr/bin/legion-toolkit-gui
Icon=legion-toolkit
Terminal=false
Type=Application
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;laptop;power;battery;rgb;fan;thermal;
StartupNotify=true
StartupWMClass=LegionToolkit
Actions=PowerQuiet;PowerBalanced;PowerPerformance;

[Desktop Action PowerQuiet]
Name=Quiet Mode
Exec=/usr/bin/legion-toolkit power set quiet

[Desktop Action PowerBalanced]
Name=Balanced Mode
Exec=/usr/bin/legion-toolkit power set balanced

[Desktop Action PowerPerformance]
Name=Performance Mode
Exec=/usr/bin/legion-toolkit power set performance
EOF

# Create systemd service
cat > "$DEB_DIR/etc/systemd/system/legion-toolkit.service" << EOF
[Unit]
Description=Legion Toolkit Daemon
After=multi-user.target legion-laptop.service
Wants=legion-laptop.service

[Service]
Type=simple
ExecStart=/usr/bin/legion-toolkit daemon start
Restart=always
RestartSec=10
User=root
Environment="DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1"

[Install]
WantedBy=multi-user.target
EOF

# Create autostart file
cat > "$DEB_DIR/etc/xdg/autostart/legion-toolkit.desktop" << EOF
[Desktop Entry]
Name=Legion Toolkit System Tray
Comment=Legion Toolkit system tray application
Exec=/usr/bin/legion-toolkit-gui --tray
Icon=legion-toolkit
Terminal=false
Type=Application
Categories=System;
X-GNOME-Autostart-enabled=true
Hidden=false
NoDisplay=false
EOF

# Create control file (binary package format)
cat > "$DEB_DIR/DEBIAN/control" << EOF
Package: legion-toolkit
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Depends: dotnet-runtime-8.0, libx11-6, libxrandr2, libxi6, libnotify-bin, x11-utils, ddcutil, redshift
Recommends: legion-laptop-dkms
Suggests: nvidia-settings
Maintainer: Vivek Chamoli <vivekchamoli@github.com>
Description: Comprehensive control utility for Lenovo Legion laptops
 Legion Toolkit provides a graphical and command-line interface for
 controlling various hardware features of Lenovo Legion laptops on Linux.
 .
 Features:
  - Power mode control (Quiet, Balanced, Performance)
  - Battery management (charge limits, conservation mode)
  - Thermal monitoring and fan control
  - RGB keyboard lighting control
  - Display refresh rate and color management
  - Automation profiles and rules
  - System tray integration
  - Comprehensive CLI support
 .
 This package requires the legion-laptop kernel module for full functionality.
Homepage: https://github.com/vivekchamoli/LenovoLegion7i
EOF

# Create postinst script
cat > "$DEB_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

# Reload systemd
systemctl daemon-reload

# Enable service but don't start it
systemctl enable legion-toolkit.service 2>/dev/null || true

# Update icon cache
gtk-update-icon-cache -f /usr/share/icons/hicolor 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true

# Add user to required groups
if getent group video > /dev/null 2>&1; then
    for user in $(getent passwd | awk -F: '$3 >= 1000 && $3 < 60000 {print $1}'); do
        usermod -a -G video "$user" 2>/dev/null || true
    done
fi

# Check for kernel module
if ! lsmod | grep -q legion_laptop; then
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "⚠  IMPORTANT: Legion laptop kernel module not detected!"
    echo ""
    echo "For full functionality, install the legion-laptop module:"
    echo "  sudo apt install legion-laptop-dkms"
    echo ""
    echo "Or build from source:"
    echo "  https://github.com/johnfanv2/LenovoLegion5LinuxSupport"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
fi

echo ""
echo "✓ Legion Toolkit installed successfully!"
echo ""
echo "To start the GUI: legion-toolkit-gui"
echo "To use the CLI: legion-toolkit --help"
echo "To start the daemon: sudo systemctl start legion-toolkit"
echo ""

#DEBHELPER#
EOF
chmod 755 "$DEB_DIR/DEBIAN/postinst"

# Create prerm script
cat > "$DEB_DIR/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# Stop and disable service
systemctl stop legion-toolkit.service 2>/dev/null || true
systemctl disable legion-toolkit.service 2>/dev/null || true

#DEBHELPER#
EOF
chmod 755 "$DEB_DIR/DEBIAN/prerm"

# Create postrm script
cat > "$DEB_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/bash
set -e

# Reload systemd
systemctl daemon-reload 2>/dev/null || true

# Update caches
gtk-update-icon-cache -f /usr/share/icons/hicolor 2>/dev/null || true
update-desktop-database /usr/share/applications 2>/dev/null || true

#DEBHELPER#
EOF
chmod 755 "$DEB_DIR/DEBIAN/postrm"

# Build the package
print_section "Building .deb Package"
cd "$BUILD_DIR"
fakeroot dpkg-deb --build "$DEB_DIR" || error_exit "Package build failed"

# Run lintian check
echo "Running package validation..."
lintian "legion-toolkit_${VERSION}_amd64.deb" || true

success "Debian package created: $BUILD_DIR/legion-toolkit_${VERSION}_amd64.deb"

# Create AppImage (optional)
print_section "Creating AppImage (Optional)"

if command -v appimagetool &> /dev/null; then
    APPIMAGE_DIR="$BUILD_DIR/LegionToolkit.AppDir"
    mkdir -p "$APPIMAGE_DIR/usr/bin"
    mkdir -p "$APPIMAGE_DIR/usr/share/applications"
    mkdir -p "$APPIMAGE_DIR/usr/share/icons/hicolor/256x256/apps"

    # Copy files for AppImage
    cp "$BUILD_DIR/publish/LegionToolkit" "$APPIMAGE_DIR/usr/bin/"
    cp "$DEB_DIR/usr/share/applications/legion-toolkit.desktop" "$APPIMAGE_DIR/"
    cp "$DEB_DIR/usr/share/applications/legion-toolkit.desktop" "$APPIMAGE_DIR/usr/share/applications/"
    cp -r "$DEB_DIR/usr/share/icons/"* "$APPIMAGE_DIR/usr/share/icons/" 2>/dev/null || true

    # Create AppRun
    cat > "$APPIMAGE_DIR/AppRun" << 'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/LegionToolkit" "$@"
EOF
    chmod +x "$APPIMAGE_DIR/AppRun"

    # Build AppImage
    ARCH=x86_64 appimagetool "$APPIMAGE_DIR" "$BUILD_DIR/LegionToolkit-${VERSION}-x86_64.AppImage"
    success "AppImage created: $BUILD_DIR/LegionToolkit-${VERSION}-x86_64.AppImage"
else
    echo -e "${YELLOW}⚠ appimagetool not found. Skipping AppImage creation.${NC}"
fi

# Create installation archive
print_section "Creating Installation Archive"

ARCHIVE_DIR="$BUILD_DIR/legion-toolkit-ubuntu-${VERSION}"
mkdir -p "$ARCHIVE_DIR"

# Copy package files
cp "$BUILD_DIR/legion-toolkit_${VERSION}_amd64.deb" "$ARCHIVE_DIR/"
cp "$BUILD_DIR/LegionToolkit-${VERSION}-x86_64.AppImage" "$ARCHIVE_DIR/" 2>/dev/null || true

# Create installation script
cat > "$ARCHIVE_DIR/install.sh" << 'EOF'
#!/bin/bash
# Legion Toolkit Ubuntu Installer

set -e

echo "════════════════════════════════════════════"
echo "     Legion Toolkit Ubuntu Installer"
echo "════════════════════════════════════════════"
echo ""

# Check if running as root
if [ "$EUID" -eq 0 ]; then
   echo "Please do not run this installer as root/sudo"
   exit 1
fi

# Install .deb package
echo "Installing Legion Toolkit..."
sudo dpkg -i legion-toolkit_*.deb || {
    echo "Fixing dependencies..."
    sudo apt-get install -f -y
    sudo dpkg -i legion-toolkit_*.deb
}

echo ""
echo "✓ Installation complete!"
echo ""
echo "Usage:"
echo "  GUI: legion-toolkit-gui"
echo "  CLI: legion-toolkit --help"
echo "  Start daemon: sudo systemctl start legion-toolkit"
echo ""
echo "For AppImage (no installation needed):"
echo "  chmod +x LegionToolkit-*.AppImage"
echo "  ./LegionToolkit-*.AppImage"
EOF
chmod +x "$ARCHIVE_DIR/install.sh"

# Create README
cat > "$ARCHIVE_DIR/README.md" << EOF
# Legion Toolkit for Ubuntu

Version: $VERSION

## Installation Methods

### Method 1: Using the installer script (Recommended)
\`\`\`bash
./install.sh
\`\`\`

### Method 2: Manual .deb installation
\`\`\`bash
sudo dpkg -i legion-toolkit_${VERSION}_amd64.deb
sudo apt-get install -f  # Fix any dependency issues
\`\`\`

### Method 3: AppImage (No installation)
\`\`\`bash
chmod +x LegionToolkit-${VERSION}-x86_64.AppImage
./LegionToolkit-${VERSION}-x86_64.AppImage
\`\`\`

## Requirements

- Ubuntu 20.04 or later (or Debian-based distribution)
- Lenovo Legion laptop
- legion-laptop kernel module (recommended)

## Features

- Power mode management (Quiet, Balanced, Performance, Custom)
- Battery conservation and rapid charge control
- RGB keyboard control
- Thermal monitoring and fan control
- GPU management
- Display configuration
- System tray integration
- Comprehensive CLI

## Post-Installation

1. Start the GUI:
   \`\`\`bash
   legion-toolkit-gui
   \`\`\`

2. Enable auto-start daemon (optional):
   \`\`\`bash
   sudo systemctl enable --now legion-toolkit
   \`\`\`

3. Add to startup applications (GUI will auto-start on login)

## Troubleshooting

If you encounter permission issues:
\`\`\`bash
sudo usermod -a -G video $USER
# Log out and back in
\`\`\`

For kernel module issues:
\`\`\`bash
sudo apt install legion-laptop-dkms
# Or build from: https://github.com/johnfanv2/LenovoLegion5LinuxSupport
\`\`\`

## Uninstallation

\`\`\`bash
sudo apt remove legion-toolkit
\`\`\`

## Support

Report issues at: https://github.com/LenovoLegion7iToolkit
EOF

# Create archive
cd "$BUILD_DIR"
tar -czf "legion-toolkit-ubuntu-${VERSION}.tar.gz" "legion-toolkit-ubuntu-${VERSION}/"
success "Installation archive created: $BUILD_DIR/legion-toolkit-ubuntu-${VERSION}.tar.gz"

# Summary
print_section "Build Complete!"

echo -e "${GREEN}✓ Build successful!${NC}"
echo ""
echo "Generated packages:"
echo "  • Debian Package: ${BLUE}$BUILD_DIR/legion-toolkit_${VERSION}_amd64.deb${NC}"
[ -f "$BUILD_DIR/LegionToolkit-${VERSION}-x86_64.AppImage" ] && \
    echo "  • AppImage: ${BLUE}$BUILD_DIR/LegionToolkit-${VERSION}-x86_64.AppImage${NC}"
echo "  • Archive: ${BLUE}$BUILD_DIR/legion-toolkit-ubuntu-${VERSION}.tar.gz${NC}"
echo ""
echo "To install locally:"
echo "  ${YELLOW}sudo dpkg -i $BUILD_DIR/legion-toolkit_${VERSION}_amd64.deb${NC}"
echo ""
echo "To distribute, share:"
echo "  ${YELLOW}$BUILD_DIR/legion-toolkit-ubuntu-${VERSION}.tar.gz${NC}"