# Legion Toolkit for Linux

A comprehensive system management application for Lenovo Legion laptops running on Linux. This is the Linux equivalent of the Windows Legion Toolkit, providing complete hardware control and monitoring capabilities.

## Features

### 🌡️ Thermal Management
- **Real-time monitoring** of CPU/GPU temperatures and fan speeds
- **Performance modes**: Quiet, Balanced, Performance
- **Custom fan curves** with manual fan speed control
- **Temperature limits** and thermal protection
- **Advanced fan control** with preset curves and response tuning

### 🎨 RGB Lighting Control
- **4-zone RGB keyboard** support with visual zone selection
- **6 RGB profiles** with custom color management
- **Dynamic effects**: Breathing, Rainbow, Wave, Ripple
- **Preset themes**: Gaming, Professional, Stealth, Matrix
- **Per-zone color control** with RGB sliders and live preview

### 🔋 Battery Management
- **Conservation mode** for extended AC usage (55-60% charge limit)
- **Custom charging thresholds** (start/stop percentages)
- **Rapid charging** toggle with thermal monitoring
- **Battery health** tracking with cycle count and capacity
- **Power profiles**: PowerSaver, Balanced, Performance
- **Always-on USB** for peripheral charging when laptop is off

### 🤖 Automation & Macros
- **Rule-based automation** with temperature, utilization, and battery triggers
- **Macro sequences** for complex system configurations
- **Preset macros**: Gaming Mode, Silent Mode, Battery Saver
- **Custom actions**: Performance mode switching, RGB control, fan management
- **Real-time monitoring** with rule execution tracking

### 📊 System Monitoring
- **Live dashboard** with real-time sensor data
- **Hardware detection** for Legion Slim 7i Gen 9 (16IRX9)
- **GPU monitoring** (NVIDIA/AMD support)
- **System uptime** and power draw tracking
- **Thermal throttling** detection and alerts

## Hardware Support

### Primary Target
- **Legion Slim 7i Gen 9 (16IRX9)** - Full feature support

### Compatibility
- Other Legion laptops with kernel module support
- Systems with hwmon thermal sensors
- NVIDIA/AMD GPUs with temperature monitoring
- Standard Linux power management interfaces

### Requirements
- Linux kernel with hwmon support
- Optional: `legion_laptop` kernel module for enhanced features
- NVIDIA/AMD GPU drivers for GPU monitoring
- Root/sudo access for hardware control (handled by installer)

## Installation

### Option 1: Quick Install (Recommended)
```bash
# Build and install in one step
chmod +x build.sh
./build.sh
sudo ./publish/install.sh
```

### Option 2: Manual Installation
```bash
# Clone or download the source
git clone <repository-url>
cd LenovoLegion7iToolkit/LenovoLegionToolkit.Avalonia

# Build the application
dotnet restore
dotnet publish --configuration Release --runtime linux-x64 --self-contained true

# Install manually
sudo cp bin/Release/net8.0/linux-x64/publish/LenovoLegionToolkit.Avalonia /usr/local/bin/legion-toolkit
sudo chmod +x /usr/local/bin/legion-toolkit
```

### Post-Installation Setup
1. Add your user to the legion group:
   ```bash
   sudo usermod -a -G legion $USER
   ```
2. Log out and log back in (or restart)
3. Launch the application:
   ```bash
   legion-toolkit
   ```

## Usage

### First Run
1. Launch Legion Toolkit from the applications menu or terminal
2. The application will detect your hardware automatically
3. Navigate through the sidebar to access different features
4. Enable automation monitoring for rule-based responses

### Daily Use
- **Dashboard**: Monitor real-time system status
- **Thermal Control**: Adjust performance modes and fan settings
- **RGB Lighting**: Customize keyboard lighting and effects
- **Battery**: Manage charging behavior and power profiles
- **Automation**: Set up rules for automatic system responses

### Command Line
```bash
# Launch GUI
legion-toolkit

# Check if service is running
systemctl --user status legion-toolkit

# View logs
journalctl --user -u legion-toolkit -f
```

## Configuration

### Files and Directories
- **Main config**: `~/.config/legion-toolkit/`
- **RGB profiles**: `~/.config/legion-toolkit/rgb-profiles.json`
- **Automation rules**: `~/.config/legion-toolkit/automation.json`
- **Application settings**: `~/.config/legion-toolkit/settings.json`

### Hardware Interfaces
- **Thermal sensors**: `/sys/class/hwmon/`
- **Legion kernel module**: `/sys/kernel/legion_laptop/`
- **RGB LEDs**: `/sys/class/leds/legion::kbd_backlight_*`
- **Battery**: `/sys/class/power_supply/BAT0/`

## Development

### Prerequisites
- .NET 8 SDK
- Linux development environment
- Access to Legion hardware (for testing)

### Building from Source
```bash
# Clone repository
git clone <repository-url>
cd LenovoLegion7iToolkit/LenovoLegionToolkit.Avalonia

# Restore dependencies
dotnet restore

# Build for development
dotnet build

# Run in development mode
dotnet run

# Build for release
./build.sh
```

### Project Structure
```
LenovoLegionToolkit.Avalonia/
├── Linux/Hardware/          # Hardware interface layers
│   ├── LinuxThermalController.cs
│   ├── LinuxRgbController.cs
│   ├── LinuxBatteryController.cs
│   └── LinuxAutomationController.cs
├── ViewModels/              # MVVM view models
├── Views/                   # Avalonia UI views
├── Converters/             # Data binding converters
└── App.axaml               # Application styling
```

### Contributing
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test on actual Legion hardware
5. Submit a pull request

## Architecture

### Technology Stack
- **UI Framework**: Avalonia UI (cross-platform XAML)
- **Runtime**: .NET 8
- **Architecture**: MVVM with ReactiveUI
- **Hardware Access**: Linux sysfs, hwmon, kernel modules
- **Styling**: FluentAvalonia for modern UI components

### Design Principles
- **Hardware Abstraction**: Clean separation between UI and hardware layers
- **Cross-Platform**: Uses Avalonia for potential future Windows/macOS support
- **Reactive**: ReactiveUI for responsive UI updates
- **Modular**: Each hardware subsystem is independently controllable
- **Safe**: Hardware limits and safety checks throughout

## Troubleshooting

### Common Issues

#### Application Won't Start
```bash
# Check .NET installation
dotnet --version

# Check file permissions
ls -la /usr/local/bin/legion-toolkit

# Run with verbose logging
legion-toolkit --verbose
```

#### No Hardware Detected
```bash
# Check for Legion kernel module
ls /sys/kernel/legion_laptop/

# Check hwmon sensors
ls /sys/class/hwmon/

# Verify group membership
groups $USER
```

#### Permission Denied Errors
```bash
# Reinstall with proper permissions
sudo ./publish/uninstall.sh
sudo ./publish/install.sh

# Check udev rules
cat /etc/udev/rules.d/99-legion-toolkit.rules
```

#### RGB Not Working
```bash
# Check LED subsystem
ls /sys/class/leds/legion*

# Test manual LED control
echo 255 | sudo tee /sys/class/leds/legion::kbd_backlight_zone0/red
```

### Support
- **Issues**: Report bugs and feature requests on GitHub
- **Community**: Join Legion Linux communities for support
- **Documentation**: Check wiki for detailed hardware information

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Original Legion Toolkit for Windows (inspiration and feature reference)
- Legion laptop kernel module developers
- Avalonia UI team for the excellent cross-platform framework
- Linux hardware monitoring community (hwmon, thermal subsystem)

## Disclaimer

This software directly interfaces with hardware components. While safety measures are implemented, use at your own risk. The developers are not responsible for any hardware damage resulting from improper use.

**Important**: Always monitor temperatures when using custom fan curves or overriding thermal limits.