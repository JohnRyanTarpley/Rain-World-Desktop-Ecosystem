using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A binary min-heap, ported from the game's <c>RWCustom.MinHeap</c>. Kept rather than using
    /// <see cref="PriorityQueue{TElement, TPriority}"/> so that equal keys come out in the game's order,
    /// which the pre-processed exit distances depend on.
    /// </summary>
    internal sealed class MinHeap<TKey, TValue> where TKey : IComparable<TKey>
    {
        private readonly List<KeyValuePair<TKey, TValue>> list = new List<KeyValuePair<TKey, TValue>>();

        public int Count => list.Count;

        public void Add(TKey key, TValue value)
        {
            list.Add(new KeyValuePair<TKey, TValue>(key, value));
            PercolateDown(list.Count - 1);
        }

        public void Clear() => list.Clear();

        public KeyValuePair<TKey, TValue> TakeMin()
        {
            var result = list[0];
            list[0] = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            PercolateUp(0);
            return result;
        }

        // Named as in the game, where "up" sinks an entry toward the leaves.
        private void PercolateUp(int i)
        {
            while (i * 2 + 1 < list.Count)
            {
                int child = i * 2 + 1;
                if (child + 1 < list.Count && list[child + 1].Key.CompareTo(list[child].Key) < 0)
                    child++;
                if (list[child].Key.CompareTo(list[i].Key) >= 0)
                    break;
                (list[child], list[i]) = (list[i], list[child]);
                i = child;
            }
        }

        private void PercolateDown(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (list[i].Key.CompareTo(list[parent].Key) >= 0)
                    break;
                (list[parent], list[i]) = (list[i], list[parent]);
                i = parent;
            }
        }
    }
}
