# Patch: Held Sounds, Rail Conditions, Appeared Hook

> **Status:** Complete
> **Author:** Claude Fable 5.2
> **Parent:** User
> **Directive:** Build the proposed EF audio work. User decisions: rail `when` is checked on a new player turn; `until` includes `"room"`.
> **Source Plan:** Direct
> **Result:** Partial Success — built, engine-tested; playback not yet heard in game. Per-item sounds (`item_sounds`) not in this patch
> **Related Projex:** `2611181936-area-audio-rails-patch.md` | `2611182035-dice-sound-and-sound-lists-patch.md`

---

## Summary

| Added | Form |
|---|---|
| Held sound | `sound` effect keys `channel`, `until` (`action` \| `room` \| `fight`), `loop`, `fade` |
| Stop by name | `{ "stop_sound": "channel" }` |
| Conditional rail | rail keys `when`, `fade` |
| One-shot rail | rail keys `once`, `start` |
| Hook | `appeared` — self: enemy, target: player |

---

## Changes

### Core

**Files:** `src/Core/Schema.cs` | `src/Core/Model.cs` | `src/Core/PackLoader.cs` | `src/Core/Builtins.cs` | `src/Core/ExpeditionRun.cs`
- `SoundSpec`: `Channel`, `Until`, `Fade`, `Loop`, `Held`. Problems: unknown `until` | `loop` without `channel`/`until` | `repeat` > 1 on a held sound
- `AudioRail`: `When`, `Fade`, `Once`, `Start`. Problems: `once` + `every` | `fade` outside 0–60 | `start` outside 0–<1
- `IBridge`: `StopSound(channel)`, `EndSounds(until)`
- `EndSounds` calls: `"action"` at top of `Perform` (before the action's own effects → a sound the action starts lasts to the next press) | `"room"` at top of `EnterRoom` | `"fight"` at top of `EndFight`
- `ExpeditionRun.Holds(Condition)`: evaluates with dice reading 1, like labels
- `appeared`: in `Engage` for each active enemy, before `combat_start`; in `Spawn` after the arrival's intent is set

### Game

**Files:** `src/Game/AreaAudio.cs` | `src/Game/GameSounds.cs` | `src/Game/GameBridge.cs` | `src/Game/ExpeditionLocation.cs`
- `AreaAudio`: per-rail `Level` + `Wanted`; `Recheck(holds)`; `Begin`. Fade = `Level` moved toward 0|1 each frame; faded-out loop is stopped, swells back without restart if wanted again mid-fade
- `when` timing (user decision): `Recheck` runs after `run.Begin()` and after each `run.Perform` in `ExpeditionLocation.Choose`. No timer
- `GameSounds`: `Voice` list for held sounds; `Stop(channel)`, `End(until)` mark voices stopping (fade out, then destroyed) and drop queued plays; new sound on a channel replaces the old; `Clear` destroys all

### Tests + docs

- `tests/Sim/Program.cs`: `TestHeldSoundsAndAppeared`; `TestSoundsAndDice` unknown-key case `loop` → `wobble` (`loop` is now a key)
- `tools/EfSim/KitBridge.cs`: two no-op members
- `docs/effects.md` (held keys, `stop_sound`) | `docs/areas.md` (4 rail rows) | `docs/run-flow.md` (`appeared` row)

---

## Verification

**Method:** `tests/Sim` | game build, scratch `InstallDir` | `ef-sim lint` Showcase + NightExpeditionsReborn

**Result:**
```
Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps
All checks passed
build: succeeded
lint ef_showcase: 0 error(s), 0 warning(s)
lint night_expeditions_reborn: 0 error(s), 4 warning(s)   # locale placeholders, older
```

Sample outcomes identical to before → new `appeared` emit and `EndSounds` calls change no roll.

**Status:** PASS for engine + build. `src/Game` playback NOT verified.

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| `2611182035-dice-sound-and-sound-lists-patch.md` | Listed these as still open | none (point-in-time record) |

---

## Notes

- "New player turn" read as: run start + end of every player action (enemies' answer included), in or out of a fight. A rail's `when` does not react to anything between presses.
- `when` evaluates with dice reading 1; a condition with `dice()` or `chance` in it is not random there.
- `until: "room"` fires on every room entry, the first room included.
- `until: "action"` fires on every press, turn-taking or not. Opening/closing a trade is not a press.
- Trade finish (`EndTrade`) does not recheck rails; next action does.
- Unheard: fade curves (linear), channel takeover overlap, `start` seeking on ogg loops.
- Still open: `item_sounds` (per-item fire/reload/hit/miss).
