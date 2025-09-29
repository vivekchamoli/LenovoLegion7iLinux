# Windows Setup Requirements and Instructions v6.0.0

**Copyright © 2025 Vivek Chamoli**
**Repository**: https://github.com/vivekchamoli/LenovoLegion7i

## Hardware Requirements

### Primary Target Hardware
- **Model**: Legion Slim 7i Gen 9 (16IRX9)
- **CPU**: Intel Core i9-14900HX (24 cores, 32 threads)
- **GPU**: NVIDIA RTX 4070 Laptop GPU (8GB GDDR6)
- **RAM**: 16GB+ DDR5 (32GB recommended)
- **Storage**: 1TB NVMe SSD (minimum 500MB free space)
- **Display**: 16" 3.2K 165Hz Mini-LED
- **Keyboard**: 4-zone RGB Spectrum lighting

### Software Requirements
- **Operating System**: Windows 10 (20H2) or Windows 11
- **Framework**: .NET 8.0 Desktop Runtime (auto-installed)
- **Privileges**: Administrator rights required
- **Drivers**: Latest NVIDIA drivers and Lenovo system drivers

## Installation Methods

### Method 1: Direct Download (Recommended)
```cmd
REM Download from GitHub releases
start https://github.com/vivekchamoli/LenovoLegion7i/releases/latest

REM Download: LenovoLegionToolkitSetup.exe (6.9MB)
REM Run as Administrator and follow installation wizard
```

### Method 2: Package Managers

#### Using winget
```cmd
REM Install using Windows Package Manager
winget install VivekChamoli.LenovoLegion7iToolkit
```

#### Using Scoop
```cmd
REM Add required buckets
scoop bucket add versions
scoop bucket add extras

REM Install the toolkit
scoop install extras/lenovolegion7itoolkit
```

## Pre-Installation Requirements

### 1. Hardware Verification
```cmd
REM Verify hardware model
wmic computersystem get model
REM Should show: Legion Slim 7i Gen 9 or contain "16IRX9"

REM Check CPU
wmic cpu get name
REM Should show: Intel(R) Core(TM) i9-14900HX CPU

REM Verify GPU
wmic path win32_VideoController get name
REM Should show: NVIDIA GeForce RTX 4070 Laptop GPU
```

### 2. Required Drivers
Ensure these drivers are installed before running Legion Toolkit:

1. **Lenovo Energy Management Driver**
   - Required for power mode switching
   - Available through Lenovo Vantage or Lenovo Support

2. **Lenovo Vantage Gaming Feature Driver**
   - Required for advanced gaming features
   - Must be present even if Vantage is disabled

3. **NVIDIA Graphics Drivers**
   - Latest Game Ready drivers recommended
   - Required for GPU overclocking features

4. **Intel Chipset Drivers**
   - Latest drivers from Intel or Lenovo
   - Required for thermal management

### 3. .NET 8 Runtime
The installer automatically handles .NET 8 installation, but if manual installation is needed:

```cmd
REM Download .NET 8 Desktop Runtime x64
start https://dotnet.microsoft.com/en-us/download/dotnet/8.0

REM Verify installation
dotnet --info
```

Expected output should include:
- `Microsoft.NETCore.App 8.x.x`
- `Microsoft.WindowsDesktop.App 8.x.x`

## Post-Installation Setup

### 1. Initial Configuration
```cmd
REM Launch Legion Toolkit as Administrator
"C:\Program Files\Lenovo Legion Toolkit\Lenovo Legion Toolkit.exe"

REM Enable autorun and minimize on close in Settings
REM Disable Lenovo Vantage and Hotkeys if present
```

### 2. Feature Verification
- **Power Modes**: Test Fn+Q cycling (Quiet → Balanced → Performance → Custom)
- **RGB Lighting**: Verify 4-zone keyboard control
- **Thermal Monitoring**: Check CPU/GPU temperature readings
- **Fan Control**: Test custom fan curves in Custom mode
- **GPU Overclocking**: Verify NVIDIA GPU controls (if enabled in BIOS)

### 3. Conflicting Software Management
Legion Toolkit can automatically disable conflicting software:
- Lenovo Vantage
- Legion Zone
- Lenovo Hotkeys Service

**Recommended**: Use the disable option instead of uninstalling to avoid driver issues.

## Advanced Configuration

### Custom Mode Requirements
For Custom Mode to function properly, ensure BIOS version meets minimum requirements:
- **G9CN**: Version 24 or higher
- **GKCN**: Version 46 or higher
- **H1CN**: Version 39 or higher
- **HACN**: Version 31 or higher
- **HHCN**: Version 20 or higher

### RGB Lighting Setup
1. Disable Vantage services to avoid conflicts
2. Ensure no other RGB software (iCue, OpenRGB) is actively controlling keyboard
3. Use `--force-disable-rgbkb` argument if conflicts persist

### Command Line Interface
Enable CLI access in Settings, then add to PATH:
```cmd
REM Add CLI to system PATH
setx PATH "%PATH%;C:\Program Files\Lenovo Legion Toolkit"

REM Test CLI functionality
llt.exe feature --list
```

## Troubleshooting

### Common Issues

#### 1. Application Won't Start
```cmd
REM Check .NET installation
dotnet --info

REM Run with elevated privileges
runas /user:Administrator "C:\Program Files\Lenovo Legion Toolkit\Lenovo Legion Toolkit.exe"

REM Check for conflicting processes
tasklist | findstr /i "vantage\|legion\|ImController"
```

#### 2. Hardware Not Detected
- Verify Legion Slim 7i Gen 9 hardware model
- Ensure latest BIOS version is installed
- Check that required Lenovo drivers are present
- Use `--skip-compat-check` argument only if certain of compatibility

#### 3. RGB Controls Not Working
- Stop all Lenovo Vantage services
- Check for Riot Vanguard conflicts (known issue)
- Verify BIOS RGB settings are enabled
- Use RGB disable arguments if conflicts persist

#### 4. Performance Mode Issues
- Ensure AC adapter is connected for Performance/Custom modes
- Update BIOS to minimum required version
- Check AI Engine settings in BIOS if issues persist

### Advanced Arguments
For specific scenarios, use these command line arguments:

```cmd
REM Debug logging
"Lenovo Legion Toolkit.exe" --trace

REM Allow all power modes on battery (not recommended)
"Lenovo Legion Toolkit.exe" --allow-all-power-modes-on-battery

REM Skip compatibility check (use with caution)
"Lenovo Legion Toolkit.exe" --skip-compat-check

REM Disable RGB features to avoid conflicts
"Lenovo Legion Toolkit.exe" --force-disable-rgbkb --force-disable-spectrumkb
```

## Performance Optimization

### Recommended Settings
1. **Autorun**: Enable for background functionality
2. **Minimize on close**: Keep toolkit running for Actions and monitoring
3. **Update checking**: Enable for latest features and fixes
4. **Power plan sync**: Configure Windows Power Mode synchronization

### Performance Verification
```cmd
REM Check application performance
tasklist /fi "imagename eq Lenovo Legion Toolkit.exe" /fo table

REM Monitor resource usage
typeperf "\Process(Lenovo Legion Toolkit)\% Processor Time" -sc 10
```

Expected resource usage:
- **CPU**: <1% during idle operation
- **Memory**: <100MB typical usage
- **Disk**: Minimal I/O except during updates

---

## Hardware-Specific Optimizations

### Intel Core i9-14900HX Settings
- **P-cores**: 8 cores, boost to 5.8GHz
- **E-cores**: 16 cores, boost to 4.1GHz
- **TDP Settings**: 55W base, 140W turbo (configurable in Custom mode)
- **Thermal Limits**: 100°C maximum, 85°C recommended

### NVIDIA RTX 4070 Laptop GPU
- **Base Clock**: 1980MHz
- **Boost Clock**: 2175MHz
- **Memory**: 8GB GDDR6 at 16Gbps
- **TGP Range**: 85W-140W (configurable)
- **Overclocking**: Available when enabled in BIOS

### Thermal Management
- **Vapor Chamber**: Advanced cooling system
- **Dual Fans**: Independent speed control
- **Zero RPM Mode**: Fans stop under light loads
- **AI Thermal**: Machine learning optimization

---

**Legion Toolkit v6.0.0 - Windows Setup Requirements**
**Optimized for Legion Slim 7i Gen 9 (16IRX9) Hardware**
**Copyright © 2025 Vivek Chamoli**
**Repository**: https://github.com/vivekchamoli/LenovoLegion7i