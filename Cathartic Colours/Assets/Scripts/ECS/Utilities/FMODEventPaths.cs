using Unity.Collections;

namespace ECS.Utilities
{
    public static class FMODEventPaths
    {
        public static FixedString64Bytes BlockMove => "event:/Block_Move";
        public static FixedString64Bytes BlockMergeSmall => "event:/Block_Merge_Small";
        public static FixedString64Bytes BlockMergeMedium => "event:/Block_Merge_Medium";
        public static FixedString64Bytes BlockMergeLarge => "event:/Block_Merge_Large";
        public static FixedString64Bytes GameOver => "event:/GameOver";
    }
}