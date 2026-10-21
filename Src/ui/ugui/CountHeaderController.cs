using UnityEngine;
using TMPro;
using com.github.lhervier.ksp.shared;

namespace com.github.lhervier.ksp.mod.controlfromhere.ui.ugui
{
    /// <summary>
    /// Keeps the "Command modules: X" caption in sync with the state held by the
    /// <see cref="ControlFromHereViewModel"/> — the same list the rows are built from, so the count can
    /// never disagree with what is displayed below it.
    /// </summary>
    public class CountHeaderController : MonoBehaviour
    {
        private TextMeshProUGUI _label;
        public CountHeaderController WithLabel(TextMeshProUGUI label)
        {
            _label = label;
            return this;
        }

        private ControlFromHereViewModel _viewModel;
        public CountHeaderController WithViewModel(ControlFromHereViewModel viewModel)
        {
            _viewModel = viewModel;
            return this;
        }

        public void Start()
        {
            if (_viewModel != null)
            {
                _viewModel.OnAvailableModulesChanged.Add(Refresh);
            }
            Refresh();
        }

        public void OnDestroy()
        {
            if (_viewModel != null)
            {
                _viewModel.OnAvailableModulesChanged.Remove(Refresh);
            }
        }

        // Catch up on the count that may have changed while the window was hidden.
        public void OnEnable()
        {
            Refresh();
        }

        // Instance method (never make it static): EventVoid.Add throws on a handler with a null Target.
        private void Refresh()
        {
            if (_label == null || _viewModel == null)
            {
                return;
            }
            int count = _viewModel.AvailableModules.Count;
            _label.text = ModLocalization.GetString("labelCommandModulesCount", count).ToUpperInvariant();
        }
    }
}
