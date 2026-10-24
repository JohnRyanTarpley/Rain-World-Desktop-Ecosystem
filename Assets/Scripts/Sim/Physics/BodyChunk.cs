namespace RWDE.Sim
{
    /// <summary>
    /// One circular mass of a <see cref="PhysicalObject"/>, ported from the game's <c>BodyChunk</c>:
    /// gravity, air friction, Verlet-style position update and tile collision against walls,
    /// floors, ceilings, one-way floors and slopes.
    /// </summary>
    /// <remarks>
    /// Not ported, because the desktop has none of it: water (<c>submersion</c>, buoyancy, splashes),
    /// sand burrowing (<c>burrow</c>, <c>buried</c>) and Watcher terrain curves (<c>room.terrain</c>,
    /// <c>terrainCurveNormal</c>). <c>MoveWithOtherObject</c> needs the room's chunk glue, and
    /// <c>goThroughFloors</c> lacks the game's "grabbed by a creature that can pull it into a shortcut"
    /// override; both wait for the systems they depend on.
    /// </remarks>
    public class BodyChunk
    {
        // BodyChunk.MaxRepeats
        private const int MaxRepeats = 100000;

        public Vector2 pos;
        public Vector2 lastPos;
        public Vector2 lastLastPos;
        /// <summary>A position to jump to at the start of this chunk's next update.</summary>
        public Vector2? setPos;
        public Vector2 vel;
        public float rad;
        /// <summary>Scales <see cref="rad"/> for terrain collision only.</summary>
        public float terrainSqueeze;
        private float slopeRad;
        public float mass;
        public BodyChunk rotationChunk;
        public bool collideWithTerrain;
        public bool collideWithSlopes;
        public bool goThroughFloors;
        public bool collideWithObjects = true;
        public float loudness = 1f;
        public int index;
        public float restrictInRoomRange = 1200f;
        public float defaultRestrictInRoomRange = 1200f;
        private IntVector2 contactPoint;
        public IntVector2 lastContactPoint;

        public BodyChunk(PhysicalObject owner, int index, Vector2 pos, float rad, float mass)
        {
            this.owner = owner;
            this.index = index;
            this.pos = pos;
            lastPos = pos;
            lastLastPos = pos;
            vel = new Vector2(0f, 0f);
            this.rad = rad;
            this.mass = mass;
            contactPoint = new IntVector2(0, 0);
            onSlope = 0;
            terrainSqueeze = 1f;
            collideWithTerrain = true;
            goThroughFloors = false;
            collideWithSlopes = true;
        }

        public PhysicalObject owner { get; private set; }

        /// <summary>The side touching terrain this tick: x -1/1 = left/right wall, y -1/1 = floor/ceiling.</summary>
        public IntVector2 ContactPoint => contactPoint;

        /// <summary>-1 or 1 while resting on a slope that falls to the left or right; 0 otherwise.</summary>
        public int onSlope { get; private set; }

        private float TerrainRad => Mathf.Max(rad * terrainSqueeze, 1f);

        /// <summary>Direction from <see cref="rotationChunk"/> to this chunk, or up when there is none.</summary>
        public Vector2 Rotation => rotationChunk == null ? new Vector2(0f, 1f) : (pos - rotationChunk.pos).normalized;

        public void Update()
        {
            if (float.IsNaN(vel.y)) vel.y = 0f;
            if (float.IsNaN(vel.x)) vel.x = 0f;
            vel.y -= owner.gravity;
            vel *= owner.airFriction;
            lastLastPos = lastPos;
            lastPos = pos;
            if (setPos.HasValue)
            {
                pos = setPos.Value;
                setPos = null;
            }
            else
            {
                pos += vel;
            }
            onSlope = 0;
            slopeRad = TerrainRad;
            lastContactPoint = contactPoint;
            if (collideWithTerrain)
            {
                CheckVerticalCollision();
                if (collideWithSlopes)
                {
                    CheckAgainstSlopesVertically();
                }
                CheckHorizontalCollision();
            }
            else
            {
                contactPoint.x = 0;
                contactPoint.y = 0;
            }
            if (!owner.IsGrabbed)
            {
                Room room = owner.room;
                if (pos.x < -restrictInRoomRange)
                {
                    vel.x = 0f;
                    pos.x = -restrictInRoomRange;
                }
                else if (pos.x > room.PixelWidth + restrictInRoomRange)
                {
                    vel.x = 0f;
                    pos.x = room.PixelWidth + restrictInRoomRange;
                }
                if (pos.y < -restrictInRoomRange)
                {
                    vel.y = 0f;
                    pos.y = -restrictInRoomRange;
                }
                else if (pos.y > room.PixelHeight + restrictInRoomRange)
                {
                    vel.y = 0f;
                    pos.y = room.PixelHeight + restrictInRoomRange;
                }
            }
        }

        /// <summary>Teleports the chunk with no velocity carried over from the old position.</summary>
        public void HardSetPosition(Vector2 newPos)
        {
            pos = newPos;
            lastPos = newPos;
            lastLastPos = newPos;
            if (setPos.HasValue)
            {
                setPos = newPos;
            }
        }

        /// <summary>
        /// Moves the chunk from another object's update. If this object has not updated yet this tick,
        /// the move is deferred to its update so it isn't overwritten.
        /// </summary>
        public void MoveFromOutsideMyUpdate(bool eu, Vector2 moveTo)
        {
            if (owner.evenUpdate == eu)
            {
                pos = moveTo;
            }
            else
            {
                setPos = moveTo;
            }
        }

        public void RelativeMoveFromOutsideMyUpdate(bool eu, Vector2 move)
        {
            if (owner.evenUpdate == eu)
            {
                pos += move;
            }
            else
            {
                setPos = pos + move;
            }
        }

        // BodyChunk.CheckHorizontalCollision: sweeps the tile columns crossed this tick and stops at the
        // first solid tile with open space on the side the chunk came from.
        private void CheckHorizontalCollision()
        {
            Room room = owner.room;
            contactPoint.x = 0;
            IntVector2 tilePosition = room.GetTilePosition(lastPos);
            int repeats = 0;
            if (vel.x > 0f)
            {
                int xEnd = room.GetTilePosition(new Vector2(pos.x + TerrainRad + 0.01f, 0f)).x;
                int xStart = room.GetTilePosition(new Vector2(lastPos.x + TerrainRad, 0f)).x;
                int yTop = room.GetTilePosition(new Vector2(0f, pos.y + TerrainRad - 1f)).y;
                int yBottom = room.GetTilePosition(new Vector2(0f, pos.y - TerrainRad + 1f)).y;
                bool hit = false;
                for (int i = xStart; i <= xEnd && !hit; i++)
                {
                    for (int j = yBottom; j <= yTop && !hit; j++)
                    {
                        if (room.GetTile(i, j).Terrain == Room.Tile.TerrainType.Solid && room.GetTile(i - 1, j).Terrain != Room.Tile.TerrainType.Solid
                            && (tilePosition.x < i || room.GetTile(lastPos).Terrain == Room.Tile.TerrainType.Solid))
                        {
                            pos.x = i * 20f - TerrainRad;
                            if (vel.x > owner.impactTreshhold)
                            {
                                owner.TerrainImpact(index, new IntVector2(1, 0), Mathf.Abs(vel.x), lastContactPoint.x < 1);
                            }
                            contactPoint.x = 1;
                            vel.x = -Mathf.Abs(vel.x) * owner.bounce;
                            if (Mathf.Abs(vel.x) < 1f + 9f * (1f - owner.bounce))
                            {
                                vel.x = 0f;
                            }
                            vel.y *= Mathf.Clamp(owner.surfaceFriction * 2f, 0f, 1f);
                            hit = true;
                        }
                        if (++repeats > MaxRepeats) hit = true;
                    }
                }
            }
            else if (vel.x < 0f)
            {
                int xEnd = room.GetTilePosition(new Vector2(pos.x - TerrainRad - 0.01f, 0f)).x;
                int xStart = room.GetTilePosition(new Vector2(lastPos.x - TerrainRad, 0f)).x;
                int yTop = room.GetTilePosition(new Vector2(0f, pos.y + TerrainRad - 1f)).y;
                int yBottom = room.GetTilePosition(new Vector2(0f, pos.y - TerrainRad + 1f)).y;
                bool hit = false;
                for (int i = xStart; i >= xEnd && !hit; i--)
                {
                    for (int j = yBottom; j <= yTop && !hit; j++)
                    {
                        if (room.GetTile(i, j).Terrain == Room.Tile.TerrainType.Solid && room.GetTile(i + 1, j).Terrain != Room.Tile.TerrainType.Solid
                            && (tilePosition.x > i || room.GetTile(lastPos).Terrain == Room.Tile.TerrainType.Solid))
                        {
                            pos.x = (i + 1f) * 20f + TerrainRad;
                            if (vel.x < -owner.impactTreshhold)
                            {
                                owner.TerrainImpact(index, new IntVector2(-1, 0), Mathf.Abs(vel.x), lastContactPoint.x > -1);
                            }
                            contactPoint.x = -1;
                            vel.x = Mathf.Abs(vel.x) * owner.bounce;
                            if (Mathf.Abs(vel.x) < 1f + 9f * (1f - owner.bounce))
                            {
                                vel.x = 0f;
                            }
                            vel.y *= Mathf.Clamp(owner.surfaceFriction * 2f, 0f, 1f);
                            hit = true;
                        }
                        if (++repeats > MaxRepeats) hit = true;
                    }
                }
            }
        }

        // BodyChunk.CheckVerticalCollision: as the horizontal check, against ceilings when rising and
        // floors (solid or one-way) when falling. A landing bounce smaller than gravity is killed.
        private void CheckVerticalCollision()
        {
            Room room = owner.room;
            contactPoint.y = 0;
            IntVector2 tilePosition = room.GetTilePosition(lastPos);
            int repeats = 0;
            if (pos.y > lastPos.y)
            {
                int yEnd = room.GetTilePosition(new Vector2(0f, pos.y + TerrainRad + 0.01f)).y;
                int yStart = room.GetTilePosition(new Vector2(0f, lastPos.y + TerrainRad)).y;
                int xLeft = room.GetTilePosition(new Vector2(pos.x - TerrainRad + 1f, 0f)).x;
                int xRight = room.GetTilePosition(new Vector2(pos.x + TerrainRad - 1f, 0f)).x;
                bool hit = false;
                for (int i = yStart; i <= yEnd && !hit; i++)
                {
                    for (int j = xLeft; j <= xRight && !hit; j++)
                    {
                        if (room.GetTile(j, i).Terrain == Room.Tile.TerrainType.Solid && room.GetTile(j, i - 1).Terrain != Room.Tile.TerrainType.Solid
                            && (tilePosition.y < i || room.GetTile(lastPos).Terrain == Room.Tile.TerrainType.Solid))
                        {
                            pos.y = i * 20f - TerrainRad;
                            if (vel.y > owner.impactTreshhold)
                            {
                                owner.TerrainImpact(index, new IntVector2(0, 1), Mathf.Abs(vel.y), lastContactPoint.y < 1);
                            }
                            contactPoint.y = 1;
                            vel.y = -Mathf.Abs(vel.y) * owner.bounce;
                            if (Mathf.Abs(vel.y) < 1f + 9f * (1f - owner.bounce))
                            {
                                vel.y = 0f;
                            }
                            vel.x *= Mathf.Clamp(owner.surfaceFriction * 2f, 0f, 1f);
                            hit = true;
                        }
                        if (++repeats > MaxRepeats) hit = true;
                    }
                }
            }
            else if (pos.y < lastPos.y)
            {
                int yEnd = room.GetTilePosition(new Vector2(0f, pos.y - TerrainRad - 0.01f)).y;
                int yStart = room.GetTilePosition(new Vector2(0f, lastPos.y - TerrainRad)).y;
                int xLeft = room.GetTilePosition(new Vector2(pos.x - TerrainRad + 1f, 0f)).x;
                int xRight = room.GetTilePosition(new Vector2(pos.x + TerrainRad - 1f, 0f)).x;
                bool hit = false;
                for (int i = yStart; i >= yEnd && !hit; i--)
                {
                    for (int j = xLeft; j <= xRight && !hit; j++)
                    {
                        if (SolidFloor(j, i) && !SolidFloor(j, i + 1)
                            && (tilePosition.y > i || room.GetTile(lastPos).Terrain == Room.Tile.TerrainType.Solid))
                        {
                            pos.y = (i + 1f) * 20f + TerrainRad;
                            if (vel.y < -owner.impactTreshhold)
                            {
                                owner.TerrainImpact(index, new IntVector2(0, -1), Mathf.Abs(vel.y), lastContactPoint.y > -1);
                            }
                            contactPoint.y = -1;
                            vel.y = Mathf.Abs(vel.y) * owner.bounce;
                            if (vel.y < owner.gravity || vel.y < 1f + 9f * (1f - owner.bounce))
                            {
                                vel.y = 0f;
                            }
                            vel.x *= Mathf.Clamp(owner.surfaceFriction * 2f, 0f, 1f);
                            hit = true;
                        }
                        if (++repeats > MaxRepeats) hit = true;
                    }
                }
            }
        }

        // BodyChunk.checkAgainstSlopesVertically: rests the chunk on (or under) the diagonal of a slope
        // tile it overlaps, and nudges it downhill on a floor slope.
        private void CheckAgainstSlopesVertically()
        {
            Room room = owner.room;
            IntVector2 tilePosition = room.GetTilePosition(pos);
            IntVector2 offset = new IntVector2(0, 0);
            Room.SlopeDirection slopeDirection = room.IdentifySlope(pos);
            Vector2 middle = room.MiddleOfTile(pos);
            if (room.GetTile(pos).Terrain != Room.Tile.TerrainType.Slope)
            {
                if (room.IdentifySlope(tilePosition.x - 1, tilePosition.y) != Room.SlopeDirection.Broken && pos.x - slopeRad <= middle.x - 10f)
                {
                    slopeDirection = room.IdentifySlope(tilePosition.x - 1, tilePosition.y);
                    offset.x = -1;
                }
                else if (room.IdentifySlope(tilePosition.x + 1, tilePosition.y) != Room.SlopeDirection.Broken && pos.x + slopeRad >= middle.x + 10f)
                {
                    slopeDirection = room.IdentifySlope(tilePosition.x + 1, tilePosition.y);
                    offset.x = 1;
                }
                else if (pos.y - slopeRad < middle.y - 10f)
                {
                    if (room.IdentifySlope(tilePosition.x, tilePosition.y - 1) != Room.SlopeDirection.Broken)
                    {
                        slopeDirection = room.IdentifySlope(tilePosition.x, tilePosition.y - 1);
                        offset.y = -1;
                    }
                }
                else if (pos.y + slopeRad > middle.y + 10f && room.IdentifySlope(tilePosition.x, tilePosition.y + 1) != Room.SlopeDirection.Broken)
                {
                    slopeDirection = room.IdentifySlope(tilePosition.x, tilePosition.y + 1);
                    offset.y = 1;
                }
            }
            if (slopeDirection == Room.SlopeDirection.Broken)
            {
                return;
            }

            Vector2 tileMiddle = room.MiddleOfTile(room.GetTilePosition(pos) + offset);
            int downhill = 0;
            float surfaceY;
            int side;
            if (slopeDirection == Room.SlopeDirection.UpLeft)
            {
                downhill = -1;
                surfaceY = pos.x - (tileMiddle.x - 10f) + (tileMiddle.y - 10f);
                side = -1;
            }
            else if (slopeDirection == Room.SlopeDirection.UpRight)
            {
                downhill = 1;
                surfaceY = 20f - (pos.x - (tileMiddle.x - 10f)) + (tileMiddle.y - 10f);
                side = -1;
            }
            else if (slopeDirection == Room.SlopeDirection.DownLeft)
            {
                surfaceY = 20f - (pos.x - (tileMiddle.x - 10f)) + (tileMiddle.y - 10f);
                side = 1;
            }
            else
            {
                surfaceY = pos.x - (tileMiddle.x - 10f) + (tileMiddle.y - 10f);
                side = 1;
            }

            // The game offsets by slopeRad twice here.
            if (side == -1 && pos.y <= surfaceY + slopeRad + slopeRad)
            {
                pos.y = surfaceY + slopeRad + slopeRad;
                contactPoint.y = -1;
                vel.x *= 1f - owner.surfaceFriction;
                vel.x += Mathf.Abs(vel.y) * Mathf.Clamp(0.5f - owner.surfaceFriction, 0f, 0.5f) * downhill * 0.2f;
                vel.y = 0f;
                onSlope = downhill;
                slopeRad = TerrainRad - 1f;
            }
            else if (side == 1 && pos.y >= surfaceY - slopeRad - slopeRad)
            {
                pos.y = surfaceY - slopeRad - slopeRad;
                contactPoint.y = 1;
                vel.y = 0f;
                vel.x *= 1f - owner.surfaceFriction;
                slopeRad = TerrainRad - 1f;
            }
        }

        // BodyChunk.SolidFloor: a one-way floor holds only while the whole object was above it last tick.
        private bool SolidFloor(int x, int y)
        {
            Room room = owner.room;
            Room.Tile.TerrainType terrain = room.GetTile(x, y).Terrain;
            if (terrain == Room.Tile.TerrainType.Solid)
            {
                return true;
            }
            if (terrain == Room.Tile.TerrainType.Floor && !goThroughFloors)
            {
                float lowest = room.PixelHeight;
                for (int i = 0; i < owner.bodyChunks.Length; i++)
                {
                    if (owner.bodyChunks[i].lastPos.y < lowest)
                    {
                        lowest = owner.bodyChunks[i].lastPos.y;
                    }
                }
                return room.GetTilePosition(new Vector2(0f, lowest)).y > y;
            }
            return false;
        }
    }
}
