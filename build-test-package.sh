#!/bin/bash
# Simple script to build the Legion Toolkit test package
# Run this on Ubuntu/Debian system

set -e

echo "Building Legion Toolkit Test Package..."

# Check if we're on a Debian-based system
if ! command -v dpkg-deb &> /dev/null; then
    echo "Error: dpkg-deb not found. This script must be run on Ubuntu/Debian."
    echo "Install with: sudo apt install dpkg-dev"
    exit 1
fi

# Build the package
echo "Creating .deb package..."
dpkg-deb --build legion-toolkit-test

# Check if package was created
if [ -f "legion-toolkit-test.deb" ]; then
    echo "✓ Package created successfully: legion-toolkit-test.deb"

    # Show package info
    echo ""
    echo "Package Information:"
    dpkg-deb --info legion-toolkit-test.deb

    echo ""
    echo "Package Contents:"
    dpkg-deb --contents legion-toolkit-test.deb

    echo ""
    echo "Installation Instructions:"
    echo "1. Copy this entire directory to an Ubuntu system"
    echo "2. Run: sudo dpkg -i legion-toolkit-test.deb"
    echo "3. If there are dependency issues, run: sudo apt-get install -f"
    echo "4. Test with: legion-toolkit-test"
    echo "5. GUI test with: legion-toolkit-test --gui"
    echo ""
    echo "Uninstall with: sudo apt remove legion-toolkit-test"
else
    echo "✗ Package creation failed"
    exit 1
fi