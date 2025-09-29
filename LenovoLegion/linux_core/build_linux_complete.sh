#!/bin/bash
# Complete Linux Build System for Legion Toolkit
# Builds all components with full Windows feature parity
# Supports DEB, RPM, and AppImage packaging

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Build configuration
VERSION="6.0.0"
BUILD_DIR="$(pwd)/build"
DIST_DIR="$(pwd)/dist"
PKG_NAME="legion-toolkit"

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Legion Toolkit Linux Complete Build${NC}"
echo -e "${GREEN}Version: ${VERSION}${NC}"
echo -e "${GREEN}========================================${NC}"

# Check for required dependencies
check_dependencies() {
    echo -e "${YELLOW}Checking build dependencies...${NC}"

    REQUIRED_TOOLS=(
        "python3" "pip3" "gcc" "make" "dkms"
        "dpkg-dev" "rpmbuild" "git" "wget"
        "pkg-config" "libgtk-4-dev" "libadwaita-1-dev"
    )

    MISSING_TOOLS=()

    for tool in "${REQUIRED_TOOLS[@]}"; do
        if ! command -v "$tool" &> /dev/null && ! dpkg -l | grep -q "$tool"; then
            MISSING_TOOLS+=("$tool")
        fi
    done

    if [ ${#MISSING_TOOLS[@]} -ne 0 ]; then
        echo -e "${RED}Missing required tools: ${MISSING_TOOLS[*]}${NC}"
        echo -e "${YELLOW}Install with:${NC}"
        echo "sudo apt update"
        echo "sudo apt install ${MISSING_TOOLS[*]}"
        exit 1
    fi

    echo -e "${GREEN}All dependencies satisfied${NC}"
}

# Clean previous builds
clean_build() {
    echo -e "${YELLOW}Cleaning previous builds...${NC}"
    rm -rf "$BUILD_DIR" "$DIST_DIR"
    mkdir -p "$BUILD_DIR" "$DIST_DIR"
    echo -e "${GREEN}Build directories cleaned${NC}"
}

# Build kernel module with DKMS
build_kernel_module() {
    echo -e "${YELLOW}Building Legion kernel module...${NC}"

    cd kernel_module

    # Create dkms.conf
    cat > dkms.conf << EOF
PACKAGE_NAME="legion-laptop"
PACKAGE_VERSION="${VERSION}"
BUILT_MODULE_NAME[0]="legion_laptop_16irx9"
DEST_MODULE_LOCATION[0]="/updates/dkms"
AUTOINSTALL="yes"
REMAKE_INITRD="yes"
EOF

    # Build module
    make clean
    make

    # Create DKMS package
    DKMS_DIR="/usr/src/legion-laptop-${VERSION}"
    sudo mkdir -p "$DKMS_DIR"
    sudo cp -r * "$DKMS_DIR/"

    # Add to DKMS (remove old version first)
    sudo dkms remove legion-laptop/${VERSION} --all 2>/dev/null || true
    sudo dkms add -m legion-laptop -v ${VERSION}
    sudo dkms build -m legion-laptop -v ${VERSION}

    echo -e "${GREEN}Kernel module built successfully${NC}"
    cd ..
}

# Setup Python environment
setup_python_env() {
    echo -e "${YELLOW}Setting up Python environment...${NC}"

    # Create virtual environment
    python3 -m venv "$BUILD_DIR/venv"
    source "$BUILD_DIR/venv/bin/activate"

    # Upgrade pip
    pip install --upgrade pip setuptools wheel

    # Install build dependencies
    pip install \
        PyGObject \
        pycairo \
        numpy \
        torch \
        torchvision \
        scikit-learn \
        psutil \
        pyinstaller \
        pygobject \
        requests

    echo -e "${GREEN}Python environment ready${NC}"
}

# Build GUI application
build_gui_application() {
    echo -e "${YELLOW}Building GUI application...${NC}"

    source "$BUILD_DIR/venv/bin/activate"

    # Create main executable script
    cat > "$BUILD_DIR/legion-toolkit-gui" << 'EOF'
#!/usr/bin/env python3
import sys
import os
from pathlib import Path

# Add library path
sys.path.insert(0, '/usr/lib/legion-toolkit')

# Set up environment
os.environ['GI_TYPELIB_PATH'] = '/usr/lib/x86_64-linux-gnu/girepository-1.0'

# Import and run GUI
try:
    from gui.legion_toolkit_gui import main
    sys.exit(main())
except ImportError as e:
    print(f"Error: {e}")
    print("Please ensure legion-toolkit is properly installed")
    sys.exit(1)
EOF
    chmod +x "$BUILD_DIR/legion-toolkit-gui"

    echo -e "${GREEN}GUI application built${NC}"
}

# Build CLI application
build_cli_application() {
    echo -e "${YELLOW}Building CLI application...${NC}"

    source "$BUILD_DIR/venv/bin/activate"

    # Create CLI executable script
    cat > "$BUILD_DIR/legion-toolkit-cli" << 'EOF'
#!/usr/bin/env python3
import sys
import os
from pathlib import Path

# Add library path
sys.path.insert(0, '/usr/lib/legion-toolkit')

# Import and run CLI
try:
    from cli.legion_toolkit_cli import main
    import asyncio
    sys.exit(asyncio.run(main()))
except ImportError as e:
    print(f"Error: {e}")
    print("Please ensure legion-toolkit is properly installed")
    sys.exit(1)
EOF
    chmod +x "$BUILD_DIR/legion-toolkit-cli"

    echo -e "${GREEN}CLI application built${NC}"
}

# Build AI service
build_ai_service() {
    echo -e "${YELLOW}Building AI optimization service...${NC}"

    source "$BUILD_DIR/venv/bin/activate"

    # Create AI service script
    cat > "$BUILD_DIR/legion-ai-optimizer" << 'EOF'
#!/usr/bin/env python3
import sys
import asyncio
import signal
import logging
from pathlib import Path

# Add library path
sys.path.insert(0, '/usr/lib/legion-toolkit')

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('/var/log/legion-ai-optimizer.log'),
        logging.StreamHandler()
    ]
)

logger = logging.getLogger('legion-ai-optimizer')

class AIOptimizerService:
    def __init__(self):
        self.running = False
        self.ai_controller = None

    async def start(self):
        logger.info("Starting Legion AI Optimizer Service")

        try:
            from ai.ai_controller import LinuxAIController
            self.ai_controller = LinuxAIController()

            if not await self.ai_controller.initialize():
                logger.error("Failed to initialize AI controller")
                return False

            self.running = True
            await self.ai_controller.start_monitoring()

            logger.info("AI optimizer service started successfully")

            # Keep service running
            while self.running:
                await asyncio.sleep(10)

        except Exception as e:
            logger.error(f"AI optimizer service error: {e}")
            return False

    async def stop(self):
        logger.info("Stopping Legion AI Optimizer Service")
        self.running = False

        if self.ai_controller:
            await self.ai_controller.stop_monitoring()

        logger.info("AI optimizer service stopped")

# Service instance
service = AIOptimizerService()

def signal_handler(signum, frame):
    logger.info(f"Received signal {signum}")
    asyncio.create_task(service.stop())

# Setup signal handlers
signal.signal(signal.SIGTERM, signal_handler)
signal.signal(signal.SIGINT, signal_handler)

if __name__ == "__main__":
    try:
        asyncio.run(service.start())
    except KeyboardInterrupt:
        logger.info("Service interrupted by user")
    except Exception as e:
        logger.error(f"Service failed: {e}")
        sys.exit(1)
EOF
    chmod +x "$BUILD_DIR/legion-ai-optimizer"

    # Create systemd service file
    cat > "$BUILD_DIR/legion-ai-optimizer.service" << EOF
[Unit]
Description=Legion AI Optimization Service
Documentation=https://github.com/vivekchamoli/LenovoLegion7i
After=multi-user.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/usr/bin/legion-ai-optimizer
Restart=on-failure
RestartSec=5
User=root
Group=root
StandardOutput=journal
StandardError=journal
SyslogIdentifier=legion-ai-optimizer

# Security settings
NoNewPrivileges=yes
ProtectSystem=strict
ProtectHome=yes
ReadWritePaths=/var/log /sys/kernel/legion_laptop
PrivateTmp=yes

[Install]
WantedBy=multi-user.target
EOF

    echo -e "${GREEN}AI service built${NC}"
}

# Create desktop files
create_desktop_files() {
    echo -e "${YELLOW}Creating desktop files...${NC}"

    # Main application desktop file
    cat > "$BUILD_DIR/legion-toolkit.desktop" << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Legion Toolkit
GenericName=Legion Hardware Control
Comment=Advanced hardware control for Legion laptops
Icon=legion-toolkit
Exec=legion-toolkit-gui
Terminal=false
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;hardware;performance;gaming;thermal;fan;rgb;
StartupNotify=true
EOF

    # CLI desktop file (for terminal applications menu)
    cat > "$BUILD_DIR/legion-toolkit-cli.desktop" << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Legion Toolkit CLI
GenericName=Legion Command Line Interface
Comment=Command line interface for Legion hardware control
Icon=utilities-terminal
Exec=x-terminal-emulator -e legion-toolkit-cli
Terminal=true
Categories=System;TerminalEmulator;
Keywords=legion;lenovo;cli;terminal;hardware;
NoDisplay=false
EOF

    echo -e "${GREEN}Desktop files created${NC}"
}

# Create documentation
create_documentation() {
    echo -e "${YELLOW}Creating documentation...${NC}"

    cat > "$BUILD_DIR/README.md" << EOF
# Legion Toolkit for Linux

Advanced hardware control suite for Lenovo Legion laptops, specifically optimized for Legion Slim 7i Gen 9 (16IRX9).

## Features

- **Complete Hardware Control**: CPU/GPU power management, thermal control, fan curves
- **AI-Powered Optimization**: Machine learning thermal prediction and optimization
- **RGB Control**: Full Spectrum RGB keyboard lighting with 4 zones
- **Performance Modes**: Quiet, Balanced, Performance, and Custom modes
- **Real-time Monitoring**: Temperature, fan speeds, power consumption
- **Command Line Interface**: Full CLI for automation and scripting
- **Cross-Platform**: Feature parity with Windows Legion Toolkit

## Installation

### Ubuntu/Debian
\`\`\`bash
sudo dpkg -i legion-toolkit_${VERSION}_amd64.deb
sudo apt-get install -f  # Fix dependencies if needed
\`\`\`

### Fedora/RHEL/CentOS
\`\`\`bash
sudo rpm -ivh legion-toolkit-${VERSION}-1.x86_64.rpm
\`\`\`

### Universal (AppImage)
\`\`\`bash
chmod +x legion-toolkit-${VERSION}-x86_64.AppImage
sudo ./legion-toolkit-${VERSION}-x86_64.AppImage
\`\`\`

## Usage

### GUI Application
\`\`\`bash
legion-toolkit-gui
\`\`\`

### Command Line Interface
\`\`\`bash
# Show system information
legion-toolkit-cli info

# Set performance mode
legion-toolkit-cli perf set performance

# Control fans
legion-toolkit-cli fan set 1 75  # Set fan 1 to 75%

# RGB lighting
legion-toolkit-cli rgb set rainbow --speed 7

# GPU overclocking
legion-toolkit-cli gpu overclock 150 500

# AI optimization
legion-toolkit-cli ai optimize
\`\`\`

### AI Service
\`\`\`bash
# Start AI optimization service
sudo systemctl enable legion-ai-optimizer
sudo systemctl start legion-ai-optimizer

# Check service status
sudo systemctl status legion-ai-optimizer
\`\`\`

## Requirements

- Linux kernel 5.4+
- Root/sudo access for hardware control
- NVIDIA drivers (for GPU features)
- GTK4 and Libadwaita (for GUI)

## Supported Hardware

- Legion Slim 7i Gen 9 (16IRX9)
- Intel Core i9-14900HX
- NVIDIA RTX 4070 Laptop GPU
- 4-zone RGB Spectrum keyboard

## License

GPL-3.0 License

## Support

For issues and support, visit: https://github.com/vivekchamoli/LenovoLegion7i
EOF

    # Create man pages
    mkdir -p "$BUILD_DIR/man/man1"

    cat > "$BUILD_DIR/man/man1/legion-toolkit-cli.1" << EOF
.TH LEGION-TOOLKIT-CLI 1 "$(date +'%B %Y')" "Legion Toolkit ${VERSION}" "User Commands"
.SH NAME
legion-toolkit-cli \- Command line interface for Legion laptop hardware control
.SH SYNOPSIS
.B legion-toolkit-cli
[\fIOPTION\fR]... [\fICOMMAND\fR]...
.SH DESCRIPTION
Legion Toolkit CLI provides command line access to Legion laptop hardware features including performance modes, thermal management, RGB lighting, and AI optimization.
.SH COMMANDS
.TP
.B info
Show comprehensive system information
.TP
.B perf get|set MODE
Get or set performance mode (quiet, balanced, performance, custom)
.TP
.B temps
Display all temperature sensors
.TP
.B fans
Show fan speeds and status
.TP
.B fan set FAN SPEED
Set fan speed (FAN: 1-2, SPEED: 0-100%)
.TP
.B power get|set COMPONENT LIMIT
Get or set power limits (cpu_pl1, cpu_pl2, gpu_tgp)
.TP
.B rgb set MODE [OPTIONS]
Set RGB lighting mode (off, static, breathing, rainbow, wave)
.TP
.B gpu info|overclock|reset
GPU information and overclocking controls
.TP
.B ai status|optimize|start|stop
AI optimization features
.TP
.B monitor [DURATION]
Monitor temperatures for specified duration
.SH EXAMPLES
.TP
legion-toolkit-cli perf set performance
Set performance mode to maximum performance
.TP
legion-toolkit-cli fan set 1 80
Set fan 1 speed to 80%
.TP
legion-toolkit-cli rgb set static --color FF0000
Set RGB to red static color
.TP
legion-toolkit-cli gpu overclock 150 500
Overclock GPU: +150MHz core, +500MHz memory
.SH FILES
.TP
.I /sys/kernel/legion_laptop/
Kernel module interface files
.TP
.I /var/log/legion-ai-optimizer.log
AI service log file
.SH SEE ALSO
.BR legion-toolkit-gui (1)
.SH AUTHOR
Vivek Chamoli
EOF

    echo -e "${GREEN}Documentation created${NC}"
}

# Create DEB package
create_deb_package() {
    echo -e "${YELLOW}Creating DEB package...${NC}"

    PKG_DIR="${DIST_DIR}/${PKG_NAME}_${VERSION}_amd64"

    # Create package structure
    mkdir -p "${PKG_DIR}/DEBIAN"
    mkdir -p "${PKG_DIR}/usr/bin"
    mkdir -p "${PKG_DIR}/usr/lib/legion-toolkit"
    mkdir -p "${PKG_DIR}/usr/share/applications"
    mkdir -p "${PKG_DIR}/usr/share/icons/hicolor/256x256/apps"
    mkdir -p "${PKG_DIR}/usr/share/doc/legion-toolkit"
    mkdir -p "${PKG_DIR}/usr/share/man/man1"
    mkdir -p "${PKG_DIR}/etc/systemd/system"
    mkdir -p "${PKG_DIR}/usr/src/legion-laptop-${VERSION}"

    # Copy executables
    cp "$BUILD_DIR/legion-toolkit-gui" "${PKG_DIR}/usr/bin/"
    cp "$BUILD_DIR/legion-toolkit-cli" "${PKG_DIR}/usr/bin/"
    cp "$BUILD_DIR/legion-ai-optimizer" "${PKG_DIR}/usr/bin/"

    # Copy Python modules
    cp -r hardware/ "${PKG_DIR}/usr/lib/legion-toolkit/"
    cp -r ai/ "${PKG_DIR}/usr/lib/legion-toolkit/"
    cp -r cli/ "${PKG_DIR}/usr/lib/legion-toolkit/"
    cp -r gui/ "${PKG_DIR}/usr/lib/legion-toolkit/"

    # Copy desktop files
    cp "$BUILD_DIR"/*.desktop "${PKG_DIR}/usr/share/applications/"

    # Copy documentation
    cp "$BUILD_DIR/README.md" "${PKG_DIR}/usr/share/doc/legion-toolkit/"
    cp "$BUILD_DIR/man/man1"/* "${PKG_DIR}/usr/share/man/man1/"

    # Copy systemd service
    cp "$BUILD_DIR/legion-ai-optimizer.service" "${PKG_DIR}/etc/systemd/system/"

    # Copy kernel module source
    cp -r kernel_module/* "${PKG_DIR}/usr/src/legion-laptop-${VERSION}/"

    # Create icon (placeholder)
    convert -size 256x256 xc:blue "${PKG_DIR}/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png" 2>/dev/null || {
        # Fallback if ImageMagick not available
        echo "Creating placeholder icon"
        touch "${PKG_DIR}/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png"
    }

    # Create control file
    cat > "${PKG_DIR}/DEBIAN/control" << EOF
Package: ${PKG_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: amd64
Depends: python3 (>= 3.10), python3-gi, gir1.2-gtk-4.0, gir1.2-adw-1, dkms, build-essential, linux-headers-generic
Recommends: nvidia-driver-535
Suggests: python3-torch, python3-sklearn
Maintainer: Vivek Chamoli <vivek@legion-toolkit.org>
Description: Advanced hardware control for Legion laptops
 Legion Toolkit provides comprehensive hardware control for Lenovo Legion
 laptops including performance tuning, thermal management, RGB lighting,
 and AI-powered optimization. Specifically optimized for Legion Slim 7i
 Gen 9 (16IRX9) with Intel Core i9-14900HX and NVIDIA RTX 4070.
 .
 Features include:
  * Real-time thermal monitoring and fan control
  * GPU overclocking and power management
  * 4-zone RGB Spectrum keyboard control
  * AI-powered thermal prediction and optimization
  * Performance mode switching (Quiet/Balanced/Performance/Custom)
  * Command line interface for automation
  * Cross-platform compatibility with Windows version
Homepage: https://github.com/vivekchamoli/LenovoLegion7i
EOF

    # Create postinst script
    cat > "${PKG_DIR}/DEBIAN/postinst" << 'EOF'
#!/bin/bash
set -e

echo "Configuring Legion Toolkit..."

# Install kernel module via DKMS
if command -v dkms >/dev/null 2>&1; then
    echo "Installing kernel module..."
    dkms add -m legion-laptop -v 6.0.0
    dkms build -m legion-laptop -v 6.0.0
    dkms install -m legion-laptop -v 6.0.0

    # Load module
    modprobe legion_laptop_16irx9 || echo "Note: Kernel module will be available after reboot"
fi

# Enable and start AI service
if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload
    systemctl enable legion-ai-optimizer.service
    echo "AI optimization service enabled (start with: systemctl start legion-ai-optimizer)"
fi

# Update icon cache
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache /usr/share/icons/hicolor/ || true
fi

# Update desktop database
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications/ || true
fi

echo "Legion Toolkit installation completed!"
echo "Run 'legion-toolkit-gui' to start the GUI application"
echo "Run 'legion-toolkit-cli --help' for command line usage"

exit 0
EOF
    chmod 755 "${PKG_DIR}/DEBIAN/postinst"

    # Create prerm script
    cat > "${PKG_DIR}/DEBIAN/prerm" << 'EOF'
#!/bin/bash
set -e

# Stop AI service
if command -v systemctl >/dev/null 2>&1; then
    systemctl stop legion-ai-optimizer.service || true
    systemctl disable legion-ai-optimizer.service || true
fi

# Remove kernel module
if command -v dkms >/dev/null 2>&1; then
    dkms remove legion-laptop/6.0.0 --all || true
fi

exit 0
EOF
    chmod 755 "${PKG_DIR}/DEBIAN/prerm"

    # Build package
    dpkg-deb --build "${PKG_DIR}"

    echo -e "${GREEN}DEB package created: ${PKG_NAME}_${VERSION}_amd64.deb${NC}"
}

# Create RPM package
create_rpm_package() {
    echo -e "${YELLOW}Creating RPM package...${NC}"

    # Create RPM build structure
    RPM_BUILD_DIR="${BUILD_DIR}/rpm"
    mkdir -p "${RPM_BUILD_DIR}"/{BUILD,RPMS,SOURCES,SPECS,SRPMS}

    # Create tarball
    TARBALL="${PKG_NAME}-${VERSION}.tar.gz"
    tar -czf "${RPM_BUILD_DIR}/SOURCES/${TARBALL}" \
        --transform "s,^,${PKG_NAME}-${VERSION}/," \
        hardware/ ai/ cli/ gui/ kernel_module/ \
        "$BUILD_DIR/legion-toolkit-gui" \
        "$BUILD_DIR/legion-toolkit-cli" \
        "$BUILD_DIR/legion-ai-optimizer" \
        "$BUILD_DIR"/*.desktop \
        "$BUILD_DIR/legion-ai-optimizer.service" \
        "$BUILD_DIR/README.md"

    # Create spec file
    cat > "${RPM_BUILD_DIR}/SPECS/${PKG_NAME}.spec" << EOF
Name:           ${PKG_NAME}
Version:        ${VERSION}
Release:        1%{?dist}
Summary:        Advanced hardware control for Legion laptops
License:        GPL-3.0
URL:            https://github.com/vivekchamoli/LenovoLegion7i
Source0:        %{name}-%{version}.tar.gz

BuildRequires:  python3-devel >= 3.10
BuildRequires:  desktop-file-utils
BuildRequires:  systemd-rpm-macros

Requires:       python3 >= 3.10
Requires:       python3-gobject
Requires:       gtk4
Requires:       libadwaita
Requires:       dkms
Requires:       kernel-devel
Recommends:     nvidia-driver

%description
Legion Toolkit provides comprehensive hardware control for Lenovo Legion
laptops including performance tuning, thermal management, RGB lighting,
and AI-powered optimization. Specifically optimized for Legion Slim 7i
Gen 9 (16IRX9) with Intel Core i9-14900HX and NVIDIA RTX 4070.

%prep
%autosetup

%build
# Nothing to build - Python scripts

%install
rm -rf \$RPM_BUILD_ROOT

# Create directories
install -d \$RPM_BUILD_ROOT%{_bindir}
install -d \$RPM_BUILD_ROOT%{_libdir}/%{name}
install -d \$RPM_BUILD_ROOT%{_datadir}/applications
install -d \$RPM_BUILD_ROOT%{_datadir}/icons/hicolor/256x256/apps
install -d \$RPM_BUILD_ROOT%{_docdir}/%{name}
install -d \$RPM_BUILD_ROOT%{_mandir}/man1
install -d \$RPM_BUILD_ROOT%{_unitdir}
install -d \$RPM_BUILD_ROOT%{_usrsrc}/legion-laptop-%{version}

# Install executables
install -m 755 legion-toolkit-gui \$RPM_BUILD_ROOT%{_bindir}/
install -m 755 legion-toolkit-cli \$RPM_BUILD_ROOT%{_bindir}/
install -m 755 legion-ai-optimizer \$RPM_BUILD_ROOT%{_bindir}/

# Install Python modules
cp -r hardware/ ai/ cli/ gui/ \$RPM_BUILD_ROOT%{_libdir}/%{name}/

# Install desktop files
install -m 644 *.desktop \$RPM_BUILD_ROOT%{_datadir}/applications/

# Install systemd service
install -m 644 legion-ai-optimizer.service \$RPM_BUILD_ROOT%{_unitdir}/

# Install documentation
install -m 644 README.md \$RPM_BUILD_ROOT%{_docdir}/%{name}/

# Install kernel module source
cp -r kernel_module/* \$RPM_BUILD_ROOT%{_usrsrc}/legion-laptop-%{version}/

%post
# Install kernel module
if command -v dkms >/dev/null 2>&1; then
    dkms add -m legion-laptop -v %{version}
    dkms build -m legion-laptop -v %{version}
    dkms install -m legion-laptop -v %{version}
    modprobe legion_laptop_16irx9 || true
fi

# Enable AI service
%systemd_post legion-ai-optimizer.service

%preun
%systemd_preun legion-ai-optimizer.service

%postun
%systemd_postun_with_restart legion-ai-optimizer.service

# Remove kernel module
if command -v dkms >/dev/null 2>&1; then
    dkms remove legion-laptop/%{version} --all || true
fi

%files
%license LICENSE
%doc README.md
%{_bindir}/legion-toolkit-gui
%{_bindir}/legion-toolkit-cli
%{_bindir}/legion-ai-optimizer
%{_libdir}/%{name}/
%{_datadir}/applications/*.desktop
%{_datadir}/icons/hicolor/256x256/apps/legion-toolkit.png
%{_unitdir}/legion-ai-optimizer.service
%{_usrsrc}/legion-laptop-%{version}/

%changelog
* $(date +'%a %b %d %Y') Vivek Chamoli <vivek@legion-toolkit.org> - ${VERSION}-1
- Initial release for Legion Slim 7i Gen 9
- Complete feature parity with Windows version
- AI-powered thermal optimization
- Full RGB Spectrum keyboard support
- Command line interface
- Real-time hardware monitoring
EOF

    # Build RPM
    rpmbuild --define "_topdir ${RPM_BUILD_DIR}" -ba "${RPM_BUILD_DIR}/SPECS/${PKG_NAME}.spec"

    # Copy to dist directory
    cp "${RPM_BUILD_DIR}/RPMS/x86_64/${PKG_NAME}-${VERSION}-1."*.rpm "$DIST_DIR/"

    echo -e "${GREEN}RPM package created${NC}"
}

# Create AppImage
create_appimage() {
    echo -e "${YELLOW}Creating AppImage...${NC}"

    # Download AppImage tools
    APPIMAGE_TOOL="appimagetool-x86_64.AppImage"
    if [ ! -f "$BUILD_DIR/$APPIMAGE_TOOL" ]; then
        wget -q "https://github.com/AppImage/AppImageKit/releases/download/continuous/$APPIMAGE_TOOL" \
             -O "$BUILD_DIR/$APPIMAGE_TOOL"
        chmod +x "$BUILD_DIR/$APPIMAGE_TOOL"
    fi

    # Create AppDir structure
    APPDIR="${BUILD_DIR}/AppDir"
    mkdir -p "$APPDIR/usr/bin"
    mkdir -p "$APPDIR/usr/lib"
    mkdir -p "$APPDIR/usr/share/applications"
    mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"

    # Copy executables
    cp "$BUILD_DIR/legion-toolkit-gui" "$APPDIR/usr/bin/"
    cp "$BUILD_DIR/legion-toolkit-cli" "$APPDIR/usr/bin/"

    # Copy Python modules
    cp -r hardware/ ai/ cli/ gui/ "$APPDIR/usr/lib/"

    # Copy desktop file (main one)
    cp "$BUILD_DIR/legion-toolkit.desktop" "$APPDIR/"
    cp "$BUILD_DIR/legion-toolkit.desktop" "$APPDIR/usr/share/applications/"

    # Copy icon
    cp "${DIST_DIR}/${PKG_NAME}_${VERSION}_amd64/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png" \
       "$APPDIR/usr/share/icons/hicolor/256x256/apps/"
    cp "$APPDIR/usr/share/icons/hicolor/256x256/apps/legion-toolkit.png" "$APPDIR/"

    # Create AppRun
    cat > "$APPDIR/AppRun" << 'EOF'
#!/bin/bash
SELF=$(readlink -f "$0")
HERE=${SELF%/*}

# Set up environment
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib:${LD_LIBRARY_PATH}"
export PYTHONPATH="${HERE}/usr/lib:${PYTHONPATH}"
export GI_TYPELIB_PATH="${HERE}/usr/lib/girepository-1.0:/usr/lib/x86_64-linux-gnu/girepository-1.0"

# Check for root privileges
if [ "$EUID" -ne 0 ]; then
    echo "Legion Toolkit requires root privileges for hardware access."
    echo "Please run: sudo $0"
    exit 1
fi

# Run the application
exec "${HERE}/usr/bin/legion-toolkit-gui" "$@"
EOF
    chmod +x "$APPDIR/AppRun"

    # Build AppImage
    "$BUILD_DIR/$APPIMAGE_TOOL" "$APPDIR" "$DIST_DIR/${PKG_NAME}-${VERSION}-x86_64.AppImage"

    echo -e "${GREEN}AppImage created: ${PKG_NAME}-${VERSION}-x86_64.AppImage${NC}"
}

# Create installer script
create_installer_script() {
    echo -e "${YELLOW}Creating universal installer script...${NC}"

    cat > "$DIST_DIR/install-legion-toolkit.sh" << EOF
#!/bin/bash
# Legion Toolkit Universal Installer
# Automatically detects distribution and installs appropriate package

set -e

VERSION="${VERSION}"
SCRIPT_DIR="\$(cd "\$(dirname "\${BASH_SOURCE[0]}")" && pwd)"

echo "Legion Toolkit v\${VERSION} - Universal Installer"
echo "=================================================="

# Check for root privileges
if [ "\$EUID" -ne 0 ]; then
    echo "Error: This installer requires root privileges"
    echo "Please run: sudo \$0"
    exit 1
fi

# Detect distribution
if [ -f /etc/os-release ]; then
    . /etc/os-release
    DISTRO=\$ID
    VERSION_ID=\$VERSION_ID
else
    echo "Error: Cannot detect Linux distribution"
    exit 1
fi

echo "Detected distribution: \$DISTRO \$VERSION_ID"

case \$DISTRO in
    ubuntu|debian)
        echo "Installing DEB package..."
        PKG_FILE="${PKG_NAME}_${VERSION}_amd64.deb"

        if [ -f "\$SCRIPT_DIR/\$PKG_FILE" ]; then
            dpkg -i "\$SCRIPT_DIR/\$PKG_FILE"
            apt-get install -f -y
        else
            echo "Error: \$PKG_FILE not found"
            exit 1
        fi
        ;;

    fedora|rhel|centos|rocky|almalinux)
        echo "Installing RPM package..."
        PKG_FILE="${PKG_NAME}-${VERSION}-1.*.rpm"

        if ls "\$SCRIPT_DIR"/\$PKG_FILE 1> /dev/null 2>&1; then
            rpm -ivh "\$SCRIPT_DIR"/\$PKG_FILE
        else
            echo "Error: RPM package not found"
            exit 1
        fi
        ;;

    arch|manjaro)
        echo "Arch Linux detected - using AppImage"
        echo "Note: Install manually using AUR or AppImage"
        ;;

    *)
        echo "Unsupported distribution: \$DISTRO"
        echo "Falling back to AppImage..."

        APPIMAGE_FILE="${PKG_NAME}-${VERSION}-x86_64.AppImage"
        if [ -f "\$SCRIPT_DIR/\$APPIMAGE_FILE" ]; then
            cp "\$SCRIPT_DIR/\$APPIMAGE_FILE" /usr/local/bin/legion-toolkit
            chmod +x /usr/local/bin/legion-toolkit
            echo "AppImage installed to /usr/local/bin/legion-toolkit"
        else
            echo "Error: AppImage not found"
            exit 1
        fi
        ;;
esac

echo "Installation completed successfully!"
echo ""
echo "Usage:"
echo "  GUI: legion-toolkit-gui"
echo "  CLI: legion-toolkit-cli --help"
echo "  AI Service: systemctl start legion-ai-optimizer"
echo ""
echo "For more information, visit:"
echo "https://github.com/vivekchamoli/LenovoLegion7i"
EOF

    chmod +x "$DIST_DIR/install-legion-toolkit.sh"

    echo -e "${GREEN}Universal installer created${NC}"
}

# Generate checksums
generate_checksums() {
    echo -e "${YELLOW}Generating checksums...${NC}"

    cd "$DIST_DIR"

    # Generate SHA256 checksums
    sha256sum *.deb *.rpm *.AppImage *.sh > SHA256SUMS 2>/dev/null || true

    echo -e "${GREEN}Checksums generated${NC}"
    cd - > /dev/null
}

# Main build function
main() {
    echo -e "${BLUE}Starting complete Linux build process...${NC}"

    # Build steps
    check_dependencies
    clean_build
    build_kernel_module
    setup_python_env
    build_gui_application
    build_cli_application
    build_ai_service
    create_desktop_files
    create_documentation
    create_deb_package
    create_rpm_package
    create_appimage
    create_installer_script
    generate_checksums

    echo -e "${GREEN}========================================${NC}"
    echo -e "${GREEN}Build completed successfully!${NC}"
    echo -e "${GREEN}========================================${NC}"
    echo ""
    echo -e "${YELLOW}Build outputs in ${DIST_DIR}:${NC}"
    ls -la "$DIST_DIR"
    echo ""
    echo -e "${YELLOW}Installation options:${NC}"
    echo "1. Ubuntu/Debian: sudo dpkg -i ${PKG_NAME}_${VERSION}_amd64.deb"
    echo "2. Fedora/RHEL:   sudo rpm -ivh ${PKG_NAME}-${VERSION}-1.*.rpm"
    echo "3. Universal:     sudo ./install-legion-toolkit.sh"
    echo "4. AppImage:      sudo ./${PKG_NAME}-${VERSION}-x86_64.AppImage"
    echo ""
    echo -e "${GREEN}Legion Toolkit Linux build complete with full Windows feature parity!${NC}"
}

# Run main function
main "$@"