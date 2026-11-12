using System.Collections.Generic;
using System.Linq;
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
        public const float InfoGap = 8f;
        public const float InfoLineSpacing = 2f;
        public const float IconSize = 32f;
        public const float BarHeight = 14f;

        private const float MinButtonWidth = 110f;
        private const float MaxButtonShare = 0.4f;
        private const float MinTextWidth = 80f;
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

        /// <summary>
        /// Gives every action row's button the same width, wide enough for the longest label up to a share of the window,
        /// and the rest of the row to its info: its icons, then a text column that wraps.
        /// </summary>
        public static void SizeRows(List<(RectTransform Button, RectTransform Text, int Icons)> rows, float width)
        {
            if (rows.Count == 0) return;
            float buttonWidth = Mathf.Clamp(rows.Max(r => MeasureWidth(r.Button)), MinButtonWidth, Mathf.Max(MinButtonWidth, width * MaxButtonShare));
            foreach ((RectTransform button, RectTransform text, int icons) in rows)
            {
                FitButton(button, buttonWidth);
                Pin(text, Mathf.Max(MinTextWidth, width - buttonWidth - InfoGap - icons * (IconSize + InfoGap)));
            }
        }

        // A label longer than the shared width wraps, and the button grows to hold it.
        private static void FitButton(RectTransform button, float width)
        {
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            float paddingX = label != null ? Mathf.Max(0f, button.rect.width - label.rectTransform.rect.width) : 0f;
            float paddingY = label != null ? Mathf.Max(0f, button.rect.height - label.rectTransform.rect.height) : 0f;
            LayoutElement pin = Pin(button, width);
            if (label == null) return;
            label.enableWordWrapping = true;
            float textHeight = label.GetPreferredValues(label.text, Mathf.Max(1f, width - paddingX), float.PositiveInfinity).y;
            pin.preferredHeight = Mathf.Max(LayoutUtility.GetPreferredHeight(button), Mathf.Ceil(textHeight + paddingY));
        }

        private static LayoutElement Pin(RectTransform item, float width)
        {
            LayoutElement pin = item.GetComponent<LayoutElement>() ?? item.gameObject.AddComponent<LayoutElement>();
            pin.minWidth = width;
            pin.preferredWidth = width;
            pin.flexibleWidth = 0f;
            return pin;
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
