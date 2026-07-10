using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using uOSC;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class CanvasUiSync : MonoBehaviour
    {
        internal const float RuntimeHierarchyRescanIntervalSeconds = 0.1f;
        internal const float RuntimeHierarchyRescanMaxIntervalSeconds = 0.5f;
        internal const float PendingRemoteCommitTimeoutSeconds = 30f;
        internal const string HelloAddress = "/uisync/hello";
        internal const string RequestSnapshotAddress = "/uisync/requestSnapshot";
        internal const string BeginSnapshotAddress = "/uisync/beginSnapshot";
        internal const string SnapshotStateAddress = "/uisync/snapshotState";
        internal const string EndSnapshotAddress = "/uisync/endSnapshot";
        internal const string ProposeStateAddress = "/uisync/proposeState";
        internal const string CommitStateAddress = "/uisync/commitState";
        internal const string ProposeButtonAddress = "/uisync/proposeButton";
        internal const string CommitButtonAddress = "/uisync/commitButton";
        internal const string TransportHostName = "__CanvasUiSyncTransport";

        [SerializeField] internal CanvasUiSyncProfile profile;
        [SerializeField] internal string canvasIdOverride = string.Empty;
        [SerializeField] internal bool rescanOnEnable;
        [SerializeField] internal bool syncEnabled = true;
        [SerializeField, HideInInspector] internal List<Component> excludedComponents = new List<Component>();
        [SerializeField] internal List<CanvasUiSyncExclusion> excludedUi = new List<CanvasUiSyncExclusion>();

        internal readonly Dictionary<string, UiSyncBinding> bindings = new Dictionary<string, UiSyncBinding>();
        internal readonly List<UiSyncBinding> continuousBindings = new List<UiSyncBinding>();
        internal readonly List<UiSyncBinding> polledBindings = new List<UiSyncBinding>();
        internal readonly Dictionary<string, NodeState> nodes = new Dictionary<string, NodeState>();
        internal readonly Dictionary<string, LocalStateRecord> localStates = new Dictionary<string, LocalStateRecord>();
        internal readonly Dictionary<string, StateStamp> latestAppliedButtonStamps = new Dictionary<string, StateStamp>();
        internal readonly Dictionary<string, object> lastProposedValues = new Dictionary<string, object>();
        internal readonly Dictionary<string, float> lastContinuousProposedValues = new Dictionary<string, float>();
        internal readonly Dictionary<string, float> lastProposeTimes = new Dictionary<string, float>();
        internal readonly Dictionary<string, DeferredStateCommit> deferredCommits = new Dictionary<string, DeferredStateCommit>();
        internal readonly Dictionary<string, DeferredStateCommit> pendingRemoteCommits = new Dictionary<string, DeferredStateCommit>();
        internal readonly Dictionary<string, PendingButtonCommit> pendingRemoteButtonCommits = new Dictionary<string, PendingButtonCommit>();
        internal readonly Dictionary<string, float> activeSnapshotIds = new Dictionary<string, float>();
        internal readonly Dictionary<string, bool> activeSnapshotCanInitializeLocalState = new Dictionary<string, bool>();
        internal readonly Dictionary<string, SnapshotReceiveState> snapshotReceiveStates = new Dictionary<string, SnapshotReceiveState>();
        internal readonly Dictionary<string, int> latestSnapshotSequenceBySourceSession = new Dictionary<string, int>();
        internal readonly Dictionary<string, string> ignoredPeerSessions = new Dictionary<string, string>(StringComparer.Ordinal);
        internal readonly List<string> stateCacheKeysToRemove = new List<string>();
        internal readonly List<string> expiredSnapshotIds = new List<string>();
        internal readonly List<string> snapshotIdScratch = new List<string>();
        internal readonly List<string> expiredNodeIds = new List<string>();
        internal readonly List<string> bindingKeyScratch = new List<string>();
        internal readonly Dictionary<Transform, string> pathCacheScratch = new Dictionary<Transform, string>();
        internal readonly Dictionary<Transform, int> pathHashCacheScratch = new Dictionary<Transform, int>();
        internal readonly List<string> pathSegmentScratch = new List<string>();
        internal readonly List<Toggle> toggleScratch = new List<Toggle>();
        internal readonly List<Toggle> dropdownItemToggleScratch = new List<Toggle>();
        internal readonly List<Slider> sliderScratch = new List<Slider>();
        internal readonly List<Scrollbar> scrollbarScratch = new List<Scrollbar>();
        internal readonly List<Dropdown> dropdownScratch = new List<Dropdown>();
        internal readonly List<TMP_Dropdown> tmpDropdownScratch = new List<TMP_Dropdown>();
        internal readonly List<InputField> inputFieldScratch = new List<InputField>();
        internal readonly List<TMP_InputField> tmpInputFieldScratch = new List<TMP_InputField>();
        internal readonly List<Button> buttonScratch = new List<Button>();
        internal readonly List<Transform> dropdownTemplateRootScratch = new List<Transform>();
        internal readonly List<Transform> dropdownRuntimeRootScratch = new List<Transform>();
        internal readonly Dictionary<Dropdown, GameObject> dropdownRuntimeRootCache = new Dictionary<Dropdown, GameObject>();
        internal readonly Dictionary<TMP_Dropdown, GameObject> tmpDropdownRuntimeRootCache = new Dictionary<TMP_Dropdown, GameObject>();
        internal readonly CanvasUiSyncBindingsService.BindingScanContext bindingScanContext = new CanvasUiSyncBindingsService.BindingScanContext();
        internal readonly StringBuilder stringBuilderScratch = new StringBuilder(256);
        internal string registryHash = string.Empty;
        internal string fullRegistryHash = string.Empty;
        internal string canvasId = string.Empty;
        internal string sessionId = string.Empty;
        internal long sessionStartedAtTicks;
        internal float sessionStartedAtRealtime;
        internal float nextHelloTime;
        internal float nextSnapshotRequestTime;
        internal float nextPeriodicResyncTime;
        internal float nextStatisticsLogTime;
        internal float nextHierarchyRescanTime;
        internal float currentHierarchyRescanIntervalSeconds = RuntimeHierarchyRescanIntervalSeconds;
        internal float nextPendingCommitTime = float.PositiveInfinity;
        internal float snapshotCooldownUntil;
        internal float acceptRequestedSnapshotFromNewerPeerUntil;
        internal int snapshotRetryCount;
        internal int snapshotSequence;
        internal int suppressionCount;
        internal int localSequence;
        internal long logicalTicks;
        internal int sentMessageCount;
        internal int receivedMessageCount;
        internal int sentValueCount;
        internal int receivedValueCount;
        internal long sentApproxBytes;
        internal long receivedApproxBytes;
        internal int lastGcCollectionCount0;
        internal int lastGcCollectionCount1;
        internal int lastGcCollectionCount2;
        internal int bindingHierarchySignature;
        internal bool initialized;
        internal bool hasSnapshot;
        internal bool acceptRequestedSnapshotFromNewerPeer;
        internal bool resumeSynchronizationOnEnable;
        internal bool transportListenerSubscribed;
        internal bool hasConfiguredExclusions;
        internal uOscServer server;
        internal uOscClient client;
        internal Canvas canvasComponent;
        internal GameObject transportHost;

        public bool SyncEnabled => syncEnabled;

        internal sealed class NodeState
        {
            public NodeState(string nodeId, string sessionId, float lastSeenAt, long sessionStartedAtTicks = 0L, float sessionUptimeSeconds = -1f)
            {
                NodeId = nodeId;
                SessionId = sessionId;
                LastSeenAt = lastSeenAt;
                SessionStartedAtTicks = sessionStartedAtTicks;
                SessionUptimeSeconds = sessionUptimeSeconds;
            }

            public string NodeId { get; }
            public string SessionId { get; set; }
            public float LastSeenAt { get; set; }
            public long SessionStartedAtTicks { get; set; }
            public float SessionUptimeSeconds { get; set; }
            public string RegistryHash { get; set; } = string.Empty;
            public string FullRegistryHash { get; set; } = string.Empty;
            public bool HasExclusions { get; set; }
        }

        internal sealed class SnapshotReceiveState
        {
            public SnapshotReceiveState(string sourceNodeId, string sourceSessionId, bool canInitializeLocalState, int expectedStateCount, string remoteRegistryHash, bool remoteHasExclusions, string remoteFullRegistryHash)
            {
                SourceNodeId = sourceNodeId;
                SourceSessionId = sourceSessionId;
                CanInitializeLocalState = canInitializeLocalState;
                ExpectedStateCount = expectedStateCount;
                RemoteRegistryHash = remoteRegistryHash;
                RemoteHasExclusions = remoteHasExclusions;
                RemoteFullRegistryHash = remoteFullRegistryHash;
            }

            public string SourceNodeId { get; }
            public string SourceSessionId { get; }
            public bool CanInitializeLocalState { get; }
            public int ExpectedStateCount { get; }
            public string RemoteRegistryHash { get; }
            public bool RemoteHasExclusions { get; }
            public string RemoteFullRegistryHash { get; }
            public HashSet<string> ReceivedSyncIds { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> PendingSyncIds { get; } = new HashSet<string>(StringComparer.Ordinal);
            public bool EndReceived { get; set; }
            public float CompletionDeadline { get; set; } = float.PositiveInfinity;

            public bool HasCompletePayload => EndReceived && (ExpectedStateCount < 0 || ReceivedSyncIds.Count >= ExpectedStateCount);
            public bool IsReady => HasCompletePayload && PendingSyncIds.Count == 0;

            public bool IsRegistryCompatible(string localRegistryHash, string localFullRegistryHash, bool localHasExclusions)
            {
                return string.IsNullOrEmpty(RemoteRegistryHash) || string.Equals(RemoteRegistryHash, localRegistryHash, StringComparison.Ordinal) || (RemoteHasExclusions || localHasExclusions) && !string.IsNullOrEmpty(RemoteFullRegistryHash) && string.Equals(RemoteFullRegistryHash, localFullRegistryHash, StringComparison.Ordinal);
            }
        }

        internal sealed class LocalStateRecord
        {
            public LocalStateRecord(object value, string valueType, StateStamp stamp)
            {
                Value = value;
                ValueType = valueType;
                Stamp = stamp;
                PendingValue = value;
                PendingStamp = stamp;
            }

            public object Value { get; set; }
            public string ValueType { get; }
            public StateStamp Stamp { get; set; }
            public float LastBroadcastAt { get; set; }
            public float NextBroadcastAt { get; set; }
            public bool HasPendingBroadcast { get; set; }
            public object PendingValue { get; set; }
            public StateStamp PendingStamp { get; set; }
        }

        internal readonly struct StateStamp
        {
            public StateStamp(long logicalTicks, string nodeId, int sequence)
            {
                LogicalTicks = logicalTicks;
                NodeId = nodeId;
                Sequence = sequence;
            }

            public long LogicalTicks { get; }
            public string NodeId { get; }
            public int Sequence { get; }
        }

        internal sealed class UiSyncBinding : IDisposable
        {
            public UiSyncBinding(Component component, string syncId, string valueType, Func<object> readValue, Action<object> applyValue, bool isContinuous, bool requiresPolling = false)
            {
                Component = component;
                SyncId = syncId;
                ValueType = valueType;
                this.readValue = readValue;
                this.applyValue = applyValue;
                IsContinuous = isContinuous;
                RequiresPolling = requiresPolling;
            }

            private readonly Func<object> readValue;
            private readonly Action<object> applyValue;
            private Func<int> readIntValue;
            public Component Component { get; }
            public string SyncId { get; }
            public string ValueType { get; }
            public bool IsContinuous { get; }
            public bool RequiresPolling { get; }
            public bool IsInteracting { get; set; }
            public Action Unsubscribe { get; set; }

            public object ReadValue()
            {
                if (readValue != null)
                {
                    return readValue();
                }

                return readIntValue == null ? null : readIntValue();
            }

            public UiSyncBinding WithIntReader(Func<int> readValue)
            {
                readIntValue = readValue;
                return this;
            }

            public bool TryReadIntValue(out int value)
            {
                if (readIntValue == null)
                {
                    value = 0;
                    return false;
                }

                value = readIntValue();
                return true;
            }

            public void ApplyValue(object value)
            {
                applyValue?.Invoke(value);
            }

            public void Dispose()
            {
                Unsubscribe?.Invoke();
            }
        }

        internal readonly struct DeferredStateCommit
        {
            public DeferredStateCommit(string valueType, object value, StateStamp stamp, float receivedAt = 0f, bool isSnapshot = false, bool canInitializeLocalState = false, float timeoutSeconds = PendingRemoteCommitTimeoutSeconds)
            {
                ValueType = valueType;
                Value = value;
                Stamp = stamp;
                ReceivedAt = receivedAt;
                IsSnapshot = isSnapshot;
                CanInitializeLocalState = canInitializeLocalState;
                TimeoutSeconds = timeoutSeconds;
            }

            public string ValueType { get; }
            public object Value { get; }
            public StateStamp Stamp { get; }
            public float ReceivedAt { get; }
            public bool IsSnapshot { get; }
            public bool CanInitializeLocalState { get; }
            public float TimeoutSeconds { get; }
        }

        internal readonly struct PendingButtonCommit
        {
            public PendingButtonCommit(StateStamp stamp, float receivedAt, string sourceNodeId = null, string sourceSessionId = null, bool waitForRegistryMatch = false)
            {
                Stamp = stamp;
                ReceivedAt = receivedAt;
                SourceNodeId = sourceNodeId;
                SourceSessionId = sourceSessionId;
                WaitForRegistryMatch = waitForRegistryMatch;
            }

            public StateStamp Stamp { get; }
            public float ReceivedAt { get; }
            public string SourceNodeId { get; }
            public string SourceSessionId { get; }
            public bool WaitForRegistryMatch { get; }
        }

        internal readonly struct SuppressionScope : IDisposable
        {
            private readonly CanvasUiSync owner;

            public SuppressionScope(CanvasUiSync owner)
            {
                this.owner = owner;
                owner.suppressionCount++;
            }

            public void Dispose()
            {
                owner.suppressionCount = Mathf.Max(0, owner.suppressionCount - 1);
            }
        }

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogWarning("CanvasUiSync profile is not assigned.", this);
                enabled = false;
                return;
            }

            canvasId = string.IsNullOrWhiteSpace(canvasIdOverride) ? gameObject.name : canvasIdOverride.Trim();
            sessionId = Guid.NewGuid().ToString("N");
            sessionStartedAtTicks = DateTime.UtcNow.Ticks;
            sessionStartedAtRealtime = Time.realtimeSinceStartup;
            canvasComponent = GetComponent<Canvas>();
            RefreshExclusionRules();
            InitializeTransport();
            ScanBindings();
            InitializeLocalState();
            bindingHierarchySignature = ComputeBindingHierarchySignature();
            initialized = true;
        }

        private void Start()
        {
            if (!initialized)
            {
                return;
            }

            ScheduleSynchronizationNow(Time.unscaledTime);
            lastGcCollectionCount0 = GC.CollectionCount(0);
            lastGcCollectionCount1 = GC.CollectionCount(1);
            lastGcCollectionCount2 = GC.CollectionCount(2);
            RequestSnapshotIfNeeded(true);
        }

        private void OnEnable()
        {
            if (!initialized)
            {
                return;
            }

            SubscribeTransportListener();
            if (rescanOnEnable)
            {
                ScanBindings();
                InitializeLocalState();
                bindingHierarchySignature = ComputeBindingHierarchySignature();
                ResetRuntimeHierarchyRescanSchedule(Time.unscaledTime);
            }

            if (resumeSynchronizationOnEnable && syncEnabled)
            {
                resumeSynchronizationOnEnable = false;
                AllowRequestedSnapshotFromNewerPeer();
                hasSnapshot = false;
                snapshotRetryCount = 0;
                snapshotCooldownUntil = 0f;
                ScheduleSynchronizationNow(Time.unscaledTime);
                SendHello();
                RequestSnapshotIfNeeded(true);
            }
        }

        private void OnDisable()
        {
            if (!initialized)
            {
                return;
            }

            UnsubscribeTransportListener();
            resumeSynchronizationOnEnable = syncEnabled;
            CanvasUiSyncProtocolService.CancelActiveSnapshotReception(this);
            ClearContinuousInteractionStates();
        }

        public void SetSyncEnabled(bool value)
        {
            if (syncEnabled == value)
            {
                return;
            }

            syncEnabled = value;
            if (!initialized)
            {
                return;
            }

            if (!syncEnabled)
            {
                resumeSynchronizationOnEnable = false;
                CanvasUiSyncProtocolService.CancelActiveSnapshotReception(this);
                ClearContinuousInteractionStates();
                return;
            }

            RefreshBindingsIfHierarchyChanged(true);
            AllowRequestedSnapshotFromNewerPeer();
            hasSnapshot = false;
            snapshotRetryCount = 0;
            snapshotCooldownUntil = 0f;
            ScheduleSynchronizationNow(Time.unscaledTime);
            SendHello();
            RequestSnapshotIfNeeded(true);
        }

        public void EnableSync()
        {
            SetSyncEnabled(true);
        }

        public void DisableSync()
        {
            SetSyncEnabled(false);
        }

        public void RefreshExclusions()
        {
            RefreshExclusionRules(true);
            PruneExcludedPendingCommits();
            if (!initialized)
            {
                return;
            }

            RefreshBindingsIfHierarchyChanged(true);
        }

        private void Update()
        {
            if (!initialized || !syncEnabled)
            {
                return;
            }

            var now = Time.unscaledTime;
            if (now >= nextHelloTime)
            {
                SendHello();
                nextHelloTime = now + Mathf.Max(0.1f, profile.helloIntervalSeconds);
            }

            TickSnapshotRetry(now);
            TickSnapshotCleanup(now);
            TickPeriodicResync(now);
            TickStatisticsLog(now);
            UpdateContinuousInteractions();
            UpdatePolledBindings();
            TickNodeTimeout(now);
            FlushPendingCommits(now);
            TickRuntimeHierarchyRescan(now);
        }

        private void OnDestroy()
        {
            UnsubscribeTransportListener();

            foreach (var binding in bindings.Values)
            {
                binding.Dispose();
            }
        }

        internal bool CanProcessRuntimeEvents()
        {
            return initialized && syncEnabled && isActiveAndEnabled;
        }

        internal bool CanPublishLocalEvents()
        {
            return CanProcessRuntimeEvents() && snapshotReceiveStates.Count == 0;
        }

        internal float GetSessionUptimeSeconds()
        {
            return Mathf.Max(0f, Time.realtimeSinceStartup - sessionStartedAtRealtime);
        }

        internal void AllowRequestedSnapshotFromNewerPeer()
        {
            acceptRequestedSnapshotFromNewerPeer = true;
            var retryWindowSeconds = Mathf.Max(profile.snapshotRequestIntervalSeconds, profile.snapshotRetryCooldownSeconds) * Mathf.Max(1, profile.snapshotRequestRetryCount);
            acceptRequestedSnapshotFromNewerPeerUntil = Time.unscaledTime + Mathf.Max(0.5f, profile.snapshotStateTimeoutSeconds) + Mathf.Max(0f, profile.initialSyncPendingTimeoutSeconds) + retryWindowSeconds;
        }

        internal bool CanAcceptRequestedSnapshotFromNewerPeer()
        {
            if (acceptRequestedSnapshotFromNewerPeer && Time.unscaledTime <= acceptRequestedSnapshotFromNewerPeerUntil)
            {
                return true;
            }

            ClearRequestedSnapshotFromNewerPeer();
            return false;
        }

        internal void ClearRequestedSnapshotFromNewerPeer()
        {
            acceptRequestedSnapshotFromNewerPeer = false;
            acceptRequestedSnapshotFromNewerPeerUntil = 0f;
        }

        internal void SubscribeTransportListener()
        {
            if (server == null)
            {
                transportListenerSubscribed = false;
                return;
            }

            server.onDataReceived.RemoveListener(OnOscMessageReceived);
            server.onDataReceived.AddListener(OnOscMessageReceived);
            transportListenerSubscribed = true;
        }

        internal void UnsubscribeTransportListener()
        {
            if (server != null)
            {
                server.onDataReceived.RemoveListener(OnOscMessageReceived);
            }

            transportListenerSubscribed = false;
        }

        internal void ScheduleSynchronizationNow(float now)
        {
            nextHelloTime = now;
            nextSnapshotRequestTime = now;
            nextPeriodicResyncTime = profile.periodicFullResyncIntervalSeconds > 0f ? now + profile.periodicFullResyncIntervalSeconds : float.PositiveInfinity;
            nextStatisticsLogTime = ShouldStatisticsLog() ? now + profile.statisticsLogIntervalSeconds : float.PositiveInfinity;
            ResetRuntimeHierarchyRescanSchedule(now);
        }

        internal void ClearContinuousInteractionStates()
        {
            for (var index = 0; index < continuousBindings.Count; index++)
            {
                var binding = continuousBindings[index];
                binding.IsInteracting = false;
                deferredCommits.Remove(binding.SyncId);
                if (binding.Component != null && binding.Component.TryGetComponent<CanvasUiSyncContinuousInteractionTracker>(out var tracker))
                {
                    tracker.Cancel(this, binding);
                }
            }
        }

        internal void InitializeTransport()
        {
            CanvasUiSyncTransportService.InitializeTransport(this);
        }

        internal GameObject GetOrCreateTransportHost()
        {
            return CanvasUiSyncTransportService.GetOrCreateTransportHost(this);
        }

        internal void InitializeLocalState()
        {
            CanvasUiSyncStateService.InitializeLocalState(this);
        }

        internal void ApplyPendingRemoteCommits()
        {
            var canApplyPendingSnapshotStates = CanvasUiSyncProtocolService.CanApplyPendingSnapshotStates(this);
            stateCacheKeysToRemove.Clear();
            foreach (var pair in pendingRemoteCommits)
            {
                if (IsSyncIdExcluded(pair.Key))
                {
                    CanvasUiSyncProtocolService.HandlePendingSnapshotStateApplied(this, pair.Key);
                    stateCacheKeysToRemove.Add(pair.Key);
                    continue;
                }

                if (!bindings.TryGetValue(pair.Key, out var binding))
                {
                    continue;
                }

                var pending = pair.Value;
                if (pending.IsSnapshot && !canApplyPendingSnapshotStates)
                {
                    continue;
                }

                if (!string.Equals(binding.ValueType, pending.ValueType, StringComparison.Ordinal))
                {
                    HandleTypeMismatch(pair.Key, binding.ValueType, pending.ValueType);
                    stateCacheKeysToRemove.Add(pair.Key);
                    continue;
                }

                ApplyRemoteState(pair.Key, pending.ValueType, pending.Value, pending.Stamp, pending.IsSnapshot, pending.CanInitializeLocalState);
                CanvasUiSyncProtocolService.HandlePendingSnapshotStateApplied(this, pair.Key);
                stateCacheKeysToRemove.Add(pair.Key);
            }

            foreach (var syncId in stateCacheKeysToRemove)
            {
                pendingRemoteCommits.Remove(syncId);
            }

            stateCacheKeysToRemove.Clear();
        }

        internal void ApplyPendingRemoteButtonCommits()
        {
            if (pendingRemoteButtonCommits.Count == 0)
            {
                return;
            }

            stateCacheKeysToRemove.Clear();
            foreach (var pair in pendingRemoteButtonCommits)
            {
                if (IsSyncIdExcluded(pair.Key))
                {
                    stateCacheKeysToRemove.Add(pair.Key);
                    continue;
                }

                if (CanvasUiSyncProtocolService.ShouldDiscardPendingButtonCommit(this, pair.Value))
                {
                    CanvasUiSyncProtocolService.HandlePendingSnapshotStateApplied(this, pair.Key);
                    stateCacheKeysToRemove.Add(pair.Key);
                    continue;
                }

                if (!CanvasUiSyncProtocolService.CanApplyPendingButtonCommit(this, pair.Value))
                {
                    continue;
                }

                if (!bindings.ContainsKey(pair.Key))
                {
                    continue;
                }

                if (!bindings.TryGetValue(pair.Key, out var binding))
                {
                    continue;
                }

                ApplyButtonCommit(binding, pair.Key, pair.Value.Stamp);
                CanvasUiSyncProtocolService.HandlePendingSnapshotStateApplied(this, pair.Key);
                stateCacheKeysToRemove.Add(pair.Key);
            }

            foreach (var syncId in stateCacheKeysToRemove)
            {
                pendingRemoteButtonCommits.Remove(syncId);
            }

            stateCacheKeysToRemove.Clear();
        }

        internal void PruneTransientStateCaches()
        {
            RemoveMissingBindingState(lastProposedValues);
            RemoveMissingBindingState(lastContinuousProposedValues);
            RemoveMissingBindingState(lastProposeTimes);
            RemoveMissingBindingState(deferredCommits);
            PruneExcludedPendingCommits();
            PrunePendingRemoteCommits();
        }

        internal void PrunePendingRemoteCommits()
        {
            if (pendingRemoteCommits.Count == 0 && pendingRemoteButtonCommits.Count == 0)
            {
                return;
            }

            var now = Time.unscaledTime;
            stateCacheKeysToRemove.Clear();
            foreach (var pair in pendingRemoteCommits)
            {
                if (pair.Value.IsSnapshot && IsSnapshotStatePending(pair.Key))
                {
                    continue;
                }

                if (now - pair.Value.ReceivedAt <= Mathf.Max(0f, pair.Value.TimeoutSeconds))
                {
                    continue;
                }

                stateCacheKeysToRemove.Add(pair.Key);
            }

            foreach (var syncId in stateCacheKeysToRemove)
            {
                pendingRemoteCommits.Remove(syncId);
            }

            stateCacheKeysToRemove.Clear();
            foreach (var pair in pendingRemoteButtonCommits)
            {
                if (now - pair.Value.ReceivedAt <= PendingRemoteCommitTimeoutSeconds)
                {
                    continue;
                }

                stateCacheKeysToRemove.Add(pair.Key);
            }

            foreach (var syncId in stateCacheKeysToRemove)
            {
                pendingRemoteButtonCommits.Remove(syncId);
            }

            stateCacheKeysToRemove.Clear();
        }

        internal bool IsSnapshotStatePending(string syncId)
        {
            foreach (var snapshotState in snapshotReceiveStates.Values)
            {
                if (snapshotState.PendingSyncIds.Contains(syncId))
                {
                    return true;
                }
            }

            return false;
        }

        internal float GetPendingRemoteCommitTimeoutSeconds(bool isSnapshot)
        {
            if (!isSnapshot)
            {
                return PendingRemoteCommitTimeoutSeconds;
            }

            return profile != null ? Mathf.Max(0f, profile.initialSyncPendingTimeoutSeconds) : 1f;
        }

        internal void RemoveMissingBindingState<TValue>(Dictionary<string, TValue> valuesBySyncId)
        {
            if (valuesBySyncId.Count == 0)
            {
                return;
            }

            stateCacheKeysToRemove.Clear();
            foreach (var syncId in valuesBySyncId.Keys)
            {
                if (!bindings.ContainsKey(syncId))
                {
                    stateCacheKeysToRemove.Add(syncId);
                }
            }

            foreach (var syncId in stateCacheKeysToRemove)
            {
                valuesBySyncId.Remove(syncId);
            }

            stateCacheKeysToRemove.Clear();
        }

        internal bool IsComponentExcluded(Component component)
        {
            if (component == null)
            {
                return false;
            }

            if (excludedUi != null)
            {
                for (var index = 0; index < excludedUi.Count; index++)
                {
                    var exclusion = excludedUi[index];
                    if (exclusion == null)
                    {
                        continue;
                    }

                    if (TryReadExclusionLocator(exclusion, out var bindingId, out var hierarchyPath, out var componentType) && MatchesExclusionLocator(component, bindingId, hierarchyPath, componentType))
                    {
                        return true;
                    }
                }
            }

            if (excludedComponents != null)
            {
                for (var index = 0; index < excludedComponents.Count; index++)
                {
                    if (TryBuildExclusionLocator(excludedComponents[index], out var bindingId, out var hierarchyPath, out var componentType) && MatchesExclusionLocator(component, bindingId, hierarchyPath, componentType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool IsSyncIdExcluded(string syncId)
        {
            if (string.IsNullOrEmpty(syncId))
            {
                return false;
            }

            if (excludedUi != null)
            {
                for (var index = 0; index < excludedUi.Count; index++)
                {
                    var exclusion = excludedUi[index];
                    if (exclusion == null)
                    {
                        continue;
                    }

                    if (TryReadExclusionLocator(exclusion, out var bindingId, out var hierarchyPath, out var componentType) && MatchesExcludedSyncId(syncId, bindingId, hierarchyPath, componentType))
                    {
                        return true;
                    }
                }
            }

            if (excludedComponents != null)
            {
                for (var index = 0; index < excludedComponents.Count; index++)
                {
                    if (TryBuildExclusionLocator(excludedComponents[index], out var bindingId, out var hierarchyPath, out var componentType) && MatchesExcludedSyncId(syncId, bindingId, hierarchyPath, componentType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal bool HasConfiguredExclusions()
        {
            return hasConfiguredExclusions;
        }

        internal void RefreshExclusionRules(bool updateExistingLocators = false)
        {
            excludedUi ??= new List<CanvasUiSyncExclusion>();
            if (excludedComponents != null && excludedComponents.Count > 0)
            {
                for (var index = 0; index < excludedComponents.Count; index++)
                {
                    var component = excludedComponents[index];
                    if (component != null)
                    {
                        excludedUi.Add(new CanvasUiSyncExclusion { target = component });
                    }
                }

                excludedComponents.Clear();
            }

            for (var index = 0; index < excludedUi.Count; index++)
            {
                var exclusion = excludedUi[index];
                if (exclusion == null)
                {
                    exclusion = new CanvasUiSyncExclusion();
                    excludedUi[index] = exclusion;
                }

                if (exclusion.target == null || HasExclusionLocator(exclusion) && (!updateExistingLocators || exclusion.target == exclusion.capturedTarget))
                {
                    continue;
                }

                if (!TryBuildExclusionLocator(exclusion.target, out var bindingId, out var hierarchyPath, out var componentType))
                {
                    continue;
                }

                exclusion.bindingId = bindingId;
                exclusion.hierarchyPath = hierarchyPath;
                exclusion.componentType = componentType;
                exclusion.capturedTarget = exclusion.target;
            }

            hasConfiguredExclusions = false;
            for (var index = 0; index < excludedUi.Count; index++)
            {
                if (HasExclusionLocator(excludedUi[index]))
                {
                    hasConfiguredExclusions = true;
                    break;
                }
            }
        }

        internal void PruneExcludedPendingCommits()
        {
            stateCacheKeysToRemove.Clear();
            foreach (var pair in pendingRemoteCommits)
            {
                if (IsSyncIdExcluded(pair.Key))
                {
                    stateCacheKeysToRemove.Add(pair.Key);
                }
            }

            foreach (var pair in snapshotReceiveStates)
            {
                foreach (var syncId in pair.Value.PendingSyncIds)
                {
                    if (IsSyncIdExcluded(syncId) && !stateCacheKeysToRemove.Contains(syncId))
                    {
                        stateCacheKeysToRemove.Add(syncId);
                    }
                }
            }

            for (var index = 0; index < stateCacheKeysToRemove.Count; index++)
            {
                var syncId = stateCacheKeysToRemove[index];
                pendingRemoteCommits.Remove(syncId);
                CanvasUiSyncProtocolService.HandlePendingSnapshotStateApplied(this, syncId);
            }

            stateCacheKeysToRemove.Clear();
            foreach (var pair in pendingRemoteButtonCommits)
            {
                if (IsSyncIdExcluded(pair.Key))
                {
                    stateCacheKeysToRemove.Add(pair.Key);
                }
            }

            for (var index = 0; index < stateCacheKeysToRemove.Count; index++)
            {
                pendingRemoteButtonCommits.Remove(stateCacheKeysToRemove[index]);
            }

            stateCacheKeysToRemove.Clear();
        }

        internal void DiscardPendingExcludedSyncId(string syncId)
        {
            pendingRemoteCommits.Remove(syncId);
            pendingRemoteButtonCommits.Remove(syncId);
            CanvasUiSyncProtocolService.HandlePendingSnapshotStateApplied(this, syncId);
        }

        private bool TryReadExclusionLocator(CanvasUiSyncExclusion exclusion, out string bindingId, out string hierarchyPath, out string componentType)
        {
            bindingId = exclusion.bindingId;
            hierarchyPath = exclusion.hierarchyPath;
            componentType = exclusion.componentType;
            return HasExclusionLocator(exclusion) || TryBuildExclusionLocator(exclusion.target, out bindingId, out hierarchyPath, out componentType);
        }

        private static bool HasExclusionLocator(CanvasUiSyncExclusion exclusion)
        {
            return exclusion != null && (!string.IsNullOrWhiteSpace(exclusion.bindingId) || !string.IsNullOrEmpty(exclusion.hierarchyPath));
        }

        private bool TryBuildExclusionLocator(Component target, out string bindingId, out string hierarchyPath, out string componentType)
        {
            bindingId = string.Empty;
            hierarchyPath = string.Empty;
            componentType = GetSupportedComponentType(target);
            if (target == null || target.transform == null || target.transform != transform && !target.transform.IsChildOf(transform))
            {
                return false;
            }

            var explicitBindingId = ReadExplicitBindingId(target);
            if (!string.IsNullOrWhiteSpace(explicitBindingId))
            {
                bindingId = explicitBindingId;
                return true;
            }

            hierarchyPath = CanvasUiSyncBindingsService.BuildPath(this, target.transform);
            return !string.IsNullOrEmpty(hierarchyPath);
        }

        private bool MatchesExclusionLocator(Component component, string bindingId, string hierarchyPath, string excludedComponentType)
        {
            var componentBindingId = ReadExplicitBindingId(component);
            if (!string.IsNullOrWhiteSpace(bindingId))
            {
                return string.Equals(componentBindingId, bindingId, StringComparison.Ordinal) && IsComponentTypeExcluded(component, excludedComponentType);
            }

            if (string.IsNullOrEmpty(hierarchyPath) || !string.IsNullOrWhiteSpace(componentBindingId))
            {
                return false;
            }

            return string.Equals(CanvasUiSyncBindingsService.BuildPath(this, component.transform), hierarchyPath, StringComparison.Ordinal) && IsComponentTypeExcluded(component, excludedComponentType);
        }

        private bool MatchesExcludedSyncId(string syncId, string bindingId, string hierarchyPath, string excludedComponentType)
        {
            var locator = !string.IsNullOrWhiteSpace(bindingId) ? bindingId : hierarchyPath;
            if (string.IsNullOrEmpty(locator))
            {
                return false;
            }

            var prefix = canvasId + "/" + locator + ":";
            return syncId.StartsWith(prefix, StringComparison.Ordinal) && IsBindingTypeExcluded(syncId.Substring(prefix.Length), excludedComponentType);
        }

        private static bool IsComponentTypeExcluded(Component component, string excludedComponentType)
        {
            return string.IsNullOrEmpty(excludedComponentType) || string.Equals(GetSupportedComponentType(component), excludedComponentType, StringComparison.Ordinal);
        }

        private static bool IsBindingTypeExcluded(string bindingType, string excludedComponentType)
        {
            if (string.IsNullOrEmpty(excludedComponentType) || string.Equals(bindingType, excludedComponentType, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.Equals(excludedComponentType, "Dropdown", StringComparison.Ordinal) && !string.Equals(excludedComponentType, "TMP_Dropdown", StringComparison.Ordinal))
            {
                return false;
            }

            return string.Equals(bindingType, excludedComponentType + "Expanded", StringComparison.Ordinal) || bindingType.StartsWith(excludedComponentType + "ItemToggle[", StringComparison.Ordinal);
        }

        private static string GetSupportedComponentType(Component component)
        {
            if (component is Toggle)
            {
                return "Toggle";
            }

            if (component is Slider)
            {
                return "Slider";
            }

            if (component is Scrollbar)
            {
                return "Scrollbar";
            }

            if (component is Dropdown)
            {
                return "Dropdown";
            }

            if (component is TMP_Dropdown)
            {
                return "TMP_Dropdown";
            }

            if (component is InputField)
            {
                return "InputField";
            }

            if (component is TMP_InputField)
            {
                return "TMP_InputField";
            }

            return component is Button ? "Button" : string.Empty;
        }

        internal bool ShouldDebugLog()
        {
            return profile != null && profile.enableDebugLog;
        }

        internal bool ShouldVerboseLog()
        {
            return ShouldDebugLog() && profile.verboseLog;
        }

        internal bool ShouldStatisticsLog()
        {
            return ShouldDebugLog() && profile.enableStatisticsLog;
        }

        internal void ScanBindings()
        {
            CanvasUiSyncBindingsService.ScanBindings(this);
        }

        internal void RegisterToggles()
        {
            CanvasUiSyncBindingsService.RegisterToggles(this);
        }

        internal void RegisterSliders()
        {
            CanvasUiSyncBindingsService.RegisterSliders(this);
        }

        internal void RegisterScrollbars()
        {
            CanvasUiSyncBindingsService.RegisterScrollbars(this);
        }

        internal void RegisterDropdowns()
        {
            CanvasUiSyncBindingsService.RegisterDropdowns(this);
        }

        internal void RegisterTmpDropdowns()
        {
            CanvasUiSyncBindingsService.RegisterTmpDropdowns(this);
        }

        internal void RegisterInputFields()
        {
            CanvasUiSyncBindingsService.RegisterInputFields(this);
        }

        internal void RegisterTmpInputFields()
        {
            CanvasUiSyncBindingsService.RegisterTmpInputFields(this);
        }

        internal void RegisterButtons()
        {
            CanvasUiSyncBindingsService.RegisterButtons(this);
        }

        internal void RegisterBinding(UiSyncBinding binding)
        {
            CanvasUiSyncBindingsService.RegisterBinding(this, binding);
        }

        internal string BuildSyncId(Transform target, string componentType)
        {
            return CanvasUiSyncBindingsService.BuildSyncId(this, target, componentType);
        }

        internal string BuildSyncIdPrefix(Transform target, string componentTypePrefix)
        {
            return CanvasUiSyncBindingsService.BuildSyncIdPrefix(this, target, componentTypePrefix);
        }

        internal string BuildPath(Transform target)
        {
            return CanvasUiSyncBindingsService.BuildPath(this, target);
        }

        internal string ReadExplicitBindingId(Component target)
        {
            return CanvasUiSyncBindingsService.ReadExplicitBindingId(target);
        }

        internal string ComputeRegistryHash()
        {
            return CanvasUiSyncBindingsService.ComputeRegistryHash(this);
        }

        internal void ApplyValueToBinding(UiSyncBinding binding, object value)
        {
            CanvasUiSyncBindingsService.ApplyValueToBinding(this, binding, value);
        }

        internal void TickRuntimeHierarchyRescan(float now)
        {
            CanvasUiSyncBindingsService.TickRuntimeHierarchyRescan(this, now);
        }

        internal bool TryRefreshBindingsForSyncId(string syncId)
        {
            return CanvasUiSyncBindingsService.TryRefreshBindingsForSyncId(this, syncId);
        }

        internal bool RefreshBindingsIfHierarchyChanged(bool force)
        {
            return CanvasUiSyncBindingsService.RefreshBindingsIfHierarchyChanged(this, force);
        }

        internal int ComputeBindingHierarchySignature()
        {
            return CanvasUiSyncBindingsService.ComputeBindingHierarchySignature(this);
        }

        internal void ResetRuntimeHierarchyRescanSchedule(float now)
        {
            currentHierarchyRescanIntervalSeconds = RuntimeHierarchyRescanIntervalSeconds;
            nextHierarchyRescanTime = now + currentHierarchyRescanIntervalSeconds;
        }

        internal int ComputeStableHash(string value)
        {
            return CanvasUiSyncBindingsService.ComputeStableHash(value);
        }

        internal void HandleDropdownRuntimeRootChanged(Dropdown dropdown, GameObject runtimeRoot)
        {
            CanvasUiSyncBindingsService.HandleDropdownRuntimeRootChanged(this, dropdown, runtimeRoot);
        }

        internal void HandleDropdownRuntimeRootChanged(TMP_Dropdown dropdown, GameObject runtimeRoot)
        {
            CanvasUiSyncBindingsService.HandleDropdownRuntimeRootChanged(this, dropdown, runtimeRoot);
        }

        internal void AppendBindingHierarchySignature<TComponent>(ref int hash, IEnumerable<TComponent> components, string componentType) where TComponent : Component
        {
            CanvasUiSyncBindingsService.AppendBindingHierarchySignature(this, ref hash, components, componentType);
        }

        internal void OnOscMessageReceived(Message message)
        {
            CanvasUiSyncProtocolService.OnOscMessageReceived(this, message);
        }

        internal void HandleReceivedPayload(string address, object[] values)
        {
            CanvasUiSyncProtocolService.HandleReceivedPayload(this, address, values);
        }

        internal void RecordReceivedPayload(string address, object[] values)
        {
            CanvasUiSyncProtocolService.RecordReceivedPayload(this, address, values);
        }

        internal void DispatchReceivedPayload(string address, object[] values)
        {
            CanvasUiSyncProtocolService.DispatchReceivedPayload(this, address, values);
        }

        internal void HandleHello(object[] values)
        {
            CanvasUiSyncProtocolService.HandleHello(this, values);
        }

        internal void HandleRequestSnapshot(object[] values)
        {
            CanvasUiSyncProtocolService.HandleRequestSnapshot(this, values);
        }

        internal void HandleBeginSnapshot(object[] values)
        {
            CanvasUiSyncProtocolService.HandleBeginSnapshot(this, values);
        }

        internal void HandleSnapshotState(object[] values)
        {
            CanvasUiSyncProtocolService.HandleSnapshotState(this, values);
        }

        internal void HandleEndSnapshot(object[] values)
        {
            CanvasUiSyncProtocolService.HandleEndSnapshot(this, values);
        }

        internal void HandleCommitState(object[] values)
        {
            CanvasUiSyncProtocolService.HandleCommitState(this, values);
        }

        internal void HandleCommitButton(object[] values)
        {
            CanvasUiSyncProtocolService.HandleCommitButton(this, values);
        }

        internal bool ApplyButtonCommit(UiSyncBinding binding, string syncId, StateStamp stamp)
        {
            return CanvasUiSyncProtocolService.ApplyButtonCommit(this, binding, syncId, stamp);
        }

        internal bool IsPeerAuthorized(string nodeId)
        {
            return CanvasUiSyncProtocolService.IsPeerAuthorized(this, nodeId);
        }

        internal bool ShouldIgnoreIncomingPeer(string nodeId)
        {
            return CanvasUiSyncProtocolService.ShouldIgnoreIncomingPeer(this, nodeId);
        }

        internal bool IsPeerSessionIgnored(string nodeId, string peerSessionId)
        {
            return CanvasUiSyncProtocolService.IsPeerSessionIgnored(this, nodeId, peerSessionId);
        }

        internal bool HasIgnoredPeerSession(string nodeId)
        {
            return CanvasUiSyncProtocolService.HasIgnoredPeerSession(this, nodeId);
        }

        internal bool CanApplyPendingSnapshotStates()
        {
            return CanvasUiSyncProtocolService.CanApplyPendingSnapshotStates(this);
        }

        internal void HandleBindingsRefreshed()
        {
            CanvasUiSyncProtocolService.HandleBindingsRefreshed(this);
        }

        internal StateStamp CreateLocalStamp()
        {
            return CanvasUiSyncProtocolService.CreateLocalStamp(this);
        }

        internal StateStamp ReadStamp(object[] values, int startIndex)
        {
            return CanvasUiSyncProtocolService.ReadStamp(this, values, startIndex);
        }

        internal bool TryReadStamp(object[] values, int startIndex, out StateStamp stamp)
        {
            return CanvasUiSyncProtocolService.TryReadStamp(this, values, startIndex, out stamp);
        }

        internal bool IsIncomingStampNewer(StateStamp current, StateStamp incoming)
        {
            return CanvasUiSyncProtocolService.IsIncomingStampNewer(current, incoming);
        }

        internal object SerializeValue(object value, string valueType)
        {
            return CanvasUiSyncProtocolService.SerializeValue(this, value, valueType);
        }

        internal object DeserializeValue(object value, string valueType)
        {
            return CanvasUiSyncProtocolService.DeserializeValue(this, value, valueType);
        }

        internal void HandleUnknownSyncId(string syncId)
        {
            CanvasUiSyncProtocolService.HandleUnknownSyncId(this, syncId);
        }

        internal void HandleTypeMismatch(string syncId, string localType, string remoteType)
        {
            CanvasUiSyncProtocolService.HandleTypeMismatch(this, syncId, localType, remoteType);
        }

        internal void OnLocalStateChanged(UiSyncBinding binding, object value, bool force)
        {
            CanvasUiSyncStateService.OnLocalStateChanged(this, binding, value, force);
        }

        internal void OnLocalButtonClicked(UiSyncBinding binding)
        {
            CanvasUiSyncStateService.OnLocalButtonClicked(this, binding);
        }

        internal void OnInteractionStarted(UiSyncBinding binding)
        {
            CanvasUiSyncStateService.OnInteractionStarted(this, binding);
        }

        internal void OnInteractionEnded(UiSyncBinding binding)
        {
            CanvasUiSyncStateService.OnInteractionEnded(this, binding);
        }

        internal void UpdateContinuousInteractions()
        {
            CanvasUiSyncStateService.UpdateContinuousInteractions(this);
        }

        internal void UpdatePolledBindings()
        {
            CanvasUiSyncStateService.UpdatePolledBindings(this);
        }

        internal void CommitLocalState(UiSyncBinding binding, object value, bool applyToLocalUi, StateStamp stamp)
        {
            CanvasUiSyncStateService.CommitLocalState(this, binding, value, applyToLocalUi, stamp);
        }

        internal void CommitLocalButton(UiSyncBinding binding, StateStamp stamp)
        {
            CanvasUiSyncStateService.CommitLocalButton(this, binding, stamp);
        }

        internal void ApplyRemoteState(string syncId, string valueType, object value, StateStamp stamp, bool isSnapshot)
        {
            CanvasUiSyncStateService.ApplyRemoteState(this, syncId, valueType, value, stamp, isSnapshot);
        }

        internal void ApplyRemoteState(string syncId, string valueType, object value, StateStamp stamp, bool isSnapshot, bool canInitializeLocalState)
        {
            CanvasUiSyncStateService.ApplyRemoteState(this, syncId, valueType, value, stamp, isSnapshot, canInitializeLocalState);
        }

        internal void ApplyRemoteState(string syncId, string valueType, object value, StateStamp stamp, bool isSnapshot, bool canInitializeLocalState, bool deferSnapshotUntilRegistryMatches)
        {
            CanvasUiSyncStateService.ApplyRemoteState(this, syncId, valueType, value, stamp, isSnapshot, canInitializeLocalState, deferSnapshotUntilRegistryMatches);
        }

        internal void FlushPendingCommits(float now)
        {
            CanvasUiSyncStateService.FlushPendingCommits(this, now);
        }

        internal void TickSnapshotRetry(float now)
        {
            CanvasUiSyncTransportService.TickSnapshotRetry(this, now);
        }

        internal void RequestSnapshotIfNeeded(bool force)
        {
            CanvasUiSyncTransportService.RequestSnapshotIfNeeded(this, force);
        }

        internal void TickSnapshotCleanup(float now)
        {
            CanvasUiSyncTransportService.TickSnapshotCleanup(this, now);
        }

        internal void TickPeriodicResync(float now)
        {
            CanvasUiSyncTransportService.TickPeriodicResync(this, now);
        }

        internal void TickStatisticsLog(float now)
        {
            CanvasUiSyncTransportService.TickStatisticsLog(this, now);
        }

        internal void SendHello()
        {
            CanvasUiSyncTransportService.SendHello(this);
        }

        internal IEnumerable<CanvasUiSyncRemoteEndpoint> GetActivePeerTargets()
        {
            return CanvasUiSyncTransportService.GetActivePeerTargets(this);
        }

        internal CanvasUiSyncRemoteEndpoint FindPeerTarget(string nodeId)
        {
            return CanvasUiSyncTransportService.FindPeerTarget(this, nodeId);
        }

        internal void TickNodeTimeout(float now)
        {
            CanvasUiSyncTransportService.TickNodeTimeout(this, now);
        }

        internal void SendSnapshot(CanvasUiSync target)
        {
            CanvasUiSyncTransportService.SendSnapshot(this, target);
        }

        internal void SendSnapshot(string ipAddress, int port)
        {
            CanvasUiSyncTransportService.SendSnapshot(this, ipAddress, port);
        }

        internal void SendSnapshotCore(Action<object[]> sendBegin, Action<object[]> sendState, Action<object[]> sendEnd)
        {
            CanvasUiSyncTransportService.SendSnapshotCore(this, sendBegin, sendState, sendEnd);
        }

        internal IEnumerable<object[]> EnumerateSnapshotStateValues(string snapshotId)
        {
            return CanvasUiSyncTransportService.EnumerateSnapshotStateValues(this, snapshotId);
        }

        internal void BroadcastCommit(string syncId, string valueType, object value, StateStamp stamp)
        {
            if (IsSyncIdExcluded(syncId))
            {
                return;
            }

            CanvasUiSyncTransportService.BroadcastCommit(this, syncId, valueType, value, stamp);
        }

        internal void BroadcastButton(string syncId, StateStamp stamp)
        {
            if (IsSyncIdExcluded(syncId))
            {
                return;
            }

            CanvasUiSyncTransportService.BroadcastButton(this, syncId, stamp);
        }

        internal bool HasActivePeerTarget()
        {
            return CanvasUiSyncTransportService.HasActivePeerTarget(this);
        }

        internal bool IsPeerTargetActive(CanvasUiSyncRemoteEndpoint endpoint)
        {
            return CanvasUiSyncTransportService.IsPeerTargetActive(this, endpoint);
        }

        internal void SendTo(string ipAddress, int port, string address, params object[] values)
        {
            CanvasUiSyncTransportService.SendTo(this, ipAddress, port, address, values);
        }

        internal static long EstimatePayloadBytes(string address, object[] values)
        {
            return CanvasUiSyncTransportService.EstimatePayloadBytes(address, values);
        }

        internal static string SerializeLogicalTicks(long logicalTicks)
        {
            return CanvasUiSyncTransportService.SerializeLogicalTicks(logicalTicks);
        }
    }
}
