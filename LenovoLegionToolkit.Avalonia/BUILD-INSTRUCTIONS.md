# Legion Toolkit for Linux - Build Instructions

## Complete Build and Package Creation

This script creates production-ready packages for Linux distribution:

```bash
./build-linux-complete.sh
```

## What it Creates

### Binaries
- `publish/linux-x64/LegionToolkit` - x86_64 executable (20MB)
- `publish/linux-arm64/LegionToolkit` - ARM64 executable

### Installation Packages
- `publish/legion-toolkit_3.0.0_amd64.deb` - Debian/Ubuntu package
- `publish/rpm-package/` - RPM package templates
- `publish/AppImage/` - Portable AppImage structure

### Installation Scripts
- `publish/install-system.sh` - Universal system installer
- `publish/uninstall-system.sh` - Complete removal script
- `publish/test-binary.sh` - Test without installation

### Documentation
- `publish/README.md` - Complete installation guide
- Man pages and desktop entries included

## Quick Installation Test

After building:

```bash
cd publish
sudo ./install-system.sh
sudo usermod -a -G legion $USER
# Log out and log back in
legion-toolkit
```

## Package Features

### Debian Package (.deb)
- Proper dependencies and recommendations
- Automatic group creation and udev rules
- Systemd service integration
- Man pages and desktop entries
- Post-install configuration

### System Installer (install-system.sh)
- Multi-architecture support (x64/ARM64)
- Hardware access setup
- Group and permissions management
- Kernel module loading
- Desktop integration

### AppImage (Portable)
- Self-contained executable
- No system installation required
- Limited hardware access

## Hardware Requirements

### Essential
- Lenovo Legion laptop (Generation 6+)
- Linux kernel 5.4+
- .NET 8 runtime (included in build)

### Recommended
- legion-laptop kernel module
- User in 'legion' group for hardware access
- systemd for service management

## Build Requirements

### System
- .NET 8 SDK
- bash shell
- dpkg tools (for .deb creation)
- standard Linux utilities

### Install .NET 8 SDK

**Ubuntu/Debian:**
```bash
sudo apt update
sudo apt install dotnet-sdk-8.0
```

**Fedora:**
```bash
sudo dnf install dotnet-sdk-8.0
```

**Arch:**
```bash
sudo pacman -S dotnet-sdk
```

**Manual:**
```bash
wget https://dotnet.microsoft.com/download/dotnet/8.0
```

## Advanced Usage

### Custom Version
Edit the script to change VERSION variable:
```bash
VERSION="3.1.0"  # Change this line in the script
```

### Selective Building
The script supports commenting out sections you don't need:
- Debian package creation
- RPM spec generation
- AppImage structure
- ARM64 builds

### Cross-Platform Building
Run on any Linux system to create packages for distribution.

## Troubleshooting

### Build Fails
- Ensure .NET 8 SDK is installed
- Check script permissions: `chmod +x build-linux-complete.sh`
- Run from correct directory (where .csproj exists)

### Package Installation Issues
- Use system installer: `sudo ./install-system.sh`
- Check group membership: `groups $USER`
- Verify udev rules: `sudo udevadm control --reload-rules`

### Runtime Issues
- Load kernel module: `sudo modprobe legion-laptop`
- Check permissions: `ls -la /sys/kernel/legion_laptop/`
- Test CLI first: `legion-toolkit --help`

## Development

### Source Code Structure
```
LenovoLegionToolkit.Avalonia/
├── Services/Linux/          # Linux-specific implementations
├── Views/                   # Avalonia UI views
├── ViewModels/             # MVVM view models
├── Models/                 # Data models
├── Utils/                  # Utility classes
├── CLI/                    # Command-line interface
└── build-linux-complete.sh # This build script
```

### Key Features
- **Hardware Detection:** Automatic Legion laptop identification
- **Thermal Management:** Real-time temperature monitoring and fan control
- **RGB Control:** Support for 4-zone and per-key RGB keyboards
- **Power Management:** Battery conservation and power mode switching
- **Graphics Control:** Hybrid mode and discrete GPU management
- **Automation:** Rule-based triggers and actions
- **CLI Interface:** Complete command-line control
- **System Integration:** Tray icon and systemd service

This creates a complete, production-ready Linux application for Lenovo Legion laptop management.

## Author

**Vivek Chamoli** - Linux Implementation and Packaging
Email: vivekchamoli@outlook.com
GitHub: https://github.com/vivekchamoli