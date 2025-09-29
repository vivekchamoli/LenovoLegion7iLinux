# Legion Toolkit for Linux

> **Complete Linux Implementation for Lenovo Legion Laptops**

This branch contains a fully functional Linux version of the Legion Toolkit, built with Avalonia UI and comprehensive hardware support for Lenovo Legion laptops.

## 🚀 Features

### ✅ **Thermal Management**
- Real-time CPU/GPU temperature monitoring
- Fan speed control and monitoring
- Custom thermal profiles
- Thermal throttling detection

### ✅ **RGB Keyboard Control**
- 4-zone RGB lighting support
- Per-key RGB support (where available)
- Multiple lighting effects and patterns
- Brightness control
- Custom color profiles

### ✅ **Power Management**
- Power mode switching (Quiet, Balanced, Performance, Custom)
- Battery conservation mode
- Rapid charge control
- Battery health monitoring
- Custom power limits

### ✅ **Graphics Control**
- Hybrid graphics mode switching
- Discrete GPU enable/disable
- GPU performance monitoring
- Display refresh rate control
- Panel overdrive settings

### ✅ **System Integration**
- Command-line interface (CLI)
- System tray integration
- Desktop notifications
- Systemd service support
- udev rules for hardware access

### ✅ **Automation**
- Rule-based automation system
- Trigger-based actions
- Profile switching
- Macro support
- IPC communication

## 🛠️ Installation

### Quick Installation

```bash
# Clone and build
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux
cd LenovoLegionToolkit.Avalonia

# Build and install
./build-linux-complete.sh
cd publish
sudo ./install-system.sh

# Add user to legion group
sudo usermod -a -G legion $USER
# Log out and log back in
legion-toolkit
```

### Package Installation

**Debian/Ubuntu:**
```bash
sudo dpkg -i publish/legion-toolkit_3.0.0_amd64.deb
sudo usermod -a -G legion $USER
```

**Universal Install:**
```bash
sudo ./publish/install-system.sh
sudo usermod -a -G legion $USER
```

## 📋 Requirements

### System Requirements
- **OS**: Linux kernel 5.4+ (Ubuntu 20.04+, Fedora 32+, etc.)
- **Architecture**: x86_64 or ARM64
- **Hardware**: Lenovo Legion laptop (Generation 6+)
- **Memory**: 4GB RAM minimum, 8GB recommended

### Build Requirements
- **.NET 8 SDK**
- **Git**
- **bash shell**
- **dpkg tools** (for .deb packages)

### Runtime Dependencies
- **legion-laptop kernel module** (recommended for full functionality)
- **User in 'legion' group** (for hardware access)
- **systemd** (for service management)

## 🏗️ Building from Source

### Prerequisites

**Install .NET 8 SDK:**

```bash
# Ubuntu/Debian
sudo apt update && sudo apt install dotnet-sdk-8.0

# Fedora
sudo dnf install dotnet-sdk-8.0

# Arch
sudo pacman -S dotnet-sdk

# Manual download
wget https://dotnet.microsoft.com/download/dotnet/8.0
```

### Build Process

```bash
# Clone repository
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux

# Navigate to Linux project
cd LenovoLegionToolkit.Avalonia

# Make build script executable
chmod +x build-linux-complete.sh

# Run complete build (creates packages)
./build-linux-complete.sh

# Or quick build for testing
./build-linux.sh
```

### Build Output

The build creates:
- **Native Binaries**: Self-contained executables for x64 and ARM64
- **Debian Package**: `legion-toolkit_3.0.0_amd64.deb`
- **Installation Scripts**: Universal installers and uninstallers
- **AppImage**: Portable application format
- **Documentation**: Complete setup and usage guides

## 🖥️ Usage

### GUI Application
```bash
# Launch GUI
legion-toolkit

# Launch minimized to tray
legion-toolkit --minimized

# Launch with debug logging
legion-toolkit --debug
```

### Command Line Interface
```bash
# Show help
legion-toolkit --help

# Check system status
legion-toolkit status

# Set power mode
legion-toolkit power-mode performance

# Control RGB
legion-toolkit rgb --color red --brightness 80

# Monitor temperatures
legion-toolkit thermal --monitor

# Control fans
legion-toolkit fan --speed 50

# Battery management
legion-toolkit battery --conservation on
```

### Daemon Mode
```bash
# Run as background service
legion-toolkit --daemon

# Enable systemd service
sudo systemctl enable legion-toolkit
sudo systemctl start legion-toolkit
```

## 🔧 Configuration

### Hardware Access Setup

The application requires proper permissions for hardware access:

```bash
# Add user to legion group (required)
sudo usermod -a -G legion $USER

# Load legion kernel module (recommended)
sudo modprobe legion-laptop

# Check module status
lsmod | grep legion
```

### Configuration Files

- **User Config**: `~/.config/legion-toolkit/`
- **System Config**: `/etc/legion-toolkit/`
- **Logs**: `~/.config/legion-toolkit/logs/`
- **Profiles**: `~/.config/legion-toolkit/profiles/`

### udev Rules

Automatic hardware access via udev rules:
```bash
# View installed rules
cat /etc/udev/rules.d/99-legion-toolkit.rules

# Reload rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```

## 🐛 Troubleshooting

### Common Issues

**Application won't start:**
```bash
# Check dependencies
legion-toolkit --version
dotnet --version

# Run with debug output
legion-toolkit --debug
```

**Permission denied errors:**
```bash
# Verify group membership
groups $USER

# Re-add to legion group
sudo usermod -a -G legion $USER
# Log out and log back in
```

**Hardware not detected:**
```bash
# Check legion module
sudo modprobe legion-laptop
lsmod | grep legion

# Check hardware paths
ls -la /sys/kernel/legion_laptop/
ls -la /sys/class/hwmon/
```

**Build failures:**
```bash
# Verify .NET installation
dotnet --list-sdks

# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Getting Help

- **Documentation**: Check `BUILD-INSTRUCTIONS.md`
- **Logs**: `~/.config/legion-toolkit/logs/`
- **Issues**: Open GitHub issue with logs attached
- **CLI Help**: `legion-toolkit --help`

## 🚦 Hardware Support

### Supported Models
- **Legion 5/5i/5 Pro** (Gen 6, 7, 8, 9)
- **Legion 7/7i** (Gen 6, 7, 8, 9)
- **Legion 9i** (Gen 9)
- **IdeaPad Gaming 3** (Selected models)
- **LOQ Series** (Selected models)

### Feature Matrix

| Feature | Gen 6 | Gen 7 | Gen 8 | Gen 9 |
|---------|-------|-------|-------|-------|
| Power Modes | ✅ | ✅ | ✅ | ✅ |
| Thermal Control | ✅ | ✅ | ✅ | ✅ |
| RGB 4-Zone | ✅ | ✅ | ✅ | ✅ |
| RGB Per-Key | ❌ | ✅ | ✅ | ✅ |
| Hybrid Graphics | ✅ | ✅ | ✅ | ✅ |
| Custom Mode | ❌ | ✅ | ✅ | ✅ |
| Rapid Charge | ✅ | ✅ | ✅ | ✅ |

## 🤝 Contributing

We welcome contributions to improve Linux support!

### Development Setup
```bash
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux
cd LenovoLegionToolkit.Avalonia

# Install development dependencies
dotnet restore
dotnet build
```

### Code Structure
```
LenovoLegionToolkit.Avalonia/
├── Services/Linux/          # Linux-specific implementations
├── Views/                   # Avalonia UI views
├── ViewModels/             # MVVM view models
├── Models/                 # Data models and enums
├── Utils/                  # Platform utilities
├── CLI/                    # Command-line interface
├── IPC/                    # Inter-process communication
└── Resources/              # Application resources
```

### Adding Hardware Support
1. Add hardware detection in `LinuxHardwareService.cs`
2. Implement feature in appropriate service
3. Add CLI commands if needed
4. Update capability detection
5. Test on target hardware

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- **Original Windows Version**: BartoszCichecki and contributors
- **Linux Implementation**: Vivek Chamoli
- **legion-laptop Module**: Maintainers and contributors
- **Avalonia UI**: Cross-platform UI framework team

---

**For detailed build instructions, see**: [`BUILD-INSTRUCTIONS.md`](BUILD-INSTRUCTIONS.md)

**For the complete build script, see**: [`build-linux-complete.sh`](build-linux-complete.sh)