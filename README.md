# Legion Toolkit for Linux

> **🐧 Complete Linux Implementation for Lenovo Legion Laptops**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Platform: Linux](https://img.shields.io/badge/Platform-Linux-blue.svg)](https://www.kernel.org/)

A comprehensive system management application for Lenovo Legion laptops running on Linux. Built with modern cross-platform technologies and native Linux integration.

![Legion Toolkit Screenshot](docs/screenshot.png)

## ✨ Features

### 🌡️ **Thermal Management**
- Real-time CPU/GPU temperature monitoring
- Fan speed control and monitoring
- Custom thermal profiles
- Thermal throttling detection
- Integration with hwmon and legion-laptop module

### 🌈 **RGB Keyboard Control**
- 4-zone RGB lighting support
- Per-key RGB support (where available)
- Multiple lighting effects and patterns
- Brightness control
- Custom color profiles and animations

### ⚡ **Power Management**
- Power mode switching (Quiet, Balanced, Performance, Custom)
- Battery conservation mode
- Rapid charge control
- Battery health monitoring
- Custom power limits and profiles

### 🎮 **Graphics Control**
- Hybrid graphics mode switching (Integrated/Discrete/Auto)
- Discrete GPU enable/disable
- GPU performance monitoring
- Display refresh rate control (60Hz/144Hz/165Hz/240Hz)
- Panel overdrive settings

### 🖥️ **System Integration**
- Command-line interface (CLI) for automation
- System tray integration with native notifications
- Desktop notifications for status changes
- Systemd service support for background operation
- udev rules for proper hardware access

### 🤖 **Automation & Scripting**
- Rule-based automation system
- Trigger-based actions (AC power, temperature, time)
- Profile switching automation
- Macro support for complex sequences
- IPC communication for external control

## 🚀 Quick Start

### One-Line Install

```bash
curl -sSL https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7iLinux/main/install.sh | sudo bash
```

### Manual Installation

```bash
# Clone repository
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux/LenovoLegionToolkit.Avalonia

# Build and install
./build-linux-complete.sh
cd publish
sudo ./install-system.sh

# Add user to legion group (required for hardware access)
sudo usermod -a -G legion $USER
# Log out and log back in, then:
legion-toolkit
```

## 📦 Installation Options

### 🐧 **Debian/Ubuntu Package**
```bash
wget https://github.com/vivekchamoli/LenovoLegion7iLinux/releases/latest/download/legion-toolkit_3.0.0_amd64.deb
sudo dpkg -i legion-toolkit_3.0.0_amd64.deb
sudo usermod -a -G legion $USER
```

### 📱 **AppImage (Portable)**
```bash
wget https://github.com/vivekchamoli/LenovoLegion7iLinux/releases/latest/download/LegionToolkit-x86_64.AppImage
chmod +x LegionToolkit-x86_64.AppImage
./LegionToolkit-x86_64.AppImage
```

### 🔧 **Build from Source**
```bash
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux/LenovoLegionToolkit.Avalonia
./build-linux-complete.sh
```

## 🖥️ Usage

### GUI Application
```bash
# Launch main application
legion-toolkit

# Launch minimized to system tray
legion-toolkit --minimized

# Launch with debug logging
legion-toolkit --debug
```

### Command Line Interface
```bash
# Show system status
legion-toolkit status

# Set power mode
legion-toolkit power-mode performance

# Control RGB lighting
legion-toolkit rgb --mode wave --color blue --brightness 80

# Monitor temperatures
legion-toolkit thermal --monitor

# Control fan speeds
legion-toolkit fan --speed 75

# Battery management
legion-toolkit battery --conservation on --rapid-charge off
```

### Automation Examples
```bash
# Set performance mode when AC connected
legion-toolkit automation add --trigger ac-connected --action "power-mode performance"

# Reduce RGB brightness at night
legion-toolkit automation add --trigger time-22:00 --action "rgb --brightness 20"

# Temperature-based fan control
legion-toolkit automation add --trigger temp-cpu-80 --action "fan --profile performance"
```

## 📋 System Requirements

### Hardware Requirements
- **Laptop**: Lenovo Legion series (Gen 6+) or compatible IdeaPad Gaming
- **CPU**: x86_64 or ARM64 processor
- **Memory**: 4GB RAM (8GB recommended)
- **Storage**: 100MB free space

### Software Requirements
- **OS**: Linux kernel 5.4+ (Ubuntu 20.04+, Fedora 32+, Arch, etc.)
- **Desktop**: Any X11 or Wayland desktop environment
- **Dependencies**: Included in packages (libicu, openssl, etc.)

### Optional for Full Features
- **legion-laptop kernel module** (for advanced hardware control)
- **systemd** (for service management)
- **pkexec/sudo** (for privileged operations)

## 🛠️ Hardware Support

### ✅ **Supported Models**
- **Legion 5 Series**: 5, 5i, 5 Pro (Gen 6, 7, 8, 9)
- **Legion 7 Series**: 7, 7i (Gen 6, 7, 8, 9)
- **Legion 9 Series**: 9i (Gen 9)
- **IdeaPad Gaming**: 3, 3i (Selected models)
- **LOQ Series**: 15, 16 (Selected models)

### 🔧 **Feature Compatibility**

| Feature | Gen 6 | Gen 7 | Gen 8 | Gen 9 | Notes |
|---------|-------|-------|-------|-------|-------|
| Power Modes | ✅ | ✅ | ✅ | ✅ | Quiet/Balanced/Performance |
| Custom Mode | ❌ | ✅ | ✅ | ✅ | User-defined settings |
| Thermal Control | ✅ | ✅ | ✅ | ✅ | CPU/GPU/System temps |
| Fan Control | ✅ | ✅ | ✅ | ✅ | Manual and auto modes |
| RGB 4-Zone | ✅ | ✅ | ✅ | ✅ | Standard Legion RGB |
| RGB Per-Key | ❌ | ✅ | ✅ | ✅ | Advanced models only |
| Hybrid Graphics | ✅ | ✅ | ✅ | ✅ | Requires NVIDIA/AMD drivers |
| Battery Conservation | ✅ | ✅ | ✅ | ✅ | Extends battery life |
| Rapid Charge | ✅ | ✅ | ✅ | ✅ | Fast charging control |
| Display Control | ❌ | ✅ | ✅ | ✅ | Refresh rate switching |

## 🔧 Configuration

### Hardware Access Setup
```bash
# Add user to hardware access group
sudo usermod -a -G legion $USER

# Load Legion kernel module (if available)
sudo modprobe legion-laptop

# Check module status
lsmod | grep legion

# Install module permanently (Ubuntu/Debian)
echo "legion-laptop" | sudo tee -a /etc/modules
```

### Configuration Locations
- **User Settings**: `~/.config/legion-toolkit/`
- **System Settings**: `/etc/legion-toolkit/`
- **Log Files**: `~/.config/legion-toolkit/logs/`
- **Profiles**: `~/.config/legion-toolkit/profiles/`
- **Automation Rules**: `~/.config/legion-toolkit/automation/`

### Advanced Configuration
```bash
# Edit configuration
legion-toolkit config --edit

# Import/Export profiles
legion-toolkit profile --export gaming.json
legion-toolkit profile --import office.json

# View hardware capabilities
legion-toolkit hardware --capabilities

# Service management
sudo systemctl enable legion-toolkit
sudo systemctl start legion-toolkit
```

## 🐛 Troubleshooting

### Common Issues

<details>
<summary><strong>🚫 Permission Denied Errors</strong></summary>

```bash
# Ensure user is in legion group
groups $USER

# Re-add to legion group if missing
sudo usermod -a -G legion $USER
# Log out and log back in

# Check udev rules
ls -la /etc/udev/rules.d/99-legion-toolkit.rules

# Reload udev rules
sudo udevadm control --reload-rules
sudo udevadm trigger
```
</details>

<details>
<summary><strong>🔌 Hardware Not Detected</strong></summary>

```bash
# Check for legion module
sudo modprobe legion-laptop
lsmod | grep legion

# Check hardware paths
ls -la /sys/kernel/legion_laptop/ 2>/dev/null || echo "Module not loaded"
ls -la /sys/class/hwmon/

# Test basic functionality
legion-toolkit hardware --detect
```
</details>

<details>
<summary><strong>🖥️ GUI Not Starting</strong></summary>

```bash
# Try CLI mode first
legion-toolkit --help

# Check display environment
echo $DISPLAY
echo $WAYLAND_DISPLAY

# Check dependencies
ldd $(which legion-toolkit) | grep "not found"

# Run with debugging
legion-toolkit --debug --verbose
```
</details>

<details>
<summary><strong>🔧 Build Issues</strong></summary>

```bash
# Install .NET 8 SDK
sudo apt update && sudo apt install dotnet-sdk-8.0

# Clear build cache
dotnet clean
rm -rf bin/ obj/

# Rebuild
dotnet restore
dotnet build --configuration Release
```
</details>

## 🤝 Contributing

We welcome contributions! Here's how to get started:

### Development Setup
```bash
# Fork and clone repository
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux

# Install development dependencies
sudo apt install dotnet-sdk-8.0 git build-essential

# Set up development environment
cd LenovoLegionToolkit.Avalonia
dotnet restore
dotnet build
```

### Code Structure
```
LenovoLegionToolkit.Avalonia/
├── Services/Linux/          # Linux-specific service implementations
├── Views/                   # Avalonia UI views and windows
├── ViewModels/             # MVVM view models and logic
├── Models/                 # Data models and enums
├── Utils/                  # Platform utilities and helpers
├── CLI/                    # Command-line interface commands
├── IPC/                    # Inter-process communication
├── Resources/              # Application resources and assets
└── SystemTray/             # System tray integration
```

### Contributing Guidelines

1. **Fork** the repository
2. **Create** a feature branch (`git checkout -b feature/amazing-feature`)
3. **Commit** changes (`git commit -m 'Add amazing feature'`)
4. **Push** to branch (`git push origin feature/amazing-feature`)
5. **Open** a Pull Request

### Development Tips
- Follow existing code style and patterns
- Add appropriate logging for debugging
- Test on real Legion hardware when possible
- Update documentation for new features
- Consider backward compatibility

## 📖 Documentation

- **📚 [User Manual](docs/USER_MANUAL.md)** - Complete usage guide
- **🔧 [Build Instructions](LenovoLegionToolkit.Avalonia/BUILD-INSTRUCTIONS.md)** - Building from source
- **⚡ [CLI Reference](docs/CLI_REFERENCE.md)** - Command-line interface
- **🤖 [Automation Guide](docs/AUTOMATION.md)** - Setting up automation
- **🐛 [Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues and solutions
- **💻 [Development Guide](docs/DEVELOPMENT.md)** - Contributing and development

## 🎯 Roadmap

### ✅ **Completed (v3.0.0)**
- Complete Linux GUI application
- Hardware detection and control
- RGB keyboard support
- Power management
- CLI interface and automation
- Professional packaging

### 🚧 **In Progress (v3.1.0)**
- [ ] Flatpak packaging
- [ ] Enhanced automation system
- [ ] Performance monitoring dashboard
- [ ] Custom keyboard layouts
- [ ] Mobile companion app

### 🔮 **Planned (v3.2.0+)**
- [ ] Web interface for remote control
- [ ] Plugin system for extensions
- [ ] Advanced RGB effects editor
- [ ] Game integration features
- [ ] Cloud profile synchronization

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👨‍💻 Author

**Vivek Chamoli**
- Email: vivekchamoli@outlook.com
- GitHub: [@vivekchamoli](https://github.com/vivekchamoli)
- LinkedIn: [Vivek Chamoli](https://linkedin.com/in/vivekchamoli)

## 🙏 Acknowledgments

- **Original Windows Implementation**: [BartoszCichecki](https://github.com/BartoszCichecki) and contributors
- **legion-laptop Kernel Module**: Linux kernel maintainers and contributors
- **Avalonia UI Framework**: Cross-platform .NET UI framework team
- **Community**: Beta testers and feature contributors

## ⭐ Support the Project

If you find this project useful, please consider:

- ⭐ **Starring** the repository
- 🐛 **Reporting** bugs and issues
- 💡 **Suggesting** new features
- 🤝 **Contributing** code or documentation
- 📢 **Sharing** with other Legion users

---

<div align="center">

**🐧 Made with ❤️ for the Linux Community**

[Report Bug](https://github.com/vivekchamoli/LenovoLegion7iLinux/issues) • [Request Feature](https://github.com/vivekchamoli/LenovoLegion7iLinux/issues) • [Discussions](https://github.com/vivekchamoli/LenovoLegion7iLinux/discussions)

</div>