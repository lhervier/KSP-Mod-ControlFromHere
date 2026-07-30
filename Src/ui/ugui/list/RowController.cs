using UnityEngine;
using UnityEngine.UI;
using com.github.lhervier.ksp.controlfromheremod.ui.styles;
using com.github.lhervier.ksp.shared.ugui;
using com.github.lhervier.ksp.shared.ugui.button;
using com.github.lhervier.ksp.shared.ugui.styles;

namespace com.github.lhervier.ksp.controlfromheremod.ui.ugui.list
{
    /// <summary>
    /// Controller of a command-module row. The row content is static (rebuilt by the
    /// <see cref="ListController"/> whenever the vessel or its control point changes), but its rendering —
    /// background tint and left accent bar — depends on the row state (in control, current row, hovered)
    /// and is re-evaluated by <see cref="Refresh"/>.
    ///
    /// Clicking anywhere on the row makes it the current one; the two action buttons are relayed to the
    /// <see cref="ControlFromHereViewModel"/>, which marks the row as part of the action.
    /// </summary>
    public class RowController : MonoBehaviour
    {
        private bool _hovered;

        // ===========================================
        // Life cycle
        // ===========================================

        private ControlFromHereViewModel _viewModel;
        public RowController WithViewModel(ControlFromHereViewModel viewModel)
        {
            _viewModel = viewModel;
            return this;
        }

        private CommandModuleInfo _info;
        public RowController WithInfo(CommandModuleInfo info)
        {
            _info = info;
            return this;
        }

        private Image _bg;
        public RowController WithBackground(Image bg)
        {
            _bg = bg;
            return this;
        }

        private Image _accentBar;
        public RowController WithAccentBar(Image accentBar)
        {
            _accentBar = accentBar;
            return this;
        }

        private PointerHandler _pointer;
        public RowController WithPointerHandler(PointerHandler pointer)
        {
            _pointer = pointer;
            return this;
        }

        private ButtonController _pawButton;
        public RowController WithPawButton(ButtonController pawButton)
        {
            _pawButton = pawButton;
            return this;
        }

        private ButtonController _controlButton;
        public RowController WithControlButton(ButtonController controlButton)
        {
            _controlButton = controlButton;
            return this;
        }

        /// <summary>Registers the row's bottom separator line so the <see cref="ListController"/> can
        /// hide it on the last row (no trailing line under the list).</summary>
        public RowController WithSeparator(GameObject separator)
        {
            _separator = separator;
            return this;
        }
        private GameObject _separator;

        public void Start()
        {
            Refresh();

            if (_pointer != null)
            {
                _pointer.OnEnter = OnEnter;
                _pointer.OnExit = OnExit;
                _pointer.OnClick = OnRowClick;
            }
            if (_pawButton != null)
            {
                _pawButton.OnClick.Add(OnPawButton);
            }
            if (_controlButton != null)
            {
                _controlButton.OnClick.Add(OnControlButton);
            }
        }

        public void OnDestroy()
        {
            if (_pointer != null)
            {
                _pointer.OnEnter = null;
                _pointer.OnExit = null;
                _pointer.OnClick = null;
            }
            if (_pawButton != null)
            {
                _pawButton.OnClick.Remove(OnPawButton);
            }
            if (_controlButton != null)
            {
                _controlButton.OnClick.Remove(OnControlButton);
            }
        }

        // =============================================
        // Methods bound to events
        // =============================================

        private void OnEnter()
        {
            _hovered = true;
            Refresh();
        }

        private void OnExit()
        {
            _hovered = false;
            Refresh();
        }

        // Clicking the row only makes it current. The action buttons are children of the row but consume
        // their own click, so they never reach this handler — they go through the view model, which marks
        // the row as part of the action.
        private void OnRowClick()
        {
            _viewModel.SelectedPart = _info.Part;
        }

        private void OnPawButton() => _viewModel.ShowPaw(_info);

        private void OnControlButton() => _viewModel.ControlFromHere(_info);

        // =====================================
        // Public API
        // =====================================

        /// <summary>Hides the bottom separator line (used on the last row of the list).</summary>
        public void HideSeparator()
        {
            if (_separator != null)
            {
                _separator.SetActive(false);
            }
        }

        /// <summary>Re-evaluates the row rendering from the current state (in control, current row, hovered).
        /// Called by the <see cref="ListController"/> on every state change, and once on Start.</summary>
        public void Refresh()
        {
            bool active = _info.IsActive;
            bool selected = _viewModel.IsSelected(_info.Part);

            // Background, by decreasing priority. The controlling module keeps its green tint on hover (as
            // in the mockup), so the hover wash only ever shows on the other rows.
            if (_bg != null)
            {
                if (active) _bg.color = Palette.RowActiveBgColor;
                else if (selected) _bg.color = Palette.RowSelectedBgColor;
                else if (_hovered) _bg.color = Palette.RowHoverColor;
                else _bg.color = Color.clear;
            }

            // Left accent bar: full accent on the module in control, dimmed accent when the row is merely
            // the current one — the two are never confused.
            if (_accentBar != null)
            {
                _accentBar.enabled = active || selected;
                _accentBar.color = active ? DefaultPalette.AccentColor : DefaultPalette.AccentBorderColor;
            }
        }
    }
}
