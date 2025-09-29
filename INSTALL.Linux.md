# Legion Toolkit for Linux - Installation Guide

## System Requirements

- **OS**: Linux (Ubuntu 20.04+, Fedora 35+, Arch, or other modern distributions)
- **Hardware**: Lenovo Legion laptop (5, 7, 9 series supported)
- **Runtime**: .NET 8.0 Runtime (included in self-contained build)
- **Kernel Module**: `legion-laptop` kernel module (recommended)
- **Permissions**: Root access required for hardware control

## Installation Methods

### Method 1: AppImage (Recommended for most users)

1. Download the latest release from the releases page
2. Make the AppImage executable:
   ```bash
   chmod +x LegionToolkit-*.AppImage
   ```
3. Run the installer:
   ```bash
   ./install.sh
   ```

The installer will:
- Copy the AppImage to `~/.local/share/LegionToolkit/`
- Create a desktop launcher
- Add a command-line shortcut (`legion-toolkit`)

### Method 2: Manual Installation from Binary

1. Download and extract the release archive:
   ```bash
   tar -xzf LegionToolkit-linux-x64.tar.gz
   cd LegionToolkit
   ```

2. Run with root privileges:
   ```bash
   sudo ./LegionToolkit
   ```

### Method 3: Build from Source

1. Install prerequisites:
   ```bash
   # Ubuntu/Debian
   sudo apt update
   sudo apt install dotnet-sdk-8.0 git

   # Fedora
   sudo dnf install dotnet-sdk-8.0 git

   # Arch
   sudo pacman -S dotnet-sdk git
   ```

2. Clone and build:
   ```bash
   git clone https://github.com/[your-repo]/LenovoLegion7iToolkit.git
   cd LenovoLegion7iToolkit
   cd LenovoLegionToolkit.Avalonia
   dotnet publish -c Release -r linux-x64 --self-contained true
   ```

3. Run the built application:
   ```bash
   sudo ./publish/linux-x64/LegionToolkit
   ```

## Kernel Module Setup

For full hardware control, install the `legion-laptop` kernel module:

### Ubuntu/Debian:
```bash
sudo apt install dkms git
git clone https://github.com/johnfanv2/LenovoLegionLinux.git
cd LenovoLegionLinux/kernel_module
sudo make dkms
```

### Fedora:
```bash
sudo dnf install dkms kernel-devel git
git clone https://github.com/johnfanv2/LenovoLegionLinux.git
cd LenovoLegionLinux/kernel_module
sudo make dkms
```

### Arch:
```bash
yay -S legion-laptop-dkms-git
```

## Command Line Usage

Legion Toolkit includes a comprehensive CLI interface:

```bash
# Check status
legion-toolkit status

# Power management
legion-toolkit power --mode performance
legion-toolkit power --mode balanced
legion-toolkit power --mode quiet

# Battery management
legion-toolkit battery --conservation on
legion-toolkit battery --rapid-charge on
legion-toolkit battery --charge-limit 80

# Fan control
legion-toolkit fan --mode auto
legion-toolkit fan --mode manual --speed 50

# Display management
legion-toolkit display --refresh 165
legion-toolkit display --overdrive on

# RGB Keyboard (if supported)
legion-toolkit rgb --effect static --color FF0000
legion-toolkit rgb --brightness 2

# Run as daemon
legion-toolkit daemon --start
```

## Troubleshooting

### Permission Issues

Legion Toolkit requires root access for hardware control:

```bash
# Run with sudo
sudo legion-toolkit

# Or use pkexec for GUI authentication
pkexec legion-toolkit
```

### Missing Dependencies

If you encounter library errors:

```bash
# Ubuntu/Debian
sudo apt install libicu70 libssl3

# Fedora
sudo dnf install libicu openssl

# Arch
sudo pacman -S icu openssl
```

### Kernel Module Not Detected

Check if the module is loaded:
```bash
lsmod | grep legion
```

If not loaded:
```bash
sudo modprobe legion-laptop
```

### Application Won't Start

Check logs:
```bash
journalctl -xe | grep -i legion
```

Enable debug logging:
```bash
LEGION_DEBUG=1 sudo legion-toolkit
```

## Uninstallation

### For AppImage Installation:
```bash
./uninstall.sh
```

### For Manual Installation:
Simply delete the application directory.

## Support

- Check existing issues on GitHub
- Join the community discussions
- Review the documentation at `/Documentation`

## Security Notes

- Legion Toolkit requires root privileges to access hardware interfaces
- The application only modifies Lenovo-specific hardware settings
- No personal data is collected or transmitted
- All settings are stored locally in `~/.config/LegionToolkit/`

## License

This software is provided as-is for managing Lenovo Legion laptops on Linux.
Use at your own risk. Always ensure you have proper cooling when adjusting fan curves.