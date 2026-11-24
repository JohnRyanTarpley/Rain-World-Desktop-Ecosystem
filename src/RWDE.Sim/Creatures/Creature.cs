using System;
using System.Collections.Generic;
using DamageType = RWDE.Sim.CreatureTemplate.DamageType;

namespace RWDE.Sim
{
    /// <summary>
    /// Hands a creature that crawled into a shortcut entrance to the shortcut system, standing in for the
    /// game's <c>ShortcutHandler.SuckInCreature</c>. The shortcut (pipe) travel port implements it.
    /// </summary>
    public interface IShortcutHandler
    {
        void SuckInCreature(Creature creature, Room room, ShortcutData shortcut);
    }

    /// <summary>
    /// A realized creature: a <see cref="PhysicalObject"/> with an abstract creature, stun, death, damage
    /// and grasps. Ported from the game's <c>Creature</c>.
    /// </summary>
    /// <remarks>
    /// Not ported, as the desktop lacks what they act on: safari control (player input), water, lava and
    /// brine, mud and poison, hypothermia, ripple layers, kill tags for scoring, sounds, graphics modules
    /// and shortcut blinking (<c>NewTile</c>). Creatures entering a shortcut are handed to
    /// <see cref="World.shortcuts"/>; without one they stay in the room.
    /// </remarks>
    public abstract class Creature : PhysicalObject
    {
        /// <summary>One hold a creature has on an object (<c>Creature.Grasp</c>).</summary>
        public class Grasp
        {
            // Creature.Grasp.Shareability
            public enum Shareability
            {
                NonExclusive,
                CanOnlyShareWithNonExclusive,
                CanNotShare
            }

            public Grasp(Creature grabber, PhysicalObject grabbed, int graspUsed, int chunkGrabbed, Shareability shareability, float dominance, bool pacifying)
            {
                this.grabber = grabber;
                this.grabbed = grabbed;
                this.graspUsed = graspUsed;
                this.chunkGrabbed = chunkGrabbed;
                this.shareability = shareability;
                this.dominance = dominance;
                this.pacifying = pacifying;
            }

            public Creature grabber;
            public PhysicalObject grabbed;
            public int graspUsed;
            public int chunkGrabbed;
            public bool discontinued;
            public float dominance;
            public bool pacifying;
            public Shareability shareability;

            public BodyChunk grabbedChunk => grabbed.bodyChunks[chunkGrabbed];

            public void Release()
            {
                discontinued = true;
                grabbed.grabbedBy.Remove(this);
                grabber.grasps[graspUsed] = null;
            }

            public bool ShareabilityConflict(Shareability other)
            {
                if (shareability == Shareability.CanNotShare || other == Shareability.CanNotShare)
                    return true;
                if (shareability == Shareability.CanOnlyShareWithNonExclusive && other != Shareability.NonExclusive)
                    return true;
                if (other == Shareability.CanOnlyShareWithNonExclusive && shareability != Shareability.NonExclusive)
                    return true;
                return false;
            }
        }

        protected Creature(AbstractCreature abstractCreature, World world)
        {
            this.abstractCreature = abstractCreature;
            abstractCreature.realizedCreature = this;
            dead = State.dead;
            stun = 0;
            enteringShortCut = null;
            shortcutDelay = 0;
            mainBodyChunkIndex = 0;
            if (Template.grasps > 0)
                grasps = new Grasp[Template.grasps];
            lastCoord = abstractCreature.pos;
        }

        public readonly AbstractCreature abstractCreature;

        public int blind;
        protected int deaf;
        public int newToRoomInvinsibility;
        public int shortcutDelay;
        /// <summary>The entrance the creature is crawling into, if any (<c>Creature.enteringShortCut</c>).</summary>
        public IntVector2? enteringShortCut;
        private float shortCutRadAdd;
        public WorldCoordinate NPCTransportationDestination = new WorldCoordinate(-1, -1, -1, -1);
        public WorldCoordinate lastCoord;
        public float rainDeath;
        public DamageType stunDamageType = DamageType.None;

        public int stun { get; set; }
        public bool Stunned => stun >= 10;
        public bool Blinded => blind > 0;
        public int mainBodyChunkIndex { get; set; }
        public BodyChunk mainBodyChunk => bodyChunks[mainBodyChunkIndex];
        public bool dead { get; protected set; }
        public bool Consious => !Stunned && !dead;
        public virtual Vector2 VisionPoint => mainBodyChunk.pos;
        public bool inShortcut { get; set; }
        public Grasp[] grasps { get; protected set; }
        public virtual Vector2 DangerPos => mainBodyChunk.pos;
        public CreatureTemplate Template => abstractCreature.creatureTemplate;
        public CreatureState State => abstractCreature.state;
        public WorldCoordinate coord => abstractCreature.pos;

        /// <summary>The game's <c>safariControlled</c>: a player steering the creature. Never on the desktop.</summary>
        public bool safariControlled => false;

        /// <summary>How submerged the body is. The desktop has no water.</summary>
        public float Submersion => 0f;

        /// <summary>The world's seeded randomness, which ported code draws from in place of <c>UnityEngine.Random</c>.</summary>
        protected System.Random Random => abstractCreature.world.random;

        protected override void ChunksMoved()
        {
            var tile = room.GetTilePosition(bodyChunks[0].pos);
            abstractCreature.pos = new WorldCoordinate(room.abstractRoom.index, tile.x, tile.y, abstractCreature.pos.abstractNode);
        }

        /// <summary>Puts the creature in <paramref name="placeRoom"/> at its abstract tile (<c>Creature.PlaceInRoom</c>).</summary>
        public override void PlaceInRoom(Room placeRoom)
        {
            placeRoom.AddObject(this);
            foreach (var chunk in bodyChunks)
            {
                chunk.pos = placeRoom.MiddleOfTile(abstractCreature.pos.Tile) + Custom.DegToVec(Random.Value() * 360f);
                chunk.lastPos = chunk.pos;
                chunk.lastLastPos = chunk.pos;
                chunk.setPos = null;
                chunk.vel *= 0f;
            }
            if (grasps != null)
            {
                foreach (var grasp in grasps)
                {
                    if (grasp == null)
                        continue;
                    placeRoom.AddObject(grasp.grabbed);
                    foreach (var chunk in grasp.grabbed.bodyChunks)
                    {
                        chunk.pos = mainBodyChunk.pos;
                        chunk.lastPos = mainBodyChunk.pos;
                        chunk.lastLastPos = mainBodyChunk.pos;
                        chunk.setPos = null;
                        chunk.vel *= 0f;
                    }
                }
            }
            NewRoom(placeRoom);
        }

        public override void NewRoom(Room newRoom)
        {
            if (abstractCreature.world != newRoom.world)
                abstractCreature.world = newRoom.world;
            abstractCreature.abstractAI?.RealAI?.NewRoom(newRoom);
            newToRoomInvinsibility = 40;
        }

        public override void Update(bool eu)
        {
            lastCoord = abstractCreature.pos;
            if (newToRoomInvinsibility > 0)
                newToRoomInvinsibility--;
            if (stun > 0)
                stun--;
            if (stun < 10)
            {
                for (int i = 0; i < grabbedBy.Count; i++)
                {
                    if (grabbedBy[i].pacifying)
                    {
                        stun = 10;
                        break;
                    }
                }
            }
            if (shortcutDelay > 0)
                shortcutDelay--;
            if (blind > 0)
                blind--;
            if (deaf > 0)
                deaf--;
            rainDeath = Mathf.Clamp(rainDeath - 0.0125f, 0f, 1f);

            if (!dead && State is HealthState health && health.health < 0f && Random.Value() < -health.health && Random.Value() < 0.025f)
                Die();

            // Fell out of the bottom of the room.
            float floor = -bodyChunks[0].restrictInRoomRange + 1f;
            if (bodyChunks[0].pos.y < floor + 10f && !Template.canFly && grabbedBy.Count > 0)
            {
                while (grabbedBy.Count > 0)
                    grabbedBy[0].Release();
            }
            if (bodyChunks[0].pos.y < floor && (!Template.canFly || Stunned || dead))
            {
                Die();
                Destroy();
                return;
            }

            if (enteringShortCut.HasValue)
            {
                shortCutRadAdd += 0.2f;
                Vector2 hole = room.MiddleOfTile(enteringShortCut.Value) + Custom.IntVector2ToVector2(room.ShorcutEntranceHoleDirection(enteringShortCut.Value)) * -5f;
                int outside = 0;
                foreach (var obj in AllConnectedObjects())
                {
                    foreach (var chunk in obj.bodyChunks)
                    {
                        chunk.vel *= 0.06f;
                        if (!Custom.DistLess(chunk.pos, hole, Mathf.Max(10f, shortCutRadAdd)))
                        {
                            chunk.lastPos = chunk.pos;
                            chunk.pos += Custom.DirVec(chunk.pos, hole) * 4.5f;
                            if (obj == this)
                                outside++;
                        }
                        else
                        {
                            chunk.pos = hole;
                            chunk.lastPos = hole;
                        }
                    }
                }
                if (outside == 0)
                    SuckedIntoShortCut(enteringShortCut.Value);
                if (Stunned)
                    enteringShortCut = null;
            }
            else
            {
                shortCutRadAdd = 0.9f;
                base.Update(eu);
            }
        }

        /// <summary>This creature and what it holds (the realized side of <c>GetAllConnectedObjects</c>).</summary>
        public List<PhysicalObject> AllConnectedObjects()
        {
            var list = new List<PhysicalObject> { this };
            if (grasps != null)
                foreach (var grasp in grasps)
                    if (grasp != null && !list.Contains(grasp.grabbed))
                        list.Add(grasp.grabbed);
            return list;
        }

        // Creature.SuckedIntoShortCut, for a creature that entered on its own.
        private void SuckedIntoShortCut(IntVector2 entrancePos)
        {
            if (room == null || room.GetTile(entrancePos).Terrain != Room.Tile.TerrainType.ShortcutEntrance)
            {
                enteringShortCut = null;
                return;
            }
            var handler = room.world?.shortcuts;
            if (handler == null)
            {
                // No shortcut system yet: the creature stays put in the room.
                enteringShortCut = null;
                return;
            }
            var shortcut = room.shortcutData(entrancePos);
            if (shortcut.shortCutType == ShortcutData.Type.Normal)
            {
                var dest = shortcut.DestTile;
                abstractCreature.pos = new WorldCoordinate(abstractCreature.pos.room, dest.x, dest.y, abstractCreature.pos.abstractNode);
            }
            handler.SuckInCreature(this, room, shortcut);
            enteringShortCut = null;
            var from = room;
            foreach (var obj in AllConnectedObjects())
            {
                if (obj is Creature creature)
                    creature.inShortcut = true;
                from.RemoveObject(obj);
            }
        }

        /// <summary>Comes out of a shortcut at <paramref name="pos"/> in <paramref name="newRoom"/> (<c>Creature.SpitOutOfShortCut</c>).</summary>
        public virtual void SpitOutOfShortCut(IntVector2 pos, Room newRoom, bool spitOutAllSticks)
        {
            enteringShortCut = null;
            shortcutDelay = 20;
            inShortcut = false;
            if (!spitOutAllSticks)
                return;
            foreach (var obj in AllConnectedObjects())
            {
                foreach (var chunk in obj.bodyChunks)
                {
                    chunk.HardSetPosition(newRoom.MiddleOfTile(pos) + Custom.RNV(Random));
                    chunk.vel *= 0f;
                }
                var old = obj.room;
                newRoom.AddObject(obj);
                if (obj is Creature creature && creature != this)
                    creature.SpitOutOfShortCut(pos, newRoom, false);
                if (newRoom != old)
                    obj.NewRoom(newRoom);
            }
        }

        /// <summary>
        /// Damage and stun from a hit (<c>Creature.Violence</c>). <paramref name="directionAndMomentum"/>
        /// pushes the hit chunk; damage is divided by the template's resistances.
        /// </summary>
        public virtual void Violence(BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, DamageType type, float damage, float stunBonus)
        {
            if (directionAndMomentum.HasValue && hitChunk != null)
                hitChunk.vel += Vector2.ClampMagnitude(directionAndMomentum.Value / hitChunk.mass, 10f);
            float dmg = damage / Template.baseDamageResistance;
            float stn = (damage * 30f + stunBonus) / Template.baseStunResistance;
            if (State is HealthState healthState)
                stn *= 1.5f + Mathf.InverseLerp(0.5f, 0f, healthState.health) * Random.Value();
            if (Template.damageRestistances[(int)type, 0] > 0f)
                dmg /= Template.damageRestistances[(int)type, 0];
            if (Template.damageRestistances[(int)type, 1] > 0f)
                stn /= Template.damageRestistances[(int)type, 1];
            stunDamageType = type;
            Stun((int)stn);
            stunDamageType = DamageType.None;
            if (State is HealthState health)
            {
                health.health -= dmg;
                if (Template.quickDeath && (Random.Value() < -health.health || health.health < -1f || (health.health < 0f && Random.Value() < 0.33f)))
                    Die();
            }
            if (dmg >= Template.instantDeathDamageLimit)
                Die();
        }

        public virtual void Stun(int st)
        {
            stun = Math.Max(stun, st);
        }

        public virtual void Blind(int blnd)
        {
            blind = Math.Max(blind, blnd);
        }

        public virtual void Die()
        {
            dead = true;
            LoseAllGrasps();
            abstractCreature.Die();
        }

        public virtual void LoseAllGrasps()
        {
            if (Template.grasps > 0)
                for (int i = 0; i < grasps.Length; i++)
                    ReleaseGrasp(i);
        }

        public virtual void ReleaseGrasp(int grasp)
        {
            grasps[grasp]?.Release();
        }

        public virtual bool CanBeGrabbed(Creature grabber) => true;

        /// <summary>
        /// Takes hold of <paramref name="obj"/> by chunk <paramref name="chunkGrabbed"/> with grasp
        /// <paramref name="graspUsed"/> (<c>Creature.Grab</c>). Fails when a more dominant conflicting grasp
        /// already holds it; weaker conflicting grasps are broken.
        /// </summary>
        public virtual bool Grab(PhysicalObject obj, int graspUsed, int chunkGrabbed, Grasp.Shareability shareability, float dominance, bool overrideEquallyDominant, bool pacifying)
        {
            if (grasps == null || graspUsed < 0 || graspUsed > grasps.Length)
                return false;
            if (obj.slatedForDeletetion || (obj is Creature creature && !creature.CanBeGrabbed(this)))
                return false;
            if (grasps[graspUsed] != null && grasps[graspUsed].grabbed == obj)
            {
                ReleaseGrasp(graspUsed);
                grasps[graspUsed] = new Grasp(this, obj, graspUsed, chunkGrabbed, shareability, dominance, true);
                obj.Grabbed(grasps[graspUsed]);
                return true;
            }
            foreach (var other in obj.grabbedBy)
                if (other.grabber == this || (other.ShareabilityConflict(shareability) && ((overrideEquallyDominant && other.dominance == dominance) || other.dominance > dominance)))
                    return false;
            for (int i = obj.grabbedBy.Count - 1; i >= 0; i--)
                if (obj.grabbedBy[i].ShareabilityConflict(shareability))
                    obj.grabbedBy[i].Release();
            if (grasps[graspUsed] != null)
                ReleaseGrasp(graspUsed);
            grasps[graspUsed] = new Grasp(this, obj, graspUsed, chunkGrabbed, shareability, dominance, pacifying);
            obj.Grabbed(grasps[graspUsed]);
            return true;
        }

        public override string ToString() => Template.name + " " + abstractCreature.ID;
    }

    /// <summary>
    /// A creature that breathes air (<c>AirBreatherCreature</c>). Its lungs empty under water; the desktop
    /// has no water, so they stay full.
    /// </summary>
    public abstract class AirBreatherCreature : Creature
    {
        protected AirBreatherCreature(AbstractCreature abstractCreature, World world) : base(abstractCreature, world)
        {
        }

        public float lungs = 1f;

        public override void Update(bool eu)
        {
            base.Update(eu);
            if (dead || lungs == 1f)
                return;
            lungs = Mathf.Clamp01(lungs + 1f / 30f);
        }
    }
}
