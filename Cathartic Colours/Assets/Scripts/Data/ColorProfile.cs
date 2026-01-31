using ECS.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Data
{
    [CreateAssetMenu(fileName = "ColorProfile", menuName = "CatharticColors/ColorProfile")]
    public class ColorProfile : ScriptableObject
    {
        [Header("UI and background")] 
        [SerializeField]
        private Color backgroundColor = Color.lightGray;
        [SerializeField]
        private Color gridBackgroundColor = Color.darkGray;
        [SerializeField]
        private VisualTreeAsset uiTreeAsset = null;
        
        [System.Serializable]
        public struct BlockColorMapping
        {
            public BlockColor blockColor;
            public Color renderColor;
        }

        [Header("Block Color Mappings")]
        [SerializeField] private BlockColorMapping[] colorMappings = new BlockColorMapping[]
        {
            new BlockColorMapping { blockColor = BlockColor.None, renderColor = Color.clear },
            new BlockColorMapping { blockColor = BlockColor.Red, renderColor = Color.red },
            new BlockColorMapping { blockColor = BlockColor.Green, renderColor = Color.green },
            new BlockColorMapping { blockColor = BlockColor.Blue, renderColor = Color.blue },
            new BlockColorMapping { blockColor = BlockColor.Yellow, renderColor = Color.yellow },
            new BlockColorMapping { blockColor = BlockColor.Magenta, renderColor = Color.magenta },
            new BlockColorMapping { blockColor = BlockColor.Cyan, renderColor = Color.cyan },
            new BlockColorMapping { blockColor = BlockColor.White, renderColor = Color.white }
        };
        
        /// <summary>
        /// Get the background colors for grid cells
        /// </summary>
        public Color BackgroundColor => backgroundColor;
        public Color GridBackgroundColor => gridBackgroundColor;
        public VisualTreeAsset UITreeAsset => uiTreeAsset;

        /// <summary>
        /// Get the Unity Color for a given BlockColor
        /// </summary>
        public Color GetColor(BlockColor blockColor)
        {
            foreach (var mapping in colorMappings)
            {
                if (mapping.blockColor == blockColor)
                {
                    return mapping.renderColor;
                }
            }
        
            Debug.LogWarning($"No color mapping found for BlockColor.{blockColor}, returning white");
            return Color.white;
        }

        /// <summary>
        /// Validate that all BlockColor enum values have mappings
        /// </summary>
        private void OnValidate()
        {
            var enumValues = System.Enum.GetValues(typeof(BlockColor));
            foreach (BlockColor blockColor in enumValues)
            {
                bool hasMapping = false;
                foreach (var mapping in colorMappings)
                {
                    if (mapping.blockColor == blockColor)
                    {
                        hasMapping = true;
                        break;
                    }
                }
            
                if (!hasMapping)
                {
                    Debug.LogWarning($"Missing color mapping for BlockColor.{blockColor} in {name}");
                }
            }
        }
    }
}
