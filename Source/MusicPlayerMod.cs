using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RenHoek.MusicPlayer
{
    /// <summary>
    /// Main mod class - initializes the music player and removes vanilla music.
    /// </summary>
    public class MusicPlayerMod : Mod
    {
        public static MusicPlayerSettings Settings;
        public static MusicPlayerMod Instance;

        public MusicPlayerMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<MusicPlayerSettings>();
        }

        public override string SettingsCategory() => "RenHoek_MusicPlayer_ModName".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Single Listing_Standard with ColumnWidth + NewColumn() - the RimWorld-native two-column approach
            Listing_Standard listing = new Listing_Standard();
            listing.ColumnWidth = (inRect.width - 17f) / 2f;
            listing.Begin(inRect);
            
            // === LEFT COLUMN ===
            listing.CheckboxLabeled("RenHoek_MusicPlayer_ShuffleMode".Translate(), ref Settings.ShuffleEnabled, 
                "RenHoek_MusicPlayer_ShuffleModeDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_ContextAware".Translate(), ref Settings.ContextAware, 
                "RenHoek_MusicPlayer_ContextAwareDesc".Translate());
            listing.Gap(4f);
            
            bool prevIncludeVanillaSongs = Settings.IncludeVanillaSongs;
            listing.CheckboxLabeled("RenHoek_MusicPlayer_IncludeVanilla".Translate(), ref Settings.IncludeVanillaSongs, 
                "RenHoek_MusicPlayer_IncludeVanillaDesc".Translate());

            // Apply immediately (no restart needed) — this only affects what this mod will select/play.
            if (prevIncludeVanillaSongs != Settings.IncludeVanillaSongs)
            {
                MusicPlayerController.Instance?.InitializeSongList();

                // If we just disabled vanilla and one is currently playing, advance.
                if (!Settings.IncludeVanillaSongs)
                {
                    var current = MusicPlayerController.Instance?.CurrentSong;
                    if (current != null && current.IsVanilla)
                        MusicPlayerController.Instance?.PlayNext();
                }
            }

            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_HidePlayer".Translate(), ref Settings.HidePlayer, 
                "RenHoek_MusicPlayer_HidePlayerDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_LockOrientation".Translate(), ref Settings.LockPlaylistOrientation, 
                "RenHoek_MusicPlayer_LockOrientationDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_ScrollingTitles".Translate(), ref Settings.ScrollingTitle, 
                "RenHoek_MusicPlayer_ScrollingTitlesDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_TotalStop".Translate(), ref Settings.BlockEventTriggers, 
                "RenHoek_MusicPlayer_TotalStopDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_NowPlaying".Translate(), ref Settings.ShowNowPlayingNotification, 
                "RenHoek_MusicPlayer_NowPlayingDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_PauseWithGame".Translate(), ref Settings.PauseMusicWhenGamePaused, 
                "RenHoek_MusicPlayer_PauseWithGameDesc".Translate());
            listing.Gap(4f);
            
            listing.CheckboxLabeled("RenHoek_MusicPlayer_AutoCollapse".Translate(), ref Settings.AutoCollapsePlaylist, 
                "RenHoek_MusicPlayer_AutoCollapseDesc".Translate());
            listing.Gap(8f);
            
            // === RIGHT COLUMN ===
            listing.NewColumn();
            
            // Row 1: Forced Fade + Title Font labels
            Rect row1 = listing.GetRect(22f);
            float halfWidth = row1.width / 2f - 4f;
            Widgets.Label(new Rect(row1.x, row1.y, halfWidth, row1.height), "RenHoek_MusicPlayer_ForcedFade".Translate() + ":");
            Widgets.Label(new Rect(row1.x + halfWidth + 8f, row1.y, halfWidth, row1.height), "RenHoek_MusicPlayer_TitleFont".Translate(""));
            
            // Row 2: Forced Fade + Title Font selectors
            Rect row2 = listing.GetRect(30f);
            float forcedDuration = Settings.GetManualFadeDuration();
            string forcedDisplay = forcedDuration > 0 
                ? $"{MusicPlayerSettings.GetFadeSpeedLabel(Settings.ManualFadeSpeedIndex)} ({forcedDuration:F1}s)"
                : "RenHoek_MusicPlayer_FadeOff".Translate().ToString();
            DrawArrowSelectorInRect(new Rect(row2.x, row2.y, halfWidth, row2.height), forcedDisplay, 
                MusicPlayerSettings.FadeSpeedKeys.Length, ref Settings.ManualFadeSpeedIndex, wrapAround: false);
            
            string titleFontName = MusicPlayerSettings.FontNames[Mathf.Clamp(Settings.TitleFontIndex, 0, MusicPlayerSettings.FontNames.Length - 1)];
            int newTitleIndex = Settings.TitleFontIndex;
            DrawFontSelector(new Rect(row2.x + halfWidth + 8f, row2.y, halfWidth, row2.height), titleFontName,
                MusicPlayerSettings.FontNames.Length, ref newTitleIndex);
            if (newTitleIndex != Settings.TitleFontIndex)
            {
                Settings.TitleFontIndex = newTitleIndex;
                MusicPlayerWidget.MarkFontsDirty();
            }
            listing.Gap(4f);
            
            // Row 3: Natural Fade + Timecode Font labels
            Rect row3 = listing.GetRect(22f);
            Widgets.Label(new Rect(row3.x, row3.y, halfWidth, row3.height), "RenHoek_MusicPlayer_NaturalFade".Translate() + ":");
            Widgets.Label(new Rect(row3.x + halfWidth + 8f, row3.y, halfWidth, row3.height), "RenHoek_MusicPlayer_TimecodeFont".Translate(""));
            
            // Row 4: Natural Fade + Timecode Font selectors
            Rect row4 = listing.GetRect(30f);
            float naturalDuration = Settings.GetFadeDuration();
            string naturalDisplay = naturalDuration > 0 
                ? $"{MusicPlayerSettings.GetFadeSpeedLabel(Settings.FadeSpeedIndex)} ({naturalDuration:F1}s)"
                : "RenHoek_MusicPlayer_FadeOff".Translate().ToString();
            DrawArrowSelectorInRect(new Rect(row4.x, row4.y, halfWidth, row4.height), naturalDisplay,
                MusicPlayerSettings.FadeSpeedKeys.Length, ref Settings.FadeSpeedIndex, wrapAround: false);
            
            string timecodeFontName = MusicPlayerSettings.FontNames[Mathf.Clamp(Settings.TimecodeFontIndex, 0, MusicPlayerSettings.FontNames.Length - 1)];
            int newTimecodeIndex = Settings.TimecodeFontIndex;
            DrawFontSelector(new Rect(row4.x + halfWidth + 8f, row4.y, halfWidth, row4.height), timecodeFontName,
                MusicPlayerSettings.FontNames.Length, ref newTimecodeIndex);
            if (newTimecodeIndex != Settings.TimecodeFontIndex)
            {
                Settings.TimecodeFontIndex = newTimecodeIndex;
                MusicPlayerWidget.MarkFontsDirty();
            }
            listing.Gap(8f);
            
            // Default Position - 2x2 Grid
            listing.Label("RenHoek_MusicPlayer_DefaultPosition".Translate(""));
            Rect gridRect = listing.GetRect(70f);
            float cellWidth = gridRect.width / 2f - 4f;
            float cellHeight = 30f;
            float gap = 4f;
            
            // Visual top row = Top positions (indices 2, 3)
            Rect tlRect = new Rect(gridRect.x, gridRect.y, cellWidth, cellHeight);
            DrawPositionCell(tlRect, 2, "RenHoek_MusicPlayer_TopLeft".Translate());
            
            Rect trRect = new Rect(gridRect.x + cellWidth + gap, gridRect.y, cellWidth, cellHeight);
            DrawPositionCell(trRect, 3, "RenHoek_MusicPlayer_TopRight".Translate());
            
            // Visual bottom row = Bottom positions (indices 0, 1)
            Rect blRect = new Rect(gridRect.x, gridRect.y + cellHeight + gap, cellWidth, cellHeight);
            DrawPositionCell(blRect, 0, "RenHoek_MusicPlayer_BottomLeft".Translate());
            
            Rect brRect = new Rect(gridRect.x + cellWidth + gap, gridRect.y + cellHeight + gap, cellWidth, cellHeight);
            DrawPositionCell(brRect, 1, "RenHoek_MusicPlayer_BottomRight".Translate());
            
            listing.Gap(8f);
            
            // Silence between songs
            listing.Label("RenHoek_MusicPlayer_SilenceBetweenSongs".Translate());
            
            // Min/Max on same line with editable text fields
            Rect silenceLineRect = listing.GetRect(24f);
            float fieldWidth = 45f;
            float labelWidth = 35f;
            
            // "Min:"
            Widgets.Label(new Rect(silenceLineRect.x, silenceLineRect.y, labelWidth, silenceLineRect.height), "Min:");
            Rect minInputRect = new Rect(silenceLineRect.x + labelWidth, silenceLineRect.y, fieldWidth, silenceLineRect.height);
            string minBuffer = Settings.MinSilenceBetweenSongs.ToString("F0");
            minBuffer = Widgets.TextField(minInputRect, minBuffer);
            if (float.TryParse(minBuffer, out float parsedMin))
                Settings.MinSilenceBetweenSongs = Mathf.Clamp(parsedMin, 0f, 600f);
            
            // "s" after min
            Widgets.Label(new Rect(minInputRect.xMax + 2f, silenceLineRect.y, 15f, silenceLineRect.height), "s");
            
            // "Max:" 
            float maxStartX = minInputRect.xMax + 30f;
            Widgets.Label(new Rect(maxStartX, silenceLineRect.y, labelWidth, silenceLineRect.height), "Max:");
            Rect maxInputRect = new Rect(maxStartX + labelWidth, silenceLineRect.y, fieldWidth, silenceLineRect.height);
            string maxBuffer = Settings.MaxSilenceBetweenSongs.ToString("F0");
            maxBuffer = Widgets.TextField(maxInputRect, maxBuffer);
            if (float.TryParse(maxBuffer, out float parsedMax))
                Settings.MaxSilenceBetweenSongs = Mathf.Clamp(parsedMax, 0f, 600f);
            
            // "s" after max
            Widgets.Label(new Rect(maxInputRect.xMax + 2f, silenceLineRect.y, 15f, silenceLineRect.height), "s");
            
            // Range slider with two handles
            Rect sliderRect = listing.GetRect(22f);
            DrawRangeSlider(sliderRect, ref Settings.MinSilenceBetweenSongs, ref Settings.MaxSilenceBetweenSongs, 0f, 600f);
            
            if (Settings.MinSilenceBetweenSongs > Settings.MaxSilenceBetweenSongs)
                Settings.MinSilenceBetweenSongs = Settings.MaxSilenceBetweenSongs;
            
            listing.End();
        }
        
        private void DrawArrowSelector(Listing_Standard listing, string currentLabel, int optionCount, ref int currentIndex, Func<int, string> getLabel)
        {
            Rect rect = listing.GetRect(30f);
            DrawArrowSelectorInRect(rect, currentLabel, optionCount, ref currentIndex);
        }
        
        private void DrawArrowSelectorInRect(Rect rect, string currentLabel, int optionCount, ref int currentIndex, bool wrapAround = true)
        {
            float arrowWidth = 30f;
            bool atMin = currentIndex <= 0;
            bool atMax = currentIndex >= optionCount - 1;
            
            // Left arrow - hidden at minimum when not wrapping
            Rect leftArrow = new Rect(rect.x, rect.y, arrowWidth, rect.height);
            if (atMin && !wrapAround)
            {
                // Just draw empty button background, no arrow
                Widgets.DrawBoxSolid(leftArrow, new Color(0.15f, 0.15f, 0.15f, 0.3f));
            }
            else if (Widgets.ButtonText(leftArrow, "<"))
            {
                if (wrapAround)
                    currentIndex = (currentIndex - 1 + optionCount) % optionCount;
                else
                    currentIndex = Mathf.Max(0, currentIndex - 1);
            }
            
            // Center display
            Rect centerRect = new Rect(rect.x + arrowWidth + 4f, rect.y, rect.width - (arrowWidth * 2) - 8f, rect.height);
            Widgets.DrawBoxSolid(centerRect, new Color(0.1f, 0.1f, 0.12f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(centerRect, currentLabel);
            Text.Anchor = TextAnchor.UpperLeft;
            
            // Right arrow - hidden at maximum when not wrapping
            Rect rightArrow = new Rect(rect.xMax - arrowWidth, rect.y, arrowWidth, rect.height);
            if (atMax && !wrapAround)
            {
                // Just draw empty button background, no arrow
                Widgets.DrawBoxSolid(rightArrow, new Color(0.15f, 0.15f, 0.15f, 0.3f));
            }
            else if (Widgets.ButtonText(rightArrow, ">"))
            {
                if (wrapAround)
                    currentIndex = (currentIndex + 1) % optionCount;
                else
                    currentIndex = Mathf.Min(optionCount - 1, currentIndex + 1);
            }
        }
        
        // Font style cache for preview
        private static Dictionary<string, GUIStyle> fontStyleCache = new Dictionary<string, GUIStyle>();
        
        private void DrawFontSelector(Rect rect, string fontName, int optionCount, ref int currentIndex)
        {
            float arrowWidth = 30f;
            
            // Get actual system font name from mapping
            string actualFontName;
            if (MusicPlayerSettings.FontNameMapping.TryGetValue(fontName, out string mappedName))
                actualFontName = mappedName;
            else
                actualFontName = fontName;
            
            // Left arrow
            Rect leftArrow = new Rect(rect.x, rect.y, arrowWidth, rect.height);
            if (Widgets.ButtonText(leftArrow, "<"))
            {
                currentIndex = (currentIndex - 1 + optionCount) % optionCount;
                // Clear cache for this font to force reload
                fontStyleCache.Remove(fontName);
            }
            
            // Center display - render font name in that font
            Rect centerRect = new Rect(rect.x + arrowWidth + 4f, rect.y, rect.width - (arrowWidth * 2) - 8f, rect.height);
            Widgets.DrawBoxSolid(centerRect, new Color(0.1f, 0.1f, 0.12f));
            
            // Get or create font style for preview
            if (!fontStyleCache.TryGetValue(fontName, out GUIStyle style) || style == null)
            {
                Font font = Font.CreateDynamicFontFromOSFont(actualFontName, 14);
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = Color.white;
                if (font != null)
                {
                    style.font = font;
                }
                fontStyleCache[fontName] = style;
            }
            
            GUI.Label(centerRect, fontName, style);
            
            // Right arrow
            Rect rightArrow = new Rect(rect.xMax - arrowWidth, rect.y, arrowWidth, rect.height);
            if (Widgets.ButtonText(rightArrow, ">"))
            {
                currentIndex = (currentIndex + 1) % optionCount;
                // Clear cache to force reload on next draw
                string newFontName = MusicPlayerSettings.FontNames[(currentIndex) % MusicPlayerSettings.FontNames.Length];
                fontStyleCache.Remove(newFontName);
            }
        }
        
        private void DrawRangeSlider(Rect rect, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
        {
            // Draw track background
            Rect trackRect = new Rect(rect.x + 8f, rect.y + rect.height / 2f - 2f, rect.width - 16f, 4f);
            Widgets.DrawBoxSolid(trackRect, new Color(0.2f, 0.2f, 0.2f));
            
            // Draw filled range between handles
            float minPos = Mathf.Lerp(trackRect.x, trackRect.xMax, (minValue - minLimit) / (maxLimit - minLimit));
            float maxPos = Mathf.Lerp(trackRect.x, trackRect.xMax, (maxValue - minLimit) / (maxLimit - minLimit));
            Rect filledRect = new Rect(minPos, trackRect.y, maxPos - minPos, trackRect.height);
            Widgets.DrawBoxSolid(filledRect, new Color(0.4f, 0.6f, 0.4f));
            
            // Handle size
            float handleWidth = 12f;
            float handleHeight = rect.height - 4f;
            
            // Min handle
            Rect minHandleRect = new Rect(minPos - handleWidth / 2f, rect.y + 2f, handleWidth, handleHeight);
            Widgets.DrawBoxSolid(minHandleRect, new Color(0.6f, 0.6f, 0.6f));
            Widgets.DrawBox(minHandleRect);
            
            // Max handle
            Rect maxHandleRect = new Rect(maxPos - handleWidth / 2f, rect.y + 2f, handleWidth, handleHeight);
            Widgets.DrawBoxSolid(maxHandleRect, new Color(0.6f, 0.6f, 0.6f));
            Widgets.DrawBox(maxHandleRect);
            
            // Handle dragging
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                float mouseX = Event.current.mousePosition.x;
                float distToMin = Mathf.Abs(mouseX - minPos);
                float distToMax = Mathf.Abs(mouseX - maxPos);
                
                // Determine which handle to drag (closer one)
                draggingMinHandle = distToMin <= distToMax;
                draggingMaxHandle = !draggingMinHandle;
                Event.current.Use();
            }
            
            if (Event.current.type == EventType.MouseUp)
            {
                draggingMinHandle = false;
                draggingMaxHandle = false;
            }
            
            if (Event.current.type == EventType.MouseDrag && (draggingMinHandle || draggingMaxHandle))
            {
                float mouseX = Mathf.Clamp(Event.current.mousePosition.x, trackRect.x, trackRect.xMax);
                float newValue = Mathf.Lerp(minLimit, maxLimit, (mouseX - trackRect.x) / trackRect.width);
                newValue = Mathf.Round(newValue); // Snap to whole numbers
                
                if (draggingMinHandle)
                {
                    minValue = Mathf.Clamp(newValue, minLimit, maxValue);
                }
                else if (draggingMaxHandle)
                {
                    maxValue = Mathf.Clamp(newValue, minValue, maxLimit);
                }
                Event.current.Use();
            }
        }
        
        private static bool draggingMinHandle = false;
        private static bool draggingMaxHandle = false;
        
        private void DrawPositionCell(Rect rect, int positionIndex, string label)
        {
            bool isSelected = Settings.DefaultPositionIndex == positionIndex;
            
            // Background
            if (isSelected)
                Widgets.DrawBoxSolid(rect, new Color(0.3f, 0.5f, 0.3f, 0.5f));
            else
                Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            
            Widgets.DrawBox(rect);
            
            // Only show text if selected
            if (isSelected)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, label);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            // Click to select and reset position
            if (Widgets.ButtonInvisible(rect))
            {
                Settings.DefaultPositionIndex = positionIndex;
                MusicPlayerWidget.ResetPosition();
            }
        }
    }

    public class MusicPlayerSettings : ModSettings
    {
        public bool ShuffleEnabled = true;
        public bool ContextAware = true;
        public bool IncludeVanillaSongs = false;
        public bool HidePlayer = false;
        public bool LockPlaylistOrientation = false;
        public bool ScrollingTitle = true;
        public bool AllowResize = true;
        public bool BlockEventTriggers = false;
        public bool ShowNowPlayingNotification = true;
        public bool PauseMusicWhenGamePaused = false;
        public bool AutoCollapsePlaylist = false;
        public float PlayerVolume = 1f;
        public float MinSilenceBetweenSongs = 2f;
        public float MaxSilenceBetweenSongs = 25f;
        public int FadeSpeedIndex = 4;  // Default to Normal
        public int ManualFadeSpeedIndex = 4;  // Default to Normal
        public int TitleFontIndex = 0;   // Arial (Default)
        public int TimecodeFontIndex = 0; // Arial (Default)
        public int DefaultPositionIndex = 1;  // Default to Bottom Right
        
        public static readonly string[] PositionKeys = {
            "RenHoek_MusicPlayer_BottomLeft",
            "RenHoek_MusicPlayer_BottomRight",
            "RenHoek_MusicPlayer_TopLeft",
            "RenHoek_MusicPlayer_TopRight"
        };
        
        public static string GetPositionLabel(int index)
        {
            if (index < 0 || index >= PositionKeys.Length)
                return PositionKeys[1].Translate();
            return PositionKeys[index].Translate();
        }

        public static readonly string[] FontNames = {
            "Arial (Default)",     // 0 - Default
            "Calibri",             // 1 - RimWorld's default font
            "Segoe UI Symbol",     // 2 - Good Unicode support
            "Verdana",             // 3
            "Tahoma",              // 4
            "Trebuchet MS",        // 5
            "Georgia",             // 6
            "Times New Roman",     // 7
            "Palatino Linotype",   // 8
            "Garamond",            // 9
            "Century Gothic",      // 10
            "Consolas",            // 11
            "Courier New",         // 12
            "Segoe UI",            // 13
            "Cascadia Code",       // 14
            "Bahnschrift",         // 15
            "Impact",              // 16
            "Comic Sans MS",       // 17
            "Lucida Sans Uni",     // 18 - Shortened from Lucida Sans Unicode
            "Franklin Gothic Med"  // 19 - Shortened from Franklin Gothic Medium
        };
        
        // Maps display names to actual system font names
        public static readonly Dictionary<string, string> FontNameMapping = new Dictionary<string, string>
        {
            { "Arial (Default)", "Arial" },
            { "Lucida Sans Uni", "Lucida Sans Unicode" },
            { "Franklin Gothic Med", "Franklin Gothic Medium" }
        };
        
        // Get actual font name for system font loading
        public static string GetActualFontName(int index)
        {
            if (index < 0 || index >= FontNames.Length)
                return "Arial";
            string displayName = FontNames[index];
            if (FontNameMapping.TryGetValue(displayName, out string actualName))
                return actualName;
            return displayName;
        }

        public static readonly string[] FadeSpeedKeys = { 
            "RenHoek_MusicPlayer_FadeOff", "RenHoek_MusicPlayer_FadeSuperSlow", "RenHoek_MusicPlayer_FadeVerySlow", "RenHoek_MusicPlayer_FadeSlow",
            "RenHoek_MusicPlayer_FadeNormal", "RenHoek_MusicPlayer_FadeFast", "RenHoek_MusicPlayer_FadeVeryFast", "RenHoek_MusicPlayer_FadeSuperFast"
        };
        public static readonly float[] FadeSpeedValues = { 0f, 5.0f, 4.0f, 3.0f, 2.0f, 1.0f, 0.5f, 0.2f };
        
        public static string GetFadeSpeedLabel(int index)
        {
            if (index < 0 || index >= FadeSpeedKeys.Length)
                return FadeSpeedKeys[4].Translate();  // Default to Normal
            return FadeSpeedKeys[index].Translate();
        }
        
        public float GetFadeDuration()
        {
            if (FadeSpeedIndex < 0 || FadeSpeedIndex >= FadeSpeedValues.Length)
                return FadeSpeedValues[4]; // Default to Normal
            return FadeSpeedValues[FadeSpeedIndex];
        }
        
        public float GetManualFadeDuration()
        {
            if (ManualFadeSpeedIndex < 0 || ManualFadeSpeedIndex >= FadeSpeedValues.Length)
                return FadeSpeedValues[4]; // Default to Normal
            return FadeSpeedValues[ManualFadeSpeedIndex];
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ShuffleEnabled, "ShuffleEnabled", true);
            Scribe_Values.Look(ref ContextAware, "ContextAware", true);
            Scribe_Values.Look(ref IncludeVanillaSongs, "IncludeVanillaSongs", false);
            Scribe_Values.Look(ref HidePlayer, "HidePlayer", false);
            Scribe_Values.Look(ref LockPlaylistOrientation, "LockPlaylistOrientation", false);
            Scribe_Values.Look(ref ScrollingTitle, "ScrollingTitle", true);
            Scribe_Values.Look(ref AllowResize, "AllowResize", true);
            Scribe_Values.Look(ref BlockEventTriggers, "BlockEventTriggers", false);
            Scribe_Values.Look(ref ShowNowPlayingNotification, "ShowNowPlayingNotification", true);
            Scribe_Values.Look(ref PauseMusicWhenGamePaused, "PauseMusicWhenGamePaused", false);
            Scribe_Values.Look(ref AutoCollapsePlaylist, "AutoCollapsePlaylist", false);
            Scribe_Values.Look(ref PlayerVolume, "PlayerVolume", 1f);
            Scribe_Values.Look(ref MinSilenceBetweenSongs, "MinSilenceBetweenSongs", 2f);
            Scribe_Values.Look(ref MaxSilenceBetweenSongs, "MaxSilenceBetweenSongs", 25f);
            Scribe_Values.Look(ref FadeSpeedIndex, "FadeSpeedIndex", 3);
            Scribe_Values.Look(ref ManualFadeSpeedIndex, "ManualFadeSpeedIndex", 1);
            Scribe_Values.Look(ref TitleFontIndex, "TitleFontIndex", 0);
            Scribe_Values.Look(ref TimecodeFontIndex, "TimecodeFontIndex", 0);
            Scribe_Values.Look(ref DefaultPositionIndex, "DefaultPositionIndex", 1);
        }
    }

    /// <summary>
    /// Static constructor to remove vanilla music and apply Harmony patches.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MusicPlayerInit
    {
        public static HashSet<string> VanillaSongDefNames = new HashSet<string>();        static MusicPlayerInit()
        {
            try
            {
                ApplyHarmonyPatches();
                Log.Message("[Ren Höek's Music Player] Initialized successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[Ren Höek's Music Player] Initialization error: {ex}");
            }
        }

        
static void ApplyHarmonyPatches()
        {
            var harmony = new Harmony("renhoek.musicplayer");
            harmony.PatchAll();
            Log.Message("[Ren Höek's Music Player] Harmony patches applied.");
        }
    }
}
