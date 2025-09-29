#!/usr/bin/env python3
"""
Legion Toolkit Automation System - Linux Implementation
Complete automation framework with game detection, process monitoring, and scheduling
Provides feature parity with Windows AutoListeners system
"""

import asyncio
import os
import sys
import json
import psutil
import subprocess
from pathlib import Path
from typing import Dict, List, Optional, Callable, Any
from dataclasses import dataclass, asdict
from datetime import datetime, time, timedelta
import logging
import threading
import schedule
from abc import ABC, abstractmethod

# Import hardware controllers
try:
    from ..hardware.gpu_controller import LinuxGPUController
    from ..hardware.rgb_controller import LinuxRGBController
    from ..ai.ai_controller import LinuxAIController
except ImportError:
    sys.path.append(str(Path(__file__).parent.parent))
    from hardware.gpu_controller import LinuxGPUController
    from hardware.rgb_controller import LinuxRGBController
    from ai.ai_controller import LinuxAIController

@dataclass
class AutomationProfile:
    """Automation profile configuration"""
    name: str
    description: str
    performance_mode: str = "balanced"
    cpu_pl1: int = 45
    cpu_pl2: int = 115
    gpu_tgp: int = 115
    fan1_target: int = 50
    fan2_target: int = 50
    rgb_mode: str = "breathing"
    rgb_color: str = "#00FF00"
    rgb_brightness: int = 75
    ai_optimization: bool = True
    custom_settings: Dict[str, Any] = None

@dataclass
class AutomationTrigger:
    """Automation trigger configuration"""
    trigger_type: str  # "process", "game", "time", "wifi", "idle"
    condition: Dict[str, Any]
    profile: str
    enabled: bool = True
    priority: int = 100

class BaseAutoListener(ABC):
    """Base class for all automation listeners"""

    def __init__(self, name: str):
        self.name = name
        self.enabled = True
        self.triggers: List[AutomationTrigger] = []
        self.profiles: Dict[str, AutomationProfile] = {}
        self.callbacks: List[Callable] = []
        self.logger = logging.getLogger(f"legion.automation.{name}")

    @abstractmethod
    async def start_monitoring(self):
        """Start monitoring for automation triggers"""
        pass

    @abstractmethod
    async def stop_monitoring(self):
        """Stop monitoring"""
        pass

    @abstractmethod
    async def check_triggers(self) -> Optional[str]:
        """Check if any triggers are activated, return profile name if triggered"""
        pass

    def add_trigger(self, trigger: AutomationTrigger):
        """Add automation trigger"""
        self.triggers.append(trigger)
        self.logger.info(f"Added trigger: {trigger.trigger_type} -> {trigger.profile}")

    def add_profile(self, profile: AutomationProfile):
        """Add automation profile"""
        self.profiles[profile.name] = profile
        self.logger.info(f"Added profile: {profile.name}")

    def add_callback(self, callback: Callable):
        """Add callback for profile changes"""
        self.callbacks.append(callback)

    async def apply_profile(self, profile_name: str):
        """Apply automation profile"""
        if profile_name not in self.profiles:
            self.logger.error(f"Profile not found: {profile_name}")
            return False

        profile = self.profiles[profile_name]
        self.logger.info(f"Applying profile: {profile_name}")

        # Notify callbacks
        for callback in self.callbacks:
            try:
                await callback(profile)
            except Exception as e:
                self.logger.error(f"Callback error: {e}")

        return True

class GameAutoListener(BaseAutoListener):
    """Game detection and optimization listener"""

    def __init__(self):
        super().__init__("game")
        self.game_database = self._load_game_database()
        self.current_game = None
        self.monitoring_task = None

    def _load_game_database(self) -> Dict[str, Dict]:
        """Load game detection database"""
        return {
            # Steam games
            'steam.exe': {'type': 'launcher', 'profile': 'gaming_launcher'},
            'steamwebhelper.exe': {'type': 'helper', 'profile': None},

            # Epic Games
            'epicgameslauncher.exe': {'type': 'launcher', 'profile': 'gaming_launcher'},
            'unrealengine.exe': {'type': 'engine', 'profile': 'gaming_performance'},

            # Specific games - AAA titles
            'cyberpunk2077.exe': {
                'type': 'game',
                'profile': 'gaming_extreme',
                'name': 'Cyberpunk 2077',
                'requirements': {'cpu_intense': True, 'gpu_intense': True}
            },
            'control_dx12.exe': {
                'type': 'game',
                'profile': 'gaming_rtx',
                'name': 'Control',
                'requirements': {'rtx': True, 'dlss': True}
            },
            'witcher3.exe': {
                'type': 'game',
                'profile': 'gaming_performance',
                'name': 'The Witcher 3',
                'requirements': {'cpu_intense': True}
            },
            'valorant.exe': {
                'type': 'game',
                'profile': 'gaming_esports',
                'name': 'Valorant',
                'requirements': {'low_latency': True, 'high_fps': True}
            },
            'csgo.exe': {
                'type': 'game',
                'profile': 'gaming_esports',
                'name': 'CS:GO',
                'requirements': {'low_latency': True, 'high_fps': True}
            },
            'leagueoflegends.exe': {
                'type': 'game',
                'profile': 'gaming_moba',
                'name': 'League of Legends',
                'requirements': {'balanced': True}
            },
            'dota2.exe': {
                'type': 'game',
                'profile': 'gaming_moba',
                'name': 'Dota 2',
                'requirements': {'balanced': True}
            },
            'overwatch.exe': {
                'type': 'game',
                'profile': 'gaming_esports',
                'name': 'Overwatch',
                'requirements': {'high_fps': True}
            },

            # Creative applications
            'blender.exe': {
                'type': 'creative',
                'profile': 'workstation_gpu',
                'name': 'Blender',
                'requirements': {'gpu_compute': True}
            },
            'unity.exe': {
                'type': 'development',
                'profile': 'development_balanced',
                'name': 'Unity Editor',
                'requirements': {'cpu_intense': True, 'gpu_preview': True}
            },
            'ue4editor.exe': {
                'type': 'development',
                'profile': 'development_extreme',
                'name': 'Unreal Engine',
                'requirements': {'cpu_intense': True, 'gpu_intense': True}
            },

            # AI/ML applications
            'python.exe': {
                'type': 'development',
                'profile': 'ai_development',
                'name': 'Python',
                'requirements': {'variable': True}
            },
            'jupyter-notebook.exe': {
                'type': 'ai',
                'profile': 'ai_development',
                'name': 'Jupyter Notebook',
                'requirements': {'gpu_compute': True}
            },
            'code.exe': {
                'type': 'development',
                'profile': 'development_light',
                'name': 'VS Code',
                'requirements': {'light': True}
            }
        }

    async def start_monitoring(self):
        """Start game monitoring"""
        if self.monitoring_task:
            return

        self.enabled = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("Game monitoring started")

    async def stop_monitoring(self):
        """Stop game monitoring"""
        self.enabled = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("Game monitoring stopped")

    async def _monitoring_loop(self):
        """Main monitoring loop"""
        while self.enabled:
            try:
                await self.check_triggers()
                await asyncio.sleep(2)  # Check every 2 seconds
            except Exception as e:
                self.logger.error(f"Monitoring error: {e}")
                await asyncio.sleep(5)

    async def check_triggers(self) -> Optional[str]:
        """Check for running games and applications"""
        try:
            running_processes = []

            # Get running processes
            for proc in psutil.process_iter(['pid', 'name', 'cpu_percent', 'memory_percent']):
                try:
                    proc_info = proc.info
                    if proc_info['name']:
                        running_processes.append(proc_info)
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    continue

            # Check for known games/applications
            detected_games = []
            for proc in running_processes:
                proc_name = proc['name'].lower()
                if proc_name in self.game_database:
                    game_info = self.game_database[proc_name]
                    if game_info['profile']:
                        detected_games.append({
                            'process': proc_name,
                            'profile': game_info['profile'],
                            'type': game_info['type'],
                            'name': game_info.get('name', proc_name),
                            'cpu_usage': proc['cpu_percent'],
                            'memory_usage': proc['memory_percent']
                        })

            # Determine best profile
            if detected_games:
                # Priority: games > creative > development > launchers
                priority_order = ['game', 'creative', 'development', 'launcher']

                best_game = None
                for priority_type in priority_order:
                    for game in detected_games:
                        if game['type'] == priority_type:
                            best_game = game
                            break
                    if best_game:
                        break

                if best_game and self.current_game != best_game['process']:
                    self.current_game = best_game['process']
                    self.logger.info(f"Detected: {best_game['name']} - applying {best_game['profile']}")
                    await self.apply_profile(best_game['profile'])
                    return best_game['profile']

            elif self.current_game:
                # No games running, switch to default profile
                self.current_game = None
                self.logger.info("No games detected - applying default profile")
                await self.apply_profile('default')
                return 'default'

        except Exception as e:
            self.logger.error(f"Game detection error: {e}")

        return None

class ProcessAutoListener(BaseAutoListener):
    """Process-based automation listener"""

    def __init__(self):
        super().__init__("process")
        self.monitored_processes = {}
        self.monitoring_task = None

    def add_process_trigger(self, process_name: str, profile: str, cpu_threshold: float = 50.0):
        """Add process-based trigger"""
        trigger = AutomationTrigger(
            trigger_type="process",
            condition={
                "process_name": process_name,
                "cpu_threshold": cpu_threshold
            },
            profile=profile
        )
        self.add_trigger(trigger)

    async def start_monitoring(self):
        """Start process monitoring"""
        if self.monitoring_task:
            return

        self.enabled = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("Process monitoring started")

    async def stop_monitoring(self):
        """Stop process monitoring"""
        self.enabled = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("Process monitoring stopped")

    async def _monitoring_loop(self):
        """Main monitoring loop"""
        while self.enabled:
            try:
                await self.check_triggers()
                await asyncio.sleep(5)  # Check every 5 seconds
            except Exception as e:
                self.logger.error(f"Process monitoring error: {e}")
                await asyncio.sleep(10)

    async def check_triggers(self) -> Optional[str]:
        """Check process-based triggers"""
        try:
            for trigger in self.triggers:
                if not trigger.enabled or trigger.trigger_type != "process":
                    continue

                condition = trigger.condition
                process_name = condition["process_name"]
                cpu_threshold = condition.get("cpu_threshold", 50.0)

                # Check if process is running and above threshold
                for proc in psutil.process_iter(['name', 'cpu_percent']):
                    try:
                        if proc.info['name'].lower() == process_name.lower():
                            if proc.info['cpu_percent'] > cpu_threshold:
                                self.logger.info(f"Process trigger: {process_name} ({proc.info['cpu_percent']:.1f}% CPU)")
                                await self.apply_profile(trigger.profile)
                                return trigger.profile
                    except (psutil.NoSuchProcess, psutil.AccessDenied):
                        continue

        except Exception as e:
            self.logger.error(f"Process trigger check error: {e}")

        return None

class TimeAutoListener(BaseAutoListener):
    """Time-based automation listener"""

    def __init__(self):
        super().__init__("time")
        self.scheduler = schedule
        self.monitoring_task = None

    def add_time_trigger(self, time_str: str, profile: str, days: List[str] = None):
        """Add time-based trigger"""
        trigger = AutomationTrigger(
            trigger_type="time",
            condition={
                "time": time_str,
                "days": days or ["monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"]
            },
            profile=profile
        )
        self.add_trigger(trigger)

        # Schedule the trigger
        job = self.scheduler.every().day.at(time_str).do(self._time_trigger_callback, trigger)
        if days:
            # Clear default daily schedule and add specific days
            self.scheduler.cancel_job(job)
            for day in days:
                getattr(self.scheduler.every(), day.lower()).at(time_str).do(
                    self._time_trigger_callback, trigger
                )

    def _time_trigger_callback(self, trigger: AutomationTrigger):
        """Callback for time-based triggers"""
        asyncio.create_task(self.apply_profile(trigger.profile))

    async def start_monitoring(self):
        """Start time-based monitoring"""
        if self.monitoring_task:
            return

        self.enabled = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("Time monitoring started")

    async def stop_monitoring(self):
        """Stop time-based monitoring"""
        self.enabled = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("Time monitoring stopped")

    async def _monitoring_loop(self):
        """Main monitoring loop"""
        while self.enabled:
            try:
                self.scheduler.run_pending()
                await asyncio.sleep(60)  # Check every minute
            except Exception as e:
                self.logger.error(f"Time monitoring error: {e}")
                await asyncio.sleep(60)

    async def check_triggers(self) -> Optional[str]:
        """Check time-based triggers (handled by scheduler)"""
        return None

class WiFiAutoListener(BaseAutoListener):
    """WiFi-based automation listener"""

    def __init__(self):
        super().__init__("wifi")
        self.current_network = None
        self.monitoring_task = None

    def add_wifi_trigger(self, network_name: str, profile: str):
        """Add WiFi network-based trigger"""
        trigger = AutomationTrigger(
            trigger_type="wifi",
            condition={"network_name": network_name},
            profile=profile
        )
        self.add_trigger(trigger)

    async def start_monitoring(self):
        """Start WiFi monitoring"""
        if self.monitoring_task:
            return

        self.enabled = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("WiFi monitoring started")

    async def stop_monitoring(self):
        """Stop WiFi monitoring"""
        self.enabled = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("WiFi monitoring stopped")

    async def _monitoring_loop(self):
        """Main monitoring loop"""
        while self.enabled:
            try:
                await self.check_triggers()
                await asyncio.sleep(30)  # Check every 30 seconds
            except Exception as e:
                self.logger.error(f"WiFi monitoring error: {e}")
                await asyncio.sleep(30)

    async def check_triggers(self) -> Optional[str]:
        """Check WiFi network triggers"""
        try:
            # Get current WiFi network
            network = await self._get_current_wifi_network()

            if network != self.current_network:
                self.current_network = network

                if network:
                    for trigger in self.triggers:
                        if (trigger.enabled and
                            trigger.trigger_type == "wifi" and
                            trigger.condition["network_name"] == network):

                            self.logger.info(f"WiFi trigger: Connected to {network}")
                            await self.apply_profile(trigger.profile)
                            return trigger.profile

        except Exception as e:
            self.logger.error(f"WiFi trigger check error: {e}")

        return None

    async def _get_current_wifi_network(self) -> Optional[str]:
        """Get current WiFi network name"""
        try:
            # Try NetworkManager first (most common)
            result = subprocess.run(
                ['nmcli', '-t', '-f', 'active,ssid', 'dev', 'wifi'],
                capture_output=True, text=True, timeout=5
            )

            if result.returncode == 0:
                for line in result.stdout.strip().split('\n'):
                    if line.startswith('yes:'):
                        return line.split(':', 1)[1]

            # Fallback to iwgetid
            result = subprocess.run(['iwgetid', '-r'], capture_output=True, text=True, timeout=5)
            if result.returncode == 0:
                return result.stdout.strip()

        except (subprocess.TimeoutExpired, FileNotFoundError):
            pass

        return None

class UserInactivityAutoListener(BaseAutoListener):
    """User inactivity detection listener"""

    def __init__(self):
        super().__init__("inactivity")
        self.idle_threshold = 300  # 5 minutes default
        self.monitoring_task = None
        self.is_idle = False

    def set_idle_threshold(self, seconds: int):
        """Set idle threshold in seconds"""
        self.idle_threshold = seconds

    def add_idle_trigger(self, idle_profile: str, active_profile: str = "default"):
        """Add idle/active triggers"""
        idle_trigger = AutomationTrigger(
            trigger_type="idle",
            condition={"state": "idle"},
            profile=idle_profile
        )

        active_trigger = AutomationTrigger(
            trigger_type="idle",
            condition={"state": "active"},
            profile=active_profile
        )

        self.add_trigger(idle_trigger)
        self.add_trigger(active_trigger)

    async def start_monitoring(self):
        """Start inactivity monitoring"""
        if self.monitoring_task:
            return

        self.enabled = True
        self.monitoring_task = asyncio.create_task(self._monitoring_loop())
        self.logger.info("Inactivity monitoring started")

    async def stop_monitoring(self):
        """Stop inactivity monitoring"""
        self.enabled = False
        if self.monitoring_task:
            self.monitoring_task.cancel()
            try:
                await self.monitoring_task
            except asyncio.CancelledError:
                pass
        self.logger.info("Inactivity monitoring stopped")

    async def _monitoring_loop(self):
        """Main monitoring loop"""
        while self.enabled:
            try:
                await self.check_triggers()
                await asyncio.sleep(10)  # Check every 10 seconds
            except Exception as e:
                self.logger.error(f"Inactivity monitoring error: {e}")
                await asyncio.sleep(10)

    async def check_triggers(self) -> Optional[str]:
        """Check inactivity triggers"""
        try:
            idle_time = await self._get_idle_time()

            if idle_time >= self.idle_threshold and not self.is_idle:
                self.is_idle = True
                self.logger.info(f"User idle for {idle_time}s - applying idle profile")

                for trigger in self.triggers:
                    if (trigger.enabled and
                        trigger.trigger_type == "idle" and
                        trigger.condition["state"] == "idle"):
                        await self.apply_profile(trigger.profile)
                        return trigger.profile

            elif idle_time < self.idle_threshold and self.is_idle:
                self.is_idle = False
                self.logger.info("User active - applying active profile")

                for trigger in self.triggers:
                    if (trigger.enabled and
                        trigger.trigger_type == "idle" and
                        trigger.condition["state"] == "active"):
                        await self.apply_profile(trigger.profile)
                        return trigger.profile

        except Exception as e:
            self.logger.error(f"Inactivity check error: {e}")

        return None

    async def _get_idle_time(self) -> int:
        """Get user idle time in seconds"""
        try:
            # Try xprintidle first (X11)
            result = subprocess.run(['xprintidle'], capture_output=True, text=True, timeout=5)
            if result.returncode == 0:
                return int(result.stdout.strip()) // 1000  # Convert ms to seconds

            # Fallback for Wayland - check last input device activity
            try:
                import glob
                input_devices = glob.glob('/sys/class/input/*/uevent')
                if input_devices:
                    # This is a simplified approach for Wayland
                    # In practice, you'd need to use specific Wayland protocols
                    return 0  # Assume active if we can't detect
            except:
                pass

            return 0  # Default to active

        except (subprocess.TimeoutExpired, FileNotFoundError, ValueError):
            return 0

class AutomationManager:
    """Main automation management system"""

    def __init__(self):
        self.listeners: Dict[str, BaseAutoListener] = {}
        self.hardware_controllers = {}
        self.logger = logging.getLogger("legion.automation.manager")
        self.config_file = Path.home() / '.config' / 'legion-toolkit' / 'automation.json'

        # Initialize listeners
        self.listeners["game"] = GameAutoListener()
        self.listeners["process"] = ProcessAutoListener()
        self.listeners["time"] = TimeAutoListener()
        self.listeners["wifi"] = WiFiAutoListener()
        self.listeners["inactivity"] = UserInactivityAutoListener()

        # Load default profiles
        self._load_default_profiles()

    async def initialize(self):
        """Initialize automation system"""
        try:
            # Initialize hardware controllers
            self.hardware_controllers["gpu"] = LinuxGPUController()
            self.hardware_controllers["rgb"] = LinuxRGBController()
            self.hardware_controllers["ai"] = LinuxAIController()

            await self.hardware_controllers["gpu"].initialize()
            await self.hardware_controllers["rgb"].initialize()
            await self.hardware_controllers["ai"].initialize()

            # Register callbacks for all listeners
            for listener in self.listeners.values():
                listener.add_callback(self._apply_hardware_profile)

            # Load saved configuration
            await self.load_configuration()

            self.logger.info("Automation system initialized")
            return True

        except Exception as e:
            self.logger.error(f"Automation initialization failed: {e}")
            return False

    async def start_all_listeners(self):
        """Start all automation listeners"""
        for name, listener in self.listeners.items():
            try:
                await listener.start_monitoring()
                self.logger.info(f"Started {name} listener")
            except Exception as e:
                self.logger.error(f"Failed to start {name} listener: {e}")

    async def stop_all_listeners(self):
        """Stop all automation listeners"""
        for name, listener in self.listeners.items():
            try:
                await listener.stop_monitoring()
                self.logger.info(f"Stopped {name} listener")
            except Exception as e:
                self.logger.error(f"Failed to stop {name} listener: {e}")

    async def _apply_hardware_profile(self, profile: AutomationProfile):
        """Apply profile to hardware controllers"""
        try:
            # Apply performance settings via kernel module
            await self._write_kernel_param("performance_mode", profile.performance_mode)
            await self._write_kernel_param("cpu_pl1", str(profile.cpu_pl1))
            await self._write_kernel_param("cpu_pl2", str(profile.cpu_pl2))
            await self._write_kernel_param("gpu_tgp", str(profile.gpu_tgp))
            await self._write_kernel_param("fan1_target", str(profile.fan1_target))
            await self._write_kernel_param("fan2_target", str(profile.fan2_target))

            # Apply RGB settings
            rgb_controller = self.hardware_controllers.get("rgb")
            if rgb_controller:
                await rgb_controller.set_mode(profile.rgb_mode)
                await rgb_controller.set_brightness(profile.rgb_brightness)
                if profile.rgb_mode in ["static", "breathing"]:
                    await rgb_controller.set_static_color(profile.rgb_color)

            # Apply AI optimization
            ai_controller = self.hardware_controllers.get("ai")
            if ai_controller and profile.ai_optimization:
                if not ai_controller.monitoring_active:
                    await ai_controller.start_monitoring()

            # Apply custom settings
            if profile.custom_settings:
                for key, value in profile.custom_settings.items():
                    await self._write_kernel_param(key, str(value))

            self.logger.info(f"Applied profile: {profile.name}")

        except Exception as e:
            self.logger.error(f"Failed to apply profile {profile.name}: {e}")

    async def _write_kernel_param(self, param: str, value: str):
        """Write parameter to kernel module"""
        try:
            path = f"/sys/kernel/legion_laptop/{param}"
            subprocess.run(['sudo', 'sh', '-c', f'echo {value} > {path}'],
                          check=True, timeout=5)
        except (subprocess.CalledProcessError, subprocess.TimeoutExpired) as e:
            self.logger.error(f"Failed to write {param}: {e}")

    def _load_default_profiles(self):
        """Load default automation profiles"""
        default_profiles = {
            "default": AutomationProfile(
                name="default",
                description="Default balanced profile",
                performance_mode="balanced",
                cpu_pl1=45, cpu_pl2=115, gpu_tgp=115,
                fan1_target=50, fan2_target=50,
                rgb_mode="breathing", rgb_color="#00FF00", rgb_brightness=75,
                ai_optimization=True
            ),
            "gaming_performance": AutomationProfile(
                name="gaming_performance",
                description="High performance gaming",
                performance_mode="performance",
                cpu_pl1=55, cpu_pl2=140, gpu_tgp=140,
                fan1_target=80, fan2_target=80,
                rgb_mode="wave", rgb_color="#FF0000", rgb_brightness=100,
                ai_optimization=True
            ),
            "gaming_extreme": AutomationProfile(
                name="gaming_extreme",
                description="Maximum performance for demanding games",
                performance_mode="custom",
                cpu_pl1=55, cpu_pl2=140, gpu_tgp=140,
                fan1_target=100, fan2_target=100,
                rgb_mode="rainbow", rgb_brightness=100,
                ai_optimization=True
            ),
            "gaming_esports": AutomationProfile(
                name="gaming_esports",
                description="Optimized for competitive gaming",
                performance_mode="performance",
                cpu_pl1=50, cpu_pl2=125, gpu_tgp=120,
                fan1_target=70, fan2_target=70,
                rgb_mode="static", rgb_color="#00FFFF", rgb_brightness=90,
                ai_optimization=True
            ),
            "quiet": AutomationProfile(
                name="quiet",
                description="Silent operation",
                performance_mode="quiet",
                cpu_pl1=35, cpu_pl2=90, gpu_tgp=80,
                fan1_target=30, fan2_target=30,
                rgb_mode="breathing", rgb_color="#0000FF", rgb_brightness=50,
                ai_optimization=False
            ),
            "workstation_gpu": AutomationProfile(
                name="workstation_gpu",
                description="GPU-intensive workstation tasks",
                performance_mode="performance",
                cpu_pl1=45, cpu_pl2=115, gpu_tgp=140,
                fan1_target=60, fan2_target=80,
                rgb_mode="static", rgb_color="#FF8000", rgb_brightness=80,
                ai_optimization=True
            ),
            "ai_development": AutomationProfile(
                name="ai_development",
                description="AI/ML development and training",
                performance_mode="custom",
                cpu_pl1=50, cpu_pl2=120, gpu_tgp=140,
                fan1_target=70, fan2_target=90,
                rgb_mode="breathing", rgb_color="#8000FF", rgb_brightness=85,
                ai_optimization=True
            )
        }

        # Add profiles to all listeners
        for listener in self.listeners.values():
            for profile in default_profiles.values():
                listener.add_profile(profile)

    async def save_configuration(self):
        """Save automation configuration"""
        try:
            config = {
                "listeners": {},
                "profiles": {}
            }

            # Save listener configurations
            for name, listener in self.listeners.items():
                config["listeners"][name] = {
                    "enabled": listener.enabled,
                    "triggers": [asdict(trigger) for trigger in listener.triggers]
                }

            # Save profiles
            for listener in self.listeners.values():
                for profile_name, profile in listener.profiles.items():
                    config["profiles"][profile_name] = asdict(profile)

            # Ensure config directory exists
            self.config_file.parent.mkdir(parents=True, exist_ok=True)

            # Write configuration
            with open(self.config_file, 'w') as f:
                json.dump(config, f, indent=2, default=str)

            self.logger.info("Configuration saved")

        except Exception as e:
            self.logger.error(f"Failed to save configuration: {e}")

    async def load_configuration(self):
        """Load automation configuration"""
        try:
            if not self.config_file.exists():
                self.logger.info("No saved configuration found, using defaults")
                return

            with open(self.config_file, 'r') as f:
                config = json.load(f)

            # Load listener configurations
            if "listeners" in config:
                for name, listener_config in config["listeners"].items():
                    if name in self.listeners:
                        listener = self.listeners[name]
                        listener.enabled = listener_config.get("enabled", True)

                        # Load triggers
                        for trigger_data in listener_config.get("triggers", []):
                            trigger = AutomationTrigger(**trigger_data)
                            listener.add_trigger(trigger)

            # Load profiles
            if "profiles" in config:
                for profile_name, profile_data in config["profiles"].items():
                    profile = AutomationProfile(**profile_data)
                    for listener in self.listeners.values():
                        listener.add_profile(profile)

            self.logger.info("Configuration loaded")

        except Exception as e:
            self.logger.error(f"Failed to load configuration: {e}")

# Example usage and testing
async def main():
    """Example usage of automation system"""
    logging.basicConfig(level=logging.INFO)

    # Create and initialize automation manager
    automation = AutomationManager()
    await automation.initialize()

    # Add some example triggers
    game_listener = automation.listeners["game"]

    # Time-based triggers
    time_listener = automation.listeners["time"]
    time_listener.add_time_trigger("09:00", "default")  # Work hours
    time_listener.add_time_trigger("18:00", "gaming_performance")  # Gaming time
    time_listener.add_time_trigger("23:00", "quiet")  # Night mode

    # WiFi-based triggers
    wifi_listener = automation.listeners["wifi"]
    wifi_listener.add_wifi_trigger("HomeNetwork", "gaming_performance")
    wifi_listener.add_wifi_trigger("OfficeNetwork", "quiet")

    # Inactivity triggers
    idle_listener = automation.listeners["inactivity"]
    idle_listener.set_idle_threshold(300)  # 5 minutes
    idle_listener.add_idle_trigger("quiet", "default")

    # Start all listeners
    await automation.start_all_listeners()

    print("Automation system running. Press Ctrl+C to stop.")

    try:
        # Run for demonstration
        await asyncio.sleep(60)
    except KeyboardInterrupt:
        print("Stopping automation system...")
    finally:
        await automation.stop_all_listeners()
        await automation.save_configuration()

if __name__ == "__main__":
    asyncio.run(main())