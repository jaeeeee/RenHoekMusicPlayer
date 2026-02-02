using RimWorld;
using UnityEngine;
using Verse;

namespace RenHoek.MusicPlayer
{
    /// <summary>
    /// Keybinding definitions for the music player.
    /// </summary>
    [DefOf]
    public static class MusicPlayerKeyBindingDefOf
    {
        public static KeyBindingDef RenHoekMusic_PlayPause;
        public static KeyBindingDef RenHoekMusic_NextTrack;
        public static KeyBindingDef RenHoekMusic_PrevTrack;
        public static KeyBindingDef RenHoekMusic_ToggleWidget;
        public static KeyBindingDef RenHoekMusic_ToggleShuffle;
        public static KeyBindingDef RenHoekMusic_ToggleContext;

        static MusicPlayerKeyBindingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MusicPlayerKeyBindingDefOf));
        }
    }

    /// <summary>
    /// Handles keybinding input checking.
    /// </summary>
    public static class MusicPlayerKeyBindings
    {
        /// <summary>
        /// Check for keybinding presses. Call this from OnGUI.
        /// </summary>
        public static void CheckKeyBindings()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            if (Find.CurrentMap == null) return;
            
            // Don't process keybinds if a text field is focused or a dialog is open
            if (Find.WindowStack.WindowsForcePause) return;
            if (GUIUtility.keyboardControl != 0) return;

            // Play/Pause
            if (MusicPlayerKeyBindingDefOf.RenHoekMusic_PlayPause?.KeyDownEvent ?? false)
            {
                MusicPlayerController.Instance?.TogglePause();
                Event.current.Use();
            }

            // Next Track
            if (MusicPlayerKeyBindingDefOf.RenHoekMusic_NextTrack?.KeyDownEvent ?? false)
            {
                MusicPlayerController.Instance?.PlayNext();
                Event.current.Use();
            }

            // Previous Track
            if (MusicPlayerKeyBindingDefOf.RenHoekMusic_PrevTrack?.KeyDownEvent ?? false)
            {
                MusicPlayerController.Instance?.PlayPrevious();
                Event.current.Use();
            }

            // Toggle Widget Visibility
            if (MusicPlayerKeyBindingDefOf.RenHoekMusic_ToggleWidget?.KeyDownEvent ?? false)
            {
                if (MusicPlayerMod.Settings != null)
                {
                    MusicPlayerMod.Settings.HidePlayer = !MusicPlayerMod.Settings.HidePlayer;
                }
                Event.current.Use();
            }

            // Toggle Shuffle
            if (MusicPlayerKeyBindingDefOf.RenHoekMusic_ToggleShuffle?.KeyDownEvent ?? false)
            {
                if (MusicPlayerMod.Settings != null)
                {
                    MusicPlayerMod.Settings.ShuffleEnabled = !MusicPlayerMod.Settings.ShuffleEnabled;
                    Messages.Message(
                        MusicPlayerMod.Settings.ShuffleEnabled ? "Shuffle: ON" : "Shuffle: OFF",
                        MessageTypeDefOf.SilentInput,
                        false
                    );
                }
                Event.current.Use();
            }

            // Toggle Context-Aware
            if (MusicPlayerKeyBindingDefOf.RenHoekMusic_ToggleContext?.KeyDownEvent ?? false)
            {
                if (MusicPlayerMod.Settings != null)
                {
                    MusicPlayerMod.Settings.ContextAware = !MusicPlayerMod.Settings.ContextAware;
                    Messages.Message(
                        MusicPlayerMod.Settings.ContextAware ? "Context-Aware: ON" : "Context-Aware: OFF",
                        MessageTypeDefOf.SilentInput,
                        false
                    );
                }
                Event.current.Use();
            }
        }
    }
}
