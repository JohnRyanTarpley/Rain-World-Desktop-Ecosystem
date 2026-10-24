namespace RWDE.Sim.Physics
{
    /// <summary>Base of everything a room ticks, as the game's <c>UpdatableAndDeletable</c>.</summary>
    public class UpdatableAndDeletable
    {
        /// <summary>Flips every tick; deferred moves (<see cref="BodyChunk.setPos"/>) key off it.</summary>
        public bool evenUpdate;

        public IPhysicsRoom room;

        public bool slatedForDeletetion { get; set; }

        public virtual void Update(bool eu)
        {
            evenUpdate = eu;
        }

        public void RemoveFromRoom()
        {
            room = null;
        }

        public virtual void Destroy()
        {
            slatedForDeletetion = true;
        }
    }
}
