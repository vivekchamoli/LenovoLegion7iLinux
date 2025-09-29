# 🔧 Enhanced Legion Kernel Module - Complete Guide

## 📋 **Overview**

The Enhanced Legion Laptop Kernel Module provides comprehensive hardware control for Lenovo Legion laptops with backward compatibility across kernel versions 5.4+ to 6.8+. This module replaces and enhances the standard `legion-laptop` driver with universal support for Legion Gen 6-9 laptops.

### **Key Features**

- ✅ **Universal Legion Support**: Gen 6, 7, 8, and 9 models
- ✅ **Backward Compatibility**: Kernel versions 5.4+ to 6.8+
- ✅ **Enhanced Hardware Control**: Thermal, RGB, battery, power modes
- ✅ **DKMS Integration**: Automatic rebuilds for kernel updates
- ✅ **Generation Detection**: Automatic model identification
- ✅ **Comprehensive Sysfs Interface**: Easy userspace integration
- ✅ **Professional Error Handling**: Robust debugging and logging

---

## 🎯 **Supported Hardware**

### **Legion Generation 6**
- Legion 5 (82B1, 82JU)
- Legion 5 Pro (82JQ)
- Legion 7 (82K6)

### **Legion Generation 7**
- Legion 5 (82RD)
- Legion 7 (82UH)
- Legion 7i Gen 7 (82TD)

### **Legion Generation 8**
- Legion 5i Gen 8
- Legion 7i Gen 8

### **Legion Generation 9**
- Legion 5i Gen 9
- Legion 7i Gen 9

### **Hardware Features by Generation**

| Feature | Gen 6 | Gen 7 | Gen 8 | Gen 9 |
|---------|-------|-------|-------|-------|
| **Thermal Control** | ✅ | ✅ | ✅ | ✅ |
| **Legion Mode** | ✅ | ✅ | ✅ | ✅ |
| **Battery Conservation** | ✅ | ✅ | ✅ | ✅ |
| **Rapid Charge** | ✅ | ✅ | ✅ | ✅ |
| **Fn Lock** | ✅ | ✅ | ✅ | ✅ |
| **RGB Control** | ✅ (4-zone) | ✅ (4-zone) | ✅ (Enhanced) | ✅ (Per-key) |
| **Fan Curve Control** | ❌ | ✅ | ✅ | ✅ |
| **Overclocking** | ❌ | ✅ | ✅ | ✅ |
| **GPU Switching** | ❌ | ✅ | ✅ | ✅ |

---

## 🚀 **Installation**

### **Method 1: DKMS Package (Recommended)**

```bash
# Install the DKMS package
sudo dpkg -i legion-laptop-enhanced-dkms_2.0.0_all.deb

# Verify installation
dkms status legion-laptop-enhanced

# Load the module
sudo modprobe legion_laptop_enhanced
```

### **Method 2: Manual Installation**

```bash
# Navigate to module directory
cd kernel-module/

# Test compatibility first
./test-compatibility.sh

# Build the module
make clean
make all

# Install manually
sudo insmod legion_laptop_enhanced.ko

# For permanent installation
sudo make install
sudo depmod -a
```

### **Method 3: DKMS Manual Setup**

```bash
# Copy source to DKMS tree
sudo cp -r kernel-module/ /usr/src/legion-laptop-enhanced-2.0.0/

# Add to DKMS
sudo dkms add -m legion-laptop-enhanced -v 2.0.0

# Build and install
sudo dkms build -m legion-laptop-enhanced -v 2.0.0
sudo dkms install -m legion-laptop-enhanced -v 2.0.0
```

---

## 🔧 **Configuration**

### **Module Parameters**

```bash
# Enable debug output
sudo modprobe legion_laptop_enhanced debug=1

# Force loading on unknown models
sudo modprobe legion_laptop_enhanced force_load=1

# Combined parameters
sudo modprobe legion_laptop_enhanced debug=1 force_load=1
```

### **Persistent Configuration**

Create `/etc/modprobe.d/legion-enhanced.conf`:

```bash
# Enhanced Legion module configuration
options legion_laptop_enhanced debug=0 force_load=0
alias platform:legion_laptop legion_laptop_enhanced
blacklist legion_laptop
```

### **Auto-loading at Boot**

Create `/etc/modules-load.d/legion-enhanced.conf`:

```bash
legion_laptop_enhanced
```

---

## 📊 **Sysfs Interface**

### **Available Attributes**

The module creates a comprehensive sysfs interface at `/sys/kernel/legion_laptop/`:

```bash
# View all available attributes
ls -la /sys/kernel/legion_laptop/

# Example attributes
thermal_mode       # 0=Quiet, 1=Balanced, 2=Performance, 3=Custom
legion_mode        # 0=Disabled, 1=Enabled
battery_conservation  # 0=Disabled, 1=Enabled
rapid_charge       # 0=Disabled, 1=Enabled
fn_lock            # 0=Disabled, 1=Enabled
generation         # Legion generation (6, 7, 8, 9)
capabilities       # Available features for this model
```

### **Usage Examples**

```bash
# Check current thermal mode
cat /sys/kernel/legion_laptop/thermal_mode

# Set performance mode
echo 2 | sudo tee /sys/kernel/legion_laptop/thermal_mode

# Enable battery conservation
echo 1 | sudo tee /sys/kernel/legion_laptop/battery_conservation

# Check capabilities
cat /sys/kernel/legion_laptop/capabilities

# View generation
cat /sys/kernel/legion_laptop/generation
```

---

## 🔍 **Troubleshooting**

### **Common Issues**

#### **Module Not Loading**

```bash
# Check if module is blacklisted
grep -r legion /etc/modprobe.d/

# Check kernel logs
dmesg | grep legion

# Try force loading
sudo modprobe legion_laptop_enhanced force_load=1
```

#### **Build Failures**

```bash
# Install kernel headers
sudo apt install linux-headers-$(uname -r)  # Ubuntu/Debian
sudo dnf install kernel-devel-$(uname -r)   # Fedora
sudo pacman -S linux-headers                # Arch

# Test build environment
cd kernel-module/
./test-compatibility.sh
```

#### **Hardware Not Detected**

```bash
# Check system information
cat /sys/class/dmi/id/sys_vendor
cat /sys/class/dmi/id/product_name
cat /sys/class/dmi/id/product_version

# Force loading if needed
sudo modprobe legion_laptop_enhanced force_load=1 debug=1
dmesg | tail -20
```

### **Debug Mode**

```bash
# Enable debug output
sudo modprobe legion_laptop_enhanced debug=1

# View debug logs
dmesg | grep legion_laptop_enhanced

# Check module info
modinfo legion_laptop_enhanced
```

### **DKMS Issues**

```bash
# Check DKMS status
dkms status

# Rebuild module
sudo dkms remove legion-laptop-enhanced/2.0.0 --all
sudo dkms install legion-laptop-enhanced/2.0.0

# Check DKMS logs
cat /var/lib/dkms/legion-laptop-enhanced/2.0.0/build/make.log
```

---

## 🧪 **Testing**

### **Compatibility Test Suite**

```bash
# Run comprehensive compatibility tests
cd kernel-module/
./test-compatibility.sh
```

The test suite checks:
- Kernel version compatibility
- Build environment
- Module compilation
- Hardware interfaces
- ACPI compatibility
- Module loading (if root)
- Performance characteristics

### **Manual Testing**

```bash
# Test module loading
sudo modprobe legion_laptop_enhanced

# Check if loaded
lsmod | grep legion

# Test sysfs interface
ls -la /sys/kernel/legion_laptop/

# Test thermal control
echo 1 | sudo tee /sys/kernel/legion_laptop/thermal_mode
cat /sys/kernel/legion_laptop/thermal_mode

# Unload module
sudo rmmod legion_laptop_enhanced
```

---

## 🔄 **Kernel Compatibility Matrix**

### **Tested Kernel Versions**

| Kernel Version | Status | Notes |
|----------------|--------|--------|
| **5.4.x** | ✅ Supported | Minimum supported version |
| **5.10.x** | ✅ Fully Tested | LTS kernel |
| **5.15.x** | ✅ Fully Tested | LTS kernel |
| **6.0.x** | ✅ Fully Tested | Enhanced features |
| **6.1.x** | ✅ Fully Tested | LTS kernel |
| **6.5.x** | ✅ Fully Tested | Latest stable |
| **6.8.x** | ✅ Fully Tested | Maximum tested |
| **6.9.x+** | ⚠️ Experimental | Not extensively tested |

### **Compatibility Features**

The module automatically detects kernel version and adjusts:
- Thermal subsystem integration
- Platform device registration
- sysfs attribute creation
- Memory management patterns

---

## 📚 **API Reference**

### **Thermal Mode Values**

```c
#define THERMAL_MODE_QUIET      0
#define THERMAL_MODE_BALANCED   1
#define THERMAL_MODE_PERFORMANCE 2
#define THERMAL_MODE_CUSTOM     3
```

### **Legion Mode Values**

```c
#define LEGION_MODE_DISABLED    0
#define LEGION_MODE_ENABLED     1
```

### **Boolean Controls**

All boolean controls accept:
- `0` or `false` = Disabled
- `1` or `true` = Enabled

### **Generation Codes**

```c
#define LEGION_GEN_6    6
#define LEGION_GEN_7    7
#define LEGION_GEN_8    8
#define LEGION_GEN_9    9
```

---

## 🔒 **Security Considerations**

### **Permissions**

The module creates sysfs attributes with appropriate permissions:
- Read-only attributes: `0444` (world-readable)
- Write attributes: `0644` (root-writable, world-readable)

### **Input Validation**

All user inputs are validated:
- Range checking for numeric values
- Capability checking before operations
- Safe ACPI method calls

### **Privilege Requirements**

- Module loading: Requires root privileges
- Hardware control: Requires root or legion group membership
- Reading status: Available to all users

---

## 🤝 **Contributing**

### **Development Setup**

```bash
# Clone the repository
git clone https://github.com/vivekchamoli/LenovoLegion7iLinux.git
cd LenovoLegion7iLinux/kernel-module/

# Install development dependencies
sudo apt install build-essential linux-headers-$(uname -r) dkms

# Test build
make clean
make all
```

### **Adding Support for New Models**

1. Update DMI table in `enhanced_legion_laptop.c`
2. Add generation-specific ACPI methods
3. Update capability matrix
4. Test thoroughly
5. Submit pull request

### **Code Style**

- Follow Linux kernel coding style
- Use appropriate debug messages
- Include comprehensive error handling
- Document all public interfaces

---

## 📞 **Support**

### **Getting Help**

- **Documentation**: [GitHub Repository](https://github.com/vivekchamoli/LenovoLegion7iLinux)
- **Issues**: [Report Bugs](https://github.com/vivekchamoli/LenovoLegion7iLinux/issues)
- **Discussions**: [Community Forum](https://github.com/vivekchamoli/LenovoLegion7iLinux/discussions)

### **Before Reporting Issues**

1. Run the compatibility test suite
2. Check kernel logs with debug enabled
3. Verify your Legion model is supported
4. Include system information in reports

### **Useful Information for Bug Reports**

```bash
# System information
uname -a
cat /sys/class/dmi/id/sys_vendor
cat /sys/class/dmi/id/product_name
cat /sys/class/dmi/id/product_version

# Module information
modinfo legion_laptop_enhanced
lsmod | grep legion

# Kernel logs
dmesg | grep legion_laptop_enhanced
```

---

## 📜 **License**

This module is licensed under GPL v2, compatible with the Linux kernel license.

**Author**: Vivek Chamoli <vivekchamoli@example.com>
**Version**: 2.0.0
**Last Updated**: September 2025

---

## 🚀 **Roadmap**

### **Future Enhancements**

- [ ] Support for Legion Gen 10+ models
- [ ] Enhanced RGB effects and animations
- [ ] Advanced fan curve customization
- [ ] Power management optimization
- [ ] Thermal throttling control
- [ ] Performance monitoring integration
- [ ] User-space daemon integration

### **Performance Improvements**

- [ ] Async ACPI method calls
- [ ] Cached hardware state
- [ ] Optimized memory usage
- [ ] Reduced CPU overhead

---

**🎉 Enhanced Legion Kernel Module - Bringing professional-grade hardware control to Legion laptops on Linux!**