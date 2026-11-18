# Patch: Comment Tidy

> **Status:** Complete
> **Author:** Claude Fable 5.2
> **Parent:** User
> **Directive:** patch-projex tidy up the comments
> **Source Plan:** Direct
> **Result:** Success
> **Related Projex:** `2611190035-comment-tidy-patch.md` in the sibling repo (same directive, same pass)

---

## Summary

Source: read-only comment review (229 blocks, both repos) → 9 EF findings. Acted on: 5. Kept as written: 4.

Rule applied: SKILL.md § Source Hygiene 6 — rejected-alternative notes are rationale, not slop. Review said drop them; kept.

Code commit: `6f748fb`

---

## Changes

### Misplaced summary

**File:** `src/Core/Builtins.cs` — Modified
- Loot effect `<summary>` sat above `ReputationAmount` → moved onto `CompileGiveLoot`, the fn it describes
- `reputation` effect comment: dropped the value / value_x100 restatement → points at `ReputationAmount`, which owns it

### Unreadable alone

**Files:** `tools/EfSim/KitBridge.cs` | `tools/EfSim/PackRun.cs` — Modified
- `reputation` field note: "The world's when the row carries one…" → says the dictionary is the row's world's, shared by a "days" row's nights
- `RollIntel` note: "A coin game read in a when…" → says what a coin game is and whose random the flip uses

### Wrap

**File:** `src/Core/Footprint.cs` — Modified
- `Measure` summary: last line re-wrapped. Text unchanged

### Banners

**File:** `workshop/publish.ps1` — Modified
- Removed 3 `# --- … ---` dividers (Source Hygiene 4)

---

## Kept

| Site | Review verdict | Why kept |
|---|---|---|
| `PackLoader.cs` — text-arg shared effect, "Rejected: doing this for every shared effect…" | trim | Names rejected alternative + its cost. Hygiene 6 |
| `PackLoader.cs` — `ScanTextUse`, "Rejected: free text in the placeholder…" | simplify | Same; stops someone loosening the a-z, 0-9, _ rule |
| `Footprint.cs` — "a single rounded length would hide which it was" | move to docs | Rationale for returning two numbers |
| `ExpeditionRun.cs` — "As above;" | minor | Overload it refers to is adjacent |

---

## Verification

**Method:** `dotnet run -c Release --project tests/Sim` | `dotnet build ExpeditionFramework.csproj -c Release` | PowerShell parser over `workshop/publish.ps1`

**Result:**
```
All checks passed
    0 個警告
    0 個錯誤
EF parse errors: 0
```

**Status:** PASS

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| none | — | Comments only; no behavior, no doc claims changed |

---

## Notes

- Reputation spec still stated in `api/ExpeditionApi.cs` and `src/Core/Model.cs` as well as `ReputationAmount`: API doc comments, each read on its own → left
