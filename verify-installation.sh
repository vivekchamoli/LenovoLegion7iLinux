#!/bin/bash
# Verification script for Legion Toolkit Linux build

echo "Legion Toolkit Linux Build Verification"
echo "========================================"
echo ""

# Repository information
REPO="https://github.com/vivekchamoli/LenovoLegion7i"
echo "✓ Repository: $REPO"

# Check if the installer script is accessible
INSTALLER_URL="https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7i/main/Scripts/ubuntu-installer.sh"
echo ""
echo "Checking installer availability..."
if curl --output /dev/null --silent --head --fail "$INSTALLER_URL"; then
    echo "✓ Installer script is accessible at:"
    echo "  $INSTALLER_URL"
else
    echo "✗ Installer script not found at expected URL"
fi

echo ""
echo "Installation command:"
echo "  wget -O - $INSTALLER_URL | bash"
echo ""
echo "Or clone and build locally:"
echo "  git clone $REPO.git"
echo "  cd LenovoLegion7i"
echo "  cd LenovoLegionToolkit.Avalonia"
echo "  dotnet build"
echo ""
echo "For Ubuntu package build:"
echo "  bash Scripts/build-ubuntu-package.sh"
echo ""
echo "Using Makefile:"
echo "  make"
echo "  sudo make install"