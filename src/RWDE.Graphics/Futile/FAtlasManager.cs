using System;
using System.Collections.Generic;

namespace RWDE.Graphics
{
    /// <summary>
    /// Every loaded atlas, and element lookup by the game's element names, as Futile's <c>FAtlasManager</c>.
    /// Element names are global across atlases: <c>new FSprite("LizardHead0.0")</c> finds the element
    /// wherever it was packed.
    /// </summary>
    public sealed class FAtlasManager
    {
        /// <summary>True in Debug builds of RWDE.Graphics, where an unknown element name throws.</summary>
#if DEBUG
        public const bool DevelopmentBuild = true;
#else
        public const bool DevelopmentBuild = false;
#endif

        private readonly List<FAtlas> _atlases = new List<FAtlas>();
        private readonly Dictionary<string, FAtlasElement> _allElementsByName = new Dictionary<string, FAtlasElement>();
        private readonly HashSet<string> _reportedUnknownElements = new HashSet<string>();
        private int _nextAtlasIndex;

        public IReadOnlyList<FAtlas> atlases => _atlases;

        /// <summary>
        /// Whether <see cref="GetElementWithName"/> throws for an unknown name, as the game does. On in
        /// development builds. Release builds report each unknown name once through
        /// <see cref="UnknownElement"/> and draw "Futile_White" instead, so one bad name doesn't end the app.
        /// </summary>
        public bool throwOnUnknownElement = DevelopmentBuild;

        /// <summary>Raised once per unknown element name, with the error message, when <see cref="throwOnUnknownElement"/> is off.</summary>
        public event Action<string> UnknownElement;

        public IEnumerable<string> ElementNames => _allElementsByName.Keys;

        public FAtlas GetAtlasWithName(string name)
        {
            for (int i = 0; i < _atlases.Count; i++)
            {
                if (_atlases[i].name == name) return _atlases[i];
            }
            return null;
        }

        public bool DoesContainAtlas(string name) => GetAtlasWithName(name) != null;

        /// <summary>Loads a TexturePacker atlas; an atlas already loaded under <paramref name="name"/> is returned as is.</summary>
        public FAtlas LoadAtlasFromJson(string name, string json, object texture, int textureWidth, int textureHeight)
        {
            return GetAtlasWithName(name) ?? AddAtlas(FAtlas.FromTexturePackerJson(name, json, texture, textureWidth, textureHeight));
        }

        /// <summary>Loads a whole texture as one element named <paramref name="name"/> (FAtlasManager.LoadAtlasFromTexture).</summary>
        public FAtlas LoadAtlasFromTexture(string name, object texture, int textureWidth, int textureHeight)
        {
            return GetAtlasWithName(name) ?? AddAtlas(FAtlas.FromSingleImage(name, texture, textureWidth, textureHeight));
        }

        // FAtlasManager.ActuallyUnloadAtlasOrImage. The caller owns and destroys the texture.
        public void UnloadAtlas(string name)
        {
            for (int i = _atlases.Count - 1; i >= 0; i--)
            {
                if (_atlases[i].name != name) continue;
                foreach (var element in _atlases[i].elements) _allElementsByName.Remove(element.name);
                _atlases.RemoveAt(i);
            }
        }

        public bool DoesContainElementWithName(string elementName) => _allElementsByName.ContainsKey(elementName);

        /// <summary>The element called <paramref name="elementName"/> (FAtlasManager.GetElementWithName).</summary>
        /// <exception cref="FutileException">
        /// No such element while <see cref="throwOnUnknownElement"/> is on (or "Futile_White" isn't loaded);
        /// the message suggests a close name if there is one.
        /// </exception>
        public FAtlasElement GetElementWithName(string elementName)
        {
            if (elementName != null && _allElementsByName.TryGetValue(elementName, out var element)) return element;

            // As the game: suggest an element whose name contains the last path segment, minus any extension.
            string suggestion = null;
            if (elementName != null)
            {
                var parts = elementName.Split(elementName.Contains("\\") ? '\\' : '/');
                var stem = parts[parts.Length - 1].Split('.')[0];
                foreach (var name in _allElementsByName.Keys)
                {
                    if (name.Contains(stem)) suggestion = name;
                }
            }
            var message = suggestion == null
                ? "Couldn't find element named '" + elementName + "'."
                : "Couldn't find element named '" + elementName + "'. Did you mean '" + suggestion + "'?";
            if (throwOnUnknownElement || !_allElementsByName.TryGetValue(Futile.WhiteElementName, out var white))
                throw new FutileException(message);
            if (_reportedUnknownElements.Add(elementName ?? "")) UnknownElement?.Invoke(message);
            return white;
        }

        private FAtlas AddAtlas(FAtlas atlas)
        {
            // FAtlasManager.AddAtlas: element names must be unique across all atlases.
            foreach (var element in atlas.elements)
            {
                if (_allElementsByName.ContainsKey(element.name))
                    throw new FutileException("Duplicate element name '" + element.name + "' found! All element names must be unique!");
            }
            atlas.index = _nextAtlasIndex++;
            foreach (var element in atlas.elements)
            {
                element.atlas = atlas;
                element.atlasIndex = atlas.index;
                _allElementsByName.Add(element.name, element);
            }
            _atlases.Add(atlas);
            return atlas;
        }
    }
}
