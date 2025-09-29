#!/bin/bash
# Remove all Claude references from git history

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

echo -e "${RED}════════════════════════════════════════════════════${NC}"
echo -e "${RED}    REMOVING CLAUDE REFERENCES FROM GIT HISTORY${NC}"
echo -e "${RED}════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${YELLOW}⚠️  WARNING: This will rewrite ALL commit history!${NC}"
echo -e "${YELLOW}⚠️  This should only be done before sharing the repo.${NC}"
echo ""
read -p "Are you SURE you want to continue? Type 'yes' to proceed: " CONFIRM

if [ "$CONFIRM" != "yes" ]; then
    echo "Cancelled."
    exit 1
fi

# Create backup
echo -e "${BLUE}Creating backup branch...${NC}"
CURRENT_BRANCH=$(git branch --show-current)
git branch backup-before-claude-removal 2>/dev/null || true

# Set proper git config
git config user.name "Vivek Chamoli"
git config user.email "vivekchamoli@github.com"

echo -e "${GREEN}Cleaning commit messages...${NC}"

# Use git filter-branch to clean ALL commit messages
git filter-branch --force --msg-filter '
    # Remove lines containing Claude references
    sed -e "/Claude Code/d" \
        -e "/Claude <noreply@anthropic.com>/d" \
        -e "/Co-Authored-By: Claude/d" \
        -e "/Generated with.*Claude/d" \
        -e "/🤖.*Claude/d" \
        -e "/claude\.ai/d" \
        -e "/anthropic/d"
' --tag-name-filter cat -- --branches --tags

# Also change the author/committer information
echo -e "${GREEN}Updating author information...${NC}"

git filter-branch --force --env-filter '
    # Fix any commits that might have Claude as author/committer
    if [ "$GIT_AUTHOR_NAME" = "Claude" ] || [ "$GIT_AUTHOR_EMAIL" = "noreply@anthropic.com" ]; then
        export GIT_AUTHOR_NAME="Vivek Chamoli"
        export GIT_AUTHOR_EMAIL="vivekchamoli@github.com"
    fi
    if [ "$GIT_COMMITTER_NAME" = "Claude" ] || [ "$GIT_COMMITTER_EMAIL" = "noreply@anthropic.com" ]; then
        export GIT_COMMITTER_NAME="Vivek Chamoli"
        export GIT_COMMITTER_EMAIL="vivekchamoli@github.com"
    fi
' --tag-name-filter cat -- --branches --tags

# Clean up refs
echo -e "${GREEN}Cleaning up...${NC}"
rm -rf .git/refs/original/ 2>/dev/null || true
git reflog expire --expire=now --all
git gc --prune=now --aggressive

# Show the cleaned history
echo ""
echo -e "${GREEN}Cleaned commit history:${NC}"
git log --oneline -10

# Check for any remaining references
echo ""
echo -e "${GREEN}Checking for remaining Claude references...${NC}"
if git log --all --grep="Claude\|claude\|Anthropic\|anthropic" --oneline | head -5 | grep .; then
    echo -e "${YELLOW}⚠️  Some references might still exist (shown above)${NC}"
else
    echo -e "${GREEN}✅ No Claude references found in commit history${NC}"
fi

echo ""
echo -e "${RED}════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✅ Local history has been cleaned!${NC}"
echo -e "${RED}════════════════════════════════════════════════════${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "1. Review the changes: git log --oneline"
echo "2. Force push to GitHub: ${RED}git push --force origin main${NC}"
echo "3. Delete backup when satisfied: git branch -D backup-before-claude-removal"
echo ""
echo -e "${RED}⚠️  IMPORTANT: Force pushing will overwrite the remote repository!${NC}"
echo -e "${RED}⚠️  Make sure no one else is working on this repository.${NC}"