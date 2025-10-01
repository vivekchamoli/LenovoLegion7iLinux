#!/bin/bash

# Quick rebuild script for the Debian package with fixed dependencies
# Run this on your Linux system

set -e

echo "🔧 Legion Toolkit - Quick Package Rebuild"
echo "=========================================="

PACKAGE_DIR="LenovoLegionToolkit.Avalonia/publish/deb/legion-toolkit"

# Check if the package directory exists
if [ ! -d "$PACKAGE_DIR" ]; then
    echo "❌ Error: Package directory not found: $PACKAGE_DIR"
    echo "   Please run from the project root directory"
    exit 1
fi

echo "📦 Rebuilding Debian package..."

# Navigate to parent directory
cd "LenovoLegionToolkit.Avalonia/publish/deb"

# Remove old .deb if exists
rm -f *.deb

# Build the package
dpkg-deb --build legion-toolkit

# Rename to proper version
if [ -f "legion-toolkit.deb" ]; then
    mv legion-toolkit.deb ../../../legion-toolkit_3.0.0_amd64.deb
    echo "✅ Package built successfully!"
    echo "   Output: legion-toolkit_3.0.0_amd64.deb"
    echo ""
    echo "📋 To install:"
    echo "   sudo dpkg -i legion-toolkit_3.0.0_amd64.deb"
    echo "   sudo apt-get install -f  # Fix any dependency issues"
else
    echo "❌ Error: Package build failed"
    exit 1
fi
