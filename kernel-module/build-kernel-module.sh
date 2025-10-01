#!/bin/bash
# Elite Legion Kernel Module Build System
# Automated kernel module compilation with comprehensive compatibility detection
# Author: Vivek Chamoli
# Version: 2.0.0

set -e

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

# Module configuration
MODULE_NAME="legion_laptop_enhanced"
MODULE_VERSION="2.0.0"
MIN_KERNEL_MAJOR=5
MIN_KERNEL_MINOR=4

echo -e "${BLUE}╔═══════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║   Elite Legion Kernel Module Build System v${MODULE_VERSION}        ║${NC}"
echo -e "${BLUE}╚═══════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# ============================================================================
# KERNEL ENVIRONMENT DETECTION
# ============================================================================

detect_kernel_environment() {
    echo -e "${CYAN}🔍 Detecting Kernel Build Environment${NC}"
    echo "─────────────────────────────────────────"

    # Get kernel version
    KERNEL_VERSION=$(uname -r)
    KERNEL_MAJOR=$(echo "$KERNEL_VERSION" | cut -d. -f1)
    KERNEL_MINOR=$(echo "$KERNEL_VERSION" | cut -d. -f2)
    KERNEL_PATCH=$(echo "$KERNEL_VERSION" | cut -d. -f3 | cut -d- -f1)

    echo -e "  Kernel Version: ${GREEN}$KERNEL_VERSION${NC}"
    echo -e "  Major.Minor.Patch: ${GREEN}${KERNEL_MAJOR}.${KERNEL_MINOR}.${KERNEL_PATCH}${NC}"

    # Check kernel version compatibility
    if [ "$KERNEL_MAJOR" -lt "$MIN_KERNEL_MAJOR" ] || \
       [ "$KERNEL_MAJOR" -eq "$MIN_KERNEL_MAJOR" -a "$KERNEL_MINOR" -lt "$MIN_KERNEL_MINOR" ]; then
        echo -e "  ${RED}✗ Kernel version too old (minimum: ${MIN_KERNEL_MAJOR}.${MIN_KERNEL_MINOR})${NC}"
        return 1
    else
        echo -e "  ${GREEN}✓ Kernel version compatible${NC}"
    fi

    # Detect kernel build directory
    KERNEL_BUILD="/lib/modules/$KERNEL_VERSION/build"

    if [ -d "$KERNEL_BUILD" ]; then
        echo -e "  ${GREEN}✓ Kernel headers found: $KERNEL_BUILD${NC}"
    else
        echo -e "  ${RED}✗ Kernel headers not found${NC}"
        echo -e "  ${YELLOW}Install with one of:${NC}"

        # Detect distribution and provide specific instructions
        if [ -f /etc/os-release ]; then
            . /etc/os-release
            case "$ID" in
                ubuntu|debian|linuxmint)
                    echo -e "    ${CYAN}sudo apt install linux-headers-$(uname -r)${NC}"
                    ;;
                fedora|rhel|centos|rocky|almalinux)
                    echo -e "    ${CYAN}sudo dnf install kernel-devel-$(uname -r)${NC}"
                    ;;
                arch|manjaro|endeavouros)
                    echo -e "    ${CYAN}sudo pacman -S linux-headers${NC}"
                    ;;
                opensuse*)
                    echo -e "    ${CYAN}sudo zypper install kernel-devel${NC}"
                    ;;
                *)
                    echo -e "    ${CYAN}Install kernel-headers or kernel-devel for your distribution${NC}"
                    ;;
            esac
        fi
        return 1
    fi

    # Check for required build tools
    echo ""
    echo -e "${CYAN}🔨 Checking Build Tools${NC}"
    echo "─────────────────────────────────────────"

    local missing_tools=()

    for tool in make gcc ld; do
        if command -v $tool &> /dev/null; then
            local version=$($tool --version 2>/dev/null | head -1)
            echo -e "  ${GREEN}✓ $tool${NC}: $version"
        else
            echo -e "  ${RED}✗ $tool not found${NC}"
            missing_tools+=($tool)
        fi
    done

    if [ ${#missing_tools[@]} -gt 0 ]; then
        echo -e "\n  ${RED}Missing build tools: ${missing_tools[*]}${NC}"
        echo -e "  ${YELLOW}Install build essentials:${NC}"
        if [ -f /etc/os-release ]; then
            . /etc/os-release
            case "$ID" in
                ubuntu|debian|linuxmint)
                    echo -e "    ${CYAN}sudo apt install build-essential${NC}"
                    ;;
                fedora|rhel|centos|rocky|almalinux)
                    echo -e "    ${CYAN}sudo dnf groupinstall 'Development Tools'${NC}"
                    ;;
                arch|manjaro|endeavouros)
                    echo -e "    ${CYAN}sudo pacman -S base-devel${NC}"
                    ;;
            esac
        fi
        return 1
    fi

    echo ""
    return 0
}

# ============================================================================
# KERNEL MODULE COMPILATION
# ============================================================================

compile_kernel_module() {
    echo -e "${CYAN}⚙️  Compiling Kernel Module${NC}"
    echo "─────────────────────────────────────────"

    # Clean previous builds
    echo -e "  ${BLUE}Cleaning previous builds...${NC}"
    make clean &> /dev/null || true

    # Build module
    echo -e "  ${BLUE}Building ${MODULE_NAME}.ko...${NC}"

    if make all KERNEL_VERSION="$KERNEL_VERSION" 2>&1 | tee /tmp/legion_build.log; then
        echo -e "  ${GREEN}✓ Module compiled successfully${NC}"

        # Verify .ko file
        if [ -f "${MODULE_NAME}.ko" ]; then
            local module_size=$(stat -f%z "${MODULE_NAME}.ko" 2>/dev/null || stat -c%s "${MODULE_NAME}.ko")
            echo -e "  ${GREEN}✓ Module file created: ${MODULE_NAME}.ko ($(numfmt --to=iec-i --suffix=B $module_size 2>/dev/null || echo $module_size bytes))${NC}"

            # Get module info
            if command -v modinfo &> /dev/null; then
                echo -e "\n  ${CYAN}Module Information:${NC}"
                modinfo "${MODULE_NAME}.ko" | grep -E "^(filename|version|description|author|depends):" | sed 's/^/    /'
            fi
            return 0
        else
            echo -e "  ${RED}✗ Module file not created${NC}"
            return 1
        fi
    else
        echo -e "  ${RED}✗ Compilation failed${NC}"
        echo -e "  ${YELLOW}Check /tmp/legion_build.log for details${NC}"
        return 1
    fi
}

# ============================================================================
# DKMS INTEGRATION
# ============================================================================

setup_dkms() {
    echo -e "${CYAN}📦 Setting up DKMS Integration${NC}"
    echo "─────────────────────────────────────────"

    # Check if DKMS is installed
    if ! command -v dkms &> /dev/null; then
        echo -e "  ${YELLOW}⚠ DKMS not installed${NC}"
        echo -e "  ${BLUE}Install DKMS for automatic kernel updates:${NC}"

        if [ -f /etc/os-release ]; then
            . /etc/os-release
            case "$ID" in
                ubuntu|debian|linuxmint)
                    echo -e "    ${CYAN}sudo apt install dkms${NC}"
                    ;;
                fedora|rhel|centos|rocky|almalinux)
                    echo -e "    ${CYAN}sudo dnf install dkms${NC}"
                    ;;
                arch|manjaro|endeavouros)
                    echo -e "    ${CYAN}sudo pacman -S dkms${NC}"
                    ;;
            esac
        fi
        return 1
    fi

    echo -e "  ${GREEN}✓ DKMS found: $(dkms --version | head -1)${NC}"

    # Remove old DKMS versions if they exist
    if dkms status | grep -q "${MODULE_NAME}"; then
        echo -e "  ${YELLOW}Removing existing DKMS registration...${NC}"
        sudo dkms remove ${MODULE_NAME}/${MODULE_VERSION} --all 2>/dev/null || true
    fi

    # Create DKMS source directory
    local dkms_dir="/usr/src/${MODULE_NAME}-${MODULE_VERSION}"
    echo -e "  ${BLUE}Creating DKMS source directory: $dkms_dir${NC}"

    sudo mkdir -p "$dkms_dir"
    sudo cp -r * "$dkms_dir/" 2>/dev/null || true

    # Register with DKMS
    echo -e "  ${BLUE}Registering with DKMS...${NC}"
    if sudo dkms add -m ${MODULE_NAME} -v ${MODULE_VERSION}; then
        echo -e "  ${GREEN}✓ DKMS registration successful${NC}"

        # Build with DKMS
        echo -e "  ${BLUE}Building with DKMS...${NC}"
        if sudo dkms build -m ${MODULE_NAME} -v ${MODULE_VERSION}; then
            echo -e "  ${GREEN}✓ DKMS build successful${NC}"

            # Install with DKMS
            echo -e "  ${BLUE}Installing with DKMS...${NC}"
            if sudo dkms install -m ${MODULE_NAME} -v ${MODULE_VERSION}; then
                echo -e "  ${GREEN}✓ DKMS installation successful${NC}"
                echo -e "  ${GREEN}✓ Module will be automatically rebuilt on kernel updates${NC}"
                return 0
            fi
        fi
    fi

    echo -e "  ${YELLOW}⚠ DKMS setup incomplete (manual installation will be used)${NC}"
    return 1
}

# ============================================================================
# MANUAL INSTALLATION
# ============================================================================

install_kernel_module() {
    echo -e "${CYAN}📥 Installing Kernel Module${NC}"
    echo "─────────────────────────────────────────"

    if [ ! -f "${MODULE_NAME}.ko" ]; then
        echo -e "  ${RED}✗ Module file not found${NC}"
        return 1
    fi

    # Check for root privileges
    if [ "$EUID" -ne 0 ]; then
        echo -e "  ${YELLOW}⚠ Root privileges required for installation${NC}"
        echo -e "  ${BLUE}Run with: sudo ./build-kernel-module.sh --install${NC}"
        return 1
    fi

    # Install module
    echo -e "  ${BLUE}Installing to /lib/modules/$KERNEL_VERSION/extra/${NC}"
    make install KERNEL_VERSION="$KERNEL_VERSION"

    # Update module dependencies
    echo -e "  ${BLUE}Updating module dependencies...${NC}"
    /sbin/depmod -a "$KERNEL_VERSION"

    echo -e "  ${GREEN}✓ Module installed successfully${NC}"

    # Try to load the module
    echo -e "  ${BLUE}Loading module...${NC}"
    if modprobe ${MODULE_NAME}; then
        echo -e "  ${GREEN}✓ Module loaded successfully${NC}"

        # Show module status
        if lsmod | grep -q ${MODULE_NAME}; then
            echo -e "  ${GREEN}✓ Module is active${NC}"
        fi
    else
        echo -e "  ${YELLOW}⚠ Module installed but not loaded (may require reboot or compatible hardware)${NC}"
    fi

    return 0
}

# ============================================================================
# MODULE TESTING
# ============================================================================

test_kernel_module() {
    echo -e "${CYAN}🧪 Testing Kernel Module${NC}"
    echo "─────────────────────────────────────────"

    # Check if module can be loaded (dry run)
    if command -v modprobe &> /dev/null; then
        echo -e "  ${BLUE}Testing module load (dry run)...${NC}"
        if modprobe --dry-run --first-time ${MODULE_NAME} 2>/dev/null; then
            echo -e "  ${GREEN}✓ Module dependencies satisfied${NC}"
        else
            echo -e "  ${YELLOW}⚠ Module may have dependency issues${NC}"
        fi
    fi

    # Check module symbols
    if [ -f "${MODULE_NAME}.ko" ]; then
        echo -e "  ${BLUE}Checking module symbols...${NC}"
        if nm "${MODULE_NAME}.ko" | grep -q "init_module"; then
            echo -e "  ${GREEN}✓ Module has required entry points${NC}"
        fi
    fi

    # Verify module signature (if kernel requires it)
    if grep -q "CONFIG_MODULE_SIG_FORCE=y" "/boot/config-$KERNEL_VERSION" 2>/dev/null; then
        echo -e "  ${YELLOW}⚠ Kernel requires signed modules${NC}"
        echo -e "  ${BLUE}You may need to disable Secure Boot or sign the module${NC}"
    fi
}

# ============================================================================
# MAIN BUILD FLOW
# ============================================================================

main() {
    local install_mode=false
    local dkms_mode=false
    local test_only=false

    # Parse arguments
    for arg in "$@"; do
        case $arg in
            --install|-i)
                install_mode=true
                ;;
            --dkms|-d)
                dkms_mode=true
                ;;
            --test|-t)
                test_only=true
                ;;
            --help|-h)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --install, -i    Build and install module"
                echo "  --dkms, -d       Setup DKMS integration"
                echo "  --test, -t       Build and test only (no install)"
                echo "  --help, -h       Show this help"
                echo ""
                echo "Examples:"
                echo "  $0              # Build only"
                echo "  $0 --test       # Build and test"
                echo "  $0 --install    # Build and install"
                echo "  $0 --dkms       # Setup DKMS (recommended)"
                exit 0
                ;;
        esac
    done

    # Step 1: Detect environment
    if ! detect_kernel_environment; then
        echo -e "\n${RED}✗ Environment check failed${NC}"
        echo -e "${YELLOW}Please install required dependencies and try again${NC}"
        exit 1
    fi

    echo ""

    # Step 2: Compile module
    if ! compile_kernel_module; then
        echo -e "\n${RED}✗ Build failed${NC}"
        exit 1
    fi

    echo ""

    # Step 3: Test module
    if [ "$test_only" = true ] || [ "$install_mode" = true ] || [ "$dkms_mode" = true ]; then
        test_kernel_module
        echo ""
    fi

    # Step 4: Install or setup DKMS
    if [ "$dkms_mode" = true ]; then
        if setup_dkms; then
            echo -e "\n${GREEN}╔═══════════════════════════════════════════════════════════════╗${NC}"
            echo -e "${GREEN}║          DKMS Setup Completed Successfully!                 ║${NC}"
            echo -e "${GREEN}╚═══════════════════════════════════════════════════════════════╝${NC}"
        else
            echo -e "\n${YELLOW}DKMS setup incomplete, falling back to manual install${NC}"
            install_mode=true
        fi
    fi

    if [ "$install_mode" = true ]; then
        if install_kernel_module; then
            echo -e "\n${GREEN}╔═══════════════════════════════════════════════════════════════╗${NC}"
            echo -e "${GREEN}║       Module Installation Completed Successfully!           ║${NC}"
            echo -e "${GREEN}╚═══════════════════════════════════════════════════════════════╝${NC}"
        else
            echo -e "\n${RED}✗ Installation failed${NC}"
            exit 1
        fi
    fi

    if [ "$test_only" = false ] && [ "$install_mode" = false ] && [ "$dkms_mode" = false ]; then
        echo -e "${GREEN}╔═══════════════════════════════════════════════════════════════╗${NC}"
        echo -e "${GREEN}║          Module Build Completed Successfully!               ║${NC}"
        echo -e "${GREEN}╚═══════════════════════════════════════════════════════════════╝${NC}"
        echo ""
        echo -e "${BLUE}Next steps:${NC}"
        echo -e "  • Install module: ${CYAN}sudo ./build-kernel-module.sh --install${NC}"
        echo -e "  • Setup DKMS: ${CYAN}sudo ./build-kernel-module.sh --dkms${NC} (recommended)"
        echo -e "  • Load module: ${CYAN}sudo modprobe ${MODULE_NAME}${NC}"
    fi

    echo ""
}

# Run main function
main "$@"
