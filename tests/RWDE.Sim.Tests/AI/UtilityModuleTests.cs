using NUnit.Framework;
using Type = RWDE.Sim.CreatureTemplate.Type;

namespace RWDE.Sim.Tests.AI
{
    // Expected values are worked out from the game's UtilityComparer, FloatTweener, StuckTracker,
    // MissionTracker, RainTracker and DenFinder.
    public class UtilityModuleTests
    {
        private static readonly StaticWorld Templates = StaticWorld.Init(StaticWorld.Dlc.All);

        private sealed class TestAI : ArtificialIntelligence
        {
            public TestAI(AbstractCreature creature) : base(creature)
            {
            }
        }

        private sealed class FixedModule : AIModule
        {
            public FixedModule(ArtificialIntelligence AI, float utility) : base(AI)
            {
                this.utility = utility;
            }

            public float utility;
            public override float Utility() => utility;
        }

        private static TestAI MakeAI(World world = null)
        {
            var room = new Room(10, 10);
            world ??= new World(1, room);
            var creature = new AbstractCreature(world, Templates.GetCreatureTemplate(Type.GreenLizard), world.GetAbstractRoom(0).realizedRoom.GetWorldCoordinate(new IntVector2(2, 2)));
            return new TestAI(creature);
        }

        // ---- FloatTweener ----

        [Test]
        public void BasicTweensLerpOrStepWithoutOvershooting()
        {
            Assert.AreEqual(0.5f, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.5f).Tween(0f, 1f), 1e-6f);
            Assert.AreEqual(0.3f, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.3f).Tween(0f, 1f), 1e-6f);
            Assert.AreEqual(1f, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.3f).Tween(0.9f, 1f), 1e-6f);
            Assert.AreEqual(0.7f, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.3f).Tween(1f, 0f), 1e-6f);
            Assert.AreEqual(1f, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.None, 0.3f).Tween(0f, 1f), 1e-6f);
        }

        [Test]
        public void UpAndDownTweenUsesTheRisingOrFallingHalf()
        {
            var tween = new FloatTweener.FloatTweenUpAndDown(
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.5f),
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.1f));
            Assert.AreEqual(0.5f, tween.Tween(0f, 1f), 1e-6f);
            Assert.AreEqual(0.9f, tween.Tween(1f, 0f), 1e-6f);
        }

        // ---- UtilityComparer ----

        [Test]
        public void ComparerPicksTheHighestWeightedUtility()
        {
            var ai = MakeAI();
            var comparer = new UtilityComparer(ai);
            var a = new FixedModule(ai, 0.8f);
            var b = new FixedModule(ai, 0.5f);
            comparer.AddComparedModule(a, null, 0.5f, 1f); // 0.4
            comparer.AddComparedModule(b, null, 1f, 1f);   // 0.5
            comparer.Update();
            Assert.AreSame(b, comparer.HighestUtilityModule());
            Assert.AreEqual(0.5f, comparer.HighestUtility(), 1e-6f);
            Assert.AreEqual(0.8f, comparer.GetSmoothedNonWeightedUtility(a), 1e-6f);
        }

        [Test]
        public void ComparerSmoothsUtilityOverTicks()
        {
            var ai = MakeAI();
            var comparer = new UtilityComparer(ai);
            var a = new FixedModule(ai, 1f);
            comparer.AddComparedModule(a, new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.15f), 1f, 1f);
            comparer.Update();
            Assert.AreEqual(0.15f, comparer.HighestUtility(), 1e-6f);
            comparer.Update();
            Assert.AreEqual(0.15f + 0.85f * 0.15f, comparer.HighestUtility(), 1e-6f);
            comparer.GetUtilityTracker(a).weight = 2f;
            Assert.AreEqual((0.15f + 0.85f * 0.15f) / 2f, comparer.GetSmoothedNonWeightedUtility(a), 1e-6f);
        }

        [Test]
        public void ComparerWithNoModulesReportsNothing()
        {
            var comparer = new UtilityComparer(MakeAI());
            comparer.Update();
            Assert.IsNull(comparer.HighestUtilityModule());
            Assert.AreEqual(0f, comparer.HighestUtility());
        }

        // ---- StuckTracker.MoveBacklog ----

        private static MovementConnection Step(int x0, int x1) =>
            new MovementConnection(MovementConnection.MovementType.Standard, new WorldCoordinate(0, x0, 1, -1), new WorldCoordinate(0, x1, 1, -1), 1);

        [Test]
        public void MoveBacklogKeepsDistinctRecentMovesNewestFirst()
        {
            var stuck = new StuckTracker(MakeAI(), true, true);
            stuck.AddSubModule(new StuckTracker.MoveBacklog(stuck));
            var log = stuck.moveBacklog;
            log.ReportNewMove(Step(1, 2));
            log.ReportNewMove(Step(1, 2)); // a repeat of the newest is not logged again
            log.ReportNewMove(default);    // nor an empty move
            log.ReportNewMove(Step(2, 3));
            Assert.AreEqual(2, log.log.Count);
            Assert.AreEqual(Step(2, 3), log.log[0]);
            Assert.IsTrue(log.IsMoveInLog(Step(1, 2), 2));
            Assert.IsFalse(log.IsMoveInLog(Step(1, 2), 1));
            Assert.IsFalse(log.IsMoveInLog(default, 10));

            for (int i = 3; i < 20; i++)
                log.ReportNewMove(Step(i, i + 1));
            Assert.AreEqual(10, log.log.Count);
            Assert.IsFalse(log.IsMoveInLog(Step(1, 2), 10));
        }

        [Test]
        public void StuckUtilityRampsBetweenTheCounterLimits()
        {
            var stuck = new StuckTracker(MakeAI(), false, false);
            Assert.AreEqual(0f, stuck.Utility());
            stuck.stuckCounter = 150;
            Assert.AreEqual(0.5f, stuck.Utility(), 1e-6f);
            stuck.Reset();
            Assert.AreEqual(0, stuck.stuckCounter);
        }

        // ---- MissionTracker ----

        [Test]
        public void MissionUtilityIsTheStrongestMissionsAndExpires()
        {
            var ai = MakeAI();
            var missions = new MissionTracker(ai);
            missions.AddMission(new MissionTracker.LeaveRoom(missions, 0.4f, 3), false);
            missions.Update();
            Assert.AreEqual(0.4f, missions.Utility(), 1e-6f);
            Assert.IsInstanceOf<MissionTracker.LeaveRoom>(missions.mostRelevantMission);
            missions.Update(); // forget counter 3 -> 1
            missions.Update(); // -> 0: abandoned, removed
            Assert.AreEqual(0f, missions.Utility());
            Assert.AreEqual(0, missions.missions.Count);
        }

        [Test]
        public void AStrongerMissionOfTheSameTypeKeepsANewOneOut()
        {
            var ai = MakeAI();
            var missions = new MissionTracker(ai);
            missions.AddMission(new MissionTracker.LeaveRoom(missions, 0.4f, 1000), false);
            // Before any update the tracker's utility is 0, so the existing mission outranks it.
            missions.AddMission(new MissionTracker.LeaveRoom(missions, 0.9f, 1000), false);
            Assert.AreEqual(1, missions.missions.Count);
            Assert.AreEqual(0.4f, missions.missions[0].baseUtility, 1e-6f);
            missions.AddMission(new MissionTracker.LeaveRoom(missions, 0.9f, 1000), true);
            Assert.AreEqual(2, missions.missions.Count);
        }

        [Test]
        public void LeavingARoomWithoutExitsIsAbandoned()
        {
            var ai = MakeAI();
            var missions = new MissionTracker(ai);
            var leave = new MissionTracker.LeaveRoom(missions, 0.4f, 1000);
            missions.AddMission(leave, false);
            missions.Update();
            missions.ActOnMostImportantMission();
            Assert.IsTrue(leave.deleteMeNextFrame);
        }

        // ---- RainTracker and DenFinder ----

        [Test]
        public void RainUtilityRisesAsTheRainNears()
        {
            var ai = MakeAI();
            var rain = new RainTracker(ai);
            Assert.AreEqual(0f, rain.Utility(), 1e-6f, "no rain cycle yet");
            ai.creature.world.timeUntilRain = 0;
            Assert.AreEqual(1f, rain.Utility(), 1e-6f);
            ai.creature.world.timeUntilRain = 2400;
            float mid = rain.Utility();
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
            ai.creature.nightCreature = true;
            Assert.AreEqual(0f, rain.Utility());
        }

        [Test]
        public void ADesktopRoomHasNoDenToFind()
        {
            var ai = MakeAI();
            var dens = new DenFinder(ai, ai.creature);
            Assert.AreEqual(NodeFinder.Status.Working, dens.status);
            dens.Update();
            Assert.AreEqual(NodeFinder.Status.NoAccessible, dens.status);
            Assert.IsNull(dens.GetDenPosition());
        }
    }
}
