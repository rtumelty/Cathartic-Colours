using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

public class LayoutObserver
{
    private readonly UIDocument uiDocument;
    private bool isInitialized = false;

    public delegate void LayoutRecalculatedHandler(float topPadding, float bottomPadding);
    public event LayoutRecalculatedHandler OnLayoutRecalculated;

    public LayoutObserver(UIDocument uiDocument)
    {
        this.uiDocument = uiDocument;
        
        // Delay the callback registration to ensure UI is fully laid out
        var root = uiDocument.rootVisualElement;
        root.schedule.Execute(() => {
            RegisterLayoutCallback();
        }).ExecuteLater(100); // Wait 100ms for UI to stabilize
    }

    private void RegisterLayoutCallback()
    {
        var root = uiDocument.rootVisualElement;
        
        root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            // Only process after initial layout is complete
            if (!isInitialized)
            {
                // Wait one more frame to ensure all elements are properly positioned
                root.schedule.Execute(() => {
                    CalculateAndNotifyLayout();
                    isInitialized = true;
                });
                return;
            }
            
            CalculateAndNotifyLayout();
        });
        
        // Also trigger an immediate calculation in case geometry is already valid
        root.schedule.Execute(() => {
            CalculateAndNotifyLayout();
            isInitialized = true;
        });
    }

    private void CalculateAndNotifyLayout()
    {
        var root = uiDocument.rootVisualElement;
        var headerElement = root.Q<VisualElement>("header");
        var footerElement = root.Q<VisualElement>("footer");

        // Calculate actual space occupied from screen edges
        float topPadding = 0;
        float bottomPadding = 0;

        if (headerElement != null)
        {
            var headerBounds = headerElement.worldBound;
            // Validate bounds are reasonable (not zero or negative)
            if (headerBounds.height > 0 && headerBounds.yMax > 0)
            {
                topPadding = headerBounds.yMax;
            }
        }

        if (footerElement != null)
        {
            var footerBounds = footerElement.worldBound;
            var rootHeight = root.resolvedStyle.height;
            // Validate bounds are reasonable
            if (footerBounds.height > 0 && rootHeight > 0 && footerBounds.yMin < rootHeight)
            {
                bottomPadding = rootHeight - footerBounds.yMin;
            }
        }

        // Only notify if we have valid padding values
        if (topPadding >= 0 && bottomPadding >= 0)
        {
            OnLayoutRecalculated?.Invoke(topPadding, bottomPadding);
        }
    }
}
