#!/bin/bash
# Apply the libicu dependency fix to existing package files

set -e

echo "=== Applying libicu Dependency Fix ==="

# Function to update debian/control
update_debian_control() {
    if [ -f "debian/control" ]; then
        echo "Updating debian/control..."

        # Backup original
        cp debian/control debian/control.backup

        # Create updated control file with flexible libicu dependencies
        cat > debian/control << 'EOF'
Source: legion-toolkit
Section: utils
Priority: optional
Maintainer: Vivek Chamoli <vivekchamoli@github.com>
Build-Depends: debhelper (>= 10),
               dotnet-sdk-8.0,
               libx11-dev,
               libxrandr-dev,
               libxi-dev
Standards-Version: 4.5.1
Homepage: https://github.com/vivekchamoli/LenovoLegion7i
Vcs-Browser: https://github.com/vivekchamoli/LenovoLegion7i
Vcs-Git: https://github.com/vivekchamoli/LenovoLegion7i.git

Package: legion-toolkit
Architecture: amd64
Depends: ${shlibs:Depends},
         ${misc:Depends},
         libicu74 | libicu72 | libicu70 | libicu67 | libicu66 | libicu60,
         libssl3 | libssl1.1,
         libstdc++6,
         libc6,
         ca-certificates
Recommends: dotnet-runtime-8.0,
           legion-laptop-dkms,
           libx11-6,
           libxrandr2,
           libxi6,
           libnotify-bin,
           x11-utils,
           ddcutil
Suggests: nvidia-settings,
         redshift
Description: Comprehensive control utility for Lenovo Legion laptops
 Legion Toolkit provides a graphical and command-line interface for
 controlling various hardware features of Lenovo Legion laptops on Linux.
 .
 Features:
  - Power mode control (Quiet, Balanced, Performance)
  - Battery management (charge limits, conservation mode)
  - Thermal monitoring and fan control
  - RGB keyboard lighting control
  - Display refresh rate and color management
  - Automation profiles and rules
  - System tray integration
  - Comprehensive CLI support
 .
 This package requires the legion-laptop kernel module for full functionality.
 Compatible with Ubuntu 18.04+ through flexible library dependencies.
EOF

        echo "✓ Updated debian/control with flexible libicu dependencies"
    else
        echo "⚠ debian/control not found"
    fi
}

# Function to update build scripts
update_build_scripts() {
    # Update Scripts/build-deb.sh
    if [ -f "Scripts/build-deb.sh" ]; then
        echo "Updating Scripts/build-deb.sh..."

        # Backup original
        cp Scripts/build-deb.sh Scripts/build-deb.sh.backup

        # Replace the problematic dependency line
        sed -i 's/Depends: libc6 (>= 2.31), libgcc-s1 (>= 3.0), libstdc++6 (>= 5.2), libicu67 | libicu66 | libicu70, libssl1.1 | libssl3, ca-certificates/Depends: libc6, libgcc-s1, libstdc++6, libicu74 | libicu72 | libicu70 | libicu67 | libicu66 | libicu60, libssl3 | libssl1.1, ca-certificates/' Scripts/build-deb.sh

        echo "✓ Updated Scripts/build-deb.sh"
    fi

    # Update Scripts/build-ubuntu-package.sh
    if [ -f "Scripts/build-ubuntu-package.sh" ]; then
        echo "Updating Scripts/build-ubuntu-package.sh..."

        # Backup original
        cp Scripts/build-ubuntu-package.sh Scripts/build-ubuntu-package.sh.backup

        # Update dependencies in the script
        sed -i 's/Depends: dotnet-runtime-8.0, libx11-6, libxrandr2, libxi6, libnotify-bin, x11-utils, ddcutil, redshift/Depends: libicu74 | libicu72 | libicu70 | libicu67 | libicu66 | libicu60, libssl3 | libssl1.1, dotnet-runtime-8.0, libx11-6, libxrandr2, libxi6, libnotify-bin, x11-utils, ddcutil, redshift/' Scripts/build-ubuntu-package.sh

        echo "✓ Updated Scripts/build-ubuntu-package.sh"
    fi
}

# Function to create a quick fix script for immediate use
create_quick_fix() {
    cat > fix-current-package.sh << 'EOF'
#!/bin/bash
# Quick fix for existing .deb package dependency issue

echo "Quick Fix for libicu Dependencies"
echo "=================================="

# Check what libicu packages are available
echo "Available libicu packages on your system:"
apt-cache search '^libicu[0-9]+$' | sort

echo ""
echo "To fix the installation issue, install the available libicu package first:"

# Find available libicu and install it
AVAILABLE_LIBICU=$(apt-cache search '^libicu[0-9]+$' | tail -1 | awk '{print $1}')

if [ -n "$AVAILABLE_LIBICU" ]; then
    echo "Installing $AVAILABLE_LIBICU..."
    sudo apt update
    sudo apt install -y "$AVAILABLE_LIBICU"

    echo ""
    echo "Now try installing your legion-toolkit package again:"
    echo "sudo dpkg -i legion-toolkit_*.deb"
    echo "sudo apt-get install -f"
else
    echo "No libicu package found. Your system may need manual dependency resolution."
fi
EOF

    chmod +x fix-current-package.sh
    echo "✓ Created fix-current-package.sh for immediate use"
}

# Main execution
echo "Starting dependency fix application..."

update_debian_control
update_build_scripts
create_quick_fix

echo ""
echo "=== Fix Applied Successfully ==="
echo ""
echo "Changes made:"
echo "1. ✓ Updated debian/control with flexible libicu dependencies"
echo "2. ✓ Updated build scripts with compatible dependencies"
echo "3. ✓ Created quick fix script for immediate use"
echo ""
echo "Next steps:"
echo "1. For immediate fix: ./fix-current-package.sh"
echo "2. For new packages: Rebuild using updated scripts"
echo ""
echo "Backup files created with .backup extension"