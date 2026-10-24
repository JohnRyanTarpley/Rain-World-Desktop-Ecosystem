using System;
using System.Linq;
using NUnit.Framework;

namespace RWDE.Sim.Tests
{
    public class TerrainDiffTests
    {
        private static IntVector2 T(int x, int y) => new IntVector2(x, y);

        private static Pipe Column(int id, int x, int y0, int y1) =>
            new Pipe(id, Enumerable.Range(y0, y1 - y0 + 1).Select(y => T(x, y)).ToArray());

        [Test]
        public void RoomFromSourceCopiesTheSnapshot()
        {
            var source = new FakeTerrainSource(6, 6)
                .WithTile(1, 0, new TileState(Room.Tile.TerrainType.Floor, horizontalBeam: true))
                .WithTile(4, 4, new TileState(Room.Tile.TerrainType.Slope))
                .WithTile(3, 2, new TileState(Room.Tile.TerrainType.Air, verticalBeam: true, wallbehind: true))
                .WithPipe(Column(1, 5, 0, 3));

            var room = new Room(source);

            Assert.AreEqual(6, room.Width);
            Assert.AreEqual(Room.Tile.TerrainType.Floor, room.Tiles[1, 0].Terrain);
            Assert.IsTrue(room.Tiles[1, 0].horizontalBeam);
            Assert.AreEqual(Room.Tile.TerrainType.Slope, room.Tiles[4, 4].Terrain);
            Assert.IsTrue(room.Tiles[3, 2].verticalBeam);
            Assert.IsTrue(room.Tiles[3, 2].wallbehind);
            Assert.IsTrue(room.Tiles[3, 2].AnyBeam);
            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[5, 3].Terrain);
            Assert.AreEqual(2, room.shortcuts.Length);
            Assert.AreEqual(0, source.DiffsTaken, "the snapshot is read directly, not through diffs");
        }

        [Test]
        public void TileChangesReportOnlyRealChanges()
        {
            var room = new Room(5, 5);

            var changes = room.ApplyTerrainDiff(new TerrainDiff()
                .SetTile(1, 1, TileState.Solid)
                .SetTile(2, 2, TileState.Air)
                .SetTile(3, 3, TileState.Solid)
                .SetTile(3, 3, TileState.Air));

            Assert.IsTrue(room.Tiles[1, 1].Solid);
            CollectionAssert.AreEquivalent(new[] { T(1, 1) }, changes.ChangedTiles);
            Assert.IsTrue(changes.Any);
        }

        [Test]
        public void EmptyDiffChangesNothing()
        {
            var room = new Room(3, 3);

            var changes = room.ApplyTerrainDiff(new TerrainDiff());

            Assert.IsFalse(changes.Any);
        }

        [Test]
        public void RemovingAPipeRestoresTheSourceTerrain()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff()
                .SetTile(2, 0, new TileState(Room.Tile.TerrainType.Floor))
                .SetTile(2, 1, TileState.Solid)
                .AddPipe(Column(4, 2, 0, 2)));
            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[2, 0].Terrain);

            var changes = room.ApplyTerrainDiff(new TerrainDiff().RemovePipe(4));

            Assert.AreEqual(Room.Tile.TerrainType.Floor, room.Tiles[2, 0].Terrain);
            Assert.AreEqual(Room.Tile.TerrainType.Solid, room.Tiles[2, 1].Terrain);
            Assert.AreEqual(Room.Tile.TerrainType.Air, room.Tiles[2, 2].Terrain);
            Assert.IsTrue(Enumerable.Range(0, 3).All(y => room.Tiles[2, y].shortCut == 0));
            Assert.AreEqual(0, room.shortcuts.Length);
            Assert.AreEqual(4, changes.RemovedPipes.Single().Id);
            CollectionAssert.AreEquivalent(new[] { T(2, 0), T(2, 1), T(2, 2) }, changes.ChangedTiles);
            Assert.IsFalse(room.TryGetPipe(4, out _));
        }

        [Test]
        public void TileChangeUnderAnEntranceKeepsTheEntrance()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(Column(1, 0, 0, 2)));

            var changes = room.ApplyTerrainDiff(new TerrainDiff().SetTile(0, 0, TileState.Solid));

            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[0, 0].Terrain);
            Assert.AreEqual(0, changes.ChangedTiles.Count);

            room.ApplyTerrainDiff(new TerrainDiff().RemovePipe(1));
            Assert.AreEqual(Room.Tile.TerrainType.Solid, room.Tiles[0, 0].Terrain, "the change shows once the pipe is gone");
        }

        [Test]
        public void AddingAPipeWithAnExistingIdReplacesIt()
        {
            var room = new Room(6, 6);
            var first = Column(9, 1, 0, 3);
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(first));

            var moved = Column(9, 4, 1, 5);
            var changes = room.ApplyTerrainDiff(new TerrainDiff().AddPipe(moved));

            Assert.AreSame(first, changes.RemovedPipes.Single());
            Assert.AreSame(moved, changes.AddedPipes.Single());
            Assert.IsTrue(room.TryGetPipe(9, out var now));
            Assert.AreSame(moved, now);
            Assert.AreEqual(Room.Tile.TerrainType.Air, room.Tiles[1, 0].Terrain);
            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[4, 5].Terrain);
            CollectionAssert.AreEqual(new[] { T(4, 5), T(4, 1) }, room.shortcutsIndex);
            Assert.AreEqual(9, changes.ChangedTiles.Count);
        }

        [Test]
        public void RemovingAnUnknownPipeIsANoOp()
        {
            var room = new Room(3, 3);

            var changes = room.ApplyTerrainDiff(new TerrainDiff().RemovePipe(42));

            Assert.IsFalse(changes.Any);
        }

        [Test]
        public void CrossingPipesKeepTheSharedTileMarkedUntilBothAreGone()
        {
            var room = new Room(5, 5);
            var vertical = Column(1, 2, 0, 4);
            var horizontal = new Pipe(2, new[] { T(0, 2), T(1, 2), T(2, 2), T(3, 2), T(4, 2) });
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(vertical).AddPipe(horizontal));
            Assert.AreEqual(4, room.shortcuts.Length);

            room.ApplyTerrainDiff(new TerrainDiff().RemovePipe(1));
            Assert.AreEqual(1, room.Tiles[2, 2].shortCut);
            Assert.AreEqual(0, room.Tiles[2, 3].shortCut);

            room.ApplyTerrainDiff(new TerrainDiff().RemovePipe(2));
            Assert.AreEqual(0, room.Tiles[2, 2].shortCut);
        }

        [Test]
        public void InvalidDiffThrowsAndLeavesTheRoomUntouched()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(Column(1, 0, 0, 3)));

            Assert.Throws<ArgumentException>(() => room.ApplyTerrainDiff(new TerrainDiff()
                .SetTile(2, 2, TileState.Solid)
                .SetTile(5, 0, TileState.Solid)));
            Assert.Throws<ArgumentException>(() => room.ApplyTerrainDiff(new TerrainDiff()
                .SetTile(2, 2, TileState.Solid)
                .AddPipe(Column(2, 4, 3, 5))), "pipe leaves the room");
            Assert.Throws<ArgumentException>(() => room.ApplyTerrainDiff(new TerrainDiff()
                .SetTile(2, 2, TileState.Solid)
                .AddPipe(new Pipe(2, new[] { T(0, 3), T(1, 3), T(2, 3) }))), "entrance on another pipe's entrance");
            Assert.Throws<ArgumentException>(() => room.ApplyTerrainDiff(new TerrainDiff()
                .AddPipe(new Pipe(2, new[] { T(0, 1), T(1, 1), T(2, 1) }))), "entrance on another pipe's path");

            Assert.AreEqual(Room.Tile.TerrainType.Air, room.Tiles[2, 2].Terrain);
            Assert.AreEqual(Room.Tile.TerrainType.Air, room.Tiles[2, 1].Terrain);
            Assert.AreEqual(1, room.Tiles[0, 1].shortCut);
            Assert.IsFalse(room.TryGetPipe(2, out _));
            Assert.AreEqual(2, room.shortcuts.Length);
        }

        [Test]
        public void ReplacingAPipeMayReuseItsOwnTiles()
        {
            var room = new Room(5, 5);
            room.ApplyTerrainDiff(new TerrainDiff().AddPipe(Column(1, 0, 0, 3)));

            // Shorter version of the same pipe: its new top entrance sits on the old path.
            Assert.DoesNotThrow(() => room.ApplyTerrainDiff(new TerrainDiff().AddPipe(Column(1, 0, 0, 2))));
            Assert.AreEqual(Room.Tile.TerrainType.ShortcutEntrance, room.Tiles[0, 2].Terrain);
            Assert.AreEqual(0, room.Tiles[0, 3].shortCut);
        }

        [Test]
        public void ApplyTerrainDiffsDrainsTheSourceInOrder()
        {
            var source = new FakeTerrainSource(5, 5);
            var room = new Room(source);
            source.Queue(new TerrainDiff().SetTile(1, 1, TileState.Solid).AddPipe(Column(3, 4, 0, 2)));
            source.Queue(new TerrainDiff().SetTile(1, 1, TileState.Air).SetTile(2, 2, TileState.Solid));

            var changes = room.ApplyTerrainDiffs(source);

            Assert.AreEqual(2, source.DiffsTaken);
            Assert.IsFalse(room.Tiles[1, 1].Solid);
            Assert.IsTrue(room.Tiles[2, 2].Solid);
            CollectionAssert.DoesNotContain(changes.ChangedTiles, T(1, 1), "changed and reverted within the batch");
            CollectionAssert.Contains(changes.ChangedTiles, T(2, 2));
            CollectionAssert.Contains(changes.ChangedTiles, T(4, 0));
            Assert.AreEqual(3, changes.AddedPipes.Single().Id);

            Assert.IsFalse(room.ApplyTerrainDiffs(source).Any, "nothing left to apply");
        }
    }
}
