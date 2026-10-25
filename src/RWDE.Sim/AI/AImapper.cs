using System;
using System.Collections.Generic;
using Acc = RWDE.Sim.AItile.Accessibility;
using Move = RWDE.Sim.MovementConnection.MovementType;
using Terrain = RWDE.Sim.Room.Tile.TerrainType;

namespace RWDE.Sim
{
    /// <summary>
    /// Builds an <see cref="AImap"/>, ported from the game's <c>AImapper</c>: accessibility of every tile,
    /// then each tile's movement connections and fall risk in scan order (bottom row first, left to
    /// right), then terrain proximity. The game's quirks are kept, including connections stored on a
    /// tile they don't start from, so pathing sees the same map as in the game.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game builds a room's map once. On the desktop terrain changes, so <see cref="Rebuild"/> redoes
    /// only the columns a change can reach and leaves the rest of the map untouched. A tile's
    /// accessibility reads its neighbours 1 tile away; its connections and fall risk read up to 4
    /// columns away and write up to 3 columns away; and a floor change shifts the floor altitude of the
    /// whole column above it. So everything a change can alter lies in full columns within 7 of it (3 of
    /// a changed shortcut entrance). Those columns are cleared and refilled by replaying, in scan order,
    /// every tile that writes to them, keeping only the writes that land in them. The result is the
    /// same as a full build, connection order included. Terrain proximity is always redone in full.
    /// </para>
    /// <para>The game's Watcher terrain curves (<c>room.terrain</c>) have no desktop counterpart and are left out.</para>
    /// </remarks>
    internal sealed class AImapper
    {
        // Reach, in columns, of a change through accessibility, connections and fall risk (see remarks).
        private const int ChangedTileReach = 7;
        private const int ChangedEntranceReach = 3;
        // How far a tile's connections and fall risk write from it.
        private const int WriteReach = 3;

        private readonly AImap map;
        private readonly Room room;
        // Columns being rebuilt; null while building the whole map.
        private bool[] owned;

        public AImapper(AImap map)
        {
            this.map = map;
            room = map.room;
        }

        /// <summary>Builds the whole map for <paramref name="room"/>.</summary>
        public static AImap Build(Room room)
        {
            var map = new AImap(room);
            var mapper = new AImapper(map);
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                    mapper.FindAccessibilityOfTile(x, y);
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    mapper.FindPassagesOfTile(x, y);
                    mapper.FindFallRiskOfTile(x, y);
                }
            mapper.MapTerrainProximity();
            return map;
        }

        /// <summary>Redoes the columns <paramref name="changes"/> can reach. See the class remarks.</summary>
        public void Rebuild(TerrainChanges changes)
        {
            owned = new bool[map.width];
            foreach (var p in changes.ChangedTiles)
                Mark(owned, p.x, ChangedTileReach);
            foreach (var pipe in changes.RemovedPipes)
            {
                Mark(owned, pipe.A.x, ChangedEntranceReach);
                Mark(owned, pipe.B.x, ChangedEntranceReach);
            }
            foreach (var pipe in changes.AddedPipes)
            {
                Mark(owned, pipe.A.x, ChangedEntranceReach);
                Mark(owned, pipe.B.x, ChangedEntranceReach);
            }

            // Tiles that may write into the owned columns: those within reach, plus shortcut entrances
            // whose shortcut ends in an owned column, since a shortcut joins tiles any distance apart.
            var replayed = new bool[map.width];
            for (int x = 0; x < map.width; x++)
                if (owned[x]) Mark(replayed, x, WriteReach);
            var farEntrances = new HashSet<IntVector2>();
            foreach (var sc in room.shortcuts)
                if (sc.shortCutType == ShortcutData.Type.Normal && owned[sc.DestTile.x] && !replayed[sc.StartTile.x])
                    farEntrances.Add(sc.StartTile);

            for (int x = 0; x < map.width; x++)
            {
                if (!owned[x]) continue;
                for (int y = 0; y < map.height; y++)
                    FindAccessibilityOfTile(x, y);
            }
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    if (!replayed[x] && !farEntrances.Contains(new IntVector2(x, y))) continue;
                    FindPassagesOfTile(x, y);
                    FindFallRiskOfTile(x, y);
                }
            MapTerrainProximity();
        }

        private static void Mark(bool[] columns, int x, int reach)
        {
            for (int i = Math.Max(0, x - reach); i <= Math.Min(columns.Length - 1, x + reach); i++)
                columns[i] = true;
        }

        private bool Owns(int x) => owned == null || (x >= 0 && x < owned.Length && owned[x]);

        private void AddOutgoing(int x, int y, MovementConnection c)
        {
            if (Owns(x)) map.map[x, y].outgoingPaths.Add(c);
        }

        private void AddIncoming(int x, int y, MovementConnection c)
        {
            if (Owns(x)) map.map[x, y].incomingPaths.Add(c);
        }

        private AItile Tile(int x, int y) => map.getAItile(x, y);
        private AItile Tile(IntVector2 p) => map.getAItile(p.x, p.y);
        private Acc A(int x, int y) => map.getAItile(x, y).acc;
        private Acc A(IntVector2 p) => map.getAItile(p.x, p.y).acc;
        private Terrain T(int x, int y) => room.GetTile(x, y).Terrain;
        private bool Solid(int x, int y) => room.GetTile(x, y).Terrain == Terrain.Solid;

        // ---- AImapper.FindAccessibilityOfCurrentTile ----

        private void FindAccessibilityOfTile(int x, int y)
        {
            var tile = room.GetTile(x, y);
            Acc acc;
            if (tile.Terrain != Terrain.Solid)
            {
                acc = Acc.Air;
                if (Solid(x, y - 1) || T(x, y - 1) == Terrain.Floor
                    || (Solid(x - 1, y - 1) && !Solid(x - 1, y)) || (Solid(x + 1, y - 1) && !Solid(x + 1, y)))
                    acc = Acc.Floor;
                else if (Solid(x - 1, y) && Solid(x + 1, y))
                    acc = Acc.Corridor;
                else if (tile.verticalBeam || tile.horizontalBeam)
                    acc = Acc.Climb;
                else if (tile.wallbehind || Solid(x - 1, y) || Solid(x + 1, y)
                         || (Solid(x - 1, y + 1) && !Solid(x, y + 1)) || (Solid(x + 1, y + 1) && !Solid(x, y + 1)))
                    acc = Acc.Wall;
                else if (Solid(x, y + 1))
                    acc = Acc.Ceiling;
            }
            else
            {
                acc = Acc.Solid;
            }
            map.map[x, y] = new AItile(acc, (tile.DeepWater ? 1 : 0) + (tile.WaterSurface ? 2 : 0))
            {
                fallRiskTile = new IntVector2(x, y)
            };
        }

        // ---- AImapper.FindPassagesOfCurrentTile ----

        private void FindPassagesOfTile(int x, int y)
        {
            var p = new IntVector2(x, y);

            // Floor altitude of the open tiles stacked on this floor.
            if (Owns(x) && (T(x, y) == Terrain.Floor || (T(x, y) == Terrain.Solid && !Solid(x, y + 1))))
                for (int i = y + 1; i < map.height && !Solid(x, i); i++)
                    map.map[x, i].floorAltitude = i - y;

            for (int j = 0; j < 4; j++)
            {
                var n = p + Custom.fourDirections[j];
                if (n.x >= 0 && n.x < room.TileWidth && n.y >= 0 && n.y < room.TileHeight)
                    AddOutgoing(x, y, new MovementConnection(Move.Standard, p, n, 1));
                var d = Custom.eightDirections[j];
                if (Owns(x) && Solid(x + d.x, y + d.y) && Solid(x - d.x, y - d.y))
                    map.map[x, y].narrowSpace = true;
            }

            var up = new IntVector2(0, 1);
            if (A(p + up) != Acc.Solid && Tile(p + up * 2).walkable && A(p + up) > A(p) && A(p + up * 2) < A(p + up))
                AddOutgoing(x, y, new MovementConnection(Move.ReachUp, p, p + up * 2, 2));
            if (Tile(p + up * 3).walkable && A(p + up) != Acc.Solid && A(p + up * 2) != Acc.Solid
                && A(p + up) > A(p) && A(p + up) > A(p + up * 3) && A(p + up * 2) > A(p) && A(p + up * 2) > A(p + up * 3))
                AddOutgoing(x, y, new MovementConnection(Move.DoubleReachUp, p, p + up * 3, 3));
            // The last comparison reads the tile above, not below; kept as in the game.
            if (A(p - up) != Acc.Solid && Tile(p - up * 2).walkable && A(p - up) > A(p) && A(p - up * 2) < A(p + up))
                AddOutgoing(x, y, new MovementConnection(Move.ReachDown, p, p - up * 2, 2));

            FindDropsOfTile(p);

            for (int k = 0; k < 2; k++)
            {
                int dx = k != 0 ? 1 : -1;
                var side = new IntVector2(dx, 0);
                if (A(p + side) != Acc.Solid && Tile(p + side * 2).walkable && A(p + side) > A(p) && A(p + side * 2) < A(p + side))
                    AddOutgoing(x, y, new MovementConnection(Move.ReachOverGap, p, p + side * 2, 2));
                if (A(p) == Acc.Floor && A(p + side) == Acc.Floor && A(x, y + 1) != Acc.Solid && A(x + dx, y + 1) != Acc.Solid)
                    AddOutgoing(x, y, new MovementConnection(Move.LizardTurn, p, p + side, 1));

                var upSide = new IntVector2(x + dx, y + 1);
                if (A(p) == Acc.Floor && T(x + dx, y) == Terrain.Slope && A(upSide) == Acc.Floor)
                {
                    AddOutgoing(x, y, new MovementConnection(Move.Slope, p, upSide, 1));
                    AddOutgoing(upSide.x, upSide.y, new MovementConnection(Move.Slope, upSide, p, 1));
                }
                else if (A(x + dx, y) != Acc.Solid && A(x, y + 1) != Acc.Solid)
                {
                    int corner = Math.Max((int)A(p), (int)A(upSide));
                    if ((int)A(x + dx, y) > corner && (int)A(x, y + 1) > corner)
                    {
                        AddOutgoing(x, y, new MovementConnection(Move.OpenDiagonal, p, upSide, 1));
                        AddOutgoing(upSide.x, upSide.y, new MovementConnection(Move.OpenDiagonal, upSide, p, 1));
                    }
                }

                var downSide = new IntVector2(x + dx, y - 1);
                if ((A(p) == Acc.Ceiling || A(p) == Acc.Wall) && T(x + dx, y) == Terrain.Slope
                    && (A(downSide) == Acc.Ceiling || A(downSide) == Acc.Wall))
                {
                    var back = new MovementConnection(Move.CeilingSlope, downSide, p, 1);
                    AddOutgoing(x, y, new MovementConnection(Move.CeilingSlope, p, downSide, 1));
                    AddOutgoing(downSide.x, downSide.y, back);
                    AddOutgoing(x, y, back);
                }
                if (A(p) == Acc.CurvedFloor && A(upSide) == Acc.CurvedFloor)
                {
                    AddOutgoing(x, y, new MovementConnection(Move.Slope, p, upSide, 1));
                    AddOutgoing(upSide.x, upSide.y, new MovementConnection(Move.Slope, upSide, p, 1));
                }

                // A reach two down and one across, around a corner the creature can't cut.
                var far = new IntVector2(x + dx, y - 2);
                Acc aFar = A(far), aSide = A(x + dx, y), aBelow2 = A(x, y - 2), aBelow = A(x, y - 1), aDiag = A(x + dx, y - 1);
                if (Tile(far).walkable && aBelow != Acc.Solid && aDiag != Acc.Solid && (aSide != Acc.Solid || aBelow2 != Acc.Solid)
                    && aSide > A(p) && aSide > aFar && aBelow2 > A(p) && aBelow2 > aFar
                    && aBelow > A(p) && aBelow > aFar && aDiag > A(p) && aDiag > aFar)
                {
                    var back = new MovementConnection(Move.SemiDiagonalReach, far, p, 3);
                    AddOutgoing(x, y, new MovementConnection(Move.SemiDiagonalReach, p, far, 3));
                    AddOutgoing(far.x, far.y, back);
                    AddOutgoing(x, y, back);
                }

                // The same two to the left and one up or down; the game uses dx as the vertical step here.
                far = new IntVector2(x - 2, y + dx);
                aFar = A(far);
                Acc aV = A(x, y + dx), aLeft2 = A(x - 2, y), aLeft = A(x - 1, y), aDiag2 = A(x - 1, y + dx);
                if (Tile(far).walkable && aLeft != Acc.Solid && aDiag2 != Acc.Solid && (aV != Acc.Solid || aLeft2 != Acc.Solid)
                    && aV > A(p) && aV > aFar && aLeft2 > A(p) && aLeft2 > aFar
                    && aLeft > A(p) && aLeft > aFar && aDiag2 > A(p) && aDiag2 > aFar)
                {
                    var back = new MovementConnection(Move.SemiDiagonalReach, far, p, 3);
                    AddOutgoing(x, y, new MovementConnection(Move.SemiDiagonalReach, p, far, 3));
                    AddOutgoing(far.x, far.y, back);
                    AddOutgoing(x, y, back);
                }
            }

            if (T(x, y) == Terrain.ShortcutEntrance)
                FindShortcutPassages(p);

            // Copy this tile's outgoing connections into their destinations' incoming lists. A tile that
            // is only replayed copies just the connections it had at this point in the original build.
            var outgoing = map.map[x, y].outgoingPaths;
            int count;
            if (Owns(x))
            {
                count = outgoing.Count;
                map.pushCount[x, y] = count;
            }
            else
            {
                count = map.pushCount[x, y];
            }
            for (int i = 0; i < count; i++)
                AddIncoming(outgoing[i].DestTile.x, outgoing[i].DestTile.y, outgoing[i]);
        }

        // The DropToFloor / DropToClimb / DropToWater part of FindPassagesOfCurrentTile.
        private void FindDropsOfTile(IntVector2 p)
        {
            int x = p.x, y = p.y;
            if (A(x, y - 1) == Acc.Solid || A(x, y - 1) <= A(p))
                return;

            int fallen = 0;
            var landings = new List<int>();
            for (int ty = y - 1; ty > 0; ty--)
            {
                if ((A(x, ty) == Acc.Floor || A(x, ty) == Acc.CurvedFloor) && (Solid(x, ty - 1) || T(x, ty - 1) == Terrain.Floor))
                {
                    AddOutgoing(x, y, new MovementConnection(Move.DropToFloor, p, new IntVector2(x, ty), y - ty));
                    landings.Add(ty);
                    landings.Add(ty - 1);
                    landings.Add(ty + 1);
                    break;
                }
                if (A(x, ty) == Acc.Climb && A(x, ty + 1) > A(x, ty))
                {
                    AddOutgoing(x, y, new MovementConnection(Move.DropToClimb, p, new IntVector2(x, ty), y - ty));
                    landings.Add(ty);
                }
                if (room.GetTile(x, ty).WaterSurface)
                {
                    AddOutgoing(x, y, new MovementConnection(Move.DropToWater, p, new IntVector2(x, ty), y - ty));
                    landings.Add(ty);
                }
                fallen++;
            }
            if (fallen < 4)
                return;

            // After a long fall, also look for landings one column to either side.
            for (int dx = 1; dx >= -1; dx -= 2)
            {
                for (int ty = y - 4; ty > 0 && A(x + dx, ty) != Acc.Solid && A(x, ty) != Acc.Solid; ty--)
                {
                    var land = new IntVector2(x + dx, ty);
                    bool clearAbove = A(x + dx, ty + 1) != Acc.Solid && A(x + dx, ty + 2) != Acc.Solid;
                    if ((A(land) == Acc.Floor || A(land) == Acc.CurvedFloor) && clearAbove
                        && (Solid(x + dx, ty - 1) || T(x + dx, ty - 1) == Terrain.Floor) && NotStraightBelowNeighbours(p, land))
                    {
                        if (!landings.Contains(ty))
                            AddOutgoing(x, y, new MovementConnection(Move.DropToFloor, p, land, y - ty));
                        break;
                    }
                    if (A(land) == Acc.Climb && clearAbove && A(x + dx, ty + 1) > A(land)
                        && NotStraightBelowNeighbours(p, land) && !landings.Contains(ty))
                        AddOutgoing(x, y, new MovementConnection(Move.DropToClimb, p, land, y - ty));
                }
            }
        }

        // A side landing only counts when neither neighbour of p would reach it by falling straight down.
        private bool NotStraightBelowNeighbours(IntVector2 p, IntVector2 land) =>
            !IsThereAStraightDropBetweenTiles(new IntVector2(p.x - 1, p.y), land)
            && !IsThereAStraightDropBetweenTiles(new IntVector2(p.x + 1, p.y), land);

        // AImapper.IsThereAStraightDropBetweenTiles: would falling straight down from a land on b?
        private bool IsThereAStraightDropBetweenTiles(IntVector2 a, IntVector2 b)
        {
            if (A(a.x, a.y - 1) == Acc.Solid || A(a.x, a.y - 1) <= A(a))
                return false;
            for (int ty = a.y - 1; ty > 0; ty--)
            {
                if (A(a.x, ty) == Acc.Floor && (Solid(a.x, ty - 1) || T(a.x, ty - 1) == Terrain.Floor))
                    return new IntVector2(a.x, ty) == b;
                if (A(a.x, ty) == Acc.Climb && A(a.x, ty + 1) > A(a.x, ty) && new IntVector2(a.x, ty) == b)
                    return true;
            }
            return false;
        }

        // The shortcut-entrance part of FindPassagesOfCurrentTile.
        private void FindShortcutPassages(IntVector2 p)
        {
            // Big creatures can squeeze into an entrance from 2 or 3 tiles in front of it.
            var hole = room.ShorcutEntranceHoleDirection(p);
            for (int l = 2; l < 4; l++)
            {
                for (int m = l == 3 ? -1 : 0; m < (l != 3 ? 1 : 2); m++)
                {
                    var pos = p + hole * l + Custom.PerpIntVec(hole) * m;
                    if (!room.HasAnySolid(pos) && room.IsPositionInsideBoundries(pos))
                    {
                        AddOutgoing(p.x, p.y, new MovementConnection(Move.BigCreatureShortCutSqueeze, p, pos, l));
                        var back = new MovementConnection(Move.BigCreatureShortCutSqueeze, pos, p, l);
                        AddOutgoing(pos.x, pos.y, back);
                        if (pos.x < p.x || pos.y < p.y)
                            AddIncoming(p.x, p.y, back);
                    }
                }
            }

            var shortcut = room.shortcutData(p);
            if (shortcut.shortCutType == ShortcutData.Type.Normal)
                AddOutgoing(p.x, p.y, new MovementConnection(Move.ShortCut, p, shortcut.DestTile, shortcut.length));
        }

        // ---- AImapper.FindFallRiskOfCurrentTile ----

        private void FindFallRiskOfTile(int x, int y)
        {
            if (room.HasAnySolid(x, y))
                return;
            if (y == 0 || room.HasAnySolid(x, y - 1))
            {
                // On a floor (or the room's bottom edge): everything straight above falls to here.
                var landing = y != 0 || room.HasAnySolid(x, -1) ? new IntVector2(x, y) : new IntVector2(-1, -1);
                for (int i = y; i < room.TileHeight && !room.HasAnySolid(x, i); i++)
                {
                    if (Owns(x) && Tile(x, i).fallRiskTile.y >= landing.y)
                        Tile(x, i).fallRiskTile = landing;
                    for (int dx = -1; dx <= 1; dx += 2)
                    {
                        // Outside the room this writes to the shared standard tile, as in the game.
                        bool inRoom = x + dx >= 0 && x + dx < map.width;
                        if ((!inRoom || Owns(x + dx)) && !room.HasAnySolid(x + dx, i) && Tile(x + dx, i).fallRiskTile.y > landing.y)
                            Tile(x + dx, i).fallRiskTile = landing;
                    }
                }
            }

            if (!Owns(x))
                return;
            int altitude = Tile(x, y).floorAltitude;
            int range = Custom.IntClamp(altitude, 0, 5);
            for (int j = -1; j < 2; j += 2)
                for (int k = 1; k < range && x + j * k >= 0 && x + j * k < room.TileWidth && !room.HasAnySolid(x + j * k, y); k++)
                    if (Tile(x + j * k, y).floorAltitude + k / 2 < altitude)
                        altitude = Tile(x + j * k, y).floorAltitude + k / 2;
            Tile(x, y).smoothedFloorAltitude = altitude;
        }

        // ---- AImapper.TerrainProximityMapper ----

        private void MapTerrainProximity()
        {
            var queue = new Queue<IntVector2>();
            for (int i = 0; i < map.width; i++)
            {
                for (int j = 0; j < map.height; j++)
                {
                    map.terrainProximity[i, j] = -1;
                    if (room.HasAnySolid(i, j))
                    {
                        map.terrainProximity[i, j] = 0;
                        continue;
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        if (room.HasAnySolid(i + Custom.fourDirections[k].x, j + Custom.fourDirections[k].y))
                        {
                            queue.Enqueue(new IntVector2(i, j));
                            map.terrainProximity[i, j] = 1;
                            break;
                        }
                    }
                }
            }
            while (queue.Count > 0)
            {
                var pos = queue.Dequeue();
                int next = map.terrainProximity[pos.x, pos.y] + 1;
                for (int i = 0; i < 4; i++)
                {
                    int nx = pos.x + Custom.fourDirections[i].x;
                    int ny = pos.y + Custom.fourDirections[i].y;
                    if (nx >= 0 && ny >= 0 && nx < map.width && ny < map.height && map.terrainProximity[nx, ny] < 0)
                    {
                        map.terrainProximity[nx, ny] = next;
                        queue.Enqueue(new IntVector2(nx, ny));
                    }
                }
            }
        }
    }
}
