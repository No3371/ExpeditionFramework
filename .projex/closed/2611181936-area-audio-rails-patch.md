# Patch: Area Audio Rails

> **Status:** Complete
> **Author:** Claude Fable 5.2
> **Parent:** User
> **Directive:** Packs define audio for night areas: loop bgm | cues played randomly every A–B s | event sfx. Audio lists = list of `{ audio, chance_to_skip }`. Multi rail/channel, so several bgm and ambience co-exist.
> **Source Plan:** Direct
> **Result:** Partial Success — built, loader-tested; not yet heard in game
> **Related Projex:** none

---

## Summary

Area gains `audio`: list of rails. Rail = own `AudioSource` → all rails play at once. Rail holds `sounds`: list of `{ audio, chance_to_skip }`.

- No `every` → loop rail: one sound picked on arrival, looped until leave
- `every: n | [a, b]` → timed rail: wait, pick one, play once, wait again
- `chance_to_skip`: percent (matches intel `chance`). Loop → rail silent that visit | timed → that play skipped
- `volume` (default 1) | `type`: `music` | `ambience` | `sound` → which game volume setting rail follows
- `"ambience": ""` → no game ambience (store ambience stopped on arrival)

Event sfx: no code needed. Game's `ModLoader.LoadSoundsCoroutine` already registers `Sounds/*.wav|ogg|mp3` as `MOD_ID:FILE_NAME`; existing action `sound` + `{ "sound": id }` effect accept those ids. Was undocumented → documented.

---

## Changes

### Schema + model + loader

**Files:** `src/Core/Schema.cs` | `src/Core/Model.cs` | `src/Core/PackLoader.cs`
**Change Type:** Modified
**What Changed:**
- `Schema`: `areas` accepts `audio`; new `AudioTypes`, shapes `AudioRail` (`sounds`, `every`, `volume`, `type`) and `AudioEntry` (`audio`, `chance_to_skip`)
- `Model`: `AudioEntry`, `AudioRail`; `AreaDef.Audio`
- `PackLoader.CompileAudio`, called from `CompileArea`. Problems: rail not object | no sounds | sound not object | missing `audio` | `chance_to_skip` outside 0–100 | bad `every` | unknown `type` | `volume` < 0. Unknown keys → warning (via `CheckShape`)

**Why:** Sound ids not validated at load — game registers mod clips itself, Core can't see them, sim has none.

### Playback

**Files:** `src/Game/AreaAudio.cs` (Created) | `src/Game/ExpeditionLocation.cs` (Modified)
**What Changed:**
- `AreaAudio`: `Start` builds one host `GameObject` + one `AudioSource` per rail; `Tick` follows volume setting, counts timed rails down (frozen while `AudioListener.pause`), fires via `PlayOneShot`; `Stop` destroys host
- Volume = rail `volume` × `StoreSoundPlayer.musicAudioSource.volume` (`music`) | `ambianceAudioSource.volume` (`ambience`) | 1 (`sound`)
- Picks use `UnityEngine.Random`, never run dice → run rolls unchanged
- Unregistered id → one log warning per id, skipped
- `ExpeditionLocation`: `Arrive` starts audio (+ stops store ambience when `ambience` is `""`) | `Tick` ticks | `Leave` stops

### Tests + docs

**Files:** `tests/Sim/Program.cs` | `docs/areas.md` | `docs/effects.md` | `docs/actions.md`
**What Changed:**
- `TestAreaAudio`: parse, defaults, 9 problem reports, 1 unknown-key warning
- `docs/areas.md`: `audio` field row, `ambience` `""` note, new `### Audio` + `#### Your own sound files`
- `sound` rows in effects/actions link to own-sound-files section

---

## Verification

**Method:** `dotnet run -c Release --project tests/Sim` | game build with scratch `InstallDir` | `ef-sim lint docs/examples/Showcase --strict`

**Result:**
```
Outcomes: knocked_out=3742, cleared=1049, left=209; longest run 28 steps
All checks passed

ExpeditionFramework -> bin\Release\netstandard2.1\ExpeditionFramework.dll
0 warnings, 0 errors

lint ef_showcase: 0 error(s), 0 warning(s)
```

**Status:** PASS for loader + build. `src/Game` playback NOT verified — needs a play in game.

---

## Impact on Related Projex

| Document | Relationship | Update Made |
|----------|-------------|-------------|
| none | — | — |

---

## Notes

- Build went to scratch `InstallDir`; `Mods/ExpeditionFramework` still holds the previous build. Plain `dotnet build -c Release` installs it.
- Unheard in game: loop seam, volume following the options menu, store ambience stop on `"ambience": ""`, timed rails across pause.
- `music` rails read `musicAudioSource.volume` as the setting. If the game fades that source itself, music rails fade with it — unchecked.
- Game has no separate effects volume setting (master, music, background, dialog only) → `sound` rails follow master only.
- mp3 loads through Unity's `AudioType.MPEG`; mp3 loops usually carry a short gap at the seam. `.ogg` | `.wav` loop clean.
- Not done: sound per die rolled. Action `sound` plays once per press; same id in one frame is dropped by `GameSounds.Claim`.
- NightExpeditionsReborn untouched: its `sfx/` folder must become `Sounds/` for the game to load it; `areas.json` needs `audio` rails.
