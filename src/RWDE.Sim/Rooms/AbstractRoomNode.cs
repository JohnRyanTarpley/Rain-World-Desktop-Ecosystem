namespace RWDE.Sim
{
    /// <summary>
    /// The game's <c>AbstractRoomNode</c>. Only its <see cref="Type"/> exists so far, because creature
    /// templates record which node types each creature maps; the abstract world adds the rest.
    /// </summary>
    public sealed partial class AbstractRoomNode
    {
        // AbstractRoomNode.Type, in the game's registration order.
        public enum Type
        {
            Exit,
            Den,
            RegionTransportation,
            SideExit,
            SkyExit,
            SeaExit,
            BatHive,
            GarbageHoles
        }
    }
}
