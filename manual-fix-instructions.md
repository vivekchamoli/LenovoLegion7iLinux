# Manual Fix for libicu Dependency Issue

## Problem
```
legion-toolkit : Depends: libicu67 but it is not installable or
                          libicu66 but it is not installable or
                          libicu70 but it is not installable
```

## Quick Fix (Immediate Solution)

### Step 1: Check Your Ubuntu Version
```bash
lsb_release -a
```

### Step 2: Find Available libicu Package
```bash
apt-cache search libicu | grep -E "^libicu[0-9]+"
```

### Step 3: Install the Available libicu Package
```bash
# For Ubuntu 24.04+
sudo apt install libicu74

# For Ubuntu 22.04/23.04
sudo apt install libicu72

# For Ubuntu 20.04/21.04
sudo apt install libicu70

# For Ubuntu 18.04
sudo apt install libicu60

# Or install whatever version is available on your system
sudo apt install $(apt-cache search '^libicu[0-9]+$' | tail -1 | awk '{print $1}')
```

### Step 4: Install Legion Toolkit Package
```bash
sudo dpkg -i legion-toolkit_*.deb
sudo apt-get install -f
```

## Ubuntu Version to libicu Mapping

| Ubuntu Version | libicu Package | Status |
|---------------|----------------|---------|
| 24.04 LTS     | libicu74      | Current |
| 23.10         | libicu72      | Current |
| 23.04         | libicu72      | EOL |
| 22.04 LTS     | libicu70      | Current |
| 21.10         | libicu70      | EOL |
| 21.04         | libicu67      | EOL |
| 20.04 LTS     | libicu66      | Current |
| 18.04 LTS     | libicu60      | EOL |

## Permanent Fix (For Package Builders)

### Option 1: Use the Automated Fix Scripts
```bash
# Apply the fix to existing package files
chmod +x apply-libicu-fix.sh
./apply-libicu-fix.sh

# Rebuild your package
./Scripts/build-ubuntu-package.sh
```

### Option 2: Manual Update of debian/control
Replace the Depends line in `debian/control` with:
```
Depends: ${shlibs:Depends},
         ${misc:Depends},
         libicu74 | libicu72 | libicu70 | libicu67 | libicu66 | libicu60,
         libssl3 | libssl1.1,
         libstdc++6,
         libc6,
         ca-certificates
```

### Option 3: Self-Contained Build
Build with `--self-contained true` to include all dependencies:
```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

## Alternative Solutions

### Use AppImage (No Dependencies)
AppImage packages include all dependencies:
```bash
# Download/build AppImage version
chmod +x LegionToolkit-*.AppImage
./LegionToolkit-*.AppImage
```

### Use Snap Package
Snap packages handle dependencies automatically:
```bash
sudo snap install legion-toolkit --dangerous --devmode
```

### Manual Installation
Install without package manager:
```bash
# Extract the binary and install manually
sudo cp legion-toolkit /usr/local/bin/
sudo chmod +x /usr/local/bin/legion-toolkit
```

## Testing Your Fix

After applying any fix:

1. **Check Dependencies**:
   ```bash
   dpkg-deb --info legion-toolkit_*.deb
   ```

2. **Test Installation**:
   ```bash
   sudo dpkg -i legion-toolkit_*.deb
   sudo apt-get install -f
   ```

3. **Verify Installation**:
   ```bash
   legion-toolkit --version
   ```

## Root Cause

The libicu library provides Unicode support and has different version numbers across Ubuntu releases. .NET applications require specific libicu versions, but the package was built with hardcoded dependencies instead of flexible alternatives.

## Prevention

For future packages:
- Use flexible dependency strings with multiple alternatives
- Test on multiple Ubuntu versions
- Consider self-contained builds for complex dependencies
- Use virtual packages when available (like `libicu-dev`)

## Support

If these fixes don't work:
1. Share your Ubuntu version: `lsb_release -a`
2. List available libicu: `apt-cache search libicu`
3. Share the exact error message
4. Try the self-contained build option