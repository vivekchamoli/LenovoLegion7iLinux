"""
Legion Toolkit Linux - Advanced hardware control for Legion laptops

This package provides comprehensive hardware control and optimization
for Lenovo Legion laptops on Linux systems.

Features:
- Direct EC register access via kernel module
- Advanced thermal management with AI optimization
- GPU overclocking and power management
- RGB keyboard control (Spectrum 4-zone)
- Modern GTK4 user interface
- Command-line tools
- Real-time monitoring and automation
"""

__version__ = "6.0.0"
__author__ = "Vivek Chamoli"
__email__ = "vivek@legion-toolkit.org"
__license__ = "GPL-3.0"

# Core modules - conditional imports for build compatibility
try:
    from . import hardware
except ImportError:
    hardware = None

try:
    from . import ai
except ImportError:
    ai = None

try:
    from . import features
except ImportError:
    features = None

try:
    from . import automation
except ImportError:
    automation = None

try:
    from . import gui
except ImportError:
    gui = None

try:
    from . import cli
except ImportError:
    cli = None

# Version information
VERSION_INFO = {
    "version": __version__,
    "author": __author__,
    "license": __license__,
    "target_hardware": "Legion Slim 7i Gen 9 (16IRX9)",
    "supported_systems": ["Linux"],
    "features": [
        "Hardware Control",
        "Thermal Management",
        "GPU Overclocking",
        "RGB Control",
        "AI Optimization",
        "Automation",
        "Modern GUI",
        "CLI Tools"
    ]
}

def get_version():
    """Get version string"""
    return __version__

def get_system_info():
    """Get system information and compatibility"""
    import platform
    import sys

    info = {
        "legion_toolkit_version": __version__,
        "python_version": sys.version,
        "platform": platform.platform(),
        "architecture": platform.architecture(),
        "processor": platform.processor(),
    }

    # Check for hardware support
    try:
        import subprocess
        result = subprocess.run(['lspci'], capture_output=True, text=True)
        if 'NVIDIA' in result.stdout:
            info["gpu_support"] = "NVIDIA detected"
        if 'Intel' in result.stdout:
            info["cpu_support"] = "Intel detected"
    except:
        info["hardware_detection"] = "Limited"

    return info

# Initialize logging
import logging

def setup_logging(level=logging.INFO, log_file=None):
    """Setup logging for Legion Toolkit"""
    formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

    # Console handler
    console_handler = logging.StreamHandler()
    console_handler.setFormatter(formatter)

    # Root logger
    root_logger = logging.getLogger('legion_toolkit')
    root_logger.setLevel(level)
    root_logger.addHandler(console_handler)

    # File handler (optional)
    if log_file:
        file_handler = logging.FileHandler(log_file)
        file_handler.setFormatter(formatter)
        root_logger.addHandler(file_handler)

    return root_logger

# Default logger
logger = setup_logging()

# Export main classes and functions - conditional for build compatibility
try:
    from .hardware.ec_controller import ECController
except ImportError:
    ECController = None

try:
    from .hardware.gpu_controller import LinuxGPUController
except ImportError:
    LinuxGPUController = None

try:
    from .hardware.rgb_controller import RGBController
except ImportError:
    RGBController = None

try:
    from .ai.ai_controller import AIController
except ImportError:
    AIController = None

try:
    from .features.battery_manager import BatteryManager
except ImportError:
    BatteryManager = None

__all__ = [
    'VERSION_INFO',
    'get_version',
    'get_system_info',
    'setup_logging',
    'ECController',
    'LinuxGPUController',
    'RGBController',
    'AIController',
    'BatteryManager',
    'hardware',
    'ai',
    'features',
    'automation',
    'gui',
    'cli'
]