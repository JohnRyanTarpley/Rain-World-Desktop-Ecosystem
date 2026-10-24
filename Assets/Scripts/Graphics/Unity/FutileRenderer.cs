using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UColor = UnityEngine.Color;
using UVector2 = UnityEngine.Vector2;

namespace RWDE.Graphics
{
    /// <summary>
    /// Puts the sprite layer on screen. Every frame (LateUpdate) it runs <see cref="RoomCamera.DrawUpdate"/>
    /// at the current <see cref="TimeStacker"/>, then turns each <see cref="FRenderBatch"/> into one Unity
    /// mesh drawn in order: one draw call per run of sprites sharing an atlas and shader, as Futile does.
    /// Stage pixels map to this transform's local space at <see cref="pixelsPerUnit"/>.
    /// </summary>
    public sealed class FutileRenderer : MonoBehaviour
    {
        /// <summary>Stage pixels per Unity unit.</summary>
        public float pixelsPerUnit = 1f;

        public string sortingLayerName = "Default";

        /// <summary>Sorting order of the first batch; later batches count up from it.</summary>
        public int baseSortingOrder;

        /// <summary>Used for the game's shader names until the game's shaders are ported. Defaults to "Sprites/Default".</summary>
        public Shader fallbackShader;

        private readonly List<MeshRenderer> _layers = new List<MeshRenderer>();
        private readonly Dictionary<(Texture, string), Material> _materials = new Dictionary<(Texture, string), Material>();
        private readonly HashSet<string> _warnedShaders = new HashSet<string>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<UVector2> _uvs = new List<UVector2>();
        private readonly List<UColor> _colors = new List<UColor>();
        private Texture2D _whiteTexture;

        /// <summary>The camera to draw; set by the app. Nothing is drawn while it is null.</summary>
        public RoomCamera RoomCamera { get; set; }

        /// <summary>Interpolation between the last two sim ticks (0..1), from the app's fixed-step clock.</summary>
        public Func<float> TimeStacker { get; set; }

        /// <summary>Unity shaders for the game's shader names (keys of <c>RainWorld.Shaders</c>).</summary>
        public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>(StringComparer.Ordinal);

        private void Awake()
        {
            if (fallbackShader == null) fallbackShader = Shader.Find("Sprites/Default");
            RegisterWhiteElement(Futile.atlasManager);
        }

        /// <summary>Registers the 16x16 white "Futile_White" element that meshes default to (Futile.CreateDefaultAtlases).</summary>
        public void RegisterWhiteElement(FAtlasManager atlasManager)
        {
            if (atlasManager.DoesContainElementWithName(Futile.WhiteElementName)) return;
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(16, 16, TextureFormat.ARGB32, false)
                {
                    name = Futile.WhiteElementName,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var pixels = new UColor[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = UColor.white;
                _whiteTexture.SetPixels(pixels);
                _whiteTexture.Apply();
            }
            atlasManager.LoadAtlasFromTexture(Futile.WhiteElementName, _whiteTexture, 16, 16);
        }

        private void LateUpdate()
        {
            if (RoomCamera == null) return;
            RoomCamera.DrawUpdate(TimeStacker != null ? Mathf.Clamp01(TimeStacker()) : 1f);
            Render(RoomCamera.stage);
        }

        /// <summary>Rebuilds the meshes from <paramref name="stage"/>'s sprite tree.</summary>
        public void Render(FStage stage)
        {
            var batches = stage.BuildBatches();
            float unitsPerPixel = 1f / pixelsPerUnit;
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                var layer = Layer(i);
                var mesh = layer.GetComponent<MeshFilter>().sharedMesh;
                mesh.Clear();

                _vertices.Clear();
                _uvs.Clear();
                _colors.Clear();
                for (int v = 0; v < batch.vertices.Count; v++)
                {
                    var p = batch.vertices[v];
                    var uv = batch.uvs[v];
                    var c = batch.colors[v];
                    _vertices.Add(new Vector3(p.x * unitsPerPixel, p.y * unitsPerPixel, 0f));
                    _uvs.Add(new UVector2(uv.x, uv.y));
                    _colors.Add(new UColor(c.r, c.g, c.b, c.a));
                }
                mesh.indexFormat = _vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uvs);
                mesh.SetColors(_colors);
                mesh.SetTriangles(batch.triangles, 0, false);
                mesh.RecalculateBounds();

                layer.sharedMaterial = GetMaterial(batch.atlas?.texture as Texture, batch.shader);
                layer.sortingLayerName = sortingLayerName;
                layer.sortingOrder = baseSortingOrder + i;
                layer.enabled = true;
            }
            for (int i = batches.Count; i < _layers.Count; i++) _layers[i].enabled = false;
        }

        private MeshRenderer Layer(int index)
        {
            while (_layers.Count <= index)
            {
                var go = new GameObject("FutileBatch" + _layers.Count);
                go.transform.SetParent(transform, false);
                var mesh = new Mesh { name = go.name };
                mesh.MarkDynamic();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = go.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                _layers.Add(meshRenderer);
            }
            return _layers[index];
        }

        private Material GetMaterial(Texture texture, FShader shader)
        {
            var key = (texture, shader.name);
            if (_materials.TryGetValue(key, out var material)) return material;

            if (!Shaders.TryGetValue(shader.name, out var unityShader) || unityShader == null)
            {
                unityShader = fallbackShader;
                if (shader != FShader.defaultShader && _warnedShaders.Add(shader.name))
                    Debug.LogWarning("RWDE.Graphics: no Unity shader for the game's '" + shader.name + "' shader; using " + unityShader.name);
            }
            material = new Material(unityShader) { name = shader.name + (texture != null ? " " + texture.name : "") };
            if (texture != null) material.mainTexture = texture;
            _materials.Add(key, material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (var layer in _layers)
            {
                if (layer != null) Destroy(layer.GetComponent<MeshFilter>().sharedMesh);
            }
            foreach (var material in _materials.Values) Destroy(material);
            _materials.Clear();
            if (_whiteTexture != null)
            {
                Futile.atlasManager.UnloadAtlas(Futile.WhiteElementName);
                Destroy(_whiteTexture);
            }
        }
    }
}
