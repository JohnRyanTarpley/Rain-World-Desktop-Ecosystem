using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A sound made in a room, which nearby creatures may hear (the game's <c>Noise.InGameNoise</c>).
    /// </summary>
    public struct InGameNoise : IEquatable<InGameNoise>
    {
        public Vector2 pos;
        /// <summary>How far away it can be heard, in pixels, before hearing skill is applied.</summary>
        public float strength;
        public PhysicalObject sourceObject;
        /// <summary>How much a hearer that can't place the noise wants to check it out.</summary>
        public float interesting;

        public InGameNoise(Vector2 pos, float strength, PhysicalObject sourceObject, float interesting)
        {
            this.pos = pos;
            this.strength = strength;
            this.sourceObject = sourceObject;
            this.interesting = interesting;
        }

        public bool Equals(InGameNoise other) =>
            pos.Equals(other.pos) && strength.Equals(other.strength) && Equals(sourceObject, other.sourceObject) && interesting.Equals(other.interesting);

        public override bool Equals(object obj) => obj is InGameNoise other && Equals(other);

        public override int GetHashCode() =>
            (((pos.GetHashCode() * 397 ^ strength.GetHashCode()) * 397 ^ (sourceObject != null ? sourceObject.GetHashCode() : 0)) * 397) ^ interesting.GetHashCode();

        public static bool operator ==(InGameNoise left, InGameNoise right) => left.Equals(right);
        public static bool operator !=(InGameNoise left, InGameNoise right) => !left.Equals(right);
    }

    /// <summary>An AI that reacts when its noise tracker places a noise (the game's <c>IAINoiseReaction</c>).</summary>
    public interface IAINoiseReaction
    {
        void ReactToNoise(NoiseTracker.TheorizedSource source, InGameNoise noise);
    }

    /// <summary>
    /// Works out where heard noises came from, ported from the game's <c>NoiseTracker</c>. Each noise is
    /// matched to a known creature (moving its tracker guess there) or to an unexplained source; the
    /// nearest unexplained source is the one to go and examine, and its utility grows with how many
    /// mysterious noises were heard.
    /// </summary>
    /// <remarks>
    /// Nothing makes noises on the desktop yet (<see cref="PhysicalObject.TerrainImpact"/> is a no-op),
    /// so this stays quiet until noises exist. Desktop gaps: no ripple layers, no water (noises are never
    /// muffled by it), no room background noise, and the hearer's deafness isn't exposed by
    /// <see cref="Creature"/>, so it counts as 0. The game's "is the source a rock" check (rocks are 5
    /// times as interesting) waits for throwable objects.
    /// </remarks>
    public class NoiseTracker : AIModule
    {
        /// <summary>A guessed origin of one or more noises (<c>NoiseTracker.TheorizedSource</c>).</summary>
        public class TheorizedSource
        {
            public TheorizedSource(NoiseTracker noiseTracker, Vector2 pos, Tracker.CreatureRepresentation creatureRep)
            {
                this.noiseTracker = noiseTracker;
                this.pos = pos;
                this.creatureRep = creatureRep;
                age = 0;
            }

            public NoiseTracker noiseTracker;
            public Vector2 pos;
            public int age;
            public bool slatedForDeletion;
            /// <summary>The creature thought to have made it, or null when unexplained.</summary>
            public Tracker.CreatureRepresentation creatureRep;
            public float LASTSOUNDSTRENGTH;

            public void Update()
            {
                age++;
                if (creatureRep != null && creatureRep.deleteMeNextFrame)
                    creatureRep = null;
                if (age > noiseTracker.forgetTime)
                    Destroy();
                LASTSOUNDSTRENGTH = Mathf.Max(10f, LASTSOUNDSTRENGTH * 0.6f);
            }

            public void Destroy() => slatedForDeletion = true;

            /// <summary>How badly <paramref name="noise"/> fits this source; lower is better.</summary>
            public float NoiseMatch(InGameNoise noise)
            {
                if (creatureRep == null)
                    return Vector2.Distance(pos, noise.pos) + 200f + age / 5f;
                return noiseTracker.NoiseMatch(noise, creatureRep) + age / 5f;
            }

            /// <summary>The source made <paramref name="noise"/>: it moves there, and so does the guess of its creature.</summary>
            public void Refresh(InGameNoise noise)
            {
                pos = noise.pos;
                age = 0;
                LASTSOUNDSTRENGTH = noise.strength;
                if (creatureRep == null)
                    return;
                creatureRep.HeardThisCreature();
                var room = noiseTracker.room;
                bool accessible = room.aimap.TileOrNeighborsAccessibleToCreature(room.GetTilePosition(noise.pos), creatureRep.representedCreature.creatureTemplate);
                if (creatureRep is Tracker.SimpleCreatureRepresentation simple && accessible)
                {
                    simple.lastSeenCoord = room.GetWorldCoordinate(noise.pos);
                    return;
                }
                if (!(creatureRep is Tracker.ElaborateCreatureRepresentation elaborate))
                    return;
                float closest = float.MaxValue;
                Tracker.Ghost ghost = null;
                for (int i = 0; i < elaborate.ghosts.Count; i++)
                {
                    var g = elaborate.ghosts[i];
                    if (g.coord.room == room.abstractRoom.index && Custom.DistLess(noise.pos, room.MiddleOfTile(g.coord.Tile), closest))
                    {
                        closest = Vector2.Distance(noise.pos, room.MiddleOfTile(g.coord.Tile));
                        ghost = g;
                    }
                }
                if (ghost == null)
                    return;
                if (accessible)
                {
                    if (closest < 100f)
                    {
                        ghost.Move(room.GetWorldCoordinate(noise.pos), noise.pos);
                        if (ghost.vel.magnitude == 0f)
                            ghost.vel = Custom.RNV(noiseTracker.AI.Random);
                        ghost.lastCoord = new WorldCoordinate(-1, -1, -1, -1);
                    }
                    else
                    {
                        ghost = ghost.Clone(room.GetWorldCoordinate(noise.pos), 0);
                    }
                }
                for (int j = 0; j < elaborate.ghosts.Count; j++)
                    elaborate.ghosts[j].generation++;
                ghost.generation = 0;
                elaborate.bestGhostDirty = true;
            }
        }

        public NoiseTracker(ArtificialIntelligence AI, Tracker tracker) : base(AI)
        {
            this.tracker = tracker;
            tracker.noiseTracker = this;
            forgetTime = 1200;
        }

        public Tracker tracker;
        public Room room;
        public List<TheorizedSource> sources = new List<TheorizedSource>();
        public bool ignoreSeenNoises = true;
        public int forgetTime;
        public float hearingSkill;
        public float mysteriousNoises;
        public int mysteriousNoiseCounter;
        /// <summary>The nearest unexplained source, which the creature may go and look at.</summary>
        public TheorizedSource soundToExamine;

        public WorldCoordinate ExaminePos => room.GetWorldCoordinate(soundToExamine.pos);

        public override float Utility() =>
            soundToExamine == null ? 0f : Mathf.Pow(Mathf.InverseLerp(1f, 20f, mysteriousNoises), 0.5f);

        public override void NewRoom(Room room)
        {
            if (this.room == room)
                return;
            this.room = room;
            sources.Clear();
            UpdateExamineSound();
        }

        public override void Update()
        {
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (sources[i].slatedForDeletion)
                {
                    sources.RemoveAt(i);
                    UpdateExamineSound();
                }
                else
                {
                    sources[i].Update();
                }
            }
            if (soundToExamine != null && (Custom.ManhattanDistance(AI.creature.pos, ExaminePos) < 3
                || (Custom.ManhattanDistance(AI.creature.pos, ExaminePos) < 10 && AI.VisualContact(soundToExamine.pos, 0f))))
            {
                soundToExamine.Destroy();
                UpdateExamineSound();
            }
            if (mysteriousNoiseCounter > 0)
                mysteriousNoiseCounter--;
            else if (mysteriousNoises > 0f)
                mysteriousNoises -= 0.0125f; // the game wraps this in a Mathf.Max whose result it discards
        }

        public void ClearAllUnkown()
        {
            for (int i = 0; i < sources.Count; i++)
                if (sources[i].creatureRep == null)
                    sources[i].Destroy();
        }

        /// <summary>
        /// The creature may have heard <paramref name="noise"/> (<c>NoiseTracker.HeardNoise</c>): if it is in
        /// earshot and not obviously from something in sight, it is matched to an existing source, a tracked
        /// creature, or a new mysterious source.
        /// </summary>
        public void HeardNoise(InGameNoise noise)
        {
            var me = AI.creature.realizedCreature;
            const float deaf = 0f;
            const float backgroundNoise = 0f;
            if (!Custom.DistLess(me.mainBodyChunk.pos, noise.pos, noise.strength * (1f - deaf) * hearingSkill * (1f - backgroundNoise)))
                return;
            if (ignoreSeenNoises)
            {
                if (AI.VisualContact(noise.pos, 0f))
                    return;
                var heardRep = tracker.RepresentationForObject(noise.sourceObject, false);
                if (heardRep == null && noise.sourceObject != null)
                {
                    for (int i = 0; i < noise.sourceObject.grabbedBy.Count && heardRep == null; i++)
                        heardRep = tracker.RepresentationForObject(noise.sourceObject.grabbedBy[i].grabber, false);
                }
                if (heardRep != null && heardRep.VisualContact)
                    return;
            }

            float best = float.MaxValue;
            TheorizedSource source = null;
            for (int j = 0; j < sources.Count; j++)
            {
                float match = sources[j].NoiseMatch(noise);
                float limit = sources[j].creatureRep != null ? Custom.LerpMap(sources[j].creatureRep.TicksSinceSeen, 20f, 600f, 200f, 1000f) : 300f;
                if (match < best && match < limit)
                {
                    best = match;
                    source = sources[j];
                }
            }
            if (source != null)
            {
                if (source.creatureRep == null && source.age > 10)
                {
                    mysteriousNoises += HowInterestingIsThisNoiseToMe(noise);
                    mysteriousNoiseCounter = 200;
                }
                source.Refresh(noise);
            }
            else
            {
                Tracker.CreatureRepresentation culprit = null;
                best = float.MaxValue;
                int unseen = 0;
                for (int k = 0; k < tracker.CreaturesCount; k++)
                {
                    var rep = tracker.GetRep(k);
                    float match = NoiseMatch(noise, rep);
                    if (match < best && match < Custom.LerpMap(rep.TicksSinceSeen, 20f, 600f, 200f, 1000f))
                    {
                        best = match;
                        culprit = rep;
                    }
                    if (!rep.VisualContact)
                        unseen++;
                }
                if (best > Custom.LerpMap(unseen, 0f, tracker.maxTrackedCreatures, 1000f, 300f))
                    culprit = null;
                if (culprit == null)
                {
                    mysteriousNoises += HowInterestingIsThisNoiseToMe(noise);
                    mysteriousNoiseCounter = 200;
                }
                source = new TheorizedSource(this, noise.pos, culprit);
                sources.Add(source);
                source.Refresh(noise);
            }
            UpdateExamineSound();
            (AI as IAINoiseReaction)?.ReactToNoise(source, noise);
        }

        /// <summary>Noises from the creature's own kind are a tenth as interesting (<c>HowInterestingIsThisNoiseToMe</c>).</summary>
        public float HowInterestingIsThisNoiseToMe(InGameNoise noise)
        {
            if (noise.sourceObject is Creature creature && creature.Template.TopAncestor().type == AI.creature.creatureTemplate.TopAncestor().type)
                return noise.interesting * 0.1f;
            return noise.interesting;
        }

        private float NoiseMatch(InGameNoise noise, Tracker.CreatureRepresentation critRep)
        {
            if (critRep.VisualContact && ignoreSeenNoises)
                return float.MaxValue;
            float dist = Vector2.Distance(noise.pos, room.MiddleOfTile(critRep.BestGuessForPosition().Tile));
            if (critRep is Tracker.ElaborateCreatureRepresentation elaborate)
            {
                for (int i = 0; i < elaborate.ghosts.Count; i++)
                {
                    var coord = elaborate.ghosts[i].coord;
                    if (coord.room == room.abstractRoom.index && Custom.DistLess(noise.pos, room.MiddleOfTile(coord.Tile), dist))
                        dist = Vector2.Distance(noise.pos, room.MiddleOfTile(coord.Tile));
                }
            }
            return dist;
        }

        // NoiseTracker.UpdateExamineSound: the closest unexplained source, unreachable and old ones counting as further.
        private void UpdateExamineSound()
        {
            soundToExamine = null;
            var me = AI.creature.realizedCreature;
            if (me == null)
                return;
            float best = float.MaxValue;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].slatedForDeletion)
                    continue;
                float score = Vector2.Distance(me.mainBodyChunk.pos, sources[i].pos);
                if (AI.pathFinder != null && !AI.pathFinder.CoordinateReachableAndGetbackable(me.room.GetWorldCoordinate(sources[i].pos)))
                    score += 500f;
                score += Custom.LerpMap(sources[i].age, forgetTime / 2, forgetTime, 0f, 400f);
                if (sources[i].creatureRep == null && score < best)
                {
                    best = score;
                    soundToExamine = sources[i];
                }
            }
        }
    }
}
