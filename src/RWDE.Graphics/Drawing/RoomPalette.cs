namespace RWDE.Graphics
{
    /// <summary>
    /// The colours of the current palette that graphics modules read in <c>ApplyPalette</c>, as the game's
    /// <c>RoomPalette</c>. The water colours are left out; the desktop has no water.
    /// </summary>
    public struct RoomPalette
    {
        public Color blackColor;
        public Color fogColor;
        public Color skyColor;
        public Color shortCutSymbol;
        /// <summary>The three shortcut colours (RoomPalette.shortcutColors).</summary>
        public Color[] shortcutColors;
        public float fogAmount;
        public float darkness;
        /// <summary>The palette image as an engine texture, or null.</summary>
        public object texture;
    }
}
