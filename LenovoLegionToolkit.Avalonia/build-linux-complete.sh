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

# Kernel module information
KERNEL_MODULE_VERSION="2.0.0"
KERNEL_MODULE_NAME="legion-laptop-enhanced"

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
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
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
    -p:PublishTrimmed=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false

# Make executables executable
chmod +x ./publish/linux-x64/LegionToolkit
chmod +x ./publish/linux-arm64/LegionToolkit

# Function to create DKMS package for enhanced kernel module
create_dkms_package() {
    local dkms_name="${KERNEL_MODULE_NAME}"
    local dkms_version="${KERNEL_MODULE_VERSION}"

    echo -e "      Creating DKMS package: ${dkms_name}-dkms_${dkms_version}"

    # Create DKMS package structure
    mkdir -p "./publish/dkms-package/${dkms_name}-dkms/DEBIAN"
    mkdir -p "./publish/dkms-package/${dkms_name}-dkms/usr/src/${dkms_name}-${dkms_version}"
    mkdir -p "./publish/dkms-package/${dkms_name}-dkms/usr/share/doc/${dkms_name}-dkms"

    # Copy kernel module source files
    if [ -d "../kernel-module" ]; then
        cp -r ../kernel-module/* "./publish/dkms-package/${dkms_name}-dkms/usr/src/${dkms_name}-${dkms_version}/"
    else
        echo -e "${YELLOW}⚠️  Warning: Kernel module source not found at ../kernel-module${NC}"
        echo -e "   DKMS package will be created without kernel module source"
        return 1
    fi

    # Create DKMS control file
    cat > "./publish/dkms-package/${dkms_name}-dkms/DEBIAN/control" << EOF
Package: ${dkms_name}-dkms
Version: ${dkms_version}
Section: kernel
Priority: optional
Architecture: all
Depends: dkms (>= 2.1.0.0), build-essential, linux-headers-generic | linux-headers-amd64
Maintainer: ${MAINTAINER}
Description: Enhanced Legion laptop kernel module (DKMS)
 Enhanced kernel module for Lenovo Legion laptops with backward compatibility
 for kernel versions 5.4+ to 6.8+. Provides comprehensive hardware control
 including thermal management, RGB lighting, battery control, and power modes.
 .
 This package provides the source code for the legion-laptop-enhanced kernel
 module to be built with dkms.
 .
 Features:
  - Universal Legion Gen 6-9 support
  - Backward compatibility for multiple kernel versions
  - Enhanced thermal management and monitoring
  - Comprehensive sysfs interface
  - Improved error handling and debugging
Homepage: ${HOMEPAGE}
EOF

    # Create DKMS postinst script
    cat > "./publish/dkms-package/${dkms_name}-dkms/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

DKMS_NAME="legion-laptop-enhanced"
DKMS_VERSION="2.0.0"

echo "Setting up Enhanced Legion DKMS module..."

# Add to DKMS tree
dkms add -m $DKMS_NAME -v $DKMS_VERSION

# Build and install for current kernel
CURRENT_KERNEL=$(uname -r)
echo "Building module for kernel $CURRENT_KERNEL..."

if dkms build -m $DKMS_NAME -v $DKMS_VERSION; then
    echo "✅ Module built successfully"

    if dkms install -m $DKMS_NAME -v $DKMS_VERSION; then
        echo "✅ Module installed successfully"

        # Try to load the module
        if modprobe legion_laptop_enhanced; then
            echo "✅ Enhanced Legion module loaded successfully"
        else
            echo "⚠️  Module installed but could not be loaded immediately"
            echo "   Try 'sudo modprobe legion_laptop_enhanced' or reboot"
        fi
    else
        echo "❌ Failed to install module"
        exit 1
    fi
else
    echo "❌ Failed to build module"
    echo "Please ensure kernel headers are installed:"
    echo "  Ubuntu/Debian: sudo apt install linux-headers-$(uname -r)"
    echo "  Fedora: sudo dnf install kernel-devel-$(uname -r)"
    echo "  Arch: sudo pacman -S linux-headers"
    exit 1
fi

echo ""
echo "Enhanced Legion DKMS module installation completed!"
echo "The module will be automatically rebuilt for future kernel updates."
EOF

    # Create DKMS prerm script
    cat > "./publish/dkms-package/${dkms_name}-dkms/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

DKMS_NAME="legion-laptop-enhanced"
DKMS_VERSION="2.0.0"

echo "Removing Enhanced Legion DKMS module..."

# Remove from all kernels
dkms remove -m $DKMS_NAME -v $DKMS_VERSION --all || true

echo "Enhanced Legion DKMS module removed."
EOF

    # Make scripts executable
    chmod 755 "./publish/dkms-package/${dkms_name}-dkms/DEBIAN/postinst"
    chmod 755 "./publish/dkms-package/${dkms_name}-dkms/DEBIAN/prerm"

    # Create documentation
    cat > "./publish/dkms-package/${dkms_name}-dkms/usr/share/doc/${dkms_name}-dkms/README" << EOF
Enhanced Legion Laptop Kernel Module
===================================

This package provides the enhanced legion-laptop kernel module with DKMS support.

Installation
------------
This module is automatically built and installed by DKMS when the package is installed.

Manual Operations
-----------------
- Build: sudo dkms build -m legion-laptop-enhanced -v 2.0.0
- Install: sudo dkms install -m legion-laptop-enhanced -v 2.0.0
- Remove: sudo dkms remove -m legion-laptop-enhanced -v 2.0.0 --all
- Status: dkms status legion-laptop-enhanced

Loading the Module
------------------
sudo modprobe legion_laptop_enhanced

Module Parameters
-----------------
- debug=1: Enable debug output
- force_load=1: Force loading on unknown models

Compatibility
-------------
Supports kernel versions 5.4+ to 6.8+ with automatic compatibility detection.

For more information, visit: ${HOMEPAGE}
EOF

    # Build DKMS .deb package
    echo -e "      Building DKMS .deb package..."
    cd "./publish/dkms-package"
    dpkg-deb --build "${dkms_name}-dkms"
    mv "${dkms_name}-dkms.deb" "../${dkms_name}-dkms_${dkms_version}_all.deb"
    cd "../.."

    echo -e "      ✅ DKMS package created: ${dkms_name}-dkms_${dkms_version}_all.deb"
}

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
Depends: libc6 (>= 2.31), libicu70 | libicu72 | libicu74, libssl3 | libssl1.1, libx11-6, libfontconfig1, libharfbuzz0b, libfreetype6, libxext6, libxrandr2, libxi6, libxcursor1, libxdamage1, libxfixes3, libxss1, libglib2.0-0, libgtk-3-0 | libgtk-4-1, libgdk-pixbuf-2.0-0, libcairo2, libpango-1.0-0, libatk1.0-0, libxcomposite1, libxrender1, libskia0 | libskiasharp-skia84 | libskia2, libopengl0, libgl1-mesa-glx | libgl1, libglu1-mesa | libglu1, libdrm2, libgbm1, libegl1-mesa | libegl1, libwayland-client0, libwayland-cursor0, libwayland-egl1, libxkbcommon0, libxkbcommon-x11-0, libgio-2.0-0, libdbus-1-3
Recommends: acpi-support, udev, legion-laptop-enhanced-dkms, desktop-file-utils, xdg-utils, x11-xserver-utils
Suggests: linux-modules-extra, linux-headers-generic
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

# Try to load enhanced legion-laptop module if available, fallback to standard module
if [ -x "$(command -v modprobe)" ]; then
    if modprobe legion_laptop_enhanced 2>/dev/null; then
        echo "✅ Enhanced Legion kernel module loaded successfully"
    elif modprobe legion-laptop 2>/dev/null; then
        echo "✅ Standard Legion kernel module loaded"
    else
        echo "⚠️  Note: No Legion kernel module available. Hardware control will be limited."
        echo "   Install legion-laptop-enhanced-dkms package for full functionality."
    fi
fi

# Set up systemd service if systemd is available
if [ -d "/etc/systemd/system" ] && [ -x "$(command -v systemctl)" ]; then
    systemctl daemon-reload 2>/dev/null || true
fi

# Update desktop database and icon cache for GUI visibility
echo "🔄 Updating desktop integration..."

# Update desktop database
if [ -x "$(command -v update-desktop-database)" ]; then
    update-desktop-database /usr/share/applications 2>/dev/null || true
    echo "✅ Desktop database updated"
else
    echo "⚠️  update-desktop-database not available"
fi

# Update icon cache for multiple icon themes
for icon_dir in /usr/share/icons/*/; do
    if [ -d "$icon_dir" ] && [ -x "$(command -v gtk-update-icon-cache)" ]; then
        gtk-update-icon-cache -f -t "$icon_dir" 2>/dev/null || true
    fi
done

if [ -x "$(command -v gtk-update-icon-cache)" ]; then
    gtk-update-icon-cache -f -t /usr/share/icons/hicolor 2>/dev/null || true
    echo "✅ Icon cache updated"
fi

# Update MIME database
if [ -x "$(command -v update-mime-database)" ]; then
    update-mime-database /usr/share/mime 2>/dev/null || true
    echo "✅ MIME database updated"
fi

# Ensure desktop file is properly registered
if [ -f "/usr/share/applications/legion-toolkit.desktop" ]; then
    # Fix permissions
    chmod 644 /usr/share/applications/legion-toolkit.desktop

    # Validate desktop file
    if command -v desktop-file-validate >/dev/null 2>&1; then
        VALIDATION_OUTPUT=$(desktop-file-validate /usr/share/applications/legion-toolkit.desktop 2>&1)
        if [ $? -eq 0 ]; then
            echo "✅ Desktop file validated successfully"
        else
            echo "✅ Desktop file created (validation warnings suppressed)"
            echo "   Run 'legion-toolkit-debug' for detailed validation info"
        fi
    else
        echo "✅ Desktop file created (desktop-file-validate not available)"
    fi
fi

# Update XDG database
if [ -x "$(command -v update-desktop-database)" ]; then
    update-desktop-database 2>/dev/null || true
fi

# For KDE users
if [ -x "$(command -v kbuildsycoca5)" ]; then
    kbuildsycoca5 2>/dev/null || true
elif [ -x "$(command -v kbuildsycoca4)" ]; then
    kbuildsycoca4 2>/dev/null || true
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
echo "   - GUI (manual): legion-toolkit-gui"
echo "   - Direct binary: /usr/bin/LegionToolkit"
echo "   - CLI: legion-toolkit --help"
echo ""
echo "📋 Hardware Support:"
echo "• For full functionality, ensure legion-laptop kernel module is loaded"
echo "• Check module status: lsmod | grep legion"
echo "• Install module: sudo modprobe legion-laptop"
echo ""
echo "🐛 Troubleshooting GUI Issues:"
echo "• Run diagnostic tool: legion-toolkit-debug"
echo "• Manual launch: /usr/bin/LegionToolkit"
echo "• Check logs: journalctl --user -f | grep -i legion"
echo "• Verify display: echo \$DISPLAY (should show :0 or similar)"
echo "• Test in terminal: DISPLAY=:0 /usr/bin/LegionToolkit"
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

# Create lib directory for native libraries
mkdir -p ./publish/debian-package/${PACKAGE_NAME}/usr/lib/${PACKAGE_NAME}/

# Copy all native libraries from publish directory
if [ -d "./publish/linux-x64/runtimes" ]; then
    echo "📦 Copying native runtime libraries..."
    cp -r ./publish/linux-x64/runtimes/* ./publish/debian-package/${PACKAGE_NAME}/usr/lib/${PACKAGE_NAME}/ 2>/dev/null || true
fi

# Copy any .so files from the publish directory
find ./publish/linux-x64/ -name "*.so*" -exec cp {} ./publish/debian-package/${PACKAGE_NAME}/usr/lib/${PACKAGE_NAME}/ \; 2>/dev/null || true

# Ensure libSkiaSharp.so is available
if [ ! -f "./publish/debian-package/${PACKAGE_NAME}/usr/lib/${PACKAGE_NAME}/libSkiaSharp.so" ]; then
    echo "⚠️  Warning: libSkiaSharp.so not found in build output"
    echo "   This may cause GUI rendering issues"
fi

# Create symbolic link for command line access
cd ./publish/debian-package/${PACKAGE_NAME}/usr/bin/
ln -sf LegionToolkit legion-toolkit
cd ../../../../..

# Create GUI launcher wrapper script
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/bin/legion-toolkit-gui << 'EOF'
#!/bin/bash
# Legion Toolkit GUI Launcher Wrapper
# Enhanced GIO-based desktop integration with comprehensive graphics support

# Set library paths for SkiaSharp and native dependencies
export LD_LIBRARY_PATH="/usr/lib/legion-toolkit:/usr/lib/legion-toolkit/linux-x64/native:/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH"
export SKIASHARP_LIBRARY_PATH="/usr/lib/legion-toolkit:/usr/lib/x86_64-linux-gnu"

# GIO-based desktop integration
export GIO_MODULE_DIR="/usr/lib/x86_64-linux-gnu/gio/modules"
export GSETTINGS_SCHEMA_DIR="/usr/share/glib-2.0/schemas"
export XDG_DATA_DIRS="/usr/share:/usr/local/share:${XDG_DATA_DIRS:-/usr/share}"
export XDG_CONFIG_DIRS="/etc/xdg:${XDG_CONFIG_DIRS:-/etc/xdg}"

# Avalonia framework optimizations
export AVALONIA_RENDER="software"  # Fallback to software rendering if needed
export AVALONIA_GLOBAL_SCALE_FACTOR="${AVALONIA_GLOBAL_SCALE_FACTOR:-1.0}"

# Function to detect and configure display environment
setup_display_environment() {
    # Detect session type
    local session_type="${XDG_SESSION_TYPE:-unknown}"

    case "$session_type" in
        "wayland")
            echo "🌊 Detected Wayland session"
            export GDK_BACKEND="wayland,x11"
            export QT_QPA_PLATFORM="wayland;xcb"
            export AVALONIA_RENDER="skia"
            ;;
        "x11")
            echo "🖥️  Detected X11 session"
            export GDK_BACKEND="x11"
            export QT_QPA_PLATFORM="xcb"
            export AVALONIA_RENDER="skia"
            setup_x11_display
            ;;
        *)
            echo "🔍 Auto-detecting display environment..."
            if [ -n "$WAYLAND_DISPLAY" ]; then
                export GDK_BACKEND="wayland,x11"
                export QT_QPA_PLATFORM="wayland;xcb"
            elif [ -n "$DISPLAY" ]; then
                export GDK_BACKEND="x11"
                export QT_QPA_PLATFORM="xcb"
                setup_x11_display
            else
                echo "⚠️  No display environment detected, using defaults"
                export DISPLAY="${DISPLAY:-:0}"
                export GDK_BACKEND="x11"
            fi
            ;;
    esac
}

# Function to set up X11 display
setup_x11_display() {
    if [ -n "$DISPLAY" ] && xset q >/dev/null 2>&1; then
        return 0
    fi

    # Try common display configurations
    for disp in ":0" ":1" ":10" ":0.0"; do
        export DISPLAY="$disp"
        if xset q >/dev/null 2>&1; then
            echo "✅ Using X11 DISPLAY=$DISPLAY"
            return 0
        fi
    done

    # Try to get display from loginctl
    if command -v loginctl >/dev/null 2>&1 && [ -n "$XDG_SESSION_ID" ]; then
        local session_display=$(loginctl show-session "$XDG_SESSION_ID" -p Display --value 2>/dev/null)
        if [ -n "$session_display" ]; then
            export DISPLAY="$session_display"
            if xset q >/dev/null 2>&1; then
                echo "✅ Using session DISPLAY=$DISPLAY"
                return 0
            fi
        fi
    fi

    echo "⚠️  Warning: Could not detect working X11 display, using default"
    export DISPLAY="${DISPLAY:-:0}"
    return 1
}

# Function to check hardware acceleration
check_graphics_support() {
    if command -v glxinfo >/dev/null 2>&1; then
        local renderer=$(glxinfo 2>/dev/null | grep "OpenGL renderer" | head -1)
        if [ -n "$renderer" ]; then
            echo "🎮 Graphics: $renderer"
            export AVALONIA_RENDER="opengl"
        else
            echo "🔧 Hardware acceleration not available, using software rendering"
            export AVALONIA_RENDER="software"
        fi
    fi
}

# Main launcher function
main() {
    echo "🚀 Legion Toolkit GUI Launcher v3.0.0"
    echo "======================================="

    # Check if binary exists
    if [ ! -x "/usr/bin/LegionToolkit" ]; then
        echo "❌ Error: LegionToolkit binary not found at /usr/bin/LegionToolkit"
        exit 1
    fi

    # Configure display environment
    setup_display_environment

    # Check graphics acceleration support
    check_graphics_support

    # Verify critical dependencies
    echo "🔍 Checking dependencies..."

    # Check for SkiaSharp native library
    local skiasharp_found=false
    for lib_path in "/usr/lib/legion-toolkit" "/usr/lib/x86_64-linux-gnu" "/usr/lib"; do
        if [ -f "$lib_path/libSkiaSharp.so" ] || [ -f "$lib_path/libskia.so" ]; then
            echo "✅ SkiaSharp library found in $lib_path"
            skiasharp_found=true
            break
        fi
    done

    if [ "$skiasharp_found" = "false" ]; then
        echo "⚠️  Warning: SkiaSharp native library not found, using software rendering"
        export AVALONIA_RENDER="software"
    fi

    # Check for key system libraries
    local missing_libs=""
    for lib in libgio-2.0.so.0 libglib-2.0.so.0 libgtk-3.so.0; do
        if ! ldconfig -p | grep -q "$lib"; then
            missing_libs="$missing_libs $lib"
        fi
    done

    if [ -n "$missing_libs" ]; then
        echo "⚠️  Warning: Missing system libraries:$missing_libs"
        echo "   Install with: sudo apt install libgio-2.0-0 libglib2.0-0 libgtk-3-0"
    fi

    # Configure HiDPI support
    if [ -n "$GDK_SCALE" ] || [ -n "$GDK_DPI_SCALE" ]; then
        echo "🖥️  HiDPI scaling detected"
        export QT_AUTO_SCREEN_SCALE_FACTOR=1
        export QT_ENABLE_HIGHDPI_SCALING=1
    fi

    # Set font configuration
    export FONTCONFIG_PATH=/etc/fonts
    export QT_FONT_DPI=${QT_FONT_DPI:-96}

    # GIO and D-Bus integration for proper desktop notifications
    if [ -z "$DBUS_SESSION_BUS_ADDRESS" ] && command -v dbus-launch >/dev/null 2>&1; then
        echo "🔧 Starting D-Bus session for desktop integration"
        eval $(dbus-launch --sh-syntax)
    fi

    echo "✅ Environment configured successfully"
    echo ""

    # Launch the application with error handling
    echo "🎯 Starting Legion Toolkit..."

    # Capture startup errors
    if ! /usr/bin/LegionToolkit "$@" 2>&1; then
        local exit_code=$?
        echo ""
        echo "❌ Application failed to start (exit code: $exit_code)"
        echo ""
        echo "🛠️  Troubleshooting:"
        echo "• Run diagnostics: legion-toolkit-debug"
        echo "• Check dependencies: ldd /usr/bin/LegionToolkit"
        echo "• View logs: journalctl --user -f | grep legion"
        echo "• Manual launch: /usr/bin/LegionToolkit"
        echo ""
        exit $exit_code
    fi
}

# Run main function
main "$@"
EOF

chmod +x ./publish/debian-package/${PACKAGE_NAME}/usr/bin/legion-toolkit-gui

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

# Create comprehensive diagnostic launcher script
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/bin/legion-toolkit-debug << 'EOF'
#!/bin/bash
# Legion Toolkit Enhanced Diagnostic Tool v3.0.0

echo "🔍 Legion Toolkit Comprehensive Diagnostic"
echo "==========================================="
echo "Date: $(date)"
echo "User: $(whoami)"
echo ""

# System Information
echo "🖥️  System Information:"
echo "OS: $(lsb_release -d 2>/dev/null | cut -f2 || cat /etc/os-release | grep PRETTY_NAME | cut -d= -f2 | tr -d '\"')"
echo "Kernel: $(uname -r)"
echo "Architecture: $(uname -m)"
echo "Desktop: ${XDG_CURRENT_DESKTOP:-Unknown}"
echo "Session: ${XDG_SESSION_TYPE:-Unknown}"
echo ""

# Binary and Installation Check
echo "📍 Installation Status:"
if [ -x "/usr/bin/LegionToolkit" ]; then
    echo "✅ Main binary: /usr/bin/LegionToolkit"
    ls -la /usr/bin/LegionToolkit
else
    echo "❌ Main binary not found at /usr/bin/LegionToolkit"
fi

if [ -x "/usr/bin/legion-toolkit-gui" ]; then
    echo "✅ GUI launcher: /usr/bin/legion-toolkit-gui"
else
    echo "❌ GUI launcher not found"
fi

if [ -f "/usr/share/applications/legion-toolkit.desktop" ]; then
    echo "✅ Desktop file: /usr/share/applications/legion-toolkit.desktop"
else
    echo "❌ Desktop file not found"
fi
echo ""

# Critical Dependencies Check
echo "🔧 Critical Dependencies:"

# Check .NET dependencies
echo "• .NET Runtime Dependencies:"
missing_dotnet=""
for lib in libc6 libicu70 libicu72 libicu74 libssl3 libssl1.1; do
    if dpkg -l | grep -q "$lib" 2>/dev/null; then
        echo "  ✅ $lib"
    else
        missing_dotnet="$missing_dotnet $lib"
        echo "  ❌ $lib (missing)"
    fi
done

# Check SkiaSharp dependencies
echo "• SkiaSharp Dependencies:"
skiasharp_found=false
for lib_path in "/usr/lib/legion-toolkit" "/usr/lib/x86_64-linux-gnu" "/usr/lib" "/lib/x86_64-linux-gnu"; do
    if [ -f "$lib_path/libSkiaSharp.so" ] || [ -f "$lib_path/libskia.so" ]; then
        echo "  ✅ SkiaSharp library found: $lib_path"
        skiasharp_found=true
        break
    fi
done

if [ "$skiasharp_found" = "false" ]; then
    echo "  ❌ SkiaSharp native library not found"
    echo "     This will cause GUI rendering issues!"
fi

# Check Graphics dependencies
echo "• Graphics Dependencies:"
for lib in libgl1-mesa-glx libgl1 libopengl0 libdrm2 libgbm1 libegl1-mesa; do
    if ldconfig -p 2>/dev/null | grep -q "$lib"; then
        echo "  ✅ $lib"
    else
        echo "  ❌ $lib (missing)"
    fi
done

# Check GIO/GTK dependencies
echo "• GIO/GTK Dependencies:"
for lib in libgio-2.0-0 libglib2.0-0 libgtk-3-0 libgtk-4-1 libdbus-1-3; do
    if dpkg -l 2>/dev/null | grep -q "$lib"; then
        echo "  ✅ $lib"
    else
        echo "  ❌ $lib (missing)"
    fi
done
echo ""

# Display Environment Check
echo "🖥️  Display Environment:"
echo "DISPLAY: ${DISPLAY:-'Not set'}"
echo "WAYLAND_DISPLAY: ${WAYLAND_DISPLAY:-'Not set'}"
echo "XDG_SESSION_TYPE: ${XDG_SESSION_TYPE:-'Unknown'}"

# Test X11 connectivity
if [ -n "$DISPLAY" ]; then
    if command -v xset >/dev/null 2>&1 && xset q >/dev/null 2>&1; then
        echo "✅ X11 display accessible"
    else
        echo "❌ X11 display not accessible"
    fi
else
    echo "⚠️  DISPLAY variable not set"
fi

# Check for Wayland
if [ -n "$WAYLAND_DISPLAY" ]; then
    echo "✅ Wayland session detected"
else
    echo "ℹ️  Not a Wayland session"
fi
echo ""

# Library Path Check
echo "📚 Library Paths:"
echo "LD_LIBRARY_PATH: ${LD_LIBRARY_PATH:-'Not set'}"
if [ -d "/usr/lib/legion-toolkit" ]; then
    echo "✅ Legion Toolkit library directory exists"
    ls -la /usr/lib/legion-toolkit/ 2>/dev/null | head -5
else
    echo "❌ Legion Toolkit library directory not found"
fi
echo ""

# User Groups Check
echo "👤 User Permissions:"
if groups | grep -q legion; then
    echo "✅ User is in 'legion' group"
else
    echo "❌ User not in 'legion' group"
    echo "   Fix with: sudo usermod -a -G legion $USER"
    echo "   Then log out and log back in"
fi
echo ""

# Desktop File Validation
echo "🖥️  Desktop Integration:"
if command -v desktop-file-validate >/dev/null 2>&1; then
    echo "• Desktop file validation:"
    if desktop-file-validate /usr/share/applications/legion-toolkit.desktop 2>&1; then
        echo "  ✅ Desktop file is valid"
    else
        echo "  ⚠️  Desktop file has validation issues (see above)"
    fi
else
    echo "⚠️  desktop-file-validate not available"
    echo "   Install with: sudo apt install desktop-file-utils"
fi

# Show desktop file content
echo "• Desktop file content:"
if [ -f "/usr/share/applications/legion-toolkit.desktop" ]; then
    head -20 /usr/share/applications/legion-toolkit.desktop
else
    echo "  ❌ Desktop file not found"
fi
echo ""

# JSON Serialization Check
echo "🔧 .NET Configuration:"
echo "• Checking for reflection issues..."
if /usr/bin/LegionToolkit --version >/dev/null 2>&1; then
    echo "  ✅ Application starts without JSON reflection errors"
else
    echo "  ❌ Application fails to start (may be JSON reflection issue)"
fi
echo ""

# Hardware Detection
echo "🔌 Hardware Detection:"
if [ -f "/sys/class/dmi/id/product_name" ]; then
    echo "Model: $(cat /sys/class/dmi/id/product_name 2>/dev/null || echo 'Unknown')"
    echo "Version: $(cat /sys/class/dmi/id/product_version 2>/dev/null || echo 'Unknown')"
else
    echo "❌ Unable to detect hardware model"
fi

# Check for Legion kernel module
if lsmod | grep -q legion; then
    echo "✅ Legion kernel module loaded"
    lsmod | grep legion
else
    echo "⚠️  Legion kernel module not loaded"
    echo "   Load with: sudo modprobe legion-laptop"
fi
echo ""

# Environment Variables for GUI
echo "🌐 GUI Environment Variables:"
relevant_vars="DISPLAY WAYLAND_DISPLAY XDG_SESSION_TYPE XDG_CURRENT_DESKTOP GDK_BACKEND QT_QPA_PLATFORM AVALONIA_RENDER GIO_MODULE_DIR GSETTINGS_SCHEMA_DIR"
for var in $relevant_vars; do
    value=$(eval echo \$$var)
    if [ -n "$value" ]; then
        echo "$var=$value"
    else
        echo "$var=(not set)"
    fi
done
echo ""
echo "🔗 Dependencies Check:"
if command -v ldd >/dev/null 2>&1; then
    echo "📋 Checking binary dependencies:"
    MISSING=$(ldd /usr/bin/LegionToolkit 2>/dev/null | grep "not found")
    if [ -z "$MISSING" ]; then
        echo "✅ All dependencies satisfied"
    else
        echo "❌ Missing dependencies:"
        echo "$MISSING"
    fi

    # Check for SkiaSharp specifically
    echo ""
    echo "🎨 SkiaSharp Dependencies:"
    if ldd /usr/bin/LegionToolkit 2>/dev/null | grep -q "libSkiaSharp"; then
        echo "✅ SkiaSharp library referenced"
    else
        echo "⚠️  SkiaSharp library not found in dependencies"
    fi

    # Check native library paths
    echo ""
    echo "📚 Native Library Paths:"
    echo "LD_LIBRARY_PATH: ${LD_LIBRARY_PATH:-'Not set'}"
    if [ -d "/usr/lib/legion-toolkit" ]; then
        echo "✅ Legion native library directory exists"
        echo "   Contents: $(ls -la /usr/lib/legion-toolkit/ 2>/dev/null | wc -l) files"
        if [ -f "/usr/lib/legion-toolkit/libSkiaSharp.so" ]; then
            echo "✅ libSkiaSharp.so found"
        else
            echo "❌ libSkiaSharp.so missing"
        fi
    else
        echo "❌ Legion native library directory missing"
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
    echo "   Path: /usr/share/applications/legion-toolkit.desktop"
    echo "   Permissions: $(ls -la /usr/share/applications/legion-toolkit.desktop 2>/dev/null | cut -d' ' -f1)"

    if command -v desktop-file-validate >/dev/null 2>&1; then
        echo "🔍 Desktop file validation:"
        VALIDATION_OUTPUT=$(desktop-file-validate /usr/share/applications/legion-toolkit.desktop 2>&1)
        if [ $? -eq 0 ]; then
            echo "✅ Desktop file is valid"
        else
            echo "⚠️  Desktop file has validation issues:"
            echo "$VALIDATION_OUTPUT" | sed 's/^/   /'
        fi
    else
        echo "⚠️  desktop-file-validate not available"
    fi

    # Check key desktop file content
    echo "🔧 Desktop file content check:"
    echo "   Exec: $(grep '^Exec=' /usr/share/applications/legion-toolkit.desktop 2>/dev/null)"
    echo "   Categories: $(grep '^Categories=' /usr/share/applications/legion-toolkit.desktop 2>/dev/null)"
    echo "   Actions: $(grep '^Actions=' /usr/share/applications/legion-toolkit.desktop 2>/dev/null)"
else
    echo "❌ Desktop file missing"
fi

# Check alternative desktop file
if [ -f "/usr/share/applications/legion-toolkit-direct.desktop" ]; then
    echo "📄 Alternative desktop file exists: legion-toolkit-direct.desktop"
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
Version=1.0
Type=Application
Name=Legion Toolkit
GenericName=Legion Laptop Management
Comment=System management tool for Lenovo Legion laptops - Thermal, RGB, Battery Control
Exec=/usr/bin/legion-toolkit-gui %F
Icon=${PACKAGE_NAME}
Categories=System;
Keywords=legion;lenovo;laptop;thermal;rgb;battery;performance;gaming;
MimeType=application/x-legion-profile;
Terminal=false
StartupNotify=true
StartupWMClass=LegionToolkit
X-GNOME-SingleWindow=true
X-KDE-StartupNotify=true
TryExec=/usr/bin/legion-toolkit-gui
Actions=PowerQuiet;PowerBalanced;PowerPerformance;BatteryConservation;OpenCLI;DirectLaunch;

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

[Desktop Action OpenCLI]
Name=Open CLI Terminal
Exec=x-terminal-emulator -e /usr/bin/legion-toolkit --help
Icon=utilities-terminal

[Desktop Action DirectLaunch]
Name=Direct Launch (Debug)
Exec=/usr/bin/LegionToolkit
Icon=${PACKAGE_NAME}
EOF

# Create alternative desktop file for advanced users
cat > ./publish/debian-package/${PACKAGE_NAME}/usr/share/applications/${PACKAGE_NAME}-direct.desktop << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Legion Toolkit (Direct)
GenericName=Legion Laptop Management (Direct Launch)
Comment=Direct launch Legion Toolkit without wrapper - for debugging
Exec=env DISPLAY=:0 /usr/bin/LegionToolkit %F
Icon=${PACKAGE_NAME}
Categories=Development;
Keywords=legion;lenovo;laptop;thermal;rgb;battery;performance;gaming;debug;
Terminal=false
StartupNotify=true
StartupWMClass=LegionToolkit
TryExec=/usr/bin/LegionToolkit
NoDisplay=true
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

# Create Enhanced Legion Kernel Module DKMS Package
echo -e "   ${CYAN}🔧 Creating DKMS kernel module package...${NC}"
create_dkms_package

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
echo "   • DKMS kernel module: ./publish/legion-laptop-enhanced-dkms_${KERNEL_MODULE_VERSION}_all.deb"
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
echo "   4. DKMS kernel module: cd publish && sudo dpkg -i legion-laptop-enhanced-dkms_${KERNEL_MODULE_VERSION}_all.deb"
echo ""
echo -e "${YELLOW}⚠️  Important Notes:${NC}"
echo "   • Add user to 'legion' group after installation"
echo "   • Install DKMS kernel module for full hardware control"
echo "   • Enhanced kernel module supports Legion Gen 6-9 with backward compatibility"
echo "   • Some features require hardware access permissions"
echo "   • Log out/in required after group membership changes"
echo ""
echo -e "${GREEN}🎉 Legion Toolkit for Linux is ready for deployment!${NC}"