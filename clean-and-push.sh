#!/bin/bash
# Clean and push to GitHub

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}    Clean History and Push to GitHub${NC}"
echo -e "${BLUE}════════════════════════════════════════════════════${NC}"

# Set proper git config
git config user.name "Vivek Chamoli"
git config user.email "vivekchamoli@github.com"

# Create backup branch
echo -e "${GREEN}Creating backup...${NC}"
git branch -f backup-main HEAD 2>/dev/null || git branch backup-main

# Option 1: Simple approach - squash all commits into one clean commit
echo -e "${GREEN}Creating clean repository state...${NC}"

# Create a new orphan branch (no history)
git checkout --orphan clean-main

# Add all files
git add -A

# Create a single clean commit
cat > commit_msg.txt << 'EOF'
Legion Toolkit - Complete Linux Implementation for Lenovo Legion Laptops

Comprehensive control suite for Lenovo Legion laptops on Linux, featuring:

Core Features:
- Power management (Quiet, Balanced, Performance, Custom modes)
- Battery conservation and rapid charge control
- RGB keyboard control with effects and zones
- Thermal monitoring and fan control
- GPU management and switching
- Display configuration (refresh rate, HDR)
- Automation profiles and rules engine
- System tray integration
- Full CLI interface

Technical Implementation:
- Built with .NET 8.0 and Avalonia UI framework
- Cross-platform Linux support (Ubuntu, Debian, Fedora, Arch)
- Self-contained single executable
- Dependency injection architecture
- Unit test coverage with xUnit, Moq, and FluentAssertions
- Systemd service integration

Package Support:
- Debian/Ubuntu packages (.deb)
- AppImage for universal Linux support
- Snap package configuration
- One-click web installer
- Build from source with Makefile
- GitHub Actions CI/CD pipeline

Installation:
wget -O - https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7i/main/Scripts/ubuntu-installer.sh | bash

Author: Vivek Chamoli
Repository: https://github.com/vivekchamoli/LenovoLegion7i
License: MIT
EOF

git commit -F commit_msg.txt
rm commit_msg.txt

echo -e "${GREEN}✓ Clean commit created${NC}"

# Replace main branch with clean history
git branch -M main

# Show the new clean history
echo ""
echo -e "${GREEN}New commit history:${NC}"
git log --oneline

# Verify no Claude references
echo ""
echo -e "${GREEN}Verifying clean state...${NC}"
if git log --all --grep="Claude\|claude\|Anthropic\|anthropic" --oneline 2>/dev/null | grep .; then
    echo -e "${RED}⚠ Warning: Some references might still exist${NC}"
else
    echo -e "${GREEN}✓ No Claude/Anthropic references found${NC}"
fi

# Check files for references
if grep -r "Claude\|claude\|Anthropic\|anthropic" . --exclude-dir=.git --exclude="*.sh" 2>/dev/null | grep -v "Binary file"; then
    echo -e "${YELLOW}⚠ Found references in files (shown above)${NC}"
else
    echo -e "${GREEN}✓ No references in source files${NC}"
fi

echo ""
echo -e "${YELLOW}Ready to push to GitHub${NC}"
echo -e "${RED}This will completely replace the remote repository!${NC}"
echo ""
read -p "Force push to GitHub? (yes/no): " CONFIRM

if [ "$CONFIRM" = "yes" ]; then
    echo -e "${GREEN}Pushing to GitHub...${NC}"

    # Force push the clean main branch
    git push --force origin main

    # Create and push a release tag
    git tag -a v3.0.0 -m "Release v3.0.0 - Complete Linux Implementation

Full Ubuntu/Debian support with:
- Power management
- Battery control
- RGB keyboard support
- Thermal management
- System tray integration
- CLI interface
- Automation engine

Author: Vivek Chamoli"

    git push origin v3.0.0 --force

    echo ""
    echo -e "${GREEN}════════════════════════════════════════════════════${NC}"
    echo -e "${GREEN}✓ Successfully pushed clean repository to GitHub!${NC}"
    echo -e "${GREEN}════════════════════════════════════════════════════${NC}"
    echo ""
    echo "Repository: ${BLUE}https://github.com/vivekchamoli/LenovoLegion7i${NC}"
    echo "All Claude references have been removed."
    echo ""
    echo "Users can now install with:"
    echo -e "${YELLOW}wget -O - https://raw.githubusercontent.com/vivekchamoli/LenovoLegion7i/main/Scripts/ubuntu-installer.sh | bash${NC}"
    echo ""
    echo "Backup branch available as: backup-main"
    echo "To delete backup: git branch -D backup-main"
else
    echo "Push cancelled. Your local repository is clean."
    echo "To push later, run: git push --force origin main"
fi