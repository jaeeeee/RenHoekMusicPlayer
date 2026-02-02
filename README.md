Ren Höek’s Music Player

Video first (shows the whole UI + behavior):
https://www.youtube.com/watch?v=CflR2c4KIJ0

Repo: https://github.com/jaeeeee/RenHoekMusicPlayer

Releases: https://github.com/jaeeeee/RenHoekMusicPlayer/releases

What this is (the honest version)

This mod adds a music player widget inside RimWorld. It’s a normal RimWorld mod folder + a DLL — no installers, no EXEs, no scripts, no weird launchers.

This started as “I want better control over music in my run” and turned into a week-long learning project where I had to actually learn how RimWorld’s music system works, how to patch it safely with Harmony, how UI behaves in RimWorld, and how to keep the whole thing stable across edge cases.

I’m keeping everything public so anyone can audit it, build it, and verify what’s shipping.

Features (complete list)
UI Widget

Toggle button in RimWorld’s Play Settings area (adds a small music player icon)

Draggable window (position saved)

Optional lock position

Optional auto-collapse playlist

Optional scrolling title (marquee-style)

Optional pause music when the game is paused

UI scaling (window scale + font scaling)

Playback Controls

Play

Pause / Resume

Next track

Previous track

Restart current track

Seek bar (click/drag to jump in the song)

Time readout (current time + total time)

“Stop” that halts audio sources immediately (see Safety section)

Volume (that actually stays where you put it)

Uses RimWorld’s music volume times a mod volume multiplier

Keeps applying the effective volume so vanilla updates don’t “stomp” it during fades / transitions

Shuffle

Shuffle mode

When shuffle is enabled, the mod builds a shuffled list of eligible songs and walks it

Re-shuffles when the list is exhausted

Context-Aware Playback (optional)

When enabled, song selection can be filtered by context:

Day vs Night (based on hour: day is 6–18)

Winter (Season.Winter)

Combat (uses map dangerWatcher / StoryDanger)

Important detail (so nobody gets surprised):

Songs can be categorized automatically based on known vanilla tracks and/or their clipPath folder (e.g. /combat/, /winter/, /day/).

If something isn’t categorized, it behaves like “general music” and won’t disappear just because context filtering is on.

Install
Requirements

Harmony (load before this mod)

Install from GitHub Release (recommended)

Download the latest release zip from GitHub Releases.

Extract the RenHoekMusicPlayer folder into your RimWorld Mods folder:

Steam install: ...\Steam\steamapps\common\RimWorld\Mods\

Or Local mods: %APPDATA%\..\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Mods

Enable Harmony, then enable Ren Höek’s Music Player.

How to use (quick)

In-game, click the music player icon added to the Play Settings area.

Use the buttons:

Prev / Next

Play / Pause

Restart

Stop

Shuffle

Drag the seek bar to jump around in the current track.

Adjust volume with the slider.

Expand the playlist panel to browse/search and tweak context toggles where applicable.

Mod Settings (everything explained)

Open: Options → Mod Settings → Ren Höek’s Music Player

ShuffleEnabled — toggles shuffle behavior

ContextAwareEnabled — toggles context filtering (Day/Night/Winter/Combat)

IncludeVanillaSongs — include base game music in the mod’s selectable list

HidePlayer — hides the widget (you can still keep the mod active)

LockPosition — prevents dragging the widget

LockPlaylistOrientation — keeps playlist expansion orientation consistent

PauseMusicWhenGamePaused — auto-pauses when the game is paused

AutoCollapsePlaylist — collapses playlist automatically after interactions

BlockEventTriggers (“Total Stop”) — prevents events from forcing music back on after you stop it

ShowNowPlayingNotification — shows a small notification when track changes

ScrollingTitle — marquee/scrolling title display

MinSilence / MaxSilence — random silence window between songs (seconds)

FadeSpeedIndex — fade behavior for automatic transitions

ManualFadeSpeedIndex — fade behavior for manual skips

VolumeMultiplier — multiplies RimWorld music volume

DefaultPositionIndex — default window placement preset

TitleFontIndex / TimecodeFontIndex

ScaleUpClicks / ScaleDownClicks — UI scaling persistence

Safety / What this mod ships (no surprises)

Release zip contains a normal RimWorld mod folder:

About/ (metadata + preview)

Defs/ (keybind defs)

Languages/ (strings)

Textures/ (icons)

Assemblies/ (the DLL RimWorld loads)

No installers. No EXEs. No scripts.
Source code is in the repo under Source/.

About the Stop button

Stop uses Unity AudioSource.Stop() on music audio sources so it can actually stop playback immediately.
It doesn’t do anything outside the game process — it just stops audio sources.

If you want a softer behavior, use Pause instead.

Integrity / Verification (SHA256)

If you want to verify files match what I built on my machine:

Release zip (RenHoekMusicPlayer_v1.0.0.zip)
99E959E76723A81624C390D5283FF1BC1A4D23E0E85917F406008A6BEF5E2AA8

DLL (Assemblies/RenHoekMusicPlayer.dll)
B4BB53FFCF2EBEA681DADCAA61E4BAFC8D052DEF01007BBCC1EFE9FBC597A590

PowerShell:

Get-FileHash ".\Assemblies\RenHoekMusicPlayer.dll" -Algorithm SHA256
Get-FileHash "$env:USERPROFILE\Desktop\RenHoekMusicPlayer_v1.0.0.zip" -Algorithm SHA256


Note: if you rebuild the DLL yourself, hashes will change (expected).

Dev Notes (the complicated stuff I learned the hard way)

This is basically me reading from the notes I took while building it — not every tiny helper, but the parts that actually taught me something.

1) Harmony patching: “don’t fight vanilla, intercept it”

Vanilla will re-assert control (track selection, volume application, event-triggered music) unless you patch the pressure points.
I learned to patch only what I need so vanilla still does vanilla unless the player is actively in control.

Where to look: Source/HarmonyPatches.cs

2) Reflection: internal state you need isn’t always public

To drive a real player UI you need state like audio source/timers that isn’t always exposed.
Reflection is fine if you cache lookups, null-guard, and keep it centralized.

Where to look: Source/MusicPlayerController.cs

3) Fade transitions: treat it like a state machine

A “set volume once” fade isn’t reliable. It becomes reliable when you tick fades over time and re-apply effective volume consistently.

Where to look: MusicPlayerController.GameComponentUpdate() + fade helpers

4) Context filtering: never soft-lock into no songs

If a context bucket is empty, you need a fallback so players don’t feel like the mod “broke music.”

Where to look: MusicPlayerController.GetContextAppropiateSongs(...)

5) OnGUI: don’t do heavy work every frame

Cache filtered lists, add cooldowns to spammy actions, and keep UI as a caller (controller owns state).

Where to look: Source/MusicPlayerWidget.cs

6) Saving settings: Scribe + dictionaries

Stable keys + careful ExposeData() or you wipe/duplicate settings.

Where to look: MusicPlayerController.ExposeData()

Build from source

Targets net472.

cd .\Source
dotnet restore
dotnet build -c Release
