using System.Collections.Generic;
using NUnit.Framework;
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
    }
}
