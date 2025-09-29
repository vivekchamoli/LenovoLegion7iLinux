#!/bin/bash
# Push Legion Toolkit to GitHub with clean attribution

set -e

# Configuration - UPDATE THESE!
GITHUB_USER="vivekchamoli"
GITHUB_REPO="LenovoLegion7i"
AUTHOR_NAME="Vivek Chamoli"
AUTHOR_EMAIL="your-email@example.com"  # UPDATE THIS!
REMOTE_URL="https://github.com/${GITHUB_USER}/${GITHUB_REPO}.git"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}═══════════════════════════════════════════${NC}"
echo -e "${BLUE}   Push Clean Repository to GitHub${NC}"
echo -e "${BLUE}═══════════════════════════════════════════${NC}"
echo ""

# Set git configuration
echo -e "${GREEN}▶ Configuring git...${NC}"
git config user.name "$AUTHOR_NAME"
git config user.email "$AUTHOR_EMAIL"
echo -e "${GREEN}✓ Git configured as: $AUTHOR_NAME <$AUTHOR_EMAIL>${NC}"

# Initialize repository if needed
if [ ! -d .git ]; then
    echo -e "${GREEN}▶ Initializing git repository...${NC}"
    git init
    git branch -M main
fi

# Check for any Claude references
echo -e "${GREEN}▶ Checking for unwanted references...${NC}"
if grep -r "Claude\|claude\|Anthropic\|anthropic" . \
    --exclude-dir=.git \
    --exclude-dir=node_modules \
    --exclude-dir=bin \
    --exclude-dir=obj \
    --exclude="*.dll" \
    --exclude="*.exe" \
    --exclude="push-clean-to-github.sh" \
    --exclude="clean-git-history.sh" 2>/dev/null | grep -v "Binary file"; then

    echo -e "${RED}✗ Found references that should be removed (shown above)${NC}"
    echo "Please clean these references before pushing."
    read -p "Continue anyway? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
else
    echo -e "${GREEN}✓ No unwanted references found${NC}"
fi

# Create comprehensive .gitattributes
echo -e "${GREEN}▶ Creating .gitattributes...${NC}"
cat > .gitattributes << 'EOF'
# Git attributes for Legion Toolkit
* text=auto

# Source code
*.cs text diff=csharp
*.csx text diff=csharp
*.sln text eol=crlf merge=union
*.csproj text eol=crlf merge=union

# Scripts
*.sh text eol=lf
*.bash text eol=lf
*.fish text eol=lf
*.ps1 text eol=crlf
*.cmd text eol=crlf
*.bat text eol=crlf

# Documentation
*.md text
*.txt text
*.yml text
*.yaml text
*.json text

# Web
*.html text diff=html
*.css text diff=css
*.js text
*.ts text

# Graphics
*.png binary
*.jpg binary
*.jpeg binary
*.gif binary
*.ico binary
*.svg text

# Fonts
*.ttf binary
*.otf binary
*.woff binary
*.woff2 binary

# Archives
*.zip binary
*.tar binary
*.gz binary
*.deb binary
*.rpm binary
*.AppImage binary

# Executables
*.exe binary
*.dll binary
*.so binary

# Author info
* authorname="Vivek Chamoli"
EOF
echo -e "${GREEN}✓ .gitattributes created${NC}"

# Add remote if not exists
if ! git remote | grep -q origin; then
    echo -e "${GREEN}▶ Adding remote origin...${NC}"
    git remote add origin "$REMOTE_URL"
else
    echo -e "${GREEN}✓ Remote origin exists${NC}"
    # Update the URL in case it changed
    git remote set-url origin "$REMOTE_URL"
fi

# Stage all files
echo -e "${GREEN}▶ Staging files...${NC}"
git add -A

# Create comprehensive commit
echo -e "${GREEN}▶ Creating commit...${NC}"
COMMIT_MSG="Legion Toolkit - Complete Linux Implementation

Comprehensive control suite for Lenovo Legion laptops on Linux.

Features:
- Power management (Quiet/Balanced/Performance/Custom modes)
- Battery conservation and rapid charge control
- RGB keyboard control with effects and zones
- Thermal monitoring and fan control
- GPU management and switching
- Display configuration (refresh rate, HDR)
- Automation profiles and rules
- System tray integration
- Full CLI interface
- Systemd service support

Technical Stack:
- .NET 8.0 with Avalonia UI
- Cross-platform Linux support
- Self-contained single executable
- Dependency injection architecture
- Unit test coverage

Installation:
- Debian/Ubuntu packages (.deb)
- AppImage for universal Linux
- Snap package support
- One-click web installer
- Build from source with Makefile

Author: $AUTHOR_NAME
License: MIT
Repository: https://github.com/${GITHUB_USER}/${GITHUB_REPO}"

# Check if there are changes
if ! git diff --cached --quiet; then
    git commit -m "$COMMIT_MSG" --author="$AUTHOR_NAME <$AUTHOR_EMAIL>"
    echo -e "${GREEN}✓ Commit created${NC}"
else
    echo -e "${YELLOW}No changes to commit${NC}"
fi

# Show recent commits
echo ""
echo -e "${GREEN}▶ Recent commits:${NC}"
git log --oneline -5

# Confirm before pushing
echo ""
echo -e "${YELLOW}Ready to push to: $REMOTE_URL${NC}"
echo -e "${YELLOW}This will push as: $AUTHOR_NAME <$AUTHOR_EMAIL>${NC}"
echo ""
read -p "Push to GitHub? (Y/n): " -n 1 -r
echo

if [[ ! $REPLY =~ ^[Nn]$ ]]; then
    echo -e "${GREEN}▶ Pushing to GitHub...${NC}"

    # Push main branch
    git push -u origin main --force

    echo -e "${GREEN}✓ Successfully pushed to GitHub${NC}"

    # Option to create a release tag
    echo ""
    read -p "Create release tag v3.0.0? (Y/n): " -n 1 -r
    echo

    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        git tag -a v3.0.0 -m "Release v3.0.0 - Complete Linux Implementation

- Full Ubuntu/Debian support
- AppImage universal package
- System tray integration
- Comprehensive hardware control
- Automation engine
- RGB keyboard support

Author: $AUTHOR_NAME"

        git push origin v3.0.0
        echo -e "${GREEN}✓ Release tag created and pushed${NC}"
    fi
else
    echo -e "${YELLOW}Push cancelled${NC}"
fi

echo ""
echo -e "${GREEN}═══════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Process complete!${NC}"
echo -e "${GREEN}═══════════════════════════════════════════${NC}"
echo ""
echo "Repository URL: ${BLUE}https://github.com/${GITHUB_USER}/${GITHUB_REPO}${NC}"
echo "Installation command:"
echo -e "${YELLOW}wget -O - https://raw.githubusercontent.com/${GITHUB_USER}/${GITHUB_REPO}/main/Scripts/ubuntu-installer.sh | bash${NC}"
echo ""
echo -e "${RED}IMPORTANT: Remember to update your email address in this script!${NC}"