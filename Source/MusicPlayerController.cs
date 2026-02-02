using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RenHoek.MusicPlayer
{
    /// <summary>
    /// Unified status enum for cleaner state management
    /// </summary>
    public enum MusicStatus
    {
        Idle,
        Playing,
        Paused,
        FadingOut,
        FadingIn,
        WaitingBetweenSongs
    }

    public class MusicPlayerController : GameComponent
    {
        public static MusicPlayerController Instance { get; private set; }
        public List<SongInfo> AllSongs { get; private set; } = new List<SongInfo>();
        public SongInfo CurrentSong { get; private set; }
        
        // Status enum replaces IsPlaying/IsPaused bools
        public MusicStatus CurrentStatus { get; private set; } = MusicStatus.Idle;
        
        // Legacy compatibility properties
        public bool IsPlaying => CurrentStatus == MusicStatus.Playing || 
                                 CurrentStatus == MusicStatus.FadingOut || 
                                 CurrentStatus == MusicStatus.FadingIn;
        public bool IsPaused => CurrentStatus == MusicStatus.Paused;
        public bool IsFading => CurrentStatus == MusicStatus.FadingOut || 
                                CurrentStatus == MusicStatus.FadingIn;
        public bool IsInCombat { get; private set; }
        public int SongListVersion { get; private set; } = 0;
        
        public Dictionary<string, SongTimeSettings> SongSettings = new Dictionary<string, SongTimeSettings>();
        
        private List<SongInfo> shuffleQueue = new List<SongInfo>();
        private int shuffleIndex = 0;
        private System.Random random = new System.Random();
        private MusicManagerPlay vanillaMusicManager;
        
        // Reflection fields for vanilla access
        private AudioSource cachedAudioSource = null;
        private System.Reflection.FieldInfo lastStartedSongField = null;
        private System.Reflection.FieldInfo audioSourceField = null;
        
        // Auto-start tracking
        private bool hasAutoStarted = false;
        private int framesWaited = 0;
        private const int FramesToWaitBeforeAutoStart = 60;

        // === FADE SYSTEM ===
        private float currentFadeDuration = 1.8f;        // Active fade duration (from settings)
        private float fadeProgress = 0f;
        private float volumeBeforeFade = 1f;
        private float currentFadeVolume = 1f;  // Exposed for Harmony patch
        private SongInfo pendingSong = null;
        private bool wasInCombat = false;
        private int nextCombatCheckTick = 0;
        private bool eventTriggersBlocked = false;
        public bool PausedDueToGamePause { get; private set; } = false;
        private bool isSkipTransition = false;  // True if skip/prev triggered this fade
        
        // Random silence between songs (values from Settings)
        private float silenceTimer = 0f;
        private float currentSilenceDuration = 0f;

        public MusicPlayerController(Game game)
        {
            Instance = this;
            InitializeSongList();
            CacheReflectionFields();
        }
        
        private void CacheReflectionFields()
        {
            lastStartedSongField = typeof(MusicManagerPlay).GetField("lastStartedSong", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            audioSourceField = typeof(MusicManagerPlay).GetField("audioSource", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref SongSettings, "SongSettings", LookMode.Value, LookMode.Deep);
            if (SongSettings == null)
                SongSettings = new Dictionary<string, SongTimeSettings>();
        }

        public void InitializeSongList()
        {
            AllSongs.Clear();
            
            foreach (var songDef in DefDatabase<SongDef>.AllDefsListForReading)
            {
                if (songDef == null) continue;
                
                var songInfo = new SongInfo(songDef);
                
                if (songInfo.IsEntrySong) continue;
                
                // Only filter vanilla songs if IncludeVanillaSongs is disabled
                if (songInfo.IsVanilla && !(MusicPlayerMod.Settings?.IncludeVanillaSongs ?? false)) continue;
                
                AllSongs.Add(songInfo);
                
                if (!SongSettings.ContainsKey(songDef.defName))
                {
                    SongSettings[songDef.defName] = new SongTimeSettings(songInfo);
                }
            }

            AllSongs = AllSongs
                .OrderBy(s => s.CategorySortOrder)
                .ThenBy(s => s.DisplayName)
                .ToList();
            
            SongListVersion++;
            Log.Message($"[Ren Höek's Music Player] Loaded {AllSongs.Count} songs (IncludeVanilla={MusicPlayerMod.Settings?.IncludeVanillaSongs ?? false}).");
            ReshuffleQueue();
        }

        public override void GameComponentOnGUI()
        {
            if (vanillaMusicManager == null)
            {
                vanillaMusicManager = Find.MusicManagerPlay;
                cachedAudioSource = null;
            }
            
            // Process fade every frame for smooth transitions
            // Only on Repaint to avoid multiple calls per frame (OnGUI fires for each event type)
            if (Event.current.type == EventType.Repaint)
            {
                ProcessFade();
            }
            
            if (vanillaMusicManager != null)
            {
                DetectCurrentSongAggressive();
                
                if (!hasAutoStarted && CurrentSong == null && AllSongs.Count > 0)
                {
                    framesWaited++;
                    if (framesWaited >= FramesToWaitBeforeAutoStart)
                    {
                        hasAutoStarted = true;
                        Log.Message("[Ren Höek's Music Player] Auto-starting playlist");
                        PlayNext();
                    }
                }
            }
            
            // Widget is now drawn via Harmony patch on MapInterface.MapInterfaceOnGUI_AfterMainTabs
        }
        
        public override void GameComponentTick()
        {
            base.GameComponentTick();

            if (!IsPlaying)
                return;

            if (MusicPlayerMod.Settings?.ContextAware != true)
                return;

            // Scanning pawns every frame is expensive. Check a few times per second instead.
            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame < nextCombatCheckTick)
                return;

            nextCombatCheckTick = ticksGame + 15; // ~0.25s at 60 ticks/sec
            UpdateCombatState();
        }

        public override void GameComponentUpdate()
        {
            if (vanillaMusicManager == null)
            {
                vanillaMusicManager = Find.MusicManagerPlay;
            }
            
            // Handle pause-with-game setting
            if (MusicPlayerMod.Settings.PauseMusicWhenGamePaused)
            {
                bool gamePaused = Find.TickManager?.Paused ?? false;
                var audioSource = GetAudioSource();
                
                if (gamePaused && CurrentStatus == MusicStatus.Playing && !PausedDueToGamePause)
                {
                    // Game just paused - pause music
                    if (audioSource != null)
                    {
                        audioSource.Pause();
                        PausedDueToGamePause = true;
                    }
                }
                else if (!gamePaused && PausedDueToGamePause)
                {
                    // Game unpaused - resume music
                    if (audioSource != null)
                    {
                        audioSource.UnPause();
                    }
                    PausedDueToGamePause = false;
                }
            }
            else if (PausedDueToGamePause)
            {
                // Setting was disabled while paused - unpause
                var audioSource = GetAudioSource();
                if (audioSource != null)
                {
                    audioSource.UnPause();
                }
                PausedDueToGamePause = false;
            }

            // Check if current song is near end - start fading early so it completes smoothly
            if (CurrentStatus == MusicStatus.Playing && CurrentSong != null)
            {
                var audioSource = GetAudioSource();
                if (audioSource != null && audioSource.clip != null)
                {
                    float fadeDuration = MusicPlayerMod.Settings?.GetFadeDuration() ?? 1.8f;
                    float timeRemaining = audioSource.clip.length - audioSource.time;
                    
                    // Start fade when we're within fade duration of the end
                    if (fadeDuration > 0 && timeRemaining <= fadeDuration && timeRemaining > 0 && audioSource.isPlaying)
                    {
                        // Calculate how far into the fade we already are
                        float alreadyFaded = fadeDuration - timeRemaining;
                        float initialProgress = alreadyFaded / fadeDuration;
                        
                        // Start fade to next song with progress already set
                        PlayNextNaturalWithProgress(initialProgress);
                    }
                    // Fallback: song already ended (edge case)
                    else if (!audioSource.isPlaying && audioSource.time == 0)
                    {
                        PlayNextNatural();
                    }
                }
            }
        }
        
        /// <summary>
        /// Called when a song ends naturally - uses normal fade speed and includes silence
        /// </summary>
        private void PlayNextNatural()
        {
            PlayNextNaturalWithProgress(0f);
        }
        
        private void PlayNextNaturalWithProgress(float initialProgress)
        {
            if (AllSongs.Count == 0) return;
            
            SongInfo nextSong = null;

            if (MusicPlayerMod.Settings.ShuffleEnabled)
            {
                nextSong = GetNextShuffleSong();
            }
            else
            {
                int currentIndex = CurrentSong != null ? AllSongs.IndexOf(CurrentSong) : -1;
                int nextIndex = (currentIndex + 1) % AllSongs.Count;
                nextSong = AllSongs[nextIndex];
            }
            
            if (nextSong != null) StartFadeOutThenPlay(nextSong, isSkip: false, initialProgress: initialProgress);
        }

        // === COMBAT DETECTION ===
        private void UpdateCombatState()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            
            // Primary check: DangerWatcher rating
            bool inCombat = map.dangerWatcher != null && 
                            map.dangerWatcher.DangerRating >= StoryDanger.High;
            
            // Fallback: Check if any colonist is actively in combat (fleeing, attacking, etc.)
            if (!inCombat)
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.CurJob != null && 
                        (pawn.CurJob.def == JobDefOf.AttackMelee || 
                         pawn.CurJob.def == JobDefOf.AttackStatic ||
                         pawn.CurJob.def == JobDefOf.FleeAndCower))
                    {
                        inCombat = true;
                        break;
                    }
                }
            }
            
            IsInCombat = inCombat;
            
            // Only auto-switch if context-aware is enabled and stop hasn't blocked events
            if (!MusicPlayerMod.Settings.ContextAware || eventTriggersBlocked)
            {
                wasInCombat = inCombat;
                return;
            }
            
            // Detect combat state change
            if (inCombat != wasInCombat)
            {
                // Only process transition if not already fading
                if (!IsFading)
                {
                    Log.Message($"[Ren Höek's Music Player] Combat state changed: {wasInCombat} -> {inCombat}");
                    
                    if (inCombat)
                    {
                        // Entering combat - switch to combat music
                        Log.Message("[Ren Höek's Music Player] Combat started - looking for combat music");
                        StartCombatTransition(true);
                    }
                    else
                    {
                        // Leaving combat - switch to peaceful music
                        Log.Message("[Ren Höek's Music Player] Combat ended - switching to peaceful music");
                        StartCombatTransition(false);
                    }
                    
                    // Only update wasInCombat when we actually process the transition
                    wasInCombat = inCombat;
                }
                // If fading, don't update wasInCombat - we'll catch it next tick
            }
        }
        
        /// <summary>
        /// Immediately refresh combat state without triggering transitions.
        /// Used when user clicks skip/prev to sync UI with actual game state.
        /// </summary>
        private void RefreshCombatStateNow()
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                IsInCombat = false;
                wasInCombat = false;
                return;
            }
            
            bool inCombat = map.dangerWatcher != null && 
                            map.dangerWatcher.DangerRating >= StoryDanger.High;
            
            if (!inCombat)
            {
                foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
                {
                    if (pawn.CurJob != null && 
                        (pawn.CurJob.def == JobDefOf.AttackMelee || 
                         pawn.CurJob.def == JobDefOf.AttackStatic ||
                         pawn.CurJob.def == JobDefOf.FleeAndCower))
                    {
                        inCombat = true;
                        break;
                    }
                }
            }
            
            IsInCombat = inCombat;
            wasInCombat = inCombat;  // Sync both so no transition triggers
        }

        private void StartCombatTransition(bool toCombat)
        {
            SongInfo targetSong = null;
            
            if (toCombat)
            {
                // Find a combat song
                var combatSongs = AllSongs.Where(s => {
                    var settings = GetSongSettings(s);
                    return settings != null && settings.AllowCombat;
                }).ToList();
                
                Log.Message($"[Ren Höek's Music Player] Found {combatSongs.Count} combat songs");
                
                if (combatSongs.Count > 0)
                    targetSong = combatSongs[random.Next(combatSongs.Count)];
                else
                    Log.Warning("[Ren Höek's Music Player] No combat songs available! Add songs to a 'Combat' folder.");
            }
            else
            {
                // Find a peaceful song
                var peacefulSongs = GetContextAppropiateSongs().Where(s => {
                    var settings = GetSongSettings(s);
                    return settings == null || !settings.AllowCombat || settings.AllowDay || settings.AllowNight;
                }).ToList();
                
                if (peacefulSongs.Count > 0)
                    targetSong = peacefulSongs[random.Next(peacefulSongs.Count)];
            }
            
            if (targetSong != null)
            {
                StartFadeOutThenPlay(targetSong);
            }
        }

        // === FADE SYSTEM ===
        
        /// <summary>
        /// Start fading out, then play the specified song
        /// </summary>
        /// <param name="nextSong">Song to play after fade</param>
        /// <param name="isSkip">True for skip/prev buttons (fast fade, no silence), false for natural transitions</param>
        public void StartFadeOutThenPlay(SongInfo nextSong, bool isSkip = false, float initialProgress = 0f)
        {
            if (nextSong?.Def == null) return;
            
            // Get fade duration from settings (or skip duration for manual skip)
            float settingsFade = MusicPlayerMod.Settings?.GetFadeDuration() ?? 1.8f;
            
            var audioSource = GetAudioSource();
            bool audioPlaying = audioSource != null && audioSource.isPlaying;
            
            // If no audio playing (song ended naturally), go straight to silence or play
            if (!audioPlaying)
            {
                // Get silence settings
                float minSilence = MusicPlayerMod.Settings?.MinSilenceBetweenSongs ?? 2f;
                float maxSilence = MusicPlayerMod.Settings?.MaxSilenceBetweenSongs ?? 25f;
                
                // Natural song end - add silence if not in combat and not a skip
                if (!IsInCombat && !isSkip && maxSilence > 0)
                {
                    pendingSong = nextSong;
                    isSkipTransition = false;
                    silenceTimer = 0f;
                    currentSilenceDuration = (float)(minSilence + random.NextDouble() * (maxSilence - minSilence));
                    CurrentStatus = MusicStatus.WaitingBetweenSongs;
                    Log.Message($"[Ren Höek's Music Player] Song ended, waiting {currentSilenceDuration:F1}s before next song");
                    return;
                }
                
                // Skip or combat - play directly
                PlayDirect(nextSong);
                return;
            }
            
            // Audio is playing - do normal fade
            if (settingsFade <= 0)
            {
                // Fade disabled - just play directly
                PlayDirect(nextSong);
                return;
            }
            
            pendingSong = nextSong;
            isSkipTransition = isSkip;
            float manualFade = MusicPlayerMod.Settings?.GetManualFadeDuration() ?? 0.4f;
            currentFadeDuration = isSkip ? manualFade : settingsFade;
            volumeBeforeFade = GetEffectiveVolume();
            
            // Start fade at initial progress (for seeking near end of track)
            fadeProgress = Mathf.Clamp01(initialProgress);
            // Calculate volume based on how far into fade we already are
            float t = fadeProgress * fadeProgress; // ease-out curve
            currentFadeVolume = Mathf.Lerp(volumeBeforeFade, 0f, t);
            ApplyFadeVolume();
            
            CurrentStatus = MusicStatus.FadingOut;
            
            Log.Message($"[Ren Höek's Music Player] Fading out ({currentFadeDuration}s, starting at {fadeProgress:P0}), next: {nextSong.DisplayName}");
        }

        /// <summary>
        /// Called every frame during OnGUI for smooth fading
        /// </summary>
        private void ProcessFade()
        {
            switch (CurrentStatus)
            {
                case MusicStatus.FadingOut:
                    ProcessFadeOut();
                    break;
                case MusicStatus.FadingIn:
                    ProcessFadeIn();
                    break;
                case MusicStatus.WaitingBetweenSongs:
                    ProcessSilence();
                    break;
            }
        }

        private void ProcessFadeOut()
        {
            fadeProgress += Time.unscaledDeltaTime / currentFadeDuration;
            
            if (fadeProgress >= 1f)
            {
                // Fade out complete
                currentFadeVolume = 0f;
                ApplyFadeVolume();
                
                // Get silence settings
                float minSilence = MusicPlayerMod.Settings?.MinSilenceBetweenSongs ?? 2f;
                float maxSilence = MusicPlayerMod.Settings?.MaxSilenceBetweenSongs ?? 25f;
                
                // Skip silence during combat OR when using skip/prev buttons
                if (!IsInCombat && !isSkipTransition && maxSilence > 0)
                {
                    // FIX: Stop the audio before entering silence period
                    // This prevents vanilla MusicUpdate from resetting volume to 100%
                    var audioSource = GetAudioSource();
                    if (audioSource != null)
                    {
                        audioSource.Stop();
                    }
                    
                    silenceTimer = 0f;
                    currentSilenceDuration = (float)(minSilence + random.NextDouble() * (maxSilence - minSilence));
                    CurrentStatus = MusicStatus.WaitingBetweenSongs;
                    Log.Message($"[Ren Höek's Music Player] Waiting {currentSilenceDuration:F1}s between songs");
                }
                else
                {
                    CompleteFadeOut();
                }
            }
            else
            {
                // Ease-out curve for smoother fade
                float t = 1f - Mathf.Pow(1f - fadeProgress, 2f);
                currentFadeVolume = Mathf.Lerp(volumeBeforeFade, 0f, t);
                ApplyFadeVolume();
            }
        }

        private void ProcessSilence()
        {
            silenceTimer += Time.unscaledDeltaTime;
            // Also skip remaining silence if combat starts
            if (silenceTimer >= currentSilenceDuration || IsInCombat)
            {
                CompleteFadeOut();
            }
        }

        private void CompleteFadeOut()
        {
            if (pendingSong != null)
            {
                var songToPlay = pendingSong;
                pendingSong = null;
                PlayDirect(songToPlay);
                
                // Start fade in
                fadeProgress = 0f;
                currentFadeVolume = 0f;
                CurrentStatus = MusicStatus.FadingIn;
            }
            else
            {
                CurrentStatus = MusicStatus.Idle;
            }
        }

        private void ProcessFadeIn()
        {
            fadeProgress += Time.unscaledDeltaTime / currentFadeDuration;
            
            if (fadeProgress >= 1f)
            {
                // Fade in complete
                currentFadeVolume = GetEffectiveVolume();
                ApplyFadeVolume();
                CurrentStatus = MusicStatus.Playing;
                isSkipTransition = false;  // Reset for next transition
                Log.Message("[Ren Höek's Music Player] Fade complete");
            }
            else
            {
                // Ease-in curve
                float t = fadeProgress * fadeProgress;
                currentFadeVolume = Mathf.Lerp(0f, GetEffectiveVolume(), t);
                ApplyFadeVolume();
            }
        }

        private void ApplyFadeVolume()
        {
            var audioSource = GetAudioSource();
            if (audioSource != null)
            {
                audioSource.volume = currentFadeVolume;
            }
        }

        /// <summary>
        /// Called by Harmony patch to get the volume we want during fade
        /// </summary>
        public float GetFadeVolume()
        {
            return currentFadeVolume;
        }

        /// <summary>
        /// Get the effective volume (player setting * game setting)
        /// </summary>
        public float GetEffectiveVolume()
        {
            return Prefs.VolumeMusic * MusicPlayerMod.Settings.PlayerVolume;
        }

        // === SONG DETECTION ===
        private void DetectCurrentSongAggressive()
        {
            try
            {
                var audioSource = GetAudioSource();
                if (audioSource == null) return;
                
                bool audioPlaying = audioSource.isPlaying;
                bool hasClip = audioSource.clip != null;
                bool hasTime = audioSource.time > 0;
                
                if (lastStartedSongField == null) return;
                var songDef = lastStartedSongField.GetValue(vanillaMusicManager) as SongDef;
                
                if (songDef != null && hasClip)
                {
                    var matchingSong = AllSongs.FirstOrDefault(s => s.Def == songDef);
                    
                    if (matchingSong != null)
                    {
                        if (CurrentSong != matchingSong)
                        {
                            CurrentSong = matchingSong;
                            Log.Message($"[Ren Höek's Music Player] Detected: {matchingSong.DisplayName}");
                        }
                        
                        // Update status if not fading
                        if (!IsFading && CurrentStatus != MusicStatus.WaitingBetweenSongs)
                        {
                            if (audioPlaying)
                                CurrentStatus = MusicStatus.Playing;
                            else if (hasTime)
                                CurrentStatus = MusicStatus.Paused;
                        }
                    }
                }
                
                if (audioPlaying && CurrentSong == null && songDef != null)
                {
                    var matchingSong = AllSongs.FirstOrDefault(s => s.Def == songDef);
                    if (matchingSong != null)
                    {
                        CurrentSong = matchingSong;
                        CurrentStatus = MusicStatus.Playing;
                        Log.Message($"[Ren Höek's Music Player] Secondary detect: {matchingSong.DisplayName}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (Time.frameCount % 600 == 0)
                    Log.Warning($"[Ren Höek's Music Player] Detection error: {ex.Message}");
            }
        }
        
        public SongTimeSettings GetSongSettings(SongInfo song)
        {
            if (song?.Def == null) return null;
            
            if (!SongSettings.TryGetValue(song.Def.defName, out var settings))
            {
                settings = new SongTimeSettings(song);
                SongSettings[song.Def.defName] = settings;
            }
            return settings;
        }

        // === PLAYBACK CONTROLS ===

        /// <summary>
        /// User-initiated play - cancels any fade and plays immediately
        /// </summary>
        public void Play(SongInfo song)
        {
            if (song?.Def == null) return;
            
            // User manually playing - unblock event triggers
            eventTriggersBlocked = false;
            
            // Cancel any ongoing fade
            if (IsFading)
            {
                pendingSong = null;
                var audioSource = GetAudioSource();
                if (audioSource != null)
                    audioSource.volume = GetEffectiveVolume();
            }
            
            PlayDirect(song);
        }

        /// <summary>
        /// Internal play - no fade consideration
        /// </summary>
        private void PlayDirect(SongInfo song)
        {
            if (song?.Def == null) return;
            try
            {
                CurrentSong = song;
                CurrentStatus = MusicStatus.Playing;
                currentFadeVolume = GetEffectiveVolume();
                
                if (vanillaMusicManager != null)
                {
                    vanillaMusicManager.ForcePlaySong(song.Def, false);
                    
                    // ForcePlaySong resets volume - reapply ours
                    var audioSource = GetAudioSource();
                    if (audioSource != null)
                        audioSource.volume = currentFadeVolume;
                    
                    // Show "Now Playing" notification if enabled
                    if (MusicPlayerMod.Settings.ShowNowPlayingNotification)
                    {
                        Messages.Message($"♪ Now Playing: {song.DisplayName}", MessageTypeDefOf.SilentInput, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[Ren Höek's Music Player] Error playing: {ex.Message}");
            }
        }

        public void TogglePause()
        {
            var audioSource = GetAudioSource();
            if (audioSource == null) return;
            
            if (CurrentStatus == MusicStatus.Paused)
            {
                audioSource.UnPause();
                CurrentStatus = MusicStatus.Playing;
            }
            else if (CurrentStatus == MusicStatus.Playing)
            {
                audioSource.Pause();
                CurrentStatus = MusicStatus.Paused;
            }
        }

        public void PlayNext()
        {
            if (AllSongs.Count == 0) return;
            eventTriggersBlocked = false;
            RefreshCombatStateNow();  // Immediate refresh so UI updates
            
            // Cancel any ongoing fade or silence
            if (IsFading || CurrentStatus == MusicStatus.WaitingBetweenSongs)
            {
                pendingSong = null;
                var audioSource = GetAudioSource();
                if (audioSource != null)
                    audioSource.volume = GetEffectiveVolume();
            }
            
            SongInfo nextSong = null;

            if (MusicPlayerMod.Settings.ShuffleEnabled)
            {
                // Shuffle mode: get random context-appropriate song
                nextSong = GetNextShuffleSong();
            }
            else
            {
                // Sequential mode: find next context-appropriate song in playlist order
                var appropriateSongs = GetContextAppropiateSongs();
                int currentIndex = CurrentSong != null ? AllSongs.IndexOf(CurrentSong) : -1;
                
                // Search forward for next appropriate song
                for (int i = 1; i <= AllSongs.Count; i++)
                {
                    int checkIndex = (currentIndex + i) % AllSongs.Count;
                    if (appropriateSongs.Contains(AllSongs[checkIndex]))
                    {
                        nextSong = AllSongs[checkIndex];
                        break;
                    }
                }
                
                // Fallback if no appropriate song found
                if (nextSong == null && AllSongs.Count > 0)
                {
                    nextSong = AllSongs[(currentIndex + 1) % AllSongs.Count];
                }
            }
            
            if (nextSong != null) StartFadeOutThenPlay(nextSong, isSkip: true);
        }

        public void PlayPrevious()
        {
            if (AllSongs.Count == 0) return;
            eventTriggersBlocked = false;
            RefreshCombatStateNow();  // Immediate refresh so UI updates
            
            // Cancel any ongoing silence
            if (CurrentStatus == MusicStatus.WaitingBetweenSongs)
            {
                pendingSong = null;
            }
            
            SongInfo prevSong = null;
            
            if (MusicPlayerMod.Settings.ShuffleEnabled)
            {
                // Shuffle mode: get random context-appropriate song
                prevSong = GetNextShuffleSong();
            }
            else
            {
                // Sequential mode: find previous context-appropriate song in playlist order
                var appropriateSongs = GetContextAppropiateSongs();
                int currentIndex = CurrentSong != null ? AllSongs.IndexOf(CurrentSong) : 0;
                
                // Search backward for previous appropriate song
                for (int i = 1; i <= AllSongs.Count; i++)
                {
                    int checkIndex = (currentIndex - i + AllSongs.Count) % AllSongs.Count;
                    if (appropriateSongs.Contains(AllSongs[checkIndex]))
                    {
                        prevSong = AllSongs[checkIndex];
                        break;
                    }
                }
                
                // Fallback if no appropriate song found
                if (prevSong == null && AllSongs.Count > 0)
                {
                    prevSong = AllSongs[(currentIndex - 1 + AllSongs.Count) % AllSongs.Count];
                }
            }
            
            if (prevSong != null) StartFadeOutThenPlay(prevSong, isSkip: true);
        }

        public void RestartSong()
        {
            var audioSource = GetAudioSource();
            if (audioSource != null)
            {
                audioSource.time = 0f;
            }
        }

        public void SeekTo(float normalizedPosition)
        {
            var audioSource = GetAudioSource();
            if (audioSource?.clip == null) return;
            
            float targetTime = Mathf.Clamp01(normalizedPosition) * audioSource.clip.length;
            audioSource.time = targetTime;
        }

        public void Stop()
        {
            pendingSong = null;
            
            // If setting enabled, block events from loading music until manual play
            if (MusicPlayerMod.Settings.BlockEventTriggers)
                eventTriggersBlocked = true;
            
            var audioSource = GetAudioSource();
            if (audioSource != null)
            {
                audioSource.volume = GetEffectiveVolume();
                audioSource.Stop();
            }
            CurrentStatus = MusicStatus.Idle;
            CurrentSong = null;
        }

        // === CONTEXT MATCHING ===
        public bool SongMatchesContext(SongInfo song)
        {
            if (song?.Def == null) return true;
            
            var settings = GetSongSettings(song);
            if (settings == null) return true;
            
            if (settings.AllowAnytime) return true;
            
            var map = Find.CurrentMap;
            if (map == null) return true;

            bool inCombat = map.dangerWatcher != null && map.dangerWatcher.DangerRating >= StoryDanger.High;
            if (inCombat && settings.AllowCombat) return true;
            if (inCombat && !settings.AllowCombat) return false;

            var currentSeason = GenLocalDate.Season(map);
            if (currentSeason == Season.Winter && settings.AllowWinter) return true;

            float hour = GenLocalDate.HourFloat(map);
            bool isDay = hour >= 6f && hour < 18f;
            
            if (isDay && settings.AllowDay) return true;
            if (!isDay && settings.AllowNight) return true;

            return false;
        }

        public List<SongInfo> GetContextAppropiateSongs()
        {
            // If context-aware is disabled, return all songs
            if (!(MusicPlayerMod.Settings?.ContextAware ?? true))
            {
                return AllSongs;
            }
            
            // Filter songs based on current context (time of day, combat, season)
            var appropriate = AllSongs.Where(s => SongMatchesContext(s)).ToList();
            
            // If no songs match context, fall back to all songs
            if (appropriate.Count == 0)
            {
                Log.Warning("[Ren Höek's Music Player] No songs match current context, using all songs");
                return AllSongs;
            }
            
            return appropriate;
        }

        // === SHUFFLE ===
        private SongInfo GetNextShuffleSong()
        {
            var appropriateSongs = GetContextAppropiateSongs();
            if (appropriateSongs.Count == 0) return null;

            if (shuffleQueue.Count == 0 || !shuffleQueue.Any(s => appropriateSongs.Contains(s)))
            {
                ReshuffleQueue(appropriateSongs);
            }

            while (shuffleIndex < shuffleQueue.Count)
            {
                var song = shuffleQueue[shuffleIndex];
                shuffleIndex++;
                if (appropriateSongs.Contains(song)) return song;
            }

            ReshuffleQueue(appropriateSongs);
            shuffleIndex = 1;
            return shuffleQueue.Count > 0 ? shuffleQueue[0] : null;
        }

        private void ReshuffleQueue(List<SongInfo> songs = null)
        {
            songs = songs ?? AllSongs;
            shuffleQueue = songs.OrderBy(x => random.Next()).ToList();
            shuffleIndex = 0;
        }

        // === AUDIO ACCESS ===
        public AudioSource GetAudioSource()
        {
            try
            {
                if (vanillaMusicManager == null) return null;
                
                if (cachedAudioSource != null) return cachedAudioSource;
                
                if (audioSourceField == null) return null;
                cachedAudioSource = audioSourceField.GetValue(vanillaMusicManager) as AudioSource;
                return cachedAudioSource;
            }
            catch { return null; }
        }

        public float GetPlaybackProgress()
        {
            var audioSource = GetAudioSource();
            if (audioSource?.clip == null) return 0f;
            return audioSource.time / audioSource.clip.length;
        }

        public string GetCurrentTimeFormatted()
        {
            var audioSource = GetAudioSource();
            if (audioSource == null) return "0:00";
            return FormatTime(audioSource.time);
        }

        public string GetTotalTimeFormatted()
        {
            var audioSource = GetAudioSource();
            if (audioSource?.clip == null) return "0:00";
            return FormatTime(audioSource.clip.length);
        }

        private string FormatTime(float seconds)
        {
            int mins = (int)(seconds / 60);
            int secs = (int)(seconds % 60);
            return $"{mins}:{secs:D2}";
        }

        /// <summary>
        /// Get a status string for UI display
        /// </summary>
        public string GetStatusText()
        {
            switch (CurrentStatus)
            {
                case MusicStatus.FadingOut: return "Fading...";
                case MusicStatus.FadingIn: return "Fading in...";
                case MusicStatus.WaitingBetweenSongs: return "...";
                case MusicStatus.Paused: return "Paused";
                default: return IsInCombat ? "\u2694" : ""; // ⚔
            }
        }
    }
    
    public class SongTimeSettings : IExposable
    {
        public bool AllowDay;
        public bool AllowNight;
        public bool AllowWinter;
        public bool AllowCombat;
        public bool AllowAnytime;
        
        public SongTimeSettings()
        {
            AllowDay = true;
            AllowNight = true;
            AllowWinter = false;
            AllowCombat = false;
            AllowAnytime = false;
        }
        
        public SongTimeSettings(SongInfo song)
        {
            switch (song.Category.ToLower())
            {
                case "multi":
                    AllowAnytime = true;
                    AllowDay = true;
                    AllowNight = true;
                    AllowWinter = true;
                    AllowCombat = false;
                    break;
                case "combat":
                    AllowCombat = true;
                    AllowDay = false;
                    AllowNight = false;
                    AllowWinter = false;
                    AllowAnytime = false;
                    break;
                case "day":
                    AllowDay = true;
                    AllowNight = false;
                    AllowWinter = false;
                    AllowCombat = false;
                    AllowAnytime = false;
                    break;
                case "night":
                    AllowNight = true;
                    AllowDay = false;
                    AllowWinter = false;
                    AllowCombat = false;
                    AllowAnytime = false;
                    break;
                case "winter":
                    AllowWinter = true;
                    AllowDay = true;
                    AllowNight = true;
                    AllowCombat = false;
                    AllowAnytime = false;
                    break;
                default:
                    AllowAnytime = true;
                    AllowDay = true;
                    AllowNight = true;
                    AllowWinter = true;
                    AllowCombat = false;
                    break;
            }
        }
        
        public void ExposeData()
        {
            Scribe_Values.Look(ref AllowDay, "AllowDay", true);
            Scribe_Values.Look(ref AllowNight, "AllowNight", true);
            Scribe_Values.Look(ref AllowWinter, "AllowWinter", false);
            Scribe_Values.Look(ref AllowCombat, "AllowCombat", false);
            Scribe_Values.Look(ref AllowAnytime, "AllowAnytime", false);
        }
    }
}
