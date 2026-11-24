using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// Weighs the utilities of an AI's modules and reports the strongest, ported from the game's
    /// <c>UtilityComparer</c>. Each compared module's utility is raised to an exponent, weighted and
    /// optionally smoothed over time; the AI picks its behaviour from the winner.
    /// </summary>
    /// <remarks>The debug <c>UtilityVisualizer</c> isn't ported.</remarks>
    public class UtilityComparer : AIModule
    {
        /// <summary>One compared module and its smoothed, weighted utility (<c>UtilityComparer.UtilityTracker</c>).</summary>
        public class UtilityTracker
        {
            public float weight;
            /// <summary>Stored as in the game, which never reads it.</summary>
            public float continuationBonus;
            public AIModule module;
            public FloatTweener.FloatTween smoother;
            public float smoothedUtility;
            public float exponent = 1f;

            public UtilityTracker(AIModule module, FloatTweener.FloatTween smoother, float weight, float continuationBonus)
            {
                this.weight = weight;
                this.smoother = smoother;
                this.continuationBonus = continuationBonus;
                this.module = module;
                smoothedUtility = 0f;
            }

            public void UpdateSmoothing()
            {
                if (smoother != null)
                    smoothedUtility = smoother.Tween(smoothedUtility, UnSmoothedUtility());
            }

            private float UnSmoothedUtility() => module == null ? 0f : Mathf.Pow(module.Utility(), exponent) * weight;

            public float SmoothedUtility() => smoother != null ? smoothedUtility : UnSmoothedUtility();
        }

        public List<UtilityTracker> uTrackers = new List<UtilityTracker>();
        public UtilityTracker highestUtilityTracker;

        public UtilityComparer(ArtificialIntelligence AI) : base(AI)
        {
        }

        public void AddComparedModule(AIModule module, FloatTweener.FloatTween smoother, float weight, float continuationBonus)
        {
            uTrackers.Add(new UtilityTracker(module, smoother, weight, continuationBonus));
        }

        public override void Update()
        {
            float highest = float.MinValue;
            highestUtilityTracker = null;
            for (int i = 0; i < uTrackers.Count; i++)
            {
                uTrackers[i].UpdateSmoothing();
                if (uTrackers[i].SmoothedUtility() > highest)
                {
                    highest = uTrackers[i].SmoothedUtility();
                    highestUtilityTracker = uTrackers[i];
                }
            }
        }

        public float GetSmoothedNonWeightedUtility(AIModule module)
        {
            for (int i = 0; i < uTrackers.Count; i++)
                if (uTrackers[i].module == module)
                    return uTrackers[i].SmoothedUtility() / uTrackers[i].weight;
            return 0f;
        }

        public UtilityTracker GetUtilityTracker(AIModule module)
        {
            for (int i = 0; i < uTrackers.Count; i++)
                if (uTrackers[i].module == module)
                    return uTrackers[i];
            return null;
        }

        public AIModule HighestUtilityModule() => highestUtilityTracker?.module;

        public float HighestUtility() => highestUtilityTracker == null ? 0f : highestUtilityTracker.SmoothedUtility();
    }
}
