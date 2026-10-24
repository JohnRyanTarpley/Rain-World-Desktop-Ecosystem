using NUnit.Framework;

namespace RWDE.Sim.Tests
{
    // Expected values follow the game's Room coordinate rules: 20 px tiles, (0, 0) bottom-left.
    public class RoomTests
    {
        private static Vector2 V(float x, float y) => new Vector2(x, y);
        private static IntVector2 T(int x, int y) => new IntVector2(x, y);

        [Test]
        public void NewRoomIsAllAirWithTileCoordinates()
        {
            var room = new Room(4, 3);

            Assert.AreEqual(4, room.TileWidth);
            Assert.AreEqual(3, room.TileHeight);
            Assert.AreEqual(80f, room.PixelWidth);
            Assert.AreEqual(60f, room.PixelHeight);
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 3; y++)
                {
                    var tile = room.GetTile(x, y);
                    Assert.AreEqual(Room.Tile.TerrainType.Air, tile.Terrain);
                    Assert.AreEqual(x, tile.X);
                    Assert.AreEqual(y, tile.Y);
                    Assert.AreEqual(0, tile.shortCut);
                }
        }

        [Test]
        public void GetTilePositionFloorsPixelsToTiles()
        {
            var room = new Room(10, 10);

            Assert.AreEqual(T(0, 0), room.GetTilePosition(V(0f, 0f)));
            Assert.AreEqual(T(0, 0), room.GetTilePosition(V(19.99f, 19.99f)));
            Assert.AreEqual(T(1, 2), room.GetTilePosition(V(20f, 40f)));
            Assert.AreEqual(T(3, 0), room.GetTilePosition(V(79.5f, 5f)));
            // The +20 / -1 in the game's formula floors small negatives instead of truncating to 0.
            Assert.AreEqual(T(-1, -1), room.GetTilePosition(V(-0.5f, -19.9f)));
            // Further out the game's formula truncates again, so everything in [-40, 0) lands in tile -1.
            Assert.AreEqual(T(-1, 0), Room.StaticGetTilePosition(V(-20.5f, 0f)));
            Assert.AreEqual(T(-2, 0), Room.StaticGetTilePosition(V(-40f, 0f)));
        }

        [Test]
        public void MiddleOfTileAndTileRect()
        {
            var room = new Room(10, 10);

            Assert.AreEqual(V(10f, 10f), room.MiddleOfTile(0, 0));
            Assert.AreEqual(V(70f, 50f), room.MiddleOfTile(T(3, 2)));
            Assert.AreEqual(V(70f, 50f), room.MiddleOfTile(V(61f, 59f)));

            var rect = room.TileRect(T(3, 2));
            Assert.AreEqual(60f, rect.left);
            Assert.AreEqual(40f, rect.bottom);
            Assert.AreEqual(80f, rect.right);
            Assert.AreEqual(60f, rect.top);
            Assert.IsTrue(rect.Vector2Inside(room.MiddleOfTile(3, 2)));
            Assert.IsFalse(rect.Vector2Inside(V(60f, 50f)), "edges are outside, as in the game");
        }

        [Test]
        public void TileToPixelToTileRoundTrips()
        {
            var room = new Room(30, 20);
            for (int x = -1; x < 32; x++)
                for (int y = -1; y < 22; y++)
                    Assert.AreEqual(T(x, y), room.GetTilePosition(room.MiddleOfTile(x, y)));
        }

        [Test]
        public void IsPositionInsideBoundries()
        {
            var room = new Room(5, 4);

            Assert.IsTrue(room.IsPositionInsideBoundries(T(0, 0)));
            Assert.IsTrue(room.IsPositionInsideBoundries(T(4, 3)));
            Assert.IsFalse(room.IsPositionInsideBoundries(T(5, 0)));
            Assert.IsFalse(room.IsPositionInsideBoundries(T(0, 4)));
            Assert.IsFalse(room.IsPositionInsideBoundries(T(-1, 2)));
        }

        [Test]
        public void GetTileByPixelPosition()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff().SetTile(2, 1, TileState.Solid));

            Assert.IsTrue(room.GetTile(V(55f, 25f)).Solid);
            Assert.IsFalse(room.GetTile(V(35f, 25f)).Solid);
        }

        [Test]
        public void OutsideTheRoomExtendsTheNearestEdgeTile()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff().SetTile(0, 0, TileState.Solid).SetTile(4, 2, new TileState(Room.Tile.TerrainType.Floor)));

            Assert.AreSame(room.Tiles[0, 0], room.GetTile(-3, -1));
            Assert.AreEqual(Room.Tile.TerrainType.Floor, room.GetTile(9, 2).Terrain);
            Assert.AreEqual(Room.Tile.TerrainType.Air, room.GetTile(2, 99).Terrain);
        }

        [Test]
        public void OutsideTheRoomAShortcutEdgeTileReadsAsSolidWithoutShortcut()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(0, 2), T(0, 3), T(0, 4) })));

            var outside = room.GetTile(-1, 2);
            Assert.AreEqual(Room.Tile.TerrainType.Solid, outside.Terrain);
            Assert.AreEqual(0, outside.shortCut);
            Assert.AreEqual(0, room.GetTile(-1, 3).shortCut);
            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[0, 2].Terrain, "the real tile is unchanged");
        }

        [Test]
        public void DefaultTileWinsOutsideTheRoom()
        {
            var room = new Room(5, 5);
            room.DefaultTile = new Room.Tile(0, 0, Room.Tile.TerrainType.Solid, false, false, false, 0, 0);

            Assert.AreSame(room.DefaultTile, room.GetTile(-1, 0));
            Assert.AreSame(room.Tiles[1, 1], room.GetTile(1, 1));
        }

        [Test]
        public void TileStateRejectsShortcutEntrance()
        {
            Assert.Throws<System.ArgumentException>(() => new TileState(Room.Tile.TerrainType.ShortcutEntrance));
        }
    }
}
