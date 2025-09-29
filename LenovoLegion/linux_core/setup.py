#!/usr/bin/env python3
"""
Legion Toolkit Linux - Setup script for backward compatibility
"""

from setuptools import setup, find_packages
import os
import sys

# Ensure we're running on Python 3.8+
if sys.version_info < (3, 8):
    print("Error: Legion Toolkit requires Python 3.8 or newer")
    sys.exit(1)

# Read version from __init__.py
def get_version():
    version_file = os.path.join(os.path.dirname(__file__), "legion_toolkit", "__init__.py")
    if os.path.exists(version_file):
        with open(version_file) as f:
            for line in f:
                if line.startswith("__version__"):
                    return line.split("=")[1].strip().strip('"\'')
    return "6.0.0"

# Read long description from README
def get_long_description():
    readme_file = os.path.join(os.path.dirname(__file__), "README.md")
    if os.path.exists(readme_file):
        with open(readme_file, encoding='utf-8') as f:
            return f.read()
    return "Legion Toolkit Linux - Advanced hardware control for Legion laptops"

# Check for system dependencies
def check_system_dependencies():
    """Check for required system packages"""
    required_system_packages = [
        "python3-dev",
        "build-essential",
        "dkms",
        "linux-headers-generic",
    ]

    missing_packages = []

    # Check if running on supported distribution
    try:
        import platform
        system = platform.system()
        if system != "Linux":
            print(f"Warning: Legion Toolkit is designed for Linux, detected {system}")
    except ImportError:
        pass

    # Check for pkg-config (required for PyGObject)
    if os.system("pkg-config --version >/dev/null 2>&1") != 0:
        missing_packages.append("pkg-config")

    # Check for GTK development files
    if os.system("pkg-config --exists gtk4 >/dev/null 2>&1") != 0:
        missing_packages.append("libgtk-4-dev")

    if missing_packages:
        print("Warning: Missing system packages:")
        for pkg in missing_packages:
            print(f"  - {pkg}")
        print("\nInstall with:")
        print("  Ubuntu/Debian: sudo apt install " + " ".join(missing_packages))
        print("  Fedora: sudo dnf install " + " ".join(missing_packages))
        print("  Arch: sudo pacman -S " + " ".join(missing_packages))

# Custom build command
class CustomBuildCommand:
    """Custom build steps for Legion Toolkit"""

    @staticmethod
    def build_kernel_module():
        """Build kernel module if Make is available"""
        kernel_module_dir = os.path.join(os.path.dirname(__file__), "kernel_module")
        if os.path.exists(kernel_module_dir) and os.path.exists(os.path.join(kernel_module_dir, "Makefile")):
            print("Building kernel module...")
            old_cwd = os.getcwd()
            try:
                os.chdir(kernel_module_dir)
                if os.system("make") == 0:
                    print("Kernel module built successfully")
                else:
                    print("Warning: Kernel module build failed")
            finally:
                os.chdir(old_cwd)

# Main setup
if __name__ == "__main__":
    # Check system dependencies
    check_system_dependencies()

    # Build kernel module if possible
    try:
        CustomBuildCommand.build_kernel_module()
    except Exception as e:
        print(f"Warning: Could not build kernel module: {e}")

    setup(
        name="legion-toolkit-linux",
        version=get_version(),
        description="Legion Toolkit Linux - Advanced hardware control for Legion laptops",
        long_description=get_long_description(),
        long_description_content_type="text/markdown",
        author="Vivek Chamoli",
        author_email="vivek@legion-toolkit.org",
        url="https://github.com/vivekchamoli/LenovoLegion7i",
        project_urls={
            "Bug Tracker": "https://github.com/vivekchamoli/LenovoLegion7i/issues",
            "Documentation": "https://github.com/vivekchamoli/LenovoLegion7i/blob/main/README.md",
            "Source Code": "https://github.com/vivekchamoli/LenovoLegion7i",
        },
        packages=find_packages(),
        include_package_data=True,
        package_data={
            "legion_toolkit.gui": ["*.ui", "*.css", "*.png", "*.svg", "*.gresource"],
            "legion_toolkit.kernel_module": ["*.c", "*.h", "Makefile", "dkms.conf"],
            "legion_toolkit.data": ["*.json", "*.yaml", "*.conf"],
        },
        entry_points={
            "console_scripts": [
                "legion-toolkit=legion_toolkit.cli.main:main",
                "legion-ai-optimizer=legion_toolkit.ai.optimizer:main",
                "legion-hardware-test=legion_toolkit.hardware.test:main",
            ],
            "gui_scripts": [
                "legion-toolkit-gui=legion_toolkit.gui.main:main",
            ],
        },
        python_requires=">=3.8",
        install_requires=[
            # Core dependencies
            "numpy>=1.21.0",
            "psutil>=5.8.0",

            # AI/ML dependencies
            "torch>=1.12.0",
            "scikit-learn>=1.1.0",

            # GUI dependencies (optional, installed by distribution packages)
            # "PyGObject>=3.42.0",  # Handled by system packages

            # Web/API dependencies
            "aiohttp>=3.8.0",
            "websockets>=10.0",

            # System integration
            # "dbus-python>=1.2.18",  # Handled by system packages

            # Configuration and serialization
            "PyYAML>=6.0",
            "click>=8.0.0",
        ],
        extras_require={
            "full": [
                "torch>=1.12.0",
                "torchvision>=0.13.0",
                "fastapi>=0.95.0",
                "uvicorn>=0.20.0",
            ],
            "dev": [
                "pytest>=7.0.0",
                "pytest-asyncio>=0.20.0",
                "black>=22.0.0",
                "isort>=5.10.0",
                "flake8>=5.0.0",
            ],
        },
        classifiers=[
            "Development Status :: 5 - Production/Stable",
            "Intended Audience :: End Users/Desktop",
            "License :: OSI Approved :: GNU General Public License v3 (GPLv3)",
            "Operating System :: POSIX :: Linux",
            "Programming Language :: Python :: 3",
            "Programming Language :: Python :: 3.8",
            "Programming Language :: Python :: 3.9",
            "Programming Language :: Python :: 3.10",
            "Programming Language :: Python :: 3.11",
            "Programming Language :: Python :: 3.12",
            "Topic :: System :: Hardware",
            "Topic :: Utilities",
        ],
        keywords="legion laptop hardware thermal gaming lenovo",
        zip_safe=False,
    )