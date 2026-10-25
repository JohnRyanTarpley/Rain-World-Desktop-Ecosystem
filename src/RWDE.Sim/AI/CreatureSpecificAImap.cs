using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// One pre-baked creature's view of a room, ported from the game's <c>CreatureSpecificAImap</c>: for
    /// every tile it can be on, the distance (in steps) to each node it maps, and how central the tile is
    /// among the tiles that lead to an exit. Get it from <see cref="AImap.CreatureSpecificAImap"/>.
    /// </summary>
    /// <remarks>
    /// The game bakes these maps, and each node's connectivity, into the level files. The desktop builds
    /// them on first use with <see cref="AIdataPreprocessor"/> and drops them when the terrain changes.
    /// The node connectivity (the game's <c>AbstractRoomNode.connectivity</c>) is kept here too.
    /// </remarks>
    public sealed class CreatureSpecificAImap
    {
        private readonly int[,,] intGrid;
        private readonly float[,] floatGrid;
        private readonly IntRect coveredArea;
        // [from node, to node, 0 = cost / 1 = length], room node indices; -1 where there is no way.
        private readonly int[,,] connectivity;

        public readonly IntVector2[] accessableTiles;
        public readonly int numberOfNodes;

        internal CreatureSpecificAImap(AImap aiMap, CreatureTemplate crit)
        {
            numberOfNodes = aiMap.room.abstractRoom.NodesRelevantToCreature(crit);
            int left = aiMap.width, top = 0, right = 0, bottom = aiMap.height;
            bool any = false;
            for (int x = 0; x < aiMap.width; x++)
                for (int y = 0; y < aiMap.height; y++)
                    if (aiMap.TileAccessibleToCreature(x, y, crit))
                    {
                        any = true;
                        if (x < left) left = x;
                        if (y < bottom) bottom = y;
                        if (x > right) right = x;
                        if (y > top) top = y;
                    }
            coveredArea = new IntRect(left, bottom, right, top);
            if (any)
            {
                intGrid = new int[right - left + 1, top - bottom + 1, numberOfNodes];
                floatGrid = new float[aiMap.width, aiMap.height];
            }
            else
            {
                intGrid = new int[1, 1, numberOfNodes];
                floatGrid = new float[1, 1];
            }
            for (int x = 0; x <= right - left; x++)
                for (int y = 0; y <= top - bottom; y++)
                    for (int n = 0; n < numberOfNodes; n++)
                        intGrid[x, y, n] = -1;

            var tiles = new List<IntVector2>();
            for (int x = 0; x < aiMap.width; x++)
                for (int y = 0; y < aiMap.height; y++)
                    if (aiMap.TileAccessibleToCreature(x, y, crit))
                    {
                        for (int n = 0; n < numberOfNodes; n++)
                            intGrid[x - coveredArea.left, y - coveredArea.bottom, n] = 0;
                        tiles.Add(new IntVector2(x, y));
                    }
            accessableTiles = tiles.ToArray();

            int roomNodes = aiMap.room.abstractRoom.nodes.Length;
            connectivity = new int[roomNodes, roomNodes, 2];
            for (int i = 0; i < roomNodes; i++)
                for (int j = 0; j < roomNodes; j++)
                {
                    connectivity[i, j, 0] = -1;
                    connectivity[i, j, 1] = -1;
                }
        }

        /// <summary>Straight-line distance in tiles, plus one (<c>CreatureSpecificAImap.TriangulateDistance</c>).</summary>
        public int TriangulateDistance(IntVector2 A, IntVector2 B) => (int)Vector2.Distance(IntVector2.ToVector2(A), IntVector2.ToVector2(B)) + 1;

        /// <summary>
        /// Steps from the tile to the creature's <paramref name="creatureSpecificExitIndex"/>-th node: 1 on
        /// its entrance, 0 where the node can't be reached, -1 on tiles the creature can't be on.
        /// </summary>
        public int GetDistanceToExit(int x, int y, int creatureSpecificExitIndex)
        {
            if (creatureSpecificExitIndex == -1)
                return -1;
            if (Custom.InsideRect(x, y, coveredArea) && creatureSpecificExitIndex < numberOfNodes)
                return intGrid[x - coveredArea.left, y - coveredArea.bottom, creatureSpecificExitIndex];
            return -1;
        }

        /// <summary>How central the tile is among those that lead to an exit, 1 at the most central; -1 outside the map.</summary>
        public float GetAccessibility(int x, int y)
        {
            if (x >= 0 && x < floatGrid.GetLength(0) && y >= 0 && y < floatGrid.GetLength(1))
                return floatGrid[x, y];
            return -1f;
        }

        internal void SetDistanceToExit(int x, int y, int exitNumber, int i)
        {
            if (Custom.InsideRect(x, y, coveredArea) && intGrid[x - coveredArea.left, y - coveredArea.bottom, exitNumber] != -1 && i > -1)
                intGrid[x - coveredArea.left, y - coveredArea.bottom, exitNumber] = i;
        }

        internal void SetAccessibility(int x, int y, float f)
        {
            if (x >= 0 && x < floatGrid.GetLength(0) && y >= 0 && y < floatGrid.GetLength(1))
                floatGrid[x, y] = f;
        }

        internal int GetConnectivity(int from, int to, int part) =>
            from >= 0 && to >= 0 && from < connectivity.GetLength(0) && to < connectivity.GetLength(1) ? connectivity[from, to, part] : -1;

        internal void SetConnectivity(int from, int to, int cost, int length)
        {
            connectivity[from, to, 0] = cost;
            connectivity[from, to, 1] = length;
        }
    }
}
