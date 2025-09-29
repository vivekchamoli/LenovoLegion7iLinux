#!/bin/bash
# Push Legion Toolkit to GitHub Repository

set -e

# Configuration
GITHUB_USER="vivekchamoli"
GITHUB_REPO="LenovoLegion7i"
REMOTE_URL="https://github.com/${GITHUB_USER}/${GITHUB_REPO}.git"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}═══════════════════════════════════════════${NC}"
echo -e "${BLUE}   Push Legion Toolkit to GitHub${NC}"
echo -e "${BLUE}═══════════════════════════════════════════${NC}"

# Check if git is initialized
if [ ! -d .git ]; then
    echo -e "${YELLOW}Initializing git repository...${NC}"
    git init
    git branch -M main
fi

# Check if remote exists
if ! git remote | grep -q origin; then
    echo -e "${YELLOW}Adding remote origin...${NC}"
    git remote add origin "$REMOTE_URL"
else
    echo -e "${GREEN}✓ Remote origin already configured${NC}"
fi

# Create .gitignore if not exists
if [ ! -f .gitignore ]; then
    echo -e "${YELLOW}Creating .gitignore...${NC}"
    cat > .gitignore << 'EOF'
# Build outputs
bin/
obj/
publish/
build/
build-ubuntu/
releases/
*.deb
*.AppImage
*.tar.gz

# IDE files
.vs/
.vscode/
.idea/
*.user
*.suo
*.swp
*.swo
*~

# OS files
.DS_Store
Thumbs.db
desktop.ini

# Test results
TestResults/
*.trx
*.coverage

# Logs
*.log
logs/

# Temporary files
*.tmp
temp/
tmp/

# NuGet packages
*.nupkg
packages/

# Node modules (if any frontend)
node_modules/

# Python (if any scripts)
__pycache__/
*.pyc
EOF
fi

# Add all files
echo -e "${GREEN}▶ Staging files...${NC}"
git add .

# Show status
echo -e "${GREEN}▶ Git status:${NC}"
git status --short

# Commit
echo -e "${GREEN}▶ Creating commit...${NC}"
COMMIT_MSG="Add Linux production build for Legion Toolkit

- Complete Ubuntu/Debian package support (.deb)
- AppImage for universal Linux support
- Systemd service integration
- System tray support
- Comprehensive CLI interface
- RGB keyboard control
- Power management
- Battery conservation
- Thermal monitoring
- Automated installation scripts
- GitHub Actions CI/CD workflow
- Full documentation"

git commit -m "$COMMIT_MSG" || echo -e "${YELLOW}No changes to commit${NC}"

# Create tag for release
read -p "Create release tag? (e.g., v3.0.0) [leave empty to skip]: " TAG_NAME
if [ ! -z "$TAG_NAME" ]; then
    git tag -a "$TAG_NAME" -m "Release $TAG_NAME - Linux Production Build"
    echo -e "${GREEN}✓ Created tag: $TAG_NAME${NC}"
fi

# Push to GitHub
echo -e "${GREEN}▶ Pushing to GitHub...${NC}"
echo -e "${YELLOW}You may need to authenticate with GitHub${NC}"

# Push main branch
git push -u origin main || git push origin main

# Push tags if any
if [ ! -z "$TAG_NAME" ]; then
    git push origin "$TAG_NAME"
    echo -e "${GREEN}✓ Pushed tag: $TAG_NAME${NC}"
fi

echo ""
echo -e "${GREEN}═══════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Successfully pushed to GitHub!${NC}"
echo -e "${GREEN}═══════════════════════════════════════════${NC}"
echo ""
echo "Repository: ${BLUE}${REMOTE_URL}${NC}"
echo ""
echo "Next steps:"
echo "1. Visit: ${BLUE}https://github.com/${GITHUB_USER}/${GITHUB_REPO}${NC}"
echo "2. Check the Actions tab for build status"
echo "3. Create a release if you tagged the commit"
echo ""
echo "To install on Ubuntu:"
echo -e "${YELLOW}wget -O - https://raw.githubusercontent.com/${GITHUB_USER}/${GITHUB_REPO}/main/Scripts/ubuntu-installer.sh | bash${NC}"