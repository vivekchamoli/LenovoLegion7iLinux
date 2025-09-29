#!/bin/bash
# Script to clean git history and set proper attribution

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${BLUE}═══════════════════════════════════════════${NC}"
echo -e "${BLUE}   Git History Cleanup & Attribution Fix${NC}"
echo -e "${BLUE}═══════════════════════════════════════════${NC}"
echo ""

# Set proper git config
echo -e "${GREEN}▶ Setting git configuration...${NC}"
git config user.name "Vivek Chamoli"
git config user.email "vivekchamoli@example.com"  # Replace with your actual email

echo -e "${GREEN}✓ Git config updated${NC}"

# Option to rewrite history (careful - this changes all commit hashes)
echo ""
echo -e "${YELLOW}⚠ WARNING: Rewriting history will change all commit hashes!${NC}"
echo -e "${YELLOW}Only do this if the repository hasn't been shared yet.${NC}"
read -p "Do you want to rewrite git history to fix authorship? (y/N): " -n 1 -r
echo

if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${GREEN}▶ Rewriting git history...${NC}"

    # Backup current branch
    CURRENT_BRANCH=$(git branch --show-current)
    git branch backup-$CURRENT_BRANCH 2>/dev/null || true

    # Rewrite history to change author/committer
    git filter-branch --force --env-filter '
        OLD_EMAIL="noreply@anthropic.com"
        CORRECT_NAME="Vivek Chamoli"
        CORRECT_EMAIL="vivekchamoli@example.com"

        if [ "$GIT_COMMITTER_EMAIL" = "$OLD_EMAIL" ] || [ "$GIT_COMMITTER_NAME" = "Claude" ]
        then
            export GIT_COMMITTER_NAME="$CORRECT_NAME"
            export GIT_COMMITTER_EMAIL="$CORRECT_EMAIL"
        fi
        if [ "$GIT_AUTHOR_EMAIL" = "$OLD_EMAIL" ] || [ "$GIT_AUTHOR_NAME" = "Claude" ]
        then
            export GIT_AUTHOR_NAME="$CORRECT_NAME"
            export GIT_AUTHOR_EMAIL="$CORRECT_EMAIL"
        fi
    ' --tag-name-filter cat -- --branches --tags

    echo -e "${GREEN}✓ History rewritten${NC}"
    echo -e "${YELLOW}Note: Backup branch created as 'backup-$CURRENT_BRANCH'${NC}"
else
    echo -e "${BLUE}Skipping history rewrite${NC}"
fi

# Create a clean commit for the final state
echo ""
echo -e "${GREEN}▶ Creating clean commit...${NC}"

# Stage all current changes
git add -A

# Create commit message
COMMIT_MSG="Legion Toolkit Linux Production Build

Complete Linux implementation for Lenovo Legion laptops including:
- Avalonia-based cross-platform GUI
- Comprehensive CLI interface
- System tray integration
- Power management (Quiet/Balanced/Performance/Custom)
- Battery conservation and rapid charge control
- RGB keyboard control with effects
- Thermal monitoring and fan control
- GPU management and switching
- Display configuration
- Automation profiles and rules
- Debian/Ubuntu package support
- AppImage for universal Linux support
- Systemd service integration
- Full documentation and installation scripts

Author: Vivek Chamoli
Repository: https://github.com/vivekchamoli/LenovoLegion7i"

# Check if there are changes to commit
if ! git diff --cached --quiet; then
    git commit -m "$COMMIT_MSG" || true
    echo -e "${GREEN}✓ Clean commit created${NC}"
else
    echo -e "${YELLOW}No changes to commit${NC}"
fi

# Remove any references to Claude from commit messages
echo ""
echo -e "${GREEN}▶ Cleaning commit messages...${NC}"

# This will interactively rebase to edit commit messages
echo -e "${YELLOW}To clean commit messages manually:${NC}"
echo "1. Run: git rebase -i --root"
echo "2. Change 'pick' to 'reword' for commits mentioning Claude"
echo "3. Remove any Claude references from commit messages"
echo ""

# Final verification
echo -e "${GREEN}▶ Final verification...${NC}"

# Check for any remaining references
if grep -r "Claude\|claude\|Anthropic\|anthropic" . --exclude-dir=.git --exclude="clean-git-history.sh" 2>/dev/null | grep -v "Binary file"; then
    echo -e "${YELLOW}⚠ Found remaining references - please review above${NC}"
else
    echo -e "${GREEN}✓ No Claude/Anthropic references found in code${NC}"
fi

echo ""
echo -e "${GREEN}═══════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Cleanup complete!${NC}"
echo -e "${GREEN}═══════════════════════════════════════════${NC}"
echo ""
echo "Next steps:"
echo "1. Review the changes: git log --oneline"
echo "2. If history was rewritten, force push: git push --force origin main"
echo "3. Delete backup branch when satisfied: git branch -D backup-$CURRENT_BRANCH"
echo ""
echo -e "${YELLOW}Remember to update your email in the script and git config!${NC}"