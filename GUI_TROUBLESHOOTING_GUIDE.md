# 🐛 Legion Toolkit GUI Launch Troubleshooting Guide

## 📋 **Quick Diagnosis**

If the Legion Toolkit GUI isn't launching after installation, run the diagnostic tool first:

```bash
# Run comprehensive diagnostic
legion-toolkit-debug

# Try manual GUI launch
legion-toolkit-gui

# Direct binary launch (for debugging)
/usr/bin/LegionToolkit
```

---

## 🔍 **Common Issues & Solutions**

### **Issue 1: Application Menu Entry Not Visible**

**Symptoms:**
- Legion Toolkit doesn't appear in application menu
- No icon in desktop environment

**Solutions:**
```bash
# Update desktop database
sudo update-desktop-database /usr/share/applications

# Update icon cache
sudo gtk-update-icon-cache -f -t /usr/share/icons/hicolor

# For KDE users
kbuildsycoca5

# Restart desktop environment or log out/in
```

### **Issue 2: GUI Launches But Window Not Visible**

**Symptoms:**
- Process starts but no window appears
- App appears in taskbar but no visible window

**Solutions:**
```bash
# Check if process is running
ps aux | grep LegionToolkit

# Kill any running instances
pkill -f LegionToolkit

# Check display environment
echo $DISPLAY
echo $XDG_SESSION_TYPE

# Try with explicit display
DISPLAY=:0 /usr/bin/LegionToolkit

# For Wayland sessions
GDK_BACKEND=wayland /usr/bin/LegionToolkit
```

### **Issue 3: Missing Dependencies**

**Symptoms:**
- Error messages about missing libraries
- Crashes on startup

**Solutions:**
```bash
# Check missing dependencies
ldd /usr/bin/LegionToolkit | grep "not found"

# Install common dependencies (Ubuntu/Debian)
sudo apt update
sudo apt install libicu72 libssl3 libx11-6 libfontconfig1 \
                 libharfbuzz0b libfreetype6 libxext6 libxrandr2 \
                 libxi6 libxcursor1 libglib2.0-0 libgtk-3-0

# For older systems, try alternative packages
sudo apt install libicu70 libssl1.1 libgtk-4-1
```

### **Issue 4: Permission Errors**

**Symptoms:**
- "Permission denied" errors
- Hardware features not working

**Solutions:**
```bash
# Add user to legion group
sudo usermod -a -G legion $USER

# Log out and log back in, then verify
groups | grep legion

# Check file permissions
ls -la /usr/bin/LegionToolkit

# Fix permissions if needed
sudo chmod +x /usr/bin/LegionToolkit
sudo chmod +x /usr/bin/legion-toolkit-gui
```

### **Issue 5: X11/Wayland Compatibility**

**Symptoms:**
- Works in X11 but not Wayland (or vice versa)
- Display server conflicts

**Solutions:**
```bash
# Check current session type
echo $XDG_SESSION_TYPE

# For X11 sessions
export DISPLAY=:0
xset q  # Should show display info

# For Wayland sessions with XWayland
export GDK_BACKEND="wayland,x11"
export QT_QPA_PLATFORM="wayland;xcb"

# Force X11 backend on Wayland
GDK_BACKEND=x11 QT_QPA_PLATFORM=xcb /usr/bin/LegionToolkit
```

---

## 🔧 **Advanced Troubleshooting**

### **Debug Launch Script**

Create a debug launch script to capture detailed error information:

```bash
# Create debug script
cat > ~/debug-legion-toolkit.sh << 'EOF'
#!/bin/bash
echo "=== Legion Toolkit Debug Launch ==="
echo "Date: $(date)"
echo "User: $(whoami)"
echo "Groups: $(groups)"
echo "Display: $DISPLAY"
echo "Session: $XDG_SESSION_TYPE"
echo "Desktop: $XDG_CURRENT_DESKTOP"
echo ""

echo "=== Environment ==="
env | grep -E "(DISPLAY|XDG|GTK|QT)" | sort
echo ""

echo "=== Binary Check ==="
ls -la /usr/bin/LegionToolkit
echo ""

echo "=== Dependencies ==="
ldd /usr/bin/LegionToolkit | head -10
echo ""

echo "=== Launch Attempt ==="
strace -e trace=openat,connect /usr/bin/LegionToolkit 2>&1 | head -20
EOF

chmod +x ~/debug-legion-toolkit.sh
~/debug-legion-toolkit.sh
```

### **Log Analysis**

Check system logs for errors:

```bash
# Check user session logs
journalctl --user -f | grep -i legion &

# Launch application and watch logs
legion-toolkit-gui

# Check system logs
sudo journalctl -f | grep -i legion

# Check X11 logs
less /var/log/Xorg.0.log | grep -i error
```

### **Manual Desktop File Creation**

If desktop integration fails, create a manual entry:

```bash
# Create user-specific desktop file
mkdir -p ~/.local/share/applications

cat > ~/.local/share/applications/legion-toolkit-manual.desktop << EOF
[Desktop Entry]
Version=1.0
Type=Application
Name=Legion Toolkit (Manual)
Comment=Manual Legion Toolkit launcher
Exec=/usr/bin/legion-toolkit-gui %F
Icon=legion-toolkit
Categories=System;Settings;
Terminal=false
StartupNotify=true
TryExec=/usr/bin/legion-toolkit-gui
EOF

# Update user desktop database
update-desktop-database ~/.local/share/applications
```

---

## 🖥️ **Desktop Environment Specific Fixes**

### **GNOME/Ubuntu**

```bash
# Reset GNOME shell (if GUI freezes)
busctl --user call org.gnome.Shell /org/gnome/Shell org.gnome.Shell Eval s 'Meta.restart("Restarting…")'

# Check GNOME extensions conflicts
gnome-extensions list --enabled

# Launch with GNOME-specific settings
GDK_BACKEND=x11 /usr/bin/LegionToolkit
```

### **KDE Plasma**

```bash
# Update KDE service cache
kbuildsycoca5

# Reset plasma if needed
plasma-desktop --replace &

# KDE-specific launch
QT_QPA_PLATFORM=xcb /usr/bin/LegionToolkit
```

### **XFCE**

```bash
# Update XFCE panel
xfce4-panel --restart

# Clear XFCE cache
rm -rf ~/.cache/xfce4

# XFCE-specific launch
GTK_THEME=Adwaita /usr/bin/LegionToolkit
```

---

## 📊 **System Requirements Verification**

### **Minimum Requirements**

```bash
# Check system specifications
echo "=== System Info ==="
uname -a
lsb_release -a 2>/dev/null || cat /etc/os-release
echo ""

echo "=== Desktop Environment ==="
echo "Current: $XDG_CURRENT_DESKTOP"
echo "Session: $XDG_SESSION_TYPE"
echo ""

echo "=== Display Server ==="
if [ -n "$DISPLAY" ]; then
    echo "X11 Display: $DISPLAY"
    xrandr --listmonitors 2>/dev/null || echo "xrandr not available"
fi

if [ -n "$WAYLAND_DISPLAY" ]; then
    echo "Wayland Display: $WAYLAND_DISPLAY"
fi
echo ""

echo "=== Memory ==="
free -h
echo ""

echo "=== Graphics ==="
lspci | grep -i vga
```

### **Supported Distributions**

- ✅ Ubuntu 20.04+ (LTS recommended)
- ✅ Debian 11+ (Bullseye+)
- ✅ Fedora 35+
- ✅ openSUSE Leap 15.4+
- ✅ Arch Linux (current)
- ✅ Pop!_OS 22.04+
- ✅ Linux Mint 21+

---

## 🚑 **Emergency Solutions**

### **Complete Reset**

If nothing works, try a complete reset:

```bash
# Remove and reinstall
sudo dpkg -r legion-toolkit
sudo apt autoremove
sudo apt autoclean

# Clear user cache
rm -rf ~/.cache/legion-toolkit
rm -rf ~/.config/legion-toolkit
rm -rf ~/.local/share/applications/legion-toolkit*

# Reinstall
sudo dpkg -i legion-toolkit_3.0.0_amd64.deb
sudo apt install -f

# Add user to group again
sudo usermod -a -G legion $USER
```

### **Alternative Launch Methods**

```bash
# Method 1: Direct binary with environment
env DISPLAY=:0 GDK_BACKEND=x11 QT_QPA_PLATFORM=xcb /usr/bin/LegionToolkit

# Method 2: Through desktop file
gtk-launch legion-toolkit

# Method 3: Using application launcher
dex /usr/share/applications/legion-toolkit.desktop

# Method 4: Background launch
nohup /usr/bin/LegionToolkit > /tmp/legion-toolkit.log 2>&1 &
```

---

## 📞 **Getting Further Help**

### **Information to Include in Bug Reports**

```bash
# Generate comprehensive system report
cat > ~/legion-toolkit-bug-report.txt << 'EOF'
=== Legion Toolkit Bug Report ===
Date: $(date)

=== System Information ===
$(uname -a)
$(lsb_release -a 2>/dev/null || cat /etc/os-release)

=== Installation ===
$(dpkg -l | grep legion)

=== Environment ===
$(env | grep -E "(DISPLAY|XDG|GTK|QT|WAYLAND)" | sort)

=== Binary Status ===
$(ls -la /usr/bin/LegionToolkit)
$(ls -la /usr/bin/legion-toolkit-gui)

=== Dependencies ===
$(ldd /usr/bin/LegionToolkit 2>&1)

=== Desktop Integration ===
$(ls -la /usr/share/applications/legion-toolkit*)

=== Error Logs ===
$(journalctl --user --since "1 hour ago" | grep -i legion)

=== Hardware ===
$(lspci | grep -i vga)
$(cat /sys/class/dmi/id/product_name)
$(cat /sys/class/dmi/id/product_version)
EOF

echo "Bug report saved to: ~/legion-toolkit-bug-report.txt"
```

### **Support Channels**

- **GitHub Issues**: [Report Issues](https://github.com/vivekchamoli/LenovoLegion7iLinux/issues)
- **Documentation**: [User Guide](https://github.com/vivekchamoli/LenovoLegion7iLinux)
- **Community**: [Discussions](https://github.com/vivekchamoli/LenovoLegion7iLinux/discussions)

---

## ✅ **Success Verification**

Once the GUI launches successfully:

```bash
# Verify application is working
legion-toolkit --version

# Check hardware detection
legion-toolkit-debug

# Test basic functionality
legion-toolkit status

# Verify group membership
groups | grep legion
```

---

**💡 Pro Tip**: Always run `legion-toolkit-debug` first when troubleshooting GUI issues. It provides comprehensive diagnostic information and often identifies the exact problem automatically.

**🔄 Remember**: After any system changes (updates, group additions, etc.), log out and log back in or restart to ensure all changes take effect.