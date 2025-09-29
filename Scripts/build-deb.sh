#!/bin/bash

# Build Debian package for Legion Toolkit
# This script creates a .deb package for easy installation on Debian/Ubuntu systems

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
APP_NAME="legion-toolkit"
APP_VERSION="3.0.0"
ARCH="amd64"
MAINTAINER="Legion Toolkit Community <legion-toolkit@community.org>"
DESCRIPTION="Control panel for Lenovo Legion laptops"
HOMEPAGE="https://github.com/LenovoLegion/LegionToolkit"

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
AVALONIA_DIR="$PROJECT_DIR/LenovoLegionToolkit.Avalonia"
BUILD_DIR="$PROJECT_DIR/build/deb"
OUTPUT_DIR="$PROJECT_DIR/dist"
DEB_NAME="${APP_NAME}_${APP_VERSION}_${ARCH}.deb"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Building Legion Toolkit Debian Package${NC}"
echo -e "${GREEN}Version: $APP_VERSION${NC}"
echo -e "${GREEN}========================================${NC}"

# Check for required tools
echo -e "\n${YELLOW}Checking dependencies...${NC}"
MISSING_DEPS=""

if ! command -v dotnet &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS dotnet"
fi

if ! command -v dpkg-deb &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS dpkg-dev"
fi

if ! command -v fakeroot &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS fakeroot"
fi

if [ ! -z "$MISSING_DEPS" ]; then
    echo -e "${RED}Missing required tools: $MISSING_DEPS${NC}"
    echo "Install with: sudo apt install $MISSING_DEPS"
    exit 1
fi

# Clean previous builds
echo -e "\n${YELLOW}Cleaning previous builds...${NC}"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"
mkdir -p "$OUTPUT_DIR"

# Build the .NET application
echo -e "\n${YELLOW}Building .NET application...${NC}"
cd "$AVALONIA_DIR"
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "$BUILD_DIR/opt/$APP_NAME"

if [ ! -f "$BUILD_DIR/opt/$APP_NAME/LegionToolkit" ]; then
    echo -e "${RED}Build failed: executable not found${NC}"
    exit 1
fi

# Create Debian package structure
echo -e "\n${YELLOW}Creating package structure...${NC}"

# Create directories
mkdir -p "$BUILD_DIR/DEBIAN"
mkdir -p "$BUILD_DIR/usr/share/applications"
mkdir -p "$BUILD_DIR/usr/share/icons/hicolor"
mkdir -p "$BUILD_DIR/usr/share/man/man1"
mkdir -p "$BUILD_DIR/usr/bin"
mkdir -p "$BUILD_DIR/etc/systemd/system"
mkdir -p "$BUILD_DIR/etc/systemd/user"

# Create control file
echo -e "${YELLOW}Creating control file...${NC}"
cat > "$BUILD_DIR/DEBIAN/control" << EOF
Package: $APP_NAME
Version: $APP_VERSION
Architecture: $ARCH
Maintainer: $MAINTAINER
Depends: libc6 (>= 2.31), libgcc-s1 (>= 3.0), libstdc++6 (>= 5.2), libicu67 | libicu66 | libicu70, libssl1.1 | libssl3, ca-certificates
Recommends: systemd
Suggests: policykit-1
Section: utils
Priority: optional
Homepage: $HOMEPAGE
Description: $DESCRIPTION
 Legion Toolkit is a comprehensive control panel for Lenovo Legion laptops
 running Linux. It provides a graphical interface and command-line tools
 for managing power modes, battery settings, thermal controls, RGB keyboard
 lighting, and more.
 .
 Features:
  * Power mode switching (Quiet, Balanced, Performance, Custom)
  * Battery conservation mode and rapid charging
  * Thermal monitoring and fan control
  * RGB keyboard backlight customization
  * Display refresh rate and hybrid graphics control
  * Automation rules and profiles
  * Command-line interface for scripting
EOF

# Calculate installed size
INSTALLED_SIZE=$(du -sk "$BUILD_DIR" | cut -f1)
echo "Installed-Size: $INSTALLED_SIZE" >> "$BUILD_DIR/DEBIAN/control"

# Create preinst script
cat > "$BUILD_DIR/DEBIAN/preinst" << 'EOF'
#!/bin/sh
set -e

# Create legion group if it doesn't exist
if ! getent group legion >/dev/null 2>&1; then
    echo "Creating 'legion' group..."
    groupadd -r legion
fi

# Stop service if running
if systemctl is-active --quiet legion-toolkit.service; then
    echo "Stopping existing Legion Toolkit service..."
    systemctl stop legion-toolkit.service
fi

exit 0
EOF

# Create postinst script
cat > "$BUILD_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/sh
set -e

# Reload systemd
if [ -d /run/systemd/system ]; then
    systemctl daemon-reload
fi

# Update desktop database
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications
fi

# Update icon cache
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor
fi

# Update man database
if command -v mandb >/dev/null 2>&1; then
    mandb -q
fi

# Set permissions
chmod 755 /opt/legion-toolkit/LegionToolkit
chgrp legion /opt/legion-toolkit/LegionToolkit
chmod g+s /opt/legion-toolkit/LegionToolkit

# Add current user to legion group
if [ -n "$SUDO_USER" ]; then
    echo "Adding user $SUDO_USER to 'legion' group..."
    usermod -a -G legion "$SUDO_USER"
    echo "Note: You may need to log out and back in for group changes to take effect."
fi

echo "Legion Toolkit installation complete!"
echo "You can start it from the application menu or run 'legion-toolkit' from terminal."

exit 0
EOF

# Create prerm script
cat > "$BUILD_DIR/DEBIAN/prerm" << 'EOF'
#!/bin/sh
set -e

# Stop services
if systemctl is-active --quiet legion-toolkit.service; then
    systemctl stop legion-toolkit.service
fi

if systemctl is-active --quiet legion-toolkit-system.service; then
    systemctl stop legion-toolkit-system.service
fi

exit 0
EOF

# Create postrm script
cat > "$BUILD_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/sh
set -e

# Update desktop database
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications 2>/dev/null || true
fi

# Update icon cache
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t -f /usr/share/icons/hicolor 2>/dev/null || true
fi

# Reload systemd
if [ -d /run/systemd/system ]; then
    systemctl daemon-reload
fi

# On purge, remove configuration
if [ "$1" = "purge" ]; then
    rm -rf /etc/legion-toolkit
    rm -rf /var/log/legion-toolkit*
fi

exit 0
EOF

# Make scripts executable
chmod 755 "$BUILD_DIR/DEBIAN/preinst"
chmod 755 "$BUILD_DIR/DEBIAN/postinst"
chmod 755 "$BUILD_DIR/DEBIAN/prerm"
chmod 755 "$BUILD_DIR/DEBIAN/postrm"

# Copy desktop file
echo -e "${YELLOW}Installing desktop file...${NC}"
if [ -f "$AVALONIA_DIR/Resources/legion-toolkit.desktop" ]; then
    cp "$AVALONIA_DIR/Resources/legion-toolkit.desktop" "$BUILD_DIR/usr/share/applications/"
    # Update Exec path in desktop file
    sed -i "s|/opt/legion-toolkit/LegionToolkit|/usr/bin/legion-toolkit|g" "$BUILD_DIR/usr/share/applications/legion-toolkit.desktop"
else
    echo -e "${YELLOW}Warning: Desktop file not found${NC}"
fi

# Copy icons
echo -e "${YELLOW}Installing icons...${NC}"
if [ -d "$AVALONIA_DIR/Resources/icons" ]; then
    for size in 16 32 48 64 128 256 512; do
        icon_dir="$BUILD_DIR/usr/share/icons/hicolor/${size}x${size}/apps"
        mkdir -p "$icon_dir"
        if [ -f "$AVALONIA_DIR/Resources/icons/${size}x${size}/legion-toolkit.png" ]; then
            cp "$AVALONIA_DIR/Resources/icons/${size}x${size}/legion-toolkit.png" "$icon_dir/"
        fi
    done

    # Copy SVG if available
    if [ -f "$AVALONIA_DIR/Resources/icons/legion-toolkit.svg" ]; then
        mkdir -p "$BUILD_DIR/usr/share/icons/hicolor/scalable/apps"
        cp "$AVALONIA_DIR/Resources/icons/legion-toolkit.svg" "$BUILD_DIR/usr/share/icons/hicolor/scalable/apps/"
    fi
else
    echo -e "${YELLOW}Warning: Icons directory not found${NC}"
fi

# Copy and compress man page
echo -e "${YELLOW}Installing man page...${NC}"
if [ -f "$AVALONIA_DIR/Resources/man/legion-toolkit.1" ]; then
    cp "$AVALONIA_DIR/Resources/man/legion-toolkit.1" "$BUILD_DIR/usr/share/man/man1/"
    gzip -9 "$BUILD_DIR/usr/share/man/man1/legion-toolkit.1"
else
    echo -e "${YELLOW}Warning: Man page not found${NC}"
fi

# Create symlink in /usr/bin
echo -e "${YELLOW}Creating executable symlink...${NC}"
ln -s /opt/$APP_NAME/LegionToolkit "$BUILD_DIR/usr/bin/legion-toolkit"

# Copy systemd service files
echo -e "${YELLOW}Installing systemd services...${NC}"
if [ -f "$SCRIPT_DIR/legion-toolkit-system.service" ]; then
    cp "$SCRIPT_DIR/legion-toolkit-system.service" "$BUILD_DIR/etc/systemd/system/"
fi

if [ -f "$SCRIPT_DIR/legion-toolkit.service" ]; then
    cp "$SCRIPT_DIR/legion-toolkit.service" "$BUILD_DIR/etc/systemd/user/"
fi

# Create conffiles
echo -e "${YELLOW}Marking configuration files...${NC}"
cat > "$BUILD_DIR/DEBIAN/conffiles" << EOF
/etc/systemd/system/legion-toolkit-system.service
/etc/systemd/user/legion-toolkit.service
EOF

# Set proper permissions
echo -e "${YELLOW}Setting permissions...${NC}"
find "$BUILD_DIR" -type d -exec chmod 755 {} \;
find "$BUILD_DIR" -type f -exec chmod 644 {} \;
chmod 755 "$BUILD_DIR/opt/$APP_NAME/LegionToolkit"
chmod 755 "$BUILD_DIR/DEBIAN/"*

# Build the package
echo -e "\n${YELLOW}Building Debian package...${NC}"
cd "$PROJECT_DIR"
fakeroot dpkg-deb --build "$BUILD_DIR" "$OUTPUT_DIR/$DEB_NAME"

# Verify package
echo -e "\n${YELLOW}Verifying package...${NC}"
dpkg-deb --info "$OUTPUT_DIR/$DEB_NAME"

# Check with lintian if available
if command -v lintian &> /dev/null; then
    echo -e "\n${YELLOW}Running lintian checks...${NC}"
    lintian "$OUTPUT_DIR/$DEB_NAME" || true
fi

# Clean up build directory
echo -e "\n${YELLOW}Cleaning up...${NC}"
rm -rf "$BUILD_DIR"

echo -e "\n${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Package built successfully!${NC}"
echo -e "${GREEN}Output: $OUTPUT_DIR/$DEB_NAME${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "\nTo install:"
echo -e "  ${YELLOW}sudo dpkg -i $OUTPUT_DIR/$DEB_NAME${NC}"
echo -e "\nTo install with dependencies:"
echo -e "  ${YELLOW}sudo apt install $OUTPUT_DIR/$DEB_NAME${NC}"

exit 0