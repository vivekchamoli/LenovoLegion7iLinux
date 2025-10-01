/*
 * Enhanced Lenovo Legion Laptop Platform Driver
 *
 * Author: Vivek Chamoli <vivekchamoli@example.com>
 * Version: 2.0.0
 *
 * This driver provides comprehensive support for Lenovo Legion laptops
 * with backward compatibility for kernel versions 5.4+ to 6.8+
 *
 * Based on the original legion-laptop driver with enhancements for:
 * - Universal Legion Gen 6-9 support
 * - Backward compatibility
 * - Enhanced thermal management
 * - Improved sysfs interface
 * - Better error handling and debugging
 */

#include <linux/kernel.h>
#include <linux/module.h>
#include <linux/init.h>
#include <linux/acpi.h>
#include <linux/platform_device.h>
#include <linux/backlight.h>
#include <linux/hwmon.h>
#include <linux/hwmon-sysfs.h>
#include <linux/thermal.h>
#include <linux/dmi.h>
#include <linux/debugfs.h>
#include <linux/seq_file.h>
#include <linux/sysfs.h>
#include <linux/leds.h>
#include <linux/version.h>

/* Compatibility definitions for different kernel versions */
#if LINUX_VERSION_CODE < KERNEL_VERSION(5,10,0)
#ifndef LEGION_COMPAT_OLD_THERMAL
#define LEGION_COMPAT_OLD_THERMAL
#endif
#endif

#if LINUX_VERSION_CODE < KERNEL_VERSION(5,15,0)
#ifndef LEGION_COMPAT_OLD_PLATFORM
#define LEGION_COMPAT_OLD_PLATFORM
#endif
#endif

#if LINUX_VERSION_CODE >= KERNEL_VERSION(6,0,0)
#ifndef LEGION_COMPAT_NEW_KERNEL
#define LEGION_COMPAT_NEW_KERNEL
#endif
#endif

#define LEGION_ENHANCED_VERSION "2.0.0"
#define LEGION_DRIVER_NAME "legion_laptop_enhanced"

/* Module parameters */
static bool debug = false;
module_param(debug, bool, 0644);
MODULE_PARM_DESC(debug, "Enable debug output");

static bool force_load = false;
module_param(force_load, bool, 0644);
MODULE_PARM_DESC(force_load, "Force loading even if laptop model is not detected");

/* Debug macros */
#define legion_dbg(fmt, ...) \
    do { if (debug) pr_info(LEGION_DRIVER_NAME ": " fmt, ##__VA_ARGS__); } while (0)

#define legion_info(fmt, ...) \
    pr_info(LEGION_DRIVER_NAME ": " fmt, ##__VA_ARGS__)

#define legion_err(fmt, ...) \
    pr_err(LEGION_DRIVER_NAME ": " fmt, ##__VA_ARGS__)

#define legion_warn(fmt, ...) \
    pr_warn(LEGION_DRIVER_NAME ": " fmt, ##__VA_ARGS__)

/* Legion laptop generations and models */
enum legion_generation {
    LEGION_GEN_UNKNOWN = 0,
    LEGION_GEN_6,
    LEGION_GEN_7,
    LEGION_GEN_8,
    LEGION_GEN_9
};

/* ACPI method definitions for different generations */
struct legion_acpi_methods {
    char *thermal_mode_method;
    char *legion_mode_method;
    char *battery_conservation_method;
    char *rapid_charge_method;
    char *fn_lock_method;
    char *rgb_control_method;
    char *fan_curve_method;
    char *overclock_method;
};

/* Legion laptop capabilities */
struct legion_capabilities {
    bool has_thermal_control;
    bool has_legion_mode;
    bool has_battery_conservation;
    bool has_rapid_charge;
    bool has_fn_lock;
    bool has_rgb_control;
    bool has_fan_curve;
    bool has_overclock;
    bool has_gpu_switch;
    int max_thermal_zones;
    int rgb_zones;
};

/* Legion laptop private data */
struct legion_laptop {
    struct platform_device *pdev;
    struct acpi_device *adev;

    enum legion_generation generation;
    struct legion_acpi_methods methods;
    struct legion_capabilities caps;

    /* Thermal management */
    struct thermal_zone_device **thermal_zones;
    int num_thermal_zones;

    /* Hardware monitoring */
    struct device *hwmon_dev;

    /* RGB LED control */
    struct led_classdev *rgb_leds;
    int num_rgb_zones;

    /* Current states */
    int thermal_mode;
    int legion_mode;
    bool battery_conservation;
    bool rapid_charge;
    bool fn_lock;

    /* Synchronization */
    struct mutex lock;

    /* Debugging */
    struct dentry *debug_dir;

    /* Workqueue for delayed operations */
    struct workqueue_struct *workqueue;
    struct delayed_work thermal_work;
};

/* Global data */
static struct legion_laptop *legion_device = NULL;

/* ACPI method mappings for different generations */
static struct legion_acpi_methods legion_gen6_methods = {
    .thermal_mode_method = "SPMO",
    .legion_mode_method = "SLMO",
    .battery_conservation_method = "SBCM",
    .rapid_charge_method = "QCHO",
    .fn_lock_method = "SFLM",
    .rgb_control_method = "WMI1",
    .fan_curve_method = "GFAN",
    .overclock_method = NULL
};

static struct legion_acpi_methods legion_gen7_methods = {
    .thermal_mode_method = "SPMO",
    .legion_mode_method = "SLMO",
    .battery_conservation_method = "SBCM",
    .rapid_charge_method = "QCHO",
    .fn_lock_method = "SFLM",
    .rgb_control_method = "WMI2",
    .fan_curve_method = "GFAN",
    .overclock_method = "OCGS"
};

static struct legion_acpi_methods legion_gen8_methods = {
    .thermal_mode_method = "SPMO",
    .legion_mode_method = "SLMO",
    .battery_conservation_method = "SBCM",
    .rapid_charge_method = "QCHO",
    .fn_lock_method = "SFLM",
    .rgb_control_method = "WMI3",
    .fan_curve_method = "GFCV",
    .overclock_method = "OCGS"
};

static struct legion_acpi_methods legion_gen9_methods = {
    .thermal_mode_method = "SPMO",
    .legion_mode_method = "SLMO",
    .battery_conservation_method = "SBCM",
    .rapid_charge_method = "QCHO",
    .fn_lock_method = "SFLM",
    .rgb_control_method = "WMI4",
    .fan_curve_method = "GFCV",
    .overclock_method = "OCGS"
};

/* DMI-based laptop detection */
static const struct dmi_system_id legion_laptop_ids[] = {
    /* Legion 5 series - Gen 6 */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82B1"),
        },
        .driver_data = (void*)LEGION_GEN_6,
    },
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82JU"),
        },
        .driver_data = (void*)LEGION_GEN_6,
    },
    /* Legion 5 Pro series - Gen 6 */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82JQ"),
        },
        .driver_data = (void*)LEGION_GEN_6,
    },
    /* Legion 7 series - Gen 6 */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82K6"),
        },
        .driver_data = (void*)LEGION_GEN_6,
    },
    /* Legion 7i Gen 7 */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82TD"),
        },
        .driver_data = (void*)LEGION_GEN_7,
    },
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_VERSION, "Legion 7i Gen 7"),
        },
        .driver_data = (void*)LEGION_GEN_7,
    },
    /* Legion 5 series - Gen 7 */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82RD"),
        },
        .driver_data = (void*)LEGION_GEN_7,
    },
    /* Legion 7 series - Gen 7 */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_NAME, "82UH"),
        },
        .driver_data = (void*)LEGION_GEN_7,
    },
    /* Legion Gen 8 models */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_VERSION, "Legion 5i Gen 8"),
        },
        .driver_data = (void*)LEGION_GEN_8,
    },
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_VERSION, "Legion 7i Gen 8"),
        },
        .driver_data = (void*)LEGION_GEN_8,
    },
    /* Legion Gen 9 models */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_VERSION, "Legion 5i Gen 9"),
        },
        .driver_data = (void*)LEGION_GEN_9,
    },
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_VERSION, "Legion 7i Gen 9"),
        },
        .driver_data = (void*)LEGION_GEN_9,
    },
    /* Catch-all for Legion laptops */
    {
        .matches = {
            DMI_MATCH(DMI_SYS_VENDOR, "LENOVO"),
            DMI_MATCH(DMI_PRODUCT_FAMILY, "Legion"),
        },
        .driver_data = (void*)LEGION_GEN_7, /* Default to Gen 7 */
    },
    {}
};

/* Helper function to call ACPI methods */
static int legion_call_acpi_method(struct acpi_device *adev, const char *method,
                                 int arg, int *result)
{
    union acpi_object arg_obj;
    struct acpi_object_list args;
    struct acpi_buffer output = { ACPI_ALLOCATE_BUFFER, NULL };
    union acpi_object *obj;
    acpi_status status;
    int ret = 0;

    if (!method) {
        return -ENODEV;
    }

    arg_obj.type = ACPI_TYPE_INTEGER;
    arg_obj.integer.value = arg;
    args.count = 1;
    args.pointer = &arg_obj;

    status = acpi_evaluate_object(adev->handle, (char *)method, &args, &output);
    if (ACPI_FAILURE(status)) {
        legion_dbg("ACPI method %s failed: %s\n", method, acpi_format_exception(status));
        return -EIO;
    }

    obj = output.pointer;
    if (obj && obj->type == ACPI_TYPE_INTEGER && result) {
        *result = (int)obj->integer.value;
    }

    kfree(output.pointer);

    return ret;
}

/* Thermal mode control */
static int legion_set_thermal_mode(struct legion_laptop *legion, int mode)
{
    int result;
    int ret;

    if (!legion->caps.has_thermal_control) {
        return -ENODEV;
    }

    mutex_lock(&legion->lock);
    ret = legion_call_acpi_method(legion->adev, legion->methods.thermal_mode_method,
                                mode, &result);
    if (ret == 0) {
        legion->thermal_mode = mode;
        legion_dbg("Thermal mode set to %d\n", mode);
    }
    mutex_unlock(&legion->lock);

    return ret;
}

static int legion_get_thermal_mode(struct legion_laptop *legion)
{
    int result;
    int ret;

    if (!legion->caps.has_thermal_control) {
        return -ENODEV;
    }

    mutex_lock(&legion->lock);
    ret = legion_call_acpi_method(legion->adev, legion->methods.thermal_mode_method,
                                -1, &result);
    if (ret == 0) {
        legion->thermal_mode = result;
    }
    mutex_unlock(&legion->lock);

    return ret == 0 ? result : ret;
}

/* Legion mode control */
static int legion_set_legion_mode(struct legion_laptop *legion, int mode)
{
    int result;
    int ret;

    if (!legion->caps.has_legion_mode) {
        return -ENODEV;
    }

    ret = legion_call_acpi_method(legion->adev, legion->methods.legion_mode_method,
                                mode, &result);
    if (ret == 0) {
        legion->legion_mode = mode;
        legion_dbg("Legion mode set to %d\n", mode);
    }

    return ret;
}

/* Battery conservation mode */
static int legion_set_battery_conservation(struct legion_laptop *legion, bool enable)
{
    int result;
    int ret;

    if (!legion->caps.has_battery_conservation) {
        return -ENODEV;
    }

    ret = legion_call_acpi_method(legion->adev, legion->methods.battery_conservation_method,
                                enable ? 1 : 0, &result);
    if (ret == 0) {
        legion->battery_conservation = enable;
        legion_dbg("Battery conservation %s\n", enable ? "enabled" : "disabled");
    }

    return ret;
}

/* Rapid charge control */
static int legion_set_rapid_charge(struct legion_laptop *legion, bool enable)
{
    int result;
    int ret;

    if (!legion->caps.has_rapid_charge) {
        return -ENODEV;
    }

    ret = legion_call_acpi_method(legion->adev, legion->methods.rapid_charge_method,
                                enable ? 1 : 0, &result);
    if (ret == 0) {
        legion->rapid_charge = enable;
        legion_dbg("Rapid charge %s\n", enable ? "enabled" : "disabled");
    }

    return ret;
}

/* Fn lock control */
static int legion_set_fn_lock(struct legion_laptop *legion, bool enable)
{
    int result;
    int ret;

    if (!legion->caps.has_fn_lock) {
        return -ENODEV;
    }

    ret = legion_call_acpi_method(legion->adev, legion->methods.fn_lock_method,
                                enable ? 1 : 0, &result);
    if (ret == 0) {
        legion->fn_lock = enable;
        legion_dbg("Fn lock %s\n", enable ? "enabled" : "disabled");
    }

    return ret;
}

/* Sysfs attribute implementations */
static ssize_t thermal_mode_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    int mode = legion_get_thermal_mode(legion);
    return mode >= 0 ? sprintf(buf, "%d\n", mode) : mode;
}

static ssize_t thermal_mode_store(struct device *dev, struct device_attribute *attr,
                                const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    int mode;
    int ret;

    ret = kstrtoint(buf, 10, &mode);
    if (ret)
        return ret;

    if (mode < 0 || mode > 3)
        return -EINVAL;

    ret = legion_set_thermal_mode(legion, mode);
    return ret ? ret : count;
}

static ssize_t legion_mode_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->legion_mode);
}

static ssize_t legion_mode_store(struct device *dev, struct device_attribute *attr,
                               const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    int mode;
    int ret;

    ret = kstrtoint(buf, 10, &mode);
    if (ret)
        return ret;

    if (mode < 0 || mode > 1)
        return -EINVAL;

    ret = legion_set_legion_mode(legion, mode);
    return ret ? ret : count;
}

static ssize_t battery_conservation_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->battery_conservation ? 1 : 0);
}

static ssize_t battery_conservation_store(struct device *dev, struct device_attribute *attr,
                                        const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    bool enable;
    int ret;

    ret = kstrtobool(buf, &enable);
    if (ret)
        return ret;

    ret = legion_set_battery_conservation(legion, enable);
    return ret ? ret : count;
}

static ssize_t rapid_charge_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->rapid_charge ? 1 : 0);
}

static ssize_t rapid_charge_store(struct device *dev, struct device_attribute *attr,
                                const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    bool enable;
    int ret;

    ret = kstrtobool(buf, &enable);
    if (ret)
        return ret;

    ret = legion_set_rapid_charge(legion, enable);
    return ret ? ret : count;
}

static ssize_t fn_lock_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->fn_lock ? 1 : 0);
}

static ssize_t fn_lock_store(struct device *dev, struct device_attribute *attr,
                            const char *buf, size_t count)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    bool enable;
    int ret;

    ret = kstrtobool(buf, &enable);
    if (ret)
        return ret;

    ret = legion_set_fn_lock(legion, enable);
    return ret ? ret : count;
}

static ssize_t generation_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "%d\n", legion->generation);
}

static ssize_t capabilities_show(struct device *dev, struct device_attribute *attr, char *buf)
{
    struct legion_laptop *legion = dev_get_drvdata(dev);
    return sprintf(buf, "thermal_control:%d legion_mode:%d battery_conservation:%d "
                       "rapid_charge:%d fn_lock:%d rgb_control:%d fan_curve:%d "
                       "overclock:%d gpu_switch:%d\n",
                   legion->caps.has_thermal_control ? 1 : 0,
                   legion->caps.has_legion_mode ? 1 : 0,
                   legion->caps.has_battery_conservation ? 1 : 0,
                   legion->caps.has_rapid_charge ? 1 : 0,
                   legion->caps.has_fn_lock ? 1 : 0,
                   legion->caps.has_rgb_control ? 1 : 0,
                   legion->caps.has_fan_curve ? 1 : 0,
                   legion->caps.has_overclock ? 1 : 0,
                   legion->caps.has_gpu_switch ? 1 : 0);
}

/* Device attributes */
static DEVICE_ATTR_RW(thermal_mode);
static DEVICE_ATTR_RW(legion_mode);
static DEVICE_ATTR_RW(battery_conservation);
static DEVICE_ATTR_RW(rapid_charge);
static DEVICE_ATTR_RW(fn_lock);
static DEVICE_ATTR_RO(generation);
static DEVICE_ATTR_RO(capabilities);

static struct attribute *legion_laptop_attrs[] = {
    &dev_attr_thermal_mode.attr,
    &dev_attr_legion_mode.attr,
    &dev_attr_battery_conservation.attr,
    &dev_attr_rapid_charge.attr,
    &dev_attr_fn_lock.attr,
    &dev_attr_generation.attr,
    &dev_attr_capabilities.attr,
    NULL
};

static const struct attribute_group legion_laptop_group = {
    .attrs = legion_laptop_attrs,
};

/* Initialize capabilities based on generation and available ACPI methods */
static void legion_init_capabilities(struct legion_laptop *legion)
{
    struct acpi_device *adev = legion->adev;

    /* Set default capabilities based on generation */
    switch (legion->generation) {
    case LEGION_GEN_6:
        legion->caps.has_thermal_control = true;
        legion->caps.has_legion_mode = true;
        legion->caps.has_battery_conservation = true;
        legion->caps.has_rapid_charge = true;
        legion->caps.has_fn_lock = true;
        legion->caps.has_rgb_control = true;
        legion->caps.has_fan_curve = false;
        legion->caps.has_overclock = false;
        legion->caps.has_gpu_switch = false;
        legion->caps.max_thermal_zones = 2;
        legion->caps.rgb_zones = 4;
        break;

    case LEGION_GEN_7:
        legion->caps.has_thermal_control = true;
        legion->caps.has_legion_mode = true;
        legion->caps.has_battery_conservation = true;
        legion->caps.has_rapid_charge = true;
        legion->caps.has_fn_lock = true;
        legion->caps.has_rgb_control = true;
        legion->caps.has_fan_curve = true;
        legion->caps.has_overclock = true;
        legion->caps.has_gpu_switch = true;
        legion->caps.max_thermal_zones = 3;
        legion->caps.rgb_zones = 4;
        break;

    case LEGION_GEN_8:
    case LEGION_GEN_9:
        legion->caps.has_thermal_control = true;
        legion->caps.has_legion_mode = true;
        legion->caps.has_battery_conservation = true;
        legion->caps.has_rapid_charge = true;
        legion->caps.has_fn_lock = true;
        legion->caps.has_rgb_control = true;
        legion->caps.has_fan_curve = true;
        legion->caps.has_overclock = true;
        legion->caps.has_gpu_switch = true;
        legion->caps.max_thermal_zones = 4;
        legion->caps.rgb_zones = 16; /* Per-key RGB in newer models */
        break;

    default:
        /* Conservative defaults for unknown models */
        legion->caps.has_thermal_control = false;
        legion->caps.has_legion_mode = false;
        legion->caps.has_battery_conservation = false;
        legion->caps.has_rapid_charge = false;
        legion->caps.has_fn_lock = false;
        legion->caps.has_rgb_control = false;
        legion->caps.has_fan_curve = false;
        legion->caps.has_overclock = false;
        legion->caps.has_gpu_switch = false;
        legion->caps.max_thermal_zones = 1;
        legion->caps.rgb_zones = 0;
        break;
    }

    /* Verify capabilities by checking if ACPI methods exist */
    if (legion->caps.has_thermal_control) {
        if (!acpi_has_method(adev->handle, legion->methods.thermal_mode_method)) {
            legion->caps.has_thermal_control = false;
            legion_dbg("Thermal control disabled - ACPI method not found\n");
        }
    }

    if (legion->caps.has_legion_mode) {
        if (!acpi_has_method(adev->handle, legion->methods.legion_mode_method)) {
            legion->caps.has_legion_mode = false;
            legion_dbg("Legion mode disabled - ACPI method not found\n");
        }
    }

    if (legion->caps.has_battery_conservation) {
        if (!acpi_has_method(adev->handle, legion->methods.battery_conservation_method)) {
            legion->caps.has_battery_conservation = false;
            legion_dbg("Battery conservation disabled - ACPI method not found\n");
        }
    }

    if (legion->caps.has_rapid_charge) {
        if (!acpi_has_method(adev->handle, legion->methods.rapid_charge_method)) {
            legion->caps.has_rapid_charge = false;
            legion_dbg("Rapid charge disabled - ACPI method not found\n");
        }
    }

    if (legion->caps.has_fn_lock) {
        if (!acpi_has_method(adev->handle, legion->methods.fn_lock_method)) {
            legion->caps.has_fn_lock = false;
            legion_dbg("Fn lock disabled - ACPI method not found\n");
        }
    }

    legion_info("Generation %d capabilities: thermal:%d legion:%d battery:%d rapid:%d fn:%d rgb:%d\n",
                legion->generation,
                legion->caps.has_thermal_control ? 1 : 0,
                legion->caps.has_legion_mode ? 1 : 0,
                legion->caps.has_battery_conservation ? 1 : 0,
                legion->caps.has_rapid_charge ? 1 : 0,
                legion->caps.has_fn_lock ? 1 : 0,
                legion->caps.has_rgb_control ? 1 : 0);
}

/* Initialize ACPI methods based on generation */
static void legion_init_methods(struct legion_laptop *legion)
{
    switch (legion->generation) {
    case LEGION_GEN_6:
        legion->methods = legion_gen6_methods;
        break;
    case LEGION_GEN_7:
        legion->methods = legion_gen7_methods;
        break;
    case LEGION_GEN_8:
        legion->methods = legion_gen8_methods;
        break;
    case LEGION_GEN_9:
        legion->methods = legion_gen9_methods;
        break;
    default:
        legion->methods = legion_gen7_methods; /* Default fallback */
        break;
    }
}

/* Detect laptop generation from DMI */
static enum legion_generation legion_detect_generation(void)
{
    const struct dmi_system_id *id;

    id = dmi_first_match(legion_laptop_ids);
    if (id) {
        return (enum legion_generation)(unsigned long)id->driver_data;
    }

    return LEGION_GEN_UNKNOWN;
}

/* Platform device probe */
static int legion_laptop_probe(struct platform_device *pdev)
{
    struct legion_laptop *legion;
    struct acpi_device *adev;
    int ret;

    legion_info("Probing Legion Enhanced driver v%s\n", LEGION_ENHANCED_VERSION);

    /* Find ACPI device */
    adev = ACPI_COMPANION(&pdev->dev);
    if (!adev) {
        legion_err("No ACPI device found\n");
        return -ENODEV;
    }

    /* Allocate private data */
    legion = devm_kzalloc(&pdev->dev, sizeof(*legion), GFP_KERNEL);
    if (!legion)
        return -ENOMEM;

    legion->pdev = pdev;
    legion->adev = adev;

    /* Initialize mutex for thread-safe access */
    mutex_init(&legion->lock);

    /* Detect generation */
    legion->generation = legion_detect_generation();
    if (legion->generation == LEGION_GEN_UNKNOWN && !force_load) {
        legion_err("Unknown Legion laptop model - use force_load=1 to override\n");
        return -ENODEV;
    }

    if (legion->generation == LEGION_GEN_UNKNOWN) {
        legion_warn("Unknown model detected, defaulting to Gen 7 methods\n");
        legion->generation = LEGION_GEN_7;
    }

    legion_info("Detected Legion Generation %d\n", legion->generation);

    /* Initialize methods and capabilities */
    legion_init_methods(legion);
    legion_init_capabilities(legion);

    /* Set platform device data */
    platform_set_drvdata(pdev, legion);
    dev_set_drvdata(&pdev->dev, legion);

    /* Create sysfs attributes */
    ret = sysfs_create_group(&pdev->dev.kobj, &legion_laptop_group);
    if (ret) {
        legion_err("Failed to create sysfs group: %d\n", ret);
        goto err_destroy_mutex;
    }

    /* Initialize current states */
    legion_get_thermal_mode(legion);

    /* Store global reference - no mutex needed here as probe is serialized */
    legion_device = legion;

    legion_info("Legion Enhanced driver loaded successfully\n");
    return 0;

err_destroy_mutex:
    mutex_destroy(&legion->lock);
    return ret;
}

/* Platform device remove */
static int legion_laptop_remove(struct platform_device *pdev)
{
    struct legion_laptop *legion = platform_get_drvdata(pdev);

    legion_info("Removing Legion Enhanced driver\n");

    /* Clear global reference first to prevent new accesses */
    legion_device = NULL;

    /* Remove sysfs attributes - this blocks until all sysfs operations complete */
    sysfs_remove_group(&pdev->dev.kobj, &legion_laptop_group);

    /* Destroy mutex after all users are done */
    if (legion) {
        mutex_destroy(&legion->lock);
    }

    return 0;
}

/* ACPI device IDs */
static const struct acpi_device_id legion_laptop_acpi_ids[] = {
    {"PNP0C09", 0}, /* Standard Embedded Controller */
    {"VPC2004", 0}, /* Legion WMI interface */
    {"", 0},
};
MODULE_DEVICE_TABLE(acpi, legion_laptop_acpi_ids);

/* Platform driver */
static struct platform_driver legion_laptop_driver = {
    .probe = legion_laptop_probe,
    .remove = legion_laptop_remove,
    .driver = {
        .name = LEGION_DRIVER_NAME,
        .acpi_match_table = legion_laptop_acpi_ids,
    },
};

/* Platform device */
static struct platform_device *legion_laptop_device;

/* Module init */
static int __init legion_laptop_init(void)
{
    int ret;

    legion_info("Loading Legion Enhanced driver v%s\n", LEGION_ENHANCED_VERSION);

    /* Check if this is a Legion laptop */
    if (legion_detect_generation() == LEGION_GEN_UNKNOWN && !force_load) {
        legion_info("Not a supported Legion laptop - use force_load=1 to override\n");
        return -ENODEV;
    }

    /* Register platform driver */
    ret = platform_driver_register(&legion_laptop_driver);
    if (ret) {
        legion_err("Failed to register platform driver: %d\n", ret);
        return ret;
    }

    /* Create platform device */
    legion_laptop_device = platform_device_alloc(LEGION_DRIVER_NAME, -1);
    if (!legion_laptop_device) {
        ret = -ENOMEM;
        goto err_unregister_driver;
    }

    /* Find and set ACPI companion */
    {
        struct acpi_device *adev = NULL;
        acpi_handle handle;
        acpi_status status;

        /* Look for Legion embedded controller using handle */
        status = acpi_get_handle(NULL, "\\_SB.PCI0.LPCB.EC0", &handle);
        if (ACPI_FAILURE(status)) {
            /* Try alternative EC path */
            status = acpi_get_handle(NULL, "\\_SB.EC0", &handle);
        }
        if (ACPI_FAILURE(status)) {
            /* Try PNP0C09 path */
            status = acpi_get_handle(NULL, "\\_SB.PCI0.LPCB.H_EC", &handle);
        }

        if (ACPI_SUCCESS(status)) {
            /* Get device from handle - kernel 6.8+ uses acpi_fetch_acpi_dev */
#if LINUX_VERSION_CODE >= KERNEL_VERSION(6,8,0)
            adev = acpi_fetch_acpi_dev(handle);
#else
            status = acpi_bus_get_device(handle, &adev);
            if (ACPI_FAILURE(status))
                adev = NULL;
#endif
            if (adev) {
                ACPI_COMPANION_SET(&legion_laptop_device->dev, adev);
                legion_dbg("Found ACPI EC device\n");
            }
        }

        if (!adev) {
            legion_warn("ACPI EC device not found - some features may not work\n");
        }
    }

    ret = platform_device_add(legion_laptop_device);
    if (ret) {
        legion_err("Failed to add platform device: %d\n", ret);
        goto err_free_device;
    }

    legion_info("Legion Enhanced driver initialized\n");
    return 0;

err_free_device:
    platform_device_put(legion_laptop_device);
err_unregister_driver:
    platform_driver_unregister(&legion_laptop_driver);
    return ret;
}

/* Module exit */
static void __exit legion_laptop_exit(void)
{
    legion_info("Unloading Legion Enhanced driver\n");

    if (legion_laptop_device) {
        platform_device_unregister(legion_laptop_device);
    }
    platform_driver_unregister(&legion_laptop_driver);

    legion_info("Legion Enhanced driver unloaded\n");
}

module_init(legion_laptop_init);
module_exit(legion_laptop_exit);

MODULE_AUTHOR("Vivek Chamoli <vivekchamoli@example.com>");
MODULE_DESCRIPTION("Enhanced Lenovo Legion laptop platform driver with backward compatibility");
MODULE_LICENSE("GPL v2");
MODULE_VERSION(LEGION_ENHANCED_VERSION);
MODULE_ALIAS("platform:" LEGION_DRIVER_NAME);