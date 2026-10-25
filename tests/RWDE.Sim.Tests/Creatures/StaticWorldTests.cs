using System;
using System.Linq;
using NUnit.Framework;
using Type = RWDE.Sim.CreatureTemplate.Type;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;
using RelType = RWDE.Sim.CreatureTemplate.Relationship.Type;

namespace RWDE.Sim.Tests.Creatures
{
    // Expected values are read off StaticWorld.InitStaticWorld, LizardBreeds.BreedTemplate and the
    // relationship tables in the decompiled game.
    public class StaticWorldTests
    {
        private static readonly Type[] SharedDlcTypes = { Type.MirosVulture, Type.SpitLizard, Type.EelLizard, Type.ZoopLizard };

        private static StaticWorld All => StaticWorld.Init(StaticWorld.Dlc.All);

        private static CreatureTemplate Get(Type type) => All.GetCreatureTemplate(type);

        [Test]
        public void FullInstallBuildsEveryRosterType()
        {
            var world = All;
            foreach (Type t in Enum.GetValues(typeof(Type)))
            {
                var template = world.GetCreatureTemplate(t);
                Assert.IsNotNull(template, t.ToString());
                Assert.AreEqual(t, template.type);
                Assert.AreEqual((int)t, template.index);
            }
        }

        [Test]
        public void BaseGameLeavesOutSharedDlcTypes()
        {
            var world = StaticWorld.Init(StaticWorld.Dlc.BaseOnly);
            foreach (var t in SharedDlcTypes)
                Assert.IsNull(world.GetCreatureTemplate(t), t.ToString());
            Assert.IsNotNull(world.GetCreatureTemplate(Type.KingVulture));
            Assert.IsFalse(world.CreatureTemplates.Any(t => SharedDlcTypes.Contains(t.type)));
        }

        [Test]
        public void SharedTypesComeWithEitherDlc()
        {
            foreach (var dlc in new[] { new StaticWorld.Dlc(true, false), new StaticWorld.Dlc(false, true) })
            {
                var world = StaticWorld.Init(dlc);
                foreach (var t in SharedDlcTypes)
                    Assert.IsNotNull(world.GetCreatureTemplate(t), t + " with " + dlc.Downpour + "/" + dlc.Watcher);
            }
        }

        [Test]
        public void NamesAndAncestorsMatchTheGame()
        {
            Assert.AreEqual("Pink Lizard", Get(Type.PinkLizard).name);
            Assert.AreEqual("Caramel Lizard", Get(Type.SpitLizard).name);
            Assert.AreEqual("Strawberry", Get(Type.ZoopLizard).name);
            Assert.AreEqual(Type.LizardTemplate, Get(Type.EelLizard).TopAncestor().type);
            Assert.AreEqual(Type.TentaclePlant, Get(Type.PoleMimic).ancestor.type);
            Assert.AreEqual(Type.VultureGrub, Get(Type.Hazer).ancestor.type);
            Assert.AreEqual(Type.BigNeedleWorm, Get(Type.SmallNeedleWorm).ancestor.type);
            Assert.IsNull(Get(Type.MirosVulture).ancestor, "the Miros Vulture is its own template, not a Vulture descendant");
            Assert.IsTrue(Get(Type.CyanLizard).IsLizard);
            Assert.IsTrue(Get(Type.CicadaB).IsCicada);
            Assert.AreEqual("Centiwing", Get(Type.Centiwing).name);
            Assert.AreEqual(Type.BlueLizard, All.GetCreatureTemplate("Blue Lizard").type);
        }

        [Test]
        public void TerrainPreferencesFollowEachBreed()
        {
            var pink = Get(Type.PinkLizard);
            Assert.AreEqual(new PathCost(2.5f, PathCost.Legality.Allowed), pink.AccessibilityResistance(Acc.Climb));
            Assert.IsFalse(pink.AccessibilityResistance(Acc.Wall).Allowed);
            Assert.AreEqual((int)Acc.Climb, pink.maxAccessibleTerrain);

            var green = Get(Type.GreenLizard);
            Assert.AreEqual(new PathCost(2f, PathCost.Legality.Allowed), green.AccessibilityResistance(Acc.Corridor));
            Assert.IsFalse(green.AccessibilityResistance(Acc.Climb).Allowed);
            Assert.AreEqual(60f, green.ConnectionResistance(Move.LizardTurn).resistance);
            Assert.AreEqual(10f, pink.ConnectionResistance(Move.LizardTurn).resistance, "inherited from the lizard template");

            var blue = Get(Type.BlueLizard);
            Assert.IsTrue(blue.AccessibilityResistance(Acc.Wall).Allowed);
            Assert.IsTrue(blue.AccessibilityResistance(Acc.Ceiling).Allowed);
            Assert.AreEqual(15f, blue.ConnectionResistance(Move.ShortCut).resistance);

            Assert.IsTrue(Get(Type.Fly).AccessibilityResistance(Acc.Air).Allowed);
            // Everything easier than air is clamped to air's cost, so batflies may use any open tile.
            Assert.AreEqual(new PathCost(1f, PathCost.Legality.Allowed), Get(Type.Fly).AccessibilityResistance(Acc.Floor));
            Assert.IsFalse(Get(Type.Fly).AccessibilityResistance(Acc.Solid).Allowed);
            Assert.AreEqual(new PathCost(50f, PathCost.Legality.Allowed), Get(Type.EggBug).AccessibilityResistance(Acc.Wall));
            Assert.AreEqual(PathCost.Legality.Unwanted, Get(Type.EggBug).ConnectionResistance(Move.DropToClimb).legality);
        }

        [Test]
        public void EasierTerrainNeverCostsMoreThanTheHardestAllowed()
        {
            // The Strawberry Lizard asks 4 for floors but only 0.2 for climbing, its hardest terrain.
            var zoop = Get(Type.ZoopLizard);
            Assert.AreEqual((int)Acc.Climb, zoop.maxAccessibleTerrain);
            Assert.AreEqual(0.2f, zoop.AccessibilityResistance(Acc.Floor).resistance);
            Assert.AreEqual(0.2f, zoop.AccessibilityResistance(Acc.OffScreen).resistance);
        }

        [Test]
        public void UnsetTerrainIsIllegalAndGetsDearerWithDifficulty()
        {
            var overseer = Get(Type.Overseer);
            Assert.AreEqual(new PathCost(70f, PathCost.Legality.IllegalTile), overseer.AccessibilityResistance(Acc.Air));
            Assert.AreEqual(new PathCost(100f, PathCost.Legality.SolidTile), overseer.AccessibilityResistance(Acc.Solid));
            Assert.AreEqual(new PathCost(100f, PathCost.Legality.IllegalConnection), overseer.ConnectionResistance(Move.Standard));
            Assert.AreEqual(0, Get(Type.CicadaB).maxAccessibleTerrain, "the game builds Cicada B from empty lists");
        }

        [Test]
        public void ShortcutUseFollowsTheGame()
        {
            Assert.IsTrue(Get(Type.Vulture).forbidStandardShortcutEntry);
            Assert.IsFalse(Get(Type.Vulture).ConnectionResistance(Move.ShortCut).Allowed);
            Assert.IsTrue(Get(Type.KingVulture).forbidStandardShortcutEntry);
            Assert.IsFalse(Get(Type.PinkLizard).forbidStandardShortcutEntry);
            Assert.IsTrue(Get(Type.PinkLizard).ConnectionResistance(Move.ShortCut).Allowed);

            // The game copies forbidStandardShortcutEntry to descendants only with Downpour on.
            var baseOnly = StaticWorld.Init(StaticWorld.Dlc.BaseOnly);
            Assert.IsFalse(baseOnly.GetCreatureTemplate(Type.KingVulture).forbidStandardShortcutEntry);
            Assert.IsTrue(baseOnly.GetCreatureTemplate(Type.Vulture).forbidStandardShortcutEntry);
        }

        [Test]
        public void MappedNodeTypesFollowMovementCosts()
        {
            var vulture = Get(Type.Vulture);
            Assert.IsTrue(vulture.mappedNodeTypes[(int)AbstractRoomNode.Type.SkyExit]);
            Assert.IsFalse(vulture.mappedNodeTypes[(int)AbstractRoomNode.Type.Exit]);
            Assert.IsTrue(vulture.UseAnyRoomBorderExit);

            var pink = Get(Type.PinkLizard);
            Assert.IsTrue(pink.mappedNodeTypes[(int)AbstractRoomNode.Type.Exit]);
            Assert.IsTrue(pink.mappedNodeTypes[(int)AbstractRoomNode.Type.Den]);
            Assert.IsFalse(pink.UseAnyRoomBorderExit);

            var scavenger = Get(Type.Scavenger);
            Assert.IsFalse(scavenger.mappedNodeTypes[(int)AbstractRoomNode.Type.Den]);
            Assert.IsTrue(scavenger.mappedNodeTypes[(int)AbstractRoomNode.Type.RegionTransportation]);
            Assert.IsTrue(Get(Type.Fly).mappedNodeTypes[(int)AbstractRoomNode.Type.BatHive]);
            Assert.IsTrue(Get(Type.MirosVulture).mappedNodeTypes[(int)AbstractRoomNode.Type.SideExit]);
        }

        [Test]
        public void PreBakedPathingIsSharedAsInTheGame()
        {
            var world = All;
            int Index(Type t) => world.GetCreatureTemplate(t).PreBakedPathingIndex;
            Assert.AreEqual(Index(Type.BlueLizard), Index(Type.CyanLizard));
            Assert.AreEqual(Index(Type.PinkLizard), Index(Type.YellowLizard));
            Assert.AreEqual(Index(Type.PinkLizard), Index(Type.RedLizard));
            Assert.AreEqual(Index(Type.PinkLizard), Index(Type.ZoopLizard));
            Assert.AreEqual(Index(Type.GreenLizard), Index(Type.SpitLizard));
            Assert.AreEqual(Index(Type.Salamander), Index(Type.EelLizard));
            Assert.AreEqual(Index(Type.BlueLizard), Index(Type.Centipede));
            Assert.AreEqual(Index(Type.Fly), Index(Type.Centiwing));
            Assert.AreEqual(Index(Type.Fly), Index(Type.SmallNeedleWorm));
            Assert.AreEqual(Index(Type.WhiteLizard), Index(Type.EggBug));
            Assert.AreEqual(Index(Type.GreenLizard), Index(Type.Hazer));
            Assert.AreEqual(Index(Type.Vulture), Index(Type.MirosVulture));
            Assert.AreEqual(-1, Index(Type.Overseer));
            Assert.AreEqual(-1, Index(Type.TentaclePlant));
            Assert.AreEqual(-1, Index(Type.PoleMimic));
            Assert.AreEqual(-1, Index(Type.LizardTemplate));
            CollectionAssert.AreEqual(
                new[] { Type.StandardGroundCreature, Type.PinkLizard, Type.GreenLizard, Type.BlueLizard, Type.WhiteLizard,
                        Type.Salamander, Type.Fly, Type.Vulture, Type.Scavenger },
                world.preBakedPathingCreatures.Select(t => t.type));
            CollectionAssert.AreEqual(new[] { Type.Fly }, world.quantifiedCreatures.Select(t => t.type));
        }

        [Test]
        public void StatsComeFromBreedParameters()
        {
            Assert.AreEqual(2f, Get(Type.PinkLizard).baseDamageResistance, "toughness 1 * 2");
            Assert.AreEqual(6f, Get(Type.RedLizard).baseDamageResistance);
            Assert.AreEqual(3f, Get(Type.RedLizard).bodySize);
            Assert.AreEqual(1.9f, Get(Type.RedLizard).damageRestistances[(int)CreatureTemplate.DamageType.Explosion, 0]);
            Assert.AreEqual(0.8f, Get(Type.EelLizard).baseStunResistance, "the Eel Lizard's stun resistance is its toughness");
            Assert.AreEqual(4f, Get(Type.EelLizard).bodySize);
            Assert.AreEqual(0.45f, Get(Type.GreenLizard).dangerousToPlayer);
            Assert.AreEqual(0.7f, Get(Type.GreenLizard).movementBasedVision);
            Assert.AreEqual(0.3f, Get(Type.PinkLizard).movementBasedVision);
            Assert.AreEqual(CreatureTemplate.WaterRelationship.Amphibious, Get(Type.Salamander).waterRelationship);
            Assert.AreEqual(CreatureTemplate.CommunityID.Lizards, Get(Type.ZoopLizard).communityID);
            Assert.AreEqual((1f, 0f, 0f), Get(Type.RedCentipede).shortcutColor);
            Assert.AreEqual(5, Get(Type.RedCentipede).shortcutSegments);
            Assert.IsNotNull(Get(Type.Vulture).isTooCloseToTerrain);
        }

        [Test]
        public void RelationshipsAreSetAndPassedToDescendants()
        {
            AssertRel(Get(Type.GreenLizard), Type.BlueLizard, RelType.Eats, 0.25f);
            AssertRel(Get(Type.PinkLizard), Type.Vulture, RelType.Afraid, 0.9f); // LizardTemplate -> Vulture
            AssertRel(Get(Type.GreenLizard), Type.KingVulture, RelType.Afraid, 0.5f); // overrides the lizard default
            AssertRel(Get(Type.Vulture), Type.PinkLizard, RelType.Eats, 0.5f); // Vulture -> LizardTemplate
            AssertRel(Get(Type.Vulture), Type.GreenLizard, RelType.Ignores, 0f);
            AssertRel(Get(Type.KingVulture), Type.GreenLizard, RelType.Eats, 0.55f);
            AssertRel(Get(Type.CicadaB), Type.Fly, RelType.Eats, 0.4f);
            AssertRel(Get(Type.CicadaB), Type.LizardTemplate, RelType.Afraid, 0.3f); // from CicadaA
            AssertRel(Get(Type.YellowLizard), Type.YellowLizard, RelType.Pack, 0.2f);
            AssertRel(Get(Type.SpitLizard), Type.GreenLizard, RelType.AgressiveRival, 0.8f);
            AssertRel(Get(Type.SpitLizard), Type.Vulture, RelType.Afraid, 0.9f);
            AssertRel(Get(Type.BigSpider), Type.MirosVulture, RelType.Afraid, 1f);
            AssertRel(Get(Type.Fly), Type.Scavenger, RelType.Uncomfortable, 0f); // default
        }

        [Test]
        public void CyanLizardsEatAtThirtyPercent()
        {
            AssertRel(Get(Type.CyanLizard), Type.BlueLizard, RelType.Eats, 0.15f * 0.3f);
            AssertRel(Get(Type.CyanLizard), Type.Scavenger, RelType.Eats, 0.8f * 0.3f);
            AssertRel(Get(Type.CyanLizard), Type.CyanLizard, RelType.AgressiveRival, 0.075f);
        }

        [Test]
        public void EggBugsFearCreaturesBySize()
        {
            var eggBug = Get(Type.EggBug);
            AssertRel(eggBug, Type.RedCentipede, RelType.Afraid, 1f);
            AssertRel(eggBug, Type.PinkLizard, RelType.Afraid, 2f / 8f);
            AssertRel(eggBug, Type.Fly, RelType.Ignores, 0f);
            // The Infant Noodlefly is registered before its ancestor, so it keeps the adult's value.
            AssertRel(eggBug, Type.SmallNeedleWorm, RelType.Afraid, 0.7f / 8f);
        }

        [Test]
        public void EveryoneIgnoresOverseersButScavengers()
        {
            AssertRel(Get(Type.PinkLizard), Type.Overseer, RelType.Ignores, 0f);
            AssertRel(Get(Type.BigSpider), Type.Overseer, RelType.Ignores, 0f);
            AssertRel(Get(Type.Scavenger), Type.Overseer, RelType.Attacks, 1f);
        }

        [Test]
        public void DlcRelationshipsDoNotChangeBaseOnes()
        {
            var baseOnly = StaticWorld.Init(StaticWorld.Dlc.BaseOnly);
            var all = All;
            foreach (var t in baseOnly.CreatureTemplates)
                foreach (var other in baseOnly.CreatureTemplates)
                    Assert.AreEqual(t.CreatureRelationship(other.type), all.GetCreatureTemplate(t.type).CreatureRelationship(other.type),
                        t.name + " -> " + other.name);
        }

        private static void AssertRel(CreatureTemplate t, Type other, RelType type, float intensity)
        {
            var r = t.CreatureRelationship(other);
            Assert.AreEqual(type, r.type, t.name + " -> " + other);
            Assert.AreEqual(intensity, r.intensity, 1e-6f, t.name + " -> " + other);
        }
    }
}
