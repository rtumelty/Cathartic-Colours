using UnityEngine;
using UnityEngine.UIElements;

public class LayoutObserver
{
    private readonly UIDocument uiDocument;

    public delegate void LayoutRecalculatedHandler(float topPadding, float bottomPadding);
    public event LayoutRecalculatedHandler OnLayoutRecalculated;

    public LayoutObserver(UIDocument uiDocument)
    {
        this.uiDocument = uiDocument;

        var root = uiDocument.rootVisualElement;
        root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            var headerElement = root.Q<VisualElement>("header");
            var footerElement = root.Q<VisualElement>("footer");

            // Calculate actual space occupied from screen edges
            float topPadding = 0;
            float bottomPadding = 0;

            if (headerElement != null)
            {
                // Space from top of screen to bottom of header
                // = header's Y position + its height + margin-bottom
                var headerBounds = headerElement.worldBound;
                topPadding = headerBounds.yMax;
            }

            if (footerElement != null)
            {
                // Space from bottom of screen to top of footer
                // = screen height - footer's Y position
                var footerBounds = footerElement.worldBound;
                bottomPadding = root.resolvedStyle.height - footerBounds.yMin;
            }

            Debug.Log($"Top: {topPadding}px (from screen top), Bottom: {bottomPadding}px (from screen bottom)");

            OnLayoutRecalculated?.Invoke(topPadding, bottomPadding);
        });
    }
}