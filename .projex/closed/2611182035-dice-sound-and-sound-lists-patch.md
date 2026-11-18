# Patch: Dice Sound And Sound Lists

> **Status:** Complete
> **Author:** Claude Fable 5.2
> **Parent:** User
> **Directive:** Go ahead with the proposed audio work, starting with the dice sound (belongs to EF, not packs) and the `sound` options.
> **Source Plan:** Direct
> **Result:** Partial Success — built, engine-tested; playback not yet heard in game
> **Related Projex:** `2611181936-area-audio-rails-patch.md`

---

## Summary

- **Dice:** EF plays its own `expedition_framework:dice` once per die a player action throws. No pack wiring, no hook.
- **`sound` effect, object form:** `{ "sound": { "sounds": [ { audio, chance_to_skip } ], delay, repeat, gap, volume, pitch } }`. One sound picked evenly per play. `"sound": "id"` unchanged.

Still open from the proposal: `action` + `appeared` hooks | named channel + `stop_sound` | `when` / `fade` on rails | `item_sounds`.

---

## Changes

### Dice count (Core)

**Files:** `src/Core/ExpeditionRun.cs` | `src/Core/Expr.cs` | `src/Core/Model.cs`
- `ExpeditionRun.CountDice`: +1 per `RollCheck`, +N per `dice(N, s)` (from `Expr.Sum`). Skipped while rendering labels. Plain `Roll` not counted — also picks text variants and weighted entries
- `Perform`: count zeroed before the action body; `Bridge.PlayDice(count)` right after it, before `EnemyTurn` → enemy dice silent
- `IBridge`: `PlaySounds(SoundSpec)`, `PlayDice(int)`

### Sound lists (Core)

**Files:** `src/Core/Model.cs` | `src/Core/PackLoader.cs` | `src/Core/Builtins.cs`
- `SoundSpec`; `PackLoader.Spec.Sound()` + `Span` (number | `[least, most]`)
- Ranges: `chance_to_skip` 0–100 | `delay`, `gap` 0–60 | `volume` 0–4 | `pitch` 0.25–4. `gap` default 0.12
- `sound` effect: `sounds` present → spec path; else old id path. Public `IRun` untouched → C# packs unaffected

### Playback (Game)

**Files:** `src/Game/GameSounds.cs` | `src/Game/GameBridge.cs` | `ExpeditionFramework.csproj`
- `GameSounds.Play(SoundSpec)`, `PlayDice`, `Queue`, `PlayOne`. Each play = own temporary `AudioSource` (pitch is per source; game's shared channels cut each other). Volume × `audioSource2.volume`
- Dice: at most 4 heard, 0.14 s apart, volume 0.45–0.6, pitch 0.92–1.1
- csproj installs `Sounds\*.ogg`

### Other

- `tools/EfSim/KitBridge.cs`: two no-op bridge members
- `tests/Sim/Program.cs`: `TestSoundsAndDice`
- `docs/effects.md`, `docs/actions.md`: object form; dice note

---

## Verification

**Method:** `dotnet run -c Release --project tests/Sim` | game build, scratch `InstallDir` | `ef-sim lint` Showcase + NightExpeditionsReborn

**Result:**
```
Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps
All checks passed
build: succeeded; Sounds/dice.ogg in InstallDir
lint ef_showcase: 0 error(s), 0 warning(s)
lint night_expeditions_reborn: 0 error(s), 4 warning(s)   # locale placeholders, older
```

Sample outcomes identical to before the patch → counting dice changed no roll.

**Status:** PASS for engine + build. `src/Game` playback NOT verified.

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| `2611181936-area-audio-rails-patch.md` | Earlier audio patch | none |

---

## Notes

- `Sounds/dice.ogg` is on disk, untracked (audio stays out of the repo, as asked for NightExpeditionsReborn). Made from NER's `sfx/freesound_community-073088_2d6-rolling-dice-37051.mp3`: `-ss 0.27 -t 1.45`, 0.15 s fade out, `libvorbis -q:a 4`. Missing file → one log warning, no dice sound.
- Dice counted: checks + `dice()`. Not counted: `chance` conditions, loot and variant picks.
- Luck reroll of a d20 = still one die.
- Guesses to check by ear: dice volume, 4-die cap, 0.14 s spacing; dice start with the action's own `sound`, not after it.
- Temporary sources are destroyed on scaled time; a paused game (`timeScale` 0) would hold them until resume.
