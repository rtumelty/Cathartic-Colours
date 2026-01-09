using Unity.Collections;

namespace ECS.Audio
{
    public static class FMODEventPaths
    {
        public static readonly FixedString64Bytes BlockMove = new FixedString64Bytes("event:/Block_Move");
        public static readonly FixedString64Bytes BlockMergeSmall = new FixedString64Bytes("event:/Block_Merge_Small");
        public static readonly FixedString64Bytes BlockMergeMedium = new FixedString64Bytes("event:/Block_Merge_Medium");
        public static readonly FixedString64Bytes BlockMergeLarge = new FixedString64Bytes("event:/Block_Merge_Large");
        public static readonly FixedString64Bytes GameOver = new FixedString64Bytes("event:/GameOver");
    }
}