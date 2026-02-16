using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace RenHoek.MusicPlayer
{
    public class SongInfo
    {
        public SongDef Def { get; private set; }
        public string Artist { get; private set; }
        public string Title { get; private set; }
        public string DisplayName { get; private set; }
        public bool IsVanilla { get; private set; }
        public bool IsEntrySong { get; private set; }
        public bool IsOurMod { get; private set; }
        public int CategorySortOrder { get; private set; }

        // Precomputed lowercase search key - avoids ToLower() allocations during search
        public string SearchKey { get; private set; }

        // Artists whose names should NOT be split by PascalCase
        // e.g. "FamilyJules" stays as "FamilyJules", not "Family Jules"
        private static readonly Dictionary<string, string> ArtistOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"FamilyJules", "FamilyJules"},
            {"GuitarSVD", "GuitarSVD"},
        };

        /// <summary>
        /// Constructor - takes a SongDef and extracts all metadata
        /// </summary>
        public SongInfo(SongDef def)
        {
            Def = def;
            ParseSongName();
            DetermineCategory();
            // Build search key once - avoids ToLower() allocations during filtering
            SearchKey = $"{DisplayName} {Title} {Artist} {Category}".ToLowerInvariant();
        }

        /// <summary>
        /// Parses the song filename to extract Artist and Title
        /// Expected format: "Artist.SongTitle" (e.g. "FamilyJules.BuryTheLight")
        /// Handles PascalCase splitting and underscore replacement
        /// </summary>
        private void ParseSongName()
        {
            // Handle null/empty - set defaults
            if (Def == null || string.IsNullOrEmpty(Def.clipPath))
            {
                Artist = "Unknown";
                Title = "Unknown";
                DisplayName = "Unknown Track";
                IsVanilla = false;
                IsEntrySong = false;
                IsOurMod = false;
                return;
            }

            // Check if it's vanilla RimWorld music (Songs/ folder, not Music/)
            IsVanilla = Def.clipPath.StartsWith("Songs/");
            IsEntrySong = Def.defName == "EntrySong";

            // Check if it's from our mod (Music/ folder structure)
            IsOurMod = Def.clipPath.StartsWith("Music/");

            // Normalize path separators and extract filename
            string path = Def.clipPath.Replace('\\', '/');
            string filename = path;

            int lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < path.Length - 1)
                filename = path.Substring(lastSlash + 1);

            // Store original for underscore handling in fallback case
            string originalFilename = filename;

            // Remove file extensions (.ogg, .wav, .mp3)
            foreach (var ext in new[] { ".ogg", ".wav", ".mp3" })
            {
                if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    filename = filename.Substring(0, filename.Length - ext.Length);
                if (originalFilename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    originalFilename = originalFilename.Substring(0, originalFilename.Length - ext.Length);
            }

            // Try to parse "Artist.Title" format
            // Find the first dot - separator between artist and title
            int dotIndex = filename.IndexOf('.');
            if (dotIndex > 0 && dotIndex < filename.Length - 1)
            {
                // Split at the dot: "FamilyJules.BuryTheLight" -> "FamilyJules", "BuryTheLight"
                string rawArtist = filename.Substring(0, dotIndex);
                string rawTitle = filename.Substring(dotIndex + 1);

                // Check if artist should stay unsplit (e.g. "FamilyJules" not "Family Jules")
                if (ArtistOverrides.TryGetValue(rawArtist, out string artistName))
                    Artist = artistName;
                else
                    // Otherwise split PascalCase: "BeyondTheGuitar" -> "Beyond The Guitar"
                    Artist = SplitPascalCase(rawArtist.Replace("_", " ").Replace(" ", ""));

                // Always split title: "BuryTheLight" -> "Bury The Light"
                Title = SplitPascalCase(rawTitle.Replace("_", " ").Replace(" ", ""));
                DisplayName = $"{Title} - {Artist}";
            }
            else
            {
                // Fallback: no dot in filename, use whole thing as title
                Title = originalFilename.Replace("_", " ");
                if (!Title.Contains(" "))
                    Title = SplitPascalCase(Title);
                Artist = "Unknown";
                DisplayName = Title;
            }
        }

        /// <summary>
        /// Splits PascalCase into separate words
        /// "BuryTheLight" -> "Bury The Light"
        /// "FF7BattleTheme" -> "FF7 Battle Theme" (handles digits)
        /// </summary>
        private static string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var result = new StringBuilder();

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                // Add space before uppercase if previous was lowercase OR next is lowercase
                // This handles: "BuryThe" -> "Bury The" and "XMLParser" -> "XML Parser"
                if (i > 0 && char.IsUpper(c))
                {
                    bool prevIsLower = char.IsLower(input[i - 1]);
                    bool nextIsLower = (i + 1 < input.Length) && char.IsLower(input[i + 1]);

                    if (prevIsLower || nextIsLower)
                        result.Append(' ');
                }

                // Add space when transitioning from digit to uppercase letter
                // This handles: "FF7Battle" -> "FF7 Battle"
                if (i > 0)
                {
                    bool prevIsDigit = char.IsDigit(input[i - 1]);
                    bool currentIsLetter = char.IsLetter(c);

                    if (prevIsDigit && currentIsLetter && char.IsUpper(c) && result.Length > 0 && result[result.Length - 1] != ' ')
                        result.Append(' ');
                }

                result.Append(c);
            }

            return result.ToString();
        }

        /// <summary>
        /// Determines song category based on folder path or SongDef properties
        /// Priority: folder path > Def.tense > Def.allowedTimeOfDay > Def.allowedSeasons
        /// CategorySortOrder: 0=Combat, 1=Day, 2=Night, 3=Winter, 5=Multi
        /// </summary>
        private void DetermineCategory()
        {
            if (string.IsNullOrEmpty(Def?.clipPath))
            {
                CategorySortOrder = 5; // Multi (last)
                return;
            }

            string path = Def.clipPath.ToLower();

            // Check folder path first - this is the primary categorization
            if (path.Contains("/combat") || path.Contains("\\combat"))
            {
                CategorySortOrder = 0; // Combat first
                return;
            }
            if (path.Contains("/day") || path.Contains("\\day"))
            {
                CategorySortOrder = 1; // Day
                return;
            }
            if (path.Contains("/night") || path.Contains("\\night"))
            {
                CategorySortOrder = 2; // Night
                return;
            }
            if (path.Contains("/winter") || path.Contains("\\winter"))
            {
                CategorySortOrder = 3; // Winter
                return;
            }

            // For songs not in our folder structure, check SongDef properties
            if (Def.tense)
            {
                CategorySortOrder = 0; // Combat
                return;
            }

            if (Def.allowedTimeOfDay == TimeOfDay.Day)
            {
                CategorySortOrder = 1; // Day
                return;
            }
            if (Def.allowedTimeOfDay == TimeOfDay.Night)
            {
                CategorySortOrder = 2; // Night
                return;
            }

            if (Def.allowedSeasons != null && Def.allowedSeasons.Count == 1 && Def.allowedSeasons.Contains(Season.Winter))
            {
                CategorySortOrder = 3; // Winter only
                return;
            }

            // Everything else goes to Multi (plays anytime, last in list)
            CategorySortOrder = 5;
        }

        /// <summary>
        /// Returns human-readable category name based on CategorySortOrder
        /// </summary>
        public string Category
        {
            get
            {
                switch (CategorySortOrder)
                {
                    case 0: return "Combat";
                    case 1: return "Day";
                    case 2: return "Night";
                    case 3: return "Winter";
                    default: return "Multi"; // Anytime/uncategorized
                }
            }
        }

        public override string ToString() => DisplayName;
    }
}