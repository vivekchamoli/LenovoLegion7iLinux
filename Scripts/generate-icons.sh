#!/bin/bash

# Generate PNG icons from SVG source
# Requires: inkscape or rsvg-convert

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
SVG_SOURCE="$PROJECT_DIR/LenovoLegionToolkit.Avalonia/Resources/icons/legion-toolkit.svg"
ICON_DIR="$PROJECT_DIR/LenovoLegionToolkit.Avalonia/Resources/icons"

echo "Generating PNG icons from SVG source..."

# Check for required tools
if command -v inkscape &> /dev/null; then
    CONVERTER="inkscape"
elif command -v rsvg-convert &> /dev/null; then
    CONVERTER="rsvg-convert"
else
    echo "Error: Neither inkscape nor rsvg-convert found. Please install one of them."
    echo "  Ubuntu/Debian: sudo apt install inkscape"
    echo "  Fedora: sudo dnf install inkscape"
    echo "  Or: sudo apt install librsvg2-bin"
    exit 1
fi

# Array of icon sizes
SIZES=(16 32 48 64 128 256 512)

# Generate icons
for size in "${SIZES[@]}"; do
    output_dir="$ICON_DIR/${size}x${size}"
    output_file="$output_dir/legion-toolkit.png"

    echo "Generating ${size}x${size} icon..."

    if [ "$CONVERTER" = "inkscape" ]; then
        inkscape "$SVG_SOURCE" \
            --export-type=png \
            --export-filename="$output_file" \
            --export-width=$size \
            --export-height=$size \
            2>/dev/null
    else
        rsvg-convert "$SVG_SOURCE" \
            -w $size \
            -h $size \
            -o "$output_file"
    fi

    if [ -f "$output_file" ]; then
        echo "  ✓ Created $output_file"
    else
        echo "  ✗ Failed to create $output_file"
    fi
done

# Create ICO file for Windows compatibility (optional)
if command -v convert &> /dev/null; then
    echo "Creating ICO file..."
    convert "$ICON_DIR/16x16/legion-toolkit.png" \
            "$ICON_DIR/32x32/legion-toolkit.png" \
            "$ICON_DIR/48x48/legion-toolkit.png" \
            "$ICON_DIR/256x256/legion-toolkit.png" \
            "$ICON_DIR/legion-toolkit.ico"
    echo "  ✓ Created legion-toolkit.ico"
fi

echo "Icon generation complete!"