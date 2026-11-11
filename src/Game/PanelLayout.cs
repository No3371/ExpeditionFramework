using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ExpeditionFramework.Game
{
    /// <summary>
    /// Places the three expedition windows inside the game picture: the log down the left edge, the actions down the
    /// right, and the info at the top centre, all clear of the vanilla ground-and-bag window. Measured in world space
    /// at runtime, so it doesn't depend on the prefab's anchors or the canvas scale.
    /// </summary>
    internal static class PanelLayout
    {
        public const float PanelWidth = 500f;
        public const float LogWidth = 460f;
        public const float MinPanelHeight = 420f;
        public const float InfoMinHeight = 140f;
        public const float ButtonSpacing = 4f;

        private const float PanelMargin = 24f;
        private const float PanelGap = 12f;
        private const float PanelShare = 0.32f;
        private const float LogShare = 0.28f;
        private const float MinActionsHeight = 60f;
        private const float InfoChrome = 40f;
        private const float MinHeightShare = 0.5f;
        private const float MarginShare = 0.025f;
        private const float GapShare = 0.01f;
        private const float InfoHeightShare = 0.4f;
        private const float TextSlack = 4f;

        // Widths and spacing are shares of the game picture, capped at the design sizes, because the meta canvas can be
        // as small as ~960x540 units (the UI is drawn large on small screens).
        public static void Arrange(RectTransform canvas, RectTransform info, RectTransform log, RectTransform actions, float actionsHeight)
        {
            Rect area = GameArea(canvas);
            float unit = canvas.lossyScale.x;
            float margin = Mathf.Min(PanelMargin * unit, area.height * MarginShare);
            float gap = Mathf.Min(PanelGap * unit, area.width * GapShare);
            Rect? ground = GroundRect(canvas);
            float top = area.yMax - margin;
            float floor = area.yMin + margin;
            float lowest = top - Mathf.Min(MinPanelHeight * unit, area.height * MinHeightShare);

            float logLeft = area.xMin + margin;
            float logRight = logLeft + Mathf.Min(LogWidth * unit, area.width * LogShare);
            SetBox(log, logLeft, Mathf.Min(BottomClear(ground, logLeft, logRight, floor, gap), lowest), logRight, top);

            float actionsRight = area.xMax - margin;
            float actionsLeft = actionsRight - Mathf.Min(PanelWidth * unit, area.width * PanelShare);
            float actionsFloor = BottomClear(ground, actionsLeft, actionsRight, floor, gap);
            float needed = Mathf.Max(actionsHeight, MinActionsHeight) * unit;
            SetBox(actions, actionsLeft, Mathf.Max(top - needed, actionsFloor), actionsRight, top);

            float spanLeft = logRight + gap;
            float spanRight = actionsLeft - gap;
            float infoWidth = Mathf.Min(PanelWidth * unit, spanRight - spanLeft);
            float centre = Mathf.Clamp(area.center.x, spanLeft + infoWidth / 2f, spanRight - infoWidth / 2f);
            float infoLeft = centre - infoWidth / 2f;
            float infoRight = centre + infoWidth / 2f;
            float infoFloor = top - (InfoMinHeight + InfoChrome) * unit;
            float infoBottom = Mathf.Max(top - area.height * InfoHeightShare, BottomClear(ground, infoLeft, infoRight, floor, gap));
            SetBox(info, infoLeft, Mathf.Min(infoBottom, infoFloor), infoRight, top);
        }

        /// <summary>Moves buttons into rows, filling each left to right. An item flagged NewRow starts a new row.</summary>
        public static void FlowIntoRows(RectTransform host, List<(RectTransform Item, bool NewRow)> items, float width)
        {
            RectTransform row = null;
            float used = 0f;
            foreach ((RectTransform item, bool newRow) in items)
            {
                float itemWidth = Mathf.Min(MeasureWidth(item), width);
                // Pin the width so the row gives each button room for its text instead of squeezing it.
                LayoutElement pin = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
                pin.minWidth = itemWidth;
                pin.preferredWidth = itemWidth;
                pin.flexibleWidth = 0f;
                if (row == null || newRow || used + ButtonSpacing + itemWidth > width)
                {
                    row = NewRow(host);
                    used = itemWidth;
                }
                else
                {
                    used += ButtonSpacing + itemWidth;
                }
                item.SetParent(row, false);
            }
        }

        // A freshly spawned button's layout sizes aren't computed yet and read as zero, so measure its text directly,
        // plus the padding between the text and the button's edges.
        private static float MeasureWidth(RectTransform item)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(item);
            float width = LayoutUtility.GetPreferredWidth(item);
            TextMeshProUGUI label = item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                float padding = Mathf.Max(0f, item.rect.width - label.rectTransform.rect.width);
                float textWidth = label.GetPreferredValues(label.text, float.PositiveInfinity, float.PositiveInfinity).x;
                width = Mathf.Max(width, Mathf.Ceil(textWidth + padding + TextSlack));
            }
            return width;
        }

        private static RectTransform NewRow(RectTransform host)
        {
            var row = new GameObject("ef_row", typeof(RectTransform));
            row.transform.SetParent(host, false);
            HorizontalLayoutGroup group = row.AddComponent<HorizontalLayoutGroup>();
            group.spacing = ButtonSpacing;
            group.childControlWidth = true;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleLeft;
            return (RectTransform)row.transform;
        }

        // The game picture in the canvas's world space. On screens that aren't 16:9 the game draws black bars and keeps
        // its picture in a 16:9 area in the middle, but this canvas may still span the whole screen.
        private static Rect GameArea(RectTransform canvas)
        {
            Rect area = WorldRect(canvas);
            Camera camera = CanvasCamera(canvas);
            var low = new Vector2(LetterboxManager.GameAreaX, LetterboxManager.GameAreaY);
            Vector2 high = low + new Vector2(LetterboxManager.GameAreaWidth, LetterboxManager.GameAreaHeight);
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas, low, camera, out Vector3 lowWorld)
                || !RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas, high, camera, out Vector3 highWorld))
            {
                return area;
            }
            return Rect.MinMaxRect(Mathf.Max(area.xMin, lowWorld.x), Mathf.Max(area.yMin, lowWorld.y), Mathf.Min(area.xMax, highWorld.x), Mathf.Min(area.yMax, highWorld.y));
        }

        private static float BottomClear(Rect? ground, float left, float right, float floor, float gap)
        {
            if (ground is Rect g && g.xMax > left && g.xMin < right) return Mathf.Max(floor, g.yMax + gap);
            return floor;
        }

        // The vanilla ground, pockets and backpack window, in the panel canvas's world space. It lives on another canvas.
        private static Rect? GroundRect(RectTransform canvas)
        {
            PixelWindow groundWindow = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.afterhourWindow : null;
            if (groundWindow == null || groundWindow.handler == null || !groundWindow.IsVisible()) return null;
            // The window's own RectTransform has no size; its border, background and panels are children sized in pixels.
            RectTransform source = groundWindow.rectTransform;
            Rect? bounds = null;
            foreach (RectTransform part in source.GetComponentsInChildren<RectTransform>(false))
            {
                if (part == source || part.rect.width <= 0f || part.rect.height <= 0f) continue;
                Rect box = WorldRect(part);
                bounds = bounds is Rect b
                    ? Rect.MinMaxRect(Mathf.Min(b.xMin, box.xMin), Mathf.Min(b.yMin, box.yMin), Mathf.Max(b.xMax, box.xMax), Mathf.Max(b.yMax, box.yMax))
                    : box;
            }
            if (!(bounds is Rect sourceBox)) return null;
            Camera sourceCamera = CanvasCamera(source);
            Camera targetCamera = CanvasCamera(canvas);
            Vector3[] corners =
            {
                new Vector3(sourceBox.xMin, sourceBox.yMin, source.position.z),
                new Vector3(sourceBox.xMax, sourceBox.yMax, source.position.z),
            };
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
                if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(canvas, screen, targetCamera, out Vector3 world)) return null;
                xMin = Mathf.Min(xMin, world.x);
                yMin = Mathf.Min(yMin, world.y);
                xMax = Mathf.Max(xMax, world.x);
                yMax = Mathf.Max(yMax, world.y);
            }
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static Camera CanvasCamera(Transform child)
        {
            Canvas canvas = child.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void SetBox(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            if (!(rect.parent is RectTransform parent)) return;
            Vector3 scale = rect.lossyScale;
            rect.sizeDelta = new Vector2((xMax - xMin) / scale.x, (yMax - yMin) / scale.y);
            Rect now = WorldRect(rect);
            var worldShift = new Vector3(xMin - now.xMin, yMin - now.yMin, 0f);
            rect.anchoredPosition += (Vector2)parent.InverseTransformVector(worldShift);
        }
    }
}
