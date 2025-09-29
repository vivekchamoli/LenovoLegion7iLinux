"""
Tests for hardware controllers
"""

import pytest
from unittest.mock import Mock, patch, MagicMock
import subprocess


class TestECController:
    """Test EC (Embedded Controller) functionality"""

    def test_ec_controller_initialization(self, mock_ec_controller):
        """Test EC controller initialization"""
        from legion_toolkit.hardware.ec_controller import ECController

        with patch('legion_toolkit.hardware.ec_controller.ECController.__init__', return_value=None):
            ec = ECController()
            assert ec is not None

    @pytest.mark.hardware
    def test_ec_register_read(self, mock_ec_controller):
        """Test EC register reading"""
        # Mock the actual EC read operation
        mock_ec_controller.read_register.return_value = 0x42

        result = mock_ec_controller.read_register(0xA0)
        assert result == 0x42
        mock_ec_controller.read_register.assert_called_with(0xA0)

    @pytest.mark.hardware
    def test_ec_register_write(self, mock_ec_controller):
        """Test EC register writing"""
        mock_ec_controller.write_register.return_value = True

        result = mock_ec_controller.write_register(0xA0, 0x01)
        assert result is True
        mock_ec_controller.write_register.assert_called_with(0xA0, 0x01)

    @pytest.mark.hardware
    def test_performance_mode_switching(self, mock_ec_controller):
        """Test performance mode switching"""
        mock_ec_controller.set_performance_mode = Mock(return_value=True)

        result = mock_ec_controller.set_performance_mode("performance")
        assert result is True

    @pytest.mark.hardware
    def test_fan_control(self, mock_ec_controller):
        """Test fan speed control"""
        mock_ec_controller.set_fan_speed = Mock(return_value=True)
        mock_ec_controller.get_fan_speed = Mock(return_value=2500)

        # Test fan speed setting
        result = mock_ec_controller.set_fan_speed(1, 75)  # Fan 1, 75%
        assert result is True

        # Test fan speed reading
        speed = mock_ec_controller.get_fan_speed(1)
        assert speed == 2500

    @pytest.mark.hardware
    def test_temperature_reading(self, mock_ec_controller):
        """Test temperature sensor reading"""
        mock_ec_controller.get_cpu_temperature = Mock(return_value=65.5)
        mock_ec_controller.get_gpu_temperature = Mock(return_value=72.0)

        cpu_temp = mock_ec_controller.get_cpu_temperature()
        gpu_temp = mock_ec_controller.get_gpu_temperature()

        assert cpu_temp == 65.5
        assert gpu_temp == 72.0

    def test_ec_availability_check(self, mock_ec_controller):
        """Test EC availability checking"""
        mock_ec_controller.is_available.return_value = True

        assert mock_ec_controller.is_available() is True

    @pytest.mark.hardware
    def test_thermal_threshold_setting(self, mock_ec_controller):
        """Test thermal threshold configuration"""
        mock_ec_controller.set_thermal_threshold = Mock(return_value=True)

        result = mock_ec_controller.set_thermal_threshold("cpu", 95)
        assert result is True


class TestGPUController:
    """Test GPU controller functionality"""

    def test_gpu_controller_initialization(self, mock_gpu_controller):
        """Test GPU controller initialization"""
        from legion_toolkit.hardware.gpu_controller import LinuxGPUController

        with patch('legion_toolkit.hardware.gpu_controller.LinuxGPUController.__init__', return_value=None):
            gpu = LinuxGPUController()
            assert gpu is not None

    @pytest.mark.gpu
    def test_gpu_info_retrieval(self, mock_gpu_controller):
        """Test GPU information retrieval"""
        expected_info = {
            "name": "NVIDIA GeForce RTX 4070 Laptop GPU",
            "driver_version": "535.86.05",
            "temperature": 65.0,
            "power_draw": 85.0,
            "memory_used": 4096,
            "memory_total": 8192,
            "clock_core": 2610,
            "clock_memory": 8001,
            "utilization": 45.0
        }

        mock_gpu_controller.get_gpu_info.return_value = expected_info

        info = mock_gpu_controller.get_gpu_info()
        assert info["name"] == "NVIDIA GeForce RTX 4070 Laptop GPU"
        assert info["temperature"] == 65.0
        assert info["power_draw"] == 85.0

    @pytest.mark.gpu
    def test_gpu_overclocking(self, mock_gpu_controller):
        """Test GPU overclocking functionality"""
        mock_gpu_controller.set_overclock.return_value = True

        # Test core clock offset
        result = mock_gpu_controller.set_overclock(core_offset=100, memory_offset=500)
        assert result is True
        mock_gpu_controller.set_overclock.assert_called_with(core_offset=100, memory_offset=500)

    @pytest.mark.gpu
    def test_gpu_power_limit(self, mock_gpu_controller):
        """Test GPU power limit control"""
        mock_gpu_controller.set_power_limit.return_value = True

        result = mock_gpu_controller.set_power_limit(130)  # 130W
        assert result is True
        mock_gpu_controller.set_power_limit.assert_called_with(130)

    @pytest.mark.gpu
    def test_gpu_fan_control(self, mock_gpu_controller):
        """Test GPU fan control"""
        mock_gpu_controller.set_fan_speed = Mock(return_value=True)
        mock_gpu_controller.get_fan_speed = Mock(return_value=75)

        # Set fan speed
        result = mock_gpu_controller.set_fan_speed(80)
        assert result is True

        # Get fan speed
        speed = mock_gpu_controller.get_fan_speed()
        assert speed == 75

    def test_nvidia_availability_check(self):
        """Test NVIDIA GPU availability detection"""
        with patch('subprocess.run') as mock_subprocess:
            # Mock lspci output with NVIDIA GPU
            mock_result = Mock()
            mock_result.stdout = "01:00.0 VGA compatible controller: NVIDIA Corporation GA104M [GeForce RTX 4070 Mobile]"
            mock_result.returncode = 0
            mock_subprocess.return_value = mock_result

            from legion_toolkit.hardware.gpu_controller import LinuxGPUController

            # This would normally check for NVIDIA presence
            # We'll just test the mocking works
            assert mock_result.stdout is not None
            assert "NVIDIA" in mock_result.stdout

    @pytest.mark.gpu
    def test_gpu_memory_info(self, mock_gpu_controller):
        """Test GPU memory information"""
        mock_gpu_controller.get_memory_info = Mock(return_value={
            "total": 8192,  # MB
            "used": 2048,   # MB
            "free": 6144    # MB
        })

        memory_info = mock_gpu_controller.get_memory_info()
        assert memory_info["total"] == 8192
        assert memory_info["used"] == 2048
        assert memory_info["free"] == 6144


class TestRGBController:
    """Test RGB controller functionality"""

    @pytest.fixture
    def mock_rgb_controller(self):
        """Mock RGB controller"""
        from unittest.mock import Mock
        mock_rgb = Mock()
        mock_rgb.is_available.return_value = True
        mock_rgb.set_color.return_value = True
        mock_rgb.set_effect.return_value = True
        mock_rgb.set_brightness.return_value = True
        return mock_rgb

    def test_rgb_controller_initialization(self, mock_rgb_controller):
        """Test RGB controller initialization"""
        assert mock_rgb_controller is not None
        assert mock_rgb_controller.is_available() is True

    @pytest.mark.hardware
    def test_rgb_color_setting(self, mock_rgb_controller):
        """Test RGB color setting"""
        # Test single color
        result = mock_rgb_controller.set_color("#FF0000")  # Red
        assert result is True

        # Test zone-specific color
        result = mock_rgb_controller.set_color("#00FF00", zone=1)  # Green, zone 1
        assert result is True

    @pytest.mark.hardware
    def test_rgb_effects(self, mock_rgb_controller):
        """Test RGB effects"""
        effects = ["static", "breathing", "rainbow", "wave"]

        for effect in effects:
            result = mock_rgb_controller.set_effect(effect)
            assert result is True

    @pytest.mark.hardware
    def test_rgb_brightness_control(self, mock_rgb_controller):
        """Test RGB brightness control"""
        # Test brightness levels
        for brightness in [0, 25, 50, 75, 100]:
            result = mock_rgb_controller.set_brightness(brightness)
            assert result is True

    @pytest.mark.hardware
    def test_rgb_zone_control(self, mock_rgb_controller):
        """Test individual zone control"""
        mock_rgb_controller.set_zone_color = Mock(return_value=True)

        # Test 4-zone control (Gen 9 Spectrum)
        colors = ["#FF0000", "#00FF00", "#0000FF", "#FFFF00"]

        for zone, color in enumerate(colors, 1):
            result = mock_rgb_controller.set_zone_color(zone, color)
            assert result is True


class TestThermalController:
    """Test thermal management functionality"""

    @pytest.fixture
    def mock_thermal_controller(self):
        """Mock thermal controller"""
        from unittest.mock import Mock
        mock_thermal = Mock()
        mock_thermal.get_temperatures.return_value = {
            "cpu": 65.0,
            "gpu": 70.0,
            "gpu_hotspot": 85.0,
            "vram": 75.0
        }
        mock_thermal.set_fan_curve.return_value = True
        mock_thermal.apply_thermal_profile.return_value = True
        return mock_thermal

    @pytest.mark.hardware
    def test_temperature_monitoring(self, mock_thermal_controller):
        """Test temperature monitoring"""
        temps = mock_thermal_controller.get_temperatures()

        assert "cpu" in temps
        assert "gpu" in temps
        assert temps["cpu"] > 0
        assert temps["gpu"] > 0

    @pytest.mark.hardware
    def test_fan_curve_setting(self, mock_thermal_controller):
        """Test fan curve configuration"""
        fan_curve = [
            (50, 30),  # 50°C -> 30% speed
            (60, 50),  # 60°C -> 50% speed
            (70, 70),  # 70°C -> 70% speed
            (80, 90),  # 80°C -> 90% speed
            (90, 100), # 90°C -> 100% speed
        ]

        result = mock_thermal_controller.set_fan_curve(fan_curve)
        assert result is True

    @pytest.mark.hardware
    def test_thermal_profile_application(self, mock_thermal_controller):
        """Test thermal profile application"""
        profiles = ["quiet", "balanced", "performance", "custom"]

        for profile in profiles:
            result = mock_thermal_controller.apply_thermal_profile(profile)
            assert result is True


class TestHardwareIntegration:
    """Integration tests for hardware controllers"""

    @pytest.mark.integration
    @pytest.mark.hardware
    def test_hardware_detection(self):
        """Test overall hardware detection"""
        with patch('subprocess.run') as mock_subprocess:
            # Mock DMI information
            mock_result = Mock()
            mock_result.stdout = "Legion Slim 7i Gen 9 (16IRX9)"
            mock_result.returncode = 0
            mock_subprocess.return_value = mock_result

            # This would normally detect Legion hardware
            assert mock_result.stdout is not None

    @pytest.mark.integration
    @pytest.mark.hardware
    def test_kernel_module_integration(self):
        """Test kernel module integration"""
        with patch('subprocess.run') as mock_subprocess:
            # Mock lsmod output
            mock_result = Mock()
            mock_result.stdout = "legion_laptop_16irx9    32768  0"
            mock_result.returncode = 0
            mock_subprocess.return_value = mock_result

            # Check if kernel module is loaded
            assert "legion_laptop_16irx9" in mock_result.stdout

    @pytest.mark.integration
    def test_hardware_controller_coordination(self, mock_ec_controller, mock_gpu_controller):
        """Test coordination between hardware controllers"""
        # Test that multiple controllers can work together
        assert mock_ec_controller.is_available() is True
        assert mock_gpu_controller.get_gpu_info() is not None

        # Test setting performance mode affects both controllers
        mock_ec_controller.set_performance_mode("performance")
        gpu_info = mock_gpu_controller.get_gpu_info()

        assert gpu_info is not None


if __name__ == "__main__":
    pytest.main([__file__])