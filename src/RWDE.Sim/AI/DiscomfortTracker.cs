namespace RWDE.Sim
{
    /// <summary>
    /// How unpleasant tiles are to be on, ported from the game's <c>DiscomfortTracker</c>: near creatures
    /// the AI is Uncomfortable with, near unexplained noises (if it minds them), and where threats are.
    /// The pathfinder prices steps by it (<see cref="ArtificialIntelligence.TravelPreference"/>).
    /// </summary>
    /// <remarks>
    /// Not ported: the uncomfortable item (needs the item tracker), blizzard heat sources and Watcher
    /// locusts, none of which exist on the desktop.
    /// </remarks>
    public class DiscomfortTracker : AIModule
    {
        private readonly Tracker tracker;
        private readonly float uncomfortableWithUnknownNoises;

        public DiscomfortTracker(ArtificialIntelligence AI, Tracker tracker, float uncomfortableWithUnknownNoises) : base(AI)
        {
            this.tracker = tracker;
            this.uncomfortableWithUnknownNoises = uncomfortableWithUnknownNoises;
        }

        /// <summary>The Uncomfortable-relationship creature that is closest and most recently seen.</summary>
        public Tracker.CreatureRepresentation MostUncomfortableCreature()
        {
            Tracker.CreatureRepresentation result = null;
            float best = 0f;
            for (int i = 0; i < tracker.CreaturesCount; i++)
            {
                var rep = tracker.GetRep(i);
                var rel = rep.dynamicRelationship.currentRelationship;
                if (rel.type != CreatureTemplate.Relationship.Type.Uncomfortable || rep.BestGuessForPosition().room != AI.creature.pos.room)
                    continue;
                float score = Mathf.InverseLerp(1f + rel.intensity * 40f, 0f, rep.BestGuessForPosition().Tile.FloatDist(AI.creature.pos.Tile));
                score *= Mathf.InverseLerp(1000f, 50f, rep.TicksSinceSeen);
                if (score > best)
                {
                    best = score;
                    result = rep;
                }
            }
            return result;
        }

        /// <summary>How unpleasant <paramref name="test"/> is, 0 and up (<c>DiscomfortTracker.DiscomfortOfTile</c>).</summary>
        public float DiscomfortOfTile(WorldCoordinate test)
        {
            if (test.room != AI.creature.pos.room || !test.TileDefined)
                return 0f;
            var room = AI.creature.realizedCreature.room;
            float discomfort = 0f;
            for (int i = 0; i < tracker.CreaturesCount; i++)
            {
                var rep = tracker.GetRep(i);
                var rel = rep.dynamicRelationship.currentRelationship;
                if (rel.type != CreatureTemplate.Relationship.Type.Uncomfortable || rep.BestGuessForPosition().room != test.room)
                    continue;
                float near = Mathf.InverseLerp(1f + rel.intensity * 40f, 0f, rep.BestGuessForPosition().Tile.FloatDist(test.Tile));
                near *= Mathf.InverseLerp(1000f, 50f, rep.TicksSinceSeen);
                near *= 0.5f + 0.5f * room.aimap.AccessibilityForCreature(test.Tile, rep.representedCreature.creatureTemplate);
                discomfort = Mathf.Lerp(discomfort, Mathf.Max(discomfort, Mathf.Pow(near, 0.5f)), 0.5f + 0.5f * rel.intensity);
            }
            var noises = tracker.noiseTracker;
            if (uncomfortableWithUnknownNoises > 0f && noises != null && noises.Utility() > 0f)
            {
                for (int j = 0; j < noises.sources.Count; j++)
                {
                    if (noises.sources[j].creatureRep != null)
                        continue;
                    float near = Mathf.InverseLerp(600f, 0f, Vector2.Distance(noises.sources[j].pos, room.MiddleOfTile(test.Tile)));
                    near *= Mathf.InverseLerp(1000f, 50f, noises.sources[j].age);
                    discomfort = Mathf.Lerp(discomfort, Mathf.Max(discomfort, Mathf.Pow(near, 0.5f)), (0.5f + 0.5f * noises.Utility()) * uncomfortableWithUnknownNoises);
                }
            }
            if (AI.threatTracker != null)
                discomfort = Mathf.Lerp(discomfort, 1f, Mathf.Pow(Mathf.InverseLerp(0.1f, 1f, AI.threatTracker.ThreatOfTile(test, true)), 0.25f));
            return discomfort;
        }

        public bool TileUncomfortable(WorldCoordinate test) => DiscomfortOfTile(test) > 0f;
    }
}
