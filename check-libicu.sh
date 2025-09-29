#!/bin/bash
# Script to check available libicu packages and Ubuntu version

echo "=== Ubuntu Version Check ==="
lsb_release -a 2>/dev/null || cat /etc/os-release

echo ""
echo "=== Available libicu packages ==="
apt-cache search libicu | grep -E "^libicu[0-9]+" | sort

echo ""
echo "=== Installed libicu packages ==="
dpkg -l | grep libicu || echo "No libicu packages currently installed"

echo ""
echo "=== Recommended libicu for your system ==="
# Check what version is available
if apt-cache show libicu72 >/dev/null 2>&1; then
    echo "libicu72 is available (Ubuntu 22.04+)"
elif apt-cache show libicu70 >/dev/null 2>&1; then
    echo "libicu70 is available (Ubuntu 21.10+)"
elif apt-cache show libicu67 >/dev/null 2>&1; then
    echo "libicu67 is available (Ubuntu 20.04)"
elif apt-cache show libicu66 >/dev/null 2>&1; then
    echo "libicu66 is available (Ubuntu 20.04)"
elif apt-cache show libicu63 >/dev/null 2>&1; then
    echo "libicu63 is available (Ubuntu 18.04)"
else
    echo "No standard libicu version detected. Listing all available:"
    apt-cache search libicu | grep -E "libicu[0-9]+"
fi