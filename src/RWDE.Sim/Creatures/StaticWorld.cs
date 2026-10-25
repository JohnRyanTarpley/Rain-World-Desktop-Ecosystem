using System;
using System.Collections.Generic;
using Type = RWDE.Sim.CreatureTemplate.Type;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;
using Legality = RWDE.Sim.PathCost.Legality;
using Rel = RWDE.Sim.CreatureTemplate.Relationship;

namespace RWDE.Sim
{
    /// <summary>
    /// Every <see cref="CreatureTemplate"/>, built as the game's <c>StaticWorld.InitStaticWorld</c>
    /// builds them, then given the game's relationships. Unlike the game's static class this is an
    /// instance per DLC setting, because a DLC can be switched off while the app runs.
    /// </summary>
    /// <remarks>
    /// Only the roster in docs/VISION.md, their template ancestors and the player template are built.
    /// Other game creatures never appear on the desktop; leaving them out changes no relationship
    /// between built types, because the built set includes every ancestor of each built type and the
    /// game only passes relationships down to descendants.
    /// </remarks>
    public sealed partial class StaticWorld
    {
        /// <summary>Which DLC are enabled. The game's <c>ModManager.MSC</c>, <c>ModManager.Watcher</c> and <c>ModManager.DLCShared</c>.</summary>
        public readonly struct Dlc
        {
            public Dlc(bool downpour, bool watcher)
            {
                Downpour = downpour;
                Watcher = watcher;
            }

            public bool Downpour { get; }
            public bool Watcher { get; }

            /// <summary>
            /// Content shared by both DLC, which either one enables (<c>ModManager.DLCShared</c>). The
            /// Miros Vulture and the Caramel, Strawberry and Eel Lizards are shared content.
            /// </summary>
            public bool Shared => Downpour || Watcher;

            public static Dlc BaseOnly => new Dlc(false, false);
            public static Dlc All => new Dlc(true, true);
        }

        /// <summary>
        /// The game's <c>ModManager.MMF</c> (Remix), which the desktop treats as enabled with its default
        /// options. Templates only read it for the Salamander's <c>canSwim</c>, which matters only in water.
        /// </summary>
        internal const bool Remix = true;

        private readonly CreatureTemplate[] creatureTemplates = new CreatureTemplate[CreatureTemplate.TypeCount];

        private StaticWorld(Dlc dlc)
        {
            DlcSetting = dlc;
        }

        public Dlc DlcSetting { get; }

        /// <summary>Templates that own pre-baked pathing data, in index order (<c>StaticWorld.preBakedPathingCreatures</c>).</summary>
        public CreatureTemplate[] preBakedPathingCreatures { get; private set; }

        /// <summary>Counted rather than simulated creatures, in index order (<c>StaticWorld.quantifiedCreatures</c>).</summary>
        public CreatureTemplate[] quantifiedCreatures { get; private set; }

        /// <summary>Every built template in <see cref="Type"/> order. DLC types are missing when their DLC is off.</summary>
        public IEnumerable<CreatureTemplate> CreatureTemplates
        {
            get
            {
                foreach (var t in creatureTemplates)
                    if (t != null) yield return t;
            }
        }

        /// <summary>The template for <paramref name="type"/>, or null when its DLC is off.</summary>
        public CreatureTemplate GetCreatureTemplate(Type type) => creatureTemplates[(int)type];

        /// <summary>The template whose game name is <paramref name="name"/> (e.g. "Pink Lizard"), or null.</summary>
        public CreatureTemplate GetCreatureTemplate(string name)
        {
            foreach (var t in CreatureTemplates)
                if (t.name == name) return t;
            return null;
        }

        /// <summary>Builds every template and relationship for <paramref name="dlc"/> (<c>StaticWorld.InitStaticWorld</c>).</summary>
        public static StaticWorld Init(Dlc dlc)
        {
            var world = new StaticWorld(dlc);
            world.InitTemplates();
            world.IndexTemplates();
            world.InitStaticWorldRelationships();
            if (dlc.Shared) world.InitStaticWorldRelationshipsShared();
            // InitStaticWorldRelationshipsWatcher changes nothing between built types.

            // The Cyan Lizard only eats at 30% of the intensity it inherits.
            var cyan = world.GetCreatureTemplate(Type.CyanLizard);
            for (int i = 0; i < cyan.relationships.Length; i++)
                if (cyan.relationships[i].type == Rel.Type.Eats)
                    cyan.relationships[i].intensity *= 0.3f;
            return world;
        }

        // ---- Templates (StaticWorld.InitStaticWorld) ----

        private readonly List<TileTypeResistance> tiles = new List<TileTypeResistance>();
        private readonly List<TileConnectionResistance> connections = new List<TileConnectionResistance>();

        private StaticWorld T(Acc a, float resistance, Legality legality = Legality.Allowed)
        {
            tiles.Add(new TileTypeResistance(a, resistance, legality));
            return this;
        }

        private StaticWorld C(Move m, float resistance, Legality legality = Legality.Allowed)
        {
            connections.Add(new TileConnectionResistance(m, resistance, legality));
            return this;
        }

        // Creates a template from the resistances queued with T and C, then clears them.
        private CreatureTemplate New(Type type, CreatureTemplate ancestor, Rel.Type defaultType, float defaultIntensity)
        {
            var t = new CreatureTemplate(type, ancestor, tiles, connections, new Rel(defaultType, defaultIntensity), DlcSetting);
            tiles.Clear();
            connections.Clear();
            Add(t);
            return t;
        }

        private void Add(CreatureTemplate t) => creatureTemplates[(int)t.type] = t;

        private static readonly Func<AImap, IntVector2, int> VultureTerrainRule =
            (aimap, pos) => aimap.getTerrainProximity(pos) < 2 ? 1 : 0;

        private void InitTemplates()
        {
            var dlc = DlcSetting;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.ReachDown, 2f)
                .C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 20f).C(Move.DropToWater, 20f).C(Move.ShortCut, 1.5f)
                .C(Move.NPCTransportation, 25f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f).C(Move.Slope, 1.5f);
            var standardGround = New(Type.StandardGroundCreature, null, Rel.Type.Ignores, 0f);
            standardGround.doPreBakedPathing = true;
            standardGround.virtualCreature = true;
            standardGround.canSwim = true;
            standardGround.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;

            T(Acc.Floor, 1f).T(Acc.Corridor, 1.2f).T(Acc.Climb, 1.5f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 2f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.ReachDown, 2f)
                .C(Move.DropToFloor, 3000f).C(Move.DropToClimb, 6000f).C(Move.DropToWater, 4000f).C(Move.ShortCut, 0.9f);
            var slugcat = New(Type.Slugcat, null, Rel.Type.Ignores, 0f);
            slugcat.baseDamageResistance = 1f;
            slugcat.baseStunResistance = 1f;
            slugcat.instantDeathDamageLimit = 1f;
            slugcat.doPreBakedPathing = false;
            slugcat.grasps = 2;
            slugcat.offScreenSpeed = 0.1f;
            slugcat.bodySize = 1f;
            slugcat.shortcutSegments = 2;
            slugcat.preBakedPathingAncestor = standardGround;
            slugcat.meatPoints = 3;
            slugcat.waterRelationship = CreatureTemplate.WaterRelationship.Amphibious;
            slugcat.waterPathingResistance = 2f;
            slugcat.canSwim = true;
            slugcat.ghostSedationImmune = true;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.CurvedFloor, 1f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.DoubleReachUp, 2f)
                .C(Move.ReachDown, 2f).C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 20f).C(Move.DropToWater, 20f)
                .C(Move.ShortCut, 1.5f).C(Move.NPCTransportation, 25f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f)
                .C(Move.LizardTurn, 10f).C(Move.Slope, 1.5f);
            var lizard = New(Type.LizardTemplate, null, Rel.Type.Ignores, 0f);
            lizard.offScreenSpeed = 0.3f;
            lizard.grasps = 1;
            lizard.virtualCreature = true;
            lizard.abstractedLaziness = 50;
            lizard.AI = true;
            lizard.requireAImap = true;
            lizard.bodySize = 2f;
            lizard.stowFoodInDen = true;
            lizard.shortcutSegments = 3;
            lizard.communityID = CreatureTemplate.CommunityID.Lizards;
            lizard.communityInfluence = 1f;
            lizard.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            lizard.canSwim = true;
            lizard.socialMemory = true;
            lizard.usesNPCTransportation = true;
            lizard.interestInOtherAncestorsCatches = 1.25f;
            lizard.interestInOtherCreaturesCatches = 1.1f;

            var pink = LizardBreeds.BreedTemplate(Type.PinkLizard, lizard, null, null, null, dlc);
            Add(pink);
            var green = LizardBreeds.BreedTemplate(Type.GreenLizard, lizard, null, null, null, dlc);
            Add(green);
            var blue = LizardBreeds.BreedTemplate(Type.BlueLizard, lizard, null, null, null, dlc);
            Add(blue);
            Add(LizardBreeds.BreedTemplate(Type.YellowLizard, lizard, pink, null, null, dlc));
            var white = LizardBreeds.BreedTemplate(Type.WhiteLizard, lizard, pink, null, null, dlc);
            Add(white);
            Add(LizardBreeds.BreedTemplate(Type.RedLizard, lizard, pink, null, null, dlc));
            var salamander = LizardBreeds.BreedTemplate(Type.Salamander, lizard, pink, null, null, dlc);
            Add(salamander);
            Add(LizardBreeds.BreedTemplate(Type.CyanLizard, lizard, pink, blue, null, dlc));
            if (dlc.Shared)
            {
                // StaticWorld.InitLizardsMSC
                var spit = LizardBreeds.BreedTemplate(Type.SpitLizard, lizard, null, null, green, dlc);
                spit.BlizzardWanderer = true;
                spit.BlizzardAdapted = true;
                Add(spit);
                Add(LizardBreeds.EelTemplate(lizard, salamander, dlc));
                var zoop = LizardBreeds.BreedTemplate(Type.ZoopLizard, lizard, pink, blue, green, dlc);
                zoop.BlizzardWanderer = true;
                Add(zoop);
            }

            T(Acc.Air, 1f);
            C(Move.Standard, 1f).C(Move.ShortCut, 1f).C(Move.NPCTransportation, 10f);
            var fly = New(Type.Fly, null, Rel.Type.Uncomfortable, 0f);
            fly.baseDamageResistance = 0.15f;
            fly.baseStunResistance = 0.2f;
            fly.instantDeathDamageLimit = 1f;
            fly.abstractedLaziness = 100;
            fly.AI = false;
            fly.requireAImap = true;
            fly.canFly = true;
            fly.doPreBakedPathing = true;
            fly.offScreenSpeed = 0.1f;
            fly.bodySize = 0.1f;
            fly.quantified = true;
            fly.grasps = 1;
            fly.smallCreature = true;
            fly.visualRadius = 450f;
            fly.communityID = CreatureTemplate.CommunityID.None;
            fly.countsAsAKill = 0;
            fly.waterRelationship = CreatureTemplate.WaterRelationship.AirOnly;
            fly.BlizzardWanderer = true;

            T(Acc.Air, 1f).T(Acc.OffScreen, 1f);
            C(Move.Standard, 1f).C(Move.OutsideRoom, 1f).C(Move.SkyHighway, 1f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f);
            var vulture = New(Type.Vulture, null, Rel.Type.Ignores, 0f);
            vulture.baseDamageResistance = 8.5f;
            vulture.baseStunResistance = 3.5f;
            vulture.abstractedLaziness = 10;
            vulture.AI = true;
            vulture.requireAImap = true;
            vulture.canSwim = false;
            vulture.canFly = true;
            vulture.doPreBakedPathing = true;
            vulture.canAutoAbstractPath = false;
            vulture.offScreenSpeed = 0.8f;
            vulture.bodySize = 6f;
            vulture.grasps = 1;
            vulture.stowFoodInDen = true;
            vulture.shortcutSegments = 5;
            vulture.visualRadius = 7000f;
            vulture.movementBasedVision = 0.4f;
            vulture.waterVision = 0.8f;
            vulture.throughSurfaceVision = 0.8f;
            vulture.hibernateOffScreen = true;
            vulture.dangerousToPlayer = 0.7f;
            vulture.communityInfluence = 0.25f;
            vulture.socialMemory = true;
            vulture.meatPoints = 14;
            vulture.lungCapacity = 750f;
            vulture.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            vulture.interestInOtherAncestorsCatches = 0.25f;
            vulture.interestInOtherCreaturesCatches = 0.25f;
            vulture.forbidStandardShortcutEntry = true;
            vulture.isTooCloseToTerrain = VultureTerrainRule;

            T(Acc.Air, 1f).T(Acc.OffScreen, 1f);
            C(Move.Standard, 1f).C(Move.OutsideRoom, 1f).C(Move.SkyHighway, 1f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f);
            var king = New(Type.KingVulture, vulture, Rel.Type.Ignores, 0f);
            king.baseDamageResistance = 12.5f;
            king.baseStunResistance = 6.5f;
            king.abstractedLaziness = 10;
            king.preBakedPathingAncestor = vulture;
            king.doPreBakedPathing = false;
            king.offScreenSpeed = 0.8f;
            king.bodySize = 8.5f;
            king.visualRadius = 7000f;
            king.movementBasedVision = 0.4f;
            king.waterVision = 0.8f;
            king.throughSurfaceVision = 0.8f;
            king.dangerousToPlayer = 0.9f;
            king.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            king.meatPoints = 16;
            king.lungCapacity = 750f;
            king.canFly = true;
            king.isTooCloseToTerrain = VultureTerrainRule;

            if (dlc.Shared)
            {
                // StaticWorld.InitMirosVulture: not a descendant of the Vulture template.
                T(Acc.Air, 1f).T(Acc.OffScreen, 1f);
                C(Move.Standard, 1f).C(Move.OutsideRoom, 1f).C(Move.SkyHighway, 1f).C(Move.OffScreenMovement, 1f).C(Move.SideHighway, 10f);
                var miros = New(Type.MirosVulture, null, Rel.Type.Ignores, 0f);
                miros.BlizzardWanderer = true;
                miros.BlizzardAdapted = true;
                miros.baseDamageResistance = 20f;
                miros.baseStunResistance = 5f;
                miros.abstractedLaziness = 10;
                miros.AI = true;
                miros.requireAImap = true;
                miros.canSwim = false;
                miros.canFly = true;
                miros.doPreBakedPathing = false;
                miros.preBakedPathingAncestor = vulture;
                miros.offScreenSpeed = 1.5f;
                miros.bodySize = 8f;
                miros.grasps = 1;
                miros.stowFoodInDen = true;
                miros.shortcutSegments = 5;
                miros.visualRadius = 7000f;
                miros.movementBasedVision = 0.4f;
                miros.waterVision = 0.8f;
                miros.throughSurfaceVision = 0.8f;
                miros.hibernateOffScreen = true;
                miros.dangerousToPlayer = 0.7f;
                miros.meatPoints = 15;
                miros.communityInfluence = 0.25f;
                miros.socialMemory = true;
                miros.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
                miros.isTooCloseToTerrain = VultureTerrainRule;
            }

            T(Acc.Air, 1f);
            C(Move.Standard, 1f).C(Move.ShortCut, 1f).C(Move.NPCTransportation, 10f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f);
            var cicadaA = New(Type.CicadaA, null, Rel.Type.Uncomfortable, 0f);
            cicadaA.baseDamageResistance = 0.8f;
            cicadaA.baseStunResistance = 1f;
            cicadaA.abstractedLaziness = 100;
            cicadaA.AI = true;
            cicadaA.requireAImap = true;
            cicadaA.canFly = true;
            cicadaA.doPreBakedPathing = false;
            cicadaA.offScreenSpeed = 0.5f;
            cicadaA.bodySize = 0.6f;
            cicadaA.grasps = 1;
            cicadaA.preBakedPathingAncestor = fly;
            cicadaA.stowFoodInDen = true;
            cicadaA.visualRadius = 1100f;
            cicadaA.movementBasedVision = 0.6f;
            cicadaA.communityID = CreatureTemplate.CommunityID.Cicadas;
            cicadaA.communityInfluence = 1f;
            cicadaA.socialMemory = true;
            cicadaA.lungCapacity = 180f;
            cicadaA.waterRelationship = CreatureTemplate.WaterRelationship.AirOnly;
            cicadaA.meatPoints = 3;
            cicadaA.usesNPCTransportation = true;

            New(Type.CicadaB, cicadaA, Rel.Type.Uncomfortable, 0f);

            var tentaclePlant = New(Type.TentaclePlant, null, Rel.Type.Eats, 1f);
            tentaclePlant.baseDamageResistance = 2.5f;
            tentaclePlant.baseStunResistance = 0.8f;
            tentaclePlant.AI = true;
            tentaclePlant.requireAImap = false;
            tentaclePlant.doPreBakedPathing = false;
            tentaclePlant.stowFoodInDen = true;
            tentaclePlant.offScreenSpeed = 0f;
            tentaclePlant.bodySize = 2f;
            tentaclePlant.grasps = 1;
            tentaclePlant.visualRadius = 400f;
            tentaclePlant.movementBasedVision = 1f;
            tentaclePlant.waterVision = 1f;
            tentaclePlant.throughSurfaceVision = 0f;
            tentaclePlant.dangerousToPlayer = 0.5f;
            tentaclePlant.communityInfluence = 0.1f;
            tentaclePlant.wormGrassImmune = true;
            tentaclePlant.waterRelationship = CreatureTemplate.WaterRelationship.Amphibious;
            tentaclePlant.BlizzardWanderer = true;

            var poleMimic = New(Type.PoleMimic, tentaclePlant, Rel.Type.Eats, 1f);
            poleMimic.baseDamageResistance = 0.9f;
            poleMimic.baseStunResistance = 0.5f;
            poleMimic.instantDeathDamageLimit = 1f;
            poleMimic.dangerousToPlayer = 0.5f;
            poleMimic.communityInfluence = 0.05f;
            poleMimic.countsAsAKill = 1;
            poleMimic.AI = false;
            poleMimic.requireAImap = true;
            poleMimic.stowFoodInDen = true;
            poleMimic.wormGrassImmune = true;
            poleMimic.bodySize = 1.4f;
            poleMimic.BlizzardWanderer = true;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 1f).T(Acc.Wall, 1f).T(Acc.Ceiling, 1f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.DoubleReachUp, 2f)
                .C(Move.SemiDiagonalReach, 2f).C(Move.NPCTransportation, 25f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f)
                .C(Move.Slope, 1.5f).C(Move.DropToFloor, 5f).C(Move.DropToClimb, 5f).C(Move.ShortCut, 1f).C(Move.ReachUp, 1.1f)
                .C(Move.ReachDown, 1.1f).C(Move.CeilingSlope, 2f);
            var centipede = New(Type.Centipede, null, Rel.Type.Eats, 1f);
            centipede.baseDamageResistance = 0.9f;
            centipede.baseStunResistance = 0.6f;
            centipede.quickDeath = false;
            centipede.offScreenSpeed = 0.3f;
            centipede.grasps = 2;
            centipede.abstractedLaziness = 150;
            centipede.AI = true;
            centipede.requireAImap = true;
            centipede.bodySize = 1f;
            centipede.shortcutSegments = 3;
            centipede.doPreBakedPathing = false;
            centipede.preBakedPathingAncestor = blue;
            centipede.doubleReachUpConnectionParams = blue.doubleReachUpConnectionParams;
            centipede.visualRadius = 900f;
            centipede.waterVision = 0.4f;
            centipede.throughSurfaceVision = 0.85f;
            centipede.movementBasedVision = 0.95f;
            centipede.dangerousToPlayer = 0.3f;
            centipede.communityInfluence = 0.25f;
            centipede.meatPoints = 1;
            centipede.lungCapacity = 900f;
            centipede.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            centipede.canSwim = true;
            centipede.damageRestistances[(int)CreatureTemplate.DamageType.Electric, 0] = 102f;
            centipede.damageRestistances[(int)CreatureTemplate.DamageType.Electric, 1] = 102f;
            centipede.BlizzardWanderer = true;
            centipede.BlizzardAdapted = true;
            centipede.stowFoodInDen = false;
            centipede.usesNPCTransportation = true;

            var redCentipede = New(Type.RedCentipede, centipede, Rel.Type.Eats, 1f);
            redCentipede.baseDamageResistance = 1f;
            redCentipede.visualRadius = 1100f;
            redCentipede.communityInfluence = 0.25f;
            redCentipede.meatPoints = 1;
            redCentipede.shortcutColor = (1f, 0f, 0f);
            redCentipede.shortcutSegments = 5;
            redCentipede.bodySize = 8.5f;
            redCentipede.dangerousToPlayer = 0.9f;
            redCentipede.BlizzardWanderer = true;
            redCentipede.BlizzardAdapted = true;

            var smallCentipede = New(Type.SmallCentipede, centipede, Rel.Type.Eats, 1f);
            smallCentipede.roamBetweenRoomsChance = -1f;
            smallCentipede.communityID = CreatureTemplate.CommunityID.None;
            smallCentipede.dangerousToPlayer = 0f;
            smallCentipede.baseDamageResistance = 1f;
            smallCentipede.instantDeathDamageLimit = 1f;
            smallCentipede.countsAsAKill = 0;
            smallCentipede.abstractedLaziness = 1000;
            smallCentipede.meatPoints = 0;
            smallCentipede.bodySize = 0.2f;
            smallCentipede.BlizzardWanderer = true;
            smallCentipede.BlizzardAdapted = true;

            T(Acc.Air, 1f);
            C(Move.Standard, 1f).C(Move.ShortCut, 1f).C(Move.NPCTransportation, 10f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 10f);
            var centiwing = New(Type.Centiwing, centipede, Rel.Type.Eats, 1f);
            centiwing.baseDamageResistance = 1f;
            centiwing.canFly = true;
            centiwing.doPreBakedPathing = false;
            centiwing.preBakedPathingAncestor = fly;
            centiwing.visualRadius = 300f;
            centiwing.waterVision = 0.4f;
            centiwing.throughSurfaceVision = 0.85f;
            centiwing.movementBasedVision = 0.25f;
            centiwing.dangerousToPlayer = 0.4f;
            centiwing.communityInfluence = 0.25f;
            centiwing.meatPoints = 1;
            centiwing.bodySize = 1.2f;
            centiwing.BlizzardWanderer = true;
            centiwing.BlizzardAdapted = true;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.DoubleReachUp, 2f)
                .C(Move.ReachDown, 2f).C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 10f).C(Move.DropToClimb, 20f)
                .C(Move.DropToWater, 10f).C(Move.ShortCut, 2.5f).C(Move.NPCTransportation, 225f).C(Move.OffScreenMovement, 1.2f)
                .C(Move.BetweenRooms, 15f).C(Move.Slope, 1.5f).C(Move.RegionTransportation, 400f);
            var scavenger = New(Type.Scavenger, null, Rel.Type.Ignores, 0.1f);
            scavenger.baseDamageResistance = 1.1f;
            scavenger.baseStunResistance = 1.3f;
            scavenger.instantDeathDamageLimit = 1.5f;
            scavenger.offScreenSpeed = 0.75f;
            scavenger.grasps = 4;
            scavenger.abstractedLaziness = 50;
            scavenger.AI = true;
            scavenger.requireAImap = true;
            scavenger.bodySize = 1.2f;
            scavenger.stowFoodInDen = false;
            scavenger.shortcutSegments = 2;
            scavenger.doPreBakedPathing = true;
            scavenger.visualRadius = 1200f;
            scavenger.movementBasedVision = 0.3f;
            scavenger.canSwim = true;
            scavenger.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            scavenger.hibernateOffScreen = true;
            scavenger.roamBetweenRoomsChance = -1f;
            scavenger.roamInRoomChance = -1f;
            scavenger.socialMemory = true;
            scavenger.communityID = CreatureTemplate.CommunityID.Scavengers;
            scavenger.communityInfluence = 1f;
            scavenger.dangerousToPlayer = 0.65f;
            scavenger.meatPoints = 4;
            scavenger.BlizzardWanderer = true;
            scavenger.usesNPCTransportation = true;
            scavenger.usesRegionTransportation = true;
            scavenger.usesCreatureHoles = false;

            var overseer = New(Type.Overseer, null, Rel.Type.Ignores, 0f);
            overseer.baseDamageResistance = 0.9f;
            overseer.baseStunResistance = 0.5f;
            overseer.instantDeathDamageLimit = 1f;
            overseer.AI = true;
            overseer.requireAImap = true;
            overseer.doPreBakedPathing = false;
            overseer.offScreenSpeed = 0f;
            overseer.bodySize = 0.3f;
            overseer.wormGrassImmune = true;
            overseer.saveCreature = false;
            overseer.countsAsAKill = 1;
            overseer.BlizzardWanderer = true;
            overseer.BlizzardAdapted = true;

            var vultureGrub = New(Type.VultureGrub, null, Rel.Type.Ignores, 0f);
            vultureGrub.baseDamageResistance = 0.3f;
            vultureGrub.baseStunResistance = 0.3f;
            vultureGrub.instantDeathDamageLimit = 0.6f;
            vultureGrub.AI = false;
            vultureGrub.requireAImap = false;
            vultureGrub.doPreBakedPathing = false;
            vultureGrub.offScreenSpeed = 0f;
            vultureGrub.bodySize = 0.2f;
            vultureGrub.grasps = 0;
            vultureGrub.visualRadius = 0f;
            vultureGrub.communityInfluence = 0f;
            vultureGrub.countsAsAKill = 0;
            vultureGrub.preBakedPathingAncestor = green;
            vultureGrub.waterRelationship = CreatureTemplate.WaterRelationship.AirOnly;

            var hazer = New(Type.Hazer, vultureGrub, Rel.Type.Ignores, 0f);
            hazer.waterRelationship = CreatureTemplate.WaterRelationship.Amphibious;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 2.5f).T(Acc.Wall, 50f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.ReachDown, 2f)
                .C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 10f).C(Move.DropToWater, 10f, Legality.Unwanted)
                .C(Move.DropToClimb, 10f, Legality.Unwanted).C(Move.ShortCut, 1.5f).C(Move.NPCTransportation, 3f)
                .C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 5f).C(Move.Slope, 1.5f);
            var eggBug = New(Type.EggBug, null, Rel.Type.Afraid, 0.1f);
            eggBug.baseDamageResistance = 0.15f;
            eggBug.baseStunResistance = 0.3f;
            eggBug.instantDeathDamageLimit = 0.7f;
            eggBug.offScreenSpeed = 0.3f;
            eggBug.abstractedLaziness = 50;
            eggBug.AI = true;
            eggBug.requireAImap = true;
            eggBug.bodySize = 0.4f;
            eggBug.stowFoodInDen = true;
            eggBug.shortcutSegments = 2;
            eggBug.preBakedPathingAncestor = white;
            eggBug.grasps = 0;
            eggBug.visualRadius = 1200f;
            eggBug.communityInfluence = 0.1f;
            eggBug.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            eggBug.waterPathingResistance = 40f;
            eggBug.canSwim = true;
            eggBug.meatPoints = 1;
            eggBug.usesNPCTransportation = true;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 1.5f).T(Acc.Wall, 3f).T(Acc.Ceiling, 3f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.ReachDown, 2f)
                .C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 10f).C(Move.DropToWater, 10f, Legality.Unwanted)
                .C(Move.DropToClimb, 10f, Legality.Unwanted).C(Move.ShortCut, 1.5f).C(Move.NPCTransportation, 3f)
                .C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 5f).C(Move.Slope, 1.5f).C(Move.CeilingSlope, 1.5f);
            var bigSpider = New(Type.BigSpider, null, Rel.Type.Ignores, 0f);
            bigSpider.baseDamageResistance = 0.6f;
            bigSpider.baseStunResistance = 0.5f;
            bigSpider.instantDeathDamageLimit = 1f;
            bigSpider.offScreenSpeed = 0.3f;
            bigSpider.abstractedLaziness = 50;
            bigSpider.AI = true;
            bigSpider.requireAImap = true;
            bigSpider.bodySize = 1.1f;
            bigSpider.stowFoodInDen = true;
            bigSpider.shortcutSegments = 2;
            bigSpider.preBakedPathingAncestor = blue;
            bigSpider.grasps = 1;
            bigSpider.visualRadius = 500f;
            bigSpider.communityInfluence = 0.1f;
            bigSpider.dangerousToPlayer = 0.35f;
            bigSpider.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            bigSpider.waterPathingResistance = 5f;
            bigSpider.canSwim = true;
            bigSpider.meatPoints = 3;
            bigSpider.interestInOtherAncestorsCatches = 0f;
            bigSpider.interestInOtherCreaturesCatches = 2f;
            bigSpider.usesNPCTransportation = true;

            // The game passes the Big Spider's resistance lists again, which have the same effect as inheriting.
            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 1.5f).T(Acc.Wall, 3f).T(Acc.Ceiling, 3f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.ReachDown, 2f)
                .C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 10f).C(Move.DropToWater, 10f, Legality.Unwanted)
                .C(Move.DropToClimb, 10f, Legality.Unwanted).C(Move.ShortCut, 1.5f).C(Move.NPCTransportation, 3f)
                .C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 5f).C(Move.Slope, 1.5f).C(Move.CeilingSlope, 1.5f);
            var spitter = New(Type.SpitterSpider, bigSpider, Rel.Type.Ignores, 0f);
            spitter.bodySize = 1.2f;
            spitter.baseDamageResistance = 1.1f;
            spitter.baseStunResistance = 0.6f;
            spitter.visualRadius = 1100f;
            spitter.dangerousToPlayer = 0.5f;
            spitter.meatPoints = 4;

            T(Acc.Air, 1f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 1f).C(Move.ShortCut, 1f).C(Move.NPCTransportation, 10f)
                .C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 1f);
            var bigNeedle = New(Type.BigNeedleWorm, null, Rel.Type.Uncomfortable, 1f);
            bigNeedle.baseDamageResistance = 0.4f;
            bigNeedle.baseStunResistance = 1f;
            bigNeedle.instantDeathDamageLimit = 1.2f;
            bigNeedle.abstractedLaziness = 10;
            bigNeedle.roamBetweenRoomsChance = 0.07f;
            bigNeedle.AI = true;
            bigNeedle.requireAImap = true;
            bigNeedle.canFly = true;
            bigNeedle.doPreBakedPathing = false;
            bigNeedle.offScreenSpeed = 1.5f;
            bigNeedle.bodySize = 0.7f;
            bigNeedle.grasps = 1;
            bigNeedle.preBakedPathingAncestor = fly;
            bigNeedle.stowFoodInDen = true;
            bigNeedle.visualRadius = 900f;
            bigNeedle.movementBasedVision = 0.3f;
            bigNeedle.communityInfluence = 0.5f;
            bigNeedle.socialMemory = true;
            bigNeedle.shortcutSegments = 3;
            bigNeedle.shortcutColor = (1f, 0.5f, 0.5f);
            bigNeedle.dangerousToPlayer = 0.5f;
            bigNeedle.waterRelationship = CreatureTemplate.WaterRelationship.AirOnly;
            bigNeedle.meatPoints = 3;
            bigNeedle.usesNPCTransportation = true;

            // Same here: the Big Noodlefly's lists are passed again.
            T(Acc.Air, 1f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 1f).C(Move.ShortCut, 1f).C(Move.NPCTransportation, 10f)
                .C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 1f);
            var smallNeedle = New(Type.SmallNeedleWorm, bigNeedle, Rel.Type.Uncomfortable, 1f);
            smallNeedle.smallCreature = true;
            smallNeedle.bodySize = 0.15f;
            smallNeedle.baseDamageResistance = 0.2f;
            smallNeedle.baseStunResistance = 0.4f;
            smallNeedle.socialMemory = false;
            smallNeedle.roamBetweenRoomsChance = 0.05f;
            smallNeedle.offScreenSpeed = 3.2f;
            smallNeedle.shortcutSegments = 1;
            smallNeedle.dangerousToPlayer = 0f;
            smallNeedle.meatPoints = 0;
            smallNeedle.usesNPCTransportation = true;

            T(Acc.OffScreen, 1f).T(Acc.Floor, 1f).T(Acc.Corridor, 1f).T(Acc.Climb, 1.5f).T(Acc.Wall, 3f).T(Acc.Ceiling, 3f);
            C(Move.Standard, 1f).C(Move.OpenDiagonal, 3f).C(Move.ReachOverGap, 3f).C(Move.ReachUp, 2f).C(Move.ReachDown, 2f)
                .C(Move.SemiDiagonalReach, 2f).C(Move.DropToFloor, 10f).C(Move.DropToWater, 10f).C(Move.ShortCut, 1.5f)
                .C(Move.NPCTransportation, 3f).C(Move.OffScreenMovement, 1f).C(Move.BetweenRooms, 5f).C(Move.Slope, 1.5f)
                .C(Move.CeilingSlope, 1.5f);
            var dropBug = New(Type.DropBug, null, Rel.Type.Eats, 1f);
            dropBug.abstractedLaziness = 200;
            dropBug.roamBetweenRoomsChance = 0.025f;
            dropBug.baseDamageResistance = 0.95f;
            dropBug.baseStunResistance = 0.6f;
            dropBug.offScreenSpeed = 0.1f;
            dropBug.AI = true;
            dropBug.requireAImap = true;
            dropBug.bodySize = 1.2f;
            dropBug.stowFoodInDen = true;
            dropBug.shortcutSegments = 2;
            dropBug.preBakedPathingAncestor = blue;
            dropBug.grasps = 1;
            dropBug.visualRadius = 800f;
            dropBug.movementBasedVision = 0.65f;
            dropBug.communityInfluence = 0.1f;
            dropBug.waterRelationship = CreatureTemplate.WaterRelationship.AirAndSurface;
            dropBug.waterPathingResistance = 2f;
            dropBug.canSwim = true;
            dropBug.meatPoints = 3;
            dropBug.dangerousToPlayer = 0.4f;
            dropBug.usesNPCTransportation = true;
        }

        // The tail of InitStaticWorld: indices, pre-baked pathing indices and quantified indices, in type order.
        private void IndexTemplates()
        {
            var preBaked = new List<CreatureTemplate>();
            var quantified = new List<CreatureTemplate>();
            for (int i = 0; i < creatureTemplates.Length; i++)
            {
                var t = creatureTemplates[i];
                if (t == null) continue;
                t.index = i;
                if (t.doPreBakedPathing)
                {
                    t.PreBakedPathingIndex = preBaked.Count;
                    preBaked.Add(t);
                }
                if (t.quantified)
                {
                    t.quantifiedIndex = quantified.Count;
                    quantified.Add(t);
                }
            }
            preBakedPathingCreatures = preBaked.ToArray();
            quantifiedCreatures = quantified.ToArray();
        }

        /// <summary>
        /// The game's <c>StaticWorld.EstablishRelationship</c>: sets how <paramref name="a"/> regards
        /// <paramref name="b"/>, and passes it down to every descendant of either. Types whose DLC is
        /// off are skipped, as the game skips types it has no template for.
        /// </summary>
        private void EstablishRelationship(Type a, Type b, Rel relationship)
        {
            var ta = GetCreatureTemplate(a);
            var tb = GetCreatureTemplate(b);
            if (ta == null || tb == null) return;
            ta.relationships[(int)tb.type] = relationship;
            foreach (var t in creatureTemplates)
            {
                if (t == null) continue;
                if (t.ancestor == ta) EstablishRelationship(t.type, tb.type, relationship);
                if (t.ancestor == tb) EstablishRelationship(ta.type, t.type, relationship);
            }
        }
    }
}
