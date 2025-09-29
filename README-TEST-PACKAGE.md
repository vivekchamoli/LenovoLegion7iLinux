# Legion Toolkit Test Package for Ubuntu

This directory contains a test Ubuntu package for Legion Toolkit that you can install and test on your Ubuntu system.

## What's Included

- **legion-toolkit-test/**: Complete Debian package structure
- **build-test-package.sh**: Script to build the .deb package on Ubuntu
- **README-TEST-PACKAGE.md**: This file

## Features of the Test Package

The test package includes:
- Command-line system information tool
- Basic hardware detection
- Legion laptop detection (requires root)
- Thermal monitoring (temperature sensors and fans)
- Simple GUI mode with tkinter
- Proper Ubuntu package integration

## Installation Instructions

### Step 1: Transfer to Ubuntu System
Copy the entire `legion-toolkit-test/` directory and `build-test-package.sh` to your Ubuntu system.

### Step 2: Build the Package (on Ubuntu)
```bash
# Navigate to the directory containing legion-toolkit-test/
cd /path/to/legion-toolkit-test-directory

# Make the build script executable
chmod +x build-test-package.sh

# Build the package
./build-test-package.sh
```

### Step 3: Install the Package
```bash
# Install the package
sudo dpkg -i legion-toolkit-test.deb

# If there are dependency issues, fix them
sudo apt-get install -f
```

## Usage

### Command Line Mode
```bash
# Basic system information
legion-toolkit-test

# Show help
legion-toolkit-test --help
```

### GUI Mode
```bash
# Launch GUI (requires python3-tk)
legion-toolkit-test --gui

# Install GUI dependencies if needed
sudo apt install python3-tk
```

### Desktop Integration
After installation, you should find "Legion Toolkit Test" in your applications menu under the System category.

## What the Test Shows

The test application will display:

1. **System Information**
   - Hostname, kernel version, distribution

2. **Hardware Information**
   - CPU model, memory, vendor/model (with root)

3. **Legion Hardware Check**
   - Detects if running on a Legion laptop

4. **Thermal Information**
   - Temperature sensors from `/sys/class/thermal/`
   - Fan speeds from `/sys/class/hwmon/`

## Testing Scenarios

1. **Basic Package Installation**
   - Verify package installs without errors
   - Check that files are placed correctly

2. **Command Line Functionality**
   - Run `legion-toolkit-test` and verify output
   - Test help command

3. **GUI Functionality**
   - Test GUI mode if tkinter is available
   - Verify desktop integration

4. **Hardware Detection**
   - Test with and without root privileges
   - Verify thermal sensor detection

5. **Package Removal**
   - Test uninstallation: `sudo apt remove legion-toolkit-test`

## Dependencies

The package depends on:
- python3 (>= 3.8)
- python3-gi
- python3-psutil
- acpi
- lm-sensors

Optional for GUI:
- python3-tk

## Troubleshooting

### Package Won't Install
```bash
# Check package integrity
dpkg-deb --info legion-toolkit-test.deb

# Fix broken dependencies
sudo apt-get install -f
```

### Permission Issues
```bash
# Some hardware detection requires root
sudo legion-toolkit-test
```

### Missing GUI
```bash
# Install tkinter for GUI mode
sudo apt install python3-tk
```

## Expected Output Example

```
============================================================
LEGION TOOLKIT TEST APPLICATION
============================================================

SYSTEM INFORMATION:
------------------------------
Hostname: ubuntu-test
Kernel: 5.15.0-58-generic
Distro: Ubuntu 22.04.1 LTS

HARDWARE INFORMATION:
------------------------------
Cpu: Intel(R) Core(TM) i7-12700H CPU @ 2.30GHz
Memory: 16384 MB
Vendor: LENOVO
Model: 82UG

LEGION HARDWARE CHECK:
------------------------------
✓ Legion laptop detected: 82ug

THERMAL INFORMATION:
------------------------------
Temperature sensors:
  x86_pkg_temp: 45.0°C
  acpi-0: 43.0°C

Fan sensors:
  fan1: 2850 RPM
  fan2: 2650 RPM
```

## Next Steps

If this test package works correctly:
1. The package installation process is validated
2. Basic Python dependencies are confirmed
3. System integration is working
4. Ready for the full Legion Toolkit package

## Support

If you encounter issues:
1. Check the package build output for errors
2. Verify all dependencies are installed
3. Test on a clean Ubuntu system
4. Report issues with system details

This test package validates the Ubuntu packaging pipeline before building the full Legion Toolkit.