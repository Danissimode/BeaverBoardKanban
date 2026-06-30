#!/usr/bin/env bash
# audit-public-repo.sh — Beaver Board public-repo safety audit
# Run locally before pushing or in CI. Exit code 0 = pass, non-zero = fail.

set -euo pipefail

ISSUES=0
REPORT_FILE="${REPORT_FILE:-/dev/stdout}"

log_fail() { echo "❌ FAIL: $1" >&2; ((ISSUES++)); }
log_pass() { echo "✅ PASS: $1"; }
log_warn() { echo "⚠️  WARN: $1"; }

cd "${REPO_DIR:-.}"

echo "=== Beaver Board Public Repo Audit ===" | tee "$REPORT_FILE"
echo "" | tee -a "$REPORT_FILE"

# ── 1. No AllowAnyOrigin ────────────────────────────────────────────────────
echo "Checking CORS configuration..." | tee -a "$REPORT_FILE"
if grep -r "AllowAnyOrigin" --include="*.cs" . 2>/dev/null | grep -v "AllowAnyInstance" > /dev/null; then
    log_fail "AllowAnyOrigin found in CORS config"
else
    log_pass "No AllowAnyOrigin found"
fi

# ── 2. No /Users/ or C:\Users\ paths (exclude obj/bin/.git) ─────────────────
echo "Checking for hardcoded user home paths..." | tee -a "$REPORT_FILE"
USER_PATHS=$(grep -rEl "(['\"]|/)(/(Users|C:\\\\Users)|/home/)[[:alnum:]/._-]+(['\"]|[:\"]| )" \
    --include="*.cs" --include="*.razor" --include="*.json" . 2>/dev/null \
    | grep -v "/obj/" | grep -v "/bin/" | grep -v ".git/" || true)
if [ -n "$USER_PATHS" ]; then
    log_fail "Hardcoded user home path found in:"
    echo "$USER_PATHS" | head -10 | tee -a "$REPORT_FILE"
else
    log_pass "No hardcoded user home paths"
fi

# ── 3. No PetPals / private project names ──────────────────────────────────
echo "Checking for private project references..." | tee -a "$REPORT_FILE"
PRIVATE_NAMES="PetPals|PetPal|Pet pals|kawaii-pet"
if grep -rlE "$PRIVATE_NAMES" --include="*.cs" --include="*.razor" . 2>/dev/null \
    | grep -v "/obj/" | grep -v "/bin/" | grep -v ".git/" | grep -v "\.github/" > /dev/null; then
    log_fail "Private project name found in codebase"
else
    log_pass "No private project name references"
fi

# ── 4. No hardcoded private emails ─────────────────────────────────────────
echo "Checking for private email addresses..." | tee -a "$REPORT_FILE"
EMAIL_PAT=$(grep -rE "[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.(com|net|org|ru|io|dev)" \
    --include="*.cs" --include="*.razor" . 2>/dev/null \
    | grep -v "@example\." | grep -v "noreply\|test\|placeholder\|TODO\|github.com/Danissimode" || true)
if [ -n "$EMAIL_PAT" ]; then
    log_warn "Email-like string found (review manually):"
    echo "$EMAIL_PAT" | head -3 | tee -a "$REPORT_FILE"
else
    log_pass "No obvious private emails"
fi

# ── 5. No API key / sk- patterns outside test files ────────────────────────
echo "Checking for hardcoded API keys..." | tee -a "$REPORT_FILE"
KEY_PATTERNS="sk-[a-zA-Z0-9]{20,}|api[_-]key['\"]?\s*[:=]\s*['\"][a-zA-Z0-9]{20,}"
KEY_HITS=$(grep -rE "$KEY_PATTERNS" \
    --include="*.cs" --include="*.razor" --include="*.json" \
    --exclude="*.Tests.cs" --exclude="*Mock*" --exclude="fixture*" \
    . 2>/dev/null \
    | grep -v "sk-ant\|placeholder\|REPLACE_ME\|YOUR_KEY\|test-key\|mock-key\|xxx" || true)
if [ -n "$KEY_HITS" ]; then
    log_fail "Potential hardcoded API key found:"
    echo "$KEY_HITS" | head -5 | tee -a "$REPORT_FILE"
else
    log_pass "No hardcoded API keys found"
fi

# ── 6. No hardcoded private model/provider config ───────────────────────────
echo "Checking for hardcoded private model configs..." | tee -a "$REPORT_FILE"
PRIVATE_PROVIDER=$(grep -rliE "petpal|internal-corp|enterprise-ai|my-org|company-ai" \
    --include="*.cs" --include="*.razor" --include="*.json" \
    . 2>/dev/null | grep -v ".git/" || true)
if [ -n "$PRIVATE_PROVIDER" ]; then
    log_fail "Private provider/model config found"
    echo "$PRIVATE_PROVIDER" | tee -a "$REPORT_FILE"
else
    log_pass "No private provider configs"
fi

# ── 7. Health endpoint path redaction ──────────────────────────────────────
echo "Checking health endpoint path redaction..." | tee -a "$REPORT_FILE"
if grep -rE "GetHealth|HealthCheck" --include="*.cs" . 2>/dev/null | grep -v ".git/" | head -5 | \
    grep -v "redact\|sanitize\|Hide\|mask\|replace.*path\|replace.*dir" > /dev/null 2>&1; then
    # Check if GetHealth exists and whether it looks like paths are redacted
    HEALTH_FILE=$(grep -rl "GetHealth" --include="*.cs" . 2>/dev/null | grep -v ".git/" || true)
    if [ -n "$HEALTH_FILE" ]; then
        if grep -A 30 "GetHealth" "$HEALTH_FILE" 2>/dev/null | grep -E "path|Path|Directory|dataDir" | \
            grep -v "redact\|sanitize\|Hide\|mask\|Replace" > /dev/null 2>&1; then
            log_fail "Health endpoint may expose full paths without redaction"
        else
            log_pass "Health endpoint appears to redact paths"
        fi
    else
        log_pass "No custom health endpoint found"
    fi
else
    log_pass "Health endpoint redaction check passed"
fi

# ── 8. Check for .github/workflows visibility ────────────────────────────────
echo "Checking CI configuration..." | tee -a "$REPORT_FILE"
if [ -f ".github/workflows/ci.yml" ]; then
    log_pass "GitHub Actions CI workflow exists"
else
    log_fail "No .github/workflows/ci.yml found"
fi

# ── 9. Check for Gitleaks / secrets scanning ────────────────────────────────
echo "Checking secrets scanning..." | tee -a "$REPORT_FILE"
if [ -f ".gitleaks.toml" ]; then
    log_pass ".gitleaks.toml exists"
else
    log_warn "No .gitleaks.toml found — add one for CI secrets scanning"
fi

# ── 10. README exists and not empty ─────────────────────────────────────────
echo "Checking README..." | tee -a "$REPORT_FILE"
if [ -s "README.md" ]; then
    log_pass "README.md exists"
else
    log_fail "README.md is missing or empty"
fi

# ── Summary ──────────────────────────────────────────────────────────────────
echo "" | tee -a "$REPORT_FILE"
echo "=== Audit Summary ===" | tee -a "$REPORT_FILE"
if [ "$ISSUES" -eq 0 ]; then
    echo "✅ All checks passed. Repository is safe for public release." | tee -a "$REPORT_FILE"
    exit 0
else
    echo "❌ $ISSUES issue(s) found. Fix before pushing to public repo." | tee -a "$REPORT_FILE"
    exit 1
fi
