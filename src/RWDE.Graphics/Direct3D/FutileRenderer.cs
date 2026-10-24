using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using RWDE.GameAssets;
using RWDE.GameAssets.Cache;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DirectComposition;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace RWDE.Graphics.Direct3D
{
    /// <summary>
    /// Puts the sprite layer on screen with Direct3D 11. Each <see cref="FRenderBatch"/> becomes one
    /// indexed draw, in order, as Futile's render layers. The swap chain is a composition swap chain
    /// with premultiplied alpha, shown through DirectComposition, so everything not drawn shows the
    /// desktop behind the window.
    /// </summary>
    /// <remarks>
    /// Stage pixels map 1:1 to window pixels with (0, 0) at the bottom-left and y up, as in the game.
    /// Every game shader name currently draws with the default sprite shader; names without a port yet
    /// are collected in <see cref="UnportedShaders"/>.
    /// </remarks>
    public sealed class FutileRenderer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Vertex
        {
            public float X, Y, U, V, R, G, B, A;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenConstants
        {
            public float InvHalfWidth, InvHalfHeight, Pad0, Pad1;
        }

        // Premultiplied output: DirectComposition blends the swap chain as premultiplied BGRA.
        private const string ShaderSource = @"
cbuffer Screen : register(b0) { float2 invHalfSize; float2 pad; };
struct VSIn { float2 pos : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR0; };
struct PSIn { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR0; };
PSIn VS(VSIn i)
{
    PSIn o;
    o.pos = float4(i.pos * invHalfSize - 1.0, 0.0, 1.0);
    o.uv = float2(i.uv.x, 1.0 - i.uv.y); // Futile's v points up; textures are stored top row first
    o.color = i.color;
    return o;
}
Texture2D tex : register(t0);
SamplerState samp : register(s0);
float4 PS(PSIn i) : SV_Target
{
    float4 c = tex.Sample(samp, i.uv) * i.color;
    return float4(c.rgb * c.a, c.a);
}";

        private readonly ID3D11Device _device;
        private readonly ID3D11DeviceContext _context;
        private readonly IDXGIDevice _dxgiDevice;
        private readonly IDXGISwapChain1 _swapChain;
        private readonly IDCompositionDevice _composition;
        private IDCompositionTarget _target;
        private IDCompositionVisual _visual;
        private ID3D11RenderTargetView _renderTarget;

        private readonly ID3D11VertexShader _vertexShader;
        private readonly ID3D11PixelShader _pixelShader;
        private readonly ID3D11InputLayout _inputLayout;
        private readonly ID3D11BlendState _blend;
        private readonly ID3D11SamplerState _sampler;
        private readonly ID3D11Buffer _constants;
        private ID3D11Buffer _vertexBuffer;
        private ID3D11Buffer _indexBuffer;
        private int _vertexCapacity;
        private int _indexCapacity;

        private Vertex[] _vertices = new Vertex[1024];
        private int[] _indices = new int[3072];
        private readonly List<D3DTexture> _textures = new List<D3DTexture>();
        private D3DTexture _white;

        /// <summary>Creates the device and swap chain and shows them in <paramref name="hwnd"/>.</summary>
        public FutileRenderer(IntPtr hwnd, int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);

            D3D11.D3D11CreateDevice(IntPtr.Zero, DriverType.Hardware, DeviceCreationFlags.BgraSupport,
                new[] { FeatureLevel.Level_11_0, FeatureLevel.Level_10_1, FeatureLevel.Level_10_0 },
                out _device, out _context).CheckError();

            _dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using (var adapter = _dxgiDevice.GetAdapter())
            using (var factory = adapter.GetParent<IDXGIFactory2>())
            {
                var description = new SwapChainDescription1((uint)Width, (uint)Height, Format.B8G8R8A8_UNorm, false,
                    Usage.RenderTargetOutput, 2, Scaling.Stretch, SwapEffect.FlipSequential, AlphaMode.Premultiplied,
                    SwapChainFlags.None);
                _swapChain = factory.CreateSwapChainForComposition(_device, description, null);
            }
            CreateRenderTarget();

            _composition = DComp.DCompositionCreateDevice<IDCompositionDevice>(_dxgiDevice);
            AttachToWindow(hwnd);

            var vs = Compile("VS", "vs_4_0");
            var ps = Compile("PS", "ps_4_0");
            _vertexShader = _device.CreateVertexShader(vs);
            _pixelShader = _device.CreatePixelShader(ps);
            _inputLayout = _device.CreateInputLayout(new[]
            {
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 8, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 16, 0),
            }, vs);
            _blend = _device.CreateBlendState(new BlendDescription(Blend.One, Blend.InverseSourceAlpha));
            _sampler = _device.CreateSamplerState(SamplerDescription.LinearClamp);
            _constants = _device.CreateBuffer((uint)Marshal.SizeOf<ScreenConstants>(), BindFlags.ConstantBuffer,
                ResourceUsage.Dynamic, CpuAccessFlags.Write);
        }

        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>Game shader names drawn so far that have no port yet; they draw as "Basic".</summary>
        public HashSet<string> UnportedShaders { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Shows the swap chain in <paramref name="hwnd"/>. Call it again after the window changes parent,
        /// since a composition target belongs to one window placement.
        /// </summary>
        public void AttachToWindow(IntPtr hwnd)
        {
            _visual?.Dispose();
            _target?.Dispose();
            _composition.CreateTargetForHwnd(hwnd, true, out _target).CheckError();
            _visual = _composition.CreateVisual();
            _visual.SetContent(_swapChain);
            _target.SetRoot(_visual);
            _composition.Commit();
        }

        /// <summary>Registers the 16x16 white "Futile_White" element that meshes default to (Futile.CreateDefaultAtlases).</summary>
        public void RegisterWhiteElement(FAtlasManager atlasManager)
        {
            if (atlasManager.DoesContainElementWithName(Futile.WhiteElementName)) return;
            if (_white == null)
            {
                var pixels = new byte[16 * 16 * 4];
                Array.Fill(pixels, (byte)255);
                _white = CreateTexture(Futile.WhiteElementName, 16, 16, pixels);
            }
            atlasManager.LoadAtlasFromTexture(Futile.WhiteElementName, _white, 16, 16);
        }

        /// <summary>Uploads a game atlas (PNG plus TexturePacker JSON) and registers its elements.</summary>
        public FAtlas LoadAtlas(FAtlasManager atlasManager, AtlasAsset asset)
        {
            var existing = atlasManager.GetAtlasWithName(asset.Name);
            if (existing != null) return existing;
            var rgba = PngDecoder.DecodeFile(asset.ImagePath, out var width, out var height);
            var texture = CreateTexture(asset.Name, width, height, rgba);
            return atlasManager.LoadAtlasFromJson(asset.Name, File.ReadAllText(asset.DataPath), texture, width, height);
        }

        public D3DTexture CreateTexture(string name, int width, int height, byte[] rgba)
        {
            var texture = new D3DTexture(_device, name, width, height, rgba);
            _textures.Add(texture);
            return texture;
        }

        /// <summary>Matches the swap chain to a new window size.</summary>
        public void Resize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            if (width == Width && height == Height) return;
            Width = width;
            Height = height;
            _context.OMSetRenderTargets((ID3D11RenderTargetView)null);
            _renderTarget.Dispose();
            _swapChain.ResizeBuffers(2, (uint)width, (uint)height, Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
            CreateRenderTarget();
        }

        /// <summary>
        /// Draws <paramref name="stage"/>'s sprite tree and presents it. Blocks until the next vertical
        /// blank when <paramref name="vsync"/> is set, which paces the caller's loop.
        /// </summary>
        public void Render(FStage stage, bool vsync = true)
        {
            var batches = stage.BuildBatches();

            int vertexCount = 0, indexCount = 0;
            foreach (var batch in batches)
            {
                vertexCount += batch.vertices.Count;
                indexCount += batch.triangles.Count;
            }
            if (_vertices.Length < vertexCount) Array.Resize(ref _vertices, Math.Max(vertexCount, _vertices.Length * 2));
            if (_indices.Length < indexCount) Array.Resize(ref _indices, Math.Max(indexCount, _indices.Length * 2));

            int v = 0, i = 0;
            foreach (var batch in batches)
            {
                for (int k = 0; k < batch.vertices.Count; k++, v++)
                {
                    var p = batch.vertices[k];
                    var uv = batch.uvs[k];
                    var c = batch.colors[k];
                    _vertices[v] = new Vertex { X = p.x, Y = p.y, U = uv.x, V = uv.y, R = c.r, G = c.g, B = c.b, A = c.a };
                }
                // Batch triangles index into the batch's own vertices; DrawIndexed adds the base vertex.
                batch.triangles.CopyTo(_indices, i);
                i += batch.triangles.Count;
            }

            _context.OMSetRenderTargets(_renderTarget);
            _context.ClearRenderTargetView(_renderTarget, new Color4(0f, 0f, 0f, 0f));
            if (indexCount > 0)
            {
                Upload(vertexCount, indexCount);
                SetPipeline();

                int baseVertex = 0, startIndex = 0;
                foreach (var batch in batches)
                {
                    if (batch.triangles.Count > 0)
                    {
                        var texture = batch.atlas?.texture as D3DTexture ?? _white;
                        if (texture != null)
                        {
                            if (batch.shader != FShader.defaultShader) UnportedShaders.Add(batch.shader.name);
                            _context.PSSetShaderResource(0, texture.View);
                            _context.DrawIndexed((uint)batch.triangles.Count, (uint)startIndex, baseVertex);
                        }
                    }
                    baseVertex += batch.vertices.Count;
                    startIndex += batch.triangles.Count;
                }
            }
            _swapChain.Present(vsync ? 1u : 0u);
        }

        private void SetPipeline()
        {
            var constants = _context.Map(_constants, MapMode.WriteDiscard);
            Marshal.StructureToPtr(new ScreenConstants { InvHalfWidth = 2f / Width, InvHalfHeight = 2f / Height }, constants.DataPointer, false);
            _context.Unmap(_constants);

            _context.RSSetViewport(0, 0, Width, Height);
            _context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
            _context.IASetInputLayout(_inputLayout);
            _context.IASetVertexBuffer(0, _vertexBuffer, (uint)Marshal.SizeOf<Vertex>());
            _context.IASetIndexBuffer(_indexBuffer, Format.R32_UInt, 0);
            _context.VSSetShader(_vertexShader);
            _context.VSSetConstantBuffer(0, _constants);
            _context.PSSetShader(_pixelShader);
            _context.PSSetSampler(0, _sampler);
            _context.OMSetBlendState(_blend);
        }

        private unsafe void Upload(int vertexCount, int indexCount)
        {
            if (vertexCount > _vertexCapacity)
            {
                _vertexBuffer?.Dispose();
                _vertexCapacity = Math.Max(vertexCount, _vertexCapacity * 2);
                _vertexBuffer = _device.CreateBuffer((uint)(_vertexCapacity * sizeof(Vertex)), BindFlags.VertexBuffer,
                    ResourceUsage.Dynamic, CpuAccessFlags.Write);
            }
            if (indexCount > _indexCapacity)
            {
                _indexBuffer?.Dispose();
                _indexCapacity = Math.Max(indexCount, _indexCapacity * 2);
                _indexBuffer = _device.CreateBuffer((uint)(_indexCapacity * sizeof(int)), BindFlags.IndexBuffer,
                    ResourceUsage.Dynamic, CpuAccessFlags.Write);
            }

            var mapped = _context.Map(_vertexBuffer, MapMode.WriteDiscard);
            new ReadOnlySpan<Vertex>(_vertices, 0, vertexCount).CopyTo(new Span<Vertex>((void*)mapped.DataPointer, vertexCount));
            _context.Unmap(_vertexBuffer);

            mapped = _context.Map(_indexBuffer, MapMode.WriteDiscard);
            new ReadOnlySpan<int>(_indices, 0, indexCount).CopyTo(new Span<int>((void*)mapped.DataPointer, indexCount));
            _context.Unmap(_indexBuffer);
        }

        private void CreateRenderTarget()
        {
            using (var backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0))
                _renderTarget = _device.CreateRenderTargetView(backBuffer);
        }

        private static Blob Compile(string entryPoint, string profile)
        {
            var result = Compiler.Compile(ShaderSource, entryPoint, "FutileRenderer.hlsl", profile, out Blob code, out Blob errors);
            if (result.Failure)
            {
                var message = errors?.AsString() ?? result.ToString();
                errors?.Dispose();
                throw new InvalidOperationException("RWDE.Graphics: shader " + entryPoint + " failed to compile: " + message);
            }
            errors?.Dispose();
            return code;
        }

        public void Dispose()
        {
            foreach (var texture in _textures) texture.Dispose();
            _textures.Clear();
            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();
            _constants.Dispose();
            _sampler.Dispose();
            _blend.Dispose();
            _inputLayout.Dispose();
            _pixelShader.Dispose();
            _vertexShader.Dispose();
            _renderTarget.Dispose();
            _visual?.Dispose();
            _target?.Dispose();
            _composition.Dispose();
            _swapChain.Dispose();
            _dxgiDevice.Dispose();
            _context.Dispose();
            _device.Dispose();
        }
    }
}
