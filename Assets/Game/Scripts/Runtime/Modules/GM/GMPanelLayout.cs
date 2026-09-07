#if JULYGF_DEBUG
using July.Diagnostics;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal static class GMPanelLayout
    {
        internal static void Configure()
        {
            var overlay = Object.FindObjectOfType<GMOverlayRoot>();
            if (overlay != null) ConfigureLayout(overlay.transform);
        }

        internal static void ConfigureLayout(Transform root)
        {
            // diagnostics v0.4.1 creates LayoutElements but leaves childControlSize disabled.
            // Keep the package untouched; let its layouts use the authored preferred sizes.
            foreach (var group in root.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
            {
                group.childControlWidth = true;
                group.childControlHeight = true;
                if (group.name == "Enum")
                    foreach (var label in group.GetComponentsInChildren<TMP_Text>(true))
                    {
                        label.enableAutoSizing = true;
                        label.fontSizeMin = 16;
                        label.fontSizeMax = 28;
                    }
            }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)root);
        }
    }
}
#endif
