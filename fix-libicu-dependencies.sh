#!/bin/bash
# Script to fix libicu dependency issues across Ubuntu versions

set -e

echo "=== Legion Toolkit libicu Dependency Fixer ==="

# Ubuntu version to libicu mapping
declare -A UBUNTU_LIBICU_MAP=(
    ["24.04"]="libicu74"
    ["23.10"]="libicu72"
    ["23.04"]="libicu72"
    ["22.04"]="libicu70"
    ["21.10"]="libicu70"
    ["21.04"]="libicu67"
    ["20.04"]="libicu66"
    ["18.04"]="libicu60"
)

# Function to detect Ubuntu version
get_ubuntu_version() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        echo "$VERSION_ID"
    else
        echo "unknown"
    fi
}

# Function to get available libicu packages
get_available_libicu() {
    apt-cache search '^libicu[0-9]+$' | awk '{print $1}' | sort -V
}

# Function to find best libicu match
find_best_libicu() {
    local available_packages=$(get_available_libicu)

    # Try in order of preference (newest first)
    for pkg in libicu74 libicu72 libicu70 libicu67 libicu66 libicu60; do
        if echo "$available_packages" | grep -q "^$pkg$"; then
            echo "$pkg"
            return 0
        fi
    done

    # If none of the standard versions found, return the newest available
    echo "$available_packages" | tail -1
}

# Main execution
echo "Detecting system..."
UBUNTU_VERSION=$(get_ubuntu_version)
echo "Ubuntu version: $UBUNTU_VERSION"

BEST_LIBICU=$(find_best_libicu)
echo "Best available libicu: $BEST_LIBICU"

# Create flexible dependency string
LIBICU_DEPS="$BEST_LIBICU"

# Add fallbacks for compatibility
for fallback in libicu74 libicu72 libicu70 libicu67 libicu66 libicu60; do
    if [ "$fallback" != "$BEST_LIBICU" ]; then
        LIBICU_DEPS="$LIBICU_DEPS | $fallback"
    fi
done

echo "Recommended dependency string: $LIBICU_DEPS"

# Create updated control file
cat > legion-toolkit-fixed-control << EOF
Package: legion-toolkit
Version: 6.0.0
Architecture: amd64
Maintainer: Legion Toolkit Team <legion-toolkit@community.org>
Depends: \${shlibs:Depends},
         \${misc:Depends},
         $LIBICU_DEPS,
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
Section: utils
Priority: optional
Homepage: https://github.com/vivekchamoli/LenovoLegion7i
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
 This package works across multiple Ubuntu versions by providing
 flexible dependencies for system libraries.
EOF

echo ""
echo "✓ Created fixed control file: legion-toolkit-fixed-control"
echo ""
echo "To apply this fix:"
echo "1. Copy the content of legion-toolkit-fixed-control"
echo "2. Replace your debian/control file (binary package section)"
echo "3. Rebuild your package"
echo ""
echo "Or run the automatic fix:"
echo "./apply-libicu-fix.sh"