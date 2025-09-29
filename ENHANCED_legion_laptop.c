/*
 * Enhanced Legion Laptop Kernel Module
 * Universal support for Legion Gen 6-9 with backward compatibility
 *
 * Copyright (C) 2025 Vivek Chamoli <vivekchamoli@outlook.com>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 */

#include <linux/module.h>
#include <linux/kernel.h>
#include <linux/init.h>
#include <linux/acpi.h>
#include <linux/platform_device.h>
#include <linux/hwmon.h>
#include <linux/hwmon-sysfs.h>
#include <linux/thermal.h>
#include <linux/leds.h>
#include <linux/power_supply.h>
#include <linux/version.h>
#include <linux/sysfs.h>
#include <linux/device.h>
#include <linux/dmi.h>
#include <linux/delay.h>

#define DRIVER_NAME "legion_laptop"
#define DRIVER_VERSION "6.1.0"

// Backward compatibility macros
#if LINUX_VERSION_CODE < KERNEL_VERSION(5,15,0)
#define THERMAL_NO_LIMIT (-1UL)
#define thermal_zone_device_enable(tz) do { } while (0)
#define thermal_zone_device_disable(tz) do { } while (0)
#endif

#if LINUX_VERSION_CODE < KERNEL_VERSION(5,10,0)
#define hwmon_device_register_with_groups(dev, name, drvdata, groups) \
    hwmon_device_register_with_groups(dev, name, drvdata, groups)
#endif

// Hardware generation detection
enum legion_generation {
    LEGION_GEN_UNKNOWN = 0,
    LEGION_GEN_4 = 4,
    LEGION_GEN_5 = 5,
    LEGION_GEN_6 = 6,
    LEGION_GEN_7 = 7,
    LEGION_GEN_8 = 8,
    LEGION_GEN_9 = 9
};

// Hardware capabilities
struct legion_capabilities {
    bool thermal_control;
    bool fan_control;
    bool rgb_control;
    bool power_control;
    bool battery_control;
    bool custom_mode;
};

struct legion_laptop {
    struct platform_device *pdev;
    struct acpi_device *adev;
    enum legion_generation generation;
    struct legion_capabilities caps;

    // Thermal management
    struct thermal_zone_device *cpu_thermal;
    struct thermal_zone_device *gpu_thermal;

    // Hardware monitoring
    struct device *hwmon_dev;

    // RGB control
    struct led_classdev rgb_zones[4];
    bool rgb_initialized;

    // Power management
    int current_power_mode;
    bool custom_mode_enabled;

    // EC interface
    bool ec_available;
    u16 ec_base_addr;

    // Mutex for synchronization
    struct mutex lock;
};

// Generation-specific configuration
static const struct {
    enum legion_generation gen;
    const char *acpi_thermal;
    const char *acpi_fan;
    const char *acpi_power;
    const char *acpi_rgb;
    u16 ec_addr_offset;
} legion_config[] = {
    { LEGION_GEN_6, "\\_SB.PCI0.LPC0.EC0.SPMO", "\\_SB.PCI0.LPC0.EC0.SFAN",
      "\\_SB.PCI0.LPC0.EC0.SPWR", "\\_SB.PCI0.LPC0.EC0.SRGB", 0x0300 },
    { LEGION_GEN_7, "\\_SB.PCI0.LPC0.EC0.SPMO", "\\_SB.PCI0.LPC0.EC0.SFAN",
      "\\_SB.PCI0.LPC0.EC0.SPWR", "\\_SB.PCI0.LPC0.EC0.SRGB", 0x0300 },
    { LEGION_GEN_8, "\\_SB.PC00.LPC0.EC0.SPMO", "\\_SB.PC00.LPC0.EC0.SFAN",
      "\\_SB.PC00.LPC0.EC0.SPWR", "\\_SB.PC00.LPC0.EC0.SRGB", 0x0400 },
    { LEGION_GEN_9, "\\_SB.PC00.LPC0.EC0.SPMO", "\\_SB.PC00.LPC0.EC0.SFAN",
      "\\_SB.PC00.LPC0.EC0.SPWR", "\\_SB.PC00.LPC0.EC0.SRGB", 0x0400 },
};

static struct legion_laptop *legion_device;

// DMI-based generation detection
static enum legion_generation detect_generation_by_dmi(void)
{
    const char *product = dmi_get_system_info(DMI_PRODUCT_NAME);
    const char *version = dmi_get_system_info(DMI_PRODUCT_VERSION);

    if (!product)
        return LEGION_GEN_UNKNOWN;

    pr_info("legion_laptop: Detected product: %s\n", product);

    // Legion 9 series detection
    if (strstr(product, "Legion 9i") || strstr(product, "16IRX9") ||
        strstr(product, "Legion Slim 7i Gen 9"))
        return LEGION_GEN_9;

    // Legion 8 series detection
    if (strstr(product, "Legion 7i Gen 8") || strstr(product, "16IRX8") ||
        strstr(product, "Legion 5i Gen 8") || strstr(product, "15IRX8"))
        return LEGION_GEN_8;

    // Legion 7 series detection
    if (strstr(product, "Legion 7i Gen 7") || strstr(product, "16IRX7") ||
        strstr(product, "Legion 5i Gen 7") || strstr(product, "15IRX7"))
        return LEGION_GEN_7;

    // Legion 6 series detection
    if (strstr(product, "Legion 7i Gen 6") || strstr(product, "16IRX6") ||
        strstr(product, "Legion 5i Gen 6") || strstr(product, "15IRX6"))
        return LEGION_GEN_6;

    // Fallback for newer models
    if (strstr(product, "Legion") && (strstr(product, "7i") || strstr(product, "5i")))
        return LEGION_GEN_9; // Assume latest for future compatibility

    return LEGION_GEN_UNKNOWN;
}

// ACPI helper functions with error handling
static acpi_status legion_acpi_call(struct acpi_device *adev, const char *method,
                                   int arg, unsigned long long *result)
{
    acpi_status status;
    struct acpi_object_list args;
    union acpi_object in_obj;

    if (!adev || !method)
        return AE_BAD_PARAMETER;

    in_obj.type = ACPI_TYPE_INTEGER;
    in_obj.integer.value = arg;
    args.count = 1;
    args.pointer = &in_obj;

    status = acpi_evaluate_integer(adev->handle, (char *)method, &args, result);

    if (ACPI_FAILURE(status)) {
        pr_debug("legion_laptop: ACPI call %s(%d) failed: 0x%x\n",
                method, arg, status);
    }

    return status;
}

// Capability detection
static void detect_capabilities(struct legion_laptop *legion)
{
    unsigned long long result;
    acpi_status status;
    const char *thermal_method = NULL;
    int i;

    // Initialize capabilities
    memset(&legion->caps, 0, sizeof(legion->caps));

    // Find generation-specific methods
    for (i = 0; i < ARRAY_SIZE(legion_config); i++) {
        if (legion_config[i].gen == legion->generation) {
            thermal_method = legion_config[i].acpi_thermal;
            break;
        }
    }

    if (!thermal_method) {
        pr_warn("legion_laptop: No configuration found for generation %d\n",
                legion->generation);
        return;
    }

    // Test thermal control
    status = legion_acpi_call(legion->adev, thermal_method, 0, &result);
    legion->caps.thermal_control = ACPI_SUCCESS(status);

    // Test fan control
    status = legion_acpi_call(legion->adev, legion_config[i].acpi_fan, 0, &result);
    legion->caps.fan_control = ACPI_SUCCESS(status);

    // Test power control
    status = legion_acpi_call(legion->adev, legion_config[i].acpi_power, 0, &result);
    legion->caps.power_control = ACPI_SUCCESS(status);

    // Test RGB control
    status = legion_acpi_call(legion->adev, legion_config[i].acpi_rgb, 0, &result);
    legion->caps.rgb_control = ACPI_SUCCESS(status);

    // Battery control is usually available via standard interfaces
    legion->caps.battery_control = true;

    // Custom mode availability (Gen 7+)
    legion->caps.custom_mode = (legion->generation >= LEGION_GEN_7);

    pr_info("legion_laptop: Capabilities - Thermal: %s, Fan: %s, RGB: %s, Power: %s\n",
            legion->caps.thermal_control ? "Yes" : "No",
            legion->caps.fan_control ? "Yes" : "No",
            legion->caps.rgb_control ? "Yes" : "No",
            legion->caps.power_control ? "Yes" : "No");
}

// Thermal zone operations
static int legion_thermal_get_cpu_temp(struct thermal_zone_device *tz, int *temp)
{
    struct legion_laptop *legion = tz->devdata;
    unsigned long long result;
    acpi_status status;
    int i;

    if (!legion || legion->generation == LEGION_GEN_UNKNOWN)
        return -ENODEV;

    mutex_lock(&legion->lock);

    // Find thermal method for this generation
    for (i = 0; i < ARRAY_SIZE(legion_config); i++) {
        if (legion_config[i].gen == legion->generation) {
            status = legion_acpi_call(legion->adev, legion_config[i].acpi_thermal,
                                    0, &result);
            if (ACPI_SUCCESS(status)) {
                *temp = (int)result * 1000; // Convert to millidegrees
                mutex_unlock(&legion->lock);
                return 0;
            }
            break;
        }
    }

    mutex_unlock(&legion->lock);
    return -EIO;
}

static int legion_thermal_get_gpu_temp(struct thermal_zone_device *tz, int *temp)
{
    struct legion_laptop *legion = tz->devdata;
    unsigned long long result;
    acpi_status status;
    int i;

    if (!legion || legion->generation == LEGION_GEN_UNKNOWN)
        return -ENODEV;

    mutex_lock(&legion->lock);

    // Find thermal method for this generation (GPU is usually arg=1)
    for (i = 0; i < ARRAY_SIZE(legion_config); i++) {
        if (legion_config[i].gen == legion->generation) {
            status = legion_acpi_call(legion->adev, legion_config[i].acpi_thermal,
                                    1, &result);
            if (ACPI_SUCCESS(status)) {
                *temp = (int)result * 1000; // Convert to millidegrees
                mutex_unlock(&legion->lock);
                return 0;
            }
            break;
        }
    }

    mutex_unlock(&legion->lock);
    return -EIO;
}

static struct thermal_zone_device_ops legion_cpu_thermal_ops = {
    .get_temp = legion_thermal_get_cpu_temp,
};

static struct thermal_zone_device_ops legion_gpu_thermal_ops = {
    .get_temp = legion_thermal_get_gpu_temp,
};

// sysfs attributes
static ssize_t generation_show(struct device *dev,
                              struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->generation);
}

static ssize_t capabilities_show(struct device *dev,
                                struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "thermal:%d fan:%d rgb:%d power:%d battery:%d custom:%d\n",
                  legion->caps.thermal_control,
                  legion->caps.fan_control,
                  legion->caps.rgb_control,
                  legion->caps.power_control,
                  legion->caps.battery_control,
                  legion->caps.custom_mode);
}

static ssize_t fan_mode_show(struct device *dev,
                            struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    unsigned long long result;
    acpi_status status;
    int i;

    if (!legion->caps.fan_control)
        return -ENODEV;

    mutex_lock(&legion->lock);

    for (i = 0; i < ARRAY_SIZE(legion_config); i++) {
        if (legion_config[i].gen == legion->generation) {
            status = legion_acpi_call(legion->adev, legion_config[i].acpi_fan,
                                    0, &result);
            if (ACPI_SUCCESS(status)) {
                mutex_unlock(&legion->lock);
                return sprintf(buf, "%lld\n", result);
            }
            break;
        }
    }

    mutex_unlock(&legion->lock);
    return -EIO;
}

static ssize_t fan_mode_store(struct device *dev,
                             struct device_attribute *attr,
                             const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    unsigned long long result;
    acpi_status status;
    int value, i;

    if (!legion->caps.fan_control)
        return -ENODEV;

    if (kstrtoint(buf, 10, &value))
        return -EINVAL;

    if (value < 0 || value > 2)
        return -EINVAL;

    mutex_lock(&legion->lock);

    for (i = 0; i < ARRAY_SIZE(legion_config); i++) {
        if (legion_config[i].gen == legion->generation) {
            status = legion_acpi_call(legion->adev, legion_config[i].acpi_fan,
                                    value, &result);
            if (ACPI_SUCCESS(status)) {
                mutex_unlock(&legion->lock);
                return count;
            }
            break;
        }
    }

    mutex_unlock(&legion->lock);
    return -EIO;
}

static ssize_t power_mode_show(struct device *dev,
                              struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->current_power_mode);
}

static ssize_t power_mode_store(struct device *dev,
                               struct device_attribute *attr,
                               const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    unsigned long long result;
    acpi_status status;
    int value, i;

    if (!legion->caps.power_control)
        return -ENODEV;

    if (kstrtoint(buf, 10, &value))
        return -EINVAL;

    if (value < 0 || value > 3)
        return -EINVAL;

    mutex_lock(&legion->lock);

    for (i = 0; i < ARRAY_SIZE(legion_config); i++) {
        if (legion_config[i].gen == legion->generation) {
            status = legion_acpi_call(legion->adev, legion_config[i].acpi_power,
                                    value, &result);
            if (ACPI_SUCCESS(status)) {
                legion->current_power_mode = value;
                mutex_unlock(&legion->lock);
                return count;
            }
            break;
        }
    }

    mutex_unlock(&legion->lock);
    return -EIO;
}

static ssize_t cpu_temp_show(struct device *dev,
                            struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    int temp;
    int ret;

    if (!legion->caps.thermal_control)
        return -ENODEV;

    ret = legion_thermal_get_cpu_temp(legion->cpu_thermal, &temp);
    if (ret)
        return ret;

    return sprintf(buf, "%d\n", temp / 1000); // Convert back to degrees
}

static ssize_t gpu_temp_show(struct device *dev,
                            struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    int temp;
    int ret;

    if (!legion->caps.thermal_control)
        return -ENODEV;

    ret = legion_thermal_get_gpu_temp(legion->gpu_thermal, &temp);
    if (ret)
        return ret;

    return sprintf(buf, "%d\n", temp / 1000); // Convert back to degrees
}

// Device attributes
static DEVICE_ATTR_RO(generation);
static DEVICE_ATTR_RO(capabilities);
static DEVICE_ATTR_RW(fan_mode);
static DEVICE_ATTR_RW(power_mode);
static DEVICE_ATTR_RO(cpu_temp);
static DEVICE_ATTR_RO(gpu_temp);

static struct attribute *legion_sysfs_attrs[] = {
    &dev_attr_generation.attr,
    &dev_attr_capabilities.attr,
    &dev_attr_fan_mode.attr,
    &dev_attr_power_mode.attr,
    &dev_attr_cpu_temp.attr,
    &dev_attr_gpu_temp.attr,
    NULL
};

static const struct attribute_group legion_sysfs_group = {
    .attrs = legion_sysfs_attrs,
};

static const struct attribute_group *legion_sysfs_groups[] = {
    &legion_sysfs_group,
    NULL
};

// Platform driver probe
static int legion_laptop_probe(struct platform_device *pdev)
{
    struct legion_laptop *legion;
    struct acpi_device *adev;
    int ret;

    pr_info("legion_laptop: Probing Legion laptop device\n");

    adev = ACPI_COMPANION(&pdev->dev);
    if (!adev) {
        pr_err("legion_laptop: No ACPI companion device found\n");
        return -ENODEV;
    }

    legion = devm_kzalloc(&pdev->dev, sizeof(*legion), GFP_KERNEL);
    if (!legion)
        return -ENOMEM;

    legion->pdev = pdev;
    legion->adev = adev;
    legion->generation = detect_generation_by_dmi();
    mutex_init(&legion->lock);

    if (legion->generation == LEGION_GEN_UNKNOWN) {
        pr_warn("legion_laptop: Unknown Legion generation, limited functionality\n");
        legion->generation = LEGION_GEN_9; // Assume latest for compatibility
    }

    platform_set_drvdata(pdev, legion);
    legion_device = legion; // Global reference

    // Detect hardware capabilities
    detect_capabilities(legion);

    // Create sysfs interface
    ret = sysfs_create_groups(&pdev->dev.kobj, legion_sysfs_groups);
    if (ret) {
        pr_err("legion_laptop: Failed to create sysfs groups: %d\n", ret);
        return ret;
    }

    // Register thermal zones if supported
    if (legion->caps.thermal_control) {
        legion->cpu_thermal = thermal_zone_device_register("legion_cpu",
                                                          0, 0, legion,
                                                          &legion_cpu_thermal_ops,
                                                          NULL, 0, 0);
        if (IS_ERR(legion->cpu_thermal)) {
            pr_warn("legion_laptop: Failed to register CPU thermal zone\n");
            legion->cpu_thermal = NULL;
        } else {
#if LINUX_VERSION_CODE >= KERNEL_VERSION(5,15,0)
            thermal_zone_device_enable(legion->cpu_thermal);
#endif
        }

        legion->gpu_thermal = thermal_zone_device_register("legion_gpu",
                                                          0, 0, legion,
                                                          &legion_gpu_thermal_ops,
                                                          NULL, 0, 0);
        if (IS_ERR(legion->gpu_thermal)) {
            pr_warn("legion_laptop: Failed to register GPU thermal zone\n");
            legion->gpu_thermal = NULL;
        } else {
#if LINUX_VERSION_CODE >= KERNEL_VERSION(5,15,0)
            thermal_zone_device_enable(legion->gpu_thermal);
#endif
        }
    }

    pr_info("legion_laptop: Successfully initialized Legion %s (Generation %d)\n",
            dmi_get_system_info(DMI_PRODUCT_NAME) ?: "Unknown", legion->generation);

    return 0;
}

static int legion_laptop_remove(struct platform_device *pdev)
{
    struct legion_laptop *legion = platform_get_drvdata(pdev);

    pr_info("legion_laptop: Removing Legion laptop device\n");

    // Unregister thermal zones
    if (legion->cpu_thermal) {
#if LINUX_VERSION_CODE >= KERNEL_VERSION(5,15,0)
        thermal_zone_device_disable(legion->cpu_thermal);
#endif
        thermal_zone_device_unregister(legion->cpu_thermal);
    }

    if (legion->gpu_thermal) {
#if LINUX_VERSION_CODE >= KERNEL_VERSION(5,15,0)
        thermal_zone_device_disable(legion->gpu_thermal);
#endif
        thermal_zone_device_unregister(legion->gpu_thermal);
    }

    // Remove sysfs interface
    sysfs_remove_groups(&pdev->dev.kobj, legion_sysfs_groups);

    legion_device = NULL;

    return 0;
}

// ACPI device matching - comprehensive list
static const struct acpi_device_id legion_device_ids[] = {
    {"VPC2004", 0},  // Main Legion laptops
    {"LNVNB161", 0}, // Lenovo notebook generic
    {"PNP0C09", 0},  // ACPI Embedded Controller
    {"", 0},
};
MODULE_DEVICE_TABLE(acpi, legion_device_ids);

// DMI matching for additional detection
static const struct dmi_system_id legion_dmi_ids[] = {
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "Legion"),
        },
    },
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_BOARD_NAME, "LNVNB161216"),
        },
    },
    {}
};
MODULE_DEVICE_TABLE(dmi, legion_dmi_ids);

#if LINUX_VERSION_CODE >= KERNEL_VERSION(5,15,0)
static int legion_laptop_suspend(struct device *dev)
{
    pr_debug("legion_laptop: Suspending\n");
    return 0;
}

static int legion_laptop_resume(struct device *dev)
{
    pr_debug("legion_laptop: Resuming\n");
    return 0;
}

static const struct dev_pm_ops legion_laptop_pm_ops = {
    .suspend = legion_laptop_suspend,
    .resume = legion_laptop_resume,
};
#endif

static struct platform_driver legion_laptop_driver = {
    .probe = legion_laptop_probe,
    .remove = legion_laptop_remove,
    .driver = {
        .name = DRIVER_NAME,
        .acpi_match_table = legion_device_ids,
#if LINUX_VERSION_CODE >= KERNEL_VERSION(5,15,0)
        .pm = &legion_laptop_pm_ops,
#endif
    },
};

// Module initialization
static int __init legion_laptop_init(void)
{
    int ret;

    pr_info("legion_laptop: Enhanced Legion Laptop Module v%s loading...\n",
            DRIVER_VERSION);
    pr_info("legion_laptop: Kernel version: %s\n", UTS_RELEASE);

    // Check if we're running on a Legion laptop
    if (!dmi_check_system(legion_dmi_ids)) {
        pr_info("legion_laptop: Not a Legion laptop, but allowing load for testing\n");
    }

    ret = platform_driver_register(&legion_laptop_driver);
    if (ret) {
        pr_err("legion_laptop: Failed to register platform driver: %d\n", ret);
        return ret;
    }

    pr_info("legion_laptop: Module loaded successfully\n");
    return 0;
}

static void __exit legion_laptop_exit(void)
{
    platform_driver_unregister(&legion_laptop_driver);
    pr_info("legion_laptop: Enhanced Legion Laptop Module unloaded\n");
}

module_init(legion_laptop_init);
module_exit(legion_laptop_exit);

MODULE_AUTHOR("Vivek Chamoli <vivekchamoli@outlook.com>");
MODULE_DESCRIPTION("Enhanced Legion Laptop Hardware Control Module with Universal Compatibility");
MODULE_VERSION(DRIVER_VERSION);
MODULE_LICENSE("GPL");
MODULE_ALIAS("platform:legion_laptop");
MODULE_ALIAS("acpi*:VPC2004:*");
MODULE_INFO(supported, "Legion Gen 6-9 laptops with backward compatibility");

// Module parameters for debugging
static bool debug = false;
module_param(debug, bool, 0644);
MODULE_PARM_DESC(debug, "Enable debug output");

static bool force_load = false;
module_param(force_load, bool, 0644);
MODULE_PARM_DESC(force_load, "Force load on non-Legion systems");