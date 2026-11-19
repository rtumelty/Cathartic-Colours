using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Simulation
{
    /// <summary>
    /// The board as plain data: <see cref="Width"/> x <see cref="Height"/> cells, index <c>x + y * Width</c>.
    /// Owns a <see cref="NativeArray{T}"/>, so it must be disposed.
    /// </summary>
    public struct BoardState : IDisposable
    {
        public NativeArray<Block> Cells;

        private int width;
        private int height;

        public BoardState(int width, int height, Allocator allocator)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be positive.");

            this.width = width;
            this.height = height;
            Cells = new NativeArray<Block>(width * height, allocator, NativeArrayOptions.ClearMemory);
        }

        public int Width => width;

        public int Height => height;

        public bool IsCreated => Cells.IsCreated;

        public bool InBounds(int2 position) =>
            position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;

        public int IndexOf(int2 position) => position.x + position.y * width;

        public Block this[int2 position]
        {
            get => Cells[IndexOf(position)];
            set => Cells[IndexOf(position)] = value;
        }

        public Block this[int x, int y]
        {
            get => Cells[x + y * width];
            set => Cells[x + y * width] = value;
        }

        public int CountEmpty()
        {
            int count = 0;
            for (int i = 0; i < Cells.Length; i++)
            {
                if (Cells[i].IsEmpty)
                    count++;
            }
            return count;
        }

        public BoardState Clone(Allocator allocator)
        {
            var copy = new BoardState(width, height, allocator);
            copy.Cells.CopyFrom(Cells);
            return copy;
        }

        public void Dispose()
        {
            if (Cells.IsCreated)
                Cells.Dispose();
        }
    }
}
