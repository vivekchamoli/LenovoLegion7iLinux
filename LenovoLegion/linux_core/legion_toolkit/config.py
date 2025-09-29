#!/usr/bin/env python3
"""
Legion Toolkit Configuration System
Unified configuration management for cross-platform compatibility

Provides:
- Centralized configuration management
- Cross-platform settings synchronization
- Hardware-specific configuration
- User preferences management
- Profile system for different use cases
"""

import os
import json
import yaml
from pathlib import Path
from typing import Dict, Any, Optional, List, Union
from dataclasses import dataclass, asdict, field
from enum import Enum
import logging
import platform
import getpass

logger = logging.getLogger(__name__)

class PlatformType(Enum):
    WINDOWS = "windows"
    LINUX = "linux"
    UNKNOWN = "unknown"

class HardwareProfile(Enum):
    LEGION_GEN9_16IRX9 = "legion_slim_7i_gen9_16irx9"
    LEGION_GEN8 = "legion_gen8"
    LEGION_GENERIC = "legion_generic"
    UNKNOWN = "unknown"

class PerformanceMode(Enum):
    QUIET = "quiet"
    BALANCED = "balanced"
    PERFORMANCE = "performance"
    CUSTOM = "custom"

@dataclass
class ThermalConfig:
    """Thermal management configuration"""
    cpu_temp_target: int = 85
    gpu_temp_target: int = 83
    fan_curve_aggressive: bool = False
    thermal_throttle_temp: int = 95
    ai_thermal_optimization: bool = True
    fan_speed_min: int = 20
    fan_speed_max: int = 100

@dataclass
class GPUConfig:
    """GPU configuration"""
    overclocking_enabled: bool = False
    core_clock_offset: int = 0  # MHz
    memory_clock_offset: int = 0  # MHz
    power_limit: int = 140  # Watts
    fan_curve_custom: bool = False
    auto_gpu_switching: bool = True

@dataclass
class RGBConfig:
    """RGB lighting configuration"""
    enabled: bool = True
    mode: str = "static"
    brightness: int = 75
    color_primary: str = "#FF0000"
    color_secondary: str = "#0000FF"
    animation_speed: int = 5
    zones_enabled: List[int] = field(default_factory=lambda: [1, 2, 3, 4])

@dataclass
class AutomationConfig:
    """Automation and profile switching configuration"""
    game_detection_enabled: bool = True
    auto_performance_switching: bool = True
    power_profile_ac: str = "performance"
    power_profile_battery: str = "balanced"
    wifi_optimization: bool = True
    process_monitoring: bool = True

@dataclass
class UIConfig:
    """User interface configuration"""
    theme: str = "dark"
    language: str = "en"
    start_minimized: bool = False
    minimize_to_tray: bool = True
    show_notifications: bool = True
    auto_start: bool = False
    update_check_enabled: bool = True

@dataclass
class HardwareConfig:
    """Hardware-specific configuration"""
    platform: str = ""
    model: str = ""
    cpu: str = ""
    gpu: str = ""
    memory: str = ""
    ec_support: bool = False
    kernel_module_loaded: bool = False

@dataclass
class LegionConfig:
    """Main Legion Toolkit configuration"""
    version: str = "6.0.0"
    platform: PlatformType = PlatformType.UNKNOWN
    hardware_profile: HardwareProfile = HardwareProfile.UNKNOWN
    performance_mode: PerformanceMode = PerformanceMode.BALANCED

    # Sub-configurations
    thermal: ThermalConfig = field(default_factory=ThermalConfig)
    gpu: GPUConfig = field(default_factory=GPUConfig)
    rgb: RGBConfig = field(default_factory=RGBConfig)
    automation: AutomationConfig = field(default_factory=AutomationConfig)
    ui: UIConfig = field(default_factory=UIConfig)
    hardware: HardwareConfig = field(default_factory=HardwareConfig)

    # User profiles
    profiles: Dict[str, Dict[str, Any]] = field(default_factory=dict)
    active_profile: str = "default"

    # Advanced settings
    debug_mode: bool = False
    telemetry_enabled: bool = False
    backup_settings: bool = True

class ConfigManager:
    """Configuration manager with cross-platform support"""

    def __init__(self, config_dir: Optional[Path] = None):
        self.platform = self._detect_platform()
        self.config_dir = config_dir or self._get_default_config_dir()
        self.config_file = self.config_dir / "config.json"
        self.profiles_dir = self.config_dir / "profiles"
        self.backup_dir = self.config_dir / "backups"

        # Ensure directories exist
        self.config_dir.mkdir(parents=True, exist_ok=True)
        self.profiles_dir.mkdir(parents=True, exist_ok=True)
        self.backup_dir.mkdir(parents=True, exist_ok=True)

        self._config: Optional[LegionConfig] = None

        # Load configuration
        self.load()

    def _detect_platform(self) -> PlatformType:
        """Detect the current platform"""
        system = platform.system().lower()
        if system == "windows":
            return PlatformType.WINDOWS
        elif system == "linux":
            return PlatformType.LINUX
        else:
            return PlatformType.UNKNOWN

    def _get_default_config_dir(self) -> Path:
        """Get default configuration directory based on platform"""
        if self.platform == PlatformType.WINDOWS:
            # Use APPDATA on Windows
            appdata = os.environ.get("APPDATA", "")
            if appdata:
                return Path(appdata) / "LenovoLegionToolkit"
            else:
                return Path.home() / ".legion-toolkit"
        else:
            # Use XDG config directory on Linux
            xdg_config = os.environ.get("XDG_CONFIG_HOME", "")
            if xdg_config:
                return Path(xdg_config) / "legion-toolkit"
            else:
                return Path.home() / ".config" / "legion-toolkit"

    def _detect_hardware(self) -> HardwareConfig:
        """Detect hardware configuration"""
        hardware = HardwareConfig()
        hardware.platform = self.platform.value

        try:
            if self.platform == PlatformType.LINUX:
                # Linux hardware detection

                # Check for Legion laptop
                try:
                    with open("/sys/class/dmi/id/product_name", "r") as f:
                        product_name = f.read().strip()
                        hardware.model = product_name

                        # Check for specific Gen 9 model
                        if "16IRX9" in product_name:
                            self._config.hardware_profile = HardwareProfile.LEGION_GEN9_16IRX9
                        elif "legion" in product_name.lower():
                            self._config.hardware_profile = HardwareProfile.LEGION_GENERIC
                except:
                    pass

                # Check CPU
                try:
                    with open("/proc/cpuinfo", "r") as f:
                        for line in f:
                            if "model name" in line:
                                hardware.cpu = line.split(":")[1].strip()
                                break
                except:
                    pass

                # Check GPU
                import subprocess
                try:
                    result = subprocess.run(["lspci"], capture_output=True, text=True)
                    for line in result.stdout.split('\n'):
                        if 'VGA' in line or 'Display' in line:
                            hardware.gpu = line.split(':')[-1].strip()
                            break
                except:
                    pass

                # Check for kernel module
                try:
                    result = subprocess.run(["lsmod"], capture_output=True, text=True)
                    hardware.kernel_module_loaded = "legion_laptop_16irx9" in result.stdout
                except:
                    pass

                # Check for EC support
                hardware.ec_support = Path("/sys/kernel/legion_laptop_16irx9").exists()

            elif self.platform == PlatformType.WINDOWS:
                # Windows hardware detection using WMI
                try:
                    import wmi
                    c = wmi.WMI()

                    # Get system info
                    for system in c.Win32_ComputerSystem():
                        hardware.model = f"{system.Manufacturer} {system.Model}"
                        if "legion" in hardware.model.lower():
                            if "16IRX9" in hardware.model:
                                self._config.hardware_profile = HardwareProfile.LEGION_GEN9_16IRX9
                            else:
                                self._config.hardware_profile = HardwareProfile.LEGION_GENERIC

                    # Get CPU info
                    for cpu in c.Win32_Processor():
                        hardware.cpu = cpu.Name
                        break

                    # Get GPU info
                    for gpu in c.Win32_VideoController():
                        if gpu.Name and "nvidia" in gpu.Name.lower():
                            hardware.gpu = gpu.Name
                            break

                    # EC support is available on Windows
                    hardware.ec_support = True

                except ImportError:
                    logger.warning("WMI module not available for Windows hardware detection")
                except Exception as e:
                    logger.error(f"Windows hardware detection failed: {e}")

        except Exception as e:
            logger.error(f"Hardware detection failed: {e}")

        return hardware

    @property
    def config(self) -> LegionConfig:
        """Get current configuration"""
        if self._config is None:
            self._config = self._create_default_config()
        return self._config

    def _create_default_config(self) -> LegionConfig:
        """Create default configuration"""
        config = LegionConfig()
        config.platform = self.platform
        config.hardware = self._detect_hardware()

        # Apply hardware-specific defaults
        if config.hardware_profile == HardwareProfile.LEGION_GEN9_16IRX9:
            # Gen 9 specific optimizations
            config.thermal.cpu_temp_target = 95  # Higher limit for i9-14900HX
            config.thermal.ai_thermal_optimization = True
            config.gpu.power_limit = 140  # RTX 4070 max power
            config.rgb.zones_enabled = [1, 2, 3, 4]  # 4-zone RGB

        elif config.hardware_profile in [HardwareProfile.LEGION_GEN8, HardwareProfile.LEGION_GENERIC]:
            # Generic Legion optimizations
            config.thermal.cpu_temp_target = 85
            config.gpu.power_limit = 115

        # Platform-specific defaults
        if self.platform == PlatformType.LINUX:
            config.ui.theme = "dark"  # Better for Linux
            config.automation.auto_start = False  # Manual control on Linux

        elif self.platform == PlatformType.WINDOWS:
            config.ui.auto_start = True  # Windows service integration
            config.ui.minimize_to_tray = True

        return config

    def load(self) -> bool:
        """Load configuration from file"""
        try:
            if self.config_file.exists():
                with open(self.config_file, 'r') as f:
                    config_dict = json.load(f)

                # Convert dictionary to LegionConfig
                self._config = self._dict_to_config(config_dict)
                logger.info(f"Configuration loaded from {self.config_file}")
                return True
            else:
                # Create default configuration
                self._config = self._create_default_config()
                self.save()  # Save default config
                logger.info("Default configuration created")
                return True

        except Exception as e:
            logger.error(f"Failed to load configuration: {e}")
            self._config = self._create_default_config()
            return False

    def save(self) -> bool:
        """Save configuration to file"""
        try:
            # Create backup if enabled
            if self.config.backup_settings and self.config_file.exists():
                self._create_backup()

            # Convert config to dictionary
            config_dict = asdict(self.config)

            # Convert enums to strings
            config_dict['platform'] = self.config.platform.value
            config_dict['hardware_profile'] = self.config.hardware_profile.value
            config_dict['performance_mode'] = self.config.performance_mode.value

            # Save to file with proper formatting
            with open(self.config_file, 'w') as f:
                json.dump(config_dict, f, indent=2, sort_keys=True)

            logger.info(f"Configuration saved to {self.config_file}")
            return True

        except Exception as e:
            logger.error(f"Failed to save configuration: {e}")
            return False

    def _dict_to_config(self, config_dict: Dict) -> LegionConfig:
        """Convert dictionary to LegionConfig object"""
        # Handle enum conversions
        if 'platform' in config_dict:
            config_dict['platform'] = PlatformType(config_dict['platform'])
        if 'hardware_profile' in config_dict:
            config_dict['hardware_profile'] = HardwareProfile(config_dict['hardware_profile'])
        if 'performance_mode' in config_dict:
            config_dict['performance_mode'] = PerformanceMode(config_dict['performance_mode'])

        # Create nested dataclass objects
        if 'thermal' in config_dict:
            config_dict['thermal'] = ThermalConfig(**config_dict['thermal'])
        if 'gpu' in config_dict:
            config_dict['gpu'] = GPUConfig(**config_dict['gpu'])
        if 'rgb' in config_dict:
            config_dict['rgb'] = RGBConfig(**config_dict['rgb'])
        if 'automation' in config_dict:
            config_dict['automation'] = AutomationConfig(**config_dict['automation'])
        if 'ui' in config_dict:
            config_dict['ui'] = UIConfig(**config_dict['ui'])
        if 'hardware' in config_dict:
            config_dict['hardware'] = HardwareConfig(**config_dict['hardware'])

        return LegionConfig(**config_dict)

    def _create_backup(self):
        """Create configuration backup"""
        import shutil
        from datetime import datetime

        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_file = self.backup_dir / f"config_backup_{timestamp}.json"

        try:
            shutil.copy2(self.config_file, backup_file)

            # Keep only last 10 backups
            backups = sorted(self.backup_dir.glob("config_backup_*.json"))
            for old_backup in backups[:-10]:
                old_backup.unlink()

        except Exception as e:
            logger.warning(f"Failed to create backup: {e}")

    def get_profile(self, name: str) -> Optional[Dict]:
        """Get a specific profile"""
        return self.config.profiles.get(name)

    def save_profile(self, name: str, description: str = "") -> bool:
        """Save current settings as a profile"""
        try:
            profile = {
                "description": description,
                "created": str(datetime.now()),
                "performance_mode": self.config.performance_mode.value,
                "thermal": asdict(self.config.thermal),
                "gpu": asdict(self.config.gpu),
                "rgb": asdict(self.config.rgb)
            }

            self.config.profiles[name] = profile
            self.save()
            logger.info(f"Profile '{name}' saved")
            return True

        except Exception as e:
            logger.error(f"Failed to save profile '{name}': {e}")
            return False

    def load_profile(self, name: str) -> bool:
        """Load a specific profile"""
        try:
            if name not in self.config.profiles:
                logger.error(f"Profile '{name}' not found")
                return False

            profile = self.config.profiles[name]

            # Apply profile settings
            if 'performance_mode' in profile:
                self.config.performance_mode = PerformanceMode(profile['performance_mode'])
            if 'thermal' in profile:
                self.config.thermal = ThermalConfig(**profile['thermal'])
            if 'gpu' in profile:
                self.config.gpu = GPUConfig(**profile['gpu'])
            if 'rgb' in profile:
                self.config.rgb = RGBConfig(**profile['rgb'])

            self.config.active_profile = name
            self.save()
            logger.info(f"Profile '{name}' loaded")
            return True

        except Exception as e:
            logger.error(f"Failed to load profile '{name}': {e}")
            return False

    def delete_profile(self, name: str) -> bool:
        """Delete a profile"""
        try:
            if name in self.config.profiles:
                del self.config.profiles[name]
                if self.config.active_profile == name:
                    self.config.active_profile = "default"
                self.save()
                logger.info(f"Profile '{name}' deleted")
                return True
            else:
                logger.warning(f"Profile '{name}' not found")
                return False

        except Exception as e:
            logger.error(f"Failed to delete profile '{name}': {e}")
            return False

    def list_profiles(self) -> List[str]:
        """List all available profiles"""
        return list(self.config.profiles.keys())

    def export_config(self, export_path: Path) -> bool:
        """Export configuration to file"""
        try:
            config_dict = asdict(self.config)

            # Convert enums
            config_dict['platform'] = self.config.platform.value
            config_dict['hardware_profile'] = self.config.hardware_profile.value
            config_dict['performance_mode'] = self.config.performance_mode.value

            with open(export_path, 'w') as f:
                json.dump(config_dict, f, indent=2)

            logger.info(f"Configuration exported to {export_path}")
            return True

        except Exception as e:
            logger.error(f"Failed to export configuration: {e}")
            return False

    def import_config(self, import_path: Path) -> bool:
        """Import configuration from file"""
        try:
            with open(import_path, 'r') as f:
                config_dict = json.load(f)

            # Validate and merge with current config
            imported_config = self._dict_to_config(config_dict)

            # Create backup before importing
            if self.config.backup_settings:
                self._create_backup()

            self._config = imported_config
            self.save()

            logger.info(f"Configuration imported from {import_path}")
            return True

        except Exception as e:
            logger.error(f"Failed to import configuration: {e}")
            return False

    def reset_to_defaults(self) -> bool:
        """Reset configuration to defaults"""
        try:
            # Create backup before reset
            if self.config.backup_settings:
                self._create_backup()

            self._config = self._create_default_config()
            self.save()

            logger.info("Configuration reset to defaults")
            return True

        except Exception as e:
            logger.error(f"Failed to reset configuration: {e}")
            return False

    def get_config_info(self) -> Dict:
        """Get configuration information for troubleshooting"""
        return {
            "version": self.config.version,
            "platform": self.config.platform.value,
            "hardware_profile": self.config.hardware_profile.value,
            "config_dir": str(self.config_dir),
            "config_file": str(self.config_file),
            "profiles_count": len(self.config.profiles),
            "active_profile": self.config.active_profile,
            "hardware_detected": {
                "model": self.config.hardware.model,
                "cpu": self.config.hardware.cpu,
                "gpu": self.config.hardware.gpu,
                "ec_support": self.config.hardware.ec_support,
                "kernel_module": self.config.hardware.kernel_module_loaded
            }
        }

# Global config manager instance
_config_manager: Optional[ConfigManager] = None

def get_config_manager() -> ConfigManager:
    """Get global configuration manager instance"""
    global _config_manager
    if _config_manager is None:
        _config_manager = ConfigManager()
    return _config_manager

def get_config() -> LegionConfig:
    """Get current configuration"""
    return get_config_manager().config

# Convenience functions
def save_config() -> bool:
    """Save current configuration"""
    return get_config_manager().save()

def load_config() -> bool:
    """Load configuration"""
    return get_config_manager().load()

def reset_config() -> bool:
    """Reset configuration to defaults"""
    return get_config_manager().reset_to_defaults()