using System;
using NUnit.Framework;

namespace RWDE.Sim.Tests
{
    public class ShortcutTests
    {
        private static IntVector2 T(int x, int y) => new IntVector2(x, y);

        // A vertical pipe in column 2, from (2, 1) up to (2, 5), with solid walls on both sides.
        private static Room RoomWithVerticalPipe()
        {
            var room = new Room(6, 8);
            var diff = new TerrainDiff();
            for (int y = 1; y <= 5; y++)
            {
                diff.SetTile(1, y, TileState.Solid);
                diff.SetTile(3, y, TileState.Solid);
                diff.SetTile(2, y, TileState.Solid);
            }
            diff.AddPipe(new Pipe(7, new[] { T(2, 1), T(2, 2), T(2, 3), T(2, 4), T(2, 5) }));
            room.ApplyTerrainDiff(diff);
            return room;
        }

        [Test]
        public void PipeRejectsBadPaths()
        {
            Assert.Throws<ArgumentException>(() => new Pipe(1, new[] { T(0, 0) }), "too short");
            Assert.Throws<ArgumentException>(() => new Pipe(1, new[] { T(0, 0), T(1, 1) }), "diagonal step");
            Assert.Throws<ArgumentException>(() => new Pipe(1, new[] { T(0, 0), T(0, 2) }), "gap");
            Assert.Throws<ArgumentException>(() => new Pipe(1, new[] { T(0, 0), T(0, 1), T(0, 0) }), "revisits a tile");
        }

        [Test]
        public void PipeEndsAndDirections()
        {
            var pipe = new Pipe(3, new[] { T(1, 1), T(1, 2), T(2, 2) });

            Assert.AreEqual(T(1, 1), pipe.A);
            Assert.AreEqual(T(2, 2), pipe.B);
            Assert.AreEqual(2, pipe.Length);
            Assert.AreEqual(T(2, 2), pipe.OtherEnd(T(1, 1)));
            Assert.AreEqual(T(1, 1), pipe.OtherEnd(T(2, 2)));
            CollectionAssert.AreEqual(new[] { T(2, 2), T(1, 2), T(1, 1) }, pipe.PathFrom(T(2, 2)));
            Assert.Throws<ArgumentException>(() => pipe.OtherEnd(T(1, 2)));
        }

        [Test]
        public void PipeMarksEntrancesAndPathTiles()
        {
            var room = RoomWithVerticalPipe();

            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[2, 1].Terrain);
            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[2, 5].Terrain);
            for (int y = 2; y <= 4; y++)
                Assert.AreEqual(Room.Tile.TerrainType.Solid, room.Tiles[2, y].Terrain, "path tiles keep their terrain");
            for (int y = 1; y <= 5; y++)
                Assert.AreEqual(1, room.Tiles[2, y].shortCut);
            Assert.AreEqual(0, room.Tiles[1, 3].shortCut);
            Assert.AreEqual(0, room.Tiles[2, 6].shortCut);
        }

        [Test]
        public void EachEntranceGetsAShortcutToTheOtherEndInScanOrder()
        {
            var room = RoomWithVerticalPipe();

            Assert.AreEqual(2, room.shortcuts.Length);
            // ShortcutMapper scans from the top row down, so the upper entrance comes first.
            CollectionAssert.AreEqual(new[] { T(2, 5), T(2, 1) }, room.shortcutsIndex);

            var down = room.shortcutData(T(2, 5));
            Assert.AreEqual(ShortcutData.Type.Normal, down.shortCutType);
            Assert.AreEqual(T(2, 5), down.StartTile);
            Assert.AreEqual(T(2, 1), down.DestTile);
            Assert.AreEqual(4, down.length);
            Assert.AreEqual(-1, down.destNode);
            Assert.AreEqual(7, down.pipeId);
            Assert.IsTrue(down.LeadingSomewhere);
            Assert.IsFalse(down.ToNode);
            CollectionAssert.AreEqual(new[] { T(2, 5), T(2, 4), T(2, 3), T(2, 2), T(2, 1) }, down.path);

            var up = room.shortcutData(T(2, 1));
            Assert.AreEqual(T(2, 5), up.DestTile);
            CollectionAssert.AreEqual(new[] { T(2, 1), T(2, 2), T(2, 3), T(2, 4), T(2, 5) }, up.path);
        }

        [Test]
        public void ShortcutDataByPixelPosition()
        {
            var room = RoomWithVerticalPipe();

            Assert.AreEqual(T(2, 1), room.shortcutData(room.MiddleOfTile(2, 5)).DestTile);
        }

        [Test]
        public void ShortcutDataElsewhereIsADeadEnd()
        {
            var room = RoomWithVerticalPipe();

            var none = room.shortcutData(T(2, 3));
            Assert.AreEqual(ShortcutData.Type.DeadEnd, none.shortCutType);
            Assert.AreEqual(T(2, 3), none.DestTile);
            Assert.AreEqual(0, none.length);
            Assert.AreEqual(-1, none.pipeId);
            Assert.IsFalse(none.LeadingSomewhere);
        }

        [Test]
        public void EntranceInTileZeroZeroIsFound()
        {
            var room = new Room(4, 4);
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(new Pipe(1, new[] { T(0, 0), T(1, 0), T(2, 0) })));

            Assert.AreEqual(T(2, 0), room.shortcutData(T(0, 0)).DestTile);
        }

        [Test]
        public void HoleDirectionPointsAtTheOpenSide()
        {
            var room = RoomWithVerticalPipe();

            // Walls left and right; the pipe continues inwards. The bottom entrance opens downwards.
            Assert.AreEqual(T(0, -1), room.ShorcutEntranceHoleDirection(T(2, 1)));
            Assert.AreEqual(T(0, 1), room.ShorcutEntranceHoleDirection(T(2, 5)));
        }

        [Test]
        public void HoleDirectionPrefersLeftThenDownThenRightThenUp()
        {
            var room = new Room(5, 5);

            Assert.AreEqual(T(-1, 0), room.ShorcutEntranceHoleDirection(T(2, 2)));
        }

        [Test]
        public void HoleDirectionIsZeroWhenEnclosed()
        {
            var room = new Room(3, 3);
            var diff = new TerrainDiff();
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    diff.SetTile(x, y, TileState.Solid);
            room.ApplyTerrainDiff(diff);

            Assert.AreEqual(T(0, 0), room.ShorcutEntranceHoleDirection(T(1, 1)));
        }
    }
}
