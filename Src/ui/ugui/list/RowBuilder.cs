using UnityEngine;
using UnityEngine.UI;
using TMPro;
using com.github.lhervier.ksp.controlfromheremod.ui.styles;
using com.github.lhervier.ksp.shared;
using com.github.lhervier.ksp.shared.ugui;
using com.github.lhervier.ksp.shared.ugui.badge;
using com.github.lhervier.ksp.shared.ugui.button;
using com.github.lhervier.ksp.shared.ugui.selectionring;
using com.github.lhervier.ksp.shared.ugui.sprites;
using com.github.lhervier.ksp.shared.ugui.styles;

namespace com.github.lhervier.ksp.controlfromheremod.ui.ugui.list
{
    /// <summary>
    /// One command-module row: vessel-type icon, vessel name (+ priority tag + "Piloting" badge) over the
    /// localized part title, and the two action buttons (⚙ show PAW, ⌖ control from here). The control
    /// button is disabled on the module already in control. The background tint, the left accent bar and the
    /// selection ring are left to the <see cref="RowController"/>, which drives them from the row state.
    /// </summary>
    public class RowBuilder : IUGUIBuilder<RowController>
    {
        // No gear/crosshair glyph is guaranteed in the game SDF font; the gear has a shared sprite,
        // the others pick the first glyph the font can render.
        private static string PawGlyph => SpritesIcons.SpriteOrGlyph("paw", "⚙", "☰", "≡", "P");  // ⚙ ☰ ≡
        private static string ControlGlyph => DefaultPalette.PickGlyph("⌖", "◎", "◉", "⊕", "C"); // ⌖ ◎ ◉ ⊕
        private static string CheckGlyph => DefaultPalette.PickGlyph("✓", "√", "»");             // ✓ √

        // ===========================================
        // Builder parameters
        // ===========================================

        private CommandModuleInfo _info;
        public RowBuilder WithInfo(CommandModuleInfo info)
        {
            this._info = info;
            return this;
        }

        private ControlFromHereViewModel _viewModel;
        public RowBuilder WithViewModel(ControlFromHereViewModel viewModel)
        {
            this._viewModel = viewModel;
            return this;
        }

        // ===========================================
        // Build
        // ===========================================

        public RowController Build()
        {
            var rowGo = new GameObject("Row", typeof(RectTransform));

            // Background: left transparent here, tinted by the controller's Refresh (in control / current
            // row / hovered). raycastTarget so the empty areas of the row take the hover and the click.
            var bg = rowGo.AddComponent<Image>();
            bg.sprite = SpritesGlobal.FillSprite;
            bg.type = Image.Type.Simple;
            bg.color = Color.clear;
            bg.raycastTarget = true;

            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(
                Mathf.RoundToInt(Palette.RowPaddingH),
                Mathf.RoundToInt(Palette.RowPaddingH),
                Mathf.RoundToInt(Palette.RowPaddingV),
                Mathf.RoundToInt(Palette.RowPaddingV));
            layout.spacing = Palette.RowSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // Discreet bottom separator line (hidden on the last row by the ListController).
            GameObject separator = RowElements.BuildRowSeparator(rowGo.transform, Palette.RowSeparatorColor);

            // Left accent bar (overlaid, out of layout). Always built, shown by the controller on the
            // controlling module and on the current row.
            Image accentBar = RowElements.BuildAccentBar(rowGo.transform, DefaultPalette.AccentColor);

            // Stock vessel-type sprite from the shared module; the box stays empty when unavailable.
            RowElements.BuildIconBox(rowGo.transform, VesselTypeIcons.Get(_info.VesselType));
            BuildInfoColumn(rowGo.transform);
            BuildActions(rowGo.transform, out ButtonController pawButton, out ButtonController controlButton);

            // Selection ring, built after the content: uGUI draws children in order, so the last sibling
            // is the one drawn above the row.
            SelectionRingController selectionRing = new SelectionRingBuilder()
                .WithParent(rowGo.transform)
                .Build();

            // Hover and click on the row (PointerHandler, not EventTrigger, so the mouse wheel keeps
            // scrolling the list). The controller owns what they do.
            var pointer = rowGo.AddComponent<PointerHandler>();
            return rowGo.AddComponent<RowController>()
                .WithViewModel(_viewModel)
                .WithInfo(_info)
                .WithBackground(bg)
                .WithAccentBar(accentBar)
                .WithSelectionRing(selectionRing)
                .WithPointerHandler(pointer)
                .WithPawButton(pawButton)
                .WithControlButton(controlButton)
                .WithSeparator(separator);
        }

        // ===========================================
        // Sub-elements
        // ===========================================

        private void BuildInfoColumn(Transform parent)
        {
            var colGo = new GameObject("Info", typeof(RectTransform));
            colGo.transform.SetParent(parent, false);
            var colLe = colGo.AddComponent<LayoutElement>();
            colLe.flexibleWidth = 1f;

            var colLayout = colGo.AddComponent<VerticalLayoutGroup>();
            colLayout.padding = new RectOffset(0, 0, 0, 0);
            colLayout.spacing = 1f;
            colLayout.childAlignment = TextAnchor.MiddleLeft;
            colLayout.childControlWidth = true;
            colLayout.childControlHeight = true;
            colLayout.childForceExpandWidth = true;
            colLayout.childForceExpandHeight = false;

            // Line 1: vessel name (greedy) + priority tag + "Piloting" badge.
            var line1 = NewHLine(colGo.transform, "Line1");
            line1.GetComponent<HorizontalLayoutGroup>().spacing = DefaultPalette.Spacing;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(line1.transform, false);
            var nameLe = nameGo.AddComponent<LayoutElement>();
            // Not greedy: the name takes its preferred width and shrinks (ellipsis) only when the
            // name + chips no longer fit. The trailing flexible spacer (below) eats the remaining
            // width instead, keeping the chips glued to the name on the left.
            nameLe.flexibleWidth = 0f;
            var name = UGUILabels.AddLabel(nameGo);
            name.text = _info.VesselName;
            name.fontSize = Palette.NameFontSize;
            name.color = Palette.NameColor;
            name.alignment = TextAlignmentOptions.Left;
            name.overflowMode = TextOverflowModes.Ellipsis;

            if (_info.NamingPriority > 0)
            {
                BuildPriorityTag(line1.transform);
            }
            // Orientation chip, only for parts exposing more than one control point (null otherwise).
            if (!string.IsNullOrEmpty(_info.ControlPointLabel))
            {
                BuildControlPointTag(line1.transform);
            }
            if (_info.IsActive)
            {
                BuildPilotingBadge(line1.transform);
            }
            // "✓ Aligné" chip on a suggested module while the breaker is tripped (never on the active one,
            // which is misaligned by definition — that's why it tripped).
            if (_info.IsAligned && !_info.IsActive)
            {
                BuildAlignedChip(line1.transform);
            }
            // Flexible spacer: absorbs the leftover width so name + chips stay packed to the left.
            BuildFlexibleSpacer(line1.transform);

            // Line 2: localized part title.
            var titleGo = new GameObject("PartTitle", typeof(RectTransform));
            titleGo.transform.SetParent(colGo.transform, false);
            var title = UGUILabels.AddLabel(titleGo);
            title.text = _info.PartTitle;
            title.fontSize = Palette.PartTitleFontSize;
            title.color = Palette.PartTitleColor;
            title.alignment = TextAlignmentOptions.Left;
            title.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void BuildPriorityTag(Transform parent)
        {
            new BadgeBuilder()
                .WithParent(parent)
                .WithObjectName("PriorityTag")
                .WithText("P" + _info.NamingPriority)
                .WithColors(Palette.PrioTextColor, Palette.PrioBgColor, Palette.PrioBorderColor)
                .WithBorderThickness(Palette.PrioBorderThickness)
                .WithFontSize(Palette.PrioFontSize)
                .WithPadding(Palette.PrioPaddingH, 1)
                .WithTooltip(ModLocalization.GetString("tooltipPriority"))
                .Build();
        }

        private void BuildControlPointTag(Transform parent)
        {
            new BadgeBuilder()
                .WithParent(parent)
                .WithObjectName("ControlPointTag")
                .WithText(_info.ControlPointLabel)
                .WithColors(Palette.ControlPointColor, Palette.ControlPointBgColor, Palette.ControlPointBorderColor)
                .WithBorderThickness(Palette.ControlPointBorderThickness)
                .WithFontSize(Palette.ControlPointFontSize)
                .WithPadding(Palette.ControlPointPaddingH, 1)
                .WithTooltip(ModLocalization.GetString("tooltipControlPoint"))
                .Build();
        }

        private void BuildAlignedChip(Transform parent)
        {
            new BadgeBuilder()
                .WithParent(parent)
                .WithObjectName("AlignedTag")
                .WithText(CheckGlyph + " " + ModLocalization.GetString("badgeAligned"))
                .WithColors(Palette.AlignedTextColor, Palette.AlignedBgColor, Palette.AlignedBorderColor)
                .WithBorderThickness(Palette.AlignedBorderThickness)
                .WithFontSize(Palette.AlignedFontSize)
                .WithPadding(Palette.AlignedPaddingH, 1)
                .WithTooltip(ModLocalization.GetString("tooltipAligned"))
                .Build();
        }

        private void BuildPilotingBadge(Transform parent)
        {
            RowElements.BuildPilotingBadge(parent);
        }

        // Builds the two action buttons and hands them back: the controller wires their clicks, so that
        // acting on a row also makes it the current one.
        private void BuildActions(Transform parent, out ButtonController pawButton, out ButtonController controlButton)
        {
            var groupGo = new GameObject("Actions", typeof(RectTransform));
            groupGo.transform.SetParent(parent, false);

            var layout = groupGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // "Show PAW" button.
            ButtonController paw = new ButtonBuilder()
                .WithObjectName("ShowPaw")
                .WithLabel(PawGlyph)
                .WithSize(Palette.RowButtonSize)
                .WithFontSize(Palette.RowButtonFontSize)
                .WithBackgroundColor(Palette.RowButtonBgColor)
                .WithHoverColor(Palette.RowButtonHoverColor)
                .Build();
            paw.transform.SetParent(groupGo.transform, false);
            Tooltips.Attach(paw.gameObject, ModLocalization.GetString("tooltipPaw"));
            pawButton = paw;

            // "Control from here" button (accent), disabled on the controlling module.
            ButtonController control = new ButtonBuilder()
                .WithObjectName("ControlFromHere")
                .WithLabel(ControlGlyph)
                .WithInteractableState(!_info.IsActive)
                .WithSize(Palette.RowButtonSize)
                .WithFontSize(Palette.RowButtonFontSize)
                .WithBackgroundColor(Palette.RowButtonBgColor)
                .WithHoverColor(Palette.RowButtonHoverColor)
                .WithTextColor(DefaultPalette.AccentColor)
                .Build();
            control.transform.SetParent(groupGo.transform, false);
            Tooltips.Attach(
                control.gameObject,
                ModLocalization.GetString(_info.IsActive ? "tooltipAlreadyControlling" : "tooltipControl"));
            controlButton = control;
        }

        private static void BuildFlexibleSpacer(Transform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 0f;
        }

        private static GameObject NewHLine(Transform parent, string objectName)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 0f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go;
        }
    }
}
