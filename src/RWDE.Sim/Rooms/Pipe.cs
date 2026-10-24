using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A desktop pipe (two icon columns side by side), reported by the terrain source. In the room
    /// it becomes a game shortcut: entrance tiles at both ends, joined by a tile path.
    /// </summary>
    public sealed class Pipe
    {
        private readonly IntVector2[] path;

        /// <param name="id">Stable id chosen by the terrain source; diffs remove and replace pipes by id.</param>
        /// <param name="path">Tiles from entrance A to entrance B, both included, each a 4-neighbour of the last.</param>
        public Pipe(int id, IReadOnlyList<IntVector2> path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Count < 2)
                throw new ArgumentException("A pipe path needs at least its two entrances.", nameof(path));

            var seen = new HashSet<IntVector2>();
            for (int i = 0; i < path.Count; i++)
            {
                if (!seen.Add(path[i]))
                    throw new ArgumentException("Pipe " + id + " visits " + path[i] + " twice.", nameof(path));
                if (i > 0)
                {
                    var step = path[i] - path[i - 1];
                    if (Math.Abs(step.x) + Math.Abs(step.y) != 1)
                        throw new ArgumentException("Pipe " + id + " jumps from " + path[i - 1] + " to " + path[i] + ".", nameof(path));
                }
            }

            Id = id;
            this.path = new IntVector2[path.Count];
            for (int i = 0; i < path.Count; i++) this.path[i] = path[i];
        }

        public int Id { get; }
        public IntVector2 A => path[0];
        public IntVector2 B => path[path.Length - 1];

        /// <summary>Every tile from <see cref="A"/> to <see cref="B"/>, both included.</summary>
        public IReadOnlyList<IntVector2> Path => path;

        /// <summary>Steps from one entrance to the other (the game's <c>ShortcutData.length</c>).</summary>
        public int Length => path.Length - 1;

        /// <summary>The other end of the pipe, given one entrance.</summary>
        public IntVector2 OtherEnd(IntVector2 entrance)
        {
            if (entrance == A) return B;
            if (entrance == B) return A;
            throw new ArgumentException(entrance + " is not an entrance of pipe " + Id + ".", nameof(entrance));
        }

        /// <summary>The path walked from <paramref name="entrance"/> to the other end.</summary>
        public IntVector2[] PathFrom(IntVector2 entrance)
        {
            var copy = (IntVector2[])path.Clone();
            if (entrance == B) Array.Reverse(copy);
            else if (entrance != A)
                throw new ArgumentException(entrance + " is not an entrance of pipe " + Id + ".", nameof(entrance));
            return copy;
        }

        public override string ToString() => "pipe " + Id + " (" + A + " <-> " + B + ", length " + Length + ")";
    }
}
