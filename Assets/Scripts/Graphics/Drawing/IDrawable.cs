using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>Something the <see cref="RoomCamera"/> draws, as the game's <c>IDrawable</c>.</summary>
    public interface IDrawable
    {
        /// <summary>Creates <c>sLeaser.sprites</c> (and containers), then usually calls <see cref="AddToContainer"/>.</summary>
        void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam);

        /// <summary>
        /// Positions the sprites for this frame. Interpolate sim positions with
        /// <c>Vector2.Lerp(lastPos, pos, timeStacker)</c> and subtract <paramref name="camPos"/>.
        /// </summary>
        void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos);

        /// <summary>Colours the sprites from the palette; called after <see cref="InitiateSprites"/> and on palette changes.</summary>
        void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette);

        /// <summary>Moves the sprites into <paramref name="newContatiner"/>, or the default layer when it is null.</summary>
        void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner);
    }
}
