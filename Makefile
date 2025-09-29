# Legion Toolkit Makefile for Ubuntu/Linux
# Usage: make [target]

# Variables
DOTNET = dotnet
PROJECT = LenovoLegionToolkit.Avalonia/LenovoLegionToolkit.Avalonia.csproj
PUBLISH_DIR = publish
BUILD_DIR = build
VERSION = $(shell grep -oP '(?<=<Version>)[^<]+' $(PROJECT) || echo "3.0.0")
DESTDIR ?=
PREFIX ?= /usr/local
BINDIR = $(PREFIX)/bin
LIBDIR = $(PREFIX)/lib/legion-toolkit
DATADIR = $(PREFIX)/share
SYSCONFDIR = /etc

# Build configuration
RUNTIME = linux-x64
CONFIG = Release

# Colors
RED = \033[0;31m
GREEN = \033[0;32m
YELLOW = \033[1;33m
BLUE = \033[0;34m
NC = \033[0m

.PHONY: all build clean install uninstall package test run help

# Default target
all: build

# Display help
help:
	@echo "$(BLUE)Legion Toolkit Makefile$(NC)"
	@echo ""
	@echo "Usage: make [target]"
	@echo ""
	@echo "Targets:"
	@echo "  $(GREEN)build$(NC)      - Build the application"
	@echo "  $(GREEN)clean$(NC)      - Clean build artifacts"
	@echo "  $(GREEN)install$(NC)    - Install to system"
	@echo "  $(GREEN)uninstall$(NC)  - Remove from system"
	@echo "  $(GREEN)package$(NC)    - Create .deb package"
	@echo "  $(GREEN)test$(NC)       - Run tests"
	@echo "  $(GREEN)run$(NC)        - Run the application"
	@echo ""
	@echo "Variables:"
	@echo "  PREFIX=$(PREFIX)"
	@echo "  DESTDIR=$(DESTDIR)"

# Build the application
build:
	@echo "$(GREEN)Building Legion Toolkit v$(VERSION)...$(NC)"
	@mkdir -p $(BUILD_DIR)
	$(DOTNET) publish $(PROJECT) \
		-c $(CONFIG) \
		-r $(RUNTIME) \
		--self-contained true \
		-p:PublishSingleFile=true \
		-p:PublishTrimmed=true \
		-p:TrimMode=link \
		-o $(BUILD_DIR)
	@echo "$(GREEN)✓ Build complete$(NC)"

# Clean build artifacts
clean:
	@echo "$(YELLOW)Cleaning build artifacts...$(NC)"
	@rm -rf $(BUILD_DIR)
	@rm -rf $(PUBLISH_DIR)
	@rm -rf LenovoLegionToolkit.Avalonia/bin
	@rm -rf LenovoLegionToolkit.Avalonia/obj
	@rm -rf LenovoLegionToolkit.Avalonia.Tests/bin
	@rm -rf LenovoLegionToolkit.Avalonia.Tests/obj
	@echo "$(GREEN)✓ Clean complete$(NC)"

# Install to system
install: build
	@echo "$(GREEN)Installing Legion Toolkit...$(NC)"

	# Create directories
	@install -d $(DESTDIR)$(BINDIR)
	@install -d $(DESTDIR)$(LIBDIR)
	@install -d $(DESTDIR)$(DATADIR)/applications
	@install -d $(DESTDIR)$(DATADIR)/icons/hicolor/256x256/apps
	@install -d $(DESTDIR)$(DATADIR)/man/man1
	@install -d $(DESTDIR)$(SYSCONFDIR)/systemd/system
	@install -d $(DESTDIR)$(SYSCONFDIR)/xdg/autostart

	# Install binary
	@install -m 755 $(BUILD_DIR)/LegionToolkit $(DESTDIR)$(BINDIR)/legion-toolkit

	# Create wrapper script
	@echo '#!/bin/bash' > $(DESTDIR)$(BINDIR)/legion-toolkit-gui
	@echo 'export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1' >> $(DESTDIR)$(BINDIR)/legion-toolkit-gui
	@echo 'exec $(BINDIR)/legion-toolkit "$$@"' >> $(DESTDIR)$(BINDIR)/legion-toolkit-gui
	@chmod 755 $(DESTDIR)$(BINDIR)/legion-toolkit-gui

	# Install desktop file
	@cat > $(DESTDIR)$(DATADIR)/applications/legion-toolkit.desktop << EOF
[Desktop Entry]
Name=Legion Toolkit
Comment=Control Lenovo Legion laptop features
Exec=$(BINDIR)/legion-toolkit-gui
Icon=legion-toolkit
Terminal=false
Type=Application
Categories=System;Settings;HardwareSettings;
Keywords=legion;lenovo;laptop;power;battery;rgb;fan;thermal;
EOF

	# Install systemd service
	@cat > $(DESTDIR)$(SYSCONFDIR)/systemd/system/legion-toolkit.service << EOF
[Unit]
Description=Legion Toolkit Daemon
After=multi-user.target

[Service]
Type=simple
ExecStart=$(BINDIR)/legion-toolkit daemon start
Restart=always
User=root

[Install]
WantedBy=multi-user.target
EOF

	# Install autostart
	@cp $(DESTDIR)$(DATADIR)/applications/legion-toolkit.desktop \
	     $(DESTDIR)$(SYSCONFDIR)/xdg/autostart/

	# Install man page
	@if [ -f publish/legion-toolkit.man ]; then \
		install -m 644 publish/legion-toolkit.man $(DESTDIR)$(DATADIR)/man/man1/legion-toolkit.1; \
	fi

	# Install icons
	@if [ -d LenovoLegionToolkit.Avalonia/Assets ]; then \
		cp -r LenovoLegionToolkit.Avalonia/Assets/* $(DESTDIR)$(DATADIR)/icons/hicolor/256x256/apps/ 2>/dev/null || true; \
	fi

	@echo "$(GREEN)✓ Installation complete$(NC)"
	@echo ""
	@echo "To complete setup:"
	@echo "  1. Reload systemd: sudo systemctl daemon-reload"
	@echo "  2. Enable service: sudo systemctl enable legion-toolkit"
	@echo "  3. Start service:  sudo systemctl start legion-toolkit"

# Uninstall from system
uninstall:
	@echo "$(YELLOW)Uninstalling Legion Toolkit...$(NC)"

	# Stop and disable service
	@systemctl stop legion-toolkit 2>/dev/null || true
	@systemctl disable legion-toolkit 2>/dev/null || true

	# Remove files
	@rm -f $(DESTDIR)$(BINDIR)/legion-toolkit
	@rm -f $(DESTDIR)$(BINDIR)/legion-toolkit-gui
	@rm -rf $(DESTDIR)$(LIBDIR)
	@rm -f $(DESTDIR)$(DATADIR)/applications/legion-toolkit.desktop
	@rm -f $(DESTDIR)$(DATADIR)/man/man1/legion-toolkit.1
	@rm -f $(DESTDIR)$(SYSCONFDIR)/systemd/system/legion-toolkit.service
	@rm -f $(DESTDIR)$(SYSCONFDIR)/xdg/autostart/legion-toolkit.desktop

	# Reload systemd
	@systemctl daemon-reload 2>/dev/null || true

	@echo "$(GREEN)✓ Uninstall complete$(NC)"

# Create Debian package
package: build
	@echo "$(GREEN)Creating Debian package...$(NC)"
	@bash Scripts/build-ubuntu-package.sh
	@echo "$(GREEN)✓ Package created$(NC)"

# Run tests
test:
	@echo "$(GREEN)Running tests...$(NC)"
	@$(DOTNET) test LenovoLegionToolkit.Avalonia.Tests/LenovoLegionToolkit.Avalonia.Tests.csproj
	@echo "$(GREEN)✓ Tests complete$(NC)"

# Run the application
run: build
	@echo "$(GREEN)Starting Legion Toolkit...$(NC)"
	@$(BUILD_DIR)/LegionToolkit

# Install dependencies (Ubuntu/Debian)
deps:
	@echo "$(GREEN)Installing dependencies...$(NC)"
	@sudo apt-get update
	@sudo apt-get install -y \
		dotnet-sdk-8.0 \
		libx11-6 \
		libice6 \
		libsm6 \
		libfontconfig1 \
		acpi \
		acpid \
		pciutils \
		usbutils \
		lm-sensors \
		fancontrol
	@echo "$(GREEN)✓ Dependencies installed$(NC)"

# Development build
dev:
	@echo "$(GREEN)Building for development...$(NC)"
	@$(DOTNET) build $(PROJECT) -c Debug
	@echo "$(GREEN)✓ Development build complete$(NC)"

# Format code
format:
	@echo "$(GREEN)Formatting code...$(NC)"
	@$(DOTNET) format
	@echo "$(GREEN)✓ Code formatted$(NC)"

# Check for updates
update:
	@echo "$(GREEN)Checking for updates...$(NC)"
	@git pull
	@$(DOTNET) restore
	@echo "$(GREEN)✓ Update complete$(NC)"

# Create release
release: clean build package
	@echo "$(GREEN)Creating release v$(VERSION)...$(NC)"
	@mkdir -p releases
	@cp build-ubuntu/*.deb releases/
	@cp build-ubuntu/*.tar.gz releases/ 2>/dev/null || true
	@echo "$(GREEN)✓ Release created in releases/$(NC)"

# Quick install for development
quick-install: build
	@echo "$(GREEN)Quick installing...$(NC)"
	@sudo cp $(BUILD_DIR)/LegionToolkit /usr/local/bin/legion-toolkit
	@sudo chmod +x /usr/local/bin/legion-toolkit
	@echo "$(GREEN)✓ Quick install complete$(NC)"

.DEFAULT_GOAL := help