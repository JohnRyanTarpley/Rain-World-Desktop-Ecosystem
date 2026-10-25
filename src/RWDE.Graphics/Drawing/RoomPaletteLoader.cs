using System;
using System.IO;
using RWDE.GameAssets;
using RWDE.GameAssets.Cache;

namespace RWDE.Graphics
{
    /// <summary>
    /// The palette half of the game's <c>RoomCamera</c>: loads <c>palettes/palette&lt;N&gt;.png</c>, writes the
    /// room's effect colours into it, fades between the main and fade palette and toward the rain-darkened
    /// lower half, and reads <see cref="currentPalette"/> off the result as the game does. Hand the result to
    /// <see cref="RoomCamera.ApplyPalette"/> whenever <see cref="PaletteChanged"/> fires.
    /// </summary>
    /// <remarks>
    /// Each 32x16 palette png holds the normal palette in its upper eight rows and the rain-darkened one in
    /// its lower eight. Not ported: the ghost, rot and mushroom fades, ripple palettes, terrain palettes and
    /// the ModifyEffectColor room effects (none are in the Outskirts).
    /// </remarks>
    public sealed class RoomPaletteLoader
    {
        public const int PaletteWidth = 32;
        public const int PaletteHeight = 16;

        private readonly Func<string, AssetResult<FileAsset>> _findPalette;
        private PaletteTexture _fadeTexA;
        private PaletteTexture _fadeTexB;
        private PaletteTexture _allEffectColorsTexture;
        private float _paletteBlend;
        private float _darkPalette;

        /// <param name="findPalette">Palette png by name, e.g. <see cref="GameAssetLibrary.GetPalette(string)"/>.</param>
        public RoomPaletteLoader(Func<string, AssetResult<FileAsset>> findPalette)
        {
            _findPalette = findPalette ?? throw new ArgumentNullException(nameof(findPalette));
        }

        public RoomPaletteLoader(GameAssetLibrary assets) : this(assets.GetPalette)
        {
        }

        /// <summary>The blended palette the shaders read (RoomCamera.paletteTexture, 32x8).</summary>
        public readonly PaletteTexture paletteTexture = new PaletteTexture(PaletteWidth, PaletteHeight / 2);

        public int paletteA = -1;
        public int paletteB = -1;
        /// <summary>The room's effect colours, indices into <c>effectColors.png</c>; -1 for none.</summary>
        public int effectColorA = -1;
        public int effectColorB = -1;

        public RoomPalette currentPalette;

        /// <summary>Raised after <see cref="currentPalette"/> changes.</summary>
        public event Action<RoomPalette> PaletteChanged;

        /// <summary>How far toward the fade palette, 0 to 1 (fadeCoord.x).</summary>
        public float paletteBlend
        {
            get => _paletteBlend;
            set
            {
                if (value == _paletteBlend) return;
                _paletteBlend = value;
                if (paletteA > -1) ApplyFade();
            }
        }

        /// <summary>
        /// How far toward each palette's rain-darkened half, 0 to 1 (fadeCoord.y): the rain cycle's
        /// <c>RainDarkPalette</c> times the room's rain intensity. The game re-applies the fade whenever it changes.
        /// </summary>
        public float darkPalette
        {
            get => _darkPalette;
            set
            {
                if (value == _darkPalette) return;
                _darkPalette = value;
                if (paletteA > -1) ApplyFade();
            }
        }

        public void ChangeMainPalette(int palA)
        {
            _fadeTexA = LoadPalette(palA);
            paletteA = palA;
            ApplyFade();
        }

        public void ChangeFadePalette(int palB, float blend)
        {
            bool changed = palB != paletteB;
            paletteB = palB;
            if (paletteB == -1) return;
            if (changed) _fadeTexB = LoadPalette(palB);
            _paletteBlend = blend;
            ApplyFade();
        }

        public void ChangeBothPalettes(int palA, int palB, float blend)
        {
            if (palA != paletteA) _fadeTexA = LoadPalette(palA);
            paletteA = palA;
            bool changed = palB != paletteB;
            paletteB = palB;
            if (paletteB != -1)
            {
                if (changed) _fadeTexB = LoadPalette(palB);
                _paletteBlend = blend;
            }
            ApplyFade();
        }

        /// <summary>Sets the room's effect colours and writes them into the loaded palettes (RoomCamera.ApplyEffectColorsToAllPaletteTextures).</summary>
        public void ApplyEffectColorsToAllPaletteTextures(int color1, int color2)
        {
            effectColorA = color1;
            effectColorB = color2;
            if (_fadeTexA == null) return;
            ApplyEffectColorsToPaletteTexture(_fadeTexA, color1, color2);
            if (paletteB > -1) ApplyEffectColorsToPaletteTexture(_fadeTexB, color1, color2);
            ApplyFade();
        }

        /// <summary>
        /// <c>palettes/palette&lt;pal&gt;.png</c> with the effect colours written in, or <c>palette-1</c> if
        /// that file is missing (RoomCamera.LoadPalette).
        /// </summary>
        /// <exception cref="FileNotFoundException">Neither palette is in the install.</exception>
        /// <exception cref="InvalidDataException">The palette isn't 32x16.</exception>
        public PaletteTexture LoadPalette(int pal)
        {
            var found = _findPalette("palette" + pal);
            if (!found.Found) found = _findPalette("palette-1");
            if (!found.TryGet(out var file))
                throw new FileNotFoundException($"No palette{pal} or palette-1: {found.Missing}");
            var texture = LoadImage(file);
            ApplyEffectColorsToPaletteTexture(texture, effectColorA, effectColorB);
            return texture;
        }

        private void ApplyEffectColorsToPaletteTexture(PaletteTexture texture, int color1, int color2)
        {
            if (texture.width != PaletteWidth || texture.height != PaletteHeight)
                throw new InvalidDataException($"A palette must be {PaletteWidth}x{PaletteHeight}, not {texture.width}x{texture.height}.");
            if (color1 > -1)
            {
                texture.SetPixels(30, 4, 2, 2, EffectColors().GetPixels(color1 * 2, 0, 2, 2));
                texture.SetPixels(30, 12, 2, 2, EffectColors().GetPixels(color1 * 2, 2, 2, 2));
            }
            if (color2 > -1)
            {
                texture.SetPixels(30, 2, 2, 2, EffectColors().GetPixels(color2 * 2, 0, 2, 2));
                texture.SetPixels(30, 10, 2, 2, EffectColors().GetPixels(color2 * 2, 2, 2, 2));
            }
        }

        // RoomCamera.allEffectColorsTexture, loaded on first use.
        private PaletteTexture EffectColors()
        {
            if (_allEffectColorsTexture != null) return _allEffectColorsTexture;
            var found = _findPalette("effectColors");
            if (!found.TryGet(out var file)) throw new FileNotFoundException("No effectColors palette: " + found.Missing);
            return _allEffectColorsTexture = LoadImage(file);
        }

        private void ApplyFade()
        {
            float x = _paletteBlend, y = _darkPalette;
            for (int i = 0; i < PaletteWidth; i++)
            for (int j = 8; j < 16; j++)
            {
                var a = Color.Lerp(_fadeTexA.GetPixel(i, j), _fadeTexA.GetPixel(i, j - 8), y);
                paletteTexture.SetPixel(i, j - 8, paletteB > -1
                    ? Color.Lerp(a, Color.Lerp(_fadeTexB.GetPixel(i, j), _fadeTexB.GetPixel(i, j - 8), y), x)
                    : a);
            }
            ApplyPalette();
        }

        private void ApplyPalette()
        {
            // RoomCamera.ApplyPalette: the settings row is the top one; water colours are left out.
            var t = paletteTexture;
            currentPalette = new RoomPalette
            {
                texture = t,
                fogAmount = 1f - t.GetPixel(9, 7).r,
                darkness = 1f - t.GetPixel(30, 7).r,
                blackColor = t.GetPixel(2, 7),
                fogColor = t.GetPixel(1, 7),
                skyColor = t.GetPixel(0, 7),
                shortcutColors = new[] { t.GetPixel(10, 7), t.GetPixel(11, 7), t.GetPixel(12, 7) },
                shortCutSymbol = t.GetPixel(13, 7),
            };
            PaletteChanged?.Invoke(currentPalette);
        }

        private static PaletteTexture LoadImage(FileAsset file)
        {
            var rgba = PngDecoder.DecodeFile(file.Path, out var width, out var height);
            return PaletteTexture.FromRgba(rgba, width, height);
        }
    }
}
