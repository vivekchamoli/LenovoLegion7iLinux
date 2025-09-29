#!/bin/bash
# Quick method to remove Claude from recent history and force push

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${RED}═══════════════════════════════════════════${NC}"
echo -e "${RED}  Quick Claude Reference Removal${NC}"
echo -e "${RED}═══════════════════════════════════════════${NC}"

# Set git config
git config user.name "Vivek Chamoli"
git config user.email "vivekchamoli@github.com"

# Create a new branch from current state
echo -e "${GREEN}Creating clean branch...${NC}"
git checkout -b clean-main

# Get the current tree state
TREE=$(git write-tree)

# Create a single clean commit with all current content
echo -e "${GREEN}Creating clean commit...${NC}"
COMMIT_MSG="Legion Toolkit - Complete Linux Implementation for Lenovo Legion Laptops

Comprehensive control suite featuring:
- Power management (Quiet/Balanced/Performance/Custom)
- Battery conservation and rapid charge control
- RGB keyboard control with effects
- Thermal monitoring and fan control
- GPU management and switching
- Display configuration
- System tray integration
- Full CLI interface
- Debian/Ubuntu packages
- AppImage support
- Systemd service integration

Author: Vivek Chamoli
Repository: https://github.com/vivekchamoli/LenovoLegion7i"

# Create new commit with clean message
NEW_COMMIT=$(echo "$COMMIT_MSG" | git commit-tree $TREE)

# Reset branch to new commit
git reset --hard $NEW_COMMIT

echo -e "${GREEN}Clean branch created with single commit${NC}"

# Show the result
echo -e "${GREEN}New commit:${NC}"
git log --oneline -1

echo ""
echo -e "${YELLOW}To push this clean version to GitHub:${NC}"
echo -e "${RED}1. git push --force origin clean-main:main${NC}"
echo "   This will completely replace the main branch"
echo ""
echo "Or to review first:"
echo "2. git checkout main"
echo "3. git diff main clean-main"
echo ""
echo -e "${RED}⚠️  WARNING: This will overwrite ALL history on GitHub!${NC}"