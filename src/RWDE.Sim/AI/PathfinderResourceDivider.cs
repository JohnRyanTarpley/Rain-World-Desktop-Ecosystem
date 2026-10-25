using System;

namespace RWDE.Sim
{
    /// <summary>
    /// Shares a per-tick budget of pathfinding steps between all pathfinders, ported from the game's
    /// <c>PathfinderResourceDivider</c>: when last tick's requests went over budget, each request gets
    /// its share of the budget (at least 1).
    /// </summary>
    public sealed class PathfinderResourceDivider
    {
        private int lastTotalRequestedUpdates;
        private int requestedUpdates;
        private readonly int budget = 50;
        private int lastTotalRequestedAccUpdates;
        private int requestedAccUpdates;
        private readonly int accessibilityBudget = 500;

        public void Update()
        {
            lastTotalRequestedUpdates = requestedUpdates;
            requestedUpdates = 0;
            lastTotalRequestedAccUpdates = requestedAccUpdates;
            requestedAccUpdates = 0;
        }

        public int RequestPathfinderUpdates(int ideal)
        {
            requestedUpdates += ideal;
            if (lastTotalRequestedUpdates <= budget)
                return ideal;
            return Math.Max(1, (int)Math.Floor((float)ideal / lastTotalRequestedUpdates * budget));
        }

        // The game's spelling.
        public int RequesAccesibilityUpdates(int ideal)
        {
            requestedAccUpdates += ideal;
            if (lastTotalRequestedAccUpdates <= accessibilityBudget)
                return ideal;
            return Math.Max(1, (int)Math.Floor((float)ideal / lastTotalRequestedAccUpdates * accessibilityBudget));
        }
    }
}
