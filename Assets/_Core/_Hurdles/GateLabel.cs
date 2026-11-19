using TMPro;
using UnityEngine;

namespace Game.Hurdles
{
    /// <summary>Makes the floating text label used on gates that have no label of their own.</summary>
    internal static class GateLabel
    {
        public static TMP_Text Create(Transform gate, float height, float fontSize, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(gate, false);
            // A little in front of the gate (the player runs along +Z, so she sees the -Z side).
            go.transform.localPosition = new Vector3(0f, height, -0.3f);

            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = color;
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            text.rectTransform.sizeDelta = new Vector2(4f, 2f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = fontSize * 0.4f;
            text.fontSizeMax = fontSize;
            text.sortingOrder = 10;
            return text;
        }
    }
}
