# Legion Toolkit for Linux

Comprehensive hardware control utility for Lenovo Legion laptops running Linux.

## Features

### 🎮 Complete Hardware Control
- **Power Management**: Switch between Quiet, Balanced, Performance, and Custom power modes
- **Battery Management**: Set charge limits, conservation mode, rapid charging
- **Thermal Control**: Monitor temperatures, control fan modes, custom fan curves
- **RGB Keyboard**: Full RGB control with effects, zones, and brightness
- **Display Management**: Refresh rate control, HDR, brightness, night light

### 🤖 Advanced Automation
- **Profiles**: Save and restore complete hardware configurations
- **Rules Engine**: Automate settings based on:
  - Battery level
  - Power state (AC/battery)
  - Time schedules
  - Running applications
  - Network connections
  - Display connections
- **Smart Triggers**: Chain multiple conditions with AND/OR logic

### 💻 Multiple Interfaces
- **Modern GUI**: Native Linux desktop application with system tray integration
- **Comprehensive CLI**: Full command-line control for scripts and automation
- **IPC Server**: Control from external applications via Unix sockets
- **System Service**: Run as systemd service for persistent control

## Requirements

### System Requirements
- **OS**: Linux kernel 5.10+ (Ubuntu 20.04+, Fedora 35+, Arch, openSUSE Leap 15.3+)
- **Architecture**: x86_64
- **RAM**: 512MB minimum
- **Disk**: 100MB for installation

### Dependencies
- **.NET Runtime 8.0**
- **legion-laptop kernel module** (for hardware control)
- **X11 or Wayland** (for GUI)
- Optional: `ddcutil` (for external display brightness control)
- Optional: `redshift` (for night light functionality)

## Installation

### Quick Install Script
```bash
# One-line installer (recommended)
wget -O - https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7i/main/Scripts/ubuntu-installer.sh | bash

# Or download and install manually
wget https://github.com/vivekchamoli/LenovoLegion7i/releases/latest/download/legion-toolkit_3.0.0_amd64.deb
sudo dpkg -i legion-toolkit_3.0.0_amd64.deb
```

### Package Managers

#### Debian/Ubuntu (DEB)
```bash
wget https://github.com/LenovoLegion/LegionToolkit/releases/latest/download/legion-toolkit_1.0.0_amd64.deb
sudo dpkg -i legion-toolkit_1.0.0_amd64.deb
sudo apt-get install -f  # Install dependencies if needed
```

#### Fedora/RHEL (RPM)
```bash
wget https://github.com/LenovoLegion/LegionToolkit/releases/latest/download/legion-toolkit-1.0.0.x86_64.rpm
sudo dnf install legion-toolkit-1.0.0.x86_64.rpm
```

#### Arch Linux (AUR)
```bash
yay -S legion-toolkit
# or
paru -S legion-toolkit
```

#### AppImage (Portable)
```bash
wget https://github.com/LenovoLegion/LegionToolkit/releases/latest/download/LegionToolkit-1.0.0-x86_64.AppImage
chmod +x LegionToolkit-1.0.0-x86_64.AppImage
./LegionToolkit-1.0.0-x86_64.AppImage
```

### Build from Source
```bash
# Clone the repository
git clone https://github.com/LenovoLegion/LegionToolkit.git
cd LegionToolkit

# Install .NET SDK 8.0 if not already installed
# https://dotnet.microsoft.com/download/dotnet/8.0

# Build and install
./Scripts/install.sh
```

## Kernel Module Setup

Legion Toolkit requires the `legion-laptop` kernel module for hardware control.

### Check if Module is Available
```bash
modinfo legion-laptop
```

### Install Kernel Module

#### Debian/Ubuntu
```bash
sudo apt install legion-laptop-dkms
```

#### Fedora
```bash
sudo dnf install akmod-legion-laptop
```

#### Arch
```bash
yay -S legion-laptop-dkms
```

#### Manual Installation
```bash
git clone https://github.com/johnfanv2/LenovoLegionLinux.git
cd LenovoLegionLinux/kernel_module
make
sudo make install
sudo modprobe legion-laptop
```

### Enable Module at Boot
```bash
echo "legion-laptop" | sudo tee /etc/modules-load.d/legion-laptop.conf
```

## Usage

### GUI Mode
```bash
# Launch the graphical interface
legion-toolkit

# Or from application menu: "Legion Toolkit"
```

### CLI Mode

#### Power Management
```bash
# Get current power mode
legion-toolkit power get

# Set power mode
legion-toolkit power set performance

# List available modes
legion-toolkit power list
```

#### Battery Management
```bash
# Get battery status
legion-toolkit battery status

# Set charge limit to 80%
legion-toolkit battery charge-limit 80

# Enable conservation mode
legion-toolkit battery conservation on
```

#### RGB Keyboard
```bash
# Set static red color
legion-toolkit rgb static 255 0 0

# Set breathing effect
legion-toolkit rgb effect breathing

# Set brightness to 50%
legion-toolkit rgb brightness 50

# Turn off backlighting
legion-toolkit rgb off
```

#### Thermal Monitoring
```bash
# Get current temperatures
legion-toolkit thermal status

# Monitor in real-time
legion-toolkit thermal monitor

# Set fan mode
legion-toolkit thermal fan-mode performance
```

#### Display Control
```bash
# List displays
legion-toolkit display list

# Set refresh rate to 144Hz
legion-toolkit display refresh-rate 144

# Enable night light
legion-toolkit display night-light on
```

#### Automation
```bash
# Start automation service
legion-toolkit automation start

# List profiles
legion-toolkit automation profiles list

# Apply a profile
legion-toolkit automation profiles apply "Gaming"

# Create profile from current state
legion-toolkit automation profiles create-from-current -n "My Profile"
```

### System Service

#### Enable Service
```bash
# User service (runs on login)
systemctl --user enable legion-toolkit
systemctl --user start legion-toolkit

# System service (runs at boot, requires root)
sudo systemctl enable legion-toolkit-system
sudo systemctl start legion-toolkit-system
```

#### Check Service Status
```bash
systemctl --user status legion-toolkit
# or
sudo systemctl status legion-toolkit-system
```

## Configuration

### Configuration Files
- **User Config**: `~/.config/legion-toolkit/settings.json`
- **Automation**: `~/.config/legion-toolkit/automation.json`
- **System Config**: `/etc/legion-toolkit/config.json`

### Example Configuration
```json
{
  "General": {
    "StartMinimized": true,
    "MinimizeToTray": true,
    "StartWithSystem": true
  },
  "Monitoring": {
    "EnableMonitoring": true,
    "UpdateIntervalSeconds": 2
  },
  "Automation": {
    "Enabled": true,
    "EvaluationIntervalSeconds": 30
  }
}
```

## Automation Examples

### Profile: Gaming Mode
```json
{
  "Name": "Gaming",
  "PowerMode": "Performance",
  "FanMode": "Performance",
  "RefreshRate": 165,
  "KeyboardBacklight": true,
  "RgbEffect": "Rainbow"
}
```

### Rule: Switch to Battery Saver
```json
{
  "Name": "Battery Saver",
  "Triggers": [
    {
      "Type": "BatteryLevel",
      "Operator": "LessThanOrEqual",
      "Value": 20
    }
  ],
  "Action": "ApplyProfile",
  "Profile": "Power Saver"
}
```

### Rule: Gaming Auto-Detection
```json
{
  "Name": "Auto Gaming Mode",
  "Triggers": [
    {
      "Type": "ProcessRunning",
      "ProcessName": "steam"
    }
  ],
  "Action": "ApplyProfile",
  "Profile": "Gaming"
}
```

## Troubleshooting

### Module Not Loaded
```bash
# Check if module is loaded
lsmod | grep legion

# Load module manually
sudo modprobe legion-laptop

# Check dmesg for errors
sudo dmesg | grep legion
```

### Permission Issues
```bash
# Add user to required groups
sudo usermod -a -G video,input $USER

# Re-login for changes to take effect
```

### Service Not Starting
```bash
# Check logs
journalctl --user -u legion-toolkit -f

# Reset configuration
rm -rf ~/.config/legion-toolkit
```

### GUI Not Starting
```bash
# Check display server
echo $DISPLAY
echo $WAYLAND_DISPLAY

# Try forcing X11
GDK_BACKEND=x11 legion-toolkit
```

## Development

### Building
```bash
# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run
dotnet run --project LenovoLegionToolkit.Avalonia
```

### Creating Packages
```bash
# DEB package
./Scripts/build-deb.sh

# RPM package
./Scripts/build-rpm.sh

# AppImage
./Scripts/build-appimage.sh
```

## Support

### Tested Models
- Legion 5 Series (2020-2024)
- Legion 7 Series (2020-2024)
- Legion Pro Series
- Legion Slim Series

### Getting Help
- **Issues**: [GitHub Issues](https://github.com/LenovoLegion/LegionToolkit/issues)
- **Documentation**: [Wiki](https://github.com/LenovoLegion/LegionToolkit/wiki)

## Contributing

Contributions are welcome! Please read our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests
5. Submit a pull request

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Credits

- **legion-laptop kernel module**: [johnfanv2](https://github.com/johnfanv2/LenovoLegionLinux)
- **Avalonia UI**: Cross-platform .NET UI framework
- **Community**: All contributors and testers

## Disclaimer

This software is not affiliated with or endorsed by Lenovo. Use at your own risk.