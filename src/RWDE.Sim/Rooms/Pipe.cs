using System;
using System.Collections.Generic;

namespace RWDE.Sim
{
    /// <summary>
    /// A desktop pipe, reported by the terrain source. In the room it becomes a game shortcut: an
    /// entrance tile at <see cref="A"/>, joined by a tile path to <see cref="B"/>. A pipe inside one
    /// room has an entrance at both ends (the game's <c>ShortcutData.Type.Normal</c>).
    /// </summary>
    /// <remarks>
    /// A pipe between monitor rooms is the game's room exit: each of the two rooms reports its own half,
    /// from an entrance at <see cref="A"/> to the room exit tile at <see cref="B"/> (the level files'
    /// shortcut flag 2), and both halves carry the same <see cref="RoomLink"/>. The <see cref="World"/>
    /// joins the two rooms through that pair, as the game's world file joins room exits.
    /// </remarks>
    public sealed class Pipe
    {
        private readonly IntVector2[] path;

        /// <param name="id">Stable id chosen by the terrain source; diffs remove and replace pipes by id.</param>
        /// <param name="path">Tiles from entrance A to B, both included, each a 4-neighbour of the last.</param>
        /// <param name="roomLink">
        /// -1 for a pipe inside the room. Otherwise B is a room exit, and the pipe continues in the room
        /// whose exit pipe has the same link.
        /// </param>
        public Pipe(int id, IReadOnlyList<IntVector2> path, int roomLink = -1)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (path.Count < 2)
                throw new ArgumentException("A pipe path needs at least its two ends.", nameof(path));
            if (roomLink < -1) throw new ArgumentOutOfRangeException(nameof(roomLink));

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
            RoomLink = roomLink;
            this.path = new IntVector2[path.Count];
            for (int i = 0; i < path.Count; i++) this.path[i] = path[i];
        }

        public int Id { get; }

        /// <summary>Pairs this room-exit pipe with its other half in another room; -1 for a pipe inside the room.</summary>
        public int RoomLink { get; }

        /// <summary>Whether <see cref="B"/> leaves the room rather than being a second entrance.</summary>
        public bool IsRoomExit => RoomLink >= 0;

        public IntVector2 A => path[0];
        public IntVector2 B => path[path.Length - 1];

        /// <summary>Every tile from <see cref="A"/> to <see cref="B"/>, both included.</summary>
        public IReadOnlyList<IntVector2> Path => path;

        /// <summary>Steps from one end to the other (the game's <c>ShortcutData.length</c>).</summary>
        public int Length => path.Length - 1;

        /// <summary>The other end of the pipe, given one end.</summary>
        public IntVector2 OtherEnd(IntVector2 entrance)
        {
            if (entrance == A) return B;
            if (entrance == B) return A;
            throw new ArgumentException(entrance + " is not an end of pipe " + Id + ".", nameof(entrance));
        }

        /// <summary>The path walked from <paramref name="entrance"/> to the other end.</summary>
        public IntVector2[] PathFrom(IntVector2 entrance)
        {
            var copy = (IntVector2[])path.Clone();
            if (entrance == B) Array.Reverse(copy);
            else if (entrance != A)
                throw new ArgumentException(entrance + " is not an end of pipe " + Id + ".", nameof(entrance));
            return copy;
        }

        public override string ToString() =>
            "pipe " + Id + " (" + A + " <-> " + B + ", length " + Length + (IsRoomExit ? ", room link " + RoomLink : "") + ")";
    }
}
