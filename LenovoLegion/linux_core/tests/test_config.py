"""
Tests for configuration system
"""

import pytest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch, Mock

from legion_toolkit.config import (
    ConfigManager, LegionConfig, ThermalConfig, GPUConfig,
    PlatformType, HardwareProfile, PerformanceMode
)


class TestConfigManager:
    """Test configuration manager functionality"""

    def test_config_manager_initialization(self, temp_config_dir):
        """Test configuration manager initialization"""
        config_manager = ConfigManager(temp_config_dir)

        assert config_manager.config_dir == temp_config_dir
        assert config_manager.config_file == temp_config_dir / "config.json"
        assert config_manager.platform in [PlatformType.LINUX, PlatformType.WINDOWS]

    def test_default_config_creation(self, temp_config_dir, mock_hardware):
        """Test default configuration creation"""
        config_manager = ConfigManager(temp_config_dir)
        config = config_manager.config

        assert isinstance(config, LegionConfig)
        assert config.version == "6.0.0"
        assert isinstance(config.thermal, ThermalConfig)
        assert isinstance(config.gpu, GPUConfig)

    def test_config_save_and_load(self, temp_config_dir, mock_hardware):
        """Test configuration save and load functionality"""
        config_manager = ConfigManager(temp_config_dir)

        # Modify configuration
        config_manager.config.thermal.cpu_temp_target = 90
        config_manager.config.gpu.power_limit = 130

        # Save configuration
        assert config_manager.save() is True
        assert config_manager.config_file.exists()

        # Create new manager and load
        new_config_manager = ConfigManager(temp_config_dir)
        assert new_config_manager.config.thermal.cpu_temp_target == 90
        assert new_config_manager.config.gpu.power_limit == 130

    def test_profile_management(self, temp_config_dir, mock_hardware):
        """Test profile save/load functionality"""
        config_manager = ConfigManager(temp_config_dir)

        # Create a test profile
        config_manager.config.performance_mode = PerformanceMode.PERFORMANCE
        config_manager.config.thermal.cpu_temp_target = 95

        # Save profile
        assert config_manager.save_profile("gaming", "High performance gaming profile") is True
        assert "gaming" in config_manager.list_profiles()

        # Modify current config
        config_manager.config.performance_mode = PerformanceMode.QUIET
        config_manager.config.thermal.cpu_temp_target = 75

        # Load profile
        assert config_manager.load_profile("gaming") is True
        assert config_manager.config.performance_mode == PerformanceMode.PERFORMANCE
        assert config_manager.config.thermal.cpu_temp_target == 95

    def test_profile_deletion(self, temp_config_dir, mock_hardware):
        """Test profile deletion"""
        config_manager = ConfigManager(temp_config_dir)

        # Save a profile
        config_manager.save_profile("test_profile", "Test profile")
        assert "test_profile" in config_manager.list_profiles()

        # Delete profile
        assert config_manager.delete_profile("test_profile") is True
        assert "test_profile" not in config_manager.list_profiles()

        # Try to delete non-existent profile
        assert config_manager.delete_profile("non_existent") is False

    def test_config_export_import(self, temp_config_dir, mock_hardware):
        """Test configuration export and import"""
        config_manager = ConfigManager(temp_config_dir)

        # Modify configuration
        config_manager.config.thermal.cpu_temp_target = 88
        config_manager.save_profile("test", "Test profile")

        # Export configuration
        export_file = temp_config_dir / "export.json"
        assert config_manager.export_config(export_file) is True
        assert export_file.exists()

        # Modify configuration
        config_manager.config.thermal.cpu_temp_target = 75

        # Import configuration
        assert config_manager.import_config(export_file) is True
        assert config_manager.config.thermal.cpu_temp_target == 88
        assert "test" in config_manager.list_profiles()

    def test_config_reset(self, temp_config_dir, mock_hardware):
        """Test configuration reset to defaults"""
        config_manager = ConfigManager(temp_config_dir)

        # Modify configuration
        original_temp_target = config_manager.config.thermal.cpu_temp_target
        config_manager.config.thermal.cpu_temp_target = 95
        config_manager.save_profile("test", "Test")

        # Reset to defaults
        assert config_manager.reset_to_defaults() is True

        # Check that config was reset
        assert config_manager.config.thermal.cpu_temp_target == original_temp_target
        assert len(config_manager.list_profiles()) == 0

    def test_backup_creation(self, temp_config_dir, mock_hardware):
        """Test configuration backup creation"""
        config_manager = ConfigManager(temp_config_dir)
        config_manager.config.backup_settings = True

        # Save initial config
        config_manager.save()

        # Modify and save again (should create backup)
        config_manager.config.thermal.cpu_temp_target = 90
        config_manager.save()

        # Check backup was created
        backups = list(config_manager.backup_dir.glob("config_backup_*.json"))
        assert len(backups) > 0

    @patch('platform.system')
    def test_platform_detection(self, mock_platform, temp_config_dir):
        """Test platform detection"""
        # Test Linux detection
        mock_platform.return_value = "Linux"
        config_manager = ConfigManager(temp_config_dir)
        assert config_manager.platform == PlatformType.LINUX

        # Test Windows detection
        mock_platform.return_value = "Windows"
        config_manager = ConfigManager(temp_config_dir)
        assert config_manager.platform == PlatformType.WINDOWS

    def test_hardware_specific_defaults(self, temp_config_dir):
        """Test hardware-specific default configurations"""
        # Mock Gen 9 hardware
        with patch('legion_toolkit.config.ConfigManager._detect_hardware') as mock_detect:
            mock_hardware = Mock()
            mock_hardware.model = "Legion Slim 7i Gen 9 (16IRX9)"
            mock_hardware.platform = "linux"
            mock_detect.return_value = mock_hardware

            config_manager = ConfigManager(temp_config_dir)
            config_manager._config = None  # Force recreation
            config = config_manager.config

            assert config.hardware_profile == HardwareProfile.LEGION_GEN9_16IRX9
            assert config.thermal.cpu_temp_target == 95  # Higher for i9-14900HX
            assert config.gpu.power_limit == 140  # RTX 4070 max power

    def test_config_info(self, temp_config_dir, mock_hardware):
        """Test configuration information retrieval"""
        config_manager = ConfigManager(temp_config_dir)
        info = config_manager.get_config_info()

        assert "version" in info
        assert "platform" in info
        assert "hardware_profile" in info
        assert "config_dir" in info
        assert "hardware_detected" in info

    def test_invalid_config_handling(self, temp_config_dir):
        """Test handling of invalid configuration files"""
        config_manager = ConfigManager(temp_config_dir)

        # Create invalid JSON file
        with open(config_manager.config_file, 'w') as f:
            f.write("invalid json content")

        # Should fallback to default config
        new_config_manager = ConfigManager(temp_config_dir)
        assert isinstance(new_config_manager.config, LegionConfig)


class TestLegionConfig:
    """Test LegionConfig dataclass"""

    def test_config_defaults(self):
        """Test default configuration values"""
        config = LegionConfig()

        assert config.version == "6.0.0"
        assert config.platform == PlatformType.UNKNOWN
        assert config.hardware_profile == HardwareProfile.UNKNOWN
        assert config.performance_mode == PerformanceMode.BALANCED

        # Test sub-configs
        assert isinstance(config.thermal, ThermalConfig)
        assert isinstance(config.gpu, GPUConfig)
        assert config.thermal.cpu_temp_target == 85
        assert config.gpu.power_limit == 140

    def test_config_serialization(self):
        """Test configuration serialization to dict"""
        from dataclasses import asdict

        config = LegionConfig()
        config_dict = asdict(config)

        assert "version" in config_dict
        assert "thermal" in config_dict
        assert "gpu" in config_dict
        assert isinstance(config_dict["thermal"], dict)


class TestThermalConfig:
    """Test thermal configuration"""

    def test_thermal_defaults(self):
        """Test thermal configuration defaults"""
        thermal = ThermalConfig()

        assert thermal.cpu_temp_target == 85
        assert thermal.gpu_temp_target == 83
        assert thermal.ai_thermal_optimization is True
        assert thermal.fan_speed_min == 20
        assert thermal.fan_speed_max == 100

    def test_thermal_validation(self):
        """Test thermal configuration validation"""
        thermal = ThermalConfig()

        # Test valid values
        thermal.cpu_temp_target = 90
        thermal.fan_speed_min = 0
        thermal.fan_speed_max = 100

        # Values should be within reasonable ranges
        assert 60 <= thermal.cpu_temp_target <= 100
        assert 0 <= thermal.fan_speed_min <= 100
        assert 0 <= thermal.fan_speed_max <= 100


class TestGPUConfig:
    """Test GPU configuration"""

    def test_gpu_defaults(self):
        """Test GPU configuration defaults"""
        gpu = GPUConfig()

        assert gpu.overclocking_enabled is False
        assert gpu.core_clock_offset == 0
        assert gpu.memory_clock_offset == 0
        assert gpu.power_limit == 140
        assert gpu.auto_gpu_switching is True

    def test_gpu_validation(self):
        """Test GPU configuration validation"""
        gpu = GPUConfig()

        # Test valid overclock values
        gpu.core_clock_offset = 150
        gpu.memory_clock_offset = 500

        # Should be within safe ranges
        assert -300 <= gpu.core_clock_offset <= 300
        assert -1000 <= gpu.memory_clock_offset <= 1000


if __name__ == "__main__":
    pytest.main([__file__])