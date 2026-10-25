using System.Linq;
using NUnit.Framework;
using RWDE.Shell.Terrain;
using RWDE.Sim;

namespace RWDE.Shell.Tests.Terrain
{
    public class DesktopTerrainSourceTests
    {
        [Test]
        public void ReadsTheDesktopOnceAndServesTheRasterisedTiles()
        {
            var fake = new FakeDesktopLayoutSource(DesktopFixtures.Load("small"));
            var source = new DesktopTerrainSource(fake);

            Assert.That(fake.Reads, Is.EqualTo(1));
            Assert.That((source.Width, source.Height), Is.EqualTo((20, 15)));
            Assert.That(source.GetTile(0, 14), Is.EqualTo(TileState.Solid));
            Assert.That(source.GetTile(5, 5), Is.EqualTo(TileState.Air));
            Assert.That(source.Openings.Count(o => o.IsSky), Is.EqualTo(1));
        }

        [Test]
        public void HasNoPipesAndNoDiffsYet()
        {
            var source = new DesktopTerrainSource(DesktopFixtures.Load("small"));

            Assert.That(source.Pipes, Is.Empty);
            Assert.That(source.TryTakeDiff(out var diff), Is.False);
            Assert.That(diff, Is.Null);
        }

        [Test]
        public void RoomBuiltFromTheSourceStandsCreaturesOnTheTaskbar()
        {
            var source = new DesktopTerrainSource(new FakeDesktopLayoutSource(DesktopFixtures.Load("uhd-200")));
            var room = new Room(source);

            Assert.That((room.Width, room.Height), Is.EqualTo((192, 108)));
            for (int x = 0; x < room.Width; x++)
                for (int y = 0; y < room.Height; y++)
                    Assert.That(room.GetTile(x, y).Terrain, Is.EqualTo(source.GetTile(x, y).Terrain), "tile " + x + "," + y);

            // A point just above the taskbar's top edge, mid-screen, lies in air on top of solid ground.
            var feet = source.Coordinates.ScreenToRoom(1920f, 2079f);
            var tile = room.GetTilePosition(feet);
            Assert.That(tile, Is.EqualTo(new IntVector2(96, 4)));
            Assert.That(room.GetTile(tile).Solid, Is.False);
            Assert.That(room.GetTile(tile.x, tile.y - 1).Solid, Is.True);
        }

        [Test]
        public void OpenEdgesLeadOutOfTheRoom()
        {
            var room = new Room(new DesktopTerrainSource(DesktopFixtures.Load("small")));

            // Outside the room the sim extends the edge tile, so open edge tiles continue as air.
            Assert.That(room.GetTile(-1, 5).Solid, Is.False);
            Assert.That(room.GetTile(20, 5).Solid, Is.False);
            Assert.That(room.GetTile(10, 15).Solid, Is.False);
            Assert.That(room.GetTile(10, -1).Solid, Is.True);
        }
    }
}
