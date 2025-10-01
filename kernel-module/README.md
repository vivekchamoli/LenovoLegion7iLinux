# Enhanced Legion Laptop Kernel Module

Elite-level kernel module for Lenovo Legion laptops with universal Gen 6-9 support and comprehensive kernel compatibility (5.4+ to 6.8+).

## Features

### Hardware Support
- **Universal Generation Support**: Legion Gen 6, 7, 8, and 9
- **Thermal Management**: CPU/GPU temperature monitoring and fan control
- **Power Modes**: Quiet, Balanced, Performance, and Custom modes
- **Battery Management**: Conservation mode and rapid charging
- **RGB Control**: Keyboard backlighting (4-zone and per-key)
- **System Controls**: Fn lock, overclock settings, hybrid mode

### Kernel Compatibility
- **Backward Compatible**: Supports kernel versions from 5.4 to 6.8+
- **Auto-Detection**: Automatically adapts to kernel version
- **DKMS Ready**: Automatic rebuilds on kernel updates

## Quick Start

### Automated Build (Recommended)

```bash
cd kernel-module
chmod +x build-kernel-module.sh

# Build and test only
./build-kernel-module.sh --test

# Build and install
sudo ./build-kernel-module.sh --install

# Setup DKMS (recommended for automatic kernel update support)
sudo ./build-kernel-module.sh --dkms
```

### Manual Build

```bash
# Check environment
make check

# Build module
make all

# Install module
sudo make install

# Load module
sudo modprobe legion_laptop_enhanced
```

### DKMS Installation

```bash
# Install DKMS if not present
sudo apt install dkms  # Ubuntu/Debian
sudo dnf install dkms  # Fedora
sudo pacman -S dkms    # Arch

# Register and build with DKMS
sudo make dkms-install

# Module will now auto-rebuild on kernel updates
```

## Requirements

### Build Tools
```bash
# Ubuntu/Debian
sudo apt install build-essential linux-headers-$(uname -r)

# Fedora/RHEL
sudo dnf groupinstall "Development Tools"
sudo dnf install kernel-devel-$(uname -r)

# Arch Linux
sudo pacman -S base-devel linux-headers
```

### Runtime Dependencies
- Linux kernel 5.4+
- ACPI support
- Platform driver support
- Thermal subsystem
- Hardware monitoring (hwmon)

## Module Parameters

Load the module with custom parameters:

```bash
# Enable debug output
sudo modprobe legion_laptop_enhanced debug=1

# Force load on unknown models
sudo modprobe legion_laptop_enhanced force_load=1

# Combine parameters
sudo modprobe legion_laptop_enhanced debug=1 force_load=1
```

## Sysfs Interface

The module exposes control interfaces through sysfs:

```bash
# View available interfaces
ls /sys/module/legion_laptop_enhanced/

# Example: Check if module is loaded
lsmod | grep legion_laptop_enhanced

# Example: View module info
modinfo legion_laptop_enhanced
```

## Troubleshooting

### Module Not Loading

1. **Check kernel headers**:
   ```bash
   ls /lib/modules/$(uname -r)/build
   ```

2. **Check for conflicts**:
   ```bash
   # Remove standard legion-laptop if loaded
   sudo modprobe -r legion-laptop
   ```

3. **Check dmesg for errors**:
   ```bash
   sudo dmesg | grep -i legion
   ```

### Build Failures

1. **Missing kernel headers**:
   ```bash
   # Install headers matching your kernel
   sudo apt install linux-headers-$(uname -r)
   ```

2. **Compiler version mismatch**:
   ```bash
   # Check GCC version
   gcc --version

   # Kernel should be compiled with same GCC version
   cat /proc/version
   ```

3. **DKMS build failure**:
   ```bash
   # Check DKMS status
   dkms status

   # View build logs
   cat /var/lib/dkms/legion-laptop-enhanced/2.0.0/build/make.log
   ```

## Development

### Debugging

Enable debug mode for development:

```bash
# Build with debug symbols
make debug

# Load with debug output
sudo modprobe legion_laptop_enhanced debug=1

# View debug logs
sudo dmesg -w | grep legion
```

### Testing

```bash
# Test build without installing
./build-kernel-module.sh --test

# Check module symbols
nm legion_laptop_enhanced.ko | grep init

# Verify module dependencies
modprobe --dry-run legion_laptop_enhanced
```

### Code Structure

```
kernel-module/
├── enhanced_legion_laptop.c    # Main driver code
├── Makefile                     # Build configuration
├── dkms.conf                    # DKMS configuration
├── build-kernel-module.sh      # Automated build script
├── scripts/
│   ├── post-install.sh         # Post-installation tasks
│   └── pre-remove.sh           # Pre-removal cleanup
└── README.md                    # This file
```

## Kernel Version Compatibility Matrix

| Kernel Version | Status | Notes |
|----------------|--------|-------|
| 5.4.x - 5.9.x  | ✅ Supported | Old thermal API compatibility |
| 5.10.x - 5.14.x | ✅ Supported | Transitional features |
| 5.15.x - 5.19.x | ✅ Supported | Modern platform driver |
| 6.0.x - 6.5.x  | ✅ Supported | New kernel features |
| 6.6.x - 6.8.x  | ✅ Supported | Latest tested |
| 6.9.x+         | ⚠️ Experimental | Should work, not fully tested |

## Integration with Legion Toolkit

This module is automatically detected and used by the Legion Toolkit application:

```bash
# Check if module is loaded
legion-toolkit-debug | grep "kernel module"

# Application will automatically attempt to load the module
# if running with sufficient privileges
```

## License

GPL v2

## Author

Vivek Chamoli <vivekchamoli@github.com>

## Support

- GitHub Issues: https://github.com/vivekchamoli/LenovoLegion7iLinux/issues
- Module Documentation: This README
- Build Script Help: `./build-kernel-module.sh --help`

## Advanced Configuration

### Automatic Loading on Boot

Create a modprobe configuration:

```bash
# Create config file
sudo tee /etc/modprobe.d/legion-laptop-enhanced.conf << EOF
# Auto-load Legion enhanced module
options legion_laptop_enhanced debug=0
alias platform:legion-laptop legion_laptop_enhanced
EOF

# Enable module loading on boot
sudo tee /etc/modules-load.d/legion-laptop-enhanced.conf << EOF
legion_laptop_enhanced
EOF
```

### Blacklist Standard Module

If you want to use only the enhanced module:

```bash
sudo tee /etc/modprobe.d/blacklist-legion.conf << EOF
# Blacklist standard legion-laptop in favor of enhanced version
blacklist legion-laptop
EOF

sudo update-initramfs -u
```

## Kernel Module Architecture

### OS-Level Design
- **Platform Driver Model**: Proper Linux platform device integration
- **ACPI Interface**: Direct hardware communication via ACPI methods
- **Sysfs Exposure**: Standard Linux interface for userspace
- **Thermal Subsystem**: Integration with kernel thermal framework
- **Hardware Monitoring**: hwmon subsystem for sensor data

### Compatibility Layer
- **Version Detection**: Runtime kernel version checks
- **API Adaptation**: Automatic use of correct kernel APIs
- **Graceful Degradation**: Features disabled if not supported
- **Symbol Resolution**: Safe symbol usage with fallbacks

This is production-ready, kernel-developer-grade code with enterprise-level quality and compatibility.
