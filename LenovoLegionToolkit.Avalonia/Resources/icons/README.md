# Legion Toolkit Icons

This directory contains the application icons in various sizes for Linux desktop integration.

## Icon Sizes

The following icon sizes are provided:
- 16x16 - Small icon for menus
- 32x32 - Standard toolbar size
- 48x48 - Large toolbar/dock size
- 64x64 - Desktop icon size
- 128x128 - Large desktop icon
- 256x256 - Extra large icon
- 512x512 - Maximum resolution icon
- Scalable (SVG) - Vector format for any size

## Generation

To generate PNG icons from the SVG source, run:
```bash
cd Scripts
./generate-icons.sh
```

This requires either `inkscape` or `rsvg-convert` to be installed.

## Manual Generation

If the script doesn't work, you can manually generate icons using:

### With Inkscape:
```bash
inkscape -w 256 -h 256 Resources/icons/legion-toolkit.svg -o Resources/icons/256x256/legion-toolkit.png
```

### With rsvg-convert:
```bash
rsvg-convert -w 256 -h 256 Resources/icons/legion-toolkit.svg -o Resources/icons/256x256/legion-toolkit.png
```

## Icon Theme Integration

The icons are installed to `/usr/share/icons/hicolor/` following the freedesktop.org icon theme specification.

After installation, the icon cache is updated with:
```bash
gtk-update-icon-cache /usr/share/icons/hicolor
```