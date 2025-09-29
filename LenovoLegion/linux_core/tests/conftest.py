"""
Pytest configuration and fixtures for Legion Toolkit testing
"""

import os
import sys
import pytest
import tempfile
from pathlib import Path
from unittest.mock import Mock, patch

# Add the parent directory to sys.path so we can import our modules
sys.path.insert(0, str(Path(__file__).parent.parent))

@pytest.fixture
def mock_hardware():
    """Mock hardware environment for testing"""
    hardware_info = {
        "platform": "linux",
        "model": "Legion Slim 7i Gen 9 (16IRX9)",
        "cpu": "Intel Core i9-14900HX",
        "gpu": "NVIDIA GeForce RTX 4070 Laptop GPU",
        "memory": "32GB DDR5-5600",
        "kernel_module_loaded": True,
        "ec_support": True
    }

    with patch('legion_toolkit.config.ConfigManager._detect_hardware') as mock_detect:
        mock_detect.return_value = Mock(**hardware_info)
        yield hardware_info

@pytest.fixture
def temp_config_dir():
    """Temporary configuration directory for testing"""
    with tempfile.TemporaryDirectory() as temp_dir:
        config_path = Path(temp_dir) / "legion-toolkit"
        config_path.mkdir(parents=True)
        yield config_path

@pytest.fixture
def mock_ec_controller():
    """Mock EC controller for testing hardware interactions"""
    with patch('legion_toolkit.hardware.ec_controller.ECController') as mock_ec:
        mock_instance = Mock()
        mock_instance.read_register.return_value = 0x50
        mock_instance.write_register.return_value = True
        mock_instance.is_available.return_value = True
        mock_ec.return_value = mock_instance
        yield mock_instance

@pytest.fixture
def mock_gpu_controller():
    """Mock GPU controller for testing GPU operations"""
    with patch('legion_toolkit.hardware.gpu_controller.LinuxGPUController') as mock_gpu:
        mock_instance = Mock()
        mock_instance.get_gpu_info.return_value = {
            "name": "NVIDIA GeForce RTX 4070 Laptop GPU",
            "temperature": 65.0,
            "power_draw": 85.0,
            "clock_core": 2610,
            "clock_memory": 8001,
            "utilization": 45.0
        }
        mock_instance.set_power_limit.return_value = True
        mock_instance.set_overclock.return_value = True
        mock_gpu.return_value = mock_instance
        yield mock_instance

@pytest.fixture
def sample_config():
    """Sample configuration for testing"""
    from legion_toolkit.config import LegionConfig, ThermalConfig, GPUConfig

    config = LegionConfig()
    config.thermal = ThermalConfig(
        cpu_temp_target=85,
        gpu_temp_target=80,
        ai_thermal_optimization=True
    )
    config.gpu = GPUConfig(
        overclocking_enabled=True,
        core_clock_offset=100,
        memory_clock_offset=500,
        power_limit=140
    )

    return config

@pytest.fixture(scope="session")
def skip_hardware_tests():
    """Skip hardware tests if not running on supported hardware"""
    return not (
        Path("/sys/kernel/legion_laptop_16irx9").exists() or
        os.environ.get("LEGION_FORCE_TESTS") == "1"
    )

@pytest.fixture(scope="session")
def skip_root_tests():
    """Skip tests that require root privileges"""
    return os.geteuid() != 0 and os.environ.get("LEGION_FORCE_TESTS") != "1"

# Pytest marks for test categorization
def pytest_configure(config):
    """Configure pytest marks"""
    config.addinivalue_line(
        "markers", "hardware: mark test as requiring actual hardware"
    )
    config.addinivalue_line(
        "markers", "root: mark test as requiring root privileges"
    )
    config.addinivalue_line(
        "markers", "slow: mark test as slow running"
    )
    config.addinivalue_line(
        "markers", "gpu: mark test as requiring NVIDIA GPU"
    )
    config.addinivalue_line(
        "markers", "integration: mark test as integration test"
    )

def pytest_collection_modifyitems(config, items):
    """Modify test collection based on markers"""
    skip_hw = pytest.mark.skip(reason="Hardware not available")
    skip_root = pytest.mark.skip(reason="Root privileges required")
    skip_gpu = pytest.mark.skip(reason="NVIDIA GPU not available")

    # Check for hardware availability
    hardware_available = (
        Path("/sys/kernel/legion_laptop_16irx9").exists() or
        os.environ.get("LEGION_FORCE_TESTS") == "1"
    )

    root_available = (
        os.geteuid() == 0 or
        os.environ.get("LEGION_FORCE_TESTS") == "1"
    )

    gpu_available = False
    try:
        import subprocess
        result = subprocess.run(["lspci"], capture_output=True, text=True)
        gpu_available = "NVIDIA" in result.stdout
    except:
        pass

    for item in items:
        if "hardware" in item.keywords and not hardware_available:
            item.add_marker(skip_hw)
        if "root" in item.keywords and not root_available:
            item.add_marker(skip_root)
        if "gpu" in item.keywords and not gpu_available:
            item.add_marker(skip_gpu)