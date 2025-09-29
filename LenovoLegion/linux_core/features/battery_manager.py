#!/usr/bin/env python3
"""
Legion Toolkit Battery Management - Linux Implementation
Complete battery features with conservation mode, charging limits, and power optimization
Provides feature parity with Windows BatteryFeature.cs
"""

import os
import sys
import subprocess
import asyncio
from pathlib import Path
from typing import Dict, Optional, List, Any
from dataclasses import dataclass
from datetime import datetime, timedelta
import logging
import json

@dataclass
class BatteryInfo:
    """Battery information structure"""
    present: bool
    capacity: int  # Current capacity percentage
    design_capacity: int  # Design capacity in mWh
    full_charge_capacity: int  # Full charge capacity in mWh
    current_now: int  # Current charge/discharge rate in mA
    voltage_now: int  # Current voltage in mV
    status: str  # Charging, Discharging, Full, etc.
    health: str  # Good, Degraded, Dead
    cycle_count: int  # Battery cycle count
    temperature: float  # Temperature in Celsius
    time_to_empty: Optional[int]  # Minutes to empty
    time_to_full: Optional[int]  # Minutes to full charge

@dataclass
class BatterySettings:
    """Battery management settings"""
    conservation_mode: bool = False
    charge_threshold: int = 80  # Percentage
    rapid_charge: bool = False
    always_on_usb: bool = False
    hybrid_mode: bool = True  # Use iGPU when on battery
    power_profile: str = "balanced"  # quiet, balanced, performance
    ac_power_profile: str = "performance"
    battery_power_profile: str = "quiet"

class BatteryManager:
    """Advanced battery management system"""

    def __init__(self):
        self.logger = logging.getLogger("legion.battery")
        self.settings = BatterySettings()
        self.battery_path = Path("/sys/class/power_supply/BAT0")
        self.ac_path = Path("/sys/class/power_supply/ADP1")
        self.config_file = Path.home() / '.config' / 'legion-toolkit' / 'battery.json'

        # Legion-specific EC registers for battery control
        self.kernel_module_path = "/sys/kernel/legion_laptop"

        # Battery monitoring
        self.monitoring_active = False
        self.monitoring_task = None
        self.last_battery_info = None

        # Conservation mode state
        self.conservation_active = False
        self.original_charge_threshold = None

    async def initialize(self) -> bool:
        """Initialize battery management system"""
        try:
            # Check if battery is present
            if not await self.is_battery_present():
                self.logger.warning("No battery detected")
                return False

            # Load saved settings
            await self.load_settings()

            # Apply initial settings
            await self.apply_settings()

            # Start monitoring
            await self.start_monitoring()

            self.logger.info("Battery management initialized")
            return True

        except Exception as e:
            self.logger.error(f"Battery initialization failed: {e}")
            return False

    async def is_battery_present(self) -> bool:
        """Check if battery is present"""
        try:
            if self.battery_path.exists():
                present_file = self.battery_path / "present"
                if present_file.exists():
                    with open(present_file, 'r') as f:
                        return f.read().strip() == "1"
            return False
        except Exception:
            return False

    async def get_battery_info(self) -> Optional[BatteryInfo]:
        """Get comprehensive battery information"""
        try:
            if not await self.is_battery_present():
                return None

            info_dict = {}

            # Read all battery properties
            battery_files = {
                'capacity': 'capacity',
                'energy_full_design': 'design_capacity',
                'energy_full': 'full_charge_capacity',
                'current_now': 'current_now',
                'voltage_now': 'voltage_now',
                'status': 'status',
                'health': 'health',
                'cycle_count': 'cycle_count'
            }

            for file_name, prop_name in battery_files.items():
                file_path = self.battery_path / file_name
                if file_path.exists():
                    try:
                        with open(file_path, 'r') as f:
                            value = f.read().strip()
                            if prop_name in ['status', 'health']:
                                info_dict[prop_name] = value
                            else:
                                info_dict[prop_name] = int(value)
                    except (ValueError, IOError):
                        info_dict[prop_name] = 0 if prop_name not in ['status', 'health'] else 'Unknown'

            # Calculate temperature from thermal zone
            temp = await self._get_battery_temperature()

            # Calculate time estimates
            time_to_empty, time_to_full = await self._calculate_time_estimates(info_dict)

            battery_info = BatteryInfo(
                present=True,
                capacity=info_dict.get('capacity', 0),
                design_capacity=info_dict.get('design_capacity', 0),
                full_charge_capacity=info_dict.get('full_charge_capacity', 0),
                current_now=info_dict.get('current_now', 0),
                voltage_now=info_dict.get('voltage_now', 0),
                status=info_dict.get('status', 'Unknown'),
                health=info_dict.get('health', 'Unknown'),
                cycle_count=info_dict.get('cycle_count', 0),
                temperature=temp,
                time_to_empty=time_to_empty,
                time_to_full=time_to_full
            )

            self.last_battery_info = battery_info
            return battery_info

        except Exception as e:
            self.logger.error(f"Failed to get battery info: {e}")
            return None

    async def _get_battery_temperature(self) -> float:
        """Get battery temperature from thermal zone"""
        try:
            # Try thermal zone first
            for i in range(10):
                thermal_path = Path(f"/sys/class/thermal/thermal_zone{i}")
                if thermal_path.exists():
                    type_file = thermal_path / "type"
                    if type_file.exists():
                        with open(type_file, 'r') as f:
                            zone_type = f.read().strip()
                            if 'battery' in zone_type.lower() or 'bat' in zone_type.lower():
                                temp_file = thermal_path / "temp"
                                if temp_file.exists():
                                    with open(temp_file, 'r') as f:
                                        return int(f.read().strip()) / 1000.0

            # Fallback to ACPI if available
            acpi_temp_path = Path("/proc/acpi/battery/BAT0/temperature")
            if acpi_temp_path.exists():
                with open(acpi_temp_path, 'r') as f:
                    content = f.read()
                    for line in content.split('\n'):
                        if 'temperature' in line.lower():
                            temp_str = line.split(':')[1].strip().split()[0]
                            return float(temp_str)

            return 25.0  # Default room temperature

        except Exception:
            return 25.0

    async def _calculate_time_estimates(self, info: Dict) -> tuple:
        """Calculate time to empty/full estimates"""
        try:
            current_capacity = info.get('capacity', 0)
            current_now = abs(info.get('current_now', 0))  # mA
            full_capacity = info.get('full_charge_capacity', 0)  # mWh
            voltage = info.get('voltage_now', 0) / 1000.0  # Convert to V

            if current_now == 0 or voltage == 0:
                return None, None

            # Convert current to mW
            power_now = (current_now / 1000.0) * voltage  # mW

            # Calculate remaining capacity in mWh
            remaining_capacity = (current_capacity / 100.0) * full_capacity

            # Time to empty (minutes)
            time_to_empty = None
            if power_now > 0 and remaining_capacity > 0:
                time_to_empty = int((remaining_capacity / power_now) * 60)

            # Time to full (minutes)
            time_to_full = None
            if power_now > 0:
                capacity_to_full = full_capacity - remaining_capacity
                if capacity_to_full > 0:
                    time_to_full = int((capacity_to_full / power_now) * 60)

            return time_to_empty, time_to_full

        except Exception:
            return None, None

    async def is_ac_connected(self) -> bool:
        """Check if AC adapter is connected"""
        try:
            # Try ADP1 first (most common)
            online_file = self.ac_path / "online"
            if online_file.exists():
                with open(online_file, 'r') as f:
                    return f.read().strip() == "1"

            # Try other AC adapter names
            for ac_name in ["AC", "ADP0", "ACAD"]:
                ac_path = Path(f"/sys/class/power_supply/{ac_name}")
                online_file = ac_path / "online"
                if online_file.exists():
                    with open(online_file, 'r') as f:
                        return f.read().strip() == "1"

            return False

        except Exception:
            return False

    async def enable_conservation_mode(self) -> bool:
        """Enable battery conservation mode (limit charge to 60%)"""
        try:
            if self.conservation_active:
                return True

            self.logger.info("Enabling battery conservation mode")

            # Save current charge threshold
            current_threshold = await self.get_charge_threshold()
            if current_threshold:
                self.original_charge_threshold = current_threshold

            # Set conservation threshold via EC register
            success = await self._write_kernel_param("battery_charge_threshold", "60")

            if success:
                # Also try standard Linux battery threshold interface
                await self._set_charge_threshold_standard(60)

                self.conservation_active = True
                self.settings.conservation_mode = True
                await self.save_settings()

                self.logger.info("Conservation mode enabled - charge limited to 60%")
                return True
            else:
                self.logger.error("Failed to enable conservation mode")
                return False

        except Exception as e:
            self.logger.error(f"Failed to enable conservation mode: {e}")
            return False

    async def disable_conservation_mode(self) -> bool:
        """Disable battery conservation mode"""
        try:
            if not self.conservation_active:
                return True

            self.logger.info("Disabling battery conservation mode")

            # Restore original threshold or use default
            threshold = self.original_charge_threshold or 100

            success = await self._write_kernel_param("battery_charge_threshold", str(threshold))

            if success:
                await self._set_charge_threshold_standard(threshold)

                self.conservation_active = False
                self.settings.conservation_mode = False
                self.original_charge_threshold = None
                await self.save_settings()

                self.logger.info("Conservation mode disabled")
                return True
            else:
                self.logger.error("Failed to disable conservation mode")
                return False

        except Exception as e:
            self.logger.error(f"Failed to disable conservation mode: {e}")
            return False

    async def set_charge_threshold(self, threshold: int) -> bool:
        """Set battery charge threshold (50-100%)"""
        try:
            if not 50 <= threshold <= 100:
                self.logger.error("Charge threshold must be between 50-100%")
                return False

            self.logger.info(f"Setting charge threshold to {threshold}%")

            success = await self._write_kernel_param("battery_charge_threshold", str(threshold))

            if success:
                await self._set_charge_threshold_standard(threshold)

                self.settings.charge_threshold = threshold
                await self.save_settings()

                return True
            else:
                return False

        except Exception as e:
            self.logger.error(f"Failed to set charge threshold: {e}")
            return False

    async def get_charge_threshold(self) -> Optional[int]:
        """Get current charge threshold"""
        try:
            # Try kernel module first
            value = await self._read_kernel_param("battery_charge_threshold")
            if value:
                return int(value)

            # Try standard Linux interface
            threshold = await self._get_charge_threshold_standard()
            if threshold:
                return threshold

            return None

        except Exception as e:
            self.logger.error(f"Failed to get charge threshold: {e}")
            return None

    async def _set_charge_threshold_standard(self, threshold: int):
        """Set charge threshold using standard Linux interface"""
        try:
            # ThinkPad-style interface
            threshold_path = Path("/sys/class/power_supply/BAT0/charge_control_end_threshold")
            if threshold_path.exists():
                subprocess.run(['sudo', 'sh', '-c', f'echo {threshold} > {threshold_path}'],
                              check=True, timeout=5)
                return

            # Alternative paths
            alt_paths = [
                "/sys/bus/platform/drivers/ideapad_acpi/VPC2004:00/conservation_mode",
                "/sys/devices/platform/asus-nb-wmi/charge_control_end_threshold"
            ]

            for path in alt_paths:
                if Path(path).exists():
                    # Some systems use 0/1 for conservation mode
                    value = "1" if threshold <= 60 else "0"
                    subprocess.run(['sudo', 'sh', '-c', f'echo {value} > {path}'],
                                  check=True, timeout=5)
                    break

        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            pass

    async def _get_charge_threshold_standard(self) -> Optional[int]:
        """Get charge threshold using standard Linux interface"""
        try:
            threshold_path = Path("/sys/class/power_supply/BAT0/charge_control_end_threshold")
            if threshold_path.exists():
                with open(threshold_path, 'r') as f:
                    return int(f.read().strip())

            return None

        except Exception:
            return None

    async def enable_rapid_charge(self) -> bool:
        """Enable rapid charging mode"""
        try:
            self.logger.info("Enabling rapid charge mode")

            success = await self._write_kernel_param("battery_rapid_charge", "1")

            if success:
                self.settings.rapid_charge = True
                await self.save_settings()
                return True
            else:
                return False

        except Exception as e:
            self.logger.error(f"Failed to enable rapid charge: {e}")
            return False

    async def disable_rapid_charge(self) -> bool:
        """Disable rapid charging mode"""
        try:
            self.logger.info("Disabling rapid charge mode")

            success = await self._write_kernel_param("battery_rapid_charge", "0")

            if success:
                self.settings.rapid_charge = False
                await self.save_settings()
                return True
            else:
                return False

        except Exception as e:
            self.logger.error(f"Failed to disable rapid charge: {e}")
            return False

    async def enable_always_on_usb(self) -> bool:
        """Enable always-on USB ports"""
        try:
            self.logger.info("Enabling always-on USB")

            success = await self._write_kernel_param("usb_always_on", "1")

            if success:
                self.settings.always_on_usb = True
                await self.save_settings()
                return True
            else:
                return False

        except Exception as e:
            self.logger.error(f"Failed to enable always-on USB: {e}")
            return False

    async def disable_always_on_usb(self) -> bool:
        """Disable always-on USB ports"""
        try:
            self.logger.info("Disabling always-on USB")

            success = await self._write_kernel_param("usb_always_on", "0")

            if success:
                self.settings.always_on_usb = False
                await self.save_settings()
                return True
            else:
                return False

        except Exception as e:
            self.logger.error(f"Failed to disable always-on USB: {e}")
            return False

    async def set_hybrid_mode(self, enabled: bool) -> bool:
        """Enable/disable hybrid graphics mode"""
        try:
            mode_str = "enabled" if enabled else "disabled"
            self.logger.info(f"Setting hybrid mode: {mode_str}")

            # This typically requires switching between iGPU and dGPU
            # Implementation depends on the specific graphics switching mechanism

            # For NVIDIA Optimus systems
            if enabled:
                # Switch to iGPU for battery saving
                await self._set_gpu_mode("integrated")
            else:
                # Use dGPU for performance
                await self._set_gpu_mode("discrete")

            self.settings.hybrid_mode = enabled
            await self.save_settings()

            return True

        except Exception as e:
            self.logger.error(f"Failed to set hybrid mode: {e}")
            return False

    async def _set_gpu_mode(self, mode: str):
        """Set GPU mode (integrated/discrete)"""
        try:
            # Try various GPU switching mechanisms

            # NVIDIA Prime
            if Path("/usr/bin/prime-select").exists():
                if mode == "integrated":
                    subprocess.run(['sudo', 'prime-select', 'intel'], timeout=30)
                else:
                    subprocess.run(['sudo', 'prime-select', 'nvidia'], timeout=30)
                return

            # AMD/Intel switching
            if Path("/sys/kernel/debug/vgaswitcheroo/switch").exists():
                if mode == "integrated":
                    subprocess.run(['sudo', 'sh', '-c', 'echo IGD > /sys/kernel/debug/vgaswitcheroo/switch'],
                                  timeout=10)
                else:
                    subprocess.run(['sudo', 'sh', '-c', 'echo DIS > /sys/kernel/debug/vgaswitcheroo/switch'],
                                  timeout=10)

        except (subprocess.CalledProcessError, subprocess.TimeoutExpired):
            pass

    async def apply_power_profile(self, profile: str):
        """Apply power profile based on AC/battery status"""
        try:
            is_ac = await self.is_ac_connected()

            if is_ac and profile == "auto":
                profile = self.settings.ac_power_profile
            elif not is_ac and profile == "auto":
                profile = self.settings.battery_power_profile

            # Apply profile via kernel module
            await self._write_kernel_param("performance_mode", profile)

            # Apply additional battery-specific optimizations
            if not is_ac:
                # Battery optimizations
                await self._write_kernel_param("cpu_pl2", "90")  # Reduce turbo power
                await self._write_kernel_param("gpu_tgp", "80")  # Reduce GPU power

                if self.settings.hybrid_mode:
                    await self._set_gpu_mode("integrated")
            else:
                # AC optimizations
                await self._write_kernel_param("cpu_pl2", "140")  # Full turbo power
                await self._write_kernel_param("gpu_tgp", "140")  # Full GPU power

            self.logger.info(f"Applied power profile: {profile} (AC: {is_ac})")

        except Exception as e:
            self.logger.error(f"Failed to apply power profile: {e}")

    async def start_monitoring(self):
        """Start battery monitoring"""
        if self.monitoring_active:
            return

        self.monitoring_active = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("Battery monitoring started")

    async def stop_monitoring(self):
        """Stop battery monitoring"""
        self.monitoring_active = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("Battery monitoring stopped")

    async def _monitoring_loop(self):
        """Battery monitoring loop"""
        while self.monitoring_active:
            try:
                # Check AC status and apply appropriate profile
                await self.apply_power_profile("auto")

                # Get battery info for health monitoring
                battery_info = await self.get_battery_info()
                if battery_info:
                    # Check for concerning conditions
                    if battery_info.temperature > 45:
                        self.logger.warning(f"High battery temperature: {battery_info.temperature:.1f}°C")

                    if battery_info.health == "Degraded":
                        self.logger.warning("Battery health degraded - consider replacement")

                await asyncio.sleep(30)  # Check every 30 seconds

            except Exception as e:
                self.logger.error(f"Battery monitoring error: {e}")
                await asyncio.sleep(30)

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

    async def apply_settings(self):
        """Apply current battery settings"""
        try:
            if self.settings.conservation_mode:
                await self.enable_conservation_mode()
            else:
                await self.set_charge_threshold(self.settings.charge_threshold)

            if self.settings.rapid_charge:
                await self.enable_rapid_charge()

            if self.settings.always_on_usb:
                await self.enable_always_on_usb()

            await self.set_hybrid_mode(self.settings.hybrid_mode)

        except Exception as e:
            self.logger.error(f"Failed to apply battery settings: {e}")

    async def save_settings(self):
        """Save battery settings"""
        try:
            self.config_file.parent.mkdir(parents=True, exist_ok=True)

            with open(self.config_file, 'w') as f:
                # Convert dataclass to dict for JSON serialization
                settings_dict = {
                    'conservation_mode': self.settings.conservation_mode,
                    'charge_threshold': self.settings.charge_threshold,
                    'rapid_charge': self.settings.rapid_charge,
                    'always_on_usb': self.settings.always_on_usb,
                    'hybrid_mode': self.settings.hybrid_mode,
                    'power_profile': self.settings.power_profile,
                    'ac_power_profile': self.settings.ac_power_profile,
                    'battery_power_profile': self.settings.battery_power_profile
                }
                json.dump(settings_dict, f, indent=2)

        except Exception as e:
            self.logger.error(f"Failed to save battery settings: {e}")

    async def load_settings(self):
        """Load battery settings"""
        try:
            if not self.config_file.exists():
                return

            with open(self.config_file, 'r') as f:
                settings_dict = json.load(f)

            # Update settings
            self.settings.conservation_mode = settings_dict.get('conservation_mode', False)
            self.settings.charge_threshold = settings_dict.get('charge_threshold', 80)
            self.settings.rapid_charge = settings_dict.get('rapid_charge', False)
            self.settings.always_on_usb = settings_dict.get('always_on_usb', False)
            self.settings.hybrid_mode = settings_dict.get('hybrid_mode', True)
            self.settings.power_profile = settings_dict.get('power_profile', 'balanced')
            self.settings.ac_power_profile = settings_dict.get('ac_power_profile', 'performance')
            self.settings.battery_power_profile = settings_dict.get('battery_power_profile', 'quiet')

        except Exception as e:
            self.logger.error(f"Failed to load battery settings: {e}")

# Example usage
async def main():
    """Example usage of battery manager"""
    logging.basicConfig(level=logging.INFO)

    battery_manager = BatteryManager()

    if not await battery_manager.initialize():
        print("Failed to initialize battery manager")
        return

    # Get battery information
    battery_info = await battery_manager.get_battery_info()
    if battery_info:
        print(f"Battery: {battery_info.capacity}% - {battery_info.status}")
        print(f"Health: {battery_info.health}")
        print(f"Temperature: {battery_info.temperature:.1f}°C")
        print(f"Cycle count: {battery_info.cycle_count}")

    # Check AC status
    ac_connected = await battery_manager.is_ac_connected()
    print(f"AC connected: {ac_connected}")

    # Test conservation mode
    print("Testing conservation mode...")
    await battery_manager.enable_conservation_mode()
    await asyncio.sleep(2)
    await battery_manager.disable_conservation_mode()

    print("Battery manager test completed")

if __name__ == "__main__":
    asyncio.run(main())