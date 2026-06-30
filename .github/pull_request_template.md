## Pull Request Checklist

### Pre-Merge Requirements

- [ ] `dotnet build` passes locally (0 errors, 0 warnings)
- [ ] `dotnet test` passes (or `--filter` scoped to changed area)
- [ ] `scripts/audit-public-repo.sh` passes (no secrets, no private paths, no PetPals)
- [ ] `doc/status-matrix.md` updated if feature status changed
- [ ] No new `CS####` compiler warnings introduced

### Change Classification

_select one:_

- 🟢 **Routine / Fix** — bug fix, refactor, doc update, CI/infra
- 🟡 **Feature** — new user-facing capability
- 🔴 **Breaking** — API/DB schema change, migration required
- ⚪ **Draft** — work in progress, not ready for review

### What Changed

<!-- Briefly describe WHAT changed and WHY. Link to issues/tickets if applicable. -->

### How to Test

<!-- Steps to verify the change works correctly. -->

### Notes / Known Trade-offs

<!-- Anything the reviewer should know: perf implications, design decisions, deprecations, follow-up work. -->
