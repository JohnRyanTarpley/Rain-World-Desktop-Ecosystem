namespace RWDE.Graphics.Tests
{
    /// <summary>An invented 64x32 atlas (no game content) and a fresh <see cref="Futile.atlasManager"/> per test.</summary>
    public static class TestAtlases
    {
        public const string Json = @"{""frames"": {
""square.png"": {
    ""frame"": {""x"":0,""y"":0,""w"":32,""h"":32},
    ""rotated"": false, ""trimmed"": false,
    ""spriteSourceSize"": {""x"":0,""y"":0,""w"":32,""h"":32},
    ""sourceSize"": {""w"":32,""h"":32}
},
""dot.png"": {
    ""frame"": {""x"":32,""y"":0,""w"":8,""h"":8},
    ""rotated"": false, ""trimmed"": true,
    ""spriteSourceSize"": {""x"":1,""y"":3,""w"":8,""h"":8},
    ""sourceSize"": {""w"":12,""h"":12}
},
""Lizard.Head0.5"": {
    ""frame"": {""x"":40,""y"":16,""w"":16,""h"":16},
    ""rotated"": false, ""trimmed"": false,
    ""spriteSourceSize"": {""x"":0,""y"":0,""w"":16,""h"":16},
    ""sourceSize"": {""w"":16,""h"":16}
}},
""meta"": {""image"": ""testAtlas.png"", ""size"": {""w"":64,""h"":32}, ""scale"": ""1""}
}";

        public const string OtherJson = @"{""frames"": {
""other.png"": {
    ""frame"": {""x"":0,""y"":0,""w"":4,""h"":4},
    ""rotated"": false, ""trimmed"": false,
    ""spriteSourceSize"": {""x"":0,""y"":0,""w"":4,""h"":4},
    ""sourceSize"": {""w"":4,""h"":4}
}}}";

        /// <summary>Replaces <see cref="Futile.atlasManager"/> with one holding "testAtlas", "otherAtlas" and "Futile_White".</summary>
        public static FAtlasManager Install()
        {
            var manager = new FAtlasManager();
            manager.LoadAtlasFromJson("testAtlas", Json, null, 64, 32);
            manager.LoadAtlasFromJson("otherAtlas", OtherJson, null, 4, 4);
            manager.LoadAtlasFromTexture(Futile.WhiteElementName, null, 16, 16);
            Futile.atlasManager = manager;
            return manager;
        }
    }
}
