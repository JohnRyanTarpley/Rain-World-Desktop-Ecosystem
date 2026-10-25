using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Per-species static data, ported from the game's <c>CreatureTemplate</c>: how much each tile
    /// accessibility and movement type costs, shortcut use, water rules, size and relationships.
    /// Templates are built by <see cref="StaticWorld"/>; every creature of a type shares one.
    /// </summary>
    /// <remarks>
    /// Left out: <c>breedParameters</c> (comes with <c>LizardBreedParams</c> in the lizard port) and
    /// the Safari-mode control labels <c>pickupAction</c>/<c>throwAction</c>/<c>jumpAction</c>, which
    /// only name player-control buttons and never affect behaviour.
    /// </remarks>
    public sealed class CreatureTemplate
    {
        /// <summary>
        /// The game's <c>CreatureTemplate.Type</c>, limited to the Outskirts roster in docs/VISION.md,
        /// their template ancestors and the player (<see cref="Slugcat"/>, which relationships refer to).
        /// The order is the game's registration order (base types, then Downpour's), which
        /// <see cref="StaticWorld"/> relies on where the game loops over every type.
        /// </summary>
        public enum Type
        {
            StandardGroundCreature,
            Slugcat,
            LizardTemplate,
            PinkLizard,
            GreenLizard,
            BlueLizard,
            YellowLizard,
            WhiteLizard,
            RedLizard,
            Salamander,
            CyanLizard,
            Fly,
            Vulture,
            CicadaA,
            CicadaB,
            TentaclePlant,
            PoleMimic,
            Centipede,
            RedCentipede,
            Centiwing,
            SmallCentipede,
            Scavenger,
            Overseer,
            VultureGrub,
            EggBug,
            BigSpider,
            SpitterSpider,
            SmallNeedleWorm,
            BigNeedleWorm,
            DropBug,
            KingVulture,
            Hazer,
            // DLCSharedEnums.CreatureTemplateType (Downpour, or The Watcher)
            MirosVulture,
            SpitLizard,
            EelLizard,
            ZoopLizard
        }

        /// <summary>Number of <see cref="Type"/> values; relationship arrays have this length.</summary>
        public static readonly int TypeCount = Enum.GetValues(typeof(Type)).Length;

        // CreatureTemplate.WaterRelationship
        public enum WaterRelationship
        {
            AirOnly,
            AirAndSurface,
            Amphibious,
            WaterOnly
        }

        /// <summary>The game's <c>Creature.DamageType</c>, indexing <see cref="damageRestistances"/>.</summary>
        public enum DamageType
        {
            Blunt,
            Stab,
            Bite,
            Water,
            Explosion,
            Electric,
            None
        }

        /// <summary>The game's <c>CreatureCommunities.CommunityID</c>.</summary>
        public enum CommunityID
        {
            None,
            All,
            Scavengers,
            Lizards,
            Cicadas,
            GarbageWorms,
            Deer,
            JetFish
        }

        /// <summary>How one creature type regards another. Mirrors <c>CreatureTemplate.Relationship</c>.</summary>
        public struct Relationship : IEquatable<Relationship>
        {
            // CreatureTemplate.Relationship.Type, in the game's order.
            public enum Type
            {
                DoesntTrack,
                Ignores,
                Eats,
                Afraid,
                StayOutOfWay,
                AgressiveRival,
                Attacks,
                Uncomfortable,
                Antagonizes,
                PlaysWith,
                SocialDependent,
                Pack
            }

            public Type type;
            public float intensity;

            public Relationship(Type type, float intensity)
            {
                this.type = type;
                this.intensity = intensity;
            }

            public bool GoForKill => (type == Type.Eats || type == Type.Attacks) && intensity > 0f;

            public Relationship Duplicate() => new Relationship(type, intensity);

            public bool Equals(Relationship other) => type == other.type && intensity == other.intensity;
            public override bool Equals(object obj) => obj is Relationship other && Equals(other);
            public override int GetHashCode() => ((int)type * 397) ^ intensity.GetHashCode();
            public static bool operator ==(Relationship a, Relationship b) => a.Equals(b);
            public static bool operator !=(Relationship a, Relationship b) => !a.Equals(b);

            public override string ToString() => type + " " + intensity;
        }

        public readonly Type type;
        public readonly string name;
        public readonly CreatureTemplate ancestor;
        /// <summary>The template whose pre-baked pathing data this one borrows when it has none of its own.</summary>
        public CreatureTemplate preBakedPathingAncestor;
        public int index;
        private int prebakedPathingIndex;
        public bool virtualCreature;
        public bool doPreBakedPathing;
        /// <summary>Cost of being on each <see cref="AItile.Accessibility"/>, indexed by it.</summary>
        public PathCost[] pathingPreferencesTiles;
        /// <summary>Cost per tile of each <see cref="MovementConnection.MovementType"/>, indexed by it.</summary>
        public PathCost[] pathingPreferencesConnections;
        public float baseDamageResistance;
        public float baseStunResistance;
        public float instantDeathDamageLimit;
        /// <summary>[damage type, 0] scales damage and [damage type, 1] scales stun; 0 means unset.</summary>
        public float[,] damageRestistances;
        /// <summary>The hardest accessibility the template is allowed on (Sand excluded), as an int.</summary>
        public int maxAccessibleTerrain;
        public bool AI;
        public bool requireAImap;
        public bool quantified;
        public float offScreenSpeed;
        public int abstractedLaziness;
        public bool canFly;
        public int grasps;
        public bool stowFoodInDen;
        public bool smallCreature;
        public float dangerousToPlayer;
        public float roamInRoomChance = 0.1f;
        public float roamBetweenRoomsChance = 0.1f;
        public bool canAutoAbstractPath = true;
        public float visualRadius;
        public float waterVision = 0.4f;
        public float throughSurfaceVision = 0.8f;
        public float movementBasedVision = 0.2f;
        public CommunityID communityID = CommunityID.All;
        public float communityInfluence = 0.5f;
        public int countsAsAKill = 2;
        public int meatPoints;
        public float lungCapacity = 520f;
        public bool quickDeath = true;
        public bool wormGrassImmune;
        public bool daddyCorruptionImmune;
        public bool ghostSedationImmune;
        public bool saveCreature = true;
        public bool hibernateOffScreen;
        /// <summary>Which <see cref="AbstractRoomNode.Type"/>s the creature uses, indexed by type.</summary>
        public bool[] mappedNodeTypes;
        public PathCost shortcutAversion;
        public PathCost NPCTravelAversion;
        /// <summary>
        /// Extra tile rule for creatures that keep their distance from terrain. Returns 0 for "no
        /// opinion", 1 for "inaccessible" and 2 for "accessible regardless of the template's costs".
        /// </summary>
        public Func<AImap, IntVector2, int> isTooCloseToTerrain;
        public float bodySize;
        public float scaryness;
        public float deliciousness;
        /// <summary>RGB of the creature's dots in shortcuts (the game's <c>shortcutColor</c>).</summary>
        public (float r, float g, float b) shortcutColor;
        /// <summary>How many dots the creature shows in a shortcut.</summary>
        public int shortcutSegments;
        /// <summary>How this creature regards each other type, indexed by <see cref="Type"/>.</summary>
        public Relationship[] relationships;
        public Relationship defaultRelationship;
        public int quantifiedIndex;
        public WaterRelationship waterRelationship = WaterRelationship.AirOnly;
        public float waterPathingResistance;
        public bool canSwim;
        public bool socialMemory;
        /// <summary>Hardest accessibility allowed for a DoubleReachUp at the ground, the two tiles between, and the top.</summary>
        public int[] doubleReachUpConnectionParams;
        public bool BlizzardAdapted;
        public bool BlizzardWanderer;
        public bool usesNPCTransportation;
        public bool usesRegionTransportation;
        public bool usesCreatureHoles;
        public bool wormgrassTilesIgnored;
        public bool doesNotUseDens;
        public bool abstractImmobile;
        public float interestInOtherCreaturesCatches;
        public float interestInOtherAncestorsCatches;
        /// <summary>The creature never enters an ordinary shortcut (pipe) by itself.</summary>
        public bool forbidStandardShortcutEntry;

        /// <summary>
        /// Builds a template the way the game's constructor does: start from <paramref name="ancestor"/>'s
        /// values (or illegal everything without one), then apply the given resistances on top.
        /// </summary>
        /// <param name="world">Which DLC are on; the game copies some ancestor fields only with a DLC enabled.</param>
        internal CreatureTemplate(Type type, CreatureTemplate ancestor, IList<TileTypeResistance> tileResistances,
            IList<TileConnectionResistance> connectionResistances, Relationship defaultRelationship, StaticWorld.Dlc world)
        {
            this.type = type;
            name = NameOf(type);
            if (type == Type.LizardTemplate)
                SetDoubleReachUpConnectionParams(AItile.Accessibility.Floor, AItile.Accessibility.Wall, AItile.Accessibility.Floor);
            else if (type == Type.Scavenger)
                SetDoubleReachUpConnectionParams(AItile.Accessibility.Climb, AItile.Accessibility.Air, AItile.Accessibility.Climb);

            relationships = new Relationship[TypeCount];
            this.defaultRelationship = defaultRelationship;
            for (int i = 0; i < relationships.Length; i++)
                relationships[i] = defaultRelationship;
            offScreenSpeed = 1f;
            abstractedLaziness = 1;
            mappedNodeTypes = new bool[Enum.GetValues(typeof(AbstractRoomNode.Type)).Length];
            bodySize = 1f;
            scaryness = 1f;
            deliciousness = 1f;
            shortcutColor = (1f, 1f, 1f);
            shortcutSegments = 1;
            waterRelationship = WaterRelationship.Amphibious;
            waterPathingResistance = 1f;
            shortcutAversion = new PathCost(0f, PathCost.Legality.Allowed);
            NPCTravelAversion = new PathCost(100f, PathCost.Legality.Allowed);
            damageRestistances = new float[Enum.GetValues(typeof(DamageType)).Length, 2];
            instantDeathDamageLimit = float.MaxValue;
            this.ancestor = ancestor;
            if (ancestor != null)
                InheritFrom(ancestor, world);

            int accessibilityCount = Enum.GetValues(typeof(AItile.Accessibility)).Length;
            int movementCount = Enum.GetValues(typeof(MovementConnection.MovementType)).Length;
            if (ancestor == null)
            {
                // Every tile is illegal and gets dearer the harder it is; Solid is its own legality.
                pathingPreferencesTiles = new PathCost[accessibilityCount];
                for (int j = 0; j < accessibilityCount; j++)
                    pathingPreferencesTiles[j] = new PathCost(10f * j, PathCost.Legality.IllegalTile);
                pathingPreferencesTiles[(int)AItile.Accessibility.Solid] = new PathCost(100f, PathCost.Legality.SolidTile);
                pathingPreferencesConnections = new PathCost[movementCount];
                for (int k = 0; k < movementCount; k++)
                    pathingPreferencesConnections[k] = new PathCost(100f, PathCost.Legality.IllegalConnection);
            }
            else
            {
                pathingPreferencesTiles = (PathCost[])ancestor.pathingPreferencesTiles.Clone();
                pathingPreferencesConnections = (PathCost[])ancestor.pathingPreferencesConnections.Clone();
            }

            foreach (var r in tileResistances)
            {
                pathingPreferencesTiles[(int)r.accessibility] = r.cost;
                if (r.cost.legality == PathCost.Legality.Allowed && maxAccessibleTerrain < (int)r.accessibility
                    && r.accessibility != AItile.Accessibility.Sand)
                    maxAccessibleTerrain = (int)r.accessibility;
            }
            // Nothing easier than the hardest allowed terrain may cost more than it.
            for (int m = 0; m <= maxAccessibleTerrain; m++)
                if (pathingPreferencesTiles[m] > pathingPreferencesTiles[maxAccessibleTerrain])
                    pathingPreferencesTiles[m] = pathingPreferencesTiles[maxAccessibleTerrain];
            foreach (var r in connectionResistances)
                pathingPreferencesConnections[(int)r.movementType] = r.cost;

            SetNodeType(AbstractRoomNode.Type.Exit, ConnectionResistance(MovementConnection.MovementType.ShortCut).Allowed);
            SetNodeType(AbstractRoomNode.Type.Den, ConnectionResistance(MovementConnection.MovementType.ShortCut).Allowed);
            SetNodeType(AbstractRoomNode.Type.SkyExit, ConnectionResistance(MovementConnection.MovementType.SkyHighway).Allowed);
            SetNodeType(AbstractRoomNode.Type.SeaExit, ConnectionResistance(MovementConnection.MovementType.SeaHighway).Allowed);
            SetNodeType(AbstractRoomNode.Type.SideExit, ConnectionResistance(MovementConnection.MovementType.SideHighway).Allowed);
            if (type == Type.Scavenger)
            {
                SetNodeType(AbstractRoomNode.Type.Den, false);
                SetNodeType(AbstractRoomNode.Type.RegionTransportation, true);
            }
            if (type == Type.Fly)
                SetNodeType(AbstractRoomNode.Type.BatHive, true);
        }

        // The ancestor fields the game's constructor copies, with its DLC conditions.
        private void InheritFrom(CreatureTemplate a, StaticWorld.Dlc world)
        {
            preBakedPathingAncestor = a.preBakedPathingAncestor;
            virtualCreature = a.virtualCreature;
            doPreBakedPathing = a.doPreBakedPathing;
            AI = a.AI;
            requireAImap = a.requireAImap;
            quantified = a.quantified;
            canFly = a.canFly;
            grasps = a.grasps;
            offScreenSpeed = a.offScreenSpeed;
            abstractedLaziness = a.abstractedLaziness;
            stowFoodInDen = a.stowFoodInDen;
            smallCreature = a.smallCreature;
            roamInRoomChance = a.roamInRoomChance;
            roamBetweenRoomsChance = a.roamBetweenRoomsChance;
            canAutoAbstractPath = a.canAutoAbstractPath;
            visualRadius = a.visualRadius;
            waterVision = a.waterVision;
            throughSurfaceVision = a.throughSurfaceVision;
            movementBasedVision = a.movementBasedVision;
            dangerousToPlayer = a.dangerousToPlayer;
            communityID = a.communityID;
            communityInfluence = a.communityInfluence;
            countsAsAKill = a.countsAsAKill;
            quickDeath = a.quickDeath;
            meatPoints = a.meatPoints;
            wormGrassImmune = a.wormGrassImmune;
            daddyCorruptionImmune = a.daddyCorruptionImmune;
            saveCreature = a.saveCreature;
            hibernateOffScreen = a.hibernateOffScreen;
            mappedNodeTypes = (bool[])a.mappedNodeTypes.Clone();
            bodySize = a.bodySize;
            scaryness = a.scaryness;
            deliciousness = a.deliciousness;
            shortcutColor = a.shortcutColor;
            shortcutSegments = a.shortcutSegments;
            waterRelationship = a.waterRelationship;
            waterPathingResistance = a.waterPathingResistance;
            canSwim = a.canSwim;
            socialMemory = a.socialMemory;
            shortcutAversion = a.shortcutAversion;
            NPCTravelAversion = a.NPCTravelAversion;
            doubleReachUpConnectionParams = a.doubleReachUpConnectionParams;
            relationships = (Relationship[])a.relationships.Clone();
            baseDamageResistance = a.baseDamageResistance;
            baseStunResistance = a.baseStunResistance;
            instantDeathDamageLimit = a.instantDeathDamageLimit;
            lungCapacity = a.lungCapacity;
            if (world.Shared)
            {
                BlizzardAdapted = a.BlizzardAdapted;
                BlizzardWanderer = a.BlizzardWanderer;
            }
            if (world.Downpour)
            {
                usesNPCTransportation = a.usesNPCTransportation;
                usesRegionTransportation = a.usesRegionTransportation;
                usesCreatureHoles = a.usesCreatureHoles;
                wormgrassTilesIgnored = a.wormgrassTilesIgnored;
                doesNotUseDens = a.doesNotUseDens;
                abstractImmobile = a.abstractImmobile;
                interestInOtherCreaturesCatches = a.interestInOtherCreaturesCatches;
                interestInOtherAncestorsCatches = a.interestInOtherAncestorsCatches;
                forbidStandardShortcutEntry = a.forbidStandardShortcutEntry;
            }
        }

        private void SetDoubleReachUpConnectionParams(AItile.Accessibility groundTile, AItile.Accessibility betweenTiles, AItile.Accessibility destinationTile)
        {
            doubleReachUpConnectionParams = new[] { (int)groundTile, (int)betweenTiles, (int)destinationTile };
        }

        // CreatureTemplate constructor names, plus MoreSlugcatsCreatures.CreatureName for Downpour's.
        internal static string NameOf(Type type)
        {
            switch (type)
            {
                case Type.StandardGroundCreature: return "StandardGroundCreature";
                case Type.Slugcat: return "Slugcat";
                case Type.LizardTemplate: return "Lizard";
                case Type.PinkLizard: return "Pink Lizard";
                case Type.GreenLizard: return "Green Lizard";
                case Type.BlueLizard: return "Blue Lizard";
                case Type.YellowLizard: return "Yellow Lizard";
                case Type.WhiteLizard: return "White Lizard";
                case Type.RedLizard: return "Red Lizard";
                case Type.Salamander: return "Salamander";
                case Type.CyanLizard: return "Cyan Lizard";
                case Type.Fly: return "Fly";
                case Type.Vulture: return "Vulture";
                case Type.CicadaA: return "Cicada A";
                case Type.CicadaB: return "Cicada B";
                case Type.TentaclePlant: return "Tentacle Plant";
                case Type.PoleMimic: return "Pole Mimic";
                case Type.Centipede: return "Centipede";
                case Type.RedCentipede: return "Red Centipede";
                case Type.Centiwing: return "Centiwing";
                case Type.SmallCentipede: return "Small Centipede";
                case Type.Scavenger: return "Scavenger";
                case Type.Overseer: return "Overseer";
                case Type.VultureGrub: return "Vulture Grub";
                case Type.EggBug: return "Egg Bug";
                case Type.BigSpider: return "Big Spider";
                case Type.SpitterSpider: return "Spitter Spider";
                case Type.SmallNeedleWorm: return "Small Needle";
                case Type.BigNeedleWorm: return "Big Needle";
                case Type.DropBug: return "Drop Bug";
                case Type.KingVulture: return "King Vulture";
                case Type.Hazer: return "Hazer";
                case Type.MirosVulture: return "Miros Vulture";
                case Type.SpitLizard: return "Caramel Lizard";
                case Type.EelLizard: return "Eel";
                case Type.ZoopLizard: return "Strawberry";
                default: return "???";
            }
        }

        public bool UseAnyRoomBorderExit =>
            mappedNodeTypes[(int)AbstractRoomNode.Type.SideExit] || mappedNodeTypes[(int)AbstractRoomNode.Type.SkyExit]
            || mappedNodeTypes[(int)AbstractRoomNode.Type.SeaExit];

        public bool IsCicada => type == Type.CicadaA || type == Type.CicadaB;

        public bool IsLizard => ancestor != null && ancestor.type == Type.LizardTemplate;

        public bool IsVulture => type == Type.Vulture || type == Type.KingVulture;

        /// <summary>
        /// Index of the pre-baked pathing data this template uses, or -1 for none (the creature then
        /// can't stand on any AI-map tile). Kept as the game reads it: one level of
        /// <see cref="preBakedPathingAncestor"/>, not a chain.
        /// </summary>
        public int PreBakedPathingIndex
        {
            get
            {
                if (doPreBakedPathing) return prebakedPathingIndex;
                if (preBakedPathingAncestor != null) return preBakedPathingAncestor.prebakedPathingIndex;
                return -1;
            }
            set
            {
                if (doPreBakedPathing) prebakedPathingIndex = value;
            }
        }

        public void SetNodeType(AbstractRoomNode.Type tp, bool b) => mappedNodeTypes[(int)tp] = b;

        public PathCost ConnectionResistance(MovementConnection.MovementType v) => pathingPreferencesConnections[(int)v];

        public PathCost AccessibilityResistance(AItile.Accessibility v) => pathingPreferencesTiles[(int)v];

        public bool MovementLegalInRelationToWater(bool deepWater, bool waterSurface)
        {
            switch (waterRelationship)
            {
                case WaterRelationship.AirAndSurface: return !deepWater;
                case WaterRelationship.AirOnly: return !deepWater && !waterSurface;
                case WaterRelationship.Amphibious: return true;
                case WaterRelationship.WaterOnly: return deepWater;
                default: return false;
            }
        }

        public bool AbstractSubmersionLegal(bool nodeSubmerged)
        {
            if (nodeSubmerged)
                return waterRelationship == WaterRelationship.WaterOnly || waterRelationship == WaterRelationship.Amphibious;
            return waterRelationship != WaterRelationship.WaterOnly;
        }

        public CreatureTemplate TopAncestor() => ancestor == null ? this : ancestor.TopAncestor();

        public Relationship CreatureRelationship(Type crit) => relationships[(int)crit];

        public Relationship CreatureRelationship(CreatureTemplate crit) =>
            crit == null ? new Relationship(Relationship.Type.Ignores, 0f) : relationships[(int)crit.type];

        public override string ToString() => name;
    }
}
