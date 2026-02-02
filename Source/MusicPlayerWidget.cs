using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RenHoek.MusicPlayer
{
    public static class MusicPlayerWidget
    {
        private static bool isExpanded = false;
        
        private static bool isDragging = false;
        private static bool isResizing = false;
        private static bool isLocked = false;
        private static bool playlistOnTop = true;  // TRUE = playlist expands UPWARD (default)
        private static Vector2 widgetPosition = new Vector2(-1f, -1f);  // Sentinel for uninitialized
        private static bool positionInitialized = false;
        private static Vector2 dragOffset;
        private static Vector2 songListScrollPos;
        private static string searchFilter = "";
        private static string expandedSettingsDefName = null;
        
        private static bool eventConsumedByControl = false;
        private static Rect lastSongListRect;  // Track song list rect for scroll bar
        
        // Resize system
        private static float widgetScale = 1.0f;
        private static float widthScale = 1.0f;      // Horizontal stretch
        private static float playlistHeightScale = 1.0f;  // Start at 100% height
        private const float MinScale = 1.0f;
        private const float MaxScale = 3.0f;
        private const float MinWidthScale = 0.6f;
        private const float MaxWidthScale = 3.0f;
        private const float MinPlaylistHeightScale = 0.08f;  // Absolute floor, overridden dynamically
        private const float MaxPlaylistHeightScale = 2.0f;
        
        // Dynamic max playlist height scale - limits playlist to fit on screen
        private static float EffectiveMaxPlaylistHeightScale
        {
            get
            {
                float screenHeight = UI.screenHeight;
                float bottomMenuHeight = 35f;
                float maxAllowedHeight = screenHeight - bottomMenuHeight - 20f;  // 20px margin
                float collapsedH = CollapsedHeight;
                float basePlaylist = (BaseExpandedHeight - BaseCollapsedHeight) * widgetScale;
                
                // Max scale = (maxAllowedHeight - collapsedHeight) / basePlaylistHeight
                float maxScale = (maxAllowedHeight - collapsedH) / basePlaylist;
                return Mathf.Min(MaxPlaylistHeightScale, maxScale);
            }
        }
        private const float ResizeHandleSize = 28f;
        
        // Dynamic min: search bar + spacing + one row - reduced for compact min
        private static float MinPlaylistHeight => 62f * widgetScale;
        private static float EffectiveMinPlaylistHeightScale
        {
            get
            {
                float basePlaylist = (BaseExpandedHeight - BaseCollapsedHeight) * widgetScale;
                float minScale = MinPlaylistHeight / basePlaylist;
                return Mathf.Max(MinPlaylistHeightScale, minScale);
            }
        }
        
        // Resize modes
        private static bool isResizingWidth = false;
        private static bool isResizingPlaylistHeight = false;
        private static float playlistResizeStartY = 0f;
        private static float playlistResizeStartScale = 1f;
        
        // TPS optimization
        private static List<SongInfo> cachedFilteredSongs = null;
        private static string lastSearchFilter = null;
        private static int lastSongCount = -1;
        private static int cacheRebuildFrame = -1;
        
        // Fonts
        private static Font arialFont = null;       // Base font for buttons/general UI
        private static Font titleFont = null;       // Title-specific font
        private static Font timecodeFont = null;    // Timecode-specific font
        private static GUIStyle arialLabelStyle = null;
        private static GUIStyle arialButtonStyle = null;
        private static GUIStyle titleLabelStyle = null;
        private static GUIStyle timecodeLabelStyle = null;
        private static bool fontInitialized = false;
        private static int lastTitleFontIndex = -1;
        private static int lastTimecodeFontIndex = -1;
        
        public static void MarkFontsDirty()
        {
            fontInitialized = false;
        }
        
        public static void ResetPosition()
        {
            positionInitialized = false;
        }

        // Base dimensions
        private const float BaseWidgetWidth = 301f;  // Added padding
        private const float BaseCollapsedHeight = 79f;  // Controls section height
        private const float BaseExpandedHeight = 460f;
        private const float BaseButtonSize = 26f;
        private const float BaseRowHeight = 24f;
        private const float BaseSettingsRowHeight = 26f;
        private const float BaseHeaderHeight = 22f;
        private const float BaseMargin = 6f;
        private const float BaseVolumeBarWidth = 14f;
        
        // Scaled properties
        private static float WidgetWidth => BaseWidgetWidth * widgetScale * widthScale;
        private static float CollapsedHeight => BaseCollapsedHeight * widgetScale;
        private static float PlaylistHeight => (BaseExpandedHeight - BaseCollapsedHeight) * widgetScale * Mathf.Max(playlistHeightScale, EffectiveMinPlaylistHeightScale);
        private static float ExpandedHeight => CollapsedHeight + PlaylistHeight;
        private static float ButtonSize => BaseButtonSize * widgetScale;
        private static float RowHeight => BaseRowHeight * widgetScale;
        private static float SettingsRowHeight => BaseSettingsRowHeight * widgetScale;
        private static float HeaderHeight => BaseHeaderHeight * widgetScale;
        private static float Margin => BaseMargin * widgetScale;
        private static float VolumeBarWidth => BaseVolumeBarWidth * widgetScale;

        // Colors
        private static readonly Color BackgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.95f);
        private static readonly Color AccentColor = new Color(0.4f, 0.6f, 0.9f, 1f);
        private static readonly Color PlayingColor = new Color(0.3f, 0.8f, 0.4f, 1f);
        private static readonly Color CombatColor = new Color(0.7f, 0.25f, 0.25f, 1f);
        private static readonly Color PausedColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        private static readonly Color HoverColor = new Color(0.2f, 0.2f, 0.25f, 1f);
        private static readonly Color TextColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        private static readonly Color SubTextColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color LockedColor = new Color(0.9f, 0.5f, 0.2f, 1f);
        private static readonly Color HeaderColor = new Color(0.15f, 0.15f, 0.18f, 1f);
        private static readonly Color SettingsBgColor = new Color(0.12f, 0.12f, 0.15f, 1f);
        private static readonly Color SettingsButtonActiveColor = new Color(1f, 0.8f, 0.3f, 1f);
        private static readonly Color ProgressBarBgColor = new Color(0.15f, 0.15f, 0.18f, 1f);

        // Cached styles to avoid per-frame allocations
        private static GUIStyle cachedNoScrollbar;
        private static GUIStyle cachedNoScrollbarThumb;
        
        // Cached grouped song list to avoid LINQ every frame
        private static List<(string Category, List<SongInfo> Songs)> cachedGroupedSongs;

        private static bool needsScrollToCurrentSong = false;
        private static string lastCurrentSongDefName = null;
        
        // Title scrolling
        private static float titleScrollOffset = 0f;
        private static string lastScrollingSongName = null;
        private const float ScrollSpeed = 10f;  // pixels per second

        // Peaceful messages for silence between songs
        private static string currentPeacefulMessage = null;
        private static readonly string[] PeacefulMessages = {
            // Original 50
            "A moment of stillness...",
            "The wind whispers...",
            "Peace settles over the colony...",
            "A quiet interlude...",
            "Breath of the Rim...",
            "Silence speaks...",
            "The world holds its breath...",
            "A pause in time...",
            "Tranquility reigns...",
            "The stars watch quietly...",
            "Nature's intermission...",
            "A gentle respite...",
            "Calm before the melody...",
            "The colony rests...",
            "Echoes fade away...",
            "Serenity now...",
            "A heartbeat of silence...",
            "The Rim exhales...",
            "Peaceful contemplation...",
            "Between the notes...",
            "A soundless dream...",
            "The air grows still...",
            "Quietude descends...",
            "A musical pause...",
            "The void hums softly...",
            "Ambient stillness...",
            "Rest for weary ears...",
            "The soundtrack sleeps...",
            "A silent overture...",
            "Harmony in waiting...",
            "The colony breathes...",
            "Dust motes dance in silence...",
            "A lull in the storm...",
            "Patience rewarded...",
            "The speakers rest...",
            "Anticipation builds...",
            "A moment to reflect...",
            "The music gathers strength...",
            "Silence is golden...",
            "A restful interval...",
            "The melody sleeps...",
            "Quiet contemplation...",
            "The Rim whispers secrets...",
            "A peaceful caesura...",
            "Stillness before sound...",
            "The orchestra rests...",
            "A breath between songs...",
            "Waiting in the wings...",
            "The next note approaches...",
            "A tender silence...",
            // New 50
            "The muffalo graze peacefully...",
            "Colonists pause their work...",
            "A moment of gratitude...",
            "The sun warms the fields...",
            "All hauling jobs complete...",
            "The kitchen smells wonderful...",
            "Beds are made, floors are clean...",
            "The research bench hums softly...",
            "A well-earned break...",
            "The animals are content...",
            "Stockpiles are organized...",
            "The doctor takes a rest...",
            "No one is on fire...",
            "The turrets stand vigilant...",
            "A rare peaceful moment...",
            "The crops grow steadily...",
            "Power levels stable...",
            "Food stores are plentiful...",
            "The colonists dream...",
            "Defenses hold strong...",
            "A time for reflection...",
            "The workshop awaits...",
            "Medicine is stocked...",
            "The freezer hums along...",
            "Joy levels rising...",
            "Recreation time...",
            "The colony prospers...",
            "A blessing from Randy...",
            "The traders have left happy...",
            "All wounds are tended...",
            "The prison is quiet...",
            "Art adorns the walls...",
            "The temple stands ready...",
            "Spirits are high...",
            "The beer is cold...",
            "Smokeleaf grows tall...",
            "The mood is good...",
            "Relationships blossom...",
            "The base expands...",
            "Silver flows freely...",
            "Components are crafted...",
            "Steel is plentiful...",
            "The mountain protects us...",
            "The marsh provides...",
            "A story unfolds...",
            "Another day survived...",
            "The colonists thrive...",
            "Hope on the Rim...",
            "Tomorrow brings promise...",
            "Life finds a way..."
        };
        
        // Fading messages for transitions
        private static string currentFadingMessage = null;
        private static readonly string[] FadingMessages = {
            // Original 50
            "Transitioning vibes...",
            "Hold please...",
            "Swapping tunes...",
            "Musical chairs...",
            "Plot twist incoming...",
            "The DJ is thinking...",
            "Buffering emotions...",
            "Genre shift detected...",
            "Mood.exe is loading...",
            "Vibe check in progress...",
            "Switching gears...",
            "Audio wizardry...",
            "The bards are conferring...",
            "Reticulating splines...",
            "New track who dis?",
            "Sonic transition...",
            "Loading next banger...",
            "Patience, young colonist...",
            "The soundtrack evolves...",
            "Crossfading reality...",
            "Randy is choosing music...",
            "Cassowary approved transition...",
            "Music.dll not found... jk",
            "Installing groove update...",
            "The thrumbo demands variety...",
            "Boomrat-tested fade...",
            "War crimes? No, just a fade...",
            "Recalibrating mood...",
            "The colonists approve...",
            "Smooth operator...",
            "Professional DJ noises...",
            "This fade costs 500 silver...",
            "Sponsored by nutrient paste...",
            "Hauling in new audio...",
            "Recruiting new melody...",
            "Researching: Better Music...",
            "Zoning: Vibes Only...",
            "Mental break: Listening...",
            "Mood buff incoming...",
            "Ate without table: +3...",
            "The psychic drone shifts...",
            "Solar flare? Nope, just fade...",
            "Ancient danger: New song...",
            "Drop pod: Fresh beats...",
            "Raid! ...of good music...",
            "Caravan arriving: Tunes...",
            "Crafting: Ambiance...",
            "Growing: Sick beats...",
            "Butchering: Old track...",
            "The muffalo approves...",
            // New 50
            "Chemfuel-powered transition...",
            "Acquiring new audio assets...",
            "Component: Music (1)...",
            "Drafted for groove duty...",
            "Trade offer: New song...",
            "Smelting the old track...",
            "Forbidding silence...",
            "Unforbidding bangers...",
            "Priority: Critical (Music)...",
            "Hauler en route with vibes...",
            "Deconstructing old mood...",
            "Building: Dance Floor...",
            "Manhunter pack of beats...",
            "Infestation of rhythm...",
            "Psychic ship: Party Time...",
            "The storyteller approves...",
            "Cassandra's choice...",
            "Phoebe's gentle fade...",
            "Randy says hold my beer...",
            "Tribal drums incoming...",
            "Spacer tech audio...",
            "Medieval vibes loading...",
            "Industrial strength fade...",
            "Ultratech soundwave...",
            "Archotech DJ engaged...",
            "Luciferium-enhanced bass...",
            "Go-juice for your ears...",
            "Wake-up call melody...",
            "Yayo-powered transition...",
            "Flake of musical genius...",
            "Penoxycyline for bad vibes...",
            "Ambrosia-tier audio...",
            "Glitterworld quality fade...",
            "Rimworld certified...",
            "Inspired: Creativity...",
            "Social fight: DJ vs Silence...",
            "Taming: Wild Beats...",
            "Training: Release (Music)...",
            "Bonded with this track...",
            "Sold to: Your Ears...",
            "Gift from faction: Melody...",
            "Quest complete: New Song...",
            "Caravan formed: Band...",
            "Pod launched: Bass Drop...",
            "Shuttle arriving: Tunes...",
            "Mech cluster: Beat Drop...",
            "Breach raid: Earworms...",
            "Tunneler: Subwoofer...",
            "Centipede of sound...",
            "Scyther-sharp transition..."
        };
        private static System.Random peacefulRandom = new System.Random();
        
        // Cooldown for skip/prev buttons to prevent spamming during fade
        private static float skipButtonCooldownEnd = 0f;

        private static void InitializeFonts()
        {
            int titleIdx = MusicPlayerMod.Settings?.TitleFontIndex ?? 0;
            int timecodeIdx = MusicPlayerMod.Settings?.TimecodeFontIndex ?? 0;
            
            // Reinit if font selection changed
            if (fontInitialized && titleIdx == lastTitleFontIndex && timecodeIdx == lastTimecodeFontIndex)
                return;
            
            lastTitleFontIndex = titleIdx;
            lastTimecodeFontIndex = timecodeIdx;
            
            // Base font for buttons and general UI - always use symbol font
            string[] baseFontNames = { "Segoe UI Symbol", "Arial Unicode MS", "Arial", "DejaVu Sans" };
            foreach (var fontName in baseFontNames)
            {
                arialFont = Font.CreateDynamicFontFromOSFont(fontName, 14);
                if (arialFont != null) break;
            }
            
            // Title font from settings - use GetActualFontName for proper system font name
            string titleFontName = MusicPlayerSettings.GetActualFontName(titleIdx);
            titleFont = Font.CreateDynamicFontFromOSFont(titleFontName, 14);
            if (titleFont == null) titleFont = arialFont;
            
            // Timecode font from settings - use GetActualFontName for proper system font name
            string timecodeFontName = MusicPlayerSettings.GetActualFontName(timecodeIdx);
            timecodeFont = Font.CreateDynamicFontFromOSFont(timecodeFontName, 14);
            if (timecodeFont == null) timecodeFont = arialFont;
            
            if (arialFont != null)
            {
                arialLabelStyle = new GUIStyle();
                arialLabelStyle.font = arialFont;
                arialLabelStyle.normal.textColor = Color.white;
                arialLabelStyle.alignment = TextAnchor.MiddleCenter;
                arialLabelStyle.fontSize = 14;
                
                arialButtonStyle = new GUIStyle(GUI.skin.button);
                arialButtonStyle.font = arialFont;
                arialButtonStyle.fontSize = 14;
                arialButtonStyle.alignment = TextAnchor.MiddleCenter;
                arialButtonStyle.padding = new RectOffset(0, 0, 0, 0);
                arialButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            }
            
            if (titleFont != null)
            {
                titleLabelStyle = new GUIStyle();
                titleLabelStyle.font = titleFont;
                titleLabelStyle.normal.textColor = Color.white;
                titleLabelStyle.alignment = TextAnchor.MiddleLeft;
                titleLabelStyle.fontSize = 14;
                titleLabelStyle.clipping = TextClipping.Clip;
            }
            
            if (timecodeFont != null)
            {
                timecodeLabelStyle = new GUIStyle();
                timecodeLabelStyle.font = timecodeFont;
                timecodeLabelStyle.normal.textColor = Color.white;
                timecodeLabelStyle.alignment = TextAnchor.MiddleCenter;
                timecodeLabelStyle.fontSize = 14;
            }
            
            fontInitialized = true;
        }
        
        private static void DrawScaledText(Rect rect, string text, Color color, int baseFontSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            int scaledSize = Mathf.RoundToInt(baseFontSize * widgetScale);
            scaledSize = Mathf.Max(8, scaledSize);  // Minimum readable size
            
            if (arialLabelStyle != null)
            {
                var oldSize = arialLabelStyle.fontSize;
                var oldAlign = arialLabelStyle.alignment;
                arialLabelStyle.fontSize = scaledSize;
                arialLabelStyle.normal.textColor = color;
                arialLabelStyle.alignment = alignment;
                GUI.Label(rect, text, arialLabelStyle);
                arialLabelStyle.fontSize = oldSize;
                arialLabelStyle.alignment = oldAlign;
            }
            else
            {
                GUI.color = color;
                Text.Font = GameFont.Tiny;
                Text.Anchor = alignment;
                Widgets.Label(rect, text);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }
        
        private static void DrawTitleText(Rect rect, string text, Color color, int baseFontSize, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            int scaledSize = Mathf.RoundToInt(baseFontSize * widgetScale);
            scaledSize = Mathf.Max(8, scaledSize);
            
            if (titleLabelStyle != null)
            {
                var oldSize = titleLabelStyle.fontSize;
                var oldAlign = titleLabelStyle.alignment;
                titleLabelStyle.fontSize = scaledSize;
                titleLabelStyle.normal.textColor = color;
                titleLabelStyle.alignment = alignment;
                GUI.Label(rect, text, titleLabelStyle);
                titleLabelStyle.fontSize = oldSize;
                titleLabelStyle.alignment = oldAlign;
            }
            else
            {
                DrawScaledText(rect, text, color, baseFontSize, alignment);
            }
        }
        
        private static void DrawTimecodeText(Rect rect, string text, Color color, int baseFontSize, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            int scaledSize = Mathf.RoundToInt(baseFontSize * widgetScale);
            scaledSize = Mathf.Max(8, scaledSize);
            
            if (timecodeLabelStyle != null)
            {
                var oldSize = timecodeLabelStyle.fontSize;
                var oldAlign = timecodeLabelStyle.alignment;
                timecodeLabelStyle.fontSize = scaledSize;
                timecodeLabelStyle.normal.textColor = color;
                timecodeLabelStyle.alignment = alignment;
                GUI.Label(rect, text, timecodeLabelStyle);
                timecodeLabelStyle.fontSize = oldSize;
                timecodeLabelStyle.alignment = oldAlign;
            }
            else
            {
                DrawScaledText(rect, text, color, baseFontSize, alignment);
            }
        }
        
        private static float CalcTitleTextWidth(string text, int baseFontSize)
        {
            int scaledSize = Mathf.RoundToInt(baseFontSize * widgetScale);
            scaledSize = Mathf.Max(8, scaledSize);
            
            if (titleLabelStyle != null)
            {
                var oldSize = titleLabelStyle.fontSize;
                titleLabelStyle.fontSize = scaledSize;
                float width = titleLabelStyle.CalcSize(new GUIContent(text)).x;
                titleLabelStyle.fontSize = oldSize;
                return width;
            }
            else if (arialLabelStyle != null)
            {
                var oldSize = arialLabelStyle.fontSize;
                arialLabelStyle.fontSize = scaledSize;
                float width = arialLabelStyle.CalcSize(new GUIContent(text)).x;
                arialLabelStyle.fontSize = oldSize;
                return width;
            }
            return text.Length * scaledSize * 0.6f;  // rough fallback
        }
        
        private static bool DrawArialButton(Rect rect, string text, string tooltip = null, bool disabled = false)
        {
            if (disabled)
            {
                // Draw greyed out button
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                if (arialButtonStyle != null)
                {
                    var oldSize = arialButtonStyle.fontSize;
                    arialButtonStyle.fontSize = Mathf.RoundToInt(14 * widgetScale);
                    GUI.Label(rect, text, arialButtonStyle);
                    arialButtonStyle.fontSize = oldSize;
                }
                else
                {
                    Widgets.Label(rect, text);
                }
                GUI.color = Color.white;
                if (!string.IsNullOrEmpty(tooltip))
                    TooltipHandler.TipRegion(rect, tooltip);
                return false;
            }
            
            bool clicked;
            if (arialButtonStyle != null)
            {
                var oldSize = arialButtonStyle.fontSize;
                arialButtonStyle.fontSize = Mathf.RoundToInt(14 * widgetScale);
                clicked = GUI.Button(rect, text, arialButtonStyle);
                arialButtonStyle.fontSize = oldSize;
            }
            else
            {
                clicked = Widgets.ButtonText(rect, text, true, true, true);
            }
            
            if (clicked)
                eventConsumedByControl = true;
            
            if (!string.IsNullOrEmpty(tooltip))
                TooltipHandler.TipRegion(rect, tooltip);
                
            return clicked;
        }

        private static bool controlsHidden = false;
        
        private static string GetRandomPeacefulMessage()
        {
            // Keep the same message during a single silence period
            if (currentPeacefulMessage == null)
            {
                currentPeacefulMessage = PeacefulMessages[peacefulRandom.Next(PeacefulMessages.Length)];
            }
            return currentPeacefulMessage;
        }
        
        private static void ClearPeacefulMessage()
        {
            currentPeacefulMessage = null;
        }
        
        private static string GetRandomFadingMessage()
        {
            // Keep the same message during a single fade
            if (currentFadingMessage == null)
            {
                currentFadingMessage = FadingMessages[peacefulRandom.Next(FadingMessages.Length)];
            }
            return currentFadingMessage;
        }
        
        private static void ClearFadingMessage()
        {
            currentFadingMessage = null;
        }
        
        private static bool IsSkipOnCooldown()
        {
            return Time.realtimeSinceStartup < skipButtonCooldownEnd;
        }
        
        private static void StartSkipCooldown()
        {
            // Cooldown is 2x the forced fade duration
            float manualFade = MusicPlayerMod.Settings?.GetManualFadeDuration() ?? 0.4f;
            skipButtonCooldownEnd = Time.realtimeSinceStartup + (manualFade * 2f);
        }

        private static Color GetStateColor(MusicPlayerController controller)
        {
            if (controller.IsInCombat)
                return CombatColor;
            else if (controller.IsPaused || controller.PausedDueToGamePause)
                return PausedColor;
            else if (controller.IsPlaying)
                return PlayingColor;
            else
                return AccentColor;
        }

        public static void DrawWidget()
        {
            if (MusicPlayerController.Instance == null) return;
            if (Find.CurrentMap == null) return;
            
            MusicPlayerKeyBindings.CheckKeyBindings();
            
            if (MusicPlayerMod.Settings?.HidePlayer ?? false) return;

            InitializeFonts();
            eventConsumedByControl = false;
            
            // Initialize position based on setting
            if (!positionInitialized)
            {
                float screenW = UI.screenWidth;
                float screenH = UI.screenHeight;
                float menuHeight = 35f;
                int posIndex = MusicPlayerMod.Settings?.DefaultPositionIndex ?? 1;
                
                switch (posIndex)
                {
                    case 0: // Bottom Left
                        widgetPosition = new Vector2(0f, screenH - menuHeight - CollapsedHeight);
                        break;
                    case 1: // Bottom Right (default)
                        widgetPosition = new Vector2(screenW - WidgetWidth, screenH - menuHeight - CollapsedHeight);
                        break;
                    case 2: // Top Left
                        widgetPosition = new Vector2(0f, 0f);
                        break;
                    case 3: // Top Right
                        widgetPosition = new Vector2(screenW - WidgetWidth, 0f);
                        break;
                    default: // Fallback to bottom right
                        widgetPosition = new Vector2(screenW - WidgetWidth, screenH - menuHeight - CollapsedHeight);
                        break;
                }
                positionInitialized = true;
            }

            var controller = MusicPlayerController.Instance;
            
            // Calculate height based on state
            float baseHeight;
            if (controlsHidden)
            {
                baseHeight = 34f * widgetScale;  // Title bar with buttons
            }
            else
            {
                baseHeight = isExpanded ? ExpandedHeight : CollapsedHeight;
            }
            float height = baseHeight;
            
            // Use UI.screenHeight which accounts for RimWorld's UI scaling
            float screenHeight = UI.screenHeight;
            float screenWidth = UI.screenWidth;
            
            // Bottom menu bar height - RimWorld's bottom bar is approximately 35 pixels
            float bottomMenuHeight = 35f;
            float usableScreenHeight = screenHeight - bottomMenuHeight;
            
            // Check if orientation is locked in settings
            bool orientationLocked = MusicPlayerMod.Settings?.LockPlaylistOrientation ?? false;
            
            float drawX, drawY;
            
            // widgetPosition is always the TOP-LEFT of the CONTROLS section
            if (playlistOnTop && isExpanded && !controlsHidden)
            {
                drawX = widgetPosition.x;
                drawY = widgetPosition.y - (height - CollapsedHeight);
            }
            else
            {
                drawX = widgetPosition.x;
                drawY = widgetPosition.y;
            }
            
            // Auto-swap orientation ONLY when hitting screen edges (unless locked)
            if (!orientationLocked && isExpanded && !controlsHidden)
            {
                if (playlistOnTop)
                {
                    // Playlist above - flip to bottom only if we hit the TOP edge
                    if (drawY < 0)
                    {
                        playlistOnTop = false;
                        drawY = widgetPosition.y;
                    }
                }
                else
                {
                    // Playlist below - flip to top only if we hit the BOTTOM edge (respecting menu)
                    if (drawY + height > usableScreenHeight)
                    {
                        playlistOnTop = true;
                        drawY = widgetPosition.y - (height - CollapsedHeight);
                    }
                }
            }
            
            // Clamp to screen using UI dimensions, respecting bottom menu
            drawX = Mathf.Clamp(drawX, 0, screenWidth - WidgetWidth);
            drawY = Mathf.Clamp(drawY, 0, usableScreenHeight - height);

            Rect widgetRect = new Rect(drawX, drawY, WidgetWidth, height);
            
            // Border color based on state
            Color borderColor = isLocked ? LockedColor : GetStateColor(controller);
            
            Widgets.DrawBoxSolidWithOutline(widgetRect, BackgroundColor, borderColor);

            // Suppress only horizontal scrollbar — keep vertical for playlist
            GUIStyle savedHScrollbar = GUI.skin.horizontalScrollbar;
            GUIStyle savedHThumb = GUI.skin.horizontalScrollbarThumb;
            
            // Use cached styles to avoid per-frame allocations
            if (cachedNoScrollbar == null)
            {
                cachedNoScrollbar = new GUIStyle();
                cachedNoScrollbar.fixedWidth = 0f;
                cachedNoScrollbar.fixedHeight = 0f;
            }
            if (cachedNoScrollbarThumb == null)
            {
                cachedNoScrollbarThumb = new GUIStyle();
                cachedNoScrollbarThumb.fixedWidth = 0f;
                cachedNoScrollbarThumb.fixedHeight = 0f;
            }
            GUI.skin.horizontalScrollbar = cachedNoScrollbar;
            GUI.skin.horizontalScrollbarThumb = cachedNoScrollbarThumb;

            // Draw content using absolute screen coordinates
            DrawWidgetContent(widgetRect, controller);
            
            // Restore horizontal scrollbar styles
            GUI.skin.horizontalScrollbar = savedHScrollbar;
            GUI.skin.horizontalScrollbarThumb = savedHThumb;
            
            // Handle resize and drag
            if (!isLocked) HandlePlaylistHeightResize(widgetRect);
            if (!isLocked) HandleResizing(widgetRect);
            if (!isLocked) HandleDragging(widgetRect);
            
            // Consume mouse events over widget to prevent click-through
            // But exclude the scroll bar area so it remains draggable
            if (Mouse.IsOver(widgetRect))
            {
                Event evt = Event.current;
                if (evt.type == EventType.MouseDown || evt.type == EventType.MouseUp)
                {
                    // Check if mouse is over scroll bar (right 16px of song list)
                    bool overScrollBar = false;
                    if (isExpanded && lastSongListRect.width > 0)
                    {
                        Rect scrollBarRect = new Rect(lastSongListRect.xMax - 16f, lastSongListRect.y, 16f, lastSongListRect.height);
                        overScrollBar = scrollBarRect.Contains(evt.mousePosition);
                    }
                    
                    if (!overScrollBar)
                    {
                        evt.Use();
                    }
                }
            }
        }

        private static void HandleDragging(Rect widgetRect)
        {
            Event evt = Event.current;
            
            // Match corner size from HandleResizing
            float cornerSize = 24f;
            Rect resizeHandle = new Rect(
                widgetRect.xMax - cornerSize,
                widgetRect.yMax - cornerSize,
                cornerSize,
                cornerSize
            );
            
            Rect leftResizeHandle = new Rect(
                widgetRect.x,
                widgetRect.yMax - cornerSize,
                cornerSize,
                cornerSize
            );
            
            if (evt.type == EventType.MouseDown && evt.button == 0 && widgetRect.Contains(evt.mousePosition))
            {
                if (resizeHandle.Contains(evt.mousePosition) || leftResizeHandle.Contains(evt.mousePosition))
                    return;
                
                // Don't start drag if clicking on scroll bar
                if (isExpanded && lastSongListRect.width > 0)
                {
                    Rect scrollBarRect = new Rect(lastSongListRect.xMax - 16f, lastSongListRect.y, 16f, lastSongListRect.height);
                    if (scrollBarRect.Contains(evt.mousePosition))
                        return;
                }
                    
                if (!eventConsumedByControl)
                {
                    isDragging = true;
                    
                    if (playlistOnTop && isExpanded)
                    {
                        float controlsTop = widgetRect.yMax - CollapsedHeight;
                        dragOffset = evt.mousePosition - new Vector2(widgetRect.x, controlsTop);
                    }
                    else
                    {
                        dragOffset = evt.mousePosition - new Vector2(widgetRect.x, widgetRect.y);
                    }
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                isDragging = false;
            }
            else if (evt.type == EventType.MouseDrag && isDragging)
            {
                Vector2 newAnchor = evt.mousePosition - dragOffset;
                widgetPosition.x = newAnchor.x;
                widgetPosition.y = newAnchor.y;
                evt.Use();
            }
        }
        
        private static bool isResizingLeft = false;
        
        private static void HandleResizing(Rect widgetRect)
        {
            Event evt = Event.current;
            
            // Corner resize handles - only at actual corners (bottom)
            float cornerSize = 24f;
            Rect rightCornerHandle = new Rect(
                widgetRect.xMax - cornerSize,
                widgetRect.yMax - cornerSize,
                cornerSize,
                cornerSize
            );
            
            Rect leftCornerHandle = new Rect(
                widgetRect.x,
                widgetRect.yMax - cornerSize,
                cornerSize,
                cornerSize
            );
            
            // Edge resize handles for horizontal stretch - full height of sides
            float edgeHandleThickness = 18f;
            
            Rect rightEdgeHandle = new Rect(
                widgetRect.xMax - edgeHandleThickness,
                widgetRect.y,
                edgeHandleThickness,
                widgetRect.height - cornerSize  // Stop before corner
            );
            
            Rect leftEdgeHandle = new Rect(
                widgetRect.x,
                widgetRect.y,
                edgeHandleThickness,
                widgetRect.height - cornerSize  // Stop before corner
            );
            
            // Handle mouse events - check EDGES FIRST (before corners)
            if (evt.type == EventType.MouseDown && evt.button == 0)
            {
                // Check edge handles first (more common operation)
                if (rightEdgeHandle.Contains(evt.mousePosition))
                {
                    isResizingWidth = true;
                    isResizingLeft = false;
                    isResizing = false;
                    eventConsumedByControl = true;
                    evt.Use();
                }
                else if (leftEdgeHandle.Contains(evt.mousePosition))
                {
                    isResizingWidth = true;
                    isResizingLeft = true;
                    isResizing = false;
                    eventConsumedByControl = true;
                    evt.Use();
                }
                // Then check corners
                else if (rightCornerHandle.Contains(evt.mousePosition))
                {
                    isResizing = true;
                    isResizingLeft = false;
                    isResizingWidth = false;
                    eventConsumedByControl = true;
                    evt.Use();
                }
                else if (leftCornerHandle.Contains(evt.mousePosition))
                {
                    isResizing = true;
                    isResizingLeft = true;
                    isResizingWidth = false;
                    eventConsumedByControl = true;
                    evt.Use();
                }
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0)
            {
                isResizing = false;
                isResizingLeft = false;
                isResizingWidth = false;
            }
            else if (evt.type == EventType.MouseDrag)
            {
                if (isResizing)
                {
                    if (isResizingLeft)
                    {
                        float rightEdge = widgetRect.xMax;
                        float newWidth = rightEdge - evt.mousePosition.x;
                        float newScale = newWidth / (BaseWidgetWidth * widthScale);
                        float oldScale = widgetScale;
                        widgetScale = Mathf.Clamp(newScale, MinScale, MaxScale);
                        
                        float widthDelta = (widgetScale - oldScale) * BaseWidgetWidth * widthScale;
                        widgetPosition.x -= widthDelta;
                    }
                    else
                    {
                        float newWidth = evt.mousePosition.x - widgetRect.x;
                        float newScale = newWidth / (BaseWidgetWidth * widthScale);
                        widgetScale = Mathf.Clamp(newScale, MinScale, MaxScale);
                    }
                    eventConsumedByControl = true;
                    evt.Use();
                }
                else if (isResizingWidth)
                {
                    if (isResizingLeft)
                    {
                        float rightEdge = widgetRect.xMax;
                        float newWidth = rightEdge - evt.mousePosition.x;
                        float newWidthScale = newWidth / (BaseWidgetWidth * widgetScale);
                        float oldWidthScale = widthScale;
                        widthScale = Mathf.Clamp(newWidthScale, MinWidthScale, MaxWidthScale);
                        
                        float widthDelta = (widthScale - oldWidthScale) * BaseWidgetWidth * widgetScale;
                        widgetPosition.x -= widthDelta;
                    }
                    else
                    {
                        float newWidth = evt.mousePosition.x - widgetRect.x;
                        float newWidthScale = newWidth / (BaseWidgetWidth * widgetScale);
                        widthScale = Mathf.Clamp(newWidthScale, MinWidthScale, MaxWidthScale);
                    }
                    eventConsumedByControl = true;
                    evt.Use();
                }
            }
        }
        
        private static void HandlePlaylistHeightResize(Rect widgetRect)
        {
            if (!isExpanded || controlsHidden) return;
            
            Event evt = Event.current;
            // Thicker invisible grab zone at the playlist edge - INSIDE the border
            float edgeHandleThickness = 8f;
            float borderOffset = 2f;  // Stay inside the green border
            float cornerSize = 24f;  // Match HandleResizing
            
            Rect playlistEdgeHandle;
            if (playlistOnTop)
            {
                playlistEdgeHandle = new Rect(
                    widgetRect.x + cornerSize,
                    widgetRect.y + borderOffset,
                    widgetRect.width - cornerSize * 2,
                    edgeHandleThickness
                );
            }
            else
            {
                playlistEdgeHandle = new Rect(
                    widgetRect.x + cornerSize,
                    widgetRect.yMax - edgeHandleThickness - borderOffset,
                    widgetRect.width - cornerSize * 2,
                    edgeHandleThickness
                );
            }
            
            // Draw visible grab bar (same color as background)
            Widgets.DrawBoxSolid(playlistEdgeHandle, BackgroundColor);
            
            // Block ALL mouse events in the grab handle area - this prevents song clicks/tooltips
            if (playlistEdgeHandle.Contains(evt.mousePosition))
            {
                // Consume any mouse event to prevent it reaching songs behind
                if (evt.type == EventType.MouseDown || evt.type == EventType.MouseUp || evt.type == EventType.MouseDrag)
                {
                    eventConsumedByControl = true;
                    if (evt.button != 0)
                        evt.Use();
                }
            }
            
            if (evt.type == EventType.MouseDown && evt.button == 0 && playlistEdgeHandle.Contains(evt.mousePosition))
            {
                isResizingPlaylistHeight = true;
                // Store the offset between mouse and the edge we're dragging
                if (playlistOnTop)
                {
                    playlistResizeStartY = evt.mousePosition.y;
                    playlistResizeStartScale = playlistHeightScale;
                }
                else
                {
                    playlistResizeStartY = evt.mousePosition.y;
                    playlistResizeStartScale = playlistHeightScale;
                }
                eventConsumedByControl = true;
                evt.Use();
            }
            else if (evt.type == EventType.MouseUp && evt.button == 0 && isResizingPlaylistHeight)
            {
                isResizingPlaylistHeight = false;
            }
            else if (evt.type == EventType.MouseDrag && isResizingPlaylistHeight)
            {
                float basePlaylistHeight = (BaseExpandedHeight - BaseCollapsedHeight) * widgetScale;
                float mouseDelta = playlistResizeStartY - evt.mousePosition.y;
                
                if (playlistOnTop)
                {
                    // Dragging up (negative Y) increases height
                    float deltaScale = mouseDelta / basePlaylistHeight;
                    float newHeightScale = playlistResizeStartScale + deltaScale;
                    playlistHeightScale = Mathf.Clamp(newHeightScale, EffectiveMinPlaylistHeightScale, EffectiveMaxPlaylistHeightScale);
                }
                else
                {
                    // Dragging down (positive Y) increases height
                    float deltaScale = -mouseDelta / basePlaylistHeight;
                    float newHeightScale = playlistResizeStartScale + deltaScale;
                    playlistHeightScale = Mathf.Clamp(newHeightScale, EffectiveMinPlaylistHeightScale, EffectiveMaxPlaylistHeightScale);
                }
                eventConsumedByControl = true;
                evt.Use();
            }
        }

        private static void DrawWidgetContent(Rect rect, MusicPlayerController controller)
        {
            float baseX = rect.x;
            float baseY = rect.y;
            float y = baseY + Margin;
            
            if (controlsHidden)
            {
                // Just draw title row when controls are hidden - use full available height
                float titleHeight = rect.height - Margin * 2;
                DrawTitleRow(new Rect(baseX + Margin, baseY + Margin, rect.width - Margin * 2, titleHeight), controller);
                return;
            }
            
            if (isExpanded)
            {
                float gripPadding = 8f;  // Matches grab handle size to prevent overlap
                
                if (playlistOnTop)
                {
                    // Controls at BOTTOM - fixed position from bottom edge
                    float controlsY = baseY + rect.height - CollapsedHeight + Margin;
                    DrawControlsSection(new Rect(baseX, controlsY, rect.width, CollapsedHeight - Margin), controller);
                    
                    // Search bar just above controls
                    float searchBarHeight = 22f * widgetScale;
                    float searchY = controlsY - searchBarHeight - 4f * widgetScale;
                    DrawSearchBar(new Rect(baseX + Margin, searchY, rect.width - Margin * 2, searchBarHeight));
                    
                    // Playlist fills remaining space at top, pushed down by grip padding
                    float playlistY = y + gripPadding;
                    float playlistHeight = Mathf.Max(searchY - playlistY - 4f * widgetScale, RowHeight + 4f);
                    DrawSongList(new Rect(baseX + Margin, playlistY, rect.width - Margin * 2, playlistHeight), controller);
                }
                else
                {
                    DrawControlsSection(new Rect(baseX, y, rect.width, CollapsedHeight - Margin), controller);
                    y += CollapsedHeight - Margin;
                    
                    float searchBarHeight = 22f * widgetScale;
                    DrawSearchBar(new Rect(baseX + Margin, y, rect.width - Margin * 2, searchBarHeight));
                    y += searchBarHeight + 4f * widgetScale;
                    
                    float playlistHeight = Mathf.Max(rect.yMax - y - Margin - gripPadding, RowHeight + 4f);
                    DrawSongList(new Rect(baseX + Margin, y, rect.width - Margin * 2, playlistHeight), controller);
                }
            }
            else
            {
                DrawControlsSection(new Rect(baseX, y, rect.width, CollapsedHeight - Margin), controller);
            }
        }
        
        private static void DrawSearchBar(Rect rect)
        {
            // Settings button on the right
            float settingsBtnSize = rect.height;
            float settingsBtnX = rect.xMax - settingsBtnSize;
            Rect settingsRect = new Rect(settingsBtnX, rect.y, settingsBtnSize, settingsBtnSize);
            
            // Search bar takes remaining space
            Rect searchRect = new Rect(rect.x, rect.y, rect.width - settingsBtnSize - 4f, rect.height);
            
            if (Mouse.IsOver(searchRect) && Event.current.type == EventType.MouseDown)
                eventConsumedByControl = true;
            
            searchFilter = Widgets.TextField(searchRect, searchFilter);
            if (string.IsNullOrEmpty(searchFilter))
            {
                GUI.color = SubTextColor;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(searchRect.x + 6f, searchRect.y, searchRect.width - 6f, searchRect.height), "Search songs...");
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            
            // Settings button - just the symbol
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Mouse.IsOver(settingsRect) ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(settingsRect, "\u058D");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            
            if (Mouse.IsOver(settingsRect))
                TooltipHandler.TipRegion(settingsRect, "RenHoek_MusicPlayer_OpenSettings".Translate());
            
            if (Widgets.ButtonInvisible(settingsRect))
            {
                Find.WindowStack.Add(new Dialog_ModSettings(MusicPlayerMod.Instance));
                eventConsumedByControl = true;
            }
        }
        
        private static void DrawControlsSection(Rect rect, MusicPlayerController controller)
        {
            float x = rect.x;
            float y = rect.y;
            float spacing = 2f * widgetScale;
            
            // Row 1: Song title spans FULL WIDTH - compact height
            float titleHeight = 22f * widgetScale;
            DrawTitleRow(new Rect(x + Margin, y, rect.width - Margin * 2, titleHeight), controller);
            y += titleHeight + spacing;
            
            // Volume bar on the left, content to the right
            float volumeBarX = x + Margin;
            float contentX = x + Margin + VolumeBarWidth + 4f * widgetScale;
            float contentWidth = rect.width - Margin * 2 - VolumeBarWidth - 4f * widgetScale;
            
            // Row 2: Control buttons
            float buttonRowHeight = ButtonSize;
            DrawControls(new Rect(contentX, y, contentWidth, buttonRowHeight), controller);
            
            // Row 3: Progress bar with timecode
            float progressY = y + buttonRowHeight + spacing;
            float progressHeight = 14f * widgetScale;
            DrawProgressBar(new Rect(contentX, progressY, contentWidth, progressHeight), controller);
            
            // Vertical volume bar on the left (spans buttons + progress bar)
            float volumeBarHeight = buttonRowHeight + spacing + progressHeight;
            DrawVerticalVolumeBar(new Rect(volumeBarX, y, VolumeBarWidth, volumeBarHeight), controller);
        }

        private static void DrawTitleRow(Rect rect, MusicPlayerController controller)
        {
            float btnSize = 18f * widgetScale;
            float btnSpacing = 2f * widgetScale;
            
            // Combat icon + buttons on right
            bool inCombat = controller.IsInCombat;
            float combatIconWidth = inCombat ? (14f * widgetScale + btnSpacing) : 0f;
            int numButtons = controlsHidden ? 4 : 4;  // Compact: hamburger, play, skip, shuffle. Normal: hide, expand, context, lock
            float buttonsWidth = btnSize * numButtons + btnSpacing * (numButtons - 1) + combatIconWidth;
            
            // Song title on left - offset up slightly to prevent bottom clipping
            float titleWidth = rect.width - buttonsWidth - 4f * widgetScale;
            float titleOffset = 2f * widgetScale;  // Scale-proportional offset
            Rect titleRect = new Rect(rect.x, rect.y - titleOffset, titleWidth, rect.height);
            
            string songText;
            Color titleColor;
            
            // Check if we're fading between songs
            if (controller.IsFading)
            {
                songText = GetRandomFadingMessage();
                titleColor = SubTextColor;
                // Clear peaceful message since we're not in silence
                ClearPeacefulMessage();
            }
            // Check if we're in silence between songs
            else if (controller.CurrentStatus == MusicStatus.WaitingBetweenSongs)
            {
                songText = GetRandomPeacefulMessage();
                titleColor = SubTextColor;
                // Clear fading message since we're not fading
                ClearFadingMessage();
            }
            else
            {
                // Clear both messages so we get new ones next time
                ClearPeacefulMessage();
                ClearFadingMessage();
                
                if (controller.CurrentSong != null)
                {
                    songText = controller.CurrentSong.DisplayName;
                    titleColor = GetStateColor(controller);
                }
                else
                {
                    songText = "RenHoek_MusicPlayer_NoSongPlaying".Translate();
                    titleColor = SubTextColor;
                }
            }
            
            
            // Title font size
            int titleFontSize = 11;
            
            float textWidth = CalcTitleTextWidth(songText, titleFontSize);
            bool needsScroll = textWidth > titleRect.width - 4f;
            bool scrollingEnabled = MusicPlayerMod.Settings?.ScrollingTitle ?? true;
            
            // Reset scroll if song changed
            if (songText != lastScrollingSongName)
            {
                lastScrollingSongName = songText;
                titleScrollOffset = 0f;
            }
            
            if (needsScroll && scrollingEnabled)
            {
                // Seamless loop - draw text twice with gap, scroll continuously
                float gapWidth = 40f;  // Gap between end and start
                float totalWidth = textWidth + gapWidth;
                titleScrollOffset += ScrollSpeed * Time.deltaTime;
                if (titleScrollOffset >= totalWidth)
                    titleScrollOffset -= totalWidth;
                
                // Draw with clipping
                GUI.BeginClip(titleRect);
                // First copy
                Rect scrolledRect = new Rect(-titleScrollOffset, 0, textWidth + 20f, titleRect.height);
                DrawTitleText(scrolledRect, songText, titleColor, titleFontSize);
                // Second copy for seamless loop
                Rect scrolledRect2 = new Rect(-titleScrollOffset + totalWidth, 0, textWidth + 20f, titleRect.height);
                DrawTitleText(scrolledRect2, songText, titleColor, titleFontSize);
                GUI.EndClip();
            }
            else if (needsScroll)
            {
                // Truncate if scrolling disabled
                string displayText = songText;
                while (CalcTitleTextWidth(displayText, titleFontSize) > titleRect.width - 4f && displayText.Length > 3)
                {
                    displayText = displayText.Substring(0, displayText.Length - 4) + "...";
                }
                DrawTitleText(titleRect, displayText, titleColor, titleFontSize);
            }
            else
            {
                // Fits normally
                DrawTitleText(titleRect, songText, titleColor, titleFontSize);
            }
            
            if (controller.CurrentSong != null)
                TooltipHandler.TipRegion(titleRect, "RenHoek_MusicPlayer_CurrentSongTooltip".Translate(controller.CurrentSong.Title, controller.CurrentSong.Artist, controller.CurrentSong.Category));
            
            // Right side: combat icon (if in combat) + buttons
            float btnX = rect.xMax - buttonsWidth;
            
            // Combat icon (⚔) - right aligned before buttons, vertically centered
            if (inCombat)
            {
                float combatIconSize = 14f * widgetScale;
                float combatOffset = 2f * widgetScale;
                float combatY = rect.y + (rect.height - combatIconSize) / 2f - combatOffset;
                Rect combatRect = new Rect(btnX, combatY, combatIconSize, combatIconSize);
                DrawScaledText(combatRect, "\u2694", CombatColor, 12);
                TooltipHandler.TipRegion(combatRect, "RenHoek_MusicPlayer_InCombat".Translate());
                btnX += combatIconSize + btnSpacing;
            }
            
            // Vertically center buttons in the row with same offset as title
            float btnOffset = 2f * widgetScale;
            float btnY = rect.y + (rect.height - btnSize) / 2f - btnOffset;
            
            // When controls hidden: show controls, context, lock
            // When controls visible: expand/collapse, show/hide controls, context, lock
            
            if (controlsHidden)
            {
                // Show controls button (hamburger)
                Rect hideBtn = new Rect(btnX, btnY, btnSize, btnSize);
                string hideSymbol = "\u2630";  // ☰ when hidden (show)
                if (DrawArialButton(hideBtn, hideSymbol, "RenHoek_MusicPlayer_ShowControls".Translate()))
                {
                    controlsHidden = false;
                }
                btnX += btnSize + btnSpacing;
                
                // Play/Pause button - greyed out during silence between songs
                Rect playBtn = new Rect(btnX, btnY, btnSize, btnSize);
                bool isSilence = controller.CurrentStatus == MusicStatus.WaitingBetweenSongs;
                bool isPausedByGame = controller.PausedDueToGamePause;
                bool showPause = controller.IsPlaying && !controller.IsPaused && !isPausedByGame;
                string playSymbol = showPause ? "\u23F8" : "\u25B6";
                string playTooltip = isSilence ? "RenHoek_MusicPlayer_WaitingForSong".Translate() : 
                    (isPausedByGame ? "RenHoek_MusicPlayer_PausedByGame".Translate() :
                    (showPause ? "RenHoek_MusicPlayer_Pause".Translate() : "RenHoek_MusicPlayer_Play".Translate()));
                if (DrawArialButton(playBtn, playSymbol, playTooltip, isSilence || isPausedByGame))
                {
                    if (!isPausedByGame && (controller.IsPaused || controller.IsPlaying))
                        controller.TogglePause();
                    else if (!isPausedByGame && controller.CurrentSong != null)
                        controller.Play(controller.CurrentSong);
                    else if (!isPausedByGame)
                        controller.PlayNext();
                }
                btnX += btnSize + btnSpacing;
                
                // Skip button - disabled during cooldown
                Rect skipBtn = new Rect(btnX, btnY, btnSize, btnSize);
                bool skipOnCooldown = IsSkipOnCooldown();
                if (DrawArialButton(skipBtn, "\u23ED", "RenHoek_MusicPlayer_Next".Translate(), skipOnCooldown))
                {
                    controller.PlayNext();
                    StartSkipCooldown();
                }
                btnX += btnSize + btnSpacing;
                
                // Shuffle button (position 4)
                bool shuffleOn = MusicPlayerMod.Settings?.ShuffleEnabled ?? true;
                Rect shuffleBtn = new Rect(btnX, btnY, btnSize, btnSize);
                string shuffleSymbol = "\u21C4";  // ⇄
                var oldShuffleBg = GUI.backgroundColor;
                if (shuffleOn)
                    GUI.backgroundColor = PlayingColor;
                if (DrawArialButton(shuffleBtn, shuffleSymbol, shuffleOn ? "RenHoek_MusicPlayer_ShuffleOn".Translate() : "RenHoek_MusicPlayer_ShuffleOff".Translate()))
                {
                    if (MusicPlayerMod.Settings != null)
                        MusicPlayerMod.Settings.ShuffleEnabled = !MusicPlayerMod.Settings.ShuffleEnabled;
                }
                GUI.backgroundColor = oldShuffleBg;
                btnX += btnSize + btnSpacing;
            }
            else
            {
                // Hide controls button FIRST when controls visible
                Rect hideBtn = new Rect(btnX, btnY, btnSize, btnSize);
                string hideSymbol = "\u2014";  // — when showing (hide)
                if (DrawArialButton(hideBtn, hideSymbol, "RenHoek_MusicPlayer_HideControls".Translate()))
                {
                    controlsHidden = true;
                    isExpanded = false;  // Collapse playlist when hiding controls
                }
                btnX += btnSize + btnSpacing;
                
                // Expand/Collapse button (playlist)
                // Arrow points toward where playlist will appear/disappear based on orientation
                Rect expandBtn = new Rect(btnX, btnY, btnSize, btnSize);
                string expandSymbol;
                if (playlistOnTop)
                {
                    // Playlist expands upward - arrow up when collapsed, down when expanded
                    expandSymbol = isExpanded ? "\u25BC" : "\u25B2";  // ▼ when expanded (collapse up), ▲ when collapsed (expand up)
                }
                else
                {
                    // Playlist expands downward - arrow down when collapsed, up when expanded
                    expandSymbol = isExpanded ? "\u25B2" : "\u25BC";  // ▲ when expanded (collapse down), ▼ when collapsed (expand down)
                }
                
                if (DrawArialButton(expandBtn, expandSymbol, isExpanded ? "RenHoek_MusicPlayer_Collapse".Translate() : "RenHoek_MusicPlayer_Expand".Translate()))
                    isExpanded = !isExpanded;
                btnX += btnSize + btnSpacing;
                
                // Context-aware button
                bool contextAware = MusicPlayerMod.Settings?.ContextAware ?? true;
                Rect contextBtn = new Rect(btnX, btnY, btnSize, btnSize);
                string contextSymbol = contextAware ? "\u25A3" : "\u29C9";
                if (DrawArialButton(contextBtn, contextSymbol, contextAware ? "RenHoek_MusicPlayer_ContextOn".Translate() : "RenHoek_MusicPlayer_ContextOff".Translate()))
                {
                    if (MusicPlayerMod.Settings != null)
                        MusicPlayerMod.Settings.ContextAware = !MusicPlayerMod.Settings.ContextAware;
                }
                btnX += btnSize + btnSpacing;
            }

            // Lock button - only show when controls are visible
            if (!controlsHidden)
            {
                Rect lockBtn = new Rect(btnX, btnY, btnSize, btnSize);
                var oldBg = GUI.backgroundColor;
                if (isLocked)
                    GUI.backgroundColor = LockedColor;
                string lockSymbol = isLocked ? "\u29BF" : "\u29BE";
                if (DrawArialButton(lockBtn, lockSymbol, isLocked ? "RenHoek_MusicPlayer_Unlock".Translate() : "RenHoek_MusicPlayer_Lock".Translate()))
                    isLocked = !isLocked;
                GUI.backgroundColor = oldBg;
            }
        }

        private static void DrawControls(Rect rect, MusicPlayerController controller)
        {
            float spacing = 2f * widgetScale;
            int numButtons = 6;
            float buttonWidth = (rect.width - spacing * (numButtons - 1)) / numButtons;
            float startX = rect.x;
            float btnY = rect.y + (rect.height - ButtonSize) / 2f;
            
            // Previous button - disabled during cooldown
            bool prevOnCooldown = IsSkipOnCooldown();
            if (DrawArialButton(new Rect(startX, btnY, buttonWidth, ButtonSize), "\u23EE", "RenHoek_MusicPlayer_Previous".Translate(), prevOnCooldown))
            {
                controller.PlayPrevious();
                StartSkipCooldown();
            }

            if (DrawArialButton(new Rect(startX + buttonWidth + spacing, btnY, buttonWidth, ButtonSize), "\u23EA", "RenHoek_MusicPlayer_Rewind".Translate()))
                controller.RestartSong();

            string stopTooltip = MusicPlayerMod.Settings.BlockEventTriggers ? "Total Stop" : "Stop";
            if (DrawArialButton(new Rect(startX + (buttonWidth + spacing) * 2, btnY, buttonWidth, ButtonSize), "\u23F9", stopTooltip))
            {
                controller.Stop();
                // Always kill all audio immediately
                try
                {
                    var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
                    foreach (var src in audioSources)
                    {
                        if (src != null && src.isPlaying)
                        {
                            src.Stop();
                            src.volume = 0f;
                        }
                    }
                }
                catch (Exception) { }
            }

            bool isPausedByGameExpanded = controller.PausedDueToGamePause;
            bool showPauseExpanded = controller.IsPlaying && !controller.IsPaused && !isPausedByGameExpanded;
            string playSymbol = showPauseExpanded ? "\u23F8" : "\u25B6";
            bool isSilenceExpanded = controller.CurrentStatus == MusicStatus.WaitingBetweenSongs;
            string expandedPlayTooltip = isSilenceExpanded ? "RenHoek_MusicPlayer_WaitingForSong".Translate() :
                (isPausedByGameExpanded ? "RenHoek_MusicPlayer_PausedByGame".Translate() :
                (controller.IsPaused ? "RenHoek_MusicPlayer_Resume".Translate() : (controller.IsPlaying ? "RenHoek_MusicPlayer_Pause".Translate() : "RenHoek_MusicPlayer_Play".Translate())));
            if (DrawArialButton(new Rect(startX + (buttonWidth + spacing) * 3, btnY, buttonWidth, ButtonSize), playSymbol, expandedPlayTooltip, isSilenceExpanded || isPausedByGameExpanded))
            {
                if (!isPausedByGameExpanded && controller.CurrentSong == null && controller.AllSongs.Count > 0)
                    controller.PlayNext();
                else if (!isPausedByGameExpanded && controller.CurrentSong != null)
                    controller.TogglePause();
            }

            // Next button - disabled during cooldown
            bool nextOnCooldown = IsSkipOnCooldown();
            if (DrawArialButton(new Rect(startX + (buttonWidth + spacing) * 4, btnY, buttonWidth, ButtonSize), "\u23ED", "RenHoek_MusicPlayer_Next".Translate(), nextOnCooldown))
            {
                controller.PlayNext();
                StartSkipCooldown();
            }

            var oldBg = GUI.backgroundColor;
            if (MusicPlayerMod.Settings.ShuffleEnabled)
                GUI.backgroundColor = PlayingColor;
            if (DrawArialButton(new Rect(startX + (buttonWidth + spacing) * 5, btnY, buttonWidth, ButtonSize), "\u21C4", MusicPlayerMod.Settings.ShuffleEnabled ? "RenHoek_MusicPlayer_ShuffleOn".Translate() : "RenHoek_MusicPlayer_ShuffleOff".Translate()))
                MusicPlayerMod.Settings.ShuffleEnabled = !MusicPlayerMod.Settings.ShuffleEnabled;
            GUI.backgroundColor = oldBg;
        }

        private static void DrawProgressBar(Rect rect, MusicPlayerController controller)
        {
            Color stateColor = GetStateColor(controller);
            
            // Background
            Widgets.DrawBoxSolid(rect, ProgressBarBgColor);
            
            // Progress fill
            float progress = controller.GetPlaybackProgress();
            if (progress > 0)
            {
                Rect fillRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
                Widgets.DrawBoxSolid(fillRect, stateColor * 0.7f);
            }
            
            // ALWAYS show timecode - status text only shown for non-combat paused states
            string statusText = controller.GetStatusText();
            string displayText;
            
            // Timecode is always primary, status text only in specific cases
            string timecode = $"{controller.GetCurrentTimeFormatted()} / {controller.GetTotalTimeFormatted()}";
            
            if (!string.IsNullOrEmpty(statusText) && !controller.IsInCombat)
            {
                // Show status text (like "PAUSED") instead of timecode only when not in combat
                displayText = statusText.ToUpper();
            }
            else
            {
                // Always show timecode in combat or when no status
                displayText = timecode;
            }
            
            // Draw with timecode font - vertically centered with upward offset
            float timeOffset = 2f * widgetScale;
            Rect timeRect = new Rect(rect.x, rect.y - timeOffset, rect.width, rect.height);
            DrawTimecodeText(timeRect, displayText, TextColor, 10);
            
            // Click to seek
            if (Widgets.ButtonInvisible(rect))
            {
                float clickX = Event.current.mousePosition.x - rect.x;
                float normalizedPos = Mathf.Clamp01(clickX / rect.width);
                controller.SeekTo(normalizedPos);
                eventConsumedByControl = true;
            }
            TooltipHandler.TipRegion(rect, "RenHoek_MusicPlayer_ClickToSeek".Translate());
        }

        private static void DrawVerticalVolumeBar(Rect rect, MusicPlayerController controller)
        {
            Color stateColor = GetStateColor(controller);
            
            // Background
            Widgets.DrawBoxSolid(rect, ProgressBarBgColor);
            
            // Fill from bottom up based on volume
            float volume = MusicPlayerMod.Settings.PlayerVolume;
            if (volume > 0)
            {
                float fillHeight = rect.height * volume;
                Rect fillRect = new Rect(rect.x, rect.yMax - fillHeight, rect.width, fillHeight);
                Widgets.DrawBoxSolid(fillRect, stateColor * 0.7f);
            }
            
            // ♪ icon at top
            float iconHeight = 10f * widgetScale;
            Rect iconRect = new Rect(rect.x, rect.y, rect.width, iconHeight);
            DrawScaledText(iconRect, "\u266A", SubTextColor, 9);
            
            // Click/drag to adjust volume
            Event evt = Event.current;
            if (Mouse.IsOver(rect))
            {
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    float clickY = evt.mousePosition.y - rect.y;
                    float newVolume = 1f - (clickY / rect.height);
                    MusicPlayerMod.Settings.PlayerVolume = Mathf.Clamp01(newVolume);
                    eventConsumedByControl = true;
                    evt.Use();
                }
                else if (evt.type == EventType.MouseDrag && evt.button == 0)
                {
                    float clickY = evt.mousePosition.y - rect.y;
                    float newVolume = 1f - (clickY / rect.height);
                    MusicPlayerMod.Settings.PlayerVolume = Mathf.Clamp01(newVolume);
                    eventConsumedByControl = true;
                    evt.Use();
                }
            }
            
            TooltipHandler.TipRegion(rect, "RenHoek_MusicPlayer_Volume".Translate($"{MusicPlayerMod.Settings.PlayerVolume:P0}"));
        }

        private static void DrawSongList(Rect rect, MusicPlayerController controller)
        {
            lastSongListRect = rect;  // Store for scroll bar exclusion
            var allSongs = controller.AllSongs;
            bool needsRebuild = cachedFilteredSongs == null ||
                                lastSearchFilter != searchFilter ||
                                lastSongCount != allSongs.Count ||
                                cacheRebuildFrame < Time.frameCount - 30;
            
            if (needsRebuild)
            {
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    string filter = searchFilter.ToLower();
                    cachedFilteredSongs = allSongs.Where(s => 
                        s.DisplayName.ToLower().Contains(filter) || 
                        s.Artist.ToLower().Contains(filter) ||
                        s.Title.ToLower().Contains(filter) ||
                        s.Category.ToLower().Contains(filter)
                    ).ToList();
                }
                else
                {
                    cachedFilteredSongs = allSongs;
                }
                
                // Cache the grouped, sorted result - songs pre-sorted within each group
                cachedGroupedSongs = cachedFilteredSongs
                    .GroupBy(s => s.Category)
                    .OrderBy(g => GetCategorySortOrder(g.Key))
                    .Select(g => (g.Key, g.OrderBy(s => s.DisplayName).ToList()))
                    .ToList();
                    
                lastSearchFilter = searchFilter;
                lastSongCount = allSongs.Count;
                cacheRebuildFrame = Time.frameCount;
            }
            
            var grouped = cachedGroupedSongs;
            if (grouped == null) return;
            
            string currentDefName = controller.CurrentSong?.Def?.defName;
            if (currentDefName != lastCurrentSongDefName)
            {
                lastCurrentSongDefName = currentDefName;
                if (currentDefName != null)
                    needsScrollToCurrentSong = true;
            }
            
            float contentHeight = 0;
            float currentSongY = -1f;
            foreach (var group in grouped)
            {
                contentHeight += HeaderHeight;
                foreach (var song in group.Songs)  // Already sorted
                {
                    if (controller.CurrentSong != null && song.Def == controller.CurrentSong.Def)
                        currentSongY = contentHeight;
                    
                    contentHeight += RowHeight;
                    if (expandedSettingsDefName == song.Def.defName)
                        contentHeight += SettingsRowHeight;
                }
            }
            
            if (needsScrollToCurrentSong && currentSongY >= 0)
            {
                float targetScroll = currentSongY - (rect.height / 2f) + (RowHeight / 2f);
                targetScroll = Mathf.Clamp(targetScroll, 0, Mathf.Max(0, contentHeight - rect.height));
                songListScrollPos.y = targetScroll;
                needsScrollToCurrentSong = false;
            }

            // ScrollView with vertical scrollbar
            songListScrollPos.x = 0f;
            
            // Use ScrollView for proper vertical scrollbar
            Rect viewRect = new Rect(0, 0, rect.width - 16f, contentHeight);
            songListScrollPos = GUI.BeginScrollView(rect, songListScrollPos, viewRect, false, contentHeight > rect.height);
            
            // Calculate visible Y bounds for tooltip clipping (in content coordinates)
            float visibleYMin = songListScrollPos.y;
            float visibleYMax = songListScrollPos.y + rect.height;

            float y = 0f;
            float clipWidth = viewRect.width;
            foreach (var group in grouped)
            {
                DrawCategoryHeader(new Rect(0, y, clipWidth, HeaderHeight), group.Category);
                y += HeaderHeight;

                foreach (var song in group.Songs)  // Already sorted
                {
                    DrawSongRow(new Rect(0, y, clipWidth, RowHeight), song, controller, visibleYMin, visibleYMax);
                    y += RowHeight;
                    
                    if (expandedSettingsDefName == song.Def.defName)
                    {
                        DrawSongSettings(new Rect(0, y, clipWidth, SettingsRowHeight), song, controller);
                        y += SettingsRowHeight;
                    }
                }
            }

            GUI.EndScrollView();
        }

        private static void DrawCategoryHeader(Rect rect, string category)
        {
            Widgets.DrawBoxSolid(rect, HeaderColor);
            DrawScaledText(rect, category.ToUpper(), GetCategoryColor(category), 11);
        }

        private static void DrawSongRow(Rect rect, SongInfo song, MusicPlayerController controller, float visibleYMin, float visibleYMax)
        {
            // Check if row is visible for tooltip registration (not for drawing - scroll view handles that)
            bool isVisibleForTooltips = rect.yMax > visibleYMin && rect.y < visibleYMax;

            // Skip expensive GUI work for off-screen rows (scroll view clips visually but we still pay CPU).
            if (rect.yMax < visibleYMin || rect.y > visibleYMax)
                return;

            
            bool isCurrentSong = controller.CurrentSong != null && song.Def == controller.CurrentSong.Def;
            bool isHovered = Mouse.IsOver(rect);
            bool hasExpandedSettings = expandedSettingsDefName == song.Def.defName;

            if (isCurrentSong)
                Widgets.DrawBoxSolid(rect, AccentColor * 0.3f);
            else if (isHovered)
                Widgets.DrawBoxSolid(rect, HoverColor);

            float textStartX = 8f * widgetScale;
            if (isCurrentSong)
            {
                Rect indicatorRect = new Rect(rect.x + 4f * widgetScale, rect.y, 14f * widgetScale, rect.height);
                bool showPausedIndicator = controller.IsPaused || controller.PausedDueToGamePause;
                DrawScaledText(indicatorRect, showPausedIndicator ? "||" : ">", PlayingColor, 10);
                textStartX = 20f * widgetScale;
            }

            float btnSize = 20f * widgetScale;
            Rect settingsBtnRect = new Rect(rect.xMax - btnSize - 4f * widgetScale, rect.y + 2f * widgetScale, btnSize, rect.height - 4f * widgetScale);
            
            Color btnBgColor = hasExpandedSettings ? SettingsButtonActiveColor * 0.5f : new Color(0.25f, 0.25f, 0.3f, 0.8f);
            Widgets.DrawBoxSolid(settingsBtnRect, btnBgColor);
            
            DrawScaledText(settingsBtnRect, hasExpandedSettings ? "-" : "+", hasExpandedSettings ? SettingsButtonActiveColor : TextColor, 10);
            
            if (Widgets.ButtonInvisible(settingsBtnRect))
            {
                expandedSettingsDefName = hasExpandedSettings ? null : song.Def.defName;
                eventConsumedByControl = true;
            }
            if (isVisibleForTooltips)
                TooltipHandler.TipRegion(settingsBtnRect, "RenHoek_MusicPlayer_ConfigureSong".Translate());

            float iconWidth = 20f * widgetScale;
            Rect iconRect = new Rect(rect.xMax - btnSize - iconWidth - 8f * widgetScale, rect.y, iconWidth, rect.height);
            DrawScaledText(iconRect, GetCategoryIcon(song.Category), GetCategoryColor(song.Category), 11);
            if (isVisibleForTooltips)
                TooltipHandler.TipRegion(iconRect, song.Category);

            Rect nameRect = new Rect(rect.x + textStartX, rect.y, rect.width - textStartX - btnSize - iconWidth - 16f * widgetScale, rect.height);
            
            string displayName = song.DisplayName;
            // Truncate if needed
            if (arialLabelStyle != null)
            {
                var oldSize = arialLabelStyle.fontSize;
                arialLabelStyle.fontSize = Mathf.RoundToInt(10 * widgetScale);
                while (arialLabelStyle.CalcSize(new GUIContent(displayName)).x > nameRect.width - 4f && displayName.Length > 3)
                {
                    displayName = displayName.Substring(0, displayName.Length - 4) + "...";
                }
                arialLabelStyle.fontSize = oldSize;
            }
            
            DrawScaledText(nameRect, displayName, isCurrentSong ? PlayingColor : TextColor, 10, TextAnchor.MiddleLeft);

            if (Widgets.ButtonInvisible(nameRect))
            {
                controller.Play(song);
                eventConsumedByControl = true;
                
                // Auto-collapse playlist if setting enabled
                if (MusicPlayerMod.Settings.AutoCollapsePlaylist)
                {
                    isExpanded = false;
                }
            }

            if (isVisibleForTooltips)
                TooltipHandler.TipRegion(nameRect, "RenHoek_MusicPlayer_SongTooltip".Translate(song.Title, song.Artist));
        }

        private static void DrawSongSettings(Rect rect, SongInfo song, MusicPlayerController controller)
        {
            Widgets.DrawBoxSolid(rect, SettingsBgColor);
            
            var settings = controller.GetSongSettings(song);
            if (settings == null) return;

            float toggleWidth = 46f * widgetScale;
            float spacing = 6f * widgetScale;
            float totalWidth = toggleWidth * 4 + spacing * 3;
            float startX = rect.x + (rect.width - totalWidth) / 2f;
            float toggleY = rect.y + 3f * widgetScale;
            float toggleHeight = rect.height - 6f * widgetScale;

            DrawToggleButton(new Rect(startX, toggleY, toggleWidth, toggleHeight), 
                "Day", ref settings.AllowDay, new Color(0.9f, 0.8f, 0.3f, 1f));

            DrawToggleButton(new Rect(startX + (toggleWidth + spacing), toggleY, toggleWidth, toggleHeight), 
                "Night", ref settings.AllowNight, new Color(0.5f, 0.5f, 0.9f, 1f));

            DrawToggleButton(new Rect(startX + (toggleWidth + spacing) * 2, toggleY, toggleWidth, toggleHeight), 
                "Wntr", ref settings.AllowWinter, new Color(0.6f, 0.85f, 1f, 1f));

            DrawToggleButton(new Rect(startX + (toggleWidth + spacing) * 3, toggleY, toggleWidth, toggleHeight), 
                "Cbt", ref settings.AllowCombat, new Color(0.9f, 0.3f, 0.3f, 1f));
        }

        private static void DrawToggleButton(Rect rect, string label, ref bool value, Color activeColor)
        {
            Color bgColor = value ? activeColor * 0.6f : new Color(0.3f, 0.3f, 0.3f, 0.6f);
            Color textColor = value ? Color.white : SubTextColor;
            
            Widgets.DrawBoxSolid(rect, bgColor);
            DrawScaledText(rect, label, textColor, 10);

            if (Widgets.ButtonInvisible(rect))
            {
                value = !value;
                eventConsumedByControl = true;
            }
        }

        private static int GetCategorySortOrder(string category)
        {
            switch (category.ToLower())
            {
                case "combat": return 0;
                case "day": return 1;
                case "night": return 2;
                case "winter": return 3;
                case "multi": return 4;
                default: return 5;
            }
        }

        private static Color GetCategoryColor(string category)
        {
            switch (category.ToLower())
            {
                case "combat": return new Color(0.9f, 0.3f, 0.3f, 0.9f);
                case "day": return new Color(0.9f, 0.8f, 0.3f, 0.9f);
                case "night": return new Color(0.5f, 0.5f, 0.9f, 0.9f);
                case "winter": return new Color(0.6f, 0.85f, 1f, 0.9f);
                case "multi": return new Color(0.8f, 0.6f, 0.9f, 0.9f);
                default: return new Color(0.6f, 0.6f, 0.6f, 0.9f);
            }
        }

        private static string GetCategoryIcon(string category)
        {
            switch (category.ToLower())
            {
                case "combat": return "\u2694";
                case "day": return "\u2600";
                case "night": return "\u263D";
                case "winter": return "\u2744";
                case "multi": return "\u266A";
                default: return "?";
            }
        }
    }
}
