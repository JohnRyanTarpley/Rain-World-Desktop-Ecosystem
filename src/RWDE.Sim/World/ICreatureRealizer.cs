namespace RWDE.Sim
{
    /// <summary>
    /// Creates and removes realized creatures. Species ports implement it; the <see cref="World"/>
    /// decides when and where.
    /// </summary>
    public interface ICreatureRealizer
    {
        /// <summary>
        /// Realizes <paramref name="creature"/> at <paramref name="tile"/> on a room edge, coming in along
        /// <paramref name="inward"/>. Returns false when the species can't be realized (not ported yet),
        /// which leaves it offscreen.
        /// </summary>
        bool Realize(AbstractCreature creature, Room room, IntVector2 tile, IntVector2 inward);

        /// <summary>
        /// Realizes a creature that never travels (a Pole Plant or Monster Kelp) somewhere in
        /// <paramref name="room"/> of the realizer's choosing. Returns false when there is no place for it.
        /// </summary>
        bool RealizePlaced(AbstractCreature creature, Room room);

        /// <summary>Removes the realized body; the creature is abstract again.</summary>
        void Abstractize(AbstractCreature creature);
    }
}
