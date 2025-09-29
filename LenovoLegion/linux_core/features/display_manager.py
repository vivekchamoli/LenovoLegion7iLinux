#!/usr/bin/env python3
"""
Legion Toolkit Display Management - Linux Implementation
Complete display features with HDR, G-Sync, refresh rate, and brightness control
Provides feature parity with Windows display management features
"""

import os
import sys
import subprocess
import asyncio
from pathlib import Path
from typing import Dict, Optional, List, Any, Tuple
from dataclasses import dataclass
from datetime import datetime
import logging
import json
import re

@dataclass
class DisplayInfo:
    """Display information structure"""
    name: str
    connector: str  # eDP-1, HDMI-A-1, etc.
    width: int
    height: int
    refresh_rate: float
    connected: bool
    primary: bool
    brightness: int  # 0-100%
    hdr_supported: bool
    hdr_enabled: bool
    gsync_supported: bool
    gsync_enabled: bool
    overdrive_supported: bool
    overdrive_level: int
    color_depth: int  # bits per channel
    color_space: str  # sRGB, DCI-P3, etc.

@dataclass
class DisplaySettings:
    """Display management settings"""
    auto_brightness: bool = False
    hdr_auto_switch: bool = True
    gsync_enabled: bool = True
    overdrive_level: int = 2  # 0=Off, 1=Normal, 2=Extreme
    night_light: bool = False
    night_light_temperature: int = 4000  # Kelvin
    refresh_rate_mode: str = "auto"  # auto, fixed_60, fixed_165
    power_saving_brightness: bool = True

class DisplayManager:
    """Advanced display management system"""

    def __init__(self):
        self.logger = logging.getLogger("legion.display")
        self.settings = DisplaySettings()
        self.config_file = Path.home() / '.config' / 'legion-toolkit' / 'display.json'

        # Kernel module interface
        self.kernel_module_path = "/sys/kernel/legion_laptop"

        # Display monitoring
        self.monitoring_active = False
        self.monitoring_task = None
        self.current_displays = {}

        # Brightness control paths
        self.brightness_paths = [
            "/sys/class/backlight/intel_backlight",
            "/sys/class/backlight/acpi_video0",
            "/sys/class/backlight/nvidia_0",
            "/sys/class/backlight/amdgpu_bl0"
        ]

    async def initialize(self) -> bool:
        """Initialize display management system"""
        try:
            # Load saved settings
            await self.load_settings()

            # Detect displays
            await self.detect_displays()

            # Apply initial settings
            await self.apply_settings()

            # Start monitoring
            await self.start_monitoring()

            self.logger.info("Display management initialized")
            return True

        except Exception as e:
            self.logger.error(f"Display initialization failed: {e}")
            return False

    async def detect_displays(self) -> List[DisplayInfo]:
        """Detect all connected displays"""
        try:
            displays = []

            # Try xrandr first (X11)
            displays.extend(await self._detect_displays_xrandr())

            # Try wlr-randr for Wayland
            if not displays:
                displays.extend(await self._detect_displays_wayland())

            # Try DRM interface directly
            if not displays:
                displays.extend(await self._detect_displays_drm())

            # Update internal state
            self.current_displays = {display.connector: display for display in displays}

            self.logger.info(f"Detected {len(displays)} displays")
            return displays

        except Exception as e:
            self.logger.error(f"Display detection failed: {e}")
            return []

    async def _detect_displays_xrandr(self) -> List[DisplayInfo]:
        """Detect displays using xrandr (X11)"""
        try:
            result = subprocess.run(['xrandr', '--verbose'],
                                  capture_output=True, text=True, timeout=10)

            if result.returncode != 0:
                return []

            displays = []
            current_display = None

            for line in result.stdout.split('\n'):
                line = line.strip()

                # Display connection line
                if ' connected' in line or ' disconnected' in line:
                    if current_display:
                        displays.append(current_display)

                    parts = line.split()
                    connector = parts[0]
                    connected = 'connected' in line
                    primary = 'primary' in line

                    current_display = DisplayInfo(
                        name=connector,
                        connector=connector,
                        width=0, height=0, refresh_rate=0.0,
                        connected=connected, primary=primary,
                        brightness=100, hdr_supported=False, hdr_enabled=False,
                        gsync_supported=False, gsync_enabled=False,
                        overdrive_supported=False, overdrive_level=0,
                        color_depth=8, color_space="sRGB"
                    )

                    # Parse resolution if connected
                    if connected:
                        resolution_match = re.search(r'(\d+)x(\d+)\+\d+\+\d+', line)
                        if resolution_match:
                            current_display.width = int(resolution_match.group(1))
                            current_display.height = int(resolution_match.group(2))

                # Brightness information
                elif 'Brightness:' in line and current_display:
                    brightness_match = re.search(r'Brightness: ([\d.]+)', line)
                    if brightness_match:
                        brightness = float(brightness_match.group(1))
                        current_display.brightness = int(brightness * 100)

                # Refresh rate information
                elif '*' in line and '+' in line and current_display:
                    # Current mode line
                    parts = line.split()
                    for part in parts:
                        if '*' in part:
                            try:
                                current_display.refresh_rate = float(part.replace('*', '').replace('+', ''))
                            except ValueError:
                                pass

            # Add last display
            if current_display:
                displays.append(current_display)

            # Check for HDR and other advanced features
            for display in displays:
                if display.connected:
                    await self._check_advanced_features(display)

            return displays

        except (subprocess.TimeoutExpired, FileNotFoundError):
            return []

    async def _detect_displays_wayland(self) -> List[DisplayInfo]:
        """Detect displays using wlr-randr (Wayland)"""
        try:
            result = subprocess.run(['wlr-randr'],
                                  capture_output=True, text=True, timeout=10)

            if result.returncode != 0:
                return []

            displays = []
            current_display = None

            for line in result.stdout.split('\n'):
                line = line.strip()

                if line and not line.startswith(' '):
                    # New display
                    if current_display:
                        displays.append(current_display)

                    connector = line.split()[0]
                    current_display = DisplayInfo(
                        name=connector,
                        connector=connector,
                        width=0, height=0, refresh_rate=0.0,
                        connected=True, primary=False,
                        brightness=100, hdr_supported=False, hdr_enabled=False,
                        gsync_supported=False, gsync_enabled=False,
                        overdrive_supported=False, overdrive_level=0,
                        color_depth=8, color_space="sRGB"
                    )

                elif line.startswith('current') and current_display:
                    # Parse current mode
                    match = re.search(r'(\d+) x (\d+) @ ([\d.]+) Hz', line)
                    if match:
                        current_display.width = int(match.group(1))
                        current_display.height = int(match.group(2))
                        current_display.refresh_rate = float(match.group(3))

            if current_display:
                displays.append(current_display)

            return displays

        except (subprocess.TimeoutExpired, FileNotFoundError):
            return []

    async def _detect_displays_drm(self) -> List[DisplayInfo]:
        """Detect displays using DRM interface"""
        try:
            displays = []
            drm_path = Path("/sys/class/drm")

            if not drm_path.exists():
                return []

            for connector_path in drm_path.glob("card0-*"):
                connector_name = connector_path.name.replace("card0-", "")

                # Check connection status
                status_file = connector_path / "status"
                if not status_file.exists():
                    continue

                with open(status_file, 'r') as f:
                    status = f.read().strip()

                connected = status == "connected"

                display = DisplayInfo(
                    name=connector_name,
                    connector=connector_name,
                    width=0, height=0, refresh_rate=0.0,
                    connected=connected, primary=False,
                    brightness=100, hdr_supported=False, hdr_enabled=False,
                    gsync_supported=False, gsync_enabled=False,
                    overdrive_supported=False, overdrive_level=0,
                    color_depth=8, color_space="sRGB"
                )

                if connected:
                    # Try to get mode information
                    modes_file = connector_path / "modes"
                    if modes_file.exists():
                        with open(modes_file, 'r') as f:
                            modes = f.read().strip().split('\n')
                            if modes and modes[0]:
                                # Parse first mode (usually current)
                                mode_match = re.match(r'(\d+)x(\d+)', modes[0])
                                if mode_match:
                                    display.width = int(mode_match.group(1))
                                    display.height = int(mode_match.group(2))

                displays.append(display)

            return displays

        except Exception:
            return []

    async def _check_advanced_features(self, display: DisplayInfo):
        """Check for HDR, G-Sync, and other advanced features"""
        try:
            # Check for HDR support
            display.hdr_supported = await self._check_hdr_support(display.connector)

            # Check for G-Sync/FreeSync support
            display.gsync_supported = await self._check_gsync_support(display.connector)

            # Check current HDR status
            if display.hdr_supported:
                display.hdr_enabled = await self._get_hdr_status(display.connector)

            # Check G-Sync status
            if display.gsync_supported:
                display.gsync_enabled = await self._get_gsync_status(display.connector)

            # Legion-specific features
            if "eDP" in display.connector:  # Internal display
                display.overdrive_supported = True
                display.overdrive_level = await self._get_overdrive_level()

        except Exception as e:
            self.logger.debug(f"Advanced feature check failed for {display.connector}: {e}")

    async def _check_hdr_support(self, connector: str) -> bool:
        """Check if display supports HDR"""
        try:
            # Check DRM properties
            drm_path = Path(f"/sys/class/drm/card0-{connector}")
            if drm_path.exists():
                # Look for HDR-related properties
                for prop_file in drm_path.glob("*hdr*"):
                    return True

                # Check EDID for HDR capabilities
                edid_file = drm_path / "edid"
                if edid_file.exists():
                    # This would require EDID parsing - simplified check
                    with open(edid_file, 'rb') as f:
                        edid_data = f.read()
                        # HDR support indicated by specific EDID extensions
                        return len(edid_data) > 256  # Extended EDID usually indicates HDR

            return False

        except Exception:
            return False

    async def _check_gsync_support(self, connector: str) -> bool:
        """Check if display supports G-Sync/FreeSync"""
        try:
            # Check for variable refresh rate support
            result = subprocess.run(['xrandr', '--props'],
                                  capture_output=True, text=True, timeout=5)

            if result.returncode == 0:
                output = result.stdout
                # Look for VRR-related properties
                return any(vrr_prop in output.lower() for vrr_prop in
                          ['vrr', 'freesync', 'g-sync', 'adaptive sync'])

            return False

        except (subprocess.TimeoutExpired, FileNotFoundError):
            return False

    async def get_brightness(self, display: Optional[str] = None) -> Optional[int]:
        """Get display brightness (0-100%)"""
        try:
            if display and display in self.current_displays:
                # Try to get brightness for specific external display
                return await self._get_external_brightness(display)

            # Get internal display brightness
            for brightness_path in self.brightness_paths:
                path = Path(brightness_path)
                if path.exists():
                    brightness_file = path / "brightness"
                    max_brightness_file = path / "max_brightness"

                    if brightness_file.exists() and max_brightness_file.exists():
                        with open(brightness_file, 'r') as f:
                            current = int(f.read().strip())
                        with open(max_brightness_file, 'r') as f:
                            maximum = int(f.read().strip())

                        return int((current / maximum) * 100)

            return None

        except Exception as e:
            self.logger.error(f"Failed to get brightness: {e}")
            return None

    async def set_brightness(self, brightness: int, display: Optional[str] = None) -> bool:
        """Set display brightness (0-100%)"""
        try:
            if not 0 <= brightness <= 100:
                return False

            if display and display in self.current_displays:
                # Set brightness for specific external display
                return await self._set_external_brightness(display, brightness)

            # Set internal display brightness
            for brightness_path in self.brightness_paths:
                path = Path(brightness_path)
                if path.exists():
                    brightness_file = path / "brightness"
                    max_brightness_file = path / "max_brightness"

                    if brightness_file.exists() and max_brightness_file.exists():
                        with open(max_brightness_file, 'r') as f:
                            maximum = int(f.read().strip())

                        target_brightness = int((brightness / 100) * maximum)

                        # Write brightness
                        subprocess.run(['sudo', 'sh', '-c',
                                      f'echo {target_brightness} > {brightness_file}'],
                                     check=True, timeout=5)

                        self.logger.info(f"Set brightness to {brightness}%")
                        return True

            return False

        except Exception as e:
            self.logger.error(f"Failed to set brightness: {e}")
            return False

    async def _get_external_brightness(self, connector: str) -> Optional[int]:
        """Get brightness for external display"""
        try:
            # Try xrandr for external displays
            result = subprocess.run(['xrandr', '--verbose'],
                                  capture_output=True, text=True, timeout=5)

            if result.returncode == 0:
                lines = result.stdout.split('\n')
                for i, line in enumerate(lines):
                    if connector in line and 'connected' in line:
                        # Look for brightness in following lines
                        for j in range(i + 1, min(i + 10, len(lines))):
                            if 'Brightness:' in lines[j]:
                                brightness_match = re.search(r'Brightness: ([\d.]+)', lines[j])
                                if brightness_match:
                                    return int(float(brightness_match.group(1)) * 100)

            return None

        except Exception:
            return None

    async def _set_external_brightness(self, connector: str, brightness: int) -> bool:
        """Set brightness for external display"""
        try:
            brightness_value = brightness / 100.0

            result = subprocess.run(['xrandr', '--output', connector,
                                   '--brightness', str(brightness_value)],
                                  check=True, timeout=10)

            return result.returncode == 0

        except subprocess.CalledProcessError:
            return False

    async def enable_hdr(self, display: str = None) -> bool:
        """Enable HDR for specified display"""
        try:
            display = display or self._get_primary_display()
            if not display:
                return False

            display_info = self.current_displays.get(display)
            if not display_info or not display_info.hdr_supported:
                self.logger.warning(f"HDR not supported on {display}")
                return False

            self.logger.info(f"Enabling HDR on {display}")

            # Try kernel module control first
            success = await self._write_kernel_param("hdr_enable", "1")
            if success:
                display_info.hdr_enabled = True
                return True

            # Try xrandr properties
            try:
                subprocess.run(['xrandr', '--output', display, '--set', 'HDR', '1'],
                              check=True, timeout=10)
                display_info.hdr_enabled = True
                return True
            except subprocess.CalledProcessError:
                pass

            return False

        except Exception as e:
            self.logger.error(f"Failed to enable HDR: {e}")
            return False

    async def disable_hdr(self, display: str = None) -> bool:
        """Disable HDR for specified display"""
        try:
            display = display or self._get_primary_display()
            if not display:
                return False

            self.logger.info(f"Disabling HDR on {display}")

            success = await self._write_kernel_param("hdr_enable", "0")
            if success:
                if display in self.current_displays:
                    self.current_displays[display].hdr_enabled = False
                return True

            # Try xrandr
            try:
                subprocess.run(['xrandr', '--output', display, '--set', 'HDR', '0'],
                              check=True, timeout=10)
                if display in self.current_displays:
                    self.current_displays[display].hdr_enabled = False
                return True
            except subprocess.CalledProcessError:
                pass

            return False

        except Exception as e:
            self.logger.error(f"Failed to disable HDR: {e}")
            return False

    async def _get_hdr_status(self, connector: str) -> bool:
        """Get current HDR status"""
        try:
            # Check kernel module
            status = await self._read_kernel_param("hdr_status")
            if status:
                return status == "1"

            return False

        except Exception:
            return False

    async def enable_gsync(self, display: str = None) -> bool:
        """Enable G-Sync/Variable Refresh Rate"""
        try:
            display = display or self._get_primary_display()
            if not display:
                return False

            display_info = self.current_displays.get(display)
            if not display_info or not display_info.gsync_supported:
                self.logger.warning(f"G-Sync not supported on {display}")
                return False

            self.logger.info(f"Enabling G-Sync on {display}")

            # Try kernel module control
            success = await self._write_kernel_param("gsync_enable", "1")
            if success:
                display_info.gsync_enabled = True
                return True

            return False

        except Exception as e:
            self.logger.error(f"Failed to enable G-Sync: {e}")
            return False

    async def disable_gsync(self, display: str = None) -> bool:
        """Disable G-Sync/Variable Refresh Rate"""
        try:
            display = display or self._get_primary_display()
            if not display:
                return False

            self.logger.info(f"Disabling G-Sync on {display}")

            success = await self._write_kernel_param("gsync_enable", "0")
            if success:
                if display in self.current_displays:
                    self.current_displays[display].gsync_enabled = False
                return True

            return False

        except Exception as e:
            self.logger.error(f"Failed to disable G-Sync: {e}")
            return False

    async def _get_gsync_status(self, connector: str) -> bool:
        """Get current G-Sync status"""
        try:
            status = await self._read_kernel_param("gsync_status")
            if status:
                return status == "1"

            return False

        except Exception:
            return False

    async def set_overdrive_level(self, level: int) -> bool:
        """Set display overdrive level (0=Off, 1=Normal, 2=Extreme)"""
        try:
            if not 0 <= level <= 2:
                return False

            self.logger.info(f"Setting overdrive level to {level}")

            success = await self._write_kernel_param("display_overdrive", str(level))
            if success:
                self.settings.overdrive_level = level
                await self.save_settings()
                return True

            return False

        except Exception as e:
            self.logger.error(f"Failed to set overdrive level: {e}")
            return False

    async def _get_overdrive_level(self) -> int:
        """Get current overdrive level"""
        try:
            level = await self._read_kernel_param("display_overdrive")
            if level:
                return int(level)

            return 0

        except Exception:
            return 0

    async def set_refresh_rate(self, rate: float, display: str = None) -> bool:
        """Set display refresh rate"""
        try:
            display = display or self._get_primary_display()
            if not display:
                return False

            self.logger.info(f"Setting refresh rate to {rate}Hz on {display}")

            # Use xrandr to set refresh rate
            result = subprocess.run(['xrandr', '--output', display, '--rate', str(rate)],
                                  check=True, timeout=10)

            if result.returncode == 0:
                if display in self.current_displays:
                    self.current_displays[display].refresh_rate = rate
                return True

            return False

        except subprocess.CalledProcessError as e:
            self.logger.error(f"Failed to set refresh rate: {e}")
            return False

    def _get_primary_display(self) -> Optional[str]:
        """Get primary display connector"""
        for display in self.current_displays.values():
            if display.primary and display.connected:
                return display.connector

        # Fallback to first connected display
        for display in self.current_displays.values():
            if display.connected:
                return display.connector

        return None

    async def start_monitoring(self):
        """Start display monitoring"""
        if self.monitoring_active:
            return

        self.monitoring_active = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("Display monitoring started")

    async def stop_monitoring(self):
        """Stop display monitoring"""
        self.monitoring_active = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("Display monitoring stopped")

    async def _monitoring_loop(self):
        """Display monitoring loop"""
        while self.monitoring_active:
            try:
                # Re-detect displays periodically
                await self.detect_displays()

                # Auto-adjust settings if enabled
                if self.settings.auto_brightness:
                    await self._auto_adjust_brightness()

                await asyncio.sleep(30)  # Check every 30 seconds

            except Exception as e:
                self.logger.error(f"Display monitoring error: {e}")
                await asyncio.sleep(30)

    async def _auto_adjust_brightness(self):
        """Auto-adjust brightness based on ambient light or time"""
        try:
            # This is a simplified implementation
            # In practice, you'd use ambient light sensors or time-based adjustment

            current_hour = datetime.now().hour

            if 6 <= current_hour <= 18:  # Daytime
                target_brightness = 80
            elif 18 <= current_hour <= 22:  # Evening
                target_brightness = 60
            else:  # Night
                target_brightness = 40

            current_brightness = await self.get_brightness()
            if current_brightness and abs(current_brightness - target_brightness) > 10:
                await self.set_brightness(target_brightness)

        except Exception as e:
            self.logger.error(f"Auto brightness adjustment failed: {e}")

    async def apply_settings(self):
        """Apply current display settings"""
        try:
            if self.settings.gsync_enabled:
                await self.enable_gsync()

            await self.set_overdrive_level(self.settings.overdrive_level)

        except Exception as e:
            self.logger.error(f"Failed to apply display settings: {e}")

    async def _write_kernel_param(self, param: str, value: str) -> bool:
        """Write parameter to kernel module"""
        try:
            path = f"{self.kernel_module_path}/{param}"
            subprocess.run(['sudo', 'sh', '-c', f'echo {value} > {path}'],
                          check=True, timeout=5)
            return True
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            return False

    async def _read_kernel_param(self, param: str) -> Optional[str]:
        """Read parameter from kernel module"""
        try:
            path = Path(f"{self.kernel_module_path}/{param}")
            if path.exists():
                with open(path, 'r') as f:
                    return f.read().strip()
        except Exception:
            pass
        return None

    async def save_settings(self):
        """Save display settings"""
        try:
            self.config_file.parent.mkdir(parents=True, exist_ok=True)

            settings_dict = {
                'auto_brightness': self.settings.auto_brightness,
                'hdr_auto_switch': self.settings.hdr_auto_switch,
                'gsync_enabled': self.settings.gsync_enabled,
                'overdrive_level': self.settings.overdrive_level,
                'night_light': self.settings.night_light,
                'night_light_temperature': self.settings.night_light_temperature,
                'refresh_rate_mode': self.settings.refresh_rate_mode,
                'power_saving_brightness': self.settings.power_saving_brightness
            }

            with open(self.config_file, 'w') as f:
                json.dump(settings_dict, f, indent=2)

        except Exception as e:
            self.logger.error(f"Failed to save display settings: {e}")

    async def load_settings(self):
        """Load display settings"""
        try:
            if not self.config_file.exists():
                return

            with open(self.config_file, 'r') as f:
                settings_dict = json.load(f)

            self.settings.auto_brightness = settings_dict.get('auto_brightness', False)
            self.settings.hdr_auto_switch = settings_dict.get('hdr_auto_switch', True)
            self.settings.gsync_enabled = settings_dict.get('gsync_enabled', True)
            self.settings.overdrive_level = settings_dict.get('overdrive_level', 2)
            self.settings.night_light = settings_dict.get('night_light', False)
            self.settings.night_light_temperature = settings_dict.get('night_light_temperature', 4000)
            self.settings.refresh_rate_mode = settings_dict.get('refresh_rate_mode', 'auto')
            self.settings.power_saving_brightness = settings_dict.get('power_saving_brightness', True)

        except Exception as e:
            self.logger.error(f"Failed to load display settings: {e}")

# Example usage
async def main():
    """Example usage of display manager"""
    logging.basicConfig(level=logging.INFO)

    display_manager = DisplayManager()

    if not await display_manager.initialize():
        print("Failed to initialize display manager")
        return

    # Detect displays
    displays = await display_manager.detect_displays()
    for display in displays:
        print(f"Display: {display.name} - {display.width}x{display.height}@{display.refresh_rate}Hz")
        print(f"  Connected: {display.connected}, HDR: {display.hdr_supported}")

    # Test brightness control
    current_brightness = await display_manager.get_brightness()
    if current_brightness:
        print(f"Current brightness: {current_brightness}%")

    print("Display manager test completed")

if __name__ == "__main__":
    asyncio.run(main())