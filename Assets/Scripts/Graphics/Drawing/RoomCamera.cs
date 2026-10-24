using System;
using System.Collections.Generic;
using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// Draws one room's objects, as the game's <c>RoomCamera</c> (only its drawing half). It owns the
    /// game's named sprite layers on an <see cref="FStage"/>, keeps a <see cref="SpriteLeaser"/> per drawable,
    /// and each frame calls every drawable's <c>DrawSprites</c>. Graphics modules for sim objects come from a
    /// <see cref="GraphicsModuleRegistry"/> via <see cref="UpdateGraphicsModules"/>.
    /// </summary>
    public sealed class RoomCamera
    {
        /// <summary>The sprites one drawable owns on one camera, as the game's <c>RoomCamera.SpriteLeaser</c>.</summary>
        public sealed class SpriteLeaser
        {
            public readonly IDrawable drawableObject;
            public FSprite[] sprites;
            public FContainer[] containers;
            /// <summary>Set by <see cref="CleanSpritesAndRemove"/>; the camera drops the leaser after this frame.</summary>
            public bool deleteMeNextFrame;

            /// <summary>Creates the sprites and applies the camera's palette (SpriteLeaser constructor).</summary>
            public SpriteLeaser(IDrawable obj, RoomCamera rCam)
            {
                drawableObject = obj;
                drawableObject.InitiateSprites(this, rCam);
                drawableObject.ApplyPalette(this, rCam, rCam.currentPalette);
            }

            public void Update(float timeStacker, RoomCamera rCam, Vector2 camPos)
            {
                drawableObject.DrawSprites(this, rCam, timeStacker, camPos);
            }

            public void CleanSpritesAndRemove()
            {
                deleteMeNextFrame = true;
                RemoveAllSpritesFromContainer();
            }

            public void RemoveAllSpritesFromContainer()
            {
                if (sprites != null)
                {
                    foreach (var sprite in sprites) sprite.RemoveFromContainer();
                }
                if (containers != null)
                {
                    foreach (var container in containers) container.RemoveFromContainer();
                }
            }

            public void AddSpritesToContainer(FContainer newContainer, RoomCamera rCam)
            {
                drawableObject.AddToContainer(this, rCam, newContainer);
            }

            public void UpdatePalette(RoomCamera rCam, RoomPalette palette)
            {
                drawableObject.ApplyPalette(this, rCam, palette);
            }
        }

        /// <summary>The game's sprite layers, back to front (RoomCamera.SpriteLayerIndex).</summary>
        public static readonly IReadOnlyList<string> SpriteLayerNames = new[]
        {
            "Shadows", "BackgroundShortcuts", "Background", "Midground", "Items", "Foreground", "Sand",
            "ForegroundLights", "Shortcuts", "Water", "GrabShaders", "Bloom", "WarpPoint", "HUD", "HUD2",
        };

        private readonly FContainer[] _spriteLayers;
        private readonly Dictionary<string, int> _spriteLayerIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<SpriteLeaser> _spriteLeasers = new List<SpriteLeaser>();
        private readonly Dictionary<PhysicalObject, GraphicsModule> _graphicsModules = new Dictionary<PhysicalObject, GraphicsModule>();
        private readonly HashSet<PhysicalObject> _seen = new HashSet<PhysicalObject>();
        private readonly List<PhysicalObject> _gone = new List<PhysicalObject>();

        public RoomCamera(FStage stage, Room room, GraphicsModuleRegistry registry)
        {
            this.stage = stage ?? throw new ArgumentNullException(nameof(stage));
            this.room = room;
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _spriteLayers = new FContainer[SpriteLayerNames.Count];
            for (int i = 0; i < _spriteLayers.Length; i++)
            {
                _spriteLayers[i] = new FContainer();
                stage.AddChild(_spriteLayers[i]);
                _spriteLayerIndex.Add(SpriteLayerNames[i], i);
            }
        }

        public readonly FStage stage;
        public readonly GraphicsModuleRegistry registry;
        public Room room;

        /// <summary>Camera position in room pixels; the desktop camera stays at the origin.</summary>
        public Vector2 pos;
        public Vector2 lastPos;

        public RoomPalette currentPalette;

        public IReadOnlyList<SpriteLeaser> SpriteLeasers => _spriteLeasers;

        /// <summary>The sprite layer called <paramref name="layerName"/>, e.g. "Midground" (RoomCamera.ReturnFContainer).</summary>
        public FContainer ReturnFContainer(string layerName)
        {
            if (!_spriteLayerIndex.TryGetValue(layerName, out var index))
                throw new ArgumentException("No sprite layer named '" + layerName + "'", nameof(layerName));
            return _spriteLayers[index];
        }

        /// <summary>Starts drawing <paramref name="obj"/>: creates its sprites now (RoomCamera.NewObjectInRoom).</summary>
        public SpriteLeaser NewObjectInRoom(IDrawable obj)
        {
            var leaser = new SpriteLeaser(obj, this);
            _spriteLeasers.Add(leaser);
            return leaser;
        }

        /// <summary>As <see cref="NewObjectInRoom(IDrawable)"/>, but drawn before <paramref name="insertBefore"/> in the update order.</summary>
        public SpriteLeaser NewObjectInRoom(IDrawable obj, IDrawable insertBefore)
        {
            int index = _spriteLeasers.FindIndex(l => l.drawableObject == insertBefore);
            if (index < 0) return null;
            var leaser = new SpriteLeaser(obj, this);
            _spriteLeasers.Insert(index, leaser);
            return leaser;
        }

        /// <summary>The module drawing <paramref name="obj"/>, or null.</summary>
        public GraphicsModule GraphicsModuleOf(PhysicalObject obj) =>
            _graphicsModules.TryGetValue(obj, out var module) ? module : null;

        /// <summary>
        /// Once per sim tick: creates a module for each new object in <paramref name="roomObjects"/> that has
        /// one registered, removes the sprites of objects that are gone, and runs every module's
        /// <see cref="GraphicsModule.Update"/>. In the game, <c>Room.AddObject</c> and the owner's own
        /// <c>Update</c> do this.
        /// </summary>
        public void UpdateGraphicsModules(IEnumerable<PhysicalObject> roomObjects)
        {
            _seen.Clear();
            foreach (var obj in roomObjects)
            {
                if (obj == null || obj.slatedForDeletetion || !_seen.Add(obj)) continue;
                if (!_graphicsModules.TryGetValue(obj, out var module))
                {
                    module = registry.Create(obj);
                    if (module == null) continue;
                    _graphicsModules.Add(obj, module);
                    NewObjectInRoom(module);
                }
                module.Update();
            }

            _gone.Clear();
            foreach (var obj in _graphicsModules.Keys)
            {
                if (!_seen.Contains(obj)) _gone.Add(obj);
            }
            foreach (var obj in _gone)
            {
                var module = _graphicsModules[obj];
                _graphicsModules.Remove(obj);
                module.dispose = true;
                foreach (var leaser in _spriteLeasers)
                {
                    if (leaser.drawableObject == module) leaser.CleanSpritesAndRemove();
                }
            }
        }

        /// <summary>
        /// Once per rendered frame: positions every drawable's sprites at <paramref name="timeStacker"/>
        /// (0 = last tick, 1 = this tick), back to front as the game does, and drops removed ones
        /// (RoomCamera.DrawUpdate).
        /// </summary>
        public void DrawUpdate(float timeStacker)
        {
            var camPos = Vector2.Lerp(lastPos, pos, timeStacker);
            for (int i = _spriteLeasers.Count - 1; i >= 0; i--)
            {
                var leaser = _spriteLeasers[i];
                if (!leaser.deleteMeNextFrame) leaser.Update(timeStacker, this, camPos);
                if (leaser.deleteMeNextFrame) _spriteLeasers.RemoveAt(i);
            }
        }

        /// <summary>Makes <paramref name="palette"/> current and re-colours every drawable (RoomCamera.ApplyPalette).</summary>
        public void ApplyPalette(RoomPalette palette)
        {
            currentPalette = palette;
            foreach (var leaser in _spriteLeasers) leaser.UpdatePalette(this, currentPalette);
        }
    }
}
