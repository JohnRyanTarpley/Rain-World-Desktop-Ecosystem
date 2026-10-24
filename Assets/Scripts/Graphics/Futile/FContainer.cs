using System.Collections.Generic;

namespace RWDE.Graphics
{
    /// <summary>
    /// A node holding other nodes, drawn in child order (first child at the back), as Futile's
    /// <c>FContainer</c>. Futile's sort-by-z option is left out; Rain World's modules order sprites by
    /// container and <see cref="FNode.MoveBehindOtherNode"/> instead.
    /// </summary>
    public class FContainer : FNode
    {
        protected readonly List<FNode> _childNodes = new List<FNode>(5);

        /// <summary>Adds <paramref name="node"/> at the front, or moves it there if it is already a child.</summary>
        public void AddChild(FNode node)
        {
            int index = _childNodes.IndexOf(node);
            if (index == -1)
            {
                node.HandleAddedToContainer(this);
                _childNodes.Add(node);
            }
            else if (index != _childNodes.Count - 1)
            {
                _childNodes.RemoveAt(index);
                _childNodes.Add(node);
            }
        }

        /// <summary>Inserts or moves <paramref name="node"/> so that it ends up before the child now at <paramref name="newIndex"/>.</summary>
        public void AddChildAtIndex(FNode node, int newIndex)
        {
            int index = _childNodes.IndexOf(node);
            if (newIndex > _childNodes.Count) newIndex = _childNodes.Count;
            if (index == newIndex) return;
            if (index == -1)
            {
                node.HandleAddedToContainer(this);
                _childNodes.Insert(newIndex, node);
                return;
            }
            _childNodes.RemoveAt(index);
            _childNodes.Insert(index < newIndex ? newIndex - 1 : newIndex, node);
        }

        public void RemoveChild(FNode node)
        {
            if (node.container != this) return;
            node.HandleRemovedFromContainer();
            _childNodes.Remove(node);
        }

        public void RemoveAllChildren()
        {
            foreach (var node in _childNodes) node.HandleRemovedFromContainer();
            _childNodes.Clear();
        }

        public int GetChildCount() => _childNodes.Count;

        public FNode GetChildAt(int childIndex) => _childNodes[childIndex];

        public int GetChildIndex(FNode childNode) => _childNodes.IndexOf(childNode);

        internal override void Draw(FStage stage, FMatrix parentMatrix, float parentAlpha)
        {
            base.Draw(stage, parentMatrix, parentAlpha);
            for (int i = 0; i < _childNodes.Count; i++)
            {
                if (_childNodes[i].isVisible) _childNodes[i].Draw(stage, _concatenatedMatrix, _concatenatedAlpha);
            }
        }
    }
}
