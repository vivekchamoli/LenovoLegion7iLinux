#!/bin/bash

# Legion Toolkit for Linux - Complete Build and Package Script
# Creates standalone binaries, packages, and installation files for Linux systems

set -e  # Exit on any error

echo "============================================="
echo "🚀 Legion Toolkit for Linux - Complete Build"
echo "============================================="

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Version information
VERSION="3.0.0"
PACKAGE_NAME="legion-toolkit"
MAINTAINER="Vivek Chamoli <vivekchamoli@outlook.com>"
HOMEPAGE="https://github.com/vivekchamoli/LenovoLegion7iLinux"

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
echo -e "${PURPLE}📁 Creating installation packages...${NC}"

# Create Debian package structure
echo -e "   ${CYAN}📦 Creating Debian package...${NC}"
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/DEBIAN
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/bin
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/applications
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/doc/${PACKAGE_NAME}
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/icons/hicolor/256x256/apps
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/etc/udev/rules.d
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/etc/systemd/system
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/man/man1

# Create control file for Debian package
cat > ./publish/debian-package/${PACKAGE_NAME}/DEBIAN/control << EOF
Package: ${PACKAGE_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Depends: libc6 (>= 2.31), libicu70 | libicu72, libssl3, libx11-6, libfontconfig1, libharfbuzz0b, libfreetype6
Recommends: acpi-support, udev
Suggests: linux-modules-extra
Maintainer: ${MAINTAINER}
Description: System management tool for Lenovo Legion laptops
 A comprehensive system management application for Lenovo Legion laptops
 running on Linux. Provides thermal monitoring, RGB control, battery
 management, and automation features.
 .
 Features include:
  - Real-time thermal monitoring and fan control
  - 4-zone and per-key RGB keyboard lighting with effects
  - Battery conservation and charging management
  - Automation rules and macro sequences
  - Performance mode switching (Quiet, Balanced, Performance, Custom)
  - Hybrid graphics mode control
  - Display refresh rate and overdrive control
  - Command-line interface for scripting
  - System tray integration
Homepage: ${HOMEPAGE}
EOF

# Create postinst script for Debian package
cat > ./publish/debian-package/${PACKAGE_NAME}/DEBIAN/postinst << 'EOF'
#!/bin/bash
set -e

echo "Configuring Legion Toolkit..."

# Create legion group if it doesn't exist
if ! getent group legion > /dev/null 2>&1; then
    groupadd legion
    echo "Created 'legion' group"
fi

# Set up udev rules
if [ -x "$(command -v udevadm)" ]; then
    udevadm control --reload-rules 2>/dev/null || true
    udevadm trigger 2>/dev/null || true
fi

# Try to load legion-laptop module if available
if [ -x "$(command -v modprobe)" ]; then
    modprobe legion-laptop 2>/dev/null || echo "Note: legion-laptop module not available. Some features may be limited."
fi

# Set up systemd service if systemd is available
if [ -d "/etc/systemd/system" ] && [ -x "$(command -v systemctl)" ]; then
    systemctl daemon-reload 2>/dev/null || true
fi

# Update desktop database and icon cache for GUI visibility
if [ -x "$(command -v update-desktop-database)" ]; then
    update-desktop-database /usr/share/applications 2>/dev/null || true
fi

if [ -x "$(command -v gtk-update-icon-cache)" ]; then
    gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
fi

# Update MIME database
if [ -x "$(command -v update-mime-database)" ]; then
    update-mime-database /usr/share/mime 2>/dev/null || true
fi

echo ""
echo "Legion Toolkit has been installed successfully!"
echo ""
echo "🔧 Setup Instructions:"
echo "1. Add your user to the legion group:"
echo "   sudo usermod -a -G legion \$USER"
echo ""
echo "2. Log out and log back in (or restart) for group changes to take effect"
echo ""
echo "3. Launch the application:"
echo "   - GUI: Search for 'Legion Toolkit' in your applications menu"
echo "   - Or run: LegionToolkit"
echo "   - CLI: legion-toolkit --help"
echo ""
echo "📋 Hardware Support:"
echo "• For full functionality, ensure legion-laptop kernel module is loaded"
echo "• Check module status: lsmod | grep legion"
echo "• Install module: sudo modprobe legion-laptop"
echo ""
echo "🐛 Troubleshooting GUI Issues:"
echo "• If GUI doesn't appear, try: /usr/bin/LegionToolkit"
echo "• Check dependencies: ldd /usr/bin/LegionToolkit"
echo "• Verify desktop file: desktop-file-validate /usr/share/applications/legion-toolkit.desktop"
echo ""
echo "📖 Documentation: ${HOMEPAGE}"
echo ""
EOF
chmod +x ./publish/debian-package/${PACKAGE_NAME}/DEBIAN/postinst

# Create prerm script for Debian package
cat > ./publish/debian-package/${PACKAGE_NAME}/DEBIAN/prerm << 'EOF'
#!/bin/bash
set -e

echo "Removing Legion Toolkit..."

# Stop any running instances
pkill -f LegionToolkit 2>/dev/null || true

# Stop systemd service if it exists
if [ -x "$(command -v systemctl)" ]; then
    systemctl stop legion-toolkit 2>/dev/null || true
    systemctl disable legion-toolkit 2>/dev/null || true
fi

echo "Legion Toolkit has been removed."
echo "User configurations in ~/.config/legion-toolkit/ were preserved."
echo "To remove user data: rm -rf ~/.config/legion-toolkit/"
EOF
chmod +x ./publish/debian-package/${PACKAGE_NAME}/DEBIAN/prerm

# Copy files for Debian package
cp ./publish/linux-x64/LegionToolkit ./publish/debian-package/${PACKAGE_NAME}/usr/bin/
chmod +x ./publish/debian-package/${PACKAGE_NAME}/usr/bin/LegionToolkit

# Create symbolic link for command line access
cd ./publish/debian-package/${PACKAGE_NAME}/usr/bin/
ln -sf LegionToolkit legion-toolkit
cd ../../../../..

# Copy icon files if they exist
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/icons/hicolor/256x256/apps/
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/icons/hicolor/scalable/apps/
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/share/pixmaps/

if [ -d "./Assets" ]; then
    cp ./Assets/*.png ./publish/debian-package/${PACKAGE_NAME}/usr/share/icons/hicolor/256x256/apps/${PACKAGE_NAME}.png 2>/dev/null || true
    cp ./Assets/*.svg ./publish/debian-package/${PACKAGE_NAME}/usr/share/icons/hicolor/scalable/apps/${PACKAGE_NAME}.svg 2>/dev/null || true
    cp ./Assets/*.png ./publish/debian-package/${PACKAGE_NAME}/usr/share/pixmaps/${PACKAGE_NAME}.png 2>/dev/null || true
fi

# Create fallback icon if no assets found
if [ ! -f "./publish/debian-package/${PACKAGE_NAME}/usr/share/icons/hicolor/256x256/apps/${PACKAGE_NAME}.png" ]; then
    # Create a simple fallback icon (you could replace this with a proper icon)
    echo "Creating fallback icon..."
fi

# Create diagnostic launcher script
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/bin/legion-toolkit-debug << 'EOF'
#!/bin/bash
# Legion Toolkit Diagnostic Launcher

echo "🔍 Legion Toolkit Diagnostic Information"
echo "========================================"

echo "📍 Binary Location:"
ls -la /usr/bin/LegionToolkit 2>/dev/null || echo "❌ Binary not found at /usr/bin/LegionToolkit"

echo ""
echo "🔗 Dependencies Check:"
if command -v ldd >/dev/null 2>&1; then
    MISSING=$(ldd /usr/bin/LegionToolkit 2>/dev/null | grep "not found")
    if [ -z "$MISSING" ]; then
        echo "✅ All dependencies satisfied"
    else
        echo "❌ Missing dependencies:"
        echo "$MISSING"
    fi
else
    echo "⚠️  ldd not available"
fi

echo ""
echo "🖥️  Display Environment:"
echo "DISPLAY: ${DISPLAY:-'Not set'}"
echo "WAYLAND_DISPLAY: ${WAYLAND_DISPLAY:-'Not set'}"
echo "XDG_SESSION_TYPE: ${XDG_SESSION_TYPE:-'Not set'}"

echo ""
echo "👤 User Permissions:"
echo "Current user: $(whoami)"
echo "Groups: $(groups)"
if groups | grep -q legion; then
    echo "✅ User is in legion group"
else
    echo "❌ User NOT in legion group (run: sudo usermod -a -G legion $USER)"
fi

echo ""
echo "📁 Desktop Integration:"
if [ -f "/usr/share/applications/legion-toolkit.desktop" ]; then
    echo "✅ Desktop file exists"
    if command -v desktop-file-validate >/dev/null 2>&1; then
        if desktop-file-validate /usr/share/applications/legion-toolkit.desktop 2>/dev/null; then
            echo "✅ Desktop file is valid"
        else
            echo "❌ Desktop file validation failed"
        fi
    fi
else
    echo "❌ Desktop file missing"
fi

echo ""
echo "🚀 Attempting to launch Legion Toolkit..."
echo "If the GUI doesn't appear, check the output below for errors:"
echo "------------------------------------------------------------"

# Try to launch with error output
DISPLAY=${DISPLAY:-:0} /usr/bin/LegionToolkit "$@" 2>&1
EOF

chmod +x ./publish/debian-package/${PACKAGE_NAME}/usr/bin/legion-toolkit-debug

# Create desktop file with correct paths
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/share/applications/${PACKAGE_NAME}.desktop << EOF
[Desktop Entry]
Type=Application
Name=Legion Toolkit
GenericName=Legion Laptop Management
Comment=System management tool for Lenovo Legion laptops
Exec=/usr/bin/LegionToolkit
Icon=${PACKAGE_NAME}
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;laptop;thermal;rgb;battery;performance;
Terminal=false
StartupNotify=true
StartupWMClass=LegionToolkit
TryExec=/usr/bin/LegionToolkit
Actions=PowerQuiet;PowerBalanced;PowerPerformance;BatteryConservation;

[Desktop Action PowerQuiet]
Name=Set Quiet Mode
Exec=/usr/bin/LegionToolkit power set quiet
Icon=${PACKAGE_NAME}

[Desktop Action PowerBalanced]
Name=Set Balanced Mode
Exec=/usr/bin/LegionToolkit power set balanced
Icon=${PACKAGE_NAME}

[Desktop Action PowerPerformance]
Name=Set Performance Mode
Exec=/usr/bin/LegionToolkit power set performance
Icon=${PACKAGE_NAME}

[Desktop Action BatteryConservation]
Name=Toggle Battery Conservation
Exec=/usr/bin/LegionToolkit battery conservation toggle
Icon=${PACKAGE_NAME}
EOF

# Create udev rules for hardware access
cat > ./publish/debian-package/${PACKAGE_NAME}/etc/udev/rules.d/99-${PACKAGE_NAME}.rules << 'EOF'
# Legion Toolkit hardware access rules
# Thermal monitoring
SUBSYSTEM=="hwmon", GROUP="legion", MODE="0664"
SUBSYSTEM=="thermal", GROUP="legion", MODE="0664"

# LED control (RGB, backlight)
SUBSYSTEM=="leds", GROUP="legion", MODE="0664"

# Legion laptop kernel module
KERNEL=="legion_laptop", GROUP="legion", MODE="0664"
ACTION=="add", KERNEL=="legion_laptop", RUN+="/bin/chmod 664 /sys/kernel/legion_laptop/*"

# Power supply and battery
SUBSYSTEM=="power_supply", GROUP="legion", MODE="0664"

# ACPI platform devices
KERNEL=="VPC2004:00", GROUP="legion", MODE="0664"
SUBSYSTEM=="platform", KERNEL=="ideapad_acpi", GROUP="legion", MODE="0664"

# Graphics control
KERNEL=="card[0-9]*", SUBSYSTEM=="drm", GROUP="legion", MODE="0664"
EOF

# Create systemd service file (optional)
cat > ./publish/debian-package/${PACKAGE_NAME}/etc/systemd/system/${PACKAGE_NAME}.service << EOF
[Unit]
Description=Legion Toolkit Service
After=graphical-session.target
Wants=graphical-session.target

[Service]
Type=simple
ExecStart=/usr/bin/LegionToolkit --daemon
Restart=on-failure
RestartSec=5
User=%i
Group=legion
Environment=DISPLAY=:0

[Install]
WantedBy=default.target
EOF

# Create man page
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/share/man/man1/${PACKAGE_NAME}.1 << EOF
.TH LEGION-TOOLKIT 1 "$(date '+%B %Y')" "version ${VERSION}" "User Commands"
.SH NAME
legion-toolkit, LegionToolkit \- System management tool for Lenovo Legion laptops
.SH SYNOPSIS
.B legion-toolkit
[\fIOPTION\fR]...
.SH DESCRIPTION
Legion Toolkit is a comprehensive system management application for Lenovo Legion laptops running on Linux. It provides thermal monitoring, RGB control, battery management, and automation features.
.SH OPTIONS
.TP
.B \-\-help
Show help information
.TP
.B \-\-version
Show version information
.TP
.B \-\-daemon
Run in daemon mode (background service)
.TP
.B \-\-cli
Start in command-line interface mode
.TP
.B \-\-status
Show current system status
.SH FILES
.TP
.I ~/.config/legion-toolkit/
User configuration directory
.TP
.I /etc/udev/rules.d/99-legion-toolkit.rules
Hardware access rules
.SH SEE ALSO
.BR lscpu (1),
.BR lspci (1),
.BR sensors (1)
.SH AUTHOR
Legion Toolkit Community
.SH BUGS
Report bugs at: ${HOMEPAGE}/issues
EOF
gzip ./publish/debian-package/${PACKAGE_NAME}/usr/share/man/man1/${PACKAGE_NAME}.1

# Create copyright file
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/share/doc/${PACKAGE_NAME}/copyright << EOF
Format: https://www.debian.org/doc/packaging-manuals/copyright-format/1.0/
Upstream-Name: ${PACKAGE_NAME}
Source: ${HOMEPAGE}

Files: *
Copyright: 2024 Vivek Chamoli
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

# Create changelog
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/share/doc/${PACKAGE_NAME}/changelog.Debian << EOF
${PACKAGE_NAME} (${VERSION}) stable; urgency=medium

  * Complete Linux implementation
  * Thermal monitoring and fan control
  * RGB keyboard support (4-zone and per-key)
  * Battery management features
  * Power mode switching
  * Graphics mode control
  * Command-line interface
  * System tray integration
  * Automation system

 -- Vivek Chamoli <vivekchamoli@outlook.com>  $(date -R)
EOF
gzip ./publish/debian-package/${PACKAGE_NAME}/usr/share/doc/${PACKAGE_NAME}/changelog.Debian

# Build .deb package
echo -e "   ${CYAN}🔨 Building .deb package...${NC}"
cd ./publish/debian-package
dpkg-deb --build ${PACKAGE_NAME}
mv ${PACKAGE_NAME}.deb ../${PACKAGE_NAME}_${VERSION}_amd64.deb
cd ../..

# Create RPM spec file
echo -e "   ${CYAN}📦 Creating RPM package...${NC}"
mkdir -p ./publish/rpm-package/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

cat > ./publish/rpm-package/SPECS/${PACKAGE_NAME}.spec << EOF
Name:           ${PACKAGE_NAME}
Version:        ${VERSION}
Release:        1%{?dist}
Summary:        System management tool for Lenovo Legion laptops
License:        MIT
URL:            ${HOMEPAGE}
Source0:        %{name}-%{version}.tar.gz
BuildArch:      x86_64

Requires:       glibc >= 2.31
Requires:       libicu
Requires:       openssl-libs
Recommends:     acpid
Recommends:     systemd-udev

%description
A comprehensive system management application for Lenovo Legion laptops
running on Linux. Provides thermal monitoring, RGB control, battery
management, and automation features.

%prep
%setup -q

%install
mkdir -p %{buildroot}%{_bindir}
mkdir -p %{buildroot}%{_datadir}/applications
mkdir -p %{buildroot}%{_datadir}/doc/%{name}
mkdir -p %{buildroot}%{_mandir}/man1
mkdir -p %{buildroot}/etc/udev/rules.d
mkdir -p %{buildroot}%{_unitdir}

install -m 755 LegionToolkit %{buildroot}%{_bindir}/
install -m 644 ${PACKAGE_NAME}.desktop %{buildroot}%{_datadir}/applications/
install -m 644 99-${PACKAGE_NAME}.rules %{buildroot}/etc/udev/rules.d/
install -m 644 ${PACKAGE_NAME}.service %{buildroot}%{_unitdir}/
install -m 644 ${PACKAGE_NAME}.1.gz %{buildroot}%{_mandir}/man1/

%files
%{_bindir}/LegionToolkit
%{_datadir}/applications/${PACKAGE_NAME}.desktop
%{_mandir}/man1/${PACKAGE_NAME}.1.gz
/etc/udev/rules.d/99-${PACKAGE_NAME}.rules
%{_unitdir}/${PACKAGE_NAME}.service
%doc README.md
%license LICENSE

%post
getent group legion >/dev/null || groupadd legion
udevadm control --reload-rules 2>/dev/null || true
udevadm trigger 2>/dev/null || true
systemctl daemon-reload 2>/dev/null || true

%preun
systemctl stop ${PACKAGE_NAME} 2>/dev/null || true
systemctl disable ${PACKAGE_NAME} 2>/dev/null || true

%changelog
* $(date '+%a %b %d %Y') Vivek Chamoli <vivekchamoli@outlook.com> - ${VERSION}-1
- Complete Linux implementation with full feature set
EOF

# Create AppImage structure
echo -e "   ${CYAN}📦 Creating AppImage...${NC}"
mkdir -p ./publish/AppImage/${PACKAGE_NAME}.AppDir/usr/{bin,share/{applications,icons/hicolor/256x256/apps}}

# Copy files to AppImage
cp ./publish/linux-x64/LegionToolkit ./publish/AppImage/${PACKAGE_NAME}.AppDir/usr/bin/
cp ./publish/debian-package/${PACKAGE_NAME}/usr/share/applications/${PACKAGE_NAME}.desktop ./publish/AppImage/${PACKAGE_NAME}.AppDir/usr/share/applications/

# Create AppRun script
cat > ./publish/AppImage/${PACKAGE_NAME}.AppDir/AppRun << 'EOF'
#!/bin/bash
HERE="$(dirname "$(readlink -f "${0}")")"
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
export XDG_DATA_DIRS="${HERE}/usr/share:${XDG_DATA_DIRS}"

# Check if running with proper permissions
if ! groups | grep -q legion 2>/dev/null; then
    echo "Warning: User not in 'legion' group. Some features may not work."
    echo "To fix: sudo usermod -a -G legion \$USER"
    echo "Then log out and log back in."
    echo ""
fi

exec "${HERE}/usr/bin/LegionToolkit" "$@"
EOF
chmod +x ./publish/AppImage/${PACKAGE_NAME}.AppDir/AppRun

# Copy desktop file to root for AppImage
cp ./publish/AppImage/${PACKAGE_NAME}.AppDir/usr/share/applications/${PACKAGE_NAME}.desktop ./publish/AppImage/${PACKAGE_NAME}.AppDir/

echo ""
echo -e "${YELLOW}📄 Creating installation scripts...${NC}"

# Create universal install script
cat > ./publish/install-system.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "🚀 Legion Toolkit for Linux - Installation"
echo "============================================="

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}❌ Please run as root (use sudo)${NC}"
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
        echo -e "${RED}❌ Unsupported architecture: $ARCH${NC}"
        echo "   Supported: x86_64, aarch64"
        exit 1
        ;;
esac

echo -e "${GREEN}✅ Detected architecture: $ARCH${NC}"

# Check if source directory exists
if [ ! -d "./$SOURCE_DIR" ]; then
    echo -e "${RED}❌ Build directory ./$SOURCE_DIR not found${NC}"
    echo "   Please run this script from the publish directory"
    exit 1
fi

echo -e "${YELLOW}📦 Installing Legion Toolkit...${NC}"

# Create installation directories
mkdir -p /opt/legion-toolkit
mkdir -p /usr/share/applications
mkdir -p /usr/local/bin
mkdir -p /etc/udev/rules.d
mkdir -p /usr/share/man/man1

# Copy executable and resources
cp -r ./$SOURCE_DIR/* /opt/legion-toolkit/
chmod +x /opt/legion-toolkit/LegionToolkit

# Create symlink
ln -sf /opt/legion-toolkit/LegionToolkit /usr/local/bin/legion-toolkit

# Install desktop file
cat > /usr/share/applications/legion-toolkit.desktop << 'DESKTOP_EOF'
[Desktop Entry]
Type=Application
Name=Legion Toolkit
GenericName=Legion Laptop Management
Comment=System management tool for Lenovo Legion laptops
Exec=legion-toolkit
Icon=computer
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;laptop;thermal;rgb;battery;performance;
Terminal=false
StartupNotify=true
DESKTOP_EOF

# Set up udev rules for hardware access
cat > /etc/udev/rules.d/99-legion-toolkit.rules << 'UDEV_EOF'
# Legion Toolkit hardware access rules
SUBSYSTEM=="hwmon", GROUP="legion", MODE="0664"
SUBSYSTEM=="leds", GROUP="legion", MODE="0664"
KERNEL=="legion_laptop", GROUP="legion", MODE="0664"
SUBSYSTEM=="power_supply", GROUP="legion", MODE="0664"
SUBSYSTEM=="thermal", GROUP="legion", MODE="0664"
ACTION=="add", KERNEL=="legion_laptop", RUN+="/bin/chmod 664 /sys/kernel/legion_laptop/*"
KERNEL=="VPC2004:00", GROUP="legion", MODE="0664"
SUBSYSTEM=="platform", KERNEL=="ideapad_acpi", GROUP="legion", MODE="0664"
KERNEL=="card[0-9]*", SUBSYSTEM=="drm", GROUP="legion", MODE="0664"
UDEV_EOF

# Create legion group
groupadd -f legion

# Reload udev rules
udevadm control --reload-rules 2>/dev/null || true
udevadm trigger 2>/dev/null || true

# Try to load legion-laptop module
modprobe legion-laptop 2>/dev/null || echo "Note: legion-laptop module not available"

echo ""
echo -e "${GREEN}✅ Installation completed successfully!${NC}"
echo ""
echo -e "${YELLOW}📋 Setup Instructions:${NC}"
echo "   1. Add your user to the legion group:"
echo "      sudo usermod -a -G legion \$USER"
echo "   2. Log out and log back in (or restart)"
echo "   3. Launch: legion-toolkit"
echo ""
echo -e "${YELLOW}📁 Installation locations:${NC}"
echo "   • Executable: /opt/legion-toolkit/LegionToolkit"
echo "   • Desktop entry: /usr/share/applications/legion-toolkit.desktop"
echo "   • Command: legion-toolkit"
echo "   • Udev rules: /etc/udev/rules.d/99-legion-toolkit.rules"
echo ""
echo -e "${YELLOW}🔧 Hardware Access:${NC}"
echo "   Legion kernel module, hwmon sensors, RGB LEDs, and battery"
echo "   management require the legion group membership to function."
echo ""
EOF

chmod +x ./publish/install-system.sh

# Create uninstall script
cat > ./publish/uninstall-system.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "🗑️  Legion Toolkit for Linux - Uninstall"
echo "============================================="

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}❌ Please run as root (use sudo)${NC}"
    exit 1
fi

echo "🗑️  Removing Legion Toolkit..."

# Stop any running instances
echo "   Stopping running instances..."
pkill -f LegionToolkit 2>/dev/null || true

# Remove files
echo "   Removing application files..."
rm -rf /opt/legion-toolkit
rm -f /usr/local/bin/legion-toolkit
rm -f /usr/share/applications/legion-toolkit.desktop
rm -f /etc/udev/rules.d/99-legion-toolkit.rules
rm -f /usr/share/man/man1/legion-toolkit.1.gz

# Reload udev rules
udevadm control --reload-rules 2>/dev/null || true

echo ""
echo -e "${GREEN}✅ Legion Toolkit has been uninstalled successfully${NC}"
echo ""
echo "📋 Note: User configurations in ~/.config/legion-toolkit/ were preserved"
echo "   To remove user data: rm -rf ~/.config/legion-toolkit/"
echo ""
echo "🔧 The 'legion' group was not removed. To remove it:"
echo "   sudo groupdel legion"
echo ""
EOF

chmod +x ./publish/uninstall-system.sh

# Create quick test script
cat > ./publish/test-binary.sh << 'EOF'
#!/bin/bash

echo "============================================="
echo "🧪 Legion Toolkit - Quick Test"
echo "============================================="

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Detect architecture
ARCH=$(uname -m)
case $ARCH in
    x86_64) BINARY="./linux-x64/LegionToolkit" ;;
    aarch64) BINARY="./linux-arm64/LegionToolkit" ;;
    *) echo -e "${RED}❌ Unsupported architecture: $ARCH${NC}"; exit 1 ;;
esac

if [ ! -f "$BINARY" ]; then
    echo -e "${RED}❌ Binary not found: $BINARY${NC}"
    echo "   Please run build script first"
    exit 1
fi

echo -e "${GREEN}✅ Found binary: $BINARY${NC}"
echo -e "${YELLOW}🚀 Starting Legion Toolkit...${NC}"
echo ""
echo "Note: Some features require hardware access permissions."
echo "      For full functionality, run the installer: sudo ./install-system.sh"
echo ""

# Check permissions
if ! groups | grep -q legion 2>/dev/null; then
    echo -e "${YELLOW}⚠️  Warning: Not in 'legion' group. Hardware access limited.${NC}"
fi

# Run the application
$BINARY "$@"
EOF

chmod +x ./publish/test-binary.sh

# Create README for packages
cat > ./publish/README.md << EOF
# Legion Toolkit for Linux - Installation Guide

## Quick Installation

### Option 1: System Installation (Recommended)
\`\`\`bash
sudo ./install-system.sh
sudo usermod -a -G legion \$USER
# Log out and log back in
legion-toolkit
\`\`\`

### Option 2: Debian/Ubuntu Package
\`\`\`bash
sudo dpkg -i legion-toolkit_${VERSION}_amd64.deb
sudo usermod -a -G legion \$USER
# Log out and log back in
legion-toolkit
\`\`\`

### Option 3: AppImage (Portable)
\`\`\`bash
./test-binary.sh
# Note: Limited functionality without system installation
\`\`\`

## Files Included

- **legion-toolkit_${VERSION}_amd64.deb** - Debian package for Ubuntu/Debian
- **install-system.sh** - Universal installation script
- **uninstall-system.sh** - Removal script
- **test-binary.sh** - Test runner without installation
- **linux-x64/** - x86_64 binaries and libraries
- **linux-arm64/** - ARM64 binaries and libraries

## Hardware Requirements

### Essential
- Lenovo Legion laptop (Gen 6+)
- Linux kernel 5.4+
- User in 'legion' group

### Optional (for full features)
- legion-laptop kernel module
- Root access for some operations

## Features

✅ **Thermal Management** - CPU/GPU monitoring and fan control
✅ **RGB Control** - 4-zone and per-key keyboard lighting
✅ **Power Management** - Performance modes and battery settings
✅ **Graphics Control** - Hybrid mode and discrete GPU management
✅ **CLI Interface** - Full command-line control
✅ **System Tray** - Background operation
✅ **Automation** - Rules and triggers

## Troubleshooting

### Module not loaded
\`\`\`bash
sudo modprobe legion-laptop
\`\`\`

### Permission denied
\`\`\`bash
sudo usermod -a -G legion \$USER
# Then logout/login
\`\`\`

### GUI not starting
\`\`\`bash
legion-toolkit --help  # Try CLI first
\`\`\`

## Support

- Documentation: ${HOMEPAGE}
- Issues: ${HOMEPAGE}/issues
- Discussions: ${HOMEPAGE}/discussions

## Uninstallation

\`\`\`bash
sudo ./uninstall-system.sh
# or
sudo dpkg -r legion-toolkit
\`\`\`
EOF

echo ""
echo -e "${GREEN}✅ Build completed successfully!${NC}"
echo ""
echo -e "${PURPLE}📁 Output files:${NC}"
echo "   • Linux x64 binary: ./publish/linux-x64/LegionToolkit"
echo "   • Linux ARM64 binary: ./publish/linux-arm64/LegionToolkit"
echo "   • Debian package: ./publish/legion-toolkit_${VERSION}_amd64.deb"
echo "   • System installer: ./publish/install-system.sh"
echo "   • System uninstaller: ./publish/uninstall-system.sh"
echo "   • Test runner: ./publish/test-binary.sh"
echo "   • AppImage template: ./publish/AppImage/"
echo "   • RPM spec: ./publish/rpm-package/SPECS/"
echo "   • Documentation: ./publish/README.md"
echo ""
echo -e "${CYAN}📋 Installation Options:${NC}"
echo "   1. Quick test: cd publish && ./test-binary.sh"
echo "   2. System install: cd publish && sudo ./install-system.sh"
echo "   3. Debian package: cd publish && sudo dpkg -i legion-toolkit_${VERSION}_amd64.deb"
echo ""
echo -e "${YELLOW}⚠️  Important Notes:${NC}"
echo "   • Add user to 'legion' group after installation"
echo "   • Legion kernel module recommended for full functionality"
echo "   • Some features require hardware access permissions"
echo "   • Log out/in required after group membership changes"
echo ""
echo -e "${GREEN}🎉 Legion Toolkit for Linux is ready for deployment!${NC}"