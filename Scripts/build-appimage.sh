#!/bin/bash

# Legion Toolkit AppImage Build Script
# Builds a portable AppImage that works across Linux distributions

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BUILD_DIR="$PROJECT_ROOT/build-appimage"
APP_DIR="$BUILD_DIR/AppDir"
ARCH="x86_64"
VERSION="1.0.0"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_status() {
    echo -e "${GREEN}[*]${NC} $1"
}

print_error() {
    echo -e "${RED}[!]${NC} $1" >&2
}

print_warning() {
    echo -e "${YELLOW}[!]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    print_status "Checking prerequisites..."

    # Check for .NET SDK
    if ! command -v dotnet &> /dev/null; then
        print_error ".NET SDK 8.0 is required. Please install it first."
        exit 1
    fi

    # Check for appimagetool
    if [ ! -f "/tmp/appimagetool-x86_64.AppImage" ]; then
        print_status "Downloading appimagetool..."
        wget -q -O /tmp/appimagetool-x86_64.AppImage \
            https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
        chmod +x /tmp/appimagetool-x86_64.AppImage
    fi
}

# Clean build directory
clean_build() {
    print_status "Cleaning build directory..."
    rm -rf "$BUILD_DIR"
    mkdir -p "$APP_DIR"
}

# Build the application
build_application() {
    print_status "Building Legion Toolkit..."

    cd "$PROJECT_ROOT"

    # Restore dependencies
    dotnet restore

    # Publish for Linux
    dotnet publish LenovoLegionToolkit.Avalonia/LenovoLegionToolkit.Avalonia.csproj \
        -c Release \
        -r linux-x64 \
        --self-contained true \
        -p:PublishSingleFile=false \
        -p:PublishTrimmed=false \
        -o "$APP_DIR/usr/lib/legion-toolkit"
}

# Create AppDir structure
create_appdir() {
    print_status "Creating AppImage directory structure..."

    # Create necessary directories
    mkdir -p "$APP_DIR/usr/bin"
    mkdir -p "$APP_DIR/usr/share/applications"
    mkdir -p "$APP_DIR/usr/share/icons/hicolor/scalable/apps"
    mkdir -p "$APP_DIR/usr/share/icons/hicolor/256x256/apps"
    mkdir -p "$APP_DIR/usr/lib"

    # Create launcher script
    cat > "$APP_DIR/usr/bin/legion-toolkit" << 'EOF'
#!/bin/bash
APPDIR="$(dirname "$(dirname "$(readlink -f "$0")")")"
export PATH="$APPDIR/usr/bin:$PATH"
export LD_LIBRARY_PATH="$APPDIR/usr/lib:$APPDIR/usr/lib/legion-toolkit:$LD_LIBRARY_PATH"
export QT_PLUGIN_PATH="$APPDIR/usr/lib/qt5/plugins:$QT_PLUGIN_PATH"
export DOTNET_ROOT="$APPDIR/usr/lib/dotnet"
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Check if running with GUI or CLI
if [ "$1" == "--help" ] || [ "$1" == "-h" ] || [ -n "$1" ]; then
    # CLI mode
    exec "$APPDIR/usr/lib/legion-toolkit/LenovoLegionToolkit.Avalonia" "$@"
else
    # GUI mode - check for display
    if [ -n "$DISPLAY" ]; then
        exec "$APPDIR/usr/lib/legion-toolkit/LenovoLegionToolkit.Avalonia" "$@"
    else
        echo "No display detected. Use command-line options or run with --help"
        exit 1
    fi
fi
EOF

    chmod +x "$APP_DIR/usr/bin/legion-toolkit"

    # Create AppRun script
    cat > "$APP_DIR/AppRun" << 'EOF'
#!/bin/bash
APPDIR="$(dirname "$(readlink -f "$0")")"
exec "$APPDIR/usr/bin/legion-toolkit" "$@"
EOF

    chmod +x "$APP_DIR/AppRun"
}

# Copy resources
copy_resources() {
    print_status "Copying resources..."

    # Desktop file
    cat > "$APP_DIR/usr/share/applications/legion-toolkit.desktop" << EOF
[Desktop Entry]
Name=Legion Toolkit
Comment=Control utility for Lenovo Legion laptops
Exec=legion-toolkit
Icon=legion-toolkit
Terminal=false
Type=Application
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;laptop;hardware;rgb;power;battery;
StartupNotify=true
X-AppImage-Version=$VERSION
EOF

    # Copy or create icon
    if [ -f "$PROJECT_ROOT/Resources/legion-toolkit.svg" ]; then
        cp "$PROJECT_ROOT/Resources/legion-toolkit.svg" "$APP_DIR/usr/share/icons/hicolor/scalable/apps/"
    fi

    if [ -f "$PROJECT_ROOT/Resources/legion-toolkit.png" ]; then
        cp "$PROJECT_ROOT/Resources/legion-toolkit.png" "$APP_DIR/usr/share/icons/hicolor/256x256/apps/"
    else
        # Create a simple placeholder icon if not found
        print_warning "Icon not found, creating placeholder..."
        convert -size 256x256 xc:black -fill white -draw "circle 128,128 128,32" \
            "$APP_DIR/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png" 2>/dev/null || true
    fi

    # Copy icon to root for AppImage
    if [ -f "$APP_DIR/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png" ]; then
        cp "$APP_DIR/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png" "$APP_DIR/legion-toolkit.png"
    fi

    # Copy desktop file to root
    cp "$APP_DIR/usr/share/applications/legion-toolkit.desktop" "$APP_DIR/legion-toolkit.desktop"
}

# Include dependencies
include_dependencies() {
    print_status "Including runtime dependencies..."

    # List of libraries that might be needed
    local libs=(
        "libX11.so.6"
        "libXrandr.so.2"
        "libXi.so.6"
        "libXext.so.6"
        "libXrender.so.1"
        "libXcursor.so.1"
        "libXfixes.so.3"
        "libfreetype.so.6"
        "libfontconfig.so.1"
    )

    # Copy libraries if they exist
    for lib in "${libs[@]}"; do
        local lib_path=$(ldconfig -p | grep "$lib" | head -1 | awk '{print $NF}')
        if [ -n "$lib_path" ] && [ -f "$lib_path" ]; then
            cp "$lib_path" "$APP_DIR/usr/lib/" 2>/dev/null || true
        fi
    done
}

# Create AppImage
create_appimage() {
    print_status "Creating AppImage..."

    cd "$BUILD_DIR"

    # Set architecture
    export ARCH="$ARCH"

    # Build AppImage
    /tmp/appimagetool-x86_64.AppImage "$APP_DIR" "LegionToolkit-${VERSION}-${ARCH}.AppImage"

    if [ $? -eq 0 ]; then
        print_status "AppImage created successfully!"
        print_status "Output: $BUILD_DIR/LegionToolkit-${VERSION}-${ARCH}.AppImage"

        # Make it executable
        chmod +x "LegionToolkit-${VERSION}-${ARCH}.AppImage"

        # Move to project root
        mv "LegionToolkit-${VERSION}-${ARCH}.AppImage" "$PROJECT_ROOT/"
        print_status "AppImage moved to: $PROJECT_ROOT/LegionToolkit-${VERSION}-${ARCH}.AppImage"
    else
        print_error "Failed to create AppImage"
        exit 1
    fi
}

# Main execution
main() {
    echo "========================================="
    echo "Legion Toolkit AppImage Builder"
    echo "Version: $VERSION"
    echo "Architecture: $ARCH"
    echo "========================================="
    echo

    check_prerequisites
    clean_build
    build_application
    create_appdir
    copy_resources
    include_dependencies
    create_appimage

    echo
    print_status "Build complete!"
    print_status "You can now run the AppImage with:"
    echo "  ./LegionToolkit-${VERSION}-${ARCH}.AppImage"
    echo
    print_status "To integrate with your system, you can:"
    echo "  1. Move it to ~/Applications/"
    echo "  2. Run with --install to add desktop integration"
    echo "  3. Use AppImageLauncher for automatic integration"
}

# Run main function
main "$@"