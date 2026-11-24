using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// The campaigns whose spawn tables the world files filter by: the game's
    /// <c>SlugcatStats.Timeline</c> values. A campaign filter such as <c>(White,Red)</c> on a world-file
    /// line names these. The desktop's campaign setting is one of them or <see cref="AllOutskirtsCreatures"/>.
    /// </summary>
    public sealed class Campaign
    {
        /// <summary>Which game content a campaign comes with.</summary>
        public enum Source
        {
            Base,
            Downpour,
            Watcher
        }

        /// <summary>The setting that merges every campaign's table (matches the UI's settings value).</summary>
        public const string AllOutskirtsCreatures = "All Outskirts creatures";

        private Campaign(string timeline, Source source, int foodToHibernate)
        {
            Timeline = timeline;
            From = source;
            FoodToHibernate = foodToHibernate;
        }

        /// <summary>The <c>SlugcatStats.Timeline</c> value, as written in world-file filters.</summary>
        public string Timeline { get; }

        public Source From { get; }

        /// <summary>
        /// <c>SlugcatStats.SlugcatFoodMeter(...).y</c> for the campaign's slugcat. The Outskirts uses it
        /// for one swarm room's batfly count (<c>FliesWorldAI</c> constructor).
        /// </summary>
        public int FoodToHibernate { get; }

        public override string ToString() => Timeline;

        // SlugcatStats.Timeline, in story order; food meters from SlugcatStats.SlugcatFoodMeter.
        public static readonly Campaign White = new Campaign("White", Source.Base, 4);
        public static readonly Campaign Yellow = new Campaign("Yellow", Source.Base, 3);
        public static readonly Campaign Red = new Campaign("Red", Source.Base, 6);
        public static readonly Campaign Gourmand = new Campaign("Gourmand", Source.Downpour, 7);
        public static readonly Campaign Artificer = new Campaign("Artificer", Source.Downpour, 6);
        public static readonly Campaign Rivulet = new Campaign("Rivulet", Source.Downpour, 5);
        public static readonly Campaign Spear = new Campaign("Spear", Source.Downpour, 5);
        public static readonly Campaign Saint = new Campaign("Saint", Source.Downpour, 4);
        /// <summary>Sofanthiel; the game's timeline value is "Inv".</summary>
        public static readonly Campaign Inv = new Campaign("Inv", Source.Downpour, 12);
        public static readonly Campaign Watcher = new Campaign("Watcher", Source.Watcher, 4);

        /// <summary>Every campaign that can filter a world-file line.</summary>
        public static IReadOnlyList<Campaign> All { get; } = new[]
        {
            White, Yellow, Red, Gourmand, Artificer, Rivulet, Spear, Saint, Inv, Watcher
        };

        /// <summary>
        /// True when the campaign's content is present: base campaigns always, Downpour's and The Watcher's
        /// only with that DLC enabled. A campaign from a missing or switched-off DLC has no spawn table
        /// on the desktop (docs/VISION.md, "Installed DLC").
        /// </summary>
        public bool IsAvailableWith(StaticWorld.Dlc dlc)
        {
            switch (From)
            {
                case Source.Downpour: return dlc.Downpour;
                case Source.Watcher: return dlc.Watcher;
                default: return true;
            }
        }

        /// <summary>The campaigns available with <paramref name="dlc"/> enabled, in story order.</summary>
        public static IReadOnlyList<Campaign> AvailableWith(StaticWorld.Dlc dlc)
        {
            var available = new List<Campaign>(All.Count);
            foreach (var c in All)
                if (c.IsAvailableWith(dlc)) available.Add(c);
            return available;
        }

        /// <summary>
        /// The campaign setting to use with <paramref name="dlc"/> enabled: <paramref name="setting"/>,
        /// unless it names a campaign whose DLC is missing or switched off, which falls back to
        /// <see cref="AllOutskirtsCreatures"/> (a saved choice can outlive its DLC switch).
        /// </summary>
        public static string Resolve(string setting, StaticWorld.Dlc dlc)
        {
            var campaign = IsAll(setting) ? null : Find(setting);
            return campaign != null && !campaign.IsAvailableWith(dlc) ? AllOutskirtsCreatures : setting;
        }

        /// <summary>The campaign with timeline value <paramref name="timeline"/> (case-sensitive, as the game compares), or null.</summary>
        public static Campaign Find(string timeline)
        {
            foreach (var c in All)
                if (c.Timeline == timeline) return c;
            return null;
        }

        /// <summary>
        /// The timeline an old numeric filter entry means (<c>BackwardsCompatibilityRemix.ParsePlayerNumber</c>
        /// then <c>SlugcatStats.SlugcatToTimeline</c>): 0 White, 1 Yellow, 2 Red, 3 (Night) White.
        /// Other numbers name modded slugcats, which the desktop doesn't have.
        /// </summary>
        internal static Campaign FromPlayerNumber(int number)
        {
            switch (number)
            {
                case 0: return White;
                case 1: return Yellow;
                case 2: return Red;
                case 3: return White;
                default: return null;
            }
        }

        internal static bool IsAll(string setting) =>
            string.IsNullOrWhiteSpace(setting) || string.Equals(setting, AllOutskirtsCreatures, StringComparison.Ordinal);
    }
}
