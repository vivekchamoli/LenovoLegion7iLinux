#!/bin/bash

# Legion Toolkit for Linux - Production Build Script
# Creates standalone, deployable binaries for Linux systems

set -e  # Exit on any error

echo "============================================="
echo "🚀 Legion Toolkit for Linux - Build Script"
echo "============================================="

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Check if .NET 8 is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}❌ .NET 8 SDK is not installed. Please install it first:${NC}"
    echo "   Ubuntu/Debian: sudo apt install dotnet-sdk-8.0"
    echo "   Fedora: sudo dnf install dotnet-sdk-8.0"
    echo "   Arch: sudo pacman -S dotnet-sdk"
    echo "   Or download from: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# Verify .NET version
DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✅ Found .NET SDK version: $DOTNET_VERSION${NC}"

# Check if we're in the correct directory
if [ ! -f "LenovoLegionToolkit.Avalonia.csproj" ]; then
    echo -e "${RED}❌ Please run this script from the Avalonia project directory${NC}"
    echo "   Expected: LenovoLegion7iToolkit/LenovoLegionToolkit.Avalonia/"
    exit 1
fi

# Clean previous builds
echo ""
echo -e "${YELLOW}🧹 Cleaning previous builds...${NC}"
rm -rf ./bin ./obj ./publish

echo ""
echo -e "${BLUE}🔧 Restoring packages...${NC}"
dotnet restore --verbosity quiet

echo ""
echo -e "${BLUE}🏗️  Building application...${NC}"
dotnet build --configuration Release --no-restore --verbosity quiet

# Create output directory
mkdir -p ./publish

echo ""
echo -e "${YELLOW}📦 Creating production builds...${NC}"

# Build for Linux x64
echo -e "   ${BLUE}📋 Building for Linux x64...${NC}"
dotnet publish \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output ./publish/linux-x64 \
    --verbosity quiet \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    -p:DebugType=None \
    -p:DebugSymbols=false

# Build for Linux ARM64 (for newer ARM laptops)
echo -e "   ${BLUE}📋 Building for Linux ARM64...${NC}"
dotnet publish \
    --configuration Release \
    --runtime linux-arm64 \
    --self-contained true \
    --output ./publish/linux-arm64 \
    --verbosity quiet \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link \
    -p:DebugType=None \
    -p:DebugSymbols=false

# Make executables executable
chmod +x ./publish/linux-x64/LegionToolkit
chmod +x ./publish/linux-arm64/LegionToolkit

echo ""
echo -e "${YELLOW}📁 Creating installation packages...${NC}"

# Create AppImage directory structure
echo -e "   ${BLUE}📦 Creating AppImage template...${NC}"
mkdir -p ./publish/AppImage/LegionToolkit.AppDir/usr/bin
mkdir -p ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications
mkdir -p ./publish/AppImage/LegionToolkit.AppDir/usr/share/icons/hicolor/256x256/apps

# Copy executable to AppImage
cp ./publish/linux-x64/LegionToolkit ./publish/AppImage/LegionToolkit.AppDir/usr/bin/

# Create desktop file for AppImage
cat > ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications/legion-toolkit.desktop << 'EOF'
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Exec=LegionToolkit
Icon=legion-toolkit
Comment=System management tool for Lenovo Legion laptops
Categories=System;Settings;HardwareSettings;
Terminal=false
EOF

# Create AppRun script
cat > ./publish/AppImage/LegionToolkit.AppDir/AppRun << 'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/LegionToolkit" "$@"
EOF
chmod +x ./publish/AppImage/LegionToolkit.AppDir/AppRun

# Copy desktop file to root for AppImage
cp ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications/legion-toolkit.desktop ./publish/AppImage/LegionToolkit.AppDir/

# Create Debian package structure
echo -e "   ${BLUE}📦 Creating Debian package template...${NC}"
mkdir -p ./publish/deb/legion-toolkit/DEBIAN
mkdir -p ./publish/deb/legion-toolkit/usr/bin
mkdir -p ./publish/deb/legion-toolkit/usr/share/applications
mkdir -p ./publish/deb/legion-toolkit/usr/share/doc/legion-toolkit
mkdir -p ./publish/deb/legion-toolkit/etc/udev/rules.d

# Create control file for Debian package
cat > ./publish/deb/legion-toolkit/DEBIAN/control << 'EOF'
Package: legion-toolkit
Version: 3.0.0
Section: utils
Priority: optional
Architecture: amd64
Depends: libc6 (>= 2.31)
Maintainer: Legion Toolkit Community <community@legiontoolkit.com>
Description: System management tool for Lenovo Legion laptops
 A comprehensive system management application for Lenovo Legion laptops
 running on Linux. Provides thermal monitoring, RGB control, battery
 management, and automation features.
 .
 Features include:
  - Real-time thermal monitoring and fan control
  - 4-zone RGB keyboard lighting with effects
  - Battery conservation and charging management
  - Automation rules and macro sequences
  - Performance mode switching
Homepage: https://github.com/legion-toolkit/legion-toolkit-linux
EOF

# Create postinst script for Debian package
cat > ./publish/deb/legion-toolkit/DEBIAN/postinst << 'EOF'
#!/bin/bash
set -e

# Create legion group
groupadd -f legion

# Set up udev rules
udevadm control --reload-rules
udevadm trigger

echo ""
echo "Legion Toolkit has been installed successfully!"
echo ""
echo "Setup Instructions:"
echo "1. Add your user to the legion group:"
echo "   sudo usermod -a -G legion \$USER"
echo "2. Log out and log back in (or restart)"
echo "3. Launch: legion-toolkit"
echo ""
EOF
chmod +x ./publish/deb/legion-toolkit/DEBIAN/postinst

# Create prerm script for Debian package
cat > ./publish/deb/legion-toolkit/DEBIAN/prerm << 'EOF'
#!/bin/bash
set -e

# Stop any running instances
pkill -f LegionToolkit || true

echo "Legion Toolkit has been removed."
echo "User configurations in ~/.config/legion-toolkit/ were preserved."
EOF
chmod +x ./publish/deb/legion-toolkit/DEBIAN/prerm

# Copy files for Debian package
cp ./publish/linux-x64/LegionToolkit ./publish/deb/legion-toolkit/usr/bin/
cp ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications/legion-toolkit.desktop ./publish/deb/legion-toolkit/usr/share/applications/

# Create udev rules for hardware access
cat > ./publish/deb/legion-toolkit/etc/udev/rules.d/99-legion-toolkit.rules << 'EOF'
# Legion Toolkit hardware access rules
SUBSYSTEM=="hwmon", GROUP="legion", MODE="0664"
SUBSYSTEM=="leds", GROUP="legion", MODE="0664"
KERNEL=="legion_laptop", GROUP="legion", MODE="0664"
SUBSYSTEM=="power_supply", GROUP="legion", MODE="0664"
ACTION=="add", KERNEL=="legion_laptop", RUN+="/bin/chmod 664 /sys/kernel/legion_laptop/*"
EOF

# Create copyright file
cat > ./publish/deb/legion-toolkit/usr/share/doc/legion-toolkit/copyright << 'EOF'
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: legion-toolkit
Source: https://github.com/legion-toolkit/legion-toolkit-linux

Files: *
Copyright: 2024 Legion Toolkit Community
License: MIT
 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:
 .
 The above copyright notice and this permission notice shall be included in all
 copies or substantial portions of the Software.
 .
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 SOFTWARE.
EOF

echo ""
echo -e "${YELLOW}📄 Creating installation scripts...${NC}"

# Create universal install script
cat > ./publish/install.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "🚀 Legion Toolkit for Linux - Installation"
echo "============================================="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root (use sudo)"
    exit 1
fi

# Detect system architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)
        SOURCE_DIR="linux-x64"
        ;;
    aarch64)
        SOURCE_DIR="linux-arm64"
        ;;
    *)
        echo "❌ Unsupported architecture: $ARCH"
        echo "   Supported: x86_64, aarch64"
        exit 1
        ;;
esac

echo "✅ Detected architecture: $ARCH"

# Check if source directory exists
if [ ! -d "./$SOURCE_DIR" ]; then
    echo "❌ Build directory ./$SOURCE_DIR not found"
    echo "   Please run this script from the publish directory"
    exit 1
fi

echo "📦 Installing Legion Toolkit..."

# Create installation directory
mkdir -p /opt/legion-toolkit
mkdir -p /usr/share/applications
mkdir -p /usr/local/bin
mkdir -p /etc/udev/rules.d

# Copy executable
cp ./$SOURCE_DIR/LegionToolkit /opt/legion-toolkit/
chmod +x /opt/legion-toolkit/LegionToolkit

# Create symlink
ln -sf /opt/legion-toolkit/LegionToolkit /usr/local/bin/legion-toolkit

# Install desktop file
cat > /usr/share/applications/legion-toolkit.desktop << 'DESKTOP_EOF'
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Exec=legion-toolkit
Icon=computer
Comment=System management tool for Lenovo Legion laptops
Categories=System;Settings;HardwareSettings;
Terminal=false
DESKTOP_EOF

# Set up udev rules for hardware access
cat > /etc/udev/rules.d/99-legion-toolkit.rules << 'UDEV_EOF'
# Legion Toolkit hardware access rules
SUBSYSTEM=="hwmon", GROUP="legion", MODE="0664"
SUBSYSTEM=="leds", GROUP="legion", MODE="0664"
KERNEL=="legion_laptop", GROUP="legion", MODE="0664"
SUBSYSTEM=="power_supply", GROUP="legion", MODE="0664"
ACTION=="add", KERNEL=="legion_laptop", RUN+="/bin/chmod 664 /sys/kernel/legion_laptop/*"
UDEV_EOF

# Create legion group
groupadd -f legion

# Reload udev rules
udevadm control --reload-rules
udevadm trigger

echo ""
echo "✅ Installation completed successfully!"
echo ""
echo "📋 Setup Instructions:"
echo "   1. Add your user to the legion group:"
echo "      sudo usermod -a -G legion \$USER"
echo "   2. Log out and log back in (or restart)"
echo "   3. Launch: legion-toolkit"
echo ""
echo "📁 Installation locations:"
echo "   • Executable: /opt/legion-toolkit/LegionToolkit"
echo "   • Desktop entry: /usr/share/applications/legion-toolkit.desktop"
echo "   • Command: legion-toolkit"
echo "   • Udev rules: /etc/udev/rules.d/99-legion-toolkit.rules"
echo ""
echo "🔧 Hardware Access:"
echo "   Legion kernel module, hwmon sensors, RGB LEDs, and battery"
echo "   management require the legion group membership to function."
echo ""
EOF

chmod +x ./publish/install.sh

# Create uninstall script
cat > ./publish/uninstall.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "🗑️  Legion Toolkit for Linux - Uninstall"
echo "============================================="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root (use sudo)"
    exit 1
fi

echo "🗑️  Removing Legion Toolkit..."

# Stop any running instances
echo "   Stopping running instances..."
pkill -f LegionToolkit || true

# Remove files
echo "   Removing application files..."
rm -rf /opt/legion-toolkit
rm -f /usr/local/bin/legion-toolkit
rm -f /usr/share/applications/legion-toolkit.desktop
rm -f /etc/udev/rules.d/99-legion-toolkit.rules

# Reload udev rules
udevadm control --reload-rules

echo ""
echo "✅ Legion Toolkit has been uninstalled successfully"
echo ""
echo "📋 Note: User configurations in ~/.config/legion-toolkit/ were preserved"
echo "   To remove user data: rm -rf ~/.config/legion-toolkit/"
echo ""
echo "🔧 The 'legion' group was not removed. To remove it:"
echo "   sudo groupdel legion"
echo ""
EOF

chmod +x ./publish/uninstall.sh

# Create quick test script
cat > ./publish/test.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "🧪 Legion Toolkit - Quick Test"
echo "============================================="

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64) BINARY="./linux-x64/LegionToolkit" ;;
    aarch64) BINARY="./linux-arm64/LegionToolkit" ;;
    *) echo "❌ Unsupported architecture: $ARCH"; exit 1 ;;
esac

if [ ! -f "$BINARY" ]; then
    echo "❌ Binary not found: $BINARY"
    echo "   Please run build-linux.sh first"
    exit 1
fi

echo "✅ Found binary: $BINARY"
echo "🚀 Starting Legion Toolkit..."
echo ""
echo "Note: Some features require hardware access permissions."
echo "      For full functionality, run the installer: sudo ./install.sh"
echo ""

# Run the application
$BINARY
EOF

chmod +x ./publish/test.sh

echo ""
echo -e "${GREEN}✅ Build completed successfully!${NC}"
echo ""
echo -e "${YELLOW}📁 Output files:${NC}"
echo "   • Linux x64 binary: ./publish/linux-x64/LegionToolkit"
echo "   • Linux ARM64 binary: ./publish/linux-arm64/LegionToolkit"
echo "   • Installation script: ./publish/install.sh"
echo "   • Uninstallation script: ./publish/uninstall.sh"
echo "   • Quick test script: ./publish/test.sh"
echo "   • Debian package template: ./publish/deb/"
echo "   • AppImage template: ./publish/AppImage/"
echo ""
echo -e "${BLUE}📋 Next steps:${NC}"
echo "   1. Quick test: cd publish && ./test.sh"
echo "   2. System install: cd publish && sudo ./install.sh"
echo "   3. Create packages using templates in ./publish/"
echo ""
echo -e "${YELLOW}⚠️  Important Notes:${NC}"
echo "   • Hardware access requires installation (udev rules)"
echo "   • Add user to 'legion' group after installation"
echo "   • Some features need Legion kernel module"
echo ""
echo -e "${GREEN}🎉 Legion Toolkit for Linux is ready for deployment!${NC}"