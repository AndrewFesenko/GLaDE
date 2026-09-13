using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GLaDE.Problems
{
    /// <summary>Small helpers for building world-space UI in code, shared by the editor scene builder and runtime panels.</summary>
    public static class UIKit
    {
        public static readonly Color ButtonColor = new Color(0.24f, 0.28f, 0.40f);
        public static readonly Color AccentColor = new Color(0.20f, 0.55f, 0.40f);
        public static readonly Color WarnColor = new Color(0.55f, 0.40f, 0.20f);

        public static RectTransform MakePanel(RectTransform parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.richText = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        public static Button MakeButton(RectTransform parent, string name, string label, float fontSize, Vector2 size, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = size.x; le.preferredHeight = size.y; le.minHeight = size.y;
            var img = go.GetComponent<Image>();
            img.color = color ?? ButtonColor;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            var text = MakeText(rt, "Label", label, fontSize, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(12, 8));
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(6, 4); text.rectTransform.offsetMax = new Vector2(-6, -4);
            text.color = Color.white;
            return btn;
        }

        public static void SetLabel(Button b, string label)
        {
            if (b == null) return;
            var t = b.GetComponentInChildren<TMP_Text>();
            if (t) t.text = label;
        }

        public static void SetColor(Button b, Color c)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img) img.color = c;
        }
    }
}
