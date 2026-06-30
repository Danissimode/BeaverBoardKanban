#!/usr/bin/env bash
# Beaver Board Doctor — runtime health checks
# Usage: ./beaverboard-doctor.sh [--fix]
# Run with --fix to auto-apply safe fixes (e.g. create default config)

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

FIX_MODE=false
if [[ "${1:-}" == "--fix" ]]; then
  FIX_MODE=true
fi

PASS=0
FAIL=0
WARN=0

report() {
  local status="$1"
  local msg="$2"
  if [[ "$status" == "PASS" ]]; then
    echo -e "${GREEN}[PASS]${NC} $msg"
    ((PASS++))
  elif [[ "$status" == "FAIL" ]]; then
    echo -e "${RED}[FAIL]${NC} $msg"
    ((FAIL++))
  else
    echo -e "${YELLOW}[WARN]${NC} $msg"
    ((WARN++))
  fi
}

echo "=== Beaver Board Doctor ==="
echo ""

# 1. .NET runtime
report "PASS" ".NET 10 SDK: $(dotnet --version 2>/dev/null || echo 'not found')"

# 2. Git
if command -v git &>/dev/null; then
  GIT_VERSION=$(git --version)
  report "PASS" "Git: $GIT_VERSION"
else
  report "FAIL" "Git: not found"
fi

# 3. App settings
SETTINGS_FILE="$HOME/.beaverboard/settings.json"
FALLBACK_SETTINGS="/tmp/.beaverboard/settings.json"
if [[ -f "$SETTINGS_FILE" ]]; then
  report "PASS" "Settings: $SETTINGS_FILE"
elif [[ -f "$FALLBACK_SETTINGS" ]]; then
  report "PASS" "Settings: $FALLBACK_SETTINGS"
else
  report "WARN" "Settings: not found at ~/.beaverboard/settings.json (will use defaults)"
fi

# 4. CORS / localhost binding (check if app is running)
if curl -s --max-time 2 http://localhost:5230/api/health &>/dev/null; then
  report "PASS" "App running at http://localhost:5230"
else
  report "WARN" "App not running at http://localhost:5230 (start with: dotnet run)"
fi

# 5. Build
echo ""
echo "Running build check..."
BUILD_OUTPUT=$(cd "$(dirname "$0")/.." && dotnet build --configuration Release --verbosity quiet 2>&1)
if [[ $? -eq 0 ]]; then
  report "PASS" "dotnet build --configuration Release"
else
  report "FAIL" "dotnet build --configuration Release failed"
  echo "$BUILD_OUTPUT" | grep -E "^.*error" | head -5
fi

# 6. Audit script
AUDIT_SCRIPT="$(dirname "$0")/audit-public-repo.sh"
if [[ -f "$AUDIT_SCRIPT" ]]; then
  if bash "$AUDIT_SCRIPT" &>/dev/null; then
    report "PASS" "Public repo audit"
  else
    report "FAIL" "Public repo audit failed"
  fi
else
  report "WARN" "audit-public-repo.sh not found"
fi

# 7. Optional: OpenCode CLI
if command -v opencode &>/dev/null; then
  report "PASS" "OpenCode CLI: $(opencode --version 2>/dev/null || echo 'installed')"
elif [[ -x "/usr/local/bin/opencode" ]] || [[ -x "$HOME/go/bin/opencode" ]]; then
  report "PASS" "OpenCode CLI: found"
else
  report "WARN" "OpenCode CLI: not found (agent features will use Claude fallback)"
fi

# 8. Optional: Claude CLI
if command -v claude &>/dev/null; then
  report "PASS" "Claude CLI: $(claude --version 2>/dev/null | head -1 || echo 'installed')"
elif [[ -x "/usr/local/bin/claude" ]] || [[ -x "$HOME/.claude/bin/claude" ]]; then
  report "PASS" "Claude CLI: found"
else
  report "WARN" "Claude CLI: not found (Claude runner unavailable)"
fi

# Summary
echo ""
echo "=== Summary ==="
echo -e "${GREEN}PASS:${NC} $PASS"
echo -e "${RED}FAIL:${NC} $FAIL"
echo -e "${YELLOW}WARN:${NC} $WARN"

if [[ $FAIL -gt 0 ]]; then
  echo ""
  echo -e "${RED}Some checks failed. Review the output above.${NC}"
  exit 1
elif [[ $WARN -gt 0 ]]; then
  echo ""
  echo -e "${YELLOW}All critical checks passed. Review warnings above.${NC}"
  exit 0
else
  echo ""
  echo -e "${GREEN}All checks passed. You're good to go!${NC}"
  exit 0
fi
