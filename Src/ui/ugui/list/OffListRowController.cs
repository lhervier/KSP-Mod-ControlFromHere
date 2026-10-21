using UnityEngine;
using UnityEngine.UI;
using com.github.lhervier.ksp.mod.controlfromhere.ui.styles;
using com.github.lhervier.ksp.shared.ugui;
using com.github.lhervier.ksp.shared.ugui.button;
using com.github.lhervier.ksp.shared.ugui.selectionring;

namespace com.github.lhervier.ksp.mod.controlfromhere.ui.ugui.list
{
    /// <summary>
    /// Controller of the off-list control-point row (pinned at the top of the list). Its content is static
    /// (rebuilt by the <see cref="ListController"/> whenever the vessel or its control point changes); like
    /// <see cref="RowController"/> it renders the "current row" state through <see cref="Refresh"/>, and
    /// relays its ⚙ button to the <see cref="ControlFromHereViewModel"/>.
    ///
    /// The row keeps its amber code throughout: its accent bar is always on (it is what controls the
    /// vessel) and being the current row only deepens its background. A vessel controlled by nothing has no
    /// part to act on: the row is then neither clickable nor selectable.
    /// </summary>
    public class OffListRowController : MonoBehaviour
    {
        // ===========================================
        // Life cycle
        // ===========================================

        private ControlFromHereViewModel _viewModel;
        public OffListRowController WithViewModel(ControlFromHereViewModel viewModel)
        {
            _viewModel = viewModel;
            return this;
        }

        private OffListControlInfo _info;
        public OffListRowController WithInfo(OffListControlInfo info)
        {
            _info = info;
            return this;
        }

        private Image _bg;
        public OffListRowController WithBackground(Image bg)
        {
            _bg = bg;
            return this;
        }

        private Image _accentBar;
        public OffListRowController WithAccentBar(Image accentBar)
        {
            _accentBar = accentBar;
            return this;
        }

        private SelectionRingController _selectionRing;
        public OffListRowController WithSelectionRing(SelectionRingController selectionRing)
        {
            _selectionRing = selectionRing;
            return this;
        }

        // Null when the vessel is uncontrolled: there is no part to make current.
        private PointerHandler _pointer;
        public OffListRowController WithPointerHandler(PointerHandler pointer)
        {
            _pointer = pointer;
            return this;
        }

        private ButtonController _pawButton;
        public OffListRowController WithPawButton(ButtonController pawButton)
        {
            _pawButton = pawButton;
            return this;
        }

        public void Start()
        {
            Refresh();

            if (_pointer != null)
            {
                _pointer.OnClick = OnRowClick;
            }
            if (_pawButton != null)
            {
                _pawButton.OnClick.Add(OnPawButton);
            }
        }

        public void OnDestroy()
        {
            if (_pointer != null)
            {
                _pointer.OnClick = null;
            }
            if (_pawButton != null)
            {
                _pawButton.OnClick.Remove(OnPawButton);
            }
        }

        // =============================================
        // Methods bound to events
        // =============================================

        private void OnRowClick()
        {
            _viewModel.SelectedPart = _info.Part;
        }

        private void OnPawButton() => _viewModel.ShowPaw(_info);

        // =====================================
        // Public API
        // =====================================

        /// <summary>Re-evaluates the row rendering from the current state (current row or not). Called by the
        /// <see cref="ListController"/> on every state change, and once on Start.</summary>
        public void Refresh()
        {
            bool selected = _viewModel.IsSelected(_info.Part);

            if (_bg != null)
            {
                _bg.color = selected ? Palette.OffListRowSelectedBgColor : Palette.OffListRowBgColor;
            }

            // Always shown: the off-list part is the one flying the vessel. Selection does not change it.
            if (_accentBar != null)
            {
                _accentBar.enabled = true;
            }

            // Selection ring: the row's own amber code already owns the background and the accent bar, so
            // the deepened tint alone is a faint cue. The ring gives the selection a channel of its own.
            if (_selectionRing != null)
            {
                _selectionRing.SetVisible(selected);
            }
        }
    }
}
