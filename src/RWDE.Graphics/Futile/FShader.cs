using System.Collections.Generic;

namespace RWDE.Graphics
{
    /// <summary>
    /// A shader by the game's name (the keys of <c>RainWorld.Shaders</c>, e.g. "Basic", "LizardSkin").
    /// The sprite layer only batches by it; the Unity renderer maps the name to a material.
    /// </summary>
    public sealed class FShader
    {
        private static readonly Dictionary<string, FShader> ByNameCache = new Dictionary<string, FShader>();

        private FShader(string name)
        {
            this.name = name;
        }

        public readonly string name;

        /// <summary>The game's default sprite shader (<c>FShader.defaultShader</c>, registered as "Basic").</summary>
        public static FShader defaultShader => ByName("Basic");

        /// <summary>The one shader instance for <paramref name="name"/>, so batches can compare by reference.</summary>
        public static FShader ByName(string name)
        {
            lock (ByNameCache)
            {
                if (!ByNameCache.TryGetValue(name, out var shader)) ByNameCache[name] = shader = new FShader(name);
                return shader;
            }
        }

        public override string ToString() => name;
    }
}
