namespace RWDE.Sim
{
    /// <summary>
    /// Builds and removes realized creatures for the <see cref="World"/>, standing in for the game's
    /// <c>AbstractCreature.Realize</c> and <c>InitiateAI</c>: the body class and AI come from the
    /// template's top ancestor. Species are added here as they are ported; so far the lizards whose
    /// breed parameters exist. Other species return false and stay offscreen.
    /// </summary>
    public sealed class CreatureRealizer : ICreatureRealizer
    {
        /// <summary>Whether <paramref name="template"/> has a ported body and AI.</summary>
        public static bool CanRealize(CreatureTemplate template) =>
            template.IsLizard && template.breedParameters is LizardBreedParams;

        public bool Realize(AbstractCreature creature, Room room, IntVector2 tile, IntVector2 inward)
        {
            if (!CanRealize(creature.creatureTemplate))
                return false;
            var body = new Lizard(creature, room.world);
            new LizardAI(creature, room.world);
            body.PlaceInRoom(room);
            // Point the body into the room, head first, as a creature coming in from the edge would be.
            for (int i = 0; i < body.bodyChunks.Length; i++)
            {
                var chunk = body.bodyChunks[i];
                chunk.HardSetPosition(room.MiddleOfTile(tile) - inward.ToVector2() * (i * 10f));
                chunk.vel = inward.ToVector2() * 2f;
            }
            return true;
        }

        public bool RealizePlaced(AbstractCreature creature, Room room, out IntVector2 tile)
        {
            // Every realizable species so far travels in through exits.
            tile = default;
            return false;
        }

        public void Abstractize(AbstractCreature creature)
        {
            var body = creature.realizedCreature;
            if (body == null)
                return;
            body.LoseAllGrasps();
            for (int i = body.grabbedBy.Count - 1; i >= 0; i--)
                body.grabbedBy[i].Release();
            body.room?.RemoveObject(body);
            creature.realizedCreature = null;
            creature.abstractAI.RealAI = null;
        }
    }
}
