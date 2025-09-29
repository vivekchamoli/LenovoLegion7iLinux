#!/bin/bash

# Legion Toolkit for Linux - Installation Script
set -e

INSTALL_DIR="/opt/legion-toolkit"
BIN_DIR="/usr/local/bin"
DESKTOP_DIR="/usr/share/applications"

echo "============================================="
echo "🚀 Installing Legion Toolkit for Linux"
echo "============================================="

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "❌ Please run as root (use sudo)"
    exit 1
fi

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64)
        BUILD_ARCH="linux-x64"
        ;;
    aarch64|arm64)
        BUILD_ARCH="linux-arm64"
        ;;
    *)
        echo "❌ Unsupported architecture: $ARCH"
        exit 1
        ;;
esac

echo "✅ Detected architecture: $ARCH (using $BUILD_ARCH build)"

# Check if build exists
if [ ! -f "publish/$BUILD_ARCH/LegionToolkit" ]; then
    echo "❌ Build not found for $BUILD_ARCH"
    echo "   Please run the build script first"
    exit 1
fi

# Create installation directory
echo "📁 Creating installation directory..."
mkdir -p "$INSTALL_DIR"

# Copy files
echo "📋 Installing application files..."
cp -r "publish/$BUILD_ARCH/"* "$INSTALL_DIR/"

# Make executable
chmod +x "$INSTALL_DIR/LegionToolkit"

# Create symlink in PATH
echo "🔗 Creating system link..."
ln -sf "$INSTALL_DIR/LegionToolkit" "$BIN_DIR/legion-toolkit"

# Create desktop entry
echo "🖥️ Creating desktop entry..."
cat > "$DESKTOP_DIR/legion-toolkit.desktop" << EOF
[Desktop Entry]
Name=Legion Toolkit
Comment=Comprehensive system management for Lenovo Legion laptops
Exec=legion-toolkit
Icon=computer
Terminal=false
Type=Application
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;hardware;thermal;rgb;battery;
StartupNotify=true
EOF

# Create legion group if it doesn't exist
if ! getent group legion >/dev/null 2>&1; then
    echo "👥 Creating 'legion' group..."
    groupadd legion
fi

# Check for Legion kernel module
echo "🔍 Checking system compatibility..."
if [ -d "/sys/kernel/legion_laptop" ]; then
    echo "✅ Legion kernel module detected"
elif modprobe legion-laptop 2>/dev/null; then
    echo "✅ Legion kernel module loaded"
else
    echo "⚠️ Legion kernel module not found"
    echo "   Some features may be limited without the legion-laptop module"
fi

# Set permissions for hardware access
echo "🔐 Setting up hardware access permissions..."

# hwmon access
if [ -d "/sys/class/hwmon" ]; then
    echo "SUBSYSTEM==\"hwmon\", GROUP=\"legion\", MODE=\"0664\"" > /etc/udev/rules.d/99-legion-hwmon.rules
fi

# power_supply access
if [ -d "/sys/class/power_supply" ]; then
    echo "SUBSYSTEM==\"power_supply\", GROUP=\"legion\", MODE=\"0664\"" >> /etc/udev/rules.d/99-legion-hwmon.rules
fi

# LED access
if [ -d "/sys/class/leds" ]; then
    echo "SUBSYSTEM==\"leds\", KERNEL==\"*legion*\", GROUP=\"legion\", MODE=\"0664\"" >> /etc/udev/rules.d/99-legion-hwmon.rules
fi

# Reload udev rules
udevadm control --reload-rules
udevadm trigger

echo ""
echo "✅ Installation completed successfully!"
echo ""
echo "📋 Next steps:"
echo "1. Add your user to the legion group:"
echo "   sudo usermod -a -G legion \$USER"
echo ""
echo "2. Log out and log back in (or restart)"
echo ""
echo "3. Launch the application:"
echo "   legion-toolkit"
echo ""
echo "🎯 Features available:"
echo "• Thermal monitoring and fan control"
echo "• RGB keyboard lighting (4-zone)"
echo "• Battery management and conservation"
echo "• Automation rules and macros"
echo "• Performance mode switching"
echo ""
echo "📁 Installation location: $INSTALL_DIR"
echo "🔗 Command: legion-toolkit"