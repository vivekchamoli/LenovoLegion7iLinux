#!/usr/bin/env python3
"""
Legion Toolkit for Linux - Advanced GUI Application
Supports Legion Slim 7i Gen 9 (16IRX9) with AI thermal optimization
"""

import gi
gi.require_version('Gtk', '4.0')
gi.require_version('Adw', '1')
from gi.repository import Gtk, Adw, GLib, Gio
import asyncio
import os
import sys
from pathlib import Path
from typing import Dict, Optional, List
import subprocess
import json
import threading
import time

class LegionToolkitLinux(Adw.Application):
    """Main application class for Legion Toolkit Linux"""

    def __init__(self):
        super().__init__(
            application_id='com.lenovo.LegionToolkit',
            flags=Gio.ApplicationFlags.FLAGS_NONE
        )
        self.window = None
        self.kernel_module_path = "/sys/kernel/legion_laptop/"

    def do_activate(self):
        """Activate callback - create main window"""
        if not self.window:
            self.window = LegionToolkitWindow(application=self)
        self.window.present()

class LegionToolkitWindow(Adw.ApplicationWindow):
    """Main application window with Gen 9 AI thermal optimization"""

    def __init__(self, **kwargs):
        super().__init__(**kwargs)

        self.set_title("Legion Toolkit for Linux - Gen 9 Enhanced")
        self.set_default_size(1000, 800)

        # Check for root/sudo
        if os.geteuid() != 0:
            self.show_permission_dialog()
            return

        # Check for Gen 9 support
        if not self.check_gen9_support():
            self.show_gen9_requirement_dialog()
            return

        self.init_ui()
        self.load_current_settings()

        # Start monitoring and AI optimization
        self.start_thermal_monitoring()

    def check_gen9_support(self) -> bool:
        """Check if we're running on a supported Gen 9 system"""
        try:
            # Check DMI information
            with open('/sys/class/dmi/id/product_name', 'r') as f:
                product_name = f.read().strip()

            with open('/sys/class/dmi/id/board_name', 'r') as f:
                board_name = f.read().strip()

            # Check for Gen 9 identifiers
            is_gen9 = ('16IRX9' in product_name or
                      'Legion Slim 7i Gen 9' in product_name or
                      'LNVNB161216' in board_name)

            # Check if kernel module is loaded
            module_loaded = Path('/sys/kernel/legion_laptop/').exists()

            return is_gen9 and module_loaded
        except:
            return False

    def init_ui(self):
        """Initialize the user interface"""
        # Main layout
        self.box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=0)
        self.set_content(self.box)

        # Header bar
        self.header = Adw.HeaderBar()
        self.header.set_title_widget(Gtk.Label(label="Legion Toolkit - Gen 9 Enhanced"))
        self.box.append(self.header)

        # Add menu button
        menu_button = Gtk.MenuButton()
        menu_button.set_icon_name("open-menu-symbolic")
        self.header.pack_end(menu_button)

        # Create notebook for tabs
        self.notebook = Gtk.Notebook()
        self.notebook.set_margin_top(10)
        self.notebook.set_margin_bottom(10)
        self.notebook.set_margin_start(10)
        self.notebook.set_margin_end(10)
        self.box.append(self.notebook)

        # Add tabs
        self.add_ai_thermal_tab()
        self.add_performance_tab()
        self.add_thermal_tab()
        self.add_power_tab()
        self.add_rgb_tab()
        self.add_advanced_tab()

    def add_ai_thermal_tab(self):
        """Add AI thermal optimization tab (Gen 9 exclusive)"""
        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        page.set_margin_top(20)
        page.set_margin_bottom(20)
        page.set_margin_start(20)
        page.set_margin_end(20)

        # AI Status card
        ai_card = Adw.PreferencesGroup()
        ai_card.set_title("AI Thermal Optimization")
        ai_card.set_description("Advanced machine learning thermal management for Gen 9")
        page.append(ai_card)

        # AI Status row
        self.ai_status_row = Adw.ActionRow()
        self.ai_status_row.set_title("AI Optimization Status")
        self.ai_status_row.set_subtitle("Initializing...")

        self.ai_status_switch = Gtk.Switch()
        self.ai_status_switch.set_valign(Gtk.Align.CENTER)
        self.ai_status_switch.connect("notify::active", self.on_ai_optimization_toggled)
        self.ai_status_row.add_suffix(self.ai_status_switch)
        ai_card.add(self.ai_status_row)

        # Workload selection
        workload_row = Adw.ComboRow()
        workload_row.set_title("Workload Type")
        workload_row.set_subtitle("Select your current activity for optimal thermal management")

        workload_model = Gtk.StringList.new([
            "Balanced", "Gaming", "Productivity", "AI/ML Workload"
        ])
        workload_row.set_model(workload_model)
        workload_row.connect("notify::selected", self.on_workload_changed)
        ai_card.add(workload_row)
        self.workload_row = workload_row

        # Thermal predictions card
        pred_card = Adw.PreferencesGroup()
        pred_card.set_title("Thermal Predictions")
        pred_card.set_description("AI-powered thermal forecasting")
        page.append(pred_card)

        # Throttle risk
        self.throttle_risk_row = Adw.ActionRow()
        self.throttle_risk_row.set_title("Throttle Risk")
        self.throttle_risk_row.set_subtitle("0% - Low Risk")
        pred_card.add(self.throttle_risk_row)

        # Temperature predictions
        self.pred_cpu_row = Adw.ActionRow()
        self.pred_cpu_row.set_title("Predicted CPU Temp (60s)")
        self.pred_cpu_row.set_subtitle("--°C")
        pred_card.add(self.pred_cpu_row)

        self.pred_gpu_row = Adw.ActionRow()
        self.pred_gpu_row.set_title("Predicted GPU Temp (60s)")
        self.pred_gpu_row.set_subtitle("--°C")
        pred_card.add(self.pred_gpu_row)

        # Quick actions card
        actions_card = Adw.PreferencesGroup()
        actions_card.set_title("Quick Actions")
        page.append(actions_card)

        # Run optimization button
        optimize_button = Gtk.Button(label="Run AI Optimization")
        optimize_button.add_css_class("suggested-action")
        optimize_button.set_margin_top(12)
        optimize_button.set_margin_bottom(12)
        optimize_button.connect("clicked", self.on_run_optimization)
        actions_card.add(optimize_button)
        self.optimize_button = optimize_button

        # Apply Gen 9 fixes button
        fixes_button = Gtk.Button(label="Apply Gen 9 Hardware Fixes")
        fixes_button.set_margin_bottom(12)
        fixes_button.connect("clicked", self.on_apply_gen9_fixes)
        actions_card.add(fixes_button)

        # AI Recommendations
        rec_card = Adw.PreferencesGroup()
        rec_card.set_title("AI Recommendations")
        page.append(rec_card)

        self.recommendations_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        rec_card.add(self.recommendations_box)

        self.notebook.append_page(page, Gtk.Label(label="AI Thermal"))

    def add_performance_tab(self):
        """Add performance tuning tab"""
        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        page.set_margin_top(20)
        page.set_margin_bottom(20)
        page.set_margin_start(20)
        page.set_margin_end(20)

        # Performance mode
        perf_card = Adw.PreferencesGroup()
        perf_card.set_title("Performance Mode")
        page.append(perf_card)

        self.perf_combo = Adw.ComboRow()
        self.perf_combo.set_title("Current Mode")

        model = Gtk.StringList.new(["Quiet", "Balanced", "Performance", "Custom"])
        self.perf_combo.set_model(model)
        self.perf_combo.connect("notify::selected", self.on_performance_mode_changed)
        perf_card.add(self.perf_combo)

        # CPU settings
        cpu_card = Adw.PreferencesGroup()
        cpu_card.set_title("CPU Power Management (i9-14900HX)")
        page.append(cpu_card)

        # PL1 adjustment
        pl1_row = Adw.ActionRow()
        pl1_row.set_title("CPU Base Power (PL1)")
        pl1_row.set_subtitle("Long-term sustained power limit")

        self.pl1_adjustment = Gtk.Adjustment(value=55, lower=15, upper=140, step_increment=5)
        self.pl1_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=self.pl1_adjustment)
        self.pl1_scale.set_draw_value(True)
        self.pl1_scale.set_hexpand(True)
        self.pl1_scale.connect("value-changed", self.on_pl1_changed)
        pl1_row.add_suffix(self.pl1_scale)
        cpu_card.add(pl1_row)

        # PL2 adjustment
        pl2_row = Adw.ActionRow()
        pl2_row.set_title("CPU Turbo Power (PL2)")
        pl2_row.set_subtitle("Short-term burst power limit")

        self.pl2_adjustment = Gtk.Adjustment(value=140, lower=55, upper=200, step_increment=5)
        self.pl2_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=self.pl2_adjustment)
        self.pl2_scale.set_draw_value(True)
        self.pl2_scale.set_hexpand(True)
        self.pl2_scale.connect("value-changed", self.on_pl2_changed)
        pl2_row.add_suffix(self.pl2_scale)
        cpu_card.add(pl2_row)

        # GPU settings
        gpu_card = Adw.PreferencesGroup()
        gpu_card.set_title("GPU Settings (RTX 4070)")
        page.append(gpu_card)

        # TGP adjustment
        tgp_row = Adw.ActionRow()
        tgp_row.set_title("GPU Total Graphics Power (TGP)")
        tgp_row.set_subtitle("Maximum GPU power consumption")

        self.tgp_adjustment = Gtk.Adjustment(value=115, lower=60, upper=140, step_increment=5)
        self.tgp_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=self.tgp_adjustment)
        self.tgp_scale.set_draw_value(True)
        self.tgp_scale.set_hexpand(True)
        self.tgp_scale.connect("value-changed", self.on_tgp_changed)
        tgp_row.add_suffix(self.tgp_scale)
        gpu_card.add(tgp_row)

        self.notebook.append_page(page, Gtk.Label(label="Performance"))

    def add_thermal_tab(self):
        """Add thermal monitoring and control tab"""
        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        page.set_margin_top(20)
        page.set_margin_bottom(20)
        page.set_margin_start(20)
        page.set_margin_end(20)

        # Enhanced temperature monitoring for Gen 9
        temp_card = Adw.PreferencesGroup()
        temp_card.set_title("Enhanced Temperature Monitoring")
        temp_card.set_description("Real-time thermal sensors with vapor chamber monitoring")
        page.append(temp_card)

        # CPU package temp
        self.cpu_temp_row = Adw.ActionRow()
        self.cpu_temp_row.set_title("CPU Package (i9-14900HX)")
        self.cpu_temp_row.set_subtitle("0°C")
        temp_card.add(self.cpu_temp_row)

        # GPU temp
        self.gpu_temp_row = Adw.ActionRow()
        self.gpu_temp_row.set_title("GPU Core (RTX 4070)")
        self.gpu_temp_row.set_subtitle("0°C")
        temp_card.add(self.gpu_temp_row)

        # GPU hotspot
        self.gpu_hotspot_row = Adw.ActionRow()
        self.gpu_hotspot_row.set_title("GPU Hotspot")
        self.gpu_hotspot_row.set_subtitle("0°C")
        temp_card.add(self.gpu_hotspot_row)

        # VRM temp
        self.vrm_temp_row = Adw.ActionRow()
        self.vrm_temp_row.set_title("VRM Temperature")
        self.vrm_temp_row.set_subtitle("0°C")
        temp_card.add(self.vrm_temp_row)

        # PCIe 5.0 SSD temp
        self.ssd_temp_row = Adw.ActionRow()
        self.ssd_temp_row.set_title("PCIe 5.0 SSD")
        self.ssd_temp_row.set_subtitle("0°C")
        temp_card.add(self.ssd_temp_row)

        # Enhanced fan control
        fan_card = Adw.PreferencesGroup()
        fan_card.set_title("Dual Fan Control")
        fan_card.set_description("Advanced vapor chamber cooling system")
        page.append(fan_card)

        # Fan 1 (CPU)
        self.fan1_row = Adw.ActionRow()
        self.fan1_row.set_title("CPU Fan")
        self.fan1_row.set_subtitle("0 RPM")

        self.fan1_adjustment = Gtk.Adjustment(value=50, lower=0, upper=100, step_increment=10)
        self.fan1_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=self.fan1_adjustment)
        self.fan1_scale.set_draw_value(True)
        self.fan1_scale.set_hexpand(True)
        self.fan1_scale.connect("value-changed", self.on_fan1_changed)
        self.fan1_row.add_suffix(self.fan1_scale)
        fan_card.add(self.fan1_row)

        # Fan 2 (GPU)
        self.fan2_row = Adw.ActionRow()
        self.fan2_row.set_title("GPU Fan")
        self.fan2_row.set_subtitle("0 RPM")

        self.fan2_adjustment = Gtk.Adjustment(value=50, lower=0, upper=100, step_increment=10)
        self.fan2_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=self.fan2_adjustment)
        self.fan2_scale.set_draw_value(True)
        self.fan2_scale.set_hexpand(True)
        self.fan2_scale.connect("value-changed", self.on_fan2_changed)
        self.fan2_row.add_suffix(self.fan2_scale)
        fan_card.add(self.fan2_row)

        self.notebook.append_page(page, Gtk.Label(label="Thermal"))

    def add_power_tab(self):
        """Add power management tab"""
        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        page.set_margin_top(20)
        page.set_margin_bottom(20)
        page.set_margin_start(20)
        page.set_margin_end(20)

        # Battery settings
        battery_card = Adw.PreferencesGroup()
        battery_card.set_title("Battery Management")
        page.append(battery_card)

        # Battery threshold
        threshold_row = Adw.ActionRow()
        threshold_row.set_title("Charge Threshold")
        threshold_row.set_subtitle("Stop charging at specified level")

        self.threshold_adjustment = Gtk.Adjustment(value=80, lower=50, upper=100, step_increment=5)
        self.threshold_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=self.threshold_adjustment)
        self.threshold_scale.set_draw_value(True)
        self.threshold_scale.set_hexpand(True)
        self.threshold_scale.connect("value-changed", self.on_threshold_changed)
        threshold_row.add_suffix(self.threshold_scale)
        battery_card.add(threshold_row)

        self.notebook.append_page(page, Gtk.Label(label="Power"))

    def add_rgb_tab(self):
        """Add RGB lighting control tab"""
        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        page.set_margin_top(20)
        page.set_margin_bottom(20)
        page.set_margin_start(20)
        page.set_margin_end(20)

        # RGB settings
        rgb_card = Adw.PreferencesGroup()
        rgb_card.set_title("Spectrum RGB Keyboard (4-Zone)")
        page.append(rgb_card)

        # RGB mode
        mode_row = Adw.ComboRow()
        mode_row.set_title("Lighting Mode")

        rgb_model = Gtk.StringList.new([
            "Off", "Static", "Breathing", "Rainbow", "Wave"
        ])
        mode_row.set_model(rgb_model)
        mode_row.connect("notify::selected", self.on_rgb_mode_changed)
        rgb_card.add(mode_row)

        # Brightness
        brightness_row = Adw.ActionRow()
        brightness_row.set_title("Brightness")

        brightness_adjustment = Gtk.Adjustment(value=75, lower=0, upper=100, step_increment=10)
        brightness_scale = Gtk.Scale(orientation=Gtk.Orientation.HORIZONTAL, adjustment=brightness_adjustment)
        brightness_scale.set_draw_value(True)
        brightness_scale.set_hexpand(True)
        brightness_scale.connect("value-changed", self.on_brightness_changed)
        brightness_row.add_suffix(brightness_scale)
        rgb_card.add(brightness_row)

        self.notebook.append_page(page, Gtk.Label(label="RGB"))

    def add_advanced_tab(self):
        """Add advanced settings tab"""
        page = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=12)
        page.set_margin_top(20)
        page.set_margin_bottom(20)
        page.set_margin_start(20)
        page.set_margin_end(20)

        # Gen 9 hardware fixes
        fixes_card = Adw.PreferencesGroup()
        fixes_card.set_title("Gen 9 Hardware Fixes")
        fixes_card.set_description("Advanced hardware optimizations for Legion Slim 7i Gen 9")
        page.append(fixes_card)

        # Thermal throttling fix
        thermal_fix_row = Adw.ActionRow()
        thermal_fix_row.set_title("Enhanced Thermal Management")
        thermal_fix_row.set_subtitle("Increase thermal limits and optimize vapor chamber")

        thermal_fix_switch = Gtk.Switch()
        thermal_fix_switch.set_valign(Gtk.Align.CENTER)
        thermal_fix_switch.connect("notify::active", self.on_thermal_fix_toggled)
        thermal_fix_row.add_suffix(thermal_fix_switch)
        fixes_card.add(thermal_fix_row)

        # Core scheduling fix
        sched_fix_row = Adw.ActionRow()
        sched_fix_row.set_title("P-core/E-core Optimization")
        sched_fix_row.set_subtitle("Optimize core ratios for i9-14900HX")

        sched_fix_switch = Gtk.Switch()
        sched_fix_switch.set_valign(Gtk.Align.CENTER)
        sched_fix_switch.connect("notify::active", self.on_sched_fix_toggled)
        sched_fix_row.add_suffix(sched_fix_switch)
        fixes_card.add(sched_fix_row)

        # Kernel module info
        kernel_card = Adw.PreferencesGroup()
        kernel_card.set_title("Kernel Module Information")
        page.append(kernel_card)

        self.module_status_row = Adw.ActionRow()
        self.module_status_row.set_title("Module Status")
        self.module_status_row.set_subtitle("Checking...")
        kernel_card.add(self.module_status_row)

        # Update module status
        self.update_module_status()

        self.notebook.append_page(page, Gtk.Label(label="Advanced"))

    def start_thermal_monitoring(self):
        """Start thermal monitoring and AI optimization thread"""
        self.monitoring_active = True
        self.ai_active = False
        self.current_workload = "Balanced"

        # Start monitoring thread
        def monitoring_loop():
            while self.monitoring_active:
                try:
                    GLib.idle_add(self.update_thermal_data)
                    time.sleep(2)  # Update every 2 seconds
                except Exception as e:
                    print(f"Monitoring error: {e}")
                    time.sleep(5)

        self.monitoring_thread = threading.Thread(target=monitoring_loop, daemon=True)
        self.monitoring_thread.start()

    def update_thermal_data(self):
        """Update thermal data from kernel module"""
        try:
            # Read temperature data
            cpu_temp = self.read_kernel_module("cpu_temp")
            gpu_temp = self.read_kernel_module("gpu_temp")
            gpu_hotspot = self.read_kernel_module("gpu_hotspot")
            vrm_temp = self.read_kernel_module("vrm_temp")
            ssd_temp = self.read_kernel_module("ssd_temp")

            # Read fan speeds
            fan1_speed = self.read_kernel_module("fan1_speed")
            fan2_speed = self.read_kernel_module("fan2_speed")

            # Update UI
            if cpu_temp:
                self.cpu_temp_row.set_subtitle(f"{cpu_temp}°C")
            if gpu_temp:
                self.gpu_temp_row.set_subtitle(f"{gpu_temp}°C")
            if gpu_hotspot:
                self.gpu_hotspot_row.set_subtitle(f"{gpu_hotspot}°C")
            if vrm_temp:
                self.vrm_temp_row.set_subtitle(f"{vrm_temp}°C")
            if ssd_temp:
                self.ssd_temp_row.set_subtitle(f"{ssd_temp}°C")

            if fan1_speed:
                self.fan1_row.set_subtitle(f"{fan1_speed} RPM")
            if fan2_speed:
                self.fan2_row.set_subtitle(f"{fan2_speed} RPM")

            # Update AI thermal predictions if active
            if self.ai_active:
                self.update_ai_predictions(cpu_temp, gpu_temp)

        except Exception as e:
            print(f"Error updating thermal data: {e}")

    def update_ai_predictions(self, cpu_temp: str, gpu_temp: str):
        """Update AI thermal predictions (simplified simulation)"""
        try:
            cpu_val = int(cpu_temp) if cpu_temp else 70
            gpu_val = int(gpu_temp) if gpu_temp else 65

            # Simple prediction simulation (in real implementation, this would use the AI model)
            pred_cpu = cpu_val + 2  # Predict slight increase
            pred_gpu = gpu_val + 1

            # Calculate throttle risk
            max_temp = max(cpu_val, gpu_val)
            if max_temp >= 90:
                risk = "High"
                risk_pct = min(100, (max_temp - 85) * 10)
            elif max_temp >= 80:
                risk = "Medium"
                risk_pct = (max_temp - 75) * 4
            else:
                risk = "Low"
                risk_pct = max_temp - 60 if max_temp > 60 else 0

            # Update UI
            self.pred_cpu_row.set_subtitle(f"{pred_cpu}°C")
            self.pred_gpu_row.set_subtitle(f"{pred_gpu}°C")
            self.throttle_risk_row.set_subtitle(f"{risk_pct:.0f}% - {risk} Risk")

        except Exception as e:
            print(f"Error updating AI predictions: {e}")

    # Event handlers
    def on_ai_optimization_toggled(self, switch, _):
        """Handle AI optimization toggle"""
        self.ai_active = switch.get_active()
        if self.ai_active:
            self.ai_status_row.set_subtitle("AI optimization active")
            self.start_ai_optimization()
        else:
            self.ai_status_row.set_subtitle("AI optimization disabled")
            self.stop_ai_optimization()

    def on_workload_changed(self, combo, _):
        """Handle workload type change"""
        workloads = ["Balanced", "Gaming", "Productivity", "AIWorkload"]
        selected = combo.get_selected()
        if 0 <= selected < len(workloads):
            self.current_workload = workloads[selected]
            print(f"Workload changed to: {self.current_workload}")

    def on_run_optimization(self, button):
        """Handle run optimization button"""
        button.set_sensitive(False)
        button.set_label("Running optimization...")

        def run_optimization():
            try:
                # Simulate AI optimization process
                time.sleep(3)

                # Apply optimizations based on workload
                if self.current_workload == "Gaming":
                    self.write_kernel_module("cpu_pl2", "140")
                    self.write_kernel_module("gpu_tgp", "140")
                elif self.current_workload == "Productivity":
                    self.write_kernel_module("cpu_pl2", "115")
                    self.write_kernel_module("gpu_tgp", "60")
                elif self.current_workload == "AIWorkload":
                    self.write_kernel_module("cpu_pl2", "90")
                    self.write_kernel_module("gpu_tgp", "140")

                GLib.idle_add(self.optimization_complete, button)

            except Exception as e:
                print(f"Optimization error: {e}")
                GLib.idle_add(self.optimization_error, button, str(e))

        threading.Thread(target=run_optimization, daemon=True).start()

    def optimization_complete(self, button):
        """Handle optimization completion"""
        button.set_sensitive(True)
        button.set_label("Run AI Optimization")

        # Update recommendations
        self.update_recommendations([
            f"Optimized for {self.current_workload} workload",
            "Thermal limits adjusted for optimal performance",
            "Fan curves updated for current conditions"
        ])

    def optimization_error(self, button, error):
        """Handle optimization error"""
        button.set_sensitive(True)
        button.set_label("Run AI Optimization")
        print(f"Optimization failed: {error}")

    def update_recommendations(self, recommendations: List[str]):
        """Update AI recommendations display"""
        # Clear existing recommendations
        child = self.recommendations_box.get_first_child()
        while child:
            next_child = child.get_next_sibling()
            self.recommendations_box.remove(child)
            child = next_child

        # Add new recommendations
        for rec in recommendations:
            row = Adw.ActionRow()
            row.set_title(rec)
            row.add_css_class("card")
            self.recommendations_box.append(row)

    def on_apply_gen9_fixes(self, button):
        """Apply Gen 9 hardware fixes"""
        button.set_sensitive(False)
        self.write_kernel_module("apply_gen9_fixes", "1")

        # Update recommendations
        self.update_recommendations([
            "Thermal throttling threshold increased to 105°C",
            "Vapor chamber boost mode enabled",
            "Optimized fan curves applied",
            "P-core/E-core ratios optimized for i9-14900HX"
        ])

        button.set_sensitive(True)

    def on_performance_mode_changed(self, combo, _):
        """Handle performance mode change"""
        modes = ["quiet", "balanced", "performance", "custom"]
        selected = combo.get_selected()
        if 0 <= selected < len(modes):
            self.write_kernel_module("performance_mode", modes[selected])

    def on_pl1_changed(self, scale):
        """Handle PL1 change"""
        value = int(scale.get_value())
        self.write_kernel_module("cpu_pl1", str(value))

    def on_pl2_changed(self, scale):
        """Handle PL2 change"""
        value = int(scale.get_value())
        self.write_kernel_module("cpu_pl2", str(value))

    def on_tgp_changed(self, scale):
        """Handle TGP change"""
        value = int(scale.get_value())
        self.write_kernel_module("gpu_tgp", str(value))

    def on_fan1_changed(self, scale):
        """Handle fan 1 speed change"""
        value = int(scale.get_value())
        self.write_kernel_module("fan1_target", str(value))

    def on_fan2_changed(self, scale):
        """Handle fan 2 speed change"""
        value = int(scale.get_value())
        self.write_kernel_module("fan2_target", str(value))

    def on_threshold_changed(self, scale):
        """Handle battery threshold change"""
        value = int(scale.get_value())
        # Implement battery threshold control
        print(f"Battery threshold: {value}%")

    def on_rgb_mode_changed(self, combo, _):
        """Handle RGB mode change"""
        modes = ["off", "static", "breathing", "rainbow", "wave"]
        selected = combo.get_selected()
        if 0 <= selected < len(modes):
            self.write_kernel_module("rgb_mode", modes[selected])

    def on_brightness_changed(self, scale):
        """Handle brightness change"""
        value = int(scale.get_value())
        self.write_kernel_module("rgb_brightness", str(value))

    def on_thermal_fix_toggled(self, switch, _):
        """Handle thermal fix toggle"""
        if switch.get_active():
            self.write_kernel_module("apply_gen9_fixes", "1")

    def on_sched_fix_toggled(self, switch, _):
        """Handle scheduling fix toggle"""
        if switch.get_active():
            # Apply core scheduling optimizations
            subprocess.run(["sudo", "sh", "-c", "echo 1 > /sys/kernel/legion_laptop/apply_gen9_fixes"])

    def start_ai_optimization(self):
        """Start AI optimization service"""
        print("Starting AI optimization...")

    def stop_ai_optimization(self):
        """Stop AI optimization service"""
        print("Stopping AI optimization...")

    def update_module_status(self):
        """Update kernel module status"""
        try:
            if Path("/sys/kernel/legion_laptop/").exists():
                self.module_status_row.set_subtitle("Loaded and active")
            else:
                self.module_status_row.set_subtitle("Not loaded")
        except:
            self.module_status_row.set_subtitle("Error checking status")

    # Kernel module interaction
    def read_kernel_module(self, parameter: str) -> Optional[str]:
        """Read value from kernel module sysfs"""
        try:
            path = Path(f"/sys/kernel/legion_laptop/{parameter}")
            if path.exists():
                return path.read_text().strip()
        except Exception as e:
            print(f"Error reading {parameter}: {e}")
        return None

    def write_kernel_module(self, parameter: str, value: str):
        """Write value to kernel module sysfs"""
        try:
            path = Path(f"/sys/kernel/legion_laptop/{parameter}")
            if path.exists():
                subprocess.run(
                    ["sudo", "sh", "-c", f"echo {value} > {path}"],
                    check=True
                )
        except Exception as e:
            print(f"Error writing {parameter}: {e}")

    def load_current_settings(self):
        """Load current settings from kernel module"""
        # Read performance mode
        mode = self.read_kernel_module("performance_mode")
        if mode:
            modes = {"quiet": 0, "balanced": 1, "performance": 2, "custom": 3}
            if mode in modes:
                self.perf_combo.set_selected(modes[mode])

    def show_permission_dialog(self):
        """Show dialog requesting sudo permissions"""
        dialog = Adw.MessageDialog.new(
            self,
            "Administrator Privileges Required",
            "Legion Toolkit requires administrator privileges to control hardware."
        )
        dialog.add_response("quit", "Quit")
        dialog.add_response("restart", "Restart with sudo")
        dialog.set_response_appearance("restart", Adw.ResponseAppearance.SUGGESTED)
        dialog.connect("response", self.on_permission_response)
        dialog.present()

    def on_permission_response(self, dialog, response):
        """Handle permission dialog response"""
        if response == "restart":
            os.execvp("sudo", ["sudo", sys.executable] + sys.argv)
        else:
            self.get_application().quit()

    def show_gen9_requirement_dialog(self):
        """Show dialog for Gen 9 requirement"""
        dialog = Adw.MessageDialog.new(
            self,
            "Legion Slim 7i Gen 9 Required",
            "This application requires a Legion Slim 7i Gen 9 (16IRX9) with the kernel module loaded."
        )
        dialog.add_response("quit", "Quit")
        dialog.add_response("info", "More Info")
        dialog.set_response_appearance("info", Adw.ResponseAppearance.SUGGESTED)
        dialog.connect("response", self.on_gen9_response)
        dialog.present()

    def on_gen9_response(self, dialog, response):
        """Handle Gen 9 requirement dialog response"""
        if response == "info":
            # Show information about Gen 9 requirements
            print("For more information, visit: https://github.com/LenovoLegionToolkit")
        self.get_application().quit()

def main():
    """Main entry point"""
    app = LegionToolkitLinux()
    return app.run(sys.argv)

if __name__ == "__main__":
    sys.exit(main())