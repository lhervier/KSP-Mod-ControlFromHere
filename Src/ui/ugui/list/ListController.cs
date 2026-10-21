using System.Collections.Generic;
using UnityEngine;
using TMPro;
using com.github.lhervier.ksp.mod.controlfromhere.ui.styles;
using com.github.lhervier.ksp.shared;
using com.github.lhervier.ksp.shared.ugui;

namespace com.github.lhervier.ksp.mod.controlfromhere.ui.ugui.list
{
    /// <summary>
    /// Builds the command-module rows from the state held by the <see cref="ControlFromHereViewModel"/>: it
    /// reads nothing from the game itself, and rebuilds when the view model says its state changed. Skips
    /// rebuilds while the window is hidden and catches up on re-open (OnEnable).
    ///
    /// A change of current row does not rebuild anything: the rows are only asked to re-render.
    /// </summary>
    public class ListController : MonoBehaviour
    {
        private static readonly ModLogger LOGGER = new ModLogger("List");

        // The rows currently mounted, to re-render them without rebuilding. At most one off-list row, by
        // construction (it stands for the single control point of the vessel).
        private readonly List<RowController> _rows = new List<RowController>();
        private OffListRowController _offListRow;

        private ControlFromHereViewModel _viewModel;
        public ListController WithViewModel(ControlFromHereViewModel viewModel)
        {
            this._viewModel = viewModel;
            return this;
        }

        public void Start()
        {
            if (_viewModel != null)
            {
                _viewModel.OnAvailableModulesChanged.Add(OnAvailableModulesChanged);
                _viewModel.OnSelectedPartChanged.Add(RefreshAll);
            }
            Rebuild();
        }

        public void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.OnAvailableModulesChanged.Remove(OnAvailableModulesChanged);
                _viewModel.OnSelectedPartChanged.Remove(RefreshAll);
            }
        }

        // Catch up on the state that may have changed while the window was hidden.
        public void OnEnable()
        {
            Rebuild();
        }

        // ============================================
        // Methods bound to events
        // ============================================
        // Both instance methods (never make them static): EventVoid.Add throws on a handler with a null
        // Target.

        private void OnAvailableModulesChanged()
        {
            // Rebuilding a hidden window would be wasted: OnEnable catches up on re-open.
            if (!isActiveAndEnabled)
            {
                return;
            }
            Rebuild();
        }

        private void RefreshAll()
        {
            // No point re-rendering a hidden window: OnEnable rebuilds it on re-open.
            if (!isActiveAndEnabled)
            {
                return;
            }
            if (_offListRow != null)
            {
                _offListRow.Refresh();
            }
            foreach (RowController row in _rows)
            {
                if (row != null)
                {
                    row.Refresh();
                }
            }
        }

        // =======================================
        // Internal helpers
        // =======================================

        private void Rebuild()
        {
            // OnEnable runs synchronously from AddComponent, i.e. before the builder could inject the view
            // model: there is nothing to read yet. Start rebuilds right after, so this pass is skipped.
            if (_viewModel == null)
            {
                return;
            }

            // Forget the previous rows before destroying them, so a refresh triggered in between never
            // reaches a controller whose GameObject is on its way out.
            _rows.Clear();
            _offListRow = null;

            // Clear existing children.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            if (!_viewModel.HasActiveVessel)
            {
                BuildEmptyState(ModLocalization.GetString("labelNoVessel"));
                return;
            }

            OffListControlInfo offList = _viewModel.OffListControl;
            IReadOnlyList<CommandModuleInfo> modules = _viewModel.AvailableModules;

            // Pinned at the top of the list, so it is added first.
            if (offList != null)
            {
                _offListRow = new OffListRowBuilder()
                    .WithViewModel(_viewModel)
                    .WithInfo(offList)
                    .Build();
                _offListRow.transform.SetParent(transform, false);
            }

            if (modules.Count == 0)
            {
                BuildEmptyState(ModLocalization.GetString("labelNoCommandModules"));
                RefreshAll();   // the off-list row, if any, still has to be painted
                return;
            }

            // Separate the pinned off-list row from the modules the player can actually switch to.
            if (offList != null)
            {
                BuildSeparator(ModLocalization.GetString("labelAvailableModules"));
            }

            RowController lastRow = null;
            foreach (CommandModuleInfo info in modules)
            {
                lastRow = new RowBuilder()
                    .WithViewModel(_viewModel)
                    .WithInfo(info)
                    .Build();
                lastRow.transform.SetParent(transform, false);
                _rows.Add(lastRow);
            }
            // No trailing separator under the last row of the list (mirrors the mockup's :last-child rule).
            lastRow.HideSeparator();

            // The rows render themselves on Start, which Unity only runs later in the frame: paint them now
            // so a rebuild never shows a frame of unstyled rows.
            RefreshAll();

            LOGGER.LogDebug("Rebuilt list: " + modules.Count + " row(s)"
                + (offList != null ? " + off-list control (" + offList.Status + ")" : ""));
        }

        // A thin caption row introducing the switchable modules under the pinned off-list row.
        private void BuildSeparator(string message)
        {
            var go = new GameObject("Separator", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var layout = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset(
                Mathf.RoundToInt(Palette.RowPaddingH),
                Mathf.RoundToInt(Palette.RowPaddingH),
                Mathf.RoundToInt(Palette.SeparatorPaddingV),
                Mathf.RoundToInt(Palette.SeparatorPaddingV));
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = UGUILabels.AddLabel(labelGo);
            label.text = message.ToUpperInvariant();
            label.fontSize = Palette.SeparatorFontSize;
            label.color = Palette.SeparatorTextColor;
            label.alignment = TextAlignmentOptions.Left;
        }

        private void BuildEmptyState(string message)
        {
            var go = new GameObject("Empty", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var layout = go.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.padding = new RectOffset(
                Mathf.RoundToInt(Palette.EmptyPaddingH),
                Mathf.RoundToInt(Palette.EmptyPaddingH),
                Mathf.RoundToInt(Palette.EmptyPaddingV),
                Mathf.RoundToInt(Palette.EmptyPaddingV));
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var label = UGUILabels.AddLabel(labelGo);
            label.text = message;
            label.fontSize = Palette.EmptyFontSize;
            label.color = Palette.EmptyTextColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
        }
    }
}
