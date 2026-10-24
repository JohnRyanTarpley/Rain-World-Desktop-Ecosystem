namespace RWDE.Sim.Physics
{
    /// <summary>
    /// Anything in a room with a body made of <see cref="BodyChunk"/>s held together by
    /// <see cref="BodyChunkConnection"/>s, ported from the game's <c>PhysicalObject</c>.
    /// Subclasses set the chunks, connections and physics constants in their constructor, as the game's do.
    /// </summary>
    /// <remarks>
    /// Not ported yet: the abstract-object link, grasps, appendages, weapon and explosion hits, graphics
    /// modules and blizzard wind (<c>WeatherInertia</c>; the desktop has no blizzard).
    /// </remarks>
    public abstract class PhysicalObject : UpdatableAndDeletable
    {
        /// <summary>
        /// A distance constraint between two chunks, the game's <c>PhysicalObject.BodyChunkConnection</c>.
        /// It moves both chunks (and their velocities) toward the rest distance, split by weight.
        /// </summary>
        public class BodyChunkConnection
        {
            public enum Type
            {
                /// <summary>Holds the chunks at the distance from both sides.</summary>
                Normal,
                /// <summary>Only pulls the chunks together when further apart than the distance.</summary>
                Pull,
                /// <summary>Only pushes the chunks apart when closer than the distance.</summary>
                Push,
            }

            public BodyChunk chunk1;
            public BodyChunk chunk2;
            public float distance;
            /// <summary>Fraction of the error corrected per tick.</summary>
            public float elasticity;
            /// <summary>Share of the correction applied to chunk1; chunk2 takes the rest.</summary>
            public float weightSymmetry;
            public bool active;
            public Type type;

            /// <param name="weightSymmetry">-1 to derive it from the masses, as the game does.</param>
            public BodyChunkConnection(BodyChunk chunk1, BodyChunk chunk2, float distance, Type type, float elasticity, float weightSymmetry)
            {
                this.chunk1 = chunk1;
                this.chunk2 = chunk2;
                this.distance = distance;
                this.type = type;
                this.elasticity = elasticity;
                this.weightSymmetry = weightSymmetry == -1f ? chunk2.mass / (chunk1.mass + chunk2.mass) : weightSymmetry;
                active = true;
                chunk1.rotationChunk = chunk2;
                chunk2.rotationChunk = chunk1;
            }

            public void Update()
            {
                if (!active)
                {
                    return;
                }
                float dist = Vector2.Distance(chunk1.pos, chunk2.pos);
                if (type == Type.Normal || (type == Type.Pull && dist > distance) || (type == Type.Push && dist < distance))
                {
                    Vector2 dir = Custom.DirVec(chunk1.pos, chunk2.pos);
                    Vector2 move1 = dir * ((distance - dist) * weightSymmetry * elasticity);
                    Vector2 move2 = dir * ((distance - dist) * (1f - weightSymmetry) * elasticity);
                    chunk1.pos -= move1;
                    chunk1.vel -= move1;
                    chunk2.pos += move2;
                    chunk2.vel += move2;
                }
            }
        }

        public BodyChunkConnection[] bodyChunkConnections;
        public float collisionRange;
        public int collisionLayer;
        private float g;
        public float surfaceFriction;
        public float bounce;
        /// <summary>Speed above which a terrain hit calls <see cref="TerrainImpact"/>.</summary>
        public float impactTreshhold = 1f;

        protected PhysicalObject()
        {
            // PhysicalObject constructor
            collisionRange = 50f;
        }

        public BodyChunk[] bodyChunks { get; protected set; }

        public BodyChunk firstChunk => bodyChunks[0];

        public BodyChunk FirstChunk() => bodyChunks[0];

        public float TotalMass
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    total += bodyChunks[i].mass;
                }
                return total;
            }
        }

        /// <summary>Per-tick downward acceleration: the object's own gravity times the room's.</summary>
        public float gravity
        {
            get => g * room.gravity;
            protected set => g = value;
        }

        /// <summary>Velocity multiplier per tick in air.</summary>
        public float airFriction { get; protected set; }

        // Set by the game's subclasses for water; kept so their constructors port as-is. The desktop has no water.
        public float waterFriction { get; protected set; }
        public float buoyancy { get; protected set; }

        /// <summary>True while a creature holds this object; held objects skip the room-range clamp.</summary>
        public virtual bool IsGrabbed => false;

        public bool GoThroughFloors
        {
            get
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    if (!bodyChunks[i].goThroughFloors) return false;
                }
                return true;
            }
            set
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    bodyChunks[i].goThroughFloors = value;
                }
            }
        }

        public bool CollideWithTerrain
        {
            get
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    if (bodyChunks[i].collideWithTerrain) return true;
                }
                return false;
            }
            set
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    bodyChunks[i].collideWithTerrain = value;
                }
            }
        }

        public bool CollideWithSlopes
        {
            get
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    if (bodyChunks[i].collideWithSlopes) return true;
                }
                return false;
            }
            protected set
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    bodyChunks[i].collideWithSlopes = value;
                }
            }
        }

        public bool CollideWithObjects
        {
            get
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    if (bodyChunks[i].collideWithObjects) return true;
                }
                return false;
            }
            set
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    bodyChunks[i].collideWithObjects = value;
                }
            }
        }

        /// <summary>One tick: every chunk moves and collides with terrain, then the connections are solved.</summary>
        public override void Update(bool eu)
        {
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                bodyChunks[i].Update();
            }
            for (int j = 0; j < bodyChunkConnections.Length; j++)
            {
                bodyChunkConnections[j].Update();
            }
            base.Update(eu);
        }

        /// <summary>Pushes every chunk except <paramref name="exceptedChunk"/> out of a circle.</summary>
        public virtual void PushOutOf(Vector2 pos, float rad, int exceptedChunk)
        {
            foreach (BodyChunk chunk in bodyChunks)
            {
                if (chunk.index != exceptedChunk && Custom.DistLess(chunk.pos, pos, rad + chunk.rad))
                {
                    float dist = Vector2.Distance(chunk.pos, pos);
                    Vector2 dir = Custom.DirVec(chunk.pos, pos);
                    chunk.pos -= (rad + chunk.rad - dist) * dir;
                    chunk.vel -= (rad + chunk.rad - dist) * dir;
                }
            }
        }

        /// <summary>Whether the tile offset from a chunk's tile blocks it; a one-way floor blocks only from above.</summary>
        public bool IsTileSolid(int bChunk, int relativeX, int relativeY)
        {
            IntVector2 tile = RoomGeometry.GetTilePosition(bodyChunks[bChunk].pos) + new IntVector2(relativeX, relativeY);
            switch (room.GetTerrain(tile))
            {
                case TerrainType.Solid:
                    return true;
                case TerrainType.Floor:
                    return relativeY < 0 && !bodyChunks[bChunk].goThroughFloors;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Called when a chunk hits terrain faster than <see cref="impactTreshhold"/>. The game's base
        /// version shakes the screen and makes an in-game noise; noise for AI hearing arrives with the AI work.
        /// </summary>
        public virtual void TerrainImpact(int chunk, IntVector2 direction, float speed, bool firstContact)
        {
        }

        /// <summary>Pushes chunks A and B apart along <paramref name="dir"/>, split by their masses.</summary>
        public void WeightedPush(int A, int B, Vector2 dir, float frc)
        {
            float share = bodyChunks[B].mass / (bodyChunks[A].mass + bodyChunks[B].mass);
            bodyChunks[A].vel += dir * (frc * share);
            bodyChunks[B].vel -= dir * (frc * (1f - share));
        }

        public void SetLocalGravity(float g)
        {
            gravity = g;
        }

        public float GetLocalGravity() => g;

        public void SetLocalAirFriction(float frict)
        {
            airFriction = frict;
        }
    }
}
