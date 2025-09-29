# Makefile for Legion Laptop Kernel Module
# Legion Slim 7i Gen 9 (16IRX9) Support

obj-m += legion_laptop_16irx9.o

# Module information
MODULE_NAME = legion_laptop_16irx9
MODULE_VERSION = 6.0.0

# Kernel build directory
KDIR ?= /lib/modules/$(shell uname -r)/build

# Build targets
all:
	$(MAKE) -C $(KDIR) M=$(PWD) modules

clean:
	$(MAKE) -C $(KDIR) M=$(PWD) clean
	rm -f Module.symvers modules.order

install: all
	$(MAKE) -C $(KDIR) M=$(PWD) modules_install
	depmod -a

load: install
	modprobe $(MODULE_NAME)

unload:
	modprobe -r $(MODULE_NAME) || true

reload: unload load

# DKMS support
dkms-install:
	dkms add .
	dkms build $(MODULE_NAME)/$(MODULE_VERSION)
	dkms install $(MODULE_NAME)/$(MODULE_VERSION)

dkms-uninstall:
	dkms remove $(MODULE_NAME)/$(MODULE_VERSION) --all || true

# Development targets
check:
	sparse $(src)/*.c

help:
	@echo "Available targets:"
	@echo "  all          - Build the kernel module"
	@echo "  clean        - Clean build files"
	@echo "  install      - Install the module"
	@echo "  load         - Load the module"
	@echo "  unload       - Unload the module"
	@echo "  reload       - Unload and load the module"
	@echo "  dkms-install - Install via DKMS"
	@echo "  dkms-uninstall - Remove via DKMS"
	@echo "  check        - Run static analysis"

.PHONY: all clean install load unload reload dkms-install dkms-uninstall check help