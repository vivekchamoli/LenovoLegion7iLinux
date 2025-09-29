#!/bin/bash

# Create distribution packages for Legion Toolkit
# Supports: DEB (Debian/Ubuntu), RPM (Fedora/RHEL), TAR.GZ (Universal)

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

APP_NAME="legion-toolkit"
APP_VERSION="3.0.0"
ARCH="amd64"
MAINTAINER="Legion Toolkit Community"
DESCRIPTION="Comprehensive system management for Lenovo Legion laptops"

echo -e "${GREEN}Legion Toolkit Package Builder${NC}"
echo "=============================="

# Build the application first
echo -e "${YELLOW}Building application...${NC}"
cd LenovoLegionToolkit.Avalonia
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -o ../build/publish
cd ..

# Create directories
mkdir -p build/packages
mkdir -p build/debian
mkdir -p build/rpm

# ==============================================================================
# Create DEB Package
# ==============================================================================
echo -e "${YELLOW}Creating DEB package...${NC}"

DEB_DIR="build/debian/${APP_NAME}_${APP_VERSION}_${ARCH}"
mkdir -p $DEB_DIR/{DEBIAN,usr/bin,usr/share/applications,usr/share/icons/hicolor/256x256/apps,etc/legion-toolkit}

# Copy binary
cp build/publish/LegionToolkit $DEB_DIR/usr/bin/legion-toolkit

# Create control file
cat > $DEB_DIR/DEBIAN/control << EOF
Package: ${APP_NAME}
Version: ${APP_VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: ${MAINTAINER}
Description: ${DESCRIPTION}
 Legion Toolkit provides comprehensive control over Lenovo Legion laptop
 hardware including power modes, fan control, battery management, RGB
 keyboard control, and system monitoring.
Depends: libicu70 | libicu69 | libicu68 | libicu67, libssl3 | libssl1.1
Recommends: pkexec
EOF

# Create postinst script
cat > $DEB_DIR/DEBIAN/postinst << 'EOF'
#!/bin/bash
set -e

# Create config directory
mkdir -p /etc/legion-toolkit

# Set permissions
chmod +x /usr/bin/legion-toolkit

# Create systemd service (optional)
if [ -d /etc/systemd/system ]; then
    cat > /etc/systemd/system/legion-toolkit.service << SERVICE
[Unit]
Description=Legion Toolkit Daemon
After=network.target

[Service]
Type=simple
ExecStart=/usr/bin/legion-toolkit daemon --start
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
SERVICE
    systemctl daemon-reload
fi

echo "Legion Toolkit installed successfully!"
echo "Run 'sudo legion-toolkit' to start the application"
EOF
chmod +x $DEB_DIR/DEBIAN/postinst

# Create desktop entry
cat > $DEB_DIR/usr/share/applications/legion-toolkit.desktop << EOF
[Desktop Entry]
Type=Application
Name=Legion Toolkit
Comment=${DESCRIPTION}
Exec=pkexec /usr/bin/legion-toolkit
Icon=legion-toolkit
Categories=System;Settings;
Terminal=false
StartupNotify=true
EOF

# Create icon
cat > $DEB_DIR/usr/share/icons/hicolor/256x256/apps/legion-toolkit.svg << 'EOF'
<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
  <rect width="256" height="256" fill="#1a1a1a"/>
  <text x="128" y="128" font-family="Arial" font-size="48" font-weight="bold"
        text-anchor="middle" dominant-baseline="middle" fill="#ff6b6b">LT</text>
</svg>
EOF

# Build DEB package
dpkg-deb --build $DEB_DIR
mv ${DEB_DIR}.deb build/packages/

# ==============================================================================
# Create RPM Package
# ==============================================================================
echo -e "${YELLOW}Creating RPM package...${NC}"

# Create RPM spec file
cat > build/rpm/legion-toolkit.spec << EOF
Name:           ${APP_NAME}
Version:        ${APP_VERSION}
Release:        1%{?dist}
Summary:        ${DESCRIPTION}
License:        MIT
URL:            https://github.com/LegionToolkit/LegionToolkit

%description
Legion Toolkit provides comprehensive control over Lenovo Legion laptop
hardware including power modes, fan control, battery management, RGB
keyboard control, and system monitoring.

%install
mkdir -p %{buildroot}/usr/bin
mkdir -p %{buildroot}/usr/share/applications
mkdir -p %{buildroot}/usr/share/icons/hicolor/256x256/apps
mkdir -p %{buildroot}/etc/legion-toolkit

cp %{_sourcedir}/LegionToolkit %{buildroot}/usr/bin/legion-toolkit
cp %{_sourcedir}/legion-toolkit.desktop %{buildroot}/usr/share/applications/
cp %{_sourcedir}/legion-toolkit.svg %{buildroot}/usr/share/icons/hicolor/256x256/apps/

%files
%{_bindir}/legion-toolkit
%{_datadir}/applications/legion-toolkit.desktop
%{_datadir}/icons/hicolor/256x256/apps/legion-toolkit.svg
%dir /etc/legion-toolkit

%post
chmod +x /usr/bin/legion-toolkit

%changelog
* $(date +"%a %b %d %Y") ${MAINTAINER} - ${APP_VERSION}
- Initial release
EOF

# Note: RPM building requires rpmbuild tool
echo -e "${YELLOW}RPM spec file created. To build RPM, run:${NC}"
echo "rpmbuild -bb build/rpm/legion-toolkit.spec"

# ==============================================================================
# Create Universal TAR.GZ Package
# ==============================================================================
echo -e "${YELLOW}Creating universal TAR.GZ package...${NC}"

TAR_DIR="build/${APP_NAME}-${APP_VERSION}-linux-x64"
mkdir -p $TAR_DIR

# Copy files
cp build/publish/LegionToolkit $TAR_DIR/legion-toolkit
cp INSTALL.Linux.md $TAR_DIR/README.md

# Create run script
cat > $TAR_DIR/run.sh << 'EOF'
#!/bin/bash
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

if [ "$EUID" -ne 0 ]; then
    echo "Legion Toolkit requires root privileges."
    if command -v pkexec > /dev/null 2>&1; then
        pkexec "$DIR/legion-toolkit" "$@"
    else
        sudo "$DIR/legion-toolkit" "$@"
    fi
else
    "$DIR/legion-toolkit" "$@"
fi
EOF
chmod +x $TAR_DIR/run.sh
chmod +x $TAR_DIR/legion-toolkit

# Create tarball
cd build
tar -czf packages/${APP_NAME}-${APP_VERSION}-linux-x64.tar.gz ${APP_NAME}-${APP_VERSION}-linux-x64
cd ..

# ==============================================================================
# Summary
# ==============================================================================
echo ""
echo -e "${GREEN}Package creation complete!${NC}"
echo "Created packages:"
echo "  - DEB: build/packages/${APP_NAME}_${APP_VERSION}_${ARCH}.deb"
echo "  - TAR.GZ: build/packages/${APP_NAME}-${APP_VERSION}-linux-x64.tar.gz"
echo "  - RPM Spec: build/rpm/legion-toolkit.spec (requires rpmbuild)"
echo ""
echo "Installation commands:"
echo "  DEB: sudo dpkg -i ${APP_NAME}_${APP_VERSION}_${ARCH}.deb"
echo "  TAR.GZ: tar -xzf ${APP_NAME}-${APP_VERSION}-linux-x64.tar.gz && cd ${APP_NAME}-${APP_VERSION}-linux-x64 && ./run.sh"