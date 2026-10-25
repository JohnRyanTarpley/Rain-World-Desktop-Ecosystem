using System.Collections.Generic;
using NUnit.Framework;
using RWDE.Shell.Desktop;
using RWDE.Shell.Terrain;
using RWDE.Sim;

namespace RWDE.Shell.Tests.Terrain
{
    public class IconTunnelTests
    {
        private static readonly TileState Solid = TileState.Solid;
        private static readonly TileState Air = TileState.Air;

        // The tunnel fixtures put 60 px icons in tile columns 5-7 (left), 8-10 (middle) and 11-13
        // (right), and in tile rows 15-17, 12-14 and 9-11 from the top down.

        [Test]
        public void TwoWideStackIsATunnelDownTheSeam()
        {
            var layout = DesktopFixtures.Load("tunnel-two-wide");

            Assert.That(IconTunnelDetector.Detect(layout.Icons), Is.EqualTo(new List<IconTunnel>
            {
                new IconTunnel(160, 40, 221, 3),
            }));

            var terrain = TerrainRasterizer.Rasterize(layout);
            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 9, 17) }));
            for (int y = 9; y <= 17; y++)
                for (int x = 5; x <= 10; x++)
                    Assert.That(terrain[x, y], Is.EqualTo(x == 8 ? Air : Solid), "tile " + x + "," + y);

            // Open at both ends, so creatures can crawl in from above and below.
            Assert.That(terrain[8, 8], Is.EqualTo(Air));
            Assert.That(terrain[8, 18], Is.EqualTo(Air));
        }

        [Test]
        public void SingleColumnIsASolidPillar()
        {
            var layout = DesktopFixtures.Load("tunnel-single-column");

            Assert.That(IconTunnelDetector.Detect(layout.Icons), Is.Empty);

            var terrain = TerrainRasterizer.Rasterize(layout);
            Assert.That(terrain.Tunnels, Is.Empty);
            for (int y = 9; y <= 17; y++)
                for (int x = 4; x <= 8; x++)
                    Assert.That(terrain[x, y], Is.EqualTo(x >= 5 && x <= 7 ? Solid : Air), "tile " + x + "," + y);
        }

        [Test]
        public void ThreeWideBlockIsSolidWithNoTunnel()
        {
            var layout = DesktopFixtures.Load("tunnel-three-wide");

            Assert.That(IconTunnelDetector.Detect(layout.Icons), Is.Empty);

            var terrain = TerrainRasterizer.Rasterize(layout);
            Assert.That(terrain.Tunnels, Is.Empty);
            for (int y = 9; y <= 17; y++)
                for (int x = 5; x <= 13; x++)
                    Assert.That(terrain[x, y], Is.EqualTo(Solid), "tile " + x + "," + y);
        }

        [Test]
        public void SingleRowPairIsTooShortForATunnel()
        {
            var layout = DesktopFixtures.Load("tunnel-too-short");

            Assert.That(IconTunnelDetector.Detect(layout.Icons), Is.Empty);

            var terrain = TerrainRasterizer.Rasterize(layout);
            Assert.That(terrain.Tunnels, Is.Empty);
            for (int y = 14; y <= 18; y++)
                for (int x = 5; x <= 10; x++)
                    Assert.That(terrain[x, y], Is.EqualTo(y >= 15 && y <= 17 ? Solid : Air), "tile " + x + "," + y);
        }

        [Test]
        public void JitterDoesNotAddUpAlongAStaircase()
        {
            // Fourteen rows of two 60 px icons, each row 10 px (the jitter) right of the one above. Next
            // to each other the rows line up, but the bottom row sits 130 px right of the top one, so
            // they can't be one column: only rows within the jitter of a run's first row join it.
            var icons = new List<ScreenRect>();
            for (int row = 0; row < 14; row++)
            {
                int dx = 10 * row, y = 20 + 60 * row;
                icons.Add(new ScreenRect(100 + dx, y, 160 + dx, y + 60));
                icons.Add(new ScreenRect(160 + dx, y, 220 + dx, y + 60));
            }

            var expected = new List<IconTunnel>();
            for (int run = 0; run < 7; run++)
                expected.Add(new IconTunnel(165 + 20 * run, 20 + 120 * run, 140 + 120 * run, 2));
            Assert.That(IconTunnelDetector.Detect(icons), Is.EqualTo(expected));
        }

        // These fixtures put the icon columns in tile rows 0-8 from the bottom up.

        [Test]
        public void TunnelStopsAboveTheFloorRow()
        {
            var layout = DesktopFixtures.Load("tunnel-on-floor");

            var terrain = TerrainRasterizer.Rasterize(layout);

            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 1, 8) }));
            Assert.That(terrain[8, 0], Is.EqualTo(Solid), "the floor stays whole");
            Assert.That(terrain[8, 9], Is.EqualTo(Air), "open at the top");
            Assert.That(terrain.Openings, Has.None.Matches<EdgeOpening>(o => o.Edge == RoomEdge.Bottom));
        }

        [Test]
        public void TunnelStopsAboveTheTaskbar()
        {
            var layout = DesktopFixtures.Load("tunnel-over-taskbar");

            var terrain = TerrainRasterizer.Rasterize(layout);

            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 2, 8) }));
            Assert.That(terrain[8, 0], Is.EqualTo(Solid));
            Assert.That(terrain[8, 1], Is.EqualTo(Solid), "the taskbar stays whole");
        }

        [Test]
        public void TunnelBlockedAtOneEndIsADeadEnd()
        {
            var layout = DesktopFixtures.Load("tunnel-dead-end");

            var terrain = TerrainRasterizer.Rasterize(layout);

            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 9, 17) }));
            Assert.That(terrain[8, 8], Is.EqualTo(Solid), "the lone icon below closes the bottom");
            Assert.That(terrain[8, 18], Is.EqualTo(Air), "still open at the top");
        }

        [Test]
        public void TunnelBlockedAtBothEndsStaysSolid()
        {
            var layout = DesktopFixtures.Load("tunnel-sealed");

            Assert.That(IconTunnelDetector.Detect(layout.Icons), Has.Count.EqualTo(1), "the pairs are still detected");

            var terrain = TerrainRasterizer.Rasterize(layout);
            Assert.That(terrain.Tunnels, Is.Empty);
            for (int y = 9; y <= 17; y++)
                for (int x = 5; x <= 10; x++)
                    Assert.That(terrain[x, y], Is.EqualTo(Solid), "tile " + x + "," + y);
        }
    }
}
