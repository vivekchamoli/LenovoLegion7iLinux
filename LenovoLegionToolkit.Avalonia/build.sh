#!/bin/bash

# Legion Toolkit Linux Build Script
# This script builds the Legion Toolkit for Linux using .NET 8

set -e  # Exit on any error

echo "========================================="
echo "Legion Toolkit for Linux - Build Script"
echo "========================================="

# Check if .NET 8 is installed
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET 8 SDK is not installed. Please install it first:"
    echo "   https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
fi

# Verify .NET version
DOTNET_VERSION=$(dotnet --version)
echo "✅ Found .NET SDK version: $DOTNET_VERSION"

# Check if we're in the correct directory
if [ ! -f "LenovoLegionToolkit.Avalonia.csproj" ]; then
    echo "❌ Please run this script from the Avalonia project directory"
    exit 1
fi

echo ""
echo "🔧 Restoring packages..."
dotnet restore

echo ""
echo "🏗️  Building application..."
dotnet build --configuration Release --no-restore

echo ""
echo "📦 Publishing self-contained application..."

# Create output directory
mkdir -p ./publish

# Publish for Linux x64
echo "   📋 Building for Linux x64..."
dotnet publish \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output ./publish/linux-x64 \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link

# Publish for Linux ARM64 (for newer ARM-based laptops)
echo "   📋 Building for Linux ARM64..."
dotnet publish \
    --configuration Release \
    --runtime linux-arm64 \
    --self-contained true \
    --output ./publish/linux-arm64 \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=link

echo ""
echo "📁 Creating installation packages..."

# Create AppImage directory structure
mkdir -p ./publish/AppImage/LegionToolkit.AppDir/usr/bin
mkdir -p ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications
mkdir -p ./publish/AppImage/LegionToolkit.AppDir/usr/share/icons/hicolor/256x256/apps

# Copy executable
cp ./publish/linux-x64/LenovoLegionToolkit.Avalonia ./publish/AppImage/LegionToolkit.AppDir/usr/bin/

# Create desktop file
cat > ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications/legion-toolkit.desktop << EOF
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Exec=LenovoLegionToolkit.Avalonia
Icon=legion-toolkit
Comment=System management tool for Lenovo Legion laptops
Categories=System;Settings;HardwareSettings;
EOF

# Create AppRun script
cat > ./publish/AppImage/LegionToolkit.AppDir/AppRun << 'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
exec "${HERE}/usr/bin/LenovoLegionToolkit.Avalonia" "$@"
EOF
chmod +x ./publish/AppImage/LegionToolkit.AppDir/AppRun

# Create .DirIcon (same as desktop file but for AppImage)
cp ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications/legion-toolkit.desktop ./publish/AppImage/LegionToolkit.AppDir/

echo ""
echo "📄 Creating installation scripts..."

# Create Debian package directory
mkdir -p ./publish/deb/legion-toolkit/DEBIAN
mkdir -p ./publish/deb/legion-toolkit/usr/bin
mkdir -p ./publish/deb/legion-toolkit/usr/share/applications
mkdir -p ./publish/deb/legion-toolkit/usr/share/doc/legion-toolkit

# Create control file for Debian package
cat > ./publish/deb/legion-toolkit/DEBIAN/control << EOF
Package: legion-toolkit
Version: 3.0.0
Section: utils
Priority: optional
Architecture: amd64
Depends: libc6 (>= 2.31)
Maintainer: Legion Toolkit Team
Description: System management tool for Lenovo Legion laptops
 A comprehensive system management application for Lenovo Legion laptops
 running on Linux. Provides thermal monitoring, RGB control, battery
 management, and automation features.
EOF

# Copy files for Debian package
cp ./publish/linux-x64/LenovoLegionToolkit.Avalonia ./publish/deb/legion-toolkit/usr/bin/
cp ./publish/AppImage/LegionToolkit.AppDir/usr/share/applications/legion-toolkit.desktop ./publish/deb/legion-toolkit/usr/share/applications/

# Create install script
cat > ./publish/install.sh << 'EOF'
#!/bin/bash

echo "==========================================="
echo "Legion Toolkit for Linux - Installation"
echo "==========================================="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root (use sudo)"
    exit 1
fi

# Detect system architecture
ARCH=$(uname -m)
if [ "$ARCH" == "x86_64" ]; then
    SOURCE_DIR="linux-x64"
elif [ "$ARCH" == "aarch64" ]; then
    SOURCE_DIR="linux-arm64"
else
    echo "❌ Unsupported architecture: $ARCH"
    exit 1
fi

echo "✅ Detected architecture: $ARCH"
echo "📦 Installing Legion Toolkit..."

# Create installation directory
mkdir -p /opt/legion-toolkit
mkdir -p /usr/share/applications
mkdir -p /usr/local/bin

# Copy executable
cp ./$SOURCE_DIR/LenovoLegionToolkit.Avalonia /opt/legion-toolkit/
chmod +x /opt/legion-toolkit/LenovoLegionToolkit.Avalonia

# Create symlink
ln -sf /opt/legion-toolkit/LenovoLegionToolkit.Avalonia /usr/local/bin/legion-toolkit

# Install desktop file
cat > /usr/share/applications/legion-toolkit.desktop << 'DESKTOP_EOF'
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Exec=legion-toolkit
Icon=applications-system
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
UDEV_EOF

# Create legion group
groupadd -f legion

echo ""
echo "✅ Installation completed!"
echo ""
echo "📋 Setup Instructions:"
echo "   1. Add your user to the legion group:"
echo "      sudo usermod -a -G legion $USER"
echo "   2. Log out and log back in (or restart)"
echo "   3. Run: legion-toolkit"
echo ""
echo "📁 Installation locations:"
echo "   • Executable: /opt/legion-toolkit/"
echo "   • Desktop entry: /usr/share/applications/legion-toolkit.desktop"
echo "   • Command: legion-toolkit"
echo ""
EOF

chmod +x ./publish/install.sh

# Create uninstall script
cat > ./publish/uninstall.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "Legion Toolkit for Linux - Uninstallation"
echo "============================================="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root (use sudo)"
    exit 1
fi

echo "🗑️  Removing Legion Toolkit..."

# Remove files
rm -rf /opt/legion-toolkit
rm -f /usr/local/bin/legion-toolkit
rm -f /usr/share/applications/legion-toolkit.desktop
rm -f /etc/udev/rules.d/99-legion-toolkit.rules

# Reload udev rules
udevadm control --reload-rules

echo "✅ Legion Toolkit has been uninstalled"
echo "📋 Note: User configurations in ~/.config/legion-toolkit/ were not removed"
EOF

chmod +x ./publish/uninstall.sh

echo ""
echo "✅ Build completed successfully!"
echo ""
echo "📁 Output files:"
echo "   • Linux x64: ./publish/linux-x64/"
echo "   • Linux ARM64: ./publish/linux-arm64/"
echo "   • Installation script: ./publish/install.sh"
echo "   • Uninstallation script: ./publish/uninstall.sh"
echo "   • Debian package template: ./publish/deb/"
echo "   • AppImage template: ./publish/AppImage/"
echo ""
echo "📋 Next steps:"
echo "   1. Test the application: ./publish/linux-x64/LenovoLegionToolkit.Avalonia"
echo "   2. Install system-wide: sudo ./publish/install.sh"
echo "   3. Or create packages using the templates in ./publish/"
echo ""
echo "⚠️  Note: This application requires access to hardware interfaces."
echo "   Make sure to run the install script to set up proper permissions."