using System.Collections.Generic;
using NUnit.Framework;
using RWDE.Shell.Terrain;
using RWDE.Sim;

namespace RWDE.Shell.Tests.Terrain
{
    public class TerrainRasterizerTests
    {
        private static readonly TileState Solid = TileState.Solid;
        private static readonly TileState Air = TileState.Air;

        [Test]
        public void SmallDesktopMatchesTileForTile()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("small"));

            Assert.That((terrain.Width, terrain.Height), Is.EqualTo((20, 15)));
            for (int x = 0; x < terrain.Width; x++)
                for (int y = 0; y < terrain.Height; y++)
                {
                    bool taskbar = y <= 1;
                    bool icon = x <= 1 && y >= 13;
                    Assert.That(terrain[x, y], Is.EqualTo(taskbar || icon ? Solid : Air), "tile " + x + "," + y);
                }
        }

        [Test]
        public void SmallDesktopOpensBothSidesAndTheSky()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("small"));

            Assert.That(terrain.Openings, Is.EqualTo(new List<EdgeOpening>
            {
                new EdgeOpening(RoomEdge.Left, 2, 12),
                new EdgeOpening(RoomEdge.Right, 2, 14),
                new EdgeOpening(RoomEdge.Top, 2, 19),
            }));
            Assert.That(terrain.Openings[2].IsSky, Is.True);
            Assert.That(terrain.Openings[0].IsSky, Is.False);
        }

        [Test]
        public void TaskbarAlongTheBottomIsTheFloor()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("uhd-200"));

            Assert.That((terrain.Width, terrain.Height), Is.EqualTo((192, 108)));
            // The 80 px taskbar fills rows 0-3; creatures stand on row 3, in row 4.
            for (int x = 0; x < terrain.Width; x++)
            {
                for (int y = 0; y <= 3; y++)
                    Assert.That(terrain[x, y], Is.EqualTo(Solid), "taskbar tile " + x + "," + y);
                if (x >= 8) Assert.That(terrain[x, 4], Is.EqualTo(Air), "above taskbar " + x);
            }
        }

        [Test]
        public void JitteredIconColumnIsOneUnbrokenBlock()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("uhd-200"));

            for (int y = 4; y < terrain.Height; y++)
                for (int x = 0; x <= 8; x++)
                {
                    bool icon = x <= 7 && y >= 79;
                    Assert.That(terrain[x, y], Is.EqualTo(icon ? Solid : Air), "tile " + x + "," + y);
                }

            Assert.That(terrain.Openings, Is.EqualTo(new List<EdgeOpening>
            {
                new EdgeOpening(RoomEdge.Left, 4, 78),
                new EdgeOpening(RoomEdge.Right, 4, 107),
                new EdgeOpening(RoomEdge.Top, 8, 191),
            }));
        }

        [Test]
        public void SideTaskbarIsAWallAndTheBottomRowStillAFloor()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("left-monitor-left-taskbar"));

            Assert.That((terrain.Width, terrain.Height), Is.EqualTo((96, 54)));
            for (int y = 0; y < terrain.Height; y++)
                for (int x = 0; x <= 3; x++)
                    Assert.That(terrain[x, y], Is.EqualTo(Solid), "taskbar tile " + x + "," + y);
            for (int x = 0; x < terrain.Width; x++)
                Assert.That(terrain[x, 0], Is.EqualTo(Solid), "floor " + x);
            Assert.That(terrain[4, 1], Is.EqualTo(Air));
        }

        [Test]
        public void IconOnAMonitorWithNegativeOriginBlocksItsEdge()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("left-monitor-left-taskbar"));

            for (int x = 92; x <= 95; x++)
                for (int y = 45; y <= 48; y++)
                    Assert.That(terrain[x, y], Is.EqualTo(Solid), "icon tile " + x + "," + y);
            Assert.That(terrain[91, 46], Is.EqualTo(Air));
            Assert.That(terrain[93, 49], Is.EqualTo(Air));

            Assert.That(terrain.Openings, Is.EqualTo(new List<EdgeOpening>
            {
                new EdgeOpening(RoomEdge.Right, 1, 44),
                new EdgeOpening(RoomEdge.Right, 49, 53),
                new EdgeOpening(RoomEdge.Top, 4, 95),
            }));
        }

        [Test]
        public void BareScreenIsAFloorWithOpenSidesAndSky()
        {
            var terrain = TerrainRasterizer.Rasterize(DesktopFixtures.Load("odd-size-no-taskbar"));

            Assert.That((terrain.Width, terrain.Height), Is.EqualTo((21, 16)));
            for (int x = 0; x < terrain.Width; x++)
                for (int y = 0; y < terrain.Height; y++)
                    Assert.That(terrain[x, y], Is.EqualTo(y == 0 ? Solid : Air), "tile " + x + "," + y);

            Assert.That(terrain.Openings, Is.EqualTo(new List<EdgeOpening>
            {
                new EdgeOpening(RoomEdge.Left, 1, 15),
                new EdgeOpening(RoomEdge.Right, 1, 15),
                new EdgeOpening(RoomEdge.Top, 0, 20),
            }));
        }
    }
}
