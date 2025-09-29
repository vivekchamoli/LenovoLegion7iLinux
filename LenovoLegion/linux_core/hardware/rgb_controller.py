#!/usr/bin/env python3
"""
Advanced RGB Controller for Linux - Complete Spectrum RGB Implementation
Provides feature parity with Windows RGBKeyboardBacklightController.cs and SpectrumKeyboardBacklightController.cs
Supports Legion Slim 7i Gen 9 Spectrum RGB keyboard with 4-zone control
"""

import os
import time
import math
import asyncio
import colorsys
from typing import Dict, List, Tuple, Optional, Union
from dataclasses import dataclass
from enum import Enum
import logging
from pathlib import Path

class RGBMode(Enum):
    """RGB lighting modes"""
    OFF = "off"
    STATIC = "static"
    BREATHING = "breathing"
    RAINBOW = "rainbow"
    WAVE = "wave"
    RIPPLE = "ripple"
    REACTIVE = "reactive"
    SPECTRUM_CYCLE = "spectrum_cycle"
    CUSTOM = "custom"

class RGBZone(Enum):
    """RGB keyboard zones"""
    ZONE_1 = 0  # Left side
    ZONE_2 = 1  # Center-left
    ZONE_3 = 2  # Center-right
    ZONE_4 = 3  # Right side
    ALL_ZONES = 255

@dataclass
class RGBColor:
    """RGB color representation"""
    red: int
    green: int
    blue: int

    def __post_init__(self):
        # Ensure values are in valid range
        self.red = max(0, min(255, self.red))
        self.green = max(0, min(255, self.green))
        self.blue = max(0, min(255, self.blue))

    def to_hex(self) -> str:
        """Convert to hex string"""
        return f"#{self.red:02x}{self.green:02x}{self.blue:02x}"

    def to_hsv(self) -> Tuple[float, float, float]:
        """Convert to HSV"""
        return colorsys.rgb_to_hsv(self.red/255, self.green/255, self.blue/255)

    @classmethod
    def from_hex(cls, hex_color: str) -> 'RGBColor':
        """Create from hex string"""
        hex_color = hex_color.lstrip('#')
        return cls(
            int(hex_color[0:2], 16),
            int(hex_color[2:4], 16),
            int(hex_color[4:6], 16)
        )

    @classmethod
    def from_hsv(cls, h: float, s: float, v: float) -> 'RGBColor':
        """Create from HSV values"""
        r, g, b = colorsys.hsv_to_rgb(h, s, v)
        return cls(int(r * 255), int(g * 255), int(b * 255))

@dataclass
class RGBEffect:
    """RGB effect configuration"""
    mode: RGBMode
    colors: List[RGBColor]
    speed: int  # 1-100
    brightness: int  # 1-100
    direction: str  # 'left_to_right', 'right_to_left', 'center_out', 'outside_in'
    zones: List[RGBZone]

@dataclass
class SpectrumProfile:
    """Complete RGB profile for Spectrum keyboard"""
    name: str
    effects: List[RGBEffect]
    global_brightness: int
    sync_enabled: bool
    description: str

class LinuxRGBController:
    """
    Advanced RGB controller providing complete feature parity with Windows
    Supports Legion Spectrum RGB keyboard with 4-zone control
    """

    def __init__(self):
        self.logger = logging.getLogger(__name__)
        self.kernel_module_path = "/sys/kernel/legion_laptop"
        self.current_profile = None
        self.brightness = 75
        self.is_enabled = True

        # Predefined color palettes
        self.color_palettes = {
            'legion_orange': [RGBColor(255, 105, 0), RGBColor(255, 140, 0)],
            'gaming_red': [RGBColor(255, 0, 0), RGBColor(255, 50, 50)],
            'cool_blue': [RGBColor(0, 100, 255), RGBColor(100, 150, 255)],
            'matrix_green': [RGBColor(0, 255, 0), RGBColor(50, 255, 100)],
            'purple_haze': [RGBColor(128, 0, 128), RGBColor(200, 100, 255)],
            'fire': [RGBColor(255, 0, 0), RGBColor(255, 100, 0), RGBColor(255, 200, 0)],
            'ocean': [RGBColor(0, 150, 255), RGBColor(0, 255, 200), RGBColor(100, 255, 255)],
            'rainbow': [
                RGBColor(255, 0, 0), RGBColor(255, 127, 0), RGBColor(255, 255, 0),
                RGBColor(0, 255, 0), RGBColor(0, 0, 255), RGBColor(75, 0, 130),
                RGBColor(148, 0, 211)
            ]
        }

        # Animation state
        self.animation_running = False
        self.animation_task = None

    async def initialize(self) -> bool:
        """Initialize RGB controller"""
        try:
            # Check if kernel module supports RGB control
            rgb_mode_path = Path(self.kernel_module_path) / "rgb_mode"
            if not rgb_mode_path.exists():
                self.logger.error("RGB control not available in kernel module")
                return False

            # Initialize to a known state
            await self.set_mode(RGBMode.STATIC)
            await self.set_brightness(self.brightness)

            self.logger.info("RGB controller initialized successfully")
            return True

        except Exception as e:
            self.logger.error(f"RGB controller initialization failed: {e}")
            return False

    async def set_mode(self, mode: RGBMode) -> bool:
        """Set RGB lighting mode"""
        try:
            mode_path = Path(self.kernel_module_path) / "rgb_mode"
            with open(mode_path, 'w') as f:
                f.write(mode.value)

            self.logger.info(f"RGB mode set to: {mode.value}")
            return True

        except Exception as e:
            self.logger.error(f"Failed to set RGB mode: {e}")
            return False

    async def set_brightness(self, brightness: int) -> bool:
        """Set global RGB brightness (0-100)"""
        if not 0 <= brightness <= 100:
            raise ValueError("Brightness must be between 0 and 100")

        try:
            brightness_path = Path(self.kernel_module_path) / "rgb_brightness"
            with open(brightness_path, 'w') as f:
                f.write(str(brightness))

            self.brightness = brightness
            self.logger.info(f"RGB brightness set to: {brightness}%")
            return True

        except Exception as e:
            self.logger.error(f"Failed to set RGB brightness: {e}")
            return False

    async def set_zone_color(self, zone: RGBZone, color: RGBColor) -> bool:
        """Set color for specific zone"""
        try:
            # Map zone to register
            zone_registers = {
                RGBZone.ZONE_1: "rgb_color_1",
                RGBZone.ZONE_2: "rgb_color_2",
                RGBZone.ZONE_3: "rgb_color_3",
                RGBZone.ZONE_4: "rgb_color_4"
            }

            if zone == RGBZone.ALL_ZONES:
                # Set all zones to the same color
                for zone_reg in zone_registers.values():
                    await self._write_color_register(zone_reg, color)
            else:
                zone_reg = zone_registers.get(zone)
                if zone_reg:
                    await self._write_color_register(zone_reg, color)

            return True

        except Exception as e:
            self.logger.error(f"Failed to set zone color: {e}")
            return False

    async def _write_color_register(self, register: str, color: RGBColor) -> None:
        """Write color to register via kernel module"""
        color_path = Path(self.kernel_module_path) / register
        # Write RGB values as space-separated string
        color_value = f"{color.red} {color.green} {color.blue}"
        with open(color_path, 'w') as f:
            f.write(color_value)

    async def set_static_color(self, color: RGBColor, zones: List[RGBZone] = None) -> bool:
        """Set static color for specified zones"""
        if zones is None:
            zones = [RGBZone.ALL_ZONES]

        try:
            await self.set_mode(RGBMode.STATIC)

            for zone in zones:
                await self.set_zone_color(zone, color)

            self.logger.info(f"Static color set to: {color.to_hex()}")
            return True

        except Exception as e:
            self.logger.error(f"Failed to set static color: {e}")
            return False

    async def start_breathing_effect(self, color: RGBColor, speed: int = 50) -> bool:
        """Start breathing effect"""
        try:
            await self.set_mode(RGBMode.BREATHING)
            await self.set_zone_color(RGBZone.ALL_ZONES, color)

            # Set breathing speed via kernel module
            speed_path = Path(self.kernel_module_path) / "rgb_speed"
            with open(speed_path, 'w') as f:
                f.write(str(speed))

            self.logger.info(f"Breathing effect started with color: {color.to_hex()}")
            return True

        except Exception as e:
            self.logger.error(f"Failed to start breathing effect: {e}")
            return False

    async def start_rainbow_effect(self, speed: int = 30) -> bool:
        """Start rainbow cycling effect"""
        try:
            await self.set_mode(RGBMode.RAINBOW)

            # Set rainbow speed
            speed_path = Path(self.kernel_module_path) / "rgb_speed"
            with open(speed_path, 'w') as f:
                f.write(str(speed))

            self.logger.info("Rainbow effect started")
            return True

        except Exception as e:
            self.logger.error(f"Failed to start rainbow effect: {e}")
            return False

    async def start_wave_effect(self, colors: List[RGBColor], speed: int = 40, direction: str = 'left_to_right') -> bool:
        """Start wave effect with custom colors"""
        try:
            await self.set_mode(RGBMode.WAVE)

            # Set wave colors for each zone
            for i, color in enumerate(colors[:4]):  # Max 4 zones
                zone = RGBZone(i)
                await self.set_zone_color(zone, color)

            # Set wave speed and direction
            speed_path = Path(self.kernel_module_path) / "rgb_speed"
            with open(speed_path, 'w') as f:
                f.write(str(speed))

            self.logger.info(f"Wave effect started with {len(colors)} colors")
            return True

        except Exception as e:
            self.logger.error(f"Failed to start wave effect: {e}")
            return False

    async def start_custom_animation(self, effect: RGBEffect) -> bool:
        """Start custom animation effect"""
        try:
            # Stop any existing animation
            if self.animation_task:
                self.animation_task.cancel()

            self.animation_running = True
            self.animation_task = asyncio.create_task(self._run_custom_animation(effect))

            self.logger.info(f"Custom animation started: {effect.mode.value}")
            return True

        except Exception as e:
            self.logger.error(f"Failed to start custom animation: {e}")
            return False

    async def _run_custom_animation(self, effect: RGBEffect) -> None:
        """Run custom animation loop"""
        frame = 0
        while self.animation_running:
            try:
                if effect.mode == RGBMode.SPECTRUM_CYCLE:
                    await self._animate_spectrum_cycle(frame, effect)
                elif effect.mode == RGBMode.RIPPLE:
                    await self._animate_ripple(frame, effect)
                elif effect.mode == RGBMode.REACTIVE:
                    await self._animate_reactive(frame, effect)

                frame += 1
                # Speed control: higher speed = shorter delay
                delay = 0.1 + (1.0 - effect.speed / 100) * 0.5
                await asyncio.sleep(delay)

            except asyncio.CancelledError:
                break
            except Exception as e:
                self.logger.error(f"Animation error: {e}")
                break

    async def _animate_spectrum_cycle(self, frame: int, effect: RGBEffect) -> None:
        """Animate spectrum cycling through all colors"""
        hue = (frame * 2) % 360  # 2 degrees per frame
        color = RGBColor.from_hsv(hue / 360, 1.0, effect.brightness / 100)

        for zone in effect.zones:
            await self.set_zone_color(zone, color)

    async def _animate_ripple(self, frame: int, effect: RGBEffect) -> None:
        """Animate ripple effect from center outward"""
        center_zones = [RGBZone.ZONE_2, RGBZone.ZONE_3]
        outer_zones = [RGBZone.ZONE_1, RGBZone.ZONE_4]

        # Ripple timing
        ripple_phase = (frame % 60) / 60  # 60-frame cycle

        if ripple_phase < 0.3:
            # Center zones bright
            bright_color = effect.colors[0] if effect.colors else RGBColor(255, 255, 255)
            dim_color = RGBColor(
                int(bright_color.red * 0.2),
                int(bright_color.green * 0.2),
                int(bright_color.blue * 0.2)
            )

            for zone in center_zones:
                await self.set_zone_color(zone, bright_color)
            for zone in outer_zones:
                await self.set_zone_color(zone, dim_color)
        else:
            # Outer zones bright
            bright_color = effect.colors[1] if len(effect.colors) > 1 else RGBColor(255, 0, 0)
            dim_color = RGBColor(
                int(bright_color.red * 0.2),
                int(bright_color.green * 0.2),
                int(bright_color.blue * 0.2)
            )

            for zone in outer_zones:
                await self.set_zone_color(zone, bright_color)
            for zone in center_zones:
                await self.set_zone_color(zone, dim_color)

    async def _animate_reactive(self, frame: int, effect: RGBEffect) -> None:
        """Animate reactive lighting (simulated key presses)"""
        # Simulate random key presses for demonstration
        if frame % 30 == 0:  # Every 30 frames
            import random
            zone = RGBZone(random.randint(0, 3))
            color = effect.colors[0] if effect.colors else RGBColor(255, 255, 255)

            # Flash the zone
            await self.set_zone_color(zone, color)
            await asyncio.sleep(0.1)

            # Fade back to dim
            dim_color = RGBColor(
                int(color.red * 0.1),
                int(color.green * 0.1),
                int(color.blue * 0.1)
            )
            await self.set_zone_color(zone, dim_color)

    async def stop_animation(self) -> bool:
        """Stop current animation"""
        try:
            self.animation_running = False
            if self.animation_task:
                self.animation_task.cancel()
                try:
                    await self.animation_task
                except asyncio.CancelledError:
                    pass
                self.animation_task = None

            self.logger.info("Animation stopped")
            return True

        except Exception as e:
            self.logger.error(f"Failed to stop animation: {e}")
            return False

    async def create_gaming_profile(self, game_name: str, primary_color: RGBColor) -> SpectrumProfile:
        """Create gaming-optimized RGB profile"""
        effects = [
            RGBEffect(
                mode=RGBMode.STATIC,
                colors=[primary_color],
                speed=50,
                brightness=90,
                direction='center_out',
                zones=[RGBZone.ALL_ZONES]
            ),
            RGBEffect(
                mode=RGBMode.REACTIVE,
                colors=[primary_color, RGBColor(255, 255, 255)],
                speed=80,
                brightness=100,
                direction='center_out',
                zones=[RGBZone.ALL_ZONES]
            )
        ]

        return SpectrumProfile(
            name=f"{game_name}_gaming",
            effects=effects,
            global_brightness=90,
            sync_enabled=True,
            description=f"Optimized RGB profile for {game_name}"
        )

    async def create_productivity_profile(self) -> SpectrumProfile:
        """Create productivity-focused RGB profile"""
        soft_white = RGBColor(200, 200, 180)  # Warm white
        accent_blue = RGBColor(100, 150, 255)  # Soft blue

        effects = [
            RGBEffect(
                mode=RGBMode.STATIC,
                colors=[soft_white],
                speed=50,
                brightness=40,
                direction='left_to_right',
                zones=[RGBZone.ALL_ZONES]
            ),
            RGBEffect(
                mode=RGBMode.BREATHING,
                colors=[accent_blue],
                speed=20,
                brightness=60,
                direction='center_out',
                zones=[RGBZone.ZONE_2, RGBZone.ZONE_3]
            )
        ]

        return SpectrumProfile(
            name="productivity",
            effects=effects,
            global_brightness=50,
            sync_enabled=False,
            description="Low-brightness profile for productive work"
        )

    async def create_legion_signature_profile(self) -> SpectrumProfile:
        """Create Legion signature RGB profile"""
        legion_orange = RGBColor(255, 105, 0)
        legion_red = RGBColor(255, 50, 0)

        effects = [
            RGBEffect(
                mode=RGBMode.WAVE,
                colors=[legion_orange, legion_red],
                speed=40,
                brightness=85,
                direction='left_to_right',
                zones=[RGBZone.ALL_ZONES]
            ),
            RGBEffect(
                mode=RGBMode.BREATHING,
                colors=[legion_orange],
                speed=30,
                brightness=90,
                direction='center_out',
                zones=[RGBZone.ALL_ZONES]
            )
        ]

        return SpectrumProfile(
            name="legion_signature",
            effects=effects,
            global_brightness=85,
            sync_enabled=True,
            description="Official Legion RGB signature look"
        )

    async def apply_profile(self, profile: SpectrumProfile) -> bool:
        """Apply RGB profile to keyboard"""
        try:
            # Stop any current animation
            await self.stop_animation()

            # Set global brightness
            await self.set_brightness(profile.global_brightness)

            # Apply primary effect
            if profile.effects:
                primary_effect = profile.effects[0]

                if primary_effect.mode == RGBMode.STATIC:
                    color = primary_effect.colors[0] if primary_effect.colors else RGBColor(255, 255, 255)
                    await self.set_static_color(color)
                elif primary_effect.mode == RGBMode.BREATHING:
                    color = primary_effect.colors[0] if primary_effect.colors else RGBColor(255, 0, 0)
                    await self.start_breathing_effect(color, primary_effect.speed)
                elif primary_effect.mode == RGBMode.RAINBOW:
                    await self.start_rainbow_effect(primary_effect.speed)
                elif primary_effect.mode == RGBMode.WAVE:
                    await self.start_wave_effect(primary_effect.colors, primary_effect.speed)
                else:
                    await self.start_custom_animation(primary_effect)

            self.current_profile = profile
            self.logger.info(f"Applied RGB profile: {profile.name}")
            return True

        except Exception as e:
            self.logger.error(f"Failed to apply RGB profile: {e}")
            return False

    async def get_available_modes(self) -> List[str]:
        """Get list of available RGB modes"""
        return [mode.value for mode in RGBMode]

    async def get_color_palettes(self) -> Dict[str, List[RGBColor]]:
        """Get available color palettes"""
        return self.color_palettes

    async def save_profile(self, profile: SpectrumProfile, filename: str) -> bool:
        """Save RGB profile to file"""
        try:
            import json

            profile_data = {
                'name': profile.name,
                'description': profile.description,
                'global_brightness': profile.global_brightness,
                'sync_enabled': profile.sync_enabled,
                'effects': []
            }

            for effect in profile.effects:
                effect_data = {
                    'mode': effect.mode.value,
                    'colors': [{'r': c.red, 'g': c.green, 'b': c.blue} for c in effect.colors],
                    'speed': effect.speed,
                    'brightness': effect.brightness,
                    'direction': effect.direction,
                    'zones': [zone.value for zone in effect.zones]
                }
                profile_data['effects'].append(effect_data)

            profiles_dir = Path.home() / '.config' / 'legion-toolkit' / 'rgb-profiles'
            profiles_dir.mkdir(parents=True, exist_ok=True)

            profile_file = profiles_dir / f"{filename}.json"
            with open(profile_file, 'w') as f:
                json.dump(profile_data, f, indent=2)

            self.logger.info(f"RGB profile saved: {filename}")
            return True

        except Exception as e:
            self.logger.error(f"Failed to save RGB profile: {e}")
            return False

    async def load_profile(self, filename: str) -> Optional[SpectrumProfile]:
        """Load RGB profile from file"""
        try:
            import json

            profiles_dir = Path.home() / '.config' / 'legion-toolkit' / 'rgb-profiles'
            profile_file = profiles_dir / f"{filename}.json"

            if not profile_file.exists():
                self.logger.error(f"Profile file not found: {filename}")
                return None

            with open(profile_file, 'r') as f:
                profile_data = json.load(f)

            effects = []
            for effect_data in profile_data['effects']:
                colors = [RGBColor(c['r'], c['g'], c['b']) for c in effect_data['colors']]
                zones = [RGBZone(z) for z in effect_data['zones']]

                effect = RGBEffect(
                    mode=RGBMode(effect_data['mode']),
                    colors=colors,
                    speed=effect_data['speed'],
                    brightness=effect_data['brightness'],
                    direction=effect_data['direction'],
                    zones=zones
                )
                effects.append(effect)

            profile = SpectrumProfile(
                name=profile_data['name'],
                effects=effects,
                global_brightness=profile_data['global_brightness'],
                sync_enabled=profile_data['sync_enabled'],
                description=profile_data['description']
            )

            self.logger.info(f"RGB profile loaded: {filename}")
            return profile

        except Exception as e:
            self.logger.error(f"Failed to load RGB profile: {e}")
            return None

    async def turn_off(self) -> bool:
        """Turn off RGB lighting"""
        try:
            await self.stop_animation()
            await self.set_mode(RGBMode.OFF)
            self.is_enabled = False
            self.logger.info("RGB lighting turned off")
            return True

        except Exception as e:
            self.logger.error(f"Failed to turn off RGB: {e}")
            return False

    async def turn_on(self) -> bool:
        """Turn on RGB lighting"""
        try:
            self.is_enabled = True
            if self.current_profile:
                await self.apply_profile(self.current_profile)
            else:
                await self.set_static_color(RGBColor(255, 105, 0))  # Legion orange
            self.logger.info("RGB lighting turned on")
            return True

        except Exception as e:
            self.logger.error(f"Failed to turn on RGB: {e}")
            return False

    def get_current_profile(self) -> Optional[SpectrumProfile]:
        """Get currently applied profile"""
        return self.current_profile

    def is_animation_running(self) -> bool:
        """Check if animation is currently running"""
        return self.animation_running

# Example usage and testing
async def main():
    """Example usage of RGB controller"""
    controller = LinuxRGBController()

    print("Initializing RGB controller...")
    if not await controller.initialize():
        print("Failed to initialize RGB controller")
        return

    print("Available RGB modes:")
    modes = await controller.get_available_modes()
    for mode in modes:
        print(f"  - {mode}")

    print("\nSetting static Legion orange...")
    legion_orange = RGBColor(255, 105, 0)
    await controller.set_static_color(legion_orange)
    await asyncio.sleep(2)

    print("Starting breathing effect...")
    await controller.start_breathing_effect(RGBColor(0, 255, 0))
    await asyncio.sleep(3)

    print("Starting rainbow effect...")
    await controller.start_rainbow_effect()
    await asyncio.sleep(5)

    print("Creating and applying Legion signature profile...")
    legion_profile = await controller.create_legion_signature_profile()
    await controller.apply_profile(legion_profile)
    await asyncio.sleep(5)

    print("RGB demonstration complete")

if __name__ == "__main__":
    asyncio.run(main())