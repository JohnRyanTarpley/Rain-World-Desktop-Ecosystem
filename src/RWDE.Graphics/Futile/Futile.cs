using System;

namespace RWDE.Graphics
{
    /// <summary>
    /// The globals ported graphics code reaches for, as in Futile's <c>Futile</c> class:
    /// <c>new FSprite("Circle20")</c> resolves its element through <see cref="atlasManager"/>.
    /// </summary>
    public static class Futile
    {
        /// <summary>Name of the plain white element, registered by the renderer at startup (<c>Futile.CreateDefaultAtlases</c>).</summary>
        public const string WhiteElementName = "Futile_White";

        // Futile.white
        public static readonly Color white = new Color(1f, 1f, 1f, 1f);

        /// <summary>Every loaded atlas and element. Replaced wholesale by tests.</summary>
        public static FAtlasManager atlasManager = new FAtlasManager();
    }

    /// <summary>A Futile usage error, such as an unknown element name (Futile's <c>FutileException</c>).</summary>
    public sealed class FutileException : Exception
    {
        public FutileException(string message) : base(message)
        {
        }
    }
}
