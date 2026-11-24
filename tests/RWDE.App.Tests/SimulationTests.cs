using System.Collections.Generic;
using NUnit.Framework;
using RWDE.Graphics;
using RWDE.Sim;

namespace RWDE.App.Tests
{
    public class SimulationTests
    {
        sealed class QueuedTerrain : ITerrainSource
        {
            public readonly Queue<TerrainDiff> Diffs = new Queue<TerrainDiff>();
            public int Taken;

            public int Width => 8;
            public int Height => 6;
            public TileState GetTile(int x, int y) => y == 0 ? TileState.Solid : TileState.Air;
            public IEnumerable<Pipe> Pipes => new Pipe[0];

            public bool TryTakeDiff(out TerrainDiff diff)
            {
                if (Diffs.Count == 0)
                {
                    diff = null;
                    return false;
                }
                Taken++;
                diff = Diffs.Dequeue();
                return true;
            }
        }

        [Test]
        public void RoomStartsFromTheTerrainSnapshot()
        {
            var sim = new Simulation(new QueuedTerrain(), null, null, null, 1);

            Assert.AreEqual(8, sim.Room.Width);
            Assert.AreEqual(6, sim.Room.Height);
            Assert.IsTrue(sim.Room.GetTile(3, 0).Solid);
            Assert.IsFalse(sim.Room.GetTile(3, 1).Solid);
            Assert.AreEqual(1, sim.World.NumberOfRooms);
            Assert.IsNull(sim.Camera);
        }

        [Test]
        public void TickAppliesEveryPendingDiffAndReportsTheChange()
        {
            var terrain = new QueuedTerrain();
            var sim = new Simulation(terrain, null, null, null, 1);

            Assert.IsFalse(sim.Tick(), "no diffs, no change");

            terrain.Diffs.Enqueue(new TerrainDiff().SetTile(2, 3, TileState.Solid));
            terrain.Diffs.Enqueue(new TerrainDiff().SetTile(4, 3, TileState.Solid));
            Assert.IsTrue(sim.Tick());
            Assert.AreEqual(2, terrain.Taken, "every pending diff is taken in the same tick");
            Assert.IsTrue(sim.Room.GetTile(2, 3).Solid);
            Assert.IsTrue(sim.Room.GetTile(4, 3).Solid);
            Assert.AreEqual(2, sim.Ticks);
        }

        [Test]
        public void ADiffThatChangesNothingLeavesThePictureAlone()
        {
            var terrain = new QueuedTerrain();
            var sim = new Simulation(terrain, null, null, null, 1);
            terrain.Diffs.Enqueue(new TerrainDiff().SetTile(2, 0, TileState.Solid));
            Assert.IsFalse(sim.Tick());
        }

        [Test]
        public void AnEmptyRoomIsNotAnimating()
        {
            var sim = new Simulation(new QueuedTerrain(), null, null, null, 1, new FStage());

            Assert.IsNotNull(sim.Camera);
            sim.Tick();
            sim.DrawUpdate(0.5f);
            Assert.IsFalse(sim.Animating);
        }

        [Test]
        public void SpawnDataGivesTheWorldAPopulation()
        {
            var data = new RegionSpawnData("xx", new[]
            {
                "ROOMS",
                "END ROOMS",
                "CREATURES",
                "OFFSCREEN : 0-Green-3",
                "END CREATURES",
            });
            var sim = new Simulation(new QueuedTerrain(), StaticWorld.Init(StaticWorld.Dlc.All), data,
                Campaign.AllOutskirtsCreatures, 1);

            Assert.IsNotNull(sim.World.SpawnTable);
            Assert.IsNotEmpty(sim.World.Creatures);
            sim.Tick();
            Assert.AreEqual(1, sim.Ticks);
        }

        [Test]
        public void ABaseOnlyWorldUsesOnlyBaseCampaigns()
        {
            var data = new RegionSpawnData("xx", new[]
            {
                "ROOMS",
                "END ROOMS",
                "CREATURES",
                "(Gourmand)OFFSCREEN : 0-Green-3",
                "END CREATURES",
            });

            var all = new Simulation(new QueuedTerrain(), StaticWorld.Init(StaticWorld.Dlc.BaseOnly), data,
                Campaign.AllOutskirtsCreatures, 1);
            var gourmand = new Simulation(new QueuedTerrain(), StaticWorld.Init(StaticWorld.Dlc.BaseOnly), data,
                "Gourmand", 1);

            CollectionAssert.AreEqual(new[] { Campaign.White, Campaign.Yellow, Campaign.Red }, all.World.SpawnTable.Campaigns);
            Assert.IsEmpty(all.World.Creatures, "the Gourmand-only line must not spawn without Downpour");
            Assert.AreEqual(Campaign.AllOutskirtsCreatures, gourmand.CampaignSetting);
            Assert.IsEmpty(gourmand.World.Creatures);
        }
    }
}
