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

        private static readonly Dictionary<string, (string Title, string Artist)> KnownSongs = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            // === COMBAT ===
            {"FamilyJules.AttackOfTheKillerQueen", ("Attack Of The Killer Queen", "FamilyJules")},
            {"FamilyJules.Azdaha", ("Azdaha", "FamilyJules")},
            {"FamilyJules.Ballistic", ("Ballistic", "FamilyJules")},
            {"FamilyJules.BigShot", ("Big Shot", "FamilyJules")},
            {"FamilyJules.Blammed", ("Blammed", "FamilyJules")},
            {"FamilyJules.BoomerKuwanger", ("Boomer Kuwanger", "FamilyJules")},
            {"FamilyJules.BuryTheLight", ("Bury The Light", "FamilyJules")},
            {"FamilyJules.ChampionCynthia", ("Champion Cynthia", "FamilyJules")},
            {"FamilyJules.ChaseGigaBowser", ("Chase Giga Bowser", "FamilyJules")},
            {"FamilyJules.ColossalChaos", ("Colossal Chaos", "FamilyJules")},
            {"FamilyJules.CorridorsOfTime", ("Corridors Of Time", "FamilyJules")},
            {"FamilyJules.CronCastle", ("Cron Castle", "FamilyJules")},
            {"FamilyJules.DadBattle", ("Dad Battle", "FamilyJules")},
            {"FamilyJules.DancingMad", ("Dancing Mad", "FamilyJules")},
            {"FamilyJules.DialgasFight", ("Dialga's Fight", "FamilyJules")},
            {"FamilyJules.DiggyDiggyHole", ("Diggy Diggy Hole", "FamilyJules")},
            {"FamilyJules.E1M1AtDoomsGate", ("E1M1 At Doom's Gate", "FamilyJules")},
            {"FamilyJules.Expurgation", ("Expurgation", "FamilyJules")},
            {"FamilyJules.FuneralOfFlowers", ("Funeral Of Flowers", "FamilyJules")},
            {"FamilyJules.GodOfTheDead", ("God Of The Dead", "FamilyJules")},
            {"FamilyJules.GodShatteringStar", ("God Shattering Star", "FamilyJules")},
            {"FamilyJules.Goemon2", ("Goemon 2", "FamilyJules")},
            {"FamilyJules.Grandma", ("Grandma", "FamilyJules")},
            {"FamilyJules.Guns", ("Guns", "FamilyJules")},
            {"FamilyJules.HaloTheme", ("Halo Theme", "FamilyJules")},
            {"FamilyJules.IndomitableWill", ("Indomitable Will", "FamilyJules")},
            {"FamilyJules.ItHasToBeThisWay", ("It Has To Be This Way", "FamilyJules")},
            {"FamilyJules.KnockYouDown", ("Knock You Down", "FamilyJules")},
            {"FamilyJules.LudwigTheAccursed", ("Ludwig The Accursed", "FamilyJules")},
            {"FamilyJules.MasterOfPuppets", ("Master Of Puppets", "FamilyJules")},
            {"FamilyJules.Megalovania", ("Megalovania", "FamilyJules")},
            {"FamilyJules.MooMooMeadows", ("Moo Moo Meadows", "FamilyJules")},
            {"FamilyJules.Negotiations", ("Negotiations", "FamilyJules")},
            {"FamilyJules.OneWingedAngel", ("One Winged Angel", "FamilyJules")},
            {"FamilyJules.PennyBattle", ("Penny Battle", "FamilyJules")},
            {"FamilyJules.Pico", ("Pico", "FamilyJules")},
            {"FamilyJules.RaiseUpYourBat", ("Raise Up Your Bat", "FamilyJules")},
            {"FamilyJules.RipAndTear", ("Rip And Tear", "FamilyJules")},
            {"FamilyJules.RoarOfDedede", ("Roar Of Dedede", "FamilyJules")},
            {"FamilyJules.RoarOfTheSpark", ("Roar Of The Spark", "FamilyJules")},
            {"FamilyJules.RudeBuster", ("Rude Buster", "FamilyJules")},
            {"FamilyJules.SadaTuroBattle", ("Sada Turo Battle", "FamilyJules")},
            {"FamilyJules.SharksOnMars", ("Sharks On Mars", "FamilyJules")},
            {"FamilyJules.TeamStarBattle", ("Team Star Battle", "FamilyJules")},
            {"FamilyJules.TearsOfTheKingdom", ("Tears Of The Kingdom", "FamilyJules")},
            {"FamilyJules.TwilightOfTheGods", ("Twilight Of The Gods", "FamilyJules")},
            {"FamilyJules.Ugh", ("Ugh", "FamilyJules")},
            {"FamilyJules.Uroboros", ("Uroboros", "FamilyJules")},
            {"FamilyJules.YouWereWrong", ("You Were Wrong", "FamilyJules")},
            {"FamilyJules.YouWillKnowOurNames", ("You Will Know Our Names", "FamilyJules")},
            {"FamilyJules.Zavodila", ("Zavodila", "FamilyJules")},

            // === DAY ===
            {"Acoustic.ACybersWorld", ("A Cyber's World", "Kain White")},
            {"BeyondTheGuitar.HookedOnAFeeling", ("Hooked On A Feeling", "Beyond The Guitar")},
            {"BeyondTheGuitar.MayItBe", ("May It Be", "Beyond The Guitar")},
            {"BeyondTheGuitar.OctopathTraveler2", ("Octopath Traveler 2", "Beyond The Guitar")},
            {"BeyondTheGuitar.SupermanTheme", ("Superman Theme", "Beyond The Guitar")},
            {"Classical.FrogsTheme", ("Frog's Theme", "John Oeth Guitar")},
            {"FamilyJules.BeneathTheMask", ("Beneath The Mask", "FamilyJules")},
            {"FamilyJules.EnvelopedInKindness", ("Enveloped In Kindness", "FamilyJules")},
            {"FamilyJules.RoughTrail", ("Rough Trail", "FamilyJules")},
            {"FamilyJules.SightOfSpira", ("Sight Of Spira", "FamilyJules")},
            {"FamilyJules.StaleCupcakes", ("Stale Cupcakes", "FamilyJules")},
            {"FamilyJules.StardewOverture", ("Stardew Overture", "FamilyJules")},
            {"FamilyJules.UndertaleAcoustic", ("Undertale Acoustic", "FamilyJules")},
            {"FamilyJules.ZazenTown", ("Zazen Town", "FamilyJules")},
            {"Osamuraisan.ChronoBattleMedley", ("Chrono Battle Medley", "Osamuraisan")},
            {"Osamuraisan.ChronoTriggerTheme", ("Chrono Trigger Theme", "Osamuraisan")},
            {"Osamuraisan.CorridorsOfTime", ("Corridors Of Time", "Osamuraisan")},
            {"Osamuraisan.DragonQuest3Medley", ("Dragon Quest 3 Medley", "Osamuraisan")},
            {"Osamuraisan.Emotions", ("Emotions", "Osamuraisan")},
            {"Osamuraisan.EternalWind", ("Eternal Wind", "Osamuraisan")},
            {"Osamuraisan.GausTheme", ("Gau's Theme", "Osamuraisan")},
            {"Osamuraisan.Home", ("Home", "Osamuraisan")},
            {"Osamuraisan.KidsRunThroughTheCity", ("Kids Run Through The City", "Osamuraisan")},
            {"Osamuraisan.OnOurWay", ("On Our Way", "Osamuraisan")},
            {"Osamuraisan.PeacefulDays", ("Peaceful Days", "Osamuraisan")},
            {"Osamuraisan.SongOfTheBaron", ("Song Of The Baron", "Osamuraisan")},
            {"Osamuraisan.TifasTheme", ("Tifa's Theme", "Osamuraisan")},
            {"Osamuraisan.TownWithOceanView", ("Town With Ocean View", "Osamuraisan")},
            {"Osamuraisan.TraverseTown", ("Traverse Town", "Osamuraisan")},
            {"Osamuraisan.WindScene", ("Wind Scene", "Osamuraisan")},
            {"SuperGuitarBros.ACNewHorizons6AM", ("AC New Horizons 6AM", "Super Guitar Bros")},
            {"SuperGuitarBros.DelfinoPlaza", ("Delfino Plaza", "Super Guitar Bros")},
            {"SuperGuitarBros.DragonRoostIsland", ("Dragon Roost Island", "Super Guitar Bros")},
            {"SuperGuitarBros.EarthboundTownMedley", ("Earthbound Town Medley", "Super Guitar Bros")},
            {"SuperGuitarBros.FairyFountain", ("Fairy Fountain", "Super Guitar Bros")},
            {"SuperGuitarBros.GangplankGalleon", ("Gangplank Galleon", "Super Guitar Bros")},
            {"SuperGuitarBros.GerudoValleyVibes", ("Gerudo Valley Vibes", "Super Guitar Bros")},
            {"SuperGuitarBros.InsideCastleWalls", ("Inside Castle Walls", "Super Guitar Bros")},
            {"SuperGuitarBros.KatamariFugue", ("Katamari Fugue", "Super Guitar Bros")},
            {"SuperGuitarBros.LonLonRanch", ("Lon Lon Ranch", "Super Guitar Bros")},
            {"SuperGuitarBros.LostKingdom", ("Lost Kingdom", "Super Guitar Bros")},
            {"SuperGuitarBros.MarioWorldAthletic", ("Mario World Athletic", "Super Guitar Bros")},
            {"SuperGuitarBros.MiiChannel", ("Mii Channel", "Super Guitar Bros")},
            {"SuperGuitarBros.MillennialFair", ("Millennial Fair", "Super Guitar Bros")},
            {"SuperGuitarBros.SightOfSpira", ("Sight Of Spira", "Super Guitar Bros")},
            {"SuperGuitarBros.StudiopolisZone", ("Studiopolis Zone", "Super Guitar Bros")},
            {"SuperGuitarBros.SuperMarioLand", ("Super Mario Land", "Super Guitar Bros")},
            {"SuperGuitarBros.VamoAllaFlamenco", ("Vamo Alla Flamenco", "Super Guitar Bros")},
            {"SuperGuitarBros.WaveRace64Title", ("Wave Race 64 Title", "Super Guitar Bros")},
            {"SuperGuitarBros.WiiShopTheme", ("Wii Shop Theme", "Super Guitar Bros")},
            {"SuperGuitarBros.WintersWhiteSnowman", ("Winter's White Snowman", "Super Guitar Bros")},

            // === NIGHT ===
            {"Acoustic.WorldRevolving", ("World Revolving", "Kain White")},
            {"BeyondTheGuitar.AcrossTheStars", ("Across The Stars", "Beyond The Guitar")},
            {"BeyondTheGuitar.AurielsAscension", ("Auriel's Ascension", "Beyond The Guitar")},
            {"BeyondTheGuitar.AvengersEndgame", ("Avengers Endgame", "Beyond The Guitar")},
            {"BeyondTheGuitar.DavyJonesTheme", ("Davy Jones Theme", "Beyond The Guitar")},
            {"BeyondTheGuitar.DearlyBeloved", ("Dearly Beloved", "Beyond The Guitar")},
            {"BeyondTheGuitar.Hurt", ("Hurt", "Beyond The Guitar")},
            {"BeyondTheGuitar.LastOfUs", ("Last Of Us", "Beyond The Guitar")},
            {"BeyondTheGuitar.LastOfUsHBO", ("Last Of Us HBO", "Beyond The Guitar")},
            {"BeyondTheGuitar.PunisherFranksChoice", ("Punisher Frank's Choice", "Beyond The Guitar")},
            {"BeyondTheGuitar.RomanticFlight", ("Romantic Flight", "Beyond The Guitar")},
            {"BeyondTheGuitar.ShrekFairytale", ("Shrek Fairytale", "Beyond The Guitar")},
            {"BeyondTheGuitar.TakeOnMe", ("Take On Me", "Beyond The Guitar")},
            {"BeyondTheGuitar.TifasThemeRemake", ("Tifa's Theme Remake", "Beyond The Guitar")},
            {"BeyondTheGuitar.WalkingInTheAir", ("Walking In The Air", "Beyond The Guitar")},
            {"BeyondTheGuitar.WhereIsMyMind", ("Where Is My Mind", "Beyond The Guitar")},
            {"Classical.AtBottomOfNight", ("At The Bottom Of Night", "John Oeth Guitar")},
            {"FamilyJules.1AM", ("1 AM", "FamilyJules")},
            {"FamilyJules.FisFarewell", ("Fi's Farewell", "FamilyJules")},
            {"FamilyJules.ShopClosing", ("Shop Closing", "FamilyJules")},
            {"FamilyJules.Sweden", ("Sweden", "FamilyJules")},
            {"JosiahEverhart.Asgore", ("Asgore", "Josiah Everhart")},
            {"Osamuraisan.EternalWindFF3", ("Eternal Wind FF3", "Osamuraisan")},
            {"Osamuraisan.FishermansHorizon", ("Fisherman's Horizon", "Osamuraisan")},
            {"Osamuraisan.MerryGoRoundOfLife", ("Merry Go Round Of Life", "Osamuraisan")},
            {"Osamuraisan.ProvincialTown", ("Provincial Town", "Osamuraisan")},
            {"Osamuraisan.VocaloidMedley", ("Vocaloid Medley", "Osamuraisan")},
            {"SamGriffin.AstralObservatory", ("Astral Observatory", "Sam Griffin")},
            {"SamGriffin.LowerBrinstar", ("Lower Brinstar", "Sam Griffin")},
            {"SamGriffin.MiphasTheme", ("Mipha's Theme", "Sam Griffin")},
            {"SamGriffin.StarcraftPretty", ("Starcraft Pretty", "Sam Griffin")},
            {"SuperGuitarBros.BeneathTheMask", ("Beneath The Mask", "Super Guitar Bros")},
            {"SuperGuitarBros.BlueFields", ("Blue Fields", "Super Guitar Bros")},
            {"SuperGuitarBros.BrinkOfTime", ("Brink Of Time", "Super Guitar Bros")},
            {"SuperGuitarBros.CalmBeforeTheStorm", ("Calm Before The Storm", "Super Guitar Bros")},
            {"SuperGuitarBros.Castlevania2Medley", ("Castlevania 2 Medley", "Super Guitar Bros")},
            {"SuperGuitarBros.Castlevania3Medley", ("Castlevania 3 Medley", "Super Guitar Bros")},
            {"SuperGuitarBros.CastlevaniaOriginal", ("Castlevania Original", "Super Guitar Bros")},
            {"SuperGuitarBros.ColorOfSummerSky", ("Color Of Summer Sky", "Super Guitar Bros")},
            {"SuperGuitarBros.CometObservatory", ("Comet Observatory", "Super Guitar Bros")},
            {"SuperGuitarBros.DireDireDocks", ("Dire Dire Docks", "Super Guitar Bros")},
            {"SuperGuitarBros.EccoTheDolphin", ("Ecco The Dolphin", "Super Guitar Bros")},
            {"SuperGuitarBros.FallenDown", ("Fallen Down", "Super Guitar Bros")},
            {"SuperGuitarBros.GoosebumpsTheme", ("Goosebumps Theme", "Super Guitar Bros")},
            {"SuperGuitarBros.HauntedGraveyard", ("Haunted Graveyard", "Super Guitar Bros")},
            {"SuperGuitarBros.Peaches", ("Peaches", "Super Guitar Bros")},
            {"SuperGuitarBros.PhantomAndARose", ("Phantom And A Rose", "Super Guitar Bros")},
            {"SuperGuitarBros.RainbowRoad", ("Rainbow Road", "Super Guitar Bros")},
            {"SuperGuitarBros.SimonsTheme", ("Simon's Theme", "Super Guitar Bros")},
            {"SuperGuitarBros.StarfoxMedley", ("Starfox Medley", "Super Guitar Bros")},
            {"SuperGuitarBros.XMenTheme", ("X-Men Theme", "Super Guitar Bros")},

            // === WINTER ===
            {"BeyondTheGuitar.BlackMythWukong", ("Black Myth Wukong", "Beyond The Guitar")},
            {"BeyondTheGuitar.LokiTheme", ("Loki Theme", "Beyond The Guitar")},
            {"BeyondTheGuitar.PinkPanther", ("Pink Panther", "Beyond The Guitar")},
            {"BeyondTheGuitar.SadnessAndSorrow", ("Sadness And Sorrow", "Beyond The Guitar")},
            {"BeyondTheGuitar.SpiritHomeland", ("Spirit Homeland", "Beyond The Guitar")},
            {"BeyondTheGuitar.TheLastGoodbye", ("The Last Goodbye", "Beyond The Guitar")},
            {"Classical.MadWorld", ("Mad World", "John Oeth Guitar")},
            {"Classical.OdeToHeroes", ("Ode To Heroes", "Josh Guitarofolo")},
            {"EddieVanDerMeer.DangoDaikazoku", ("Dango Daikazoku", "Eddie Van Der Meer")},
            {"EddieVanDerMeer.OnePunchManSad", ("One Punch Man Sad", "Eddie Van Der Meer")},
            {"EddieVanDerMeer.Unravel", ("Unravel", "Eddie Van Der Meer")},
            {"FamilyJules.GerudoValley", ("Gerudo Valley", "FamilyJules")},
            {"FamilyJules.StickerbushSymphony", ("Stickerbush Symphony", "FamilyJules")},
            {"GuitarSVD.DawnWineryNight", ("Dawn Winery Night", "GuitarSVD")},
            {"GuitarSVD.Dragonsong", ("Dragonsong", "GuitarSVD")},
            {"GuitarSVD.KainesTheme", ("Kaine's Theme", "GuitarSVD")},
            {"GuitarSVD.LudwigHolyBlade", ("Ludwig Holy Blade", "GuitarSVD")},
            {"GuitarSVD.WeightOfTheWorld", ("Weight Of The World", "GuitarSVD")},
            {"GuitarSVD.YonahAshesOfDreams", ("Yonah Ashes Of Dreams", "GuitarSVD")},
            {"JonasLefvert.Diablo2RogueEncampment", ("Diablo 2 Rogue Encampment", "Jonas Lefvert")},
            {"JonasLefvert.FirelinkShrine", ("Firelink Shrine", "Jonas Lefvert")},
            {"JonasLefvert.MorrowindTheme", ("Morrowind Theme", "Jonas Lefvert")},
            {"JoshGuitarofolo.BeneathTheMask", ("Beneath The Mask", "Josh Guitarofolo")},
            {"JoshGuitarofolo.CityOfTears", ("City Of Tears", "Josh Guitarofolo")},
            {"JoshGuitarofolo.Dirtmouth", ("Dirtmouth", "Josh Guitarofolo")},
            {"JoshGuitarofolo.RestingGrounds", ("Resting Grounds", "Josh Guitarofolo")},
            {"JoshGuitarofolo.WhitePalace", ("White Palace", "Josh Guitarofolo")},
            {"Osamuraisan.ClashOnBigBridge", ("Clash On Big Bridge", "Osamuraisan")},
            {"Osamuraisan.Libertango", ("Libertango", "Osamuraisan")},
            {"Osamuraisan.SchalasTheme", ("Schala's Theme", "Osamuraisan")},
            {"Osamuraisan.VamoAllaFlamenco", ("Vamo Alla Flamenco", "Osamuraisan")},
            {"Osamuraisan.WindCallsShevat", ("Wind Calls Shevat", "Osamuraisan")},
            {"SuperGuitarBros.AquaticAmbience", ("Aquatic Ambience", "Super Guitar Bros")},
            {"SuperGuitarBros.BattleFourFiends", ("Battle Four Fiends", "Super Guitar Bros")},
            {"SuperGuitarBros.BowsersRoad", ("Bowser's Road", "Super Guitar Bros")},
            {"SuperGuitarBros.Contra3Medley", ("Contra 3 Medley", "Super Guitar Bros")},
            {"SuperGuitarBros.DecisiveBattle", ("Decisive Battle", "Super Guitar Bros")},
            {"SuperGuitarBros.FF7BattleTheme", ("FF7 Battle Theme", "Super Guitar Bros")},
            {"SuperGuitarBros.FreyasTheme", ("Freya's Theme", "Super Guitar Bros")},
            {"SuperGuitarBros.GameOfThrones", ("Game Of Thrones", "Super Guitar Bros")},
            {"SuperGuitarBros.GustyGardenGalaxy", ("Gusty Garden Galaxy", "Super Guitar Bros")},
            {"SuperGuitarBros.LeavingEarth", ("Leaving Earth", "Super Guitar Bros")},
            {"SuperGuitarBros.LifeInTheMines", ("Life In The Mines", "Super Guitar Bros")},
            {"SuperGuitarBros.MegalovaniaAcoustic", ("Megalovania Acoustic", "Super Guitar Bros")},
            {"SuperGuitarBros.MetroidMedley", ("Metroid Medley", "Super Guitar Bros")},
            {"SuperGuitarBros.MinecartMadness", ("Minecart Madness", "Super Guitar Bros")},
            {"SuperGuitarBros.PerfectDarkMenu", ("Perfect Dark Menu", "Super Guitar Bros")},
            {"SuperGuitarBros.SnakeMan", ("Snake Man", "Super Guitar Bros")},
            {"SuperGuitarBros.StarWolfTheme", ("Star Wolf Theme", "Super Guitar Bros")},
            {"SuperGuitarBros.Tristram", ("Tristram", "Super Guitar Bros")},
            {"SuperGuitarBros.ZeldaDungeonMedley", ("Zelda Dungeon Medley", "Super Guitar Bros")},
        };

        public SongInfo(SongDef def)
        {
            Def = def;
            ParseSongName();
            DetermineCategory();
            // Build search key once - avoids ToLower() allocations during filtering
            SearchKey = $"{DisplayName} {Title} {Artist} {Category}".ToLowerInvariant();
        }

        private void ParseSongName()
        {
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

            string path = Def.clipPath.Replace('\\', '/');
            string filename = path;
            
            int lastSlash = path.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < path.Length - 1)
            {
                filename = path.Substring(lastSlash + 1);
            }
            
            // Store original for underscore handling
            string originalFilename = filename;
            
            // Remove extensions
            foreach (var ext in new[] { ".ogg", ".wav", ".mp3" })
            {
                if (filename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    filename = filename.Substring(0, filename.Length - ext.Length);
                if (originalFilename.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    originalFilename = originalFilename.Substring(0, originalFilename.Length - ext.Length);
            }

            // Check known songs first (before any transformations)
            if (KnownSongs.TryGetValue(filename, out var songData))
            {
                Title = songData.Title;
                Artist = songData.Artist;
                DisplayName = $"{Title} - {Artist}";
                return;
            }
            
            // Also try with underscores removed
            string noUnderscores = filename.Replace("_", "");
            if (KnownSongs.TryGetValue(noUnderscores, out songData))
            {
                Title = songData.Title;
                Artist = songData.Artist;
                DisplayName = $"{Title} - {Artist}";
                return;
            }

            // Smart parsing for unknown songs
            int dotIndex = filename.IndexOf('.');
            if (dotIndex > 0 && dotIndex < filename.Length - 1)
            {
                string rawArtist = filename.Substring(0, dotIndex);
                string rawTitle = filename.Substring(dotIndex + 1);
                
                // Replace underscores with spaces, then apply PascalCase splitting
                rawArtist = rawArtist.Replace("_", " ");
                rawTitle = rawTitle.Replace("_", " ");
                
                Artist = SplitPascalCase(rawArtist.Replace(" ", ""));
                Title = SplitPascalCase(rawTitle.Replace(" ", ""));
                DisplayName = $"{Title} - {Artist}";
            }
            else
            {
                // No dot - just use filename with underscores as spaces
                Title = originalFilename.Replace("_", " ");
                // Also try PascalCase splitting
                if (!Title.Contains(" "))
                    Title = SplitPascalCase(Title);
                Artist = "Unknown";
                DisplayName = Title;
            }
        }
        
        private static string SplitPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            
            var result = new StringBuilder();
            
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                
                if (i > 0 && char.IsUpper(c))
                {
                    bool prevIsLower = char.IsLower(input[i - 1]);
                    bool nextIsLower = (i + 1 < input.Length) && char.IsLower(input[i + 1]);
                    
                    if (prevIsLower || nextIsLower)
                    {
                        result.Append(' ');
                    }
                }
                
                if (i > 0)
                {
                    bool prevIsDigit = char.IsDigit(input[i - 1]);
                    bool currentIsLetter = char.IsLetter(c);
                    
                    if (prevIsDigit && currentIsLetter && char.IsUpper(c) && result.Length > 0 && result[result.Length - 1] != ' ')
                    {
                        result.Append(' ');
                    }
                }
                
                result.Append(c);
            }
            
            return result.ToString();
        }
        
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
