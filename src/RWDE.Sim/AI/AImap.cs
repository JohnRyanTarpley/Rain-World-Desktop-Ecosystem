using System;

namespace RWDE.Sim
{
    /// <summary>
    /// A room's AI map, ported from the game's <c>AImap</c>: per tile, its accessibility, the movement
    /// connections leaving and entering it, fall risk and distance to terrain. The tiles are the same
    /// for every creature; each <see cref="CreatureTemplate"/> decides which of them it may use.
    /// Get it from <see cref="Room.aimap"/>; the room keeps it current as terrain diffs arrive.
    /// </summary>
    /// <remarks>
    /// Not ported yet: the per-creature pre-processed data (<c>CreatureSpecificAImap</c>: exit distances,
    /// accessibility maps, triangulation), which arrives with the pathfinder, and visibility, which stays 0.
    /// The game's room access modifiers, Watcher terrain curves and zero-gravity lizard rules have no
    /// desktop counterpart: the desktop has none of the first two and <see cref="Room.gravity"/> is always 1.
    /// </remarks>
    public sealed class AImap
    {
        public readonly AItile[,] map;
        /// <summary>
        /// Steps through open tiles to the nearest solid tile: 0 on solid tiles, 1 next to them, -1 where
        /// no solid tile can be reached. Indexed [x, y].
        /// </summary>
        public readonly int[,] terrainProximity;
        /// <summary>What <see cref="getAItile(int, int)"/> returns outside the room: a solid tile.</summary>
        public readonly AItile standardTile;
        public readonly Room room;
        public readonly int width;
        public readonly int height;

        // How many of each tile's outgoing connections existed when the mapper copied them into their
        // destinations' incoming lists. The local rebuild replays those copies for tiles it keeps.
        internal readonly int[,] pushCount;

        internal AImap(Room room)
        {
            this.room = room;
            width = room.TileWidth;
            height = room.TileHeight;
            map = new AItile[width, height];
            terrainProximity = new int[width, height];
            pushCount = new int[width, height];
            standardTile = new AItile(AItile.Accessibility.Solid, 0);
        }

        // ---- Tiles ----

        public AItile getAItile(Vector2 pos) => getAItile(room.GetTilePosition(pos));
        public AItile getAItile(IntVector2 pos) => getAItile(pos.x, pos.y);

        public AItile getAItile(int x, int y) =>
            x >= 0 && x < width && y >= 0 && y < height ? map[x, y] : standardTile;

        public AItile getClampedAItile(Vector2 pos)
        {
            var p = room.GetTilePosition(pos);
            return getClampedAItile(p.x, p.y);
        }

        public AItile getClampedAItile(int x, int y) => map[Clamp(x, 0, width - 1), Clamp(y, 0, height - 1)];

        public int getTerrainProximity(Vector2 pos) => getTerrainProximity(room.GetTilePosition(pos));
        public int getTerrainProximity(IntVector2 pos) => getTerrainProximity(pos.x, pos.y);

        public int getTerrainProximity(int x, int y) =>
            x >= 0 && x < width && y >= 0 && y < height ? terrainProximity[x, y] : -1;

        public float Visibility(IntVector2 pos) => getAItile(pos).visibility;

        // ---- Creature-specific queries ----

        public bool TileAccessibleToCreature(int x, int y, CreatureTemplate crit) => TileAccessibleToCreature(new IntVector2(x, y), crit);

        public bool TileAccessibleToCreature(Vector2 pos, CreatureTemplate crit) => TileAccessibleToCreature(room.GetTilePosition(pos), crit);

        /// <summary>Whether <paramref name="crit"/> may be on the tile at all (<c>AImap.TileAccessibleToCreature</c>).</summary>
        public bool TileAccessibleToCreature(IntVector2 pos, CreatureTemplate crit)
        {
            var tile = getAItile(pos);
            if (!crit.MovementLegalInRelationToWater(tile.DeepWater, tile.WaterSurface))
                return false;
            if (crit.PreBakedPathingIndex == -1)
                return false;
            if (IsTooCloseToTerrain(pos, crit, out bool result))
                return result;
            if (!crit.AccessibilityResistance(tile.acc).Allowed)
            {
                if (crit.canSwim && tile.acc != AItile.Accessibility.Solid)
                    return tile.AnyWater;
                return false;
            }
            return true;
        }

        private bool IsTooCloseToTerrain(IntVector2 pos, CreatureTemplate crit, out bool result)
        {
            result = false;
            if (crit.daddyCorruptionImmune && crit.TopAncestor().type == CreatureTemplate.Type.LizardTemplate
                && getTerrainProximity(pos) > 10)
                return true;
            if (crit.isTooCloseToTerrain == null)
                return false;
            switch (crit.isTooCloseToTerrain(this, pos))
            {
                case 1:
                    return true;
                case 2:
                    result = true;
                    return true;
                default:
                    return false;
            }
        }

        public bool TileOrNeighborsAccessibleToCreature(IntVector2 pos, CreatureTemplate crit)
        {
            for (int i = 0; i < 5; i++)
                if (TileAccessibleToCreature(pos + Custom.fourDirectionsAndZero[i], crit))
                    return true;
            return false;
        }

        public bool ClampedTileAccessibleToCreature(IntVector2 pos, CreatureTemplate crit) =>
            TileAccessibleToCreature(new IntVector2(Clamp(pos.x, 0, width - 1), Clamp(pos.y, 0, height - 1)), crit);

        /// <summary>The tile if accessible, else its first accessible 4-neighbour, else the tile anyway.</summary>
        public IntVector2 TryForAccessibleNeighbor(IntVector2 tile, CreatureTemplate crit)
        {
            if (TileAccessibleToCreature(tile, crit)) return tile;
            for (int i = 0; i < 4; i++)
                if (TileAccessibleToCreature(tile + Custom.fourDirections[i], crit))
                    return tile + Custom.fourDirections[i];
            return tile;
        }

        public PathCost TileCostForCreature(int x, int y, CreatureTemplate crit) => TileCostForCreature(new IntVector2(x, y), crit);

        /// <summary>What standing on the tile costs <paramref name="crit"/> (<c>AImap.TileCostForCreature</c>).</summary>
        public PathCost TileCostForCreature(IntVector2 pos, CreatureTemplate crit)
        {
            if (!TileAccessibleToCreature(pos, crit))
                return new PathCost(0f, PathCost.Legality.IllegalTile);
            var tile = getAItile(pos);
            var cost = crit.AccessibilityResistance(tile.acc);
            if (tile.AnyWater && crit.canSwim && new PathCost(crit.waterPathingResistance, PathCost.Legality.Allowed) < cost)
                cost = new PathCost(crit.waterPathingResistance, PathCost.Legality.Allowed);
            // Batflies prefer open air: the further from terrain, the cheaper.
            if (crit.type == CreatureTemplate.Type.Fly)
                cost.resistance = 100f / getTerrainProximity(pos);
            return cost;
        }

        /// <summary>Whether <paramref name="crit"/> may take the connection (<c>AImap.IsConnectionAllowedForCreature</c>).</summary>
        public bool IsConnectionAllowedForCreature(MovementConnection connection, CreatureTemplate crit)
        {
            var start = connection.StartTile;
            var dest = connection.DestTile;
            if (!TileAccessibleToCreature(start, crit) || !TileAccessibleToCreature(dest, crit)
                || !crit.ConnectionResistance(connection.type).Allowed)
                return false;

            switch (connection.type)
            {
                case MovementConnection.MovementType.DropToClimb:
                case MovementConnection.MovementType.DropToFloor:
                case MovementConnection.MovementType.ReachDown:
                case MovementConnection.MovementType.ReachOverGap:
                case MovementConnection.MovementType.ReachUp:
                {
                    // A reach or drop is only for crossing tiles the creature can't walk through.
                    if (connection.type == MovementConnection.MovementType.ReachUp
                        && getAItile(start).acc == AItile.Accessibility.Floor && getAItile(dest).acc == AItile.Accessibility.Floor)
                    {
                        if (TileAccessibleToCreature(start + new IntVector2(0, 1), crit))
                            return false;
                        if (TileAccessibleToCreature(start + new IntVector2(0, -1), crit))
                            return true;
                        return getAItile(start + new IntVector2(0, -1)).acc == AItile.Accessibility.Solid;
                    }
                    var step = IntVector2.ClampAtOne(dest - start);
                    return !TileAccessibleToCreature(start + step, crit);
                }
                case MovementConnection.MovementType.SemiDiagonalReach:
                {
                    var step = IntVector2.ClampAtOne(dest - start);
                    if (Math.Abs(dest.x - start.x) > Math.Abs(dest.y - start.y)) step.y = 0;
                    else step.x = 0;
                    return !TileAccessibleToCreature(start + step, crit);
                }
                case MovementConnection.MovementType.DoubleReachUp:
                {
                    var p = crit.doubleReachUpConnectionParams;
                    if ((int)getAItile(start).acc > p[0] || (int)getAItile(start + new IntVector2(0, 1)).acc > p[1]
                        || (int)getAItile(start + new IntVector2(0, 2)).acc > p[1] || (int)getAItile(start + new IntVector2(0, 3)).acc > p[2])
                        return false;
                    return !TileAccessibleToCreature(start + new IntVector2(0, 1), crit)
                        && !TileAccessibleToCreature(start + new IntVector2(0, 2), crit);
                }
                case MovementConnection.MovementType.OpenDiagonal:
                    return !TileAccessibleToCreature(new IntVector2(start.x, dest.y), crit)
                        && !TileAccessibleToCreature(new IntVector2(dest.x, start.y), crit);
                default:
                    return true;
            }
        }

        /// <summary>The full cost of taking the connection, landing tile included (<c>AImap.ConnectionCostForCreature</c>).</summary>
        public PathCost ConnectionCostForCreature(MovementConnection connection, CreatureTemplate crit)
        {
            var cost = new PathCost(0f, PathCost.Legality.Allowed);
            if (!IsConnectionAllowedForCreature(connection, crit))
                cost += new PathCost(0f, PathCost.Legality.IllegalConnection);
            var movement = crit.ConnectionResistance(connection.type);
            movement.resistance *= connection.distance;
            cost += movement;
            return cost + TileCostForCreature(connection.DestTile, crit);
        }

        // ---- Terrain changes ----

        /// <summary>
        /// Rebuilds the parts of the map that <paramref name="changes"/> can affect, so the result
        /// matches a map built from scratch. <see cref="Room"/> calls this after applying diffs.
        /// </summary>
        public void ApplyTerrainChanges(TerrainChanges changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            if (changes.Any) new AImapper(this).Rebuild(changes);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
