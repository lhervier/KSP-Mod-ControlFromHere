using UnityEngine;
using UnityEngine.UI;
using TMPro;
using com.github.lhervier.ksp.mod.controlfromhere.ui.styles;
using com.github.lhervier.ksp.shared.ugui;
using com.github.lhervier.ksp.shared.ugui.sprites;

namespace com.github.lhervier.ksp.mod.controlfromhere.ui.ugui
{
    /// <summary>
    /// The fixed "Command modules: X" sub-header shown between the breaker banner and the list: a thin
    /// uppercase caption with a bottom separator. Builds the structure and hands the label to the
    /// <see cref="CountHeaderController"/>, which keeps the count in sync with the active vessel.
    /// </summary>
    public class CountHeaderBuilder : IUGUIBuilder<CountHeaderController>
    {
        // ===========================================
        // Builder parameters
        // ===========================================

        private ControlFromHereViewModel _viewModel;
        public CountHeaderBuilder WithViewModel(ControlFromHereViewModel viewModel)
        {
            this._viewModel = viewModel;
            return this;
        }

        // ===========================================
        // Build
        // ===========================================

        public CountHeaderController Build()
        {
            var go = new GameObject("ControlFromHere.CountHeader", typeof(RectTransform));

            var bg = go.AddComponent<Image>();
            bg.sprite = SpritesGlobal.FillSprite;
            bg.type = Image.Type.Simple;
            bg.color = Palette.CountBgColor;
            bg.raycastTarget = true;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(
                Mathf.RoundToInt(Palette.CountPaddingH),
                Mathf.RoundToInt(Palette.CountPaddingH),
                Mathf.RoundToInt(Palette.CountPaddingV),
                Mathf.RoundToInt(Palette.CountPaddingV));
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = UGUILabels.AddLabel(labelGo);
            label.fontSize = Palette.CountFontSize;
            label.color = Palette.CountTextColor;
            label.alignment = TextAlignmentOptions.Left;
            label.characterSpacing = 2f; // letter-spacing on the uppercase caption

            // Bottom separator line (out of layout).
            var sepGo = new GameObject("Separator", typeof(RectTransform));
            sepGo.transform.SetParent(go.transform, false);
            var sepLe = sepGo.AddComponent<LayoutElement>();
            sepLe.ignoreLayout = true;
            var sepRect = sepGo.GetComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0f, 0f);
            sepRect.anchorMax = new Vector2(1f, 0f);
            sepRect.pivot = new Vector2(0.5f, 0f);
            sepRect.sizeDelta = new Vector2(0f, Palette.CountSeparatorThickness);
            sepRect.anchoredPosition = Vector2.zero;
            var sepImg = sepGo.AddComponent<Image>();
            sepImg.sprite = SpritesGlobal.FillSprite;
            sepImg.type = Image.Type.Simple;
            sepImg.color = Palette.CountSeparatorColor;
            sepImg.raycastTarget = false;

            return go.AddComponent<CountHeaderController>()
                .WithLabel(label)
                .WithViewModel(_viewModel);
        }
    }
}
