using System;
using System.Collections.Generic;
using System.Linq;

namespace Loop
{
    public struct Coord : IEquatable<Coord>
    {
        public int Index { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int X => Index % Width;
        public int Y => Index / Width;

        public Coord(int width, int height, int index) { Width = width; Height = height; Index = index; }
        public Coord(int width, int height, int x, int y) : this(width, height, x + width * y) { }
        public override string ToString() => $"({X}, {Y})/({Width}×{Height})";

        public Coord AddXWrap(int dx) => new Coord(Width, Height, ((X + dx) % Width + Width) % Width, Y);
        public Coord AddYWrap(int dy) => new Coord(Width, Height, X, ((Y + dy) % Height + Height) % Height);
        public Coord AddWrap(int dx, int dy) => new Coord(Width, Height, ((X + dx) % Width + Width) % Width, ((Y + dy) % Height + Height) % Height);
        public Coord AddWrap(Coord c) => AddWrap(c.X, c.Y);

        public bool Equals(Coord other) => other.Index == Index && other.Width == Width && other.Height == Height;
        public override bool Equals(object obj) => obj is Coord && Equals((Coord) obj);
        public override int GetHashCode() => unchecked(Index * 1048583 + Width * 1031 + Height);

        public static bool operator ==(Coord one, Coord two) => one.Equals(two);
        public static bool operator !=(Coord one, Coord two) => !one.Equals(two);

        public static IEnumerable<Coord> Cells(int w, int h) => Enumerable.Range(0, w * h).Select(ix => new Coord(w, h, ix));
        public bool AdjacentToWrap(Coord other) => other == AddXWrap(1) || other == AddXWrap(-1) || other == AddYWrap(1) || other == AddYWrap(-1);
        public bool AdjacentTo(Coord other) =>
            (CanMoveBy(-1, 0) && AddXWrap(-1) == other) ||
            (CanMoveBy(1, 0) && AddXWrap(1) == other) ||
            (CanMoveBy(0, -1) && AddYWrap(-1) == other) ||
            (CanMoveBy(0, 1) && AddYWrap(1) == other);

        public bool CanGoTo(GridDirection dir, int amount = 1)
        {
            switch (dir)
            {
                case GridDirection.Up: return Y >= amount;
                case GridDirection.UpRight: return Y >= amount && X < Width - amount;
                case GridDirection.Right: return X < Width - amount;
                case GridDirection.DownRight: return Y < Height - amount && X < Width - amount;
                case GridDirection.Down: return Y < Height - amount;
                case GridDirection.DownLeft: return Y < Height - amount && X >= amount;
                case GridDirection.Left: return X >= amount;
                case GridDirection.UpLeft: return X >= amount && Y >= amount;
                default: throw new ArgumentOutOfRangeException(nameof(dir), "Invalid GridDirection enum value.");
            }
        }

        public bool CanMoveBy(int x, int y) => (X + x) >= 0 && (X + x) < Width && (Y + y) >= 0 && (Y + y) < Height;

        public Coord Neighbor(GridDirection dir)
        {
            if (!CanGoTo(dir))
                throw new InvalidOperationException("The grid has no neighbor in that direction.");
            else
                return NeighborWrap(dir);
        }

        public Coord NeighborWrap(GridDirection dir)
        {
            switch (dir)
            {
                case GridDirection.Up: return AddWrap(0, -1);
                case GridDirection.UpRight: return AddWrap(1, -1);
                case GridDirection.Right: return AddWrap(1, 0);
                case GridDirection.DownRight: return AddWrap(1, 1);
                case GridDirection.Down: return AddWrap(0, 1);
                case GridDirection.DownLeft: return AddWrap(-1, 1);
                case GridDirection.Left: return AddWrap(-1, 0);
                case GridDirection.UpLeft: return AddWrap(-1, -1);
                default: throw new ArgumentOutOfRangeException(nameof(dir), "Invalid GridDirection enum value.");
            }
        }

        public IEnumerable<Coord> Neighbors
        {
            get
            {
                for (var i = 0; i < 8; i++)
                    if (CanGoTo((GridDirection) i))
                        yield return Neighbor((GridDirection) i);
            }
        }

        public IEnumerable<Coord> NeighborsWrap
        {
            get
            {
                for (var i = 0; i < 8; i++)
                    yield return NeighborWrap((GridDirection) i);
            }
        }

        public IEnumerable<Coord> OrthogonalNeighbors
        {
            get
            {
                for (var i = 0; i < 4; i++)
                    if (CanGoTo((GridDirection) (2 * i)))
                        yield return Neighbor((GridDirection) (2 * i));
            }
        }

        public IEnumerable<Coord> OrthogonalNeighborsWrap
        {
            get
            {
                for (var i = 0; i < 8; i += 2)
                    yield return NeighborWrap((GridDirection) i);
            }
        }
    }
}