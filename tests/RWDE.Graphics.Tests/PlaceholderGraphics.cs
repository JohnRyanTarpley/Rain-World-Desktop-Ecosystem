using System;
using RWDE.Sim;

namespace RWDE.Graphics.Tests
{
    /// <summary>
    /// Test-only placeholder shapes: a square per body chunk, sized to the chunk's diameter, and a bar along
    /// each body chunk connection, all drawn with <see cref="Futile.WhiteElementName"/>. It exists so tests can
    /// draw any sim object without a ported module. It lives in the test assembly on purpose: the app never
    /// falls back to it, because it requires a Rain World install (operator, 2026-10-24).
    /// </summary>
    public sealed class PlaceholderGraphics : GraphicsModule
    {
        /// <summary>Width of a connection bar, in pixels.</summary>
        public const float ConnectionWidth = 2f;

        public PlaceholderGraphics(PhysicalObject ow) : base(ow)
        {
        }

        /// <summary>Connection bars come first so the chunks draw on top of them.</summary>
        public int FirstConnectionSprite => 0;
        public int FirstChunkSprite => owner.bodyChunkConnections.Length;
        public int TotalSprites => owner.bodyChunkConnections.Length + owner.bodyChunks.Length;

        public int ChunkSprite(int chunk) => FirstChunkSprite + chunk;
        public int ConnectionSprite(int connection) => FirstConnectionSprite + connection;

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[TotalSprites];
            for (int i = 0; i < sLeaser.sprites.Length; i++) sLeaser.sprites[i] = new FSprite(Futile.WhiteElementName);
            AddToContainer(sLeaser, rCam, null);
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            for (int i = 0; i < owner.bodyChunks.Length; i++)
            {
                var chunk = owner.bodyChunks[i];
                var sprite = sLeaser.sprites[ChunkSprite(i)];
                sprite.SetPosition(Vector2.Lerp(chunk.lastPos, chunk.pos, timeStacker) - camPos);
                sprite.width = chunk.rad * 2f;
                sprite.height = chunk.rad * 2f;
            }

            for (int i = 0; i < owner.bodyChunkConnections.Length; i++)
            {
                var connection = owner.bodyChunkConnections[i];
                var a = Vector2.Lerp(connection.chunk1.lastPos, connection.chunk1.pos, timeStacker) - camPos;
                var b = Vector2.Lerp(connection.chunk2.lastPos, connection.chunk2.pos, timeStacker) - camPos;
                var sprite = sLeaser.sprites[ConnectionSprite(i)];
                sprite.SetPosition((a + b) * 0.5f);
                sprite.width = ConnectionWidth;
                sprite.height = Vector2.Distance(a, b);
                // Futile rotation is clockwise degrees from up, as the game's Custom.VecToDeg.
                sprite.rotation = (float)(Math.Atan2(b.x - a.x, b.y - a.y) * (180.0 / Math.PI));
                sprite.isVisible = connection.active;
            }

            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            foreach (var sprite in sLeaser.sprites) sprite.color = palette.blackColor;
        }
    }
}
