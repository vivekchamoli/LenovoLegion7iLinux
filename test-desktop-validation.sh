#!/bin/bash
# Desktop File Validation Test Script
# Tests the desktop file for proper validation

echo "🧪 Testing Desktop File Validation"
echo "=================================="

# Extract the desktop file content for testing
TEMP_DIR=$(mktemp -d)
DESKTOP_FILE="$TEMP_DIR/legion-toolkit.desktop"

# Create the desktop file content (extracted from build script)
cat > "$DESKTOP_FILE" << 'EOF'
[Desktop Entry]
Version=1.0
Type=Application
Name=Legion Toolkit
GenericName=Legion Laptop Management
Comment=System management tool for Lenovo Legion laptops - Thermal, RGB, Battery Control
Exec=/usr/bin/legion-toolkit-gui %F
Icon=legion-toolkit
Categories=System;
Keywords=legion;lenovo;laptop;thermal;rgb;battery;performance;gaming;
MimeType=application/x-legion-profile;
Terminal=false
StartupNotify=true
StartupWMClass=LegionToolkit
X-GNOME-SingleWindow=true
X-KDE-StartupNotify=true
TryExec=/usr/bin/legion-toolkit-gui
Actions=PowerQuiet;PowerBalanced;PowerPerformance;BatteryConservation;OpenCLI;DirectLaunch;

[Desktop Action PowerQuiet]
Name=Set Quiet Mode
Exec=/usr/bin/LegionToolkit power set quiet
Icon=legion-toolkit

[Desktop Action PowerBalanced]
Name=Set Balanced Mode
Exec=/usr/bin/LegionToolkit power set balanced
Icon=legion-toolkit

[Desktop Action PowerPerformance]
Name=Set Performance Mode
Exec=/usr/bin/LegionToolkit power set performance
Icon=legion-toolkit

[Desktop Action BatteryConservation]
Name=Toggle Battery Conservation
Exec=/usr/bin/LegionToolkit battery conservation toggle
Icon=legion-toolkit

[Desktop Action OpenCLI]
Name=Open CLI Terminal
Exec=x-terminal-emulator -e /usr/bin/legion-toolkit --help
Icon=utilities-terminal

[Desktop Action DirectLaunch]
Name=Direct Launch (Debug)
Exec=/usr/bin/LegionToolkit
Icon=legion-toolkit
EOF

echo "📄 Created test desktop file: $DESKTOP_FILE"
echo ""

# Test with desktop-file-validate if available
if command -v desktop-file-validate >/dev/null 2>&1; then
    echo "🔍 Running desktop-file-validate..."
    echo ""

    if desktop-file-validate "$DESKTOP_FILE" 2>&1; then
        echo ""
        echo "✅ Desktop file validation PASSED!"
    else
        echo ""
        echo "❌ Desktop file validation FAILED!"
        echo ""
        echo "🔧 Validation output above shows specific issues to fix."
    fi
else
    echo "⚠️  desktop-file-validate not available"
    echo "Install with: sudo apt install desktop-file-utils"
fi

echo ""
echo "📋 Desktop file content:"
echo "========================"
cat "$DESKTOP_FILE"

# Cleanup
rm -rf "$TEMP_DIR"

echo ""
echo "🎯 Test completed!"