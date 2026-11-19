using System;

namespace Simulation
{
    /// <summary>
    /// One cell of the board. A cell is empty when <see cref="Size"/> is <see cref="BlockSize.None"/>.
    /// </summary>
    public struct Block : IEquatable<Block>
    {
        public BlockColor Color;
        public BlockSize Size;

        /// <summary>The white placeholder that becomes the next coloured block on the following move.</summary>
        public bool IsIndicator;

        public static Block Empty => default;

        public static Block Indicator => new Block
        {
            Color = BlockColor.White,
            Size = BlockSize.Small,
            IsIndicator = true
        };

        public static Block Of(BlockColor color, BlockSize size) => new Block { Color = color, Size = size };

        public bool IsEmpty => Size == BlockSize.None;

        public bool Equals(Block other) =>
            Color == other.Color && Size == other.Size && IsIndicator == other.IsIndicator;

        public override bool Equals(object obj) => obj is Block other && Equals(other);

        public override int GetHashCode() => (int)Color | ((int)Size << 8) | ((IsIndicator ? 1 : 0) << 16);

        public static bool operator ==(Block a, Block b) => a.Equals(b);

        public static bool operator !=(Block a, Block b) => !a.Equals(b);

        public override string ToString() =>
            IsEmpty ? "." : IsIndicator ? "i" : $"{Color}{Size}";
    }
}
