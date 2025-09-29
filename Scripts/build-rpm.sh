#!/bin/bash

# Build RPM package for Legion Toolkit
# This script creates an .rpm package for easy installation on Fedora/RHEL systems

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
APP_NAME="legion-toolkit"
APP_VERSION="3.0.0"
RELEASE="1"
ARCH="x86_64"
LICENSE="MIT"
VENDOR="Legion Toolkit Community"
URL="https://github.com/LenovoLegion/LegionToolkit"
SUMMARY="Control panel for Lenovo Legion laptops"
DESCRIPTION="Legion Toolkit is a comprehensive control panel for Lenovo Legion laptops
running Linux. It provides a graphical interface and command-line tools
for managing power modes, battery settings, thermal controls, RGB keyboard
lighting, and more."

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
AVALONIA_DIR="$PROJECT_DIR/LenovoLegionToolkit.Avalonia"
BUILD_ROOT="$HOME/rpmbuild"
OUTPUT_DIR="$PROJECT_DIR/dist"
RPM_NAME="${APP_NAME}-${APP_VERSION}-${RELEASE}.${ARCH}.rpm"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Building Legion Toolkit RPM Package${NC}"
echo -e "${GREEN}Version: $APP_VERSION-$RELEASE${NC}"
echo -e "${GREEN}========================================${NC}"

# Check for required tools
echo -e "\n${YELLOW}Checking dependencies...${NC}"
MISSING_DEPS=""

if ! command -v dotnet &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS dotnet"
fi

if ! command -v rpmbuild &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS rpm-build"
fi

if ! command -v rpmdev-setuptree &> /dev/null; then
    MISSING_DEPS="$MISSING_DEPS rpmdevtools"
fi

if [ ! -z "$MISSING_DEPS" ]; then
    echo -e "${RED}Missing required tools: $MISSING_DEPS${NC}"
    echo "Install with: sudo dnf install $MISSING_DEPS"
    exit 1
fi

# Setup RPM build tree
echo -e "\n${YELLOW}Setting up RPM build environment...${NC}"
rpmdev-setuptree

# Clean previous builds
rm -rf "$BUILD_ROOT/BUILD/$APP_NAME"*
rm -rf "$BUILD_ROOT/BUILDROOT/$APP_NAME"*
rm -f "$BUILD_ROOT/RPMS/${ARCH}/$APP_NAME"*
rm -f "$BUILD_ROOT/SRPMS/$APP_NAME"*
mkdir -p "$OUTPUT_DIR"

# Build the .NET application
echo -e "\n${YELLOW}Building .NET application...${NC}"
cd "$AVALONIA_DIR"
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "$BUILD_ROOT/BUILD/$APP_NAME"

if [ ! -f "$BUILD_ROOT/BUILD/$APP_NAME/LegionToolkit" ]; then
    echo -e "${RED}Build failed: executable not found${NC}"
    exit 1
fi

# Create spec file
echo -e "\n${YELLOW}Creating RPM spec file...${NC}"
cat > "$BUILD_ROOT/SPECS/$APP_NAME.spec" << EOF
Name:           $APP_NAME
Version:        $APP_VERSION
Release:        $RELEASE%{?dist}
Summary:        $SUMMARY
License:        $LICENSE
URL:            $URL
Vendor:         $VENDOR
BuildArch:      $ARCH

Requires:       glibc >= 2.31
Requires:       libgcc >= 3.0
Requires:       libstdc++ >= 5.2
Requires:       openssl-libs
Requires:       ca-certificates
Requires:       systemd

Recommends:     PolicyKit
Suggests:       kernel-modules-extra

%description
$DESCRIPTION

Features:
 * Power mode switching (Quiet, Balanced, Performance, Custom)
 * Battery conservation mode and rapid charging
 * Thermal monitoring and fan control
 * RGB keyboard backlight customization
 * Display refresh rate and hybrid graphics control
 * Automation rules and profiles
 * Command-line interface for scripting

%prep
# No source preparation needed

%build
# Application already built

%install
rm -rf \$RPM_BUILD_ROOT

# Create directories
mkdir -p \$RPM_BUILD_ROOT/opt/%{name}
mkdir -p \$RPM_BUILD_ROOT%{_bindir}
mkdir -p \$RPM_BUILD_ROOT%{_datadir}/applications
mkdir -p \$RPM_BUILD_ROOT%{_datadir}/icons/hicolor/scalable/apps
mkdir -p \$RPM_BUILD_ROOT%{_datadir}/icons/hicolor/{16x16,32x32,48x48,64x64,128x128,256x256,512x512}/apps
mkdir -p \$RPM_BUILD_ROOT%{_mandir}/man1
mkdir -p \$RPM_BUILD_ROOT%{_unitdir}
mkdir -p \$RPM_BUILD_ROOT%{_userunitdir}

# Copy application files
cp -r $BUILD_ROOT/BUILD/%{name}/* \$RPM_BUILD_ROOT/opt/%{name}/

# Create symlink
ln -s /opt/%{name}/LegionToolkit \$RPM_BUILD_ROOT%{_bindir}/legion-toolkit

# Copy desktop file
if [ -f "$AVALONIA_DIR/Resources/legion-toolkit.desktop" ]; then
    cp "$AVALONIA_DIR/Resources/legion-toolkit.desktop" \$RPM_BUILD_ROOT%{_datadir}/applications/
    sed -i "s|/opt/legion-toolkit/LegionToolkit|legion-toolkit|g" \$RPM_BUILD_ROOT%{_datadir}/applications/legion-toolkit.desktop
fi

# Copy icons
if [ -d "$AVALONIA_DIR/Resources/icons" ]; then
    for size in 16 32 48 64 128 256 512; do
        if [ -f "$AVALONIA_DIR/Resources/icons/\${size}x\${size}/legion-toolkit.png" ]; then
            cp "$AVALONIA_DIR/Resources/icons/\${size}x\${size}/legion-toolkit.png" \
               \$RPM_BUILD_ROOT%{_datadir}/icons/hicolor/\${size}x\${size}/apps/
        fi
    done

    if [ -f "$AVALONIA_DIR/Resources/icons/legion-toolkit.svg" ]; then
        cp "$AVALONIA_DIR/Resources/icons/legion-toolkit.svg" \
           \$RPM_BUILD_ROOT%{_datadir}/icons/hicolor/scalable/apps/
    fi
fi

# Copy and compress man page
if [ -f "$AVALONIA_DIR/Resources/man/legion-toolkit.1" ]; then
    cp "$AVALONIA_DIR/Resources/man/legion-toolkit.1" \$RPM_BUILD_ROOT%{_mandir}/man1/
    gzip -9 \$RPM_BUILD_ROOT%{_mandir}/man1/legion-toolkit.1
fi

# Copy systemd service files
if [ -f "$SCRIPT_DIR/legion-toolkit-system.service" ]; then
    cp "$SCRIPT_DIR/legion-toolkit-system.service" \$RPM_BUILD_ROOT%{_unitdir}/
fi

if [ -f "$SCRIPT_DIR/legion-toolkit.service" ]; then
    cp "$SCRIPT_DIR/legion-toolkit.service" \$RPM_BUILD_ROOT%{_userunitdir}/
fi

%pre
# Create legion group if it doesn't exist
getent group legion >/dev/null || groupadd -r legion

# Stop service if running
if systemctl is-active --quiet legion-toolkit.service; then
    systemctl stop legion-toolkit.service
fi

%post
# Reload systemd
systemctl daemon-reload

# Update desktop database
update-desktop-database &> /dev/null || :

# Update icon cache
touch --no-create %{_datadir}/icons/hicolor &>/dev/null || :
gtk-update-icon-cache %{_datadir}/icons/hicolor &>/dev/null || :

# Update man database
mandb -q &> /dev/null || :

# Set permissions
chmod 755 /opt/%{name}/LegionToolkit
chgrp legion /opt/%{name}/LegionToolkit
chmod g+s /opt/%{name}/LegionToolkit

# Add current user to legion group if installing interactively
if [ -n "\$SUDO_USER" ]; then
    usermod -a -G legion "\$SUDO_USER"
    echo "User \$SUDO_USER added to 'legion' group."
    echo "You may need to log out and back in for group changes to take effect."
fi

echo "Legion Toolkit installation complete!"
echo "You can start it from the application menu or run 'legion-toolkit' from terminal."

%preun
# Stop services before uninstall
if [ \$1 -eq 0 ]; then
    systemctl stop legion-toolkit.service &> /dev/null || :
    systemctl stop legion-toolkit-system.service &> /dev/null || :
fi

%postun
# Update desktop database
update-desktop-database &> /dev/null || :

# Update icon cache
if [ \$1 -eq 0 ]; then
    touch --no-create %{_datadir}/icons/hicolor &>/dev/null || :
    gtk-update-icon-cache %{_datadir}/icons/hicolor &>/dev/null || :
fi

# Reload systemd
systemctl daemon-reload &> /dev/null || :

%files
%defattr(-,root,root,-)
/opt/%{name}
%{_bindir}/legion-toolkit
%{_datadir}/applications/legion-toolkit.desktop
%{_datadir}/icons/hicolor/*/apps/legion-toolkit.*
%{_mandir}/man1/legion-toolkit.1.gz
%config(noreplace) %{_unitdir}/legion-toolkit-system.service
%config(noreplace) %{_userunitdir}/legion-toolkit.service

%changelog
* $(date +"%a %b %d %Y") $VENDOR <legion-toolkit@community.org> - ${APP_VERSION}-${RELEASE}
- Initial RPM package release
- Full feature parity with Windows version
- Support for Legion Gen 6-9 laptops
- CLI interface for automation
- Systemd service integration
EOF

# Build the RPM
echo -e "\n${YELLOW}Building RPM package...${NC}"
cd "$BUILD_ROOT"
rpmbuild -bb "SPECS/$APP_NAME.spec"

# Copy RPM to output directory
echo -e "\n${YELLOW}Copying package to output directory...${NC}"
cp "$BUILD_ROOT/RPMS/${ARCH}/$RPM_NAME" "$OUTPUT_DIR/"

# Verify package
echo -e "\n${YELLOW}Verifying package...${NC}"
rpm -qpi "$OUTPUT_DIR/$RPM_NAME"
rpm -qpl "$OUTPUT_DIR/$RPM_NAME" | head -20

# Check with rpmlint if available
if command -v rpmlint &> /dev/null; then
    echo -e "\n${YELLOW}Running rpmlint checks...${NC}"
    rpmlint "$OUTPUT_DIR/$RPM_NAME" || true
fi

echo -e "\n${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Package built successfully!${NC}"
echo -e "${GREEN}Output: $OUTPUT_DIR/$RPM_NAME${NC}"
echo -e "${GREEN}========================================${NC}"
echo -e "\nTo install:"
echo -e "  ${YELLOW}sudo dnf install $OUTPUT_DIR/$RPM_NAME${NC}"
echo -e "\nOr:"
echo -e "  ${YELLOW}sudo rpm -i $OUTPUT_DIR/$RPM_NAME${NC}"

exit 0