# Enhanced Legion Laptop Kernel Module Makefile
# Author: Vivek Chamoli
# Version: 2.0.0

obj-m += legion_laptop_enhanced.o

legion_laptop_enhanced-objs := enhanced_legion_laptop.o

# Kernel build directory detection
KERNEL_VERSION ?= $(shell uname -r)
KERNEL_BUILD ?= /lib/modules/$(KERNEL_VERSION)/build
PWD := $(shell pwd)

# Compiler flags for different kernel versions
ccflags-y := -DLEGION_ENHANCED_VERSION=\"2.0.0\"

# Kernel version compatibility checks
KERNEL_MAJOR := $(shell echo $(KERNEL_VERSION) | cut -d. -f1)
KERNEL_MINOR := $(shell echo $(KERNEL_VERSION) | cut -d. -f2)
KERNEL_PATCH := $(shell echo $(KERNEL_VERSION) | cut -d. -f3)

# Version-specific compilation flags
ifeq ($(shell test $(KERNEL_MAJOR) -eq 5 -a $(KERNEL_MINOR) -lt 10; echo $$?),0)
    ccflags-y += -DLEGION_COMPAT_OLD_THERMAL
endif

ifeq ($(shell test $(KERNEL_MAJOR) -eq 5 -a $(KERNEL_MINOR) -lt 15; echo $$?),0)
    ccflags-y += -DLEGION_COMPAT_OLD_PLATFORM
endif

ifeq ($(shell test $(KERNEL_MAJOR) -ge 6; echo $$?),0)
    ccflags-y += -DLEGION_COMPAT_NEW_KERNEL
endif

# Build targets
all:
	@echo "Building Legion Enhanced Kernel Module for kernel $(KERNEL_VERSION)"
	@echo "Kernel build directory: $(KERNEL_BUILD)"
	@if [ ! -d "$(KERNEL_BUILD)" ]; then \
		echo "Error: Kernel build directory not found. Install kernel headers."; \
		exit 1; \
	fi
	$(MAKE) -C $(KERNEL_BUILD) M=$(PWD) modules

clean:
	@echo "Cleaning Legion Enhanced Kernel Module"
	$(MAKE) -C $(KERNEL_BUILD) M=$(PWD) clean
	rm -f *.ko *.o *.mod.c *.mod.o *.symvers *.order .*.cmd
	rm -rf .tmp_versions/

install: all
	@echo "Installing Legion Enhanced Kernel Module"
	$(MAKE) -C $(KERNEL_BUILD) M=$(PWD) modules_install
	/sbin/depmod -a $(KERNEL_VERSION)

uninstall:
	@echo "Uninstalling Legion Enhanced Kernel Module"
	rm -f /lib/modules/$(KERNEL_VERSION)/extra/legion_laptop_enhanced.ko
	/sbin/depmod -a $(KERNEL_VERSION)

# DKMS integration
dkms-install:
	@echo "Installing via DKMS"
	dkms add . -m legion-laptop-enhanced -v 2.0.0
	dkms build -m legion-laptop-enhanced -v 2.0.0
	dkms install -m legion-laptop-enhanced -v 2.0.0

dkms-uninstall:
	@echo "Uninstalling via DKMS"
	dkms remove legion-laptop-enhanced/2.0.0 --all
	dkms remove legion-laptop-enhanced -v 2.0.0

# Development targets
check:
	@echo "Checking kernel module compatibility"
	@echo "Kernel version: $(KERNEL_VERSION)"
	@echo "Kernel build: $(KERNEL_BUILD)"
	@if [ -d "$(KERNEL_BUILD)" ]; then \
		echo "✓ Kernel headers found"; \
	else \
		echo "✗ Kernel headers missing"; \
	fi

debug: ccflags-y += -DDEBUG -g
debug: all

help:
	@echo "Available targets:"
	@echo "  all          - Build the kernel module"
	@echo "  clean        - Clean build artifacts"
	@echo "  install      - Install the module"
	@echo "  uninstall    - Uninstall the module"
	@echo "  dkms-install - Install via DKMS"
	@echo "  dkms-uninstall - Uninstall via DKMS"
	@echo "  check        - Check build environment"
	@echo "  debug        - Build with debug symbols"
	@echo "  help         - Show this help"

.PHONY: all clean install uninstall dkms-install dkms-uninstall check debug help