using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// The visual side of one sim object, as the game's <c>GraphicsModule</c>. Each species ports its game
    /// module onto this base (<c>Creatures/&lt;Species&gt;Graphics.cs</c>). It reads <see cref="owner"/> and
    /// never writes to it; visual-only state (IK targets, blink timers) lives here.
    /// </summary>
    /// <remarks>
    /// Not ported yet: body parts and <c>PushOutOf</c> (with the first module that has limbs), internal
    /// containers for held objects (with scavengers), culling (the whole desktop is on screen), debug labels,
    /// mud and ripple effects (not in the Outskirts).
    /// </remarks>
    public abstract class GraphicsModule : IDrawable
    {
        protected GraphicsModule(PhysicalObject ow)
        {
            owner = ow;
        }

        public PhysicalObject owner { get; private set; }

        /// <summary>Set to have the sprites removed on the next draw.</summary>
        public bool dispose;

        /// <summary>Once per sim tick, after the owner has moved (GraphicsModule.Update).</summary>
        public virtual void Update()
        {
        }

        /// <summary>Snaps visual state back to the owner, e.g. after a teleport (GraphicsModule.Reset).</summary>
        public virtual void Reset()
        {
        }

        public virtual void SuckedIntoShortCut(Vector2 shortCutPosition)
        {
        }

        public virtual void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
        }

        /// <summary>Removes the sprites once the owner is deleted, has left the camera's room, or <see cref="dispose"/> is set.</summary>
        public virtual void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            // GraphicsModule.DrawSprites
            if (owner.slatedForDeletetion || owner.room != rCam.room || dispose)
                sLeaser.CleanSpritesAndRemove();
        }

        public virtual void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
        }

        /// <summary>Moves every sprite and container into <paramref name="newContatiner"/>, by default "Midground".</summary>
        public virtual void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            // GraphicsModule.AddToContainer
            sLeaser.RemoveAllSpritesFromContainer();
            if (newContatiner == null) newContatiner = rCam.ReturnFContainer("Midground");
            foreach (var sprite in sLeaser.sprites) newContatiner.AddChild(sprite);
            if (sLeaser.containers != null)
            {
                foreach (var container in sLeaser.containers) newContatiner.AddChild(container);
            }
        }
    }
}
