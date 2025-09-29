# Legion Toolkit - Ubuntu Installation Guide

<div align="center">

![Ubuntu](https://img.shields.io/badge/Ubuntu-20.04%2B-orange?logo=ubuntu)
![Debian](https://img.shields.io/badge/Debian-11%2B-red?logo=debian)
![License](https://img.shields.io/badge/License-MIT-blue)
![Version](https://img.shields.io/badge/Version-3.0.0-green)

**Complete control suite for Lenovo Legion laptops on Ubuntu**

</div>

## 📋 Prerequisites

- Ubuntu 20.04 LTS or later (or Debian 11+)
- Lenovo Legion laptop (any model)
- Administrative (sudo) access
- 100 MB free disk space

### Recommended
- `legion-laptop` kernel module for full hardware control
- NVIDIA/AMD proprietary drivers for GPU management

## 🚀 Quick Installation

### Method 1: One-Click Installer (Easiest)

```bash
wget -O - https://raw.githubusercontent.com/LenovoLegion7iToolkit/main/Scripts/ubuntu-installer.sh | bash
```

### Method 2: PPA Repository (Auto Updates)

```bash
sudo add-apt-repository ppa:legion-toolkit/stable
sudo apt update
sudo apt install legion-toolkit
```

### Method 3: Snap Package

```bash
sudo snap install legion-toolkit --classic
```

### Method 4: Manual .deb Package

1. Download the latest release:
```bash
wget https://github.com/LenovoLegion7iToolkit/releases/latest/download/legion-toolkit-ubuntu-3.0.0.tar.gz
tar -xzf legion-toolkit-ubuntu-3.0.0.tar.gz
cd legion-toolkit-ubuntu-3.0.0
```

2. Install:
```bash
sudo dpkg -i legion-toolkit_3.0.0_amd64.deb
sudo apt-get install -f  # Fix any dependencies
```

### Method 5: Build from Source

```bash
# Clone repository
git clone https://github.com/LenovoLegion7iToolkit.git
cd LenovoLegion7iToolkit

# Install build dependencies
sudo apt install dotnet-sdk-8.0 make

# Build and install
make
sudo make install
```

## 🎮 AppImage (No Installation Required)

For testing or portable use:

```bash
wget https://github.com/LenovoLegion7iToolkit/releases/latest/download/LegionToolkit-3.0.0-x86_64.AppImage
chmod +x LegionToolkit-3.0.0-x86_64.AppImage
./LegionToolkit-3.0.0-x86_64.AppImage
```

## 🔧 Post-Installation Setup

### 1. Install Kernel Module (Important!)

For full hardware control capabilities:

```bash
# Option A: From repository (if available)
sudo apt install legion-laptop-dkms

# Option B: Build from source
git clone https://github.com/johnfanv2/LenovoLegion5LinuxSupport.git
cd LenovoLegion5LinuxSupport/kernel_module
make && sudo make install
sudo modprobe legion-laptop
```

### 2. Configure User Permissions

```bash
# Add user to required groups
sudo usermod -a -G video,input $USER

# Log out and back in for changes to take effect
```

### 3. Enable System Service

```bash
# Enable auto-start on boot
sudo systemctl enable legion-toolkit

# Start service immediately
sudo systemctl start legion-toolkit

# Check service status
sudo systemctl status legion-toolkit
```

### 4. Configure Sensors

```bash
sudo sensors-detect --auto
```

## 🖥️ Usage

### GUI Application

```bash
legion-toolkit-gui
```

Or launch from your application menu: **Legion Toolkit**

### Command Line Interface

```bash
# Show help
legion-toolkit --help

# Power management
legion-toolkit power get                    # Get current mode
legion-toolkit power set quiet              # Set quiet mode
legion-toolkit power set balanced           # Set balanced mode
legion-toolkit power set performance        # Set performance mode

# Battery management
legion-toolkit battery status                # Show battery info
legion-toolkit battery conservation on       # Enable conservation mode
legion-toolkit battery rapid-charge on       # Enable rapid charging
legion-toolkit battery threshold 60 80       # Set charge thresholds

# RGB keyboard control
legion-toolkit rgb set FF0000               # Set red color
legion-toolkit rgb brightness 80            # Set brightness
legion-toolkit rgb effect wave --speed 5    # Wave effect
legion-toolkit rgb off                      # Turn off RGB

# Thermal management
legion-toolkit thermal status                # Show temperatures
legion-toolkit thermal fan auto             # Auto fan control
legion-toolkit thermal fan manual --speed 70 # Manual fan speed

# Display control
legion-toolkit display refresh 144          # Set refresh rate
legion-toolkit display hdr on               # Enable HDR
```

### System Tray

Legion Toolkit automatically adds an icon to your system tray for quick access to:
- Power mode switching
- Battery settings
- RGB presets
- Fan control
- Quick settings

## 🔄 Updates

### Via PPA
```bash
sudo apt update && sudo apt upgrade
```

### Via Snap
```bash
sudo snap refresh legion-toolkit
```

### Manual Update
```bash
cd LenovoLegion7iToolkit
git pull
make clean
make
sudo make install
```

## 🗑️ Uninstallation

### From APT/PPA
```bash
sudo apt remove legion-toolkit
sudo apt autoremove
```

### From Snap
```bash
sudo snap remove legion-toolkit
```

### From Source
```bash
cd LenovoLegion7iToolkit
sudo make uninstall
```

## 🐛 Troubleshooting

### Permission Denied Errors

```bash
# Ensure user is in correct groups
groups $USER
# Should show: video input

# If not, add user to groups
sudo usermod -a -G video,input $USER
# Then log out and back in
```

### Kernel Module Not Loading

```bash
# Check if module is loaded
lsmod | grep legion

# Load manually
sudo modprobe legion-laptop

# Check kernel logs
dmesg | tail -20
```

### Service Not Starting

```bash
# Check service logs
journalctl -u legion-toolkit -n 50

# Run in debug mode
legion-toolkit --debug

# Reset service
sudo systemctl daemon-reload
sudo systemctl restart legion-toolkit
```

### GUI Not Opening

```bash
# Check display server
echo $DISPLAY  # Should show :0 or similar

# Try with explicit display
DISPLAY=:0 legion-toolkit-gui

# Check for missing libraries
ldd /usr/bin/legion-toolkit | grep "not found"
```

## 📊 System Requirements

### Minimum
- CPU: Any x86_64 processor
- RAM: 512 MB
- Disk: 100 MB
- Kernel: 5.4+

### Recommended
- CPU: Intel Core i5/AMD Ryzen 5 or better
- RAM: 2 GB
- Disk: 200 MB
- Kernel: 5.15+ with legion-laptop module

## 🔌 Supported Hardware

### Fully Supported Models
- Legion 5 Series (2020-2024)
- Legion 7 Series (2020-2024)
- Legion 5 Pro Series
- Legion 7i Series
- Legion Slim Series

### Partial Support
- IdeaPad Gaming Series
- Older Legion Y Series

### Features by Model

| Feature | Legion 5 | Legion 7 | Legion Pro |
|---------|----------|----------|------------|
| Power Modes | ✅ | ✅ | ✅ |
| Battery Control | ✅ | ✅ | ✅ |
| RGB Keyboard | ✅ | ✅ | ✅ |
| Fan Control | ✅ | ✅ | ✅ |
| GPU Switching | ✅ | ✅ | ✅ |
| Display Control | ⚠️ | ✅ | ✅ |

✅ = Full Support | ⚠️ = Partial Support | ❌ = Not Supported

## 🤝 Contributing

Contributions welcome! See [CONTRIBUTING.md](CONTRIBUTING.md)

## 📝 License

MIT License - See [LICENSE](LICENSE)

## 🔗 Links

- [Project Homepage](https://github.com/LenovoLegion7iToolkit)
- [Issue Tracker](https://github.com/LenovoLegion7iToolkit/issues)
- [Documentation](https://github.com/LenovoLegion7iToolkit/wiki)
- [Kernel Module](https://github.com/johnfanv2/LenovoLegion5LinuxSupport)

## 💬 Support

- GitHub Issues: [Report bugs](https://github.com/LenovoLegion7iToolkit/issues)
- Discussions: [Ask questions](https://github.com/LenovoLegion7iToolkit/discussions)

---

<div align="center">
Made with ❤️ for the Linux Legion Community
</div>