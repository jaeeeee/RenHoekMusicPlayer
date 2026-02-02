# Ren Hoek's Music Player

**Video first (shows the whole UI + behavior):**  
https://www.youtube.com/watch?v=CflR2c4KIJ0

**Repo:** https://github.com/jaeeeee/RenHoekMusicPlayer  
**Releases:** https://github.com/jaeeeee/RenHoekMusicPlayer/releases

---

## What this is (the honest version)

This mod adds a **music player widget** inside **RimWorld**. It's a normal RimWorld mod folder + a DLL — **no installers, no EXEs, no scripts, no weird launchers**.

This started as "I want better control over music in my run" and turned into a week-long learning project where I had to actually learn how RimWorld's music system works, how to patch it safely with Harmony, how UI behaves in RimWorld, and how to keep the whole thing stable across edge cases.

I'm keeping everything public so anyone can audit it, build it, and verify what's shipping.

---

## Features (complete list)

### UI widget
- Toggle button in RimWorld's **Play Settings** area (adds a small music player icon)
- Draggable window (position saved)
- Optional **lock position**
- Optional **auto-collapse playlist**
- Optional **scrolling title** (marquee-style)
- Optional **pause music when the game is paused**
- UI scaling (window scale + font scaling)

### Playback controls
- Play
- Pause / Resume
- Next track
- Previous track
- Restart current track
- Seek bar (click/drag to jump in the song)
- Time readout (current time + total time)
- "Stop" that halts audio sources immediately (see Safety section)

### Volume (that actually stays where you put it)
- Uses RimWorld's music volume *times* a **mod volume multiplier**
- Keeps applying the effective volume so vanilla updates don't stomp it during fades / transitions

### Shuffle
- Shuffle mode
- When shuffle is enabled, the mod builds a shuffled list of eligible songs and walks it
- Re-shuffles when the list is exhausted

### Context-aware playback (optional)
When enabled, song selection can be filtered by context:
- **Day** vs **Night** (based on hour: day is 6–18)
- **Winter** (`Season.Winter`)
- **Combat** (uses `map.dangerWatcher` / `StoryDanger`)

Important detail (so nobody gets surprised):
- Songs can be categorized automatically based on **known vanilla tracks** and/or their `clipPath` folder (e.g. `/combat/`, `/winter/`, `/day/`).
- If something isn't categorized, it behaves like "general music" and won't disappear just because context filtering is on.

---

## Install

### Requirements
- **Harmony** (load before this mod)

### Install from GitHub Release (recommended)
1. Download the latest release zip from GitHub Releases.
2. Extract the `RenHoekMusicPlayer` folder into your RimWorld Mods folder:
   - Steam install: `...\Steam\steamapps\common\RimWorld\Mods\`
   - Local mods: `%APPDATA%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Mods`
3. Enable **Harmony**, then enable **Ren Hoek's Music Player**.

---

## How to use (quick)

1. In-game, click the **music player icon** added to the Play Settings area.
2. Use the buttons:
   - Prev / Next
   - Play / Pause
   - Restart
   - Stop
   - Shuffle
3. Drag the seek bar to jump around in the current track.
4. Adjust volume with the slider.
5. Expand the playlist panel to browse/search and tweak context toggles where applicable.

---

## Mod Settings (everything explained)

Open: **Options → Mod Settings → Ren Hoek's Music Player**

- **ShuffleEnabled** — toggles shuffle behavior  
- **ContextAwareEnabled** — toggles context filtering (Day/Night/Winter/Combat)  
- **IncludeVanillaSongs** — include base game music in the mod's selectable list  
- **HidePlayer** — hides the widget (you can still keep the mod active)  
- **LockPosition** — prevents dragging the widget  
- **LockPlaylistOrientation** — keeps playlist expansion orientation consistent  
- **PauseMusicWhenGamePaused** — auto-pauses when the game is paused  
- **AutoCollapsePlaylist** — collapses playlist automatically after interactions  
- **BlockEventTriggers** ("Total Stop") — prevents events from forcing music back on after you stop it  
- **ShowNowPlayingNotification** — shows a small notification when track changes  
- **ScrollingTitle** — marquee/scrolling title display  
- **MinSilence / MaxSilence** — random silence window between songs (seconds)  
- **FadeSpeedIndex** — fade behavior for automatic transitions  
- **ManualFadeSpeedIndex** — fade behavior for manual skips  
- **VolumeMultiplier** — multiplies RimWorld music volume  
- **DefaultPositionIndex** — default window placement preset  
- **TitleFontIndex / TimecodeFontIndex**  
- **ScaleUpClicks / ScaleDownClicks** — UI scaling persistence  

---

## Safety / What this mod ships (no surprises)

Release zip contains a normal RimWorld mod folder:
- `About/` (metadata + preview)
- `Defs/` (keybind defs)
- `Languages/` (strings)
- `Textures/` (icons)
- `Assemblies/` (the DLL RimWorld loads)

**No installers. No EXEs. No scripts.**  
Source code is in the repo under `Source/`.

### About the Stop button
Stop uses Unity `AudioSource.Stop()` on music audio sources so it can *actually* stop playback immediately.  
It doesn't do anything outside the game process — it just stops audio sources.

If you want a softer behavior, use Pause instead.

---

## Integrity / Verification (SHA256)

If you want to verify files match what I built on my machine:

**Release zip** (`RenHoekMusicPlayer_v1.0.0.zip`)  
`99E959E76723A81624C390D5283FF1BC1A4D23E0E85917F406008A6BEF5E2AA8`

**DLL** (`Assemblies/RenHoekMusicPlayer.dll`)  
`B4BB53FFCF2EBEA681DADCAA61E4BAFC8D052DEF01007BBCC1EFE9FBC597A590`

# Dev Notes (the complicated stuff I learned the hard way)

This is basically me reading from the notes I took while building it — not every tiny helper, but the parts that actually taught me something.

## 1) Harmony patching: "don't fight vanilla, intercept it"

**What I thought at first:**  
"If I call play/stop myself, RimWorld will just… chill."

**What actually happened:**  
Vanilla music logic re-asserts itself (auto-start, next-song selection, volume getting re-applied). If you don't patch thoughtfully you get stuff like:
- you stop music and it starts again
- you set a volume and it gets stomped
- you try to control playback and the game keeps making decisions behind your back

**What I learned / how I solved it:**
- Patch only the parts that conflict with *user intent* (don't rewrite everything).
- Make "manual control" an explicit internal state so vanilla can't immediately override.
- If "Stop means STOP" exists, it needs a real internal flag *and* patches that respect it.

**Where to look:** `Source/HarmonyPatches.cs`

---

## 2) Reflection into RimWorld's music manager: you need state the game doesn't expose

**The problem:**  
RimWorld's music system has internal fields you *need* (audio source, last started song, timers/state), but it's not all available through a neat public API.

**What I learned:**
- Reflection is fine if you do it responsibly:
  1. Cache field/property lookups once (don't string-lookup every frame)
  2. Guard against nulls / missing fields across versions
  3. Keep reflection centralized so it's easy to audit

**Why this matters in practice:**
- UI has to show what's *actually* playing, not what I *think* should be playing.
- Seek/volume/pause must hit the real audio source the game is using, or you desync instantly.

**Where to look:** `Source/MusicPlayerController.cs`

---

## 3) Fade transitions: simple idea, annoying details

**What I wanted:**  
Fade out → swap track → fade in. Easy.

**What I learned:**
- Volume can be overwritten by vanilla updates, so a "set volume once" fade doesn't work.
- It becomes reliable when you treat it like a small **state machine**:
  - `Playing`
  - `Paused`
  - `FadingOut`
  - `FadingIn`
  - `SilenceWindow` (optional)

**What finally made it click:**  
Once fade transitions were explicit states with timestamps, bugs stopped being "random" and turned into:
"Oh, I forgot to handle `FadeOutComplete → start next song` / `silence window`."

**Where to look:**
- `MusicPlayerController.GameComponentUpdate()` (tick logic)
- fade helpers like `StartFadeOutThenPlay(...)`

---

## 4) Context filtering: never let users tag themselves into a dead end

**The trap:**  
Context-aware filtering sounds easy until someone tags everything wrong and gets **zero eligible songs**.

**My rule:**
- Filter when enabled
- But never allow a "no songs match" soft-lock  
  If the filtered list is empty, fall back to "all songs"

That sounds small but it's the difference between:
- "this is cool"
- "this mod broke my music"

**Where to look:** `MusicPlayerController.GetContextAppropiateSongs(...)`

---

## 5) RimWorld UI / OnGUI: powerful, but you have to be disciplined

**Stuff I got wrong early:**
- doing too much work every frame
- allowing spam-clicking (next/prev spam causes weirdness fast)
- letting layout jitter / resize weirdly

**What I learned:**
- Cache anything you can (search keys, derived display names, filtered lists).
- Add cooldowns to skip actions.
- Keep UI separate from logic: UI calls controller actions; controller owns state.

**Where to look:** `Source/MusicPlayerWidget.cs`

---

## 6) Saving per-song tags + settings: Scribe + stable keys

**What I learned:**
- Use a stable key (usually `defName`) for per-song settings.
- Handle "mod list changed" cases gracefully (songs removed/added).
- `ExposeData()` needs to be careful so you don't wipe data or create dupes.

**Where to look:** `MusicPlayerController.ExposeData()`
