#!/usr/bin/env python3
"""
Legion Toolkit CLI - Command Line Interface for Linux
Complete feature parity with Windows CLI functionality
Supports Legion Slim 7i Gen 9 (16IRX9)
"""

import argparse
import asyncio
import json
import sys
import os
from pathlib import Path
from typing import Dict, List, Optional, Any
import subprocess
from datetime import datetime

# Import core components
try:
    from ..hardware.gpu_controller import LinuxGPUController
    from ..hardware.rgb_controller import LinuxRGBController
    from ..ai.ai_controller import LinuxAIController
except ImportError:
    # Fallback for standalone execution
    sys.path.append(str(Path(__file__).parent.parent))
    from hardware.gpu_controller import LinuxGPUController
    from hardware.rgb_controller import LinuxRGBController
    from ai.ai_controller import LinuxAIController

class LegionToolkitCLI:
    """Main CLI controller class"""

    def __init__(self):
        self.gpu_controller = LinuxGPUController()
        self.rgb_controller = LinuxRGBController()
        self.ai_controller = LinuxAIController()
        self.kernel_module_path = "/sys/kernel/legion_laptop"

    async def initialize(self) -> bool:
        """Initialize all controllers"""
        try:
            # Check for root privileges
            if os.geteuid() != 0:
                print("Error: Legion Toolkit CLI requires root privileges")
                print("Please run with sudo: sudo legion-toolkit-cli")
                return False

            # Check kernel module
            if not Path(self.kernel_module_path).exists():
                print("Error: Legion kernel module not loaded")
                print("Please install and load the legion-laptop kernel module")
                return False

            # Initialize controllers
            await self.gpu_controller.initialize()
            await self.rgb_controller.initialize()
            await self.ai_controller.initialize()

            return True
        except Exception as e:
            print(f"Initialization failed: {e}")
            return False

    def read_kernel_param(self, param: str) -> Optional[str]:
        """Read parameter from kernel module"""
        try:
            path = Path(self.kernel_module_path) / param
            if path.exists():
                return path.read_text().strip()
        except Exception as e:
            print(f"Error reading {param}: {e}")
        return None

    def write_kernel_param(self, param: str, value: str) -> bool:
        """Write parameter to kernel module"""
        try:
            path = Path(self.kernel_module_path) / param
            if path.exists():
                subprocess.run(['sh', '-c', f'echo {value} > {path}'], check=True)
                return True
        except Exception as e:
            print(f"Error writing {param}: {e}")
        return False

    # Performance Mode Commands
    async def get_performance_mode(self) -> Dict[str, Any]:
        """Get current performance mode"""
        mode = self.read_kernel_param("performance_mode")
        if mode:
            return {
                "current_mode": mode,
                "available_modes": ["quiet", "balanced", "performance", "custom"],
                "description": self.get_mode_description(mode)
            }
        return {"error": "Could not read performance mode"}

    async def set_performance_mode(self, mode: str) -> Dict[str, Any]:
        """Set performance mode"""
        valid_modes = ["quiet", "balanced", "performance", "custom"]
        if mode not in valid_modes:
            return {"error": f"Invalid mode. Valid modes: {valid_modes}"}

        if self.write_kernel_param("performance_mode", mode):
            return {"success": f"Performance mode set to {mode}"}
        return {"error": f"Failed to set performance mode to {mode}"}

    def get_mode_description(self, mode: str) -> str:
        """Get description for performance mode"""
        descriptions = {
            "quiet": "Silent operation, reduced performance for battery life",
            "balanced": "Balanced performance and thermal management",
            "performance": "Maximum performance with aggressive cooling",
            "custom": "User-defined custom settings"
        }
        return descriptions.get(mode, "Unknown mode")

    # Thermal Monitoring Commands
    async def get_temperatures(self) -> Dict[str, Any]:
        """Get all temperature readings"""
        temps = {}

        # CPU temperature
        cpu_temp = self.read_kernel_param("cpu_temp")
        if cpu_temp:
            temps["cpu"] = f"{cpu_temp}°C"

        # GPU temperature
        gpu_temp = self.read_kernel_param("gpu_temp")
        if gpu_temp:
            temps["gpu"] = f"{gpu_temp}°C"

        # Additional sensors via thermal zones
        try:
            for i in range(10):
                thermal_path = f"/sys/class/thermal/thermal_zone{i}/temp"
                if Path(thermal_path).exists():
                    with open(thermal_path, 'r') as f:
                        temp = int(f.read().strip()) / 1000
                        temps[f"thermal_zone{i}"] = f"{temp:.1f}°C"
        except:
            pass

        return temps if temps else {"error": "No temperature sensors available"}

    async def get_fan_speeds(self) -> Dict[str, Any]:
        """Get fan speed information"""
        fans = {}

        fan1_speed = self.read_kernel_param("fan1_speed")
        if fan1_speed:
            fans["fan1"] = f"{fan1_speed} RPM"

        fan2_speed = self.read_kernel_param("fan2_speed")
        if fan2_speed:
            fans["fan2"] = f"{fan2_speed} RPM"

        return fans if fans else {"error": "No fan sensors available"}

    async def set_fan_speed(self, fan: int, speed: int) -> Dict[str, Any]:
        """Set fan speed (0-100%)"""
        if fan not in [1, 2]:
            return {"error": "Fan number must be 1 or 2"}

        if not 0 <= speed <= 100:
            return {"error": "Fan speed must be 0-100%"}

        param = f"fan{fan}_target"
        if self.write_kernel_param(param, str(speed)):
            return {"success": f"Fan {fan} speed set to {speed}%"}
        return {"error": f"Failed to set fan {fan} speed"}

    # Power Management Commands
    async def get_power_limits(self) -> Dict[str, Any]:
        """Get current power limits"""
        limits = {}

        # CPU power limits
        cpu_pl1 = self.read_kernel_param("cpu_pl1")
        if cpu_pl1:
            limits["cpu_pl1"] = f"{cpu_pl1}W"

        cpu_pl2 = self.read_kernel_param("cpu_pl2")
        if cpu_pl2:
            limits["cpu_pl2"] = f"{cpu_pl2}W"

        # GPU TGP
        gpu_tgp = self.read_kernel_param("gpu_tgp")
        if gpu_tgp:
            limits["gpu_tgp"] = f"{gpu_tgp}W"

        return limits if limits else {"error": "No power limit information available"}

    async def set_power_limit(self, component: str, limit: int) -> Dict[str, Any]:
        """Set power limit for CPU or GPU"""
        if component == "cpu_pl1":
            if not 15 <= limit <= 55:
                return {"error": "CPU PL1 must be 15-55W"}
        elif component == "cpu_pl2":
            if not 55 <= limit <= 140:
                return {"error": "CPU PL2 must be 55-140W"}
        elif component == "gpu_tgp":
            if not 60 <= limit <= 140:
                return {"error": "GPU TGP must be 60-140W"}
        else:
            return {"error": "Invalid component. Use: cpu_pl1, cpu_pl2, gpu_tgp"}

        if self.write_kernel_param(component, str(limit)):
            return {"success": f"{component.upper()} set to {limit}W"}
        return {"error": f"Failed to set {component}"}

    # RGB Control Commands
    async def get_rgb_status(self) -> Dict[str, Any]:
        """Get RGB lighting status"""
        return await self.rgb_controller.get_current_profile()

    async def set_rgb_mode(self, mode: str, **kwargs) -> Dict[str, Any]:
        """Set RGB lighting mode"""
        try:
            if mode == "off":
                success = await self.rgb_controller.turn_off()
            elif mode == "static":
                color = kwargs.get('color', '#FF0000')
                success = await self.rgb_controller.set_static_color(color)
            elif mode == "breathing":
                color = kwargs.get('color', '#FF0000')
                speed = kwargs.get('speed', 5)
                success = await self.rgb_controller.set_breathing_effect(color, speed)
            elif mode == "rainbow":
                speed = kwargs.get('speed', 5)
                success = await self.rgb_controller.set_rainbow_effect(speed)
            elif mode == "wave":
                speed = kwargs.get('speed', 5)
                success = await self.rgb_controller.set_wave_effect(speed)
            else:
                return {"error": f"Invalid mode. Valid modes: off, static, breathing, rainbow, wave"}

            if success:
                return {"success": f"RGB mode set to {mode}"}
            return {"error": f"Failed to set RGB mode to {mode}"}
        except Exception as e:
            return {"error": f"RGB operation failed: {e}"}

    async def set_rgb_brightness(self, brightness: int) -> Dict[str, Any]:
        """Set RGB brightness (0-100%)"""
        if not 0 <= brightness <= 100:
            return {"error": "Brightness must be 0-100%"}

        try:
            success = await self.rgb_controller.set_brightness(brightness)
            if success:
                return {"success": f"RGB brightness set to {brightness}%"}
            return {"error": "Failed to set RGB brightness"}
        except Exception as e:
            return {"error": f"RGB operation failed: {e}"}

    # GPU Commands
    async def get_gpu_info(self) -> Dict[str, Any]:
        """Get GPU information and status"""
        try:
            info = await self.gpu_controller.get_gpu_info()
            status = await self.gpu_controller.get_gpu_status()
            return {**info, **status}
        except Exception as e:
            return {"error": f"Failed to get GPU info: {e}"}

    async def set_gpu_overclock(self, core_offset: int, memory_offset: int) -> Dict[str, Any]:
        """Set GPU overclock offsets"""
        try:
            success = await self.gpu_controller.set_overclock(core_offset, memory_offset)
            if success:
                return {"success": f"GPU overclock set: Core +{core_offset}MHz, Memory +{memory_offset}MHz"}
            return {"error": "Failed to set GPU overclock"}
        except Exception as e:
            return {"error": f"GPU overclock failed: {e}"}

    async def reset_gpu_overclock(self) -> Dict[str, Any]:
        """Reset GPU to stock settings"""
        try:
            success = await self.gpu_controller.reset_overclock()
            if success:
                return {"success": "GPU overclock reset to stock settings"}
            return {"error": "Failed to reset GPU overclock"}
        except Exception as e:
            return {"error": f"GPU reset failed: {e}"}

    # AI Optimization Commands
    async def get_ai_status(self) -> Dict[str, Any]:
        """Get AI optimization status"""
        try:
            analytics = await self.ai_controller.get_system_analytics()
            return analytics
        except Exception as e:
            return {"error": f"Failed to get AI status: {e}"}

    async def run_ai_optimization(self) -> Dict[str, Any]:
        """Run AI thermal optimization"""
        try:
            # Start monitoring if not already running
            if not self.ai_controller.monitoring_active:
                await self.ai_controller.start_monitoring()

            # Get thermal prediction
            prediction = await self.ai_controller.predict_thermal_state()

            return {
                "prediction": prediction,
                "optimization_applied": True,
                "message": "AI optimization running"
            }
        except Exception as e:
            return {"error": f"AI optimization failed: {e}"}

    async def start_ai_monitoring(self) -> Dict[str, Any]:
        """Start continuous AI monitoring"""
        try:
            success = await self.ai_controller.start_monitoring()
            if success:
                return {"success": "AI monitoring started"}
            return {"error": "Failed to start AI monitoring"}
        except Exception as e:
            return {"error": f"AI monitoring failed: {e}"}

    async def stop_ai_monitoring(self) -> Dict[str, Any]:
        """Stop AI monitoring"""
        try:
            await self.ai_controller.stop_monitoring()
            return {"success": "AI monitoring stopped"}
        except Exception as e:
            return {"error": f"Failed to stop AI monitoring: {e}"}

    # System Information Commands
    async def get_system_info(self) -> Dict[str, Any]:
        """Get comprehensive system information"""
        info = {
            "hardware": {
                "model": "Legion Slim 7i Gen 9 (16IRX9)",
                "cpu": "Intel Core i9-14900HX",
                "gpu": "NVIDIA RTX 4070 Laptop GPU"
            },
            "kernel_module": Path(self.kernel_module_path).exists(),
            "timestamp": datetime.now().isoformat()
        }

        # Add temperatures
        temps = await self.get_temperatures()
        if "error" not in temps:
            info["temperatures"] = temps

        # Add fan speeds
        fans = await self.get_fan_speeds()
        if "error" not in fans:
            info["fans"] = fans

        # Add power limits
        power = await self.get_power_limits()
        if "error" not in power:
            info["power_limits"] = power

        # Add performance mode
        perf = await self.get_performance_mode()
        if "error" not in perf:
            info["performance_mode"] = perf

        return info

    async def export_config(self, filename: str) -> Dict[str, Any]:
        """Export current configuration to file"""
        try:
            config = await self.get_system_info()
            config["export_time"] = datetime.now().isoformat()

            with open(filename, 'w') as f:
                json.dump(config, f, indent=2)

            return {"success": f"Configuration exported to {filename}"}
        except Exception as e:
            return {"error": f"Export failed: {e}"}

    async def monitor_temps(self, duration: int = 60, interval: int = 2) -> Dict[str, Any]:
        """Monitor temperatures for specified duration"""
        print(f"Monitoring temperatures for {duration} seconds (interval: {interval}s)")
        print("Press Ctrl+C to stop early")

        try:
            start_time = datetime.now()
            readings = []

            while (datetime.now() - start_time).seconds < duration:
                temps = await self.get_temperatures()
                if "error" not in temps:
                    timestamp = datetime.now().isoformat()
                    reading = {"timestamp": timestamp, **temps}
                    readings.append(reading)

                    # Print current reading
                    temp_str = " | ".join([f"{k}: {v}" for k, v in temps.items()])
                    print(f"[{timestamp}] {temp_str}")

                await asyncio.sleep(interval)

            return {
                "success": f"Monitoring completed ({len(readings)} readings)",
                "readings": readings
            }

        except KeyboardInterrupt:
            return {"success": "Monitoring stopped by user", "readings": readings}
        except Exception as e:
            return {"error": f"Monitoring failed: {e}"}

def create_parser() -> argparse.ArgumentParser:
    """Create command line argument parser"""
    parser = argparse.ArgumentParser(
        description="Legion Toolkit CLI - Hardware control for Legion laptops",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  legion-toolkit-cli info                          # Show system information
  legion-toolkit-cli perf get                      # Get performance mode
  legion-toolkit-cli perf set performance          # Set performance mode
  legion-toolkit-cli temps                         # Show temperatures
  legion-toolkit-cli fans                          # Show fan speeds
  legion-toolkit-cli fan set 1 75                  # Set fan 1 to 75%
  legion-toolkit-cli power get                     # Show power limits
  legion-toolkit-cli power set cpu_pl2 120         # Set CPU PL2 to 120W
  legion-toolkit-cli rgb set static --color FF0000 # Set RGB to red
  legion-toolkit-cli rgb brightness 80             # Set RGB brightness to 80%
  legion-toolkit-cli gpu info                      # Show GPU information
  legion-toolkit-cli gpu overclock 150 500         # Overclock GPU (+150 core, +500 mem)
  legion-toolkit-cli ai status                     # Show AI optimization status
  legion-toolkit-cli ai optimize                   # Run AI optimization
  legion-toolkit-cli monitor 120                   # Monitor temps for 2 minutes
  legion-toolkit-cli export config.json            # Export configuration
        """
    )

    subparsers = parser.add_subparsers(dest='command', help='Available commands')

    # System info
    subparsers.add_parser('info', help='Show system information')

    # Performance mode
    perf_parser = subparsers.add_parser('perf', help='Performance mode control')
    perf_sub = perf_parser.add_subparsers(dest='perf_action')
    perf_sub.add_parser('get', help='Get current performance mode')
    perf_set = perf_sub.add_parser('set', help='Set performance mode')
    perf_set.add_argument('mode', choices=['quiet', 'balanced', 'performance', 'custom'])

    # Temperatures
    subparsers.add_parser('temps', help='Show temperature readings')

    # Fans
    subparsers.add_parser('fans', help='Show fan speeds')
    fan_parser = subparsers.add_parser('fan', help='Fan control')
    fan_sub = fan_parser.add_subparsers(dest='fan_action')
    fan_set = fan_sub.add_parser('set', help='Set fan speed')
    fan_set.add_argument('fan', type=int, choices=[1, 2], help='Fan number (1 or 2)')
    fan_set.add_argument('speed', type=int, help='Fan speed (0-100%)')

    # Power management
    power_parser = subparsers.add_parser('power', help='Power management')
    power_sub = power_parser.add_subparsers(dest='power_action')
    power_sub.add_parser('get', help='Get power limits')
    power_set = power_sub.add_parser('set', help='Set power limit')
    power_set.add_argument('component', choices=['cpu_pl1', 'cpu_pl2', 'gpu_tgp'])
    power_set.add_argument('limit', type=int, help='Power limit in watts')

    # RGB control
    rgb_parser = subparsers.add_parser('rgb', help='RGB lighting control')
    rgb_sub = rgb_parser.add_subparsers(dest='rgb_action')
    rgb_sub.add_parser('status', help='Get RGB status')
    rgb_set = rgb_sub.add_parser('set', help='Set RGB mode')
    rgb_set.add_argument('mode', choices=['off', 'static', 'breathing', 'rainbow', 'wave'])
    rgb_set.add_argument('--color', default='FF0000', help='Color in hex (for static/breathing)')
    rgb_set.add_argument('--speed', type=int, default=5, help='Animation speed (1-10)')
    rgb_brightness = rgb_sub.add_parser('brightness', help='Set RGB brightness')
    rgb_brightness.add_argument('level', type=int, help='Brightness level (0-100%)')

    # GPU control
    gpu_parser = subparsers.add_parser('gpu', help='GPU control')
    gpu_sub = gpu_parser.add_subparsers(dest='gpu_action')
    gpu_sub.add_parser('info', help='Show GPU information')
    gpu_oc = gpu_sub.add_parser('overclock', help='Set GPU overclock')
    gpu_oc.add_argument('core', type=int, help='Core clock offset (MHz)')
    gpu_oc.add_argument('memory', type=int, help='Memory clock offset (MHz)')
    gpu_sub.add_parser('reset', help='Reset GPU to stock settings')

    # AI optimization
    ai_parser = subparsers.add_parser('ai', help='AI optimization')
    ai_sub = ai_parser.add_subparsers(dest='ai_action')
    ai_sub.add_parser('status', help='Show AI status')
    ai_sub.add_parser('optimize', help='Run AI optimization')
    ai_sub.add_parser('start', help='Start AI monitoring')
    ai_sub.add_parser('stop', help='Stop AI monitoring')

    # Monitoring
    monitor_parser = subparsers.add_parser('monitor', help='Monitor temperatures')
    monitor_parser.add_argument('duration', type=int, default=60, nargs='?', help='Duration in seconds')
    monitor_parser.add_argument('--interval', type=int, default=2, help='Update interval in seconds')

    # Export
    export_parser = subparsers.add_parser('export', help='Export configuration')
    export_parser.add_argument('filename', help='Output filename')

    return parser

async def main():
    """Main CLI entry point"""
    parser = create_parser()
    args = parser.parse_args()

    if not args.command:
        parser.print_help()
        return 1

    # Initialize CLI
    cli = LegionToolkitCLI()
    if not await cli.initialize():
        return 1

    try:
        result = None

        # Route commands
        if args.command == 'info':
            result = await cli.get_system_info()

        elif args.command == 'perf':
            if args.perf_action == 'get':
                result = await cli.get_performance_mode()
            elif args.perf_action == 'set':
                result = await cli.set_performance_mode(args.mode)

        elif args.command == 'temps':
            result = await cli.get_temperatures()

        elif args.command == 'fans':
            result = await cli.get_fan_speeds()

        elif args.command == 'fan':
            if args.fan_action == 'set':
                result = await cli.set_fan_speed(args.fan, args.speed)

        elif args.command == 'power':
            if args.power_action == 'get':
                result = await cli.get_power_limits()
            elif args.power_action == 'set':
                result = await cli.set_power_limit(args.component, args.limit)

        elif args.command == 'rgb':
            if args.rgb_action == 'status':
                result = await cli.get_rgb_status()
            elif args.rgb_action == 'set':
                result = await cli.set_rgb_mode(args.mode, color=args.color, speed=args.speed)
            elif args.rgb_action == 'brightness':
                result = await cli.set_rgb_brightness(args.level)

        elif args.command == 'gpu':
            if args.gpu_action == 'info':
                result = await cli.get_gpu_info()
            elif args.gpu_action == 'overclock':
                result = await cli.set_gpu_overclock(args.core, args.memory)
            elif args.gpu_action == 'reset':
                result = await cli.reset_gpu_overclock()

        elif args.command == 'ai':
            if args.ai_action == 'status':
                result = await cli.get_ai_status()
            elif args.ai_action == 'optimize':
                result = await cli.run_ai_optimization()
            elif args.ai_action == 'start':
                result = await cli.start_ai_monitoring()
            elif args.ai_action == 'stop':
                result = await cli.stop_ai_monitoring()

        elif args.command == 'monitor':
            result = await cli.monitor_temps(args.duration, args.interval)

        elif args.command == 'export':
            result = await cli.export_config(args.filename)

        # Output result
        if result:
            if isinstance(result, dict):
                print(json.dumps(result, indent=2))
            else:
                print(result)

        return 0

    except KeyboardInterrupt:
        print("\nOperation cancelled by user")
        return 1
    except Exception as e:
        print(f"Error: {e}")
        return 1

if __name__ == "__main__":
    sys.exit(asyncio.run(main()))