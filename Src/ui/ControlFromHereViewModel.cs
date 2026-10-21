using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using com.github.lhervier.ksp.mod.controlfromhere.breaker;
using com.github.lhervier.ksp.shared;

namespace com.github.lhervier.ksp.mod.controlfromhere.ui
{
    /// <summary>
    /// View model of the window: it owns what the list displays, which row is the current one, and the
    /// actions the rows trigger. The uGUI layer holds no state of its own — it renders this one and
    /// delegates every action here.
    ///
    /// Reading the vessel lives here rather than in each widget: the list and the count sub-header used to
    /// enumerate the command modules separately, with their own GameEvents subscriptions. There is now a
    /// single read, a single set of subscriptions, and a single place where the awkward event ordering of
    /// <see cref="GameEvents.onVesselReferenceTransformSwitch"/> is dealt with.
    ///
    /// The current row is memorized as a <see cref="Part"/> rather than as a CommandModuleInfo: those are
    /// rebuilt from scratch on every update, whereas the part is the stable identity behind a row — and it
    /// is what both row kinds have in common (a command module and the off-list control point).
    ///
    /// Lives on the mod's flight addon GameObject, not on the window: the state therefore survives KSP
    /// destroying the window (Escape) and the player reopening it. It is deliberately not persisted —
    /// a new flight scene opens with no current row.
    /// </summary>
    public class ControlFromHereViewModel : MonoBehaviour
    {
        private static readonly ModLogger LOGGER = new ModLogger("ViewModel");

        // Guards against stacking update coroutines when several events fire in the same frame.
        private bool _updateQueued;

        // Whether we are subscribed to the breaker (its instance may not exist yet at our Start).
        private bool _breakerSubscribed;

        // ======================================================
        // Displayed state
        // ======================================================

        /// <summary>
        /// The command modules of the active vessel, in display order. Empty when there is no active vessel,
        /// or when it carries no command module.
        /// </summary>
        public IReadOnlyList<CommandModuleInfo> AvailableModules => _availableModules;
        private List<CommandModuleInfo> _availableModules = new List<CommandModuleInfo>();

        /// <summary>
        /// The vessel's control point when it is <em>not</em> one of <see cref="AvailableModules"/> — a
        /// docking port, a seat... or nothing at all. Null when the vessel is controlled from a command
        /// module, which is the ordinary case.
        /// </summary>
        public OffListControlInfo OffListControl { get; private set; }

        /// <summary>
        /// Whether there is an active vessel to read at all. Distinct from an empty
        /// <see cref="AvailableModules"/>: the list tells the two apart ("no vessel" vs "this vessel has no
        /// command module").
        /// </summary>
        public bool HasActiveVessel { get; private set; }

        /// <summary>Fired whenever the displayed state above has been recomputed.</summary>
        public readonly EventVoid OnAvailableModulesChanged = new EventVoid("ControlFromHere.OnAvailableModulesChanged");

        // ======================================================
        // Current row
        // ======================================================

        /// <summary>
        /// The part of the current row, null when no row is current. Assigning a part that has since been
        /// destroyed amounts to clearing the selection (a destroyed Part compares equal to null).
        /// </summary>
        public Part SelectedPart
        {
            get => _selectedPart;
            set
            {
                if (_selectedPart == value)
                {
                    return;
                }
                _selectedPart = value;
                OnSelectedPartChanged.Fire();
            }
        }
        private Part _selectedPart;

        /// <summary>Fired whenever the current row changes.</summary>
        public readonly EventVoid OnSelectedPartChanged = new EventVoid("ControlFromHere.OnSelectedPartChanged");

        /// <summary>
        /// Whether the given part is the current row. False on a null part, so a row that stands for no
        /// part (the uncontrolled vessel) is never marked as current.
        /// </summary>
        public bool IsSelected(Part part)
        {
            return part != null && _selectedPart == part;
        }

        // ======================================================
        // Lifecycle
        // ======================================================

        public void Start()
        {
            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWasModified.Add(OnVesselWasModified);
            GameEvents.onVesselReferenceTransformSwitch.Add(OnReferenceTransformSwitch);
            StartCoroutine(SubscribeBreakerWhenAvailable());
            UpdateAvailableModules();
        }

        public void OnDestroy()
        {
            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselWasModified.Remove(OnVesselWasModified);
            GameEvents.onVesselReferenceTransformSwitch.Remove(OnReferenceTransformSwitch);
            if (_breakerSubscribed && ThrustBreaker.Instance != null)
            {
                ThrustBreaker.Instance.OnStateChanged.Remove(OnBreakerStateChanged);
            }
            _breakerSubscribed = false;
        }

        // ======================================================
        // Methods bound to events
        // ======================================================

        // All instance methods (never make them static): EventVoid/EventData throw on a handler with a
        // null Target.
        private void OnVesselChange(Vessel vessel) => QueueUpdate();
        private void OnVesselWasModified(Vessel vessel) => QueueUpdate();
        private void OnReferenceTransformSwitch(Transform from, Transform to) => QueueUpdate();
        private void OnBreakerStateChanged() => QueueUpdate();

        // ======================================================
        // Update of the displayed state
        // ======================================================

        /// <summary>
        /// Recomputes the displayed state from the active vessel and the breaker, resolves the current row
        /// against it, then fires <see cref="OnAvailableModulesChanged"/>.
        /// </summary>
        public void UpdateAvailableModules()
        {
            _updateQueued = false;

            Vessel vessel = FlightGlobals.ActiveVessel;
            HasActiveVessel = vessel != null;

            // Non-null exactly when the toolbar icon blinks: the vessel is flown from a part we do not list,
            // or from nothing at all.
            OffListControl = CommandModulesService.GetOffListControlPoint(vessel);

            // While the breaker is tripped, rank modules by alignment with the frozen thrust direction, so
            // the ones worth taking control from bubble up.
            ThrustBreaker breaker = ThrustBreaker.Instance;
            Vector3? frozenThrust = breaker != null ? breaker.TripThrustDirection : null;
            float tolerance = breaker != null ? breaker.Threshold : 0f;
            _availableModules = CommandModulesService.GetCommandModules(vessel, frozenThrust, tolerance);

            PruneSelection();

            OnAvailableModulesChanged.Fire();
            LOGGER.LogDebug("Updated state: " + _availableModules.Count + " command module(s)"
                + (vessel != null ? " for " + vessel.vesselName : " (no active vessel)")
                + (OffListControl != null ? " (off-list control: " + OffListControl.Status + ")" : ""));
        }

        // Update on the next frame rather than synchronously: onVesselReferenceTransformSwitch is fired by
        // Vessel.SetReferenceTransform *before* it updates referenceTransformPart, so an immediate read
        // would see the old control point (and miss the switch to a docking port).
        private void QueueUpdate()
        {
            if (_updateQueued)
            {
                return;
            }
            _updateQueued = true;
            StartCoroutine(UpdateNextFrame());
        }

        private IEnumerator UpdateNextFrame()
        {
            yield return null;
            UpdateAvailableModules();   // clears _updateQueued
        }

        // Drops the current row as soon as the part behind it is no longer displayed — a vessel change, an
        // undocking, a destroyed part. Memorizing the selection as a part is precisely what allows this
        // resolution, the info objects being rebuilt on every update.
        private void PruneSelection()
        {
            Part selected = _selectedPart;
            if (selected == null)
            {
                return;
            }
            if (OffListControl != null && OffListControl.Part == selected)
            {
                return;
            }
            foreach (CommandModuleInfo info in _availableModules)
            {
                if (info.Part == selected)
                {
                    return;
                }
            }
            SelectedPart = null;
        }

        // KSP gives no ordering guarantee between addons, so the breaker's instance may not exist yet when
        // we start. Waiting for it beats polling every frame: a missed subscription would mean a trip that
        // never reorders the list.
        private IEnumerator SubscribeBreakerWhenAvailable()
        {
            while (ThrustBreaker.Instance == null)
            {
                yield return null;
            }
            if (!_breakerSubscribed)
            {
                ThrustBreaker.Instance.OnStateChanged.Add(OnBreakerStateChanged);
                _breakerSubscribed = true;
            }
        }

        // ======================================================
        // Actions
        // ======================================================

        /// <summary>
        /// Open the part action window of the given command module, and make its row the current one.
        /// </summary>
        /// <param name="info">The command module whose PAW must be opened</param>
        public void ShowPaw(CommandModuleInfo info)
        {
            if (info == null)
            {
                return;
            }
            SelectedPart = info.Part;
            CommandModulesService.ShowPaw(info);
        }

        /// <summary>
        /// Open the part action window of the off-list control point, and make its row the current one.
        /// No-op when the vessel is controlled by nothing: there is no part to show a PAW for.
        /// </summary>
        /// <param name="info">The off-list control point whose PAW must be opened</param>
        public void ShowPaw(OffListControlInfo info)
        {
            if (info == null || info.Part == null)
            {
                return;
            }
            SelectedPart = info.Part;
            CommandModulesService.ShowPaw(info.Part);
        }

        /// <summary>
        /// Make the given command module the vessel's control point, and its row the current one.
        /// </summary>
        /// <param name="info">The command module to take control from</param>
        public void ControlFromHere(CommandModuleInfo info)
        {
            if (info == null)
            {
                return;
            }
            // Selected first: taking control switches the reference transform, which recomputes the list —
            // the selection is what lets the player still see which row they acted on.
            SelectedPart = info.Part;
            CommandModulesService.ControlFromHere(info);
        }
    }
}
