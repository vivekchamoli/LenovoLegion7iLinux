#!/bin/bash
# Enhanced Legion Kernel Module Compatibility Test Script
# Author: Vivek Chamoli
# Version: 2.0.0

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODULE_NAME="legion_laptop_enhanced"
MODULE_VERSION="2.0.0"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo "============================================="
echo "🧪 Legion Enhanced Kernel Module Test Suite"
echo "============================================="
echo ""

# Function to log test results
log_test() {
    local test_name="$1"
    local result="$2"
    local details="$3"

    if [ "$result" = "PASS" ]; then
        echo -e "${GREEN}✅ $test_name: PASS${NC}"
    elif [ "$result" = "FAIL" ]; then
        echo -e "${RED}❌ $test_name: FAIL${NC}"
    elif [ "$result" = "WARN" ]; then
        echo -e "${YELLOW}⚠️  $test_name: WARNING${NC}"
    elif [ "$result" = "INFO" ]; then
        echo -e "${BLUE}ℹ️  $test_name: INFO${NC}"
    fi

    if [ -n "$details" ]; then
        echo -e "   $details"
    fi
    echo ""
}

# Test 1: Kernel version compatibility
test_kernel_version() {
    echo -e "${CYAN}🔍 Testing Kernel Version Compatibility...${NC}"

    local kernel_version=$(uname -r)
    local kernel_major=$(echo $kernel_version | cut -d. -f1)
    local kernel_minor=$(echo $kernel_version | cut -d. -f2)

    log_test "Current Kernel Version" "INFO" "Running kernel: $kernel_version"

    # Check minimum supported version (5.4+)
    if [ "$kernel_major" -gt 5 ] || ([ "$kernel_major" -eq 5 ] && [ "$kernel_minor" -ge 4 ]); then
        log_test "Minimum Version Check" "PASS" "Kernel $kernel_version >= 5.4 (supported)"
    else
        log_test "Minimum Version Check" "FAIL" "Kernel $kernel_version < 5.4 (not supported)"
        return 1
    fi

    # Check maximum tested version (6.8)
    if [ "$kernel_major" -lt 6 ] || ([ "$kernel_major" -eq 6 ] && [ "$kernel_minor" -le 8 ]); then
        log_test "Maximum Version Check" "PASS" "Kernel $kernel_version <= 6.8 (tested)"
    else
        log_test "Maximum Version Check" "WARN" "Kernel $kernel_version > 6.8 (not extensively tested)"
    fi

    return 0
}

# Test 2: Build environment
test_build_environment() {
    echo -e "${CYAN}🔨 Testing Build Environment...${NC}"

    # Check for kernel headers
    local headers_path="/lib/modules/$(uname -r)/build"
    if [ -d "$headers_path" ]; then
        log_test "Kernel Headers" "PASS" "Found at: $headers_path"
    else
        log_test "Kernel Headers" "FAIL" "Not found at: $headers_path"
        echo -e "   ${YELLOW}Install with:${NC}"
        echo -e "   Ubuntu/Debian: sudo apt install linux-headers-\$(uname -r)"
        echo -e "   Fedora: sudo dnf install kernel-devel-\$(uname -r)"
        echo -e "   Arch: sudo pacman -S linux-headers"
        return 1
    fi

    # Check for build tools
    local tools=("make" "gcc" "ld")
    for tool in "${tools[@]}"; do
        if command -v "$tool" >/dev/null 2>&1; then
            log_test "Build Tool: $tool" "PASS" "$(which $tool)"
        else
            log_test "Build Tool: $tool" "FAIL" "Not found"
            return 1
        fi
    done

    # Check for DKMS
    if command -v dkms >/dev/null 2>&1; then
        local dkms_version=$(dkms --version 2>/dev/null | head -1)
        log_test "DKMS" "PASS" "$dkms_version"
    else
        log_test "DKMS" "WARN" "Not installed - manual build only"
    fi

    return 0
}

# Test 3: Module compilation
test_module_compilation() {
    echo -e "${CYAN}🔧 Testing Module Compilation...${NC}"

    if [ ! -f "Makefile" ]; then
        log_test "Makefile Present" "FAIL" "Makefile not found in current directory"
        return 1
    fi

    log_test "Makefile Present" "PASS" "Found Makefile"

    # Clean previous builds
    echo -e "   ${BLUE}Cleaning previous builds...${NC}"
    make clean >/dev/null 2>&1 || true

    # Attempt to build
    echo -e "   ${BLUE}Compiling module...${NC}"
    if make all >/dev/null 2>&1; then
        log_test "Module Compilation" "PASS" "Module compiled successfully"

        # Check if module file exists
        if [ -f "${MODULE_NAME}.ko" ]; then
            local module_size=$(stat -c%s "${MODULE_NAME}.ko" 2>/dev/null || echo "0")
            log_test "Module File Created" "PASS" "Size: $module_size bytes"
        else
            log_test "Module File Created" "FAIL" "Module file not found"
            return 1
        fi
    else
        log_test "Module Compilation" "FAIL" "Compilation failed"
        echo -e "   ${YELLOW}Run 'make all' for detailed error output${NC}"
        return 1
    fi

    return 0
}

# Test 4: Module information
test_module_info() {
    echo -e "${CYAN}📋 Testing Module Information...${NC}"

    if [ ! -f "${MODULE_NAME}.ko" ]; then
        log_test "Module Info" "FAIL" "Module file not found"
        return 1
    fi

    # Check module info
    local modinfo_output=$(modinfo "${MODULE_NAME}.ko" 2>/dev/null)
    if [ $? -eq 0 ]; then
        log_test "Module Info Available" "PASS" "modinfo succeeded"

        # Extract key information
        local version=$(echo "$modinfo_output" | grep "^version:" | cut -d: -f2 | xargs)
        local author=$(echo "$modinfo_output" | grep "^author:" | cut -d: -f2 | xargs)
        local description=$(echo "$modinfo_output" | grep "^description:" | cut -d: -f2 | xargs)
        local license=$(echo "$modinfo_output" | grep "^license:" | cut -d: -f2 | xargs)

        log_test "Module Version" "INFO" "$version"
        log_test "Module Author" "INFO" "$author"
        log_test "Module License" "INFO" "$license"

        # Check dependencies
        local depends=$(echo "$modinfo_output" | grep "^depends:" | cut -d: -f2 | xargs)
        if [ -n "$depends" ]; then
            log_test "Module Dependencies" "INFO" "$depends"
        else
            log_test "Module Dependencies" "INFO" "No dependencies"
        fi
    else
        log_test "Module Info Available" "FAIL" "modinfo failed"
        return 1
    fi

    return 0
}

# Test 5: ACPI compatibility
test_acpi_compatibility() {
    echo -e "${CYAN}🔌 Testing ACPI Compatibility...${NC}"

    # Check if this is a Legion laptop
    local dmi_vendor=$(cat /sys/class/dmi/id/sys_vendor 2>/dev/null || echo "unknown")
    local dmi_product=$(cat /sys/class/dmi/id/product_name 2>/dev/null || echo "unknown")
    local dmi_version=$(cat /sys/class/dmi/id/product_version 2>/dev/null || echo "unknown")

    log_test "System Vendor" "INFO" "$dmi_vendor"
    log_test "Product Name" "INFO" "$dmi_product"
    log_test "Product Version" "INFO" "$dmi_version"

    if [[ "$dmi_vendor" == *"LENOVO"* ]]; then
        log_test "Lenovo System" "PASS" "Detected Lenovo system"

        if [[ "$dmi_product" == *"Legion"* ]] || [[ "$dmi_version" == *"Legion"* ]]; then
            log_test "Legion Laptop" "PASS" "Detected Legion laptop"
        else
            log_test "Legion Laptop" "WARN" "Non-Legion Lenovo system detected"
        fi
    else
        log_test "Lenovo System" "WARN" "Non-Lenovo system detected"
    fi

    # Check for ACPI EC device
    if [ -d "/sys/class/dmi/id" ]; then
        log_test "DMI Interface" "PASS" "DMI sysfs interface available"
    else
        log_test "DMI Interface" "FAIL" "DMI sysfs interface not available"
    fi

    # Check for embedded controller
    if [ -d "/proc/acpi/ec" ] || [ -f "/proc/acpi/ec/EC0/info" ]; then
        log_test "ACPI EC" "PASS" "Embedded Controller interface available"
    else
        log_test "ACPI EC" "INFO" "Standard EC interface not found"
    fi

    return 0
}

# Test 6: Hardware interfaces
test_hardware_interfaces() {
    echo -e "${CYAN}🖥️  Testing Hardware Interfaces...${NC}"

    # Check thermal zones
    local thermal_count=0
    if [ -d "/sys/class/thermal" ]; then
        thermal_count=$(ls -1d /sys/class/thermal/thermal_zone* 2>/dev/null | wc -l)
        log_test "Thermal Zones" "INFO" "Found $thermal_count thermal zones"
    else
        log_test "Thermal Zones" "WARN" "Thermal sysfs interface not available"
    fi

    # Check hwmon interfaces
    local hwmon_count=0
    if [ -d "/sys/class/hwmon" ]; then
        hwmon_count=$(ls -1d /sys/class/hwmon/hwmon* 2>/dev/null | wc -l)
        log_test "Hardware Monitoring" "INFO" "Found $hwmon_count hwmon interfaces"
    else
        log_test "Hardware Monitoring" "WARN" "hwmon sysfs interface not available"
    fi

    # Check power supply interfaces
    local power_count=0
    if [ -d "/sys/class/power_supply" ]; then
        power_count=$(ls -1d /sys/class/power_supply/* 2>/dev/null | wc -l)
        log_test "Power Supply" "INFO" "Found $power_count power supply interfaces"
    else
        log_test "Power Supply" "WARN" "Power supply sysfs interface not available"
    fi

    # Check platform devices
    if [ -d "/sys/bus/platform/devices" ]; then
        local legion_devices=$(ls -1 /sys/bus/platform/devices/ 2>/dev/null | grep -i legion | wc -l)
        if [ "$legion_devices" -gt 0 ]; then
            log_test "Legion Platform Devices" "PASS" "Found $legion_devices Legion platform devices"
        else
            log_test "Legion Platform Devices" "INFO" "No existing Legion platform devices"
        fi
    fi

    return 0
}

# Test 7: Module loading (if root)
test_module_loading() {
    echo -e "${CYAN}📦 Testing Module Loading...${NC}"

    if [ "$EUID" -ne 0 ]; then
        log_test "Module Loading" "INFO" "Skipped (requires root privileges)"
        return 0
    fi

    if [ ! -f "${MODULE_NAME}.ko" ]; then
        log_test "Module Loading" "FAIL" "Module file not found"
        return 1
    fi

    # Check if module is already loaded
    if lsmod | grep -q "^${MODULE_NAME} "; then
        log_test "Module Already Loaded" "INFO" "Module is currently loaded"
        # Try to remove it first
        if rmmod "$MODULE_NAME" 2>/dev/null; then
            log_test "Module Removal" "PASS" "Successfully unloaded existing module"
        else
            log_test "Module Removal" "WARN" "Could not unload existing module"
        fi
    fi

    # Try to load the module
    echo -e "   ${BLUE}Attempting to load module...${NC}"
    if insmod "${MODULE_NAME}.ko" 2>/dev/null; then
        log_test "Module Loading" "PASS" "Module loaded successfully"

        # Check if module is actually loaded
        if lsmod | grep -q "^${MODULE_NAME} "; then
            log_test "Module Verification" "PASS" "Module appears in lsmod"

            # Check for sysfs interface
            if [ -d "/sys/kernel/legion_laptop" ]; then
                log_test "Sysfs Interface" "PASS" "Module sysfs interface created"

                # List available attributes
                local attrs=$(ls -1 /sys/kernel/legion_laptop/ 2>/dev/null | wc -l)
                log_test "Sysfs Attributes" "INFO" "Found $attrs attributes"
            else
                log_test "Sysfs Interface" "INFO" "No sysfs interface created"
            fi

            # Try to unload the module
            if rmmod "$MODULE_NAME" 2>/dev/null; then
                log_test "Module Unloading" "PASS" "Module unloaded successfully"
            else
                log_test "Module Unloading" "WARN" "Could not unload module"
            fi
        else
            log_test "Module Verification" "FAIL" "Module not found in lsmod"
        fi
    else
        log_test "Module Loading" "FAIL" "Failed to load module"
        echo -e "   ${YELLOW}Check dmesg for error details${NC}"
    fi

    return 0
}

# Test 8: Performance and resource usage
test_performance() {
    echo -e "${CYAN}⚡ Testing Performance Characteristics...${NC}"

    if [ ! -f "${MODULE_NAME}.ko" ]; then
        log_test "Performance Test" "FAIL" "Module file not found"
        return 1
    fi

    # Check module size
    local module_size=$(stat -c%s "${MODULE_NAME}.ko" 2>/dev/null || echo "0")
    local size_kb=$((module_size / 1024))

    if [ "$size_kb" -lt 100 ]; then
        log_test "Module Size" "PASS" "${size_kb}KB (optimal)"
    elif [ "$size_kb" -lt 500 ]; then
        log_test "Module Size" "PASS" "${size_kb}KB (acceptable)"
    else
        log_test "Module Size" "WARN" "${size_kb}KB (large)"
    fi

    # Check for debug symbols
    if objdump -h "${MODULE_NAME}.ko" 2>/dev/null | grep -q "debug"; then
        log_test "Debug Symbols" "INFO" "Debug symbols present (development build)"
    else
        log_test "Debug Symbols" "INFO" "No debug symbols (optimized build)"
    fi

    # Check compilation flags
    if strings "${MODULE_NAME}.ko" 2>/dev/null | grep -q "DEBUG"; then
        log_test "Debug Mode" "INFO" "Compiled with debug support"
    else
        log_test "Debug Mode" "INFO" "Compiled without debug support"
    fi

    return 0
}

# Main test execution
main() {
    local start_time=$(date +%s)
    local total_tests=0
    local passed_tests=0
    local failed_tests=0
    local warned_tests=0

    echo -e "${PURPLE}Starting compatibility test suite...${NC}"
    echo -e "Module: $MODULE_NAME v$MODULE_VERSION"
    echo -e "System: $(uname -a)"
    echo ""

    # Run all tests
    local tests=(
        "test_kernel_version"
        "test_build_environment"
        "test_module_compilation"
        "test_module_info"
        "test_acpi_compatibility"
        "test_hardware_interfaces"
        "test_module_loading"
        "test_performance"
    )

    for test in "${tests[@]}"; do
        if $test; then
            ((passed_tests++))
        else
            ((failed_tests++))
        fi
        ((total_tests++))
    done

    # Summary
    local end_time=$(date +%s)
    local duration=$((end_time - start_time))

    echo "============================================="
    echo -e "${PURPLE}🏁 Test Suite Summary${NC}"
    echo "============================================="
    echo -e "Total Tests: $total_tests"
    echo -e "${GREEN}Passed: $passed_tests${NC}"
    echo -e "${RED}Failed: $failed_tests${NC}"
    echo -e "${YELLOW}Warnings: $warned_tests${NC}"
    echo -e "Duration: ${duration}s"
    echo ""

    if [ "$failed_tests" -eq 0 ]; then
        echo -e "${GREEN}✅ All tests passed! Module is compatible with this system.${NC}"
        echo ""
        echo -e "${CYAN}📋 Next Steps:${NC}"
        echo "1. Install with DKMS: sudo dkms install ."
        echo "2. Load module: sudo modprobe $MODULE_NAME"
        echo "3. Check status: lsmod | grep $MODULE_NAME"
        return 0
    else
        echo -e "${RED}❌ Some tests failed. Please review the issues above.${NC}"
        echo ""
        echo -e "${CYAN}📋 Troubleshooting:${NC}"
        echo "1. Install missing dependencies"
        echo "2. Check kernel headers: sudo apt install linux-headers-\$(uname -r)"
        echo "3. Review build errors: make clean && make all"
        return 1
    fi
}

# Check if we're in the right directory
if [ ! -f "dkms.conf" ] && [ ! -f "Makefile" ]; then
    echo -e "${RED}❌ Please run this script from the kernel module directory${NC}"
    echo "Expected files: dkms.conf, Makefile, ${MODULE_NAME}.c"
    exit 1
fi

# Run main function
main "$@"