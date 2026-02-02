using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RenHoek.MusicPlayer
{
    /// <summary>
    /// Harmony patches to integrate with RimWorld's music system.
    /// </summary>
    
    /// <summary>
    /// Fix vanilla's ChooseNextSong crashing when song weights are zero.
    /// Uses TryRandomElementByWeight instead of RandomElementByWeight.
    /// </summary>
    [HarmonyPatch(typeof(MusicManagerPlay), "ChooseNextSong")]
    public static class Patch_MusicManagerPlay_ChooseNextSong
    {
        public static bool Prefix(ref SongDef __result)
        {
            var songs = DefDatabase<SongDef>.AllDefsListForReading;
            
            if (songs == null || songs.Count == 0)
            {
                __result = null;
                return false;
            }
            
            // Use TryRandomElementByWeight - won't crash if totalWeight=0
            if (songs.TryRandomElementByWeight(s => s.commonality, out SongDef song))
            {
                __result = song;
            }
            else
            {
                // Fallback: grab any song if weights are all zero
                __result = songs[UnityEngine.Random.Range(0, songs.Count)];
            }
            
            return false; // Skip original method
        }
    }
    
    /// <summary>
    /// Prevent vanilla music manager from auto-starting songs.
    /// </summary>
    [HarmonyPatch(typeof(MusicManagerPlay), "StartNewSong")]
    public static class Patch_MusicManagerPlay_StartNewSong
    {
        public static bool Prefix()
        {
            // If our controller exists and has a song playing, skip vanilla auto-play
            if (MusicPlayerController.Instance?.CurrentSong != null)
            {
                return false; // Skip original method
            }
            return true;
        }
    }
    
    /// <summary>
    /// Intercept MusicUpdate to control volume during fade.
    /// RimWorld's MusicUpdate sets audioSource.volume = Prefs.VolumeMusic every tick,
    /// which overwrites our fade volume changes. This postfix reapplies our fade volume.
    /// </summary>
    [HarmonyPatch(typeof(MusicManagerPlay), "MusicUpdate")]
    public static class Patch_MusicManagerPlay_MusicUpdate
    {
        public static void Postfix(AudioSource ___audioSource)
        {
            // If we're fading, override the volume that MusicUpdate just set
            if (MusicPlayerController.Instance != null && MusicPlayerController.Instance.IsFading)
            {
                float fadeVolume = MusicPlayerController.Instance.GetFadeVolume();
                if (___audioSource != null)
                {
                    ___audioSource.volume = fadeVolume;
                }
            }
            // Also apply our volume multiplier even when not fading
            else if (MusicPlayerController.Instance != null && MusicPlayerMod.Settings.PlayerVolume < 1f)
            {
                if (___audioSource != null)
                {
                    ___audioSource.volume = MusicPlayerController.Instance.GetEffectiveVolume();
                }
            }
        }
    }
    
    /// <summary>
    /// Static texture holder - textures must be loaded in StaticConstructorOnStartup
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MusicPlayerTextures
    {
        public static readonly Texture2D MusicIconOn;
        public static readonly Texture2D MusicIconOff;
        
        static MusicPlayerTextures()
        {
            // Load custom icons
            MusicIconOn = ContentFinder<Texture2D>.Get("UI/MusicPlayerIcon", false);
            MusicIconOff = ContentFinder<Texture2D>.Get("UI/MusicPlayerIconOff", false);
            
            // Fallbacks
            if (MusicIconOn == null)
            {
                MusicIconOn = ContentFinder<Texture2D>.Get("UI/Buttons/Dev/Add", false) 
                    ?? BaseContent.BadTex;
            }
            if (MusicIconOff == null)
            {
                MusicIconOff = MusicIconOn;
            }
        }
    }
    
    /// <summary>
    /// Add a toggle button to PlaySettings (the row of small icons at bottom-right).
    /// </summary>
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    public static class Patch_PlaySettings_DoPlaySettingsGlobalControls
    {
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView) return;
            if (row == null) return;
            if (MusicPlayerMod.Settings == null) return;
            
            bool showPlayer = !MusicPlayerMod.Settings.HidePlayer;
            Texture2D icon = showPlayer ? MusicPlayerTextures.MusicIconOn : MusicPlayerTextures.MusicIconOff;
            string tooltip = showPlayer ? "RenHoek_MusicPlayer_HidePlayer".Translate() : "RenHoek_MusicPlayer_ShowPlayer".Translate();
            
            if (row.ButtonIcon(icon, tooltip))
            {
                MusicPlayerMod.Settings.HidePlayer = !MusicPlayerMod.Settings.HidePlayer;
            }
        }
    }
    
    /// <summary>
    /// Draw the music player widget AFTER main tabs, so it renders on top of PlaySettings icons.
    /// </summary>
    [HarmonyPatch(typeof(MapInterface), "MapInterfaceOnGUI_AfterMainTabs")]
    public static class Patch_MapInterface_DrawWidgetOnTop
    {
        public static void Postfix()
        {
            // Draw widget on top of other UI elements
            MusicPlayerWidget.DrawWidget();
        }
    }
}
