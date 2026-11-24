using System.Collections.Generic;
using RWDE.Sim;

namespace RWDE.UI.Windows
{
    /// <summary>One creature the settings window lists, with the DLC it needs.</summary>
    public sealed class RosterSpecies
    {
        public RosterSpecies(string internalType, string name, string family, Campaign.Source source)
        {
            InternalType = internalType;
            Name = name;
            Family = family;
            Source = source;
        }

        /// <summary>The game's name for the creature (<c>CreatureTemplate.Type</c>), as stored in Settings.</summary>
        public string InternalType { get; }

        public string Name { get; }
        public string Family { get; }
        public Campaign.Source Source { get; }

        public override string ToString() => Name;
    }

    /// <summary>The Outskirts roster from docs/VISION.md, in its order.</summary>
    public static class OutskirtsRoster
    {
        const Campaign.Source Base = Campaign.Source.Base;
        const Campaign.Source Downpour = Campaign.Source.Downpour;

        public static IReadOnlyList<RosterSpecies> All { get; } = new[]
        {
            new RosterSpecies("GreenLizard", "Green Lizard", "Lizards", Base),
            new RosterSpecies("PinkLizard", "Pink Lizard", "Lizards", Base),
            new RosterSpecies("BlueLizard", "Blue Lizard", "Lizards", Base),
            new RosterSpecies("YellowLizard", "Yellow Lizard", "Lizards", Base),
            new RosterSpecies("WhiteLizard", "White Lizard", "Lizards", Base),
            new RosterSpecies("CyanLizard", "Cyan Lizard", "Lizards", Base),
            new RosterSpecies("RedLizard", "Red Lizard", "Lizards", Base),
            new RosterSpecies("SpitLizard", "Caramel Lizard", "Lizards", Downpour),
            new RosterSpecies("ZoopLizard", "Strawberry Lizard", "Lizards", Downpour),
            new RosterSpecies("Salamander", "Salamander", "Lizards", Downpour),
            new RosterSpecies("EelLizard", "Eel Lizard", "Lizards", Downpour),
            new RosterSpecies("Fly", "Batfly", "Batflies", Base),
            new RosterSpecies("CicadaA", "Squidcada (white)", "Squidcadas", Base),
            new RosterSpecies("CicadaB", "Squidcada (dark)", "Squidcadas", Base),
            new RosterSpecies("SmallCentipede", "Infant Centipede", "Centipedes", Base),
            new RosterSpecies("Centipede", "Centipede", "Centipedes", Base),
            new RosterSpecies("RedCentipede", "Red Centipede", "Centipedes", Downpour),
            new RosterSpecies("Centiwing", "Centiwing", "Centipedes", Downpour),
            new RosterSpecies("EggBug", "Eggbug", "Small bugs", Base),
            new RosterSpecies("DropBug", "Dropwig", "Small bugs", Base),
            new RosterSpecies("BigSpider", "Big Spider", "Spiders", Base),
            new RosterSpecies("SpitterSpider", "Spitter Spider", "Spiders", Base),
            new RosterSpecies("BigNeedleWorm", "Noodlefly", "Noodleflies", Base),
            new RosterSpecies("SmallNeedleWorm", "Infant Noodlefly", "Noodleflies", Base),
            new RosterSpecies("Scavenger", "Scavenger", "Scavengers", Base),
            new RosterSpecies("Vulture", "Vulture", "Vultures", Base),
            new RosterSpecies("KingVulture", "King Vulture", "Vultures", Base),
            new RosterSpecies("MirosVulture", "Miros Vulture", "Vultures", Downpour),
            new RosterSpecies("PoleMimic", "Pole Plant", "Plants", Base),
            new RosterSpecies("TentaclePlant", "Monster Kelp", "Plants", Base),
            new RosterSpecies("Overseer", "Overseer", "Overseers", Base),
            new RosterSpecies("Hazer", "Hazer", "Other", Base),
            new RosterSpecies("SlugNPC", "Wild slugcat", "Slugcats", Downpour),
        };
    }
}
