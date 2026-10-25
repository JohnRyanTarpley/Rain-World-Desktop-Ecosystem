using RWDE.Sim;

namespace RWDE.Graphics
{
    /// <summary>
    /// Anything in the sprite tree, as Futile's <c>FNode</c>: a position, scale, rotation (degrees,
    /// clockwise) and alpha relative to its container. Futile's touch, enabler and dirty-flag machinery is
    /// left out; the stage recomputes every transform each frame (<see cref="FStage.BuildBatches"/>), as
    /// Rain World moves every sprite each frame anyway.
    /// </summary>
    public class FNode
    {
        protected float _x;
        protected float _y;
        protected float _scaleX = 1f;
        protected float _scaleY = 1f;
        protected float _rotation;
        protected float _alpha = 1f;
        protected bool _isVisible = true;
        protected FContainer _container;

        private readonly FMatrix _matrix = new FMatrix();
        protected readonly FMatrix _concatenatedMatrix = new FMatrix();
        protected float _concatenatedAlpha = 1f;

        /// <summary>Free for the owner's use (FNode.data).</summary>
        public object data;

        public float x
        {
            get => _x;
            set => _x = value;
        }

        public float y
        {
            get => _y;
            set => _y = value;
        }

        public float scaleX
        {
            get => _scaleX;
            set => _scaleX = value;
        }

        public float scaleY
        {
            get => _scaleY;
            set => _scaleY = value;
        }

        /// <summary>Sets both scales; reads <see cref="scaleX"/> (FNode.scale).</summary>
        public float scale
        {
            get => _scaleX;
            set
            {
                _scaleX = value;
                _scaleY = value;
            }
        }

        /// <summary>Rotation in degrees, clockwise.</summary>
        public float rotation
        {
            get => _rotation;
            set => _rotation = value;
        }

        public virtual float alpha
        {
            get => _alpha;
            set => _alpha = value < 0f ? 0f : value;
        }

        /// <summary>Hidden nodes, and everything inside hidden containers, are not drawn.</summary>
        public bool isVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public FContainer container => _container;

        /// <summary>This node's transform to stage space, as of the last <see cref="FStage.BuildBatches"/>.</summary>
        public FMatrix concatenatedMatrix => _concatenatedMatrix;

        /// <summary>Alpha times every ancestor's alpha, as of the last <see cref="FStage.BuildBatches"/>.</summary>
        public float concatenatedAlpha => _concatenatedAlpha;

        public void SetPosition(float newX, float newY)
        {
            _x = newX;
            _y = newY;
        }

        public void SetPosition(Vector2 newPosition) => SetPosition(newPosition.x, newPosition.y);

        public Vector2 GetPosition() => new Vector2(_x, _y);

        public void RemoveFromContainer()
        {
            _container?.RemoveChild(this);
        }

        /// <summary>Draws this node above its siblings.</summary>
        public void MoveToFront()
        {
            _container?.AddChild(this);
        }

        /// <summary>Draws this node below its siblings.</summary>
        public void MoveToBack()
        {
            _container?.AddChildAtIndex(this, 0);
        }

        public void MoveInFrontOfOtherNode(FNode otherNode)
        {
            if (_container != null && otherNode.container == _container)
                _container.AddChildAtIndex(this, _container.GetChildIndex(otherNode) + 1);
        }

        public void MoveBehindOtherNode(FNode otherNode)
        {
            if (_container != null && otherNode.container == _container)
                _container.AddChildAtIndex(this, _container.GetChildIndex(otherNode));
        }

        /// <summary>True when this node and all its containers are visible.</summary>
        public bool IsAncestryVisible() => _isVisible && (_container == null || _container.IsAncestryVisible());

        // FNode.HandleAddedToContainer: a node lives in one container at a time.
        internal void HandleAddedToContainer(FContainer newContainer)
        {
            if (_container == newContainer) return;
            _container?.RemoveChild(this);
            _container = newContainer;
        }

        internal void HandleRemovedFromContainer()
        {
            _container = null;
        }

        /// <summary>Updates the concatenated transform and alpha, then emits this node's geometry.</summary>
        internal virtual void Draw(FStage stage, FMatrix parentMatrix, float parentAlpha)
        {
            // FNode.UpdateDepthMatrixAlpha
            _matrix.SetScaleThenRotate(_x, _y, _scaleX, _scaleY, _rotation * -0.01745329f);
            if (parentMatrix != null) _concatenatedMatrix.ConcatAndCopyValues(_matrix, parentMatrix);
            else _concatenatedMatrix.CopyValues(_matrix);
            _concatenatedAlpha = parentAlpha * _alpha;
        }
    }
}
