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

        [Test]
        public void SeamRoundsTheSameWayOnNegativeCoordinates()
        {
            // Monitors left of the main one have negative x. Two rows whose seam falls half-way between
            // pixels 159 and 160 must carve the same tile column wherever the screen sits.
            DesktopLayout At(int dx)
            {
                var icons = new List<ScreenRect>();
                for (int y = 40; y < 160; y += 60)
                {
                    icons.Add(new ScreenRect(99 + dx, y, 159 + dx, y + 60));
                    icons.Add(new ScreenRect(160 + dx, y, 220 + dx, y + 60));
                }
                return new DesktopLayout(new ScreenRect(dx, 0, 400 + dx, 400), null, icons);
            }

            var home = At(0);
            var left = At(-400);

            Assert.That(IconTunnelDetector.Detect(left.Icons)[0].SeamX, Is.EqualTo(IconTunnelDetector.Detect(home.Icons)[0].SeamX - 400));
            Assert.That(TerrainRasterizer.Rasterize(left).Tunnels, Is.EqualTo(TerrainRasterizer.Rasterize(home).Tunnels));
        }

        [Test]
        public void ATunnelSplitIntoTwoRunsIsCarvedWhole()
        {
            // Four rows of pairs down to the floor; the top row's seam is 11 px off the others, so the
            // rows form two runs that both fall in tile column 8. Each alone looks closed at both ends
            // (floor below, the other run's icons above), but together they are one tunnel.
            var icons = new List<ScreenRect>();
            int[] seams = { 182, 172, 171, 171 };
            for (int row = 0; row < 4; row++)
            {
                int y = 160 + 60 * row;
                icons.Add(new ScreenRect(seams[row] - 60, y, seams[row], y + 60));
                icons.Add(new ScreenRect(seams[row], y, seams[row] + 60, y + 60));
            }
            var layout = new DesktopLayout(new ScreenRect(0, 0, 400, 400), null, icons);

            Assert.That(IconTunnelDetector.Detect(layout.Icons), Has.Count.EqualTo(2), "two runs");
            var terrain = TerrainRasterizer.Rasterize(layout);

            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 1, 11) }));
            for (int y = 1; y <= 11; y++)
                Assert.That(terrain[8, y], Is.EqualTo(Air), "tile 8," + y);
        }

        [Test]
        public void WallsNeverFillAnotherTunnel()
        {
            // Four rows of pairs, each 10 px right and 50 px down from the one above: two runs whose
            // tunnels sit in neighbouring columns and share row 12, (8, 12..17) and (9, 7..12).
            var icons = new List<ScreenRect>();
            for (int row = 0; row < 4; row++)
            {
                int dx = 10 * row, y = 40 + 50 * row;
                icons.Add(new ScreenRect(100 + dx, y, 160 + dx, y + 60));
                icons.Add(new ScreenRect(160 + dx, y, 220 + dx, y + 60));
            }
            var terrain = TerrainRasterizer.Rasterize(new DesktopLayout(new ScreenRect(0, 0, 400, 400), null, icons));

            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 12, 17), new TunnelTiles(9, 7, 12) }));
            foreach (var tunnel in terrain.Tunnels)
                for (int y = tunnel.Bottom; y <= tunnel.Top; y++)
                    Assert.That(terrain[tunnel.X, y], Is.EqualTo(Air), "tile " + tunnel.X + "," + y);
        }

        [Test]
        public void AWallThatSealsAnotherTunnelDropsIt()
        {
            // A run of four pairs down to the floor with its seam in tile column 9, (9, 1..11), and above
            // it a run of two pairs with its seam in column 8, (8, 12..18). The upper right icons stop
            // short of tile (9, 12), so the lower tunnel looks open at the top until the upper tunnel's
            // right wall fills that tile. With the floor below, it can't be entered and stays solid.
            var icons = new List<ScreenRect>
            {
                new ScreenRect(110, 35, 170, 95), new ScreenRect(170, 26, 230, 86),
                new ScreenRect(110, 95, 170, 155), new ScreenRect(170, 86, 230, 146),
            };
            for (int y = 160; y < 400; y += 60)
            {
                icons.Add(new ScreenRect(130, y, 190, y + 60));
                icons.Add(new ScreenRect(190, y, 250, y + 60));
            }
            var terrain = TerrainRasterizer.Rasterize(new DesktopLayout(new ScreenRect(0, 0, 400, 400), null, icons));

            Assert.That(terrain.Tunnels, Is.EqualTo(new List<TunnelTiles> { new TunnelTiles(8, 12, 18) }));
            for (int y = 1; y <= 12; y++)
                Assert.That(terrain[9, y], Is.EqualTo(Solid), "tile 9," + y);
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
