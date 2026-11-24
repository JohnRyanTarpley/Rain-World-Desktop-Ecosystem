using System.Collections.Generic;

namespace RWDE.Sim
{
    // The objects a room holds and ticks, and line of sight: ported from the game's Room.AddObject,
    // RemoveObject, Update (the update list and object-object collision) and VisualContact.
    public sealed partial class Room
    {
        /// <summary>Everything the room ticks, in the order added (<c>Room.updateList</c>).</summary>
        public readonly List<UpdatableAndDeletable> updateList = new List<UpdatableAndDeletable>();

        /// <summary>Physical objects by collision layer (<c>Room.physicalObjects</c>). Only objects on the same layer, 1 or 2, collide.</summary>
        public readonly List<PhysicalObject>[] physicalObjects = { new List<PhysicalObject>(), new List<PhysicalObject>(), new List<PhysicalObject>() };

        /// <summary>Flips every tick, standing in for <c>RainWorldGame.evenUpdate</c>.</summary>
        public bool evenUpdate;

        /// <summary>
        /// Adds <paramref name="obj"/> to the room (<c>Room.AddObject</c>). A creature also joins
        /// <see cref="AbstractRoom.creatures"/>, which the game keeps up through its abstract objects.
        /// </summary>
        public void AddObject(UpdatableAndDeletable obj)
        {
            if (updateList.Contains(obj))
                return;
            obj.room = this;
            obj.slatedForDeletetion = false;
            updateList.Add(obj);
            if (obj is PhysicalObject physical)
                physicalObjects[physical.collisionLayer].Add(physical);
            if (obj is Creature creature && !abstractRoom.creatures.Contains(creature.abstractCreature))
                abstractRoom.creatures.Add(creature.abstractCreature);
        }

        /// <summary>Takes <paramref name="obj"/> out of the room (<c>Room.RemoveObject</c>).</summary>
        public void RemoveObject(UpdatableAndDeletable obj)
        {
            if (obj.room == this)
                obj.RemoveFromRoom();
            updateList.Remove(obj);
            if (obj is PhysicalObject physical)
                physicalObjects[physical.collisionLayer].Remove(physical);
            if (obj is Creature creature)
                abstractRoom.creatures.Remove(creature.abstractCreature);
        }

        /// <summary>
        /// One tick of the room's objects, the object part of <c>Room.Update</c>: each object in the update
        /// list is updated (newest first, as the game iterates), objects that left or were destroyed are
        /// cleared out, then chunks of objects on the same collision layer push each other apart and each
        /// colliding pair is told once through <see cref="PhysicalObject.Collide"/>.
        /// </summary>
        public void Update()
        {
            evenUpdate = !evenUpdate;
            for (int i = updateList.Count - 1; i >= 0; i--)
            {
                if (i >= updateList.Count)
                    continue;
                var obj = updateList[i];
                if (obj.slatedForDeletetion || obj.room != this)
                {
                    CleanOutObjectNotInThisRoom(obj);
                    continue;
                }
                obj.Update(evenUpdate);
                if (obj.slatedForDeletetion || obj.room != this)
                    CleanOutObjectNotInThisRoom(obj);
            }

            for (int n = 1; n < physicalObjects.Length; n++)
            {
                var layer = physicalObjects[n];
                for (int a = 0; a < layer.Count; a++)
                    for (int b = a + 1; b < layer.Count; b++)
                        CollideObjects(layer[a], layer[b]);
            }
        }

        // Room.CleanOutObjectNotInThisRoom
        private void CleanOutObjectNotInThisRoom(UpdatableAndDeletable obj)
        {
            updateList.Remove(obj);
            if (obj is PhysicalObject physical)
            {
                physicalObjects[physical.collisionLayer].Remove(physical);
                if (obj is Creature creature && obj.room != this)
                    abstractRoom.creatures.Remove(creature.abstractCreature);
            }
            if (obj.slatedForDeletetion && obj is Creature dead)
                abstractRoom.creatures.Remove(dead.abstractCreature);
        }

        // The object-object collision in Room.Update. Objects one of which holds the other don't collide.
        private void CollideObjects(PhysicalObject a, PhysicalObject b)
        {
            if (!(Mathf.Abs(a.bodyChunks[0].pos.x - b.bodyChunks[0].pos.x) < a.collisionRange + b.collisionRange)
                || !(Mathf.Abs(a.bodyChunks[0].pos.y - b.bodyChunks[0].pos.y) < a.collisionRange + b.collisionRange))
                return;
            if (Holds(a, b) || Holds(b, a))
                return;
            bool told = false;
            for (int i = 0; i < a.bodyChunks.Length; i++)
            {
                for (int j = 0; j < b.bodyChunks.Length; j++)
                {
                    var ca = a.bodyChunks[i];
                    var cb = b.bodyChunks[j];
                    if (!ca.collideWithObjects || !cb.collideWithObjects || !Custom.DistLess(ca.pos, cb.pos, ca.rad + cb.rad))
                        continue;
                    float radii = ca.rad + cb.rad;
                    float dist = Vector2.Distance(ca.pos, cb.pos);
                    Vector2 dir = Custom.DirVec(ca.pos, cb.pos);
                    float share = cb.mass / (ca.mass + cb.mass);
                    Vector2 moveA = dir * ((radii - dist) * share);
                    Vector2 moveB = dir * ((radii - dist) * (1f - share));
                    ca.pos -= moveA;
                    ca.vel -= moveA;
                    cb.pos += moveB;
                    cb.vel += moveB;
                    if (ca.pos.x == cb.pos.x)
                    {
                        ca.vel += Custom.DegToVec(world.random.Value() * 360f) * 0.0001f;
                        cb.vel += Custom.DegToVec(world.random.Value() * 360f) * 0.0001f;
                    }
                    if (!told)
                    {
                        a.Collide(b, i, j);
                        b.Collide(a, j, i);
                    }
                    told = true;
                }
            }
        }

        private static bool Holds(PhysicalObject holder, PhysicalObject held)
        {
            if (!(holder is Creature creature) || creature.Template.grasps <= 0)
                return false;
            foreach (var grasp in creature.grasps)
                if (grasp != null && grasp.grabbed == held)
                    return true;
            return false;
        }

        // ---- Line of sight (Room.VisualContact, RayTraceTilesForTerrain) ----

        public bool VisualContact(WorldCoordinate a, WorldCoordinate b) =>
            a.room == abstractRoom.index && b.room == abstractRoom.index && VisualContact(MiddleOfTile(a.Tile), MiddleOfTile(b.Tile));

        public bool VisualContact(IntVector2 a, IntVector2 b) => VisualContact(MiddleOfTile(a), MiddleOfTile(b));

        /// <summary>Whether no solid tile lies on the line between the points, sampled every 20 px.</summary>
        public bool VisualContact(Vector2 a, Vector2 b)
        {
            if (GetTile(a).Solid || GetTile(b).Solid)
                return false;
            float dist = Vector2.Distance(a, b);
            for (float d = 20f; d < dist; d += 20f)
                if (GetTile(Vector2.Lerp(a, b, d / dist)).Solid)
                    return false;
            return true;
        }

        /// <summary>Whether a tile-by-tile line between the tiles crosses no solid tile.</summary>
        public bool RayTraceTilesForTerrain(int x0, int y0, int x1, int y1)
        {
            int dx = System.Math.Abs(x1 - x0);
            int dy = System.Math.Abs(y1 - y0);
            int x = x0;
            int y = y0;
            int n = 1 + dx + dy;
            int xInc = x1 > x0 ? 1 : -1;
            int yInc = y1 > y0 ? 1 : -1;
            int error = dx - dy;
            dx *= 2;
            dy *= 2;
            for (; n > 0; n--)
            {
                if (HasAnySolid(x, y))
                    return false;
                if (error > 0)
                {
                    x += xInc;
                    error -= dy;
                }
                else
                {
                    y += yInc;
                    error += dx;
                }
            }
            return true;
        }
    }

    public sealed partial class AbstractRoom
    {
        /// <summary>The creatures in this room (<c>AbstractRoom.creatures</c>). On the desktop every one is realized.</summary>
        public readonly List<AbstractCreature> creatures = new List<AbstractCreature>();

        /// <summary>
        /// How much <paramref name="creature"/> likes this room, 0-1 (<c>AbstractRoom.AttractionValueForCreature</c>).
        /// Desktop rooms have no room settings, so every creature finds them Neutral (0.5).
        /// </summary>
        public float AttractionValueForCreature(AbstractCreature creature) => 0.5f;

        /// <summary>The attraction scaled by room size (<c>AbstractRoom.SizeDependentAttractionValueForCreature</c>).</summary>
        public float SizeDependentAttractionValueForCreature(AbstractCreature creature)
        {
            int w = realizedRoom.TileWidth, h = realizedRoom.TileHeight;
            if (creature.creatureTemplate.canFly)
                return AttractionValueForCreature(creature) * Mathf.Lerp(w * h / 2000f, 1f, 0.985f);
            return AttractionValueForCreature(creature) * Mathf.Lerp(w * Mathf.Lerp(w, 1f, 0.5f) * h / 50000f, 1f, 0.985f);
        }
    }
}
