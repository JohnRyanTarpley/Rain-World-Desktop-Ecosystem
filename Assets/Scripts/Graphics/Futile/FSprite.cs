using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// One atlas element drawn as a quad, as Futile's <c>FSprite</c>. The anchor (0..1, default the
    /// centre) is the point of the element that sits at <see cref="FNode.x"/>, <see cref="FNode.y"/>.
    /// </summary>
    public class FSprite : FNode
    {
        // FSprite.defaultAnchorX / defaultAnchorY
        public static float defaultAnchorX = 0.5f;
        public static float defaultAnchorY = 0.5f;

        protected FAtlasElement _element;
        protected FShader _shader = FShader.defaultShader;
        protected Color _color = Futile.white;
        protected float _anchorX = defaultAnchorX;
        protected float _anchorY = defaultAnchorY;

        /// <summary>For subclasses that set their element themselves.</summary>
        protected FSprite()
        {
        }

        /// <exception cref="FutileException">No loaded atlas has an element with this name.</exception>
        public FSprite(string elementName) : this(Futile.atlasManager.GetElementWithName(elementName))
        {
        }

        public FSprite(FAtlasElement element)
        {
            _element = element;
        }

        public FAtlasElement element
        {
            get => _element;
            set => _element = value;
        }

        /// <exception cref="FutileException">No loaded atlas has an element with this name.</exception>
        public void SetElementByName(string elementName)
        {
            element = Futile.atlasManager.GetElementWithName(elementName);
        }

        public FShader shader
        {
            get => _shader;
            set => _shader = value ?? FShader.defaultShader;
        }

        public virtual Color color
        {
            get => _color;
            set => _color = value;
        }

        public virtual float anchorX
        {
            get => _anchorX;
            set => _anchorX = value;
        }

        public virtual float anchorY
        {
            get => _anchorY;
            set => _anchorY = value;
        }

        public void SetAnchor(float newX, float newY)
        {
            anchorX = newX;
            anchorY = newY;
        }

        public void SetAnchor(Vector2 newAnchor) => SetAnchor(newAnchor.x, newAnchor.y);

        public Vector2 GetAnchor() => new Vector2(_anchorX, _anchorY);

        /// <summary>The untrimmed element's rect around the anchor, unscaled (FSprite.textureRect).</summary>
        public virtual Rect textureRect
        {
            get
            {
                var size = _element.sourceSize;
                return new Rect(-_anchorX * size.x, -_anchorY * size.y, size.x, size.y);
            }
        }

        /// <summary>The drawn (trimmed) quad in local space (FSprite.localRect).</summary>
        public virtual Rect localRect
        {
            get
            {
                // FSprite.UpdateLocalVertices: TexturePacker's sourceRect.y counts from the top.
                var texture = textureRect;
                var source = _element.sourceRect;
                return new Rect(texture.x + source.x, texture.y + (texture.height - source.y - source.height), source.width, source.height);
            }
        }

        public virtual float width
        {
            get => _scaleX * textureRect.width;
            set => _scaleX = value / textureRect.width;
        }

        public virtual float height
        {
            get => _scaleY * textureRect.height;
            set => _scaleY = value / textureRect.height;
        }

        /// <summary>The vertex colour: <see cref="color"/> with its alpha times the concatenated alpha (ApplyMultipliedAlpha).</summary>
        protected Color AlphaColor(Color c) => new Color(c.r, c.g, c.b, c.a * _concatenatedAlpha);

        internal override void Draw(FStage stage, FMatrix parentMatrix, float parentAlpha)
        {
            base.Draw(stage, parentMatrix, parentAlpha);
            PopulateRenderLayer(stage.GetBatch(_element.atlas, _shader));
        }

        /// <summary>Appends this sprite's triangles, in stage space, to <paramref name="batch"/>.</summary>
        protected virtual void PopulateRenderLayer(FRenderBatch batch)
        {
            // FSprite.PopulateRenderLayer, quad facet: top-left, top-right, bottom-right, bottom-left.
            var r = localRect;
            var m = _concatenatedMatrix;
            var c = AlphaColor(_color);
            int first = batch.vertices.Count;
            batch.AddVertex(m.Apply(new Vector2(r.xMin, r.yMax)), _element.uvTopLeft, c);
            batch.AddVertex(m.Apply(new Vector2(r.xMax, r.yMax)), _element.uvTopRight, c);
            batch.AddVertex(m.Apply(new Vector2(r.xMax, r.yMin)), _element.uvBottomRight, c);
            batch.AddVertex(m.Apply(new Vector2(r.xMin, r.yMin)), _element.uvBottomLeft, c);
            batch.AddTriangle(first, first + 1, first + 2);
            batch.AddTriangle(first, first + 2, first + 3);
        }
    }
}
