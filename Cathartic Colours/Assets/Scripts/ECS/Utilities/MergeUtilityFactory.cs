using ECS.Components;

namespace ECS.Utilities
{
    public static class MergeUtilityFactory
    {
        public static IMergeUtility GetMergeUtility(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.AdvancedColorMerge:
                    return new AdvancedColorMergeUtility();
                    
                case GameMode.ColorMerge:
                    return new ColorMergeUtility();
                    
                case GameMode.Standard:
                default:
                    return new StandardMergeUtility();
            }
        }
    }
}