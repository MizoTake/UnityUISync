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
        [SerializeField] internal List<Component> excludedComponents = new List<Component>();

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
        internal readonly List<string> stateCacheKeysToRemove = new List<string>();
        internal readonly List<string> expiredSnapshotIds = new List<string>();
        internal readonly List<string> expiredNodeIds = new List<string>();
        internal readonly List<string> bindingKeyScratch = new List<string>();
        internal readonly Dictionary<Transform, string> pathCacheScratch = new Dictionary<Transform, string>();
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
        internal readonly StringBuilder stringBuilderScratch = new StringBuilder(256);
        internal string registryHash = string.Empty;
        internal string canvasId = string.Empty;
        internal string sessionId = string.Empty;
        internal float nextHelloTime;
        internal float nextSnapshotRequestTime;
        internal float nextPeriodicResyncTime;
        internal float nextStatisticsLogTime;
        internal float nextHierarchyRescanTime;
        internal float nextPendingCommitTime = float.PositiveInfinity;
        internal float snapshotCooldownUntil;
        internal int snapshotRetryCount;
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
        internal uOscServer server;
        internal uOscClient client;
        internal Canvas canvasComponent;
        internal GameObject transportHost;

        public bool SyncEnabled => syncEnabled;

        internal sealed class NodeState
        {
            public NodeState(string nodeId, string sessionId, float lastSeenAt)
            {
                NodeId = nodeId;
                SessionId = sessionId;
                LastSeenAt = lastSeenAt;
            }

            public string NodeId { get; }
            public string SessionId { get; set; }
            public float LastSeenAt { get; set; }
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
            public Component Component { get; }
            public string SyncId { get; }
            public string ValueType { get; }
            public bool IsContinuous { get; }
            public bool RequiresPolling { get; }
            public bool IsInteracting { get; set; }
            public Action Unsubscribe { get; set; }

            public object ReadValue()
            {
                return readValue == null ? null : readValue();
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
            public DeferredStateCommit(string valueType, object value, StateStamp stamp, float receivedAt = 0f)
            {
                ValueType = valueType;
                Value = value;
                Stamp = stamp;
                ReceivedAt = receivedAt;
            }

            public string ValueType { get; }
            public object Value { get; }
            public StateStamp Stamp { get; }
            public float ReceivedAt { get; }
        }

        internal readonly struct PendingButtonCommit
        {
            public PendingButtonCommit(StateStamp stamp, float receivedAt)
            {
                Stamp = stamp;
                ReceivedAt = receivedAt;
            }

            public StateStamp Stamp { get; }
            public float ReceivedAt { get; }
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
            canvasComponent = GetComponent<Canvas>();
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

            nextHelloTime = Time.unscaledTime;
            nextSnapshotRequestTime = Time.unscaledTime;
            nextPeriodicResyncTime = profile.periodicFullResyncIntervalSeconds > 0f ? Time.unscaledTime + profile.periodicFullResyncIntervalSeconds : float.PositiveInfinity;
            nextStatisticsLogTime = profile.enableStatisticsLog ? Time.unscaledTime + profile.statisticsLogIntervalSeconds : float.PositiveInfinity;
            nextHierarchyRescanTime = Time.unscaledTime + RuntimeHierarchyRescanIntervalSeconds;
            lastGcCollectionCount0 = GC.CollectionCount(0);
            lastGcCollectionCount1 = GC.CollectionCount(1);
            lastGcCollectionCount2 = GC.CollectionCount(2);
            RequestSnapshotIfNeeded(true);
        }

        private void OnEnable()
        {
            if (initialized && rescanOnEnable)
            {
                ScanBindings();
                InitializeLocalState();
                bindingHierarchySignature = ComputeBindingHierarchySignature();
            }
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
                return;
            }

            RefreshBindingsIfHierarchyChanged(true);
            hasSnapshot = false;
            snapshotRetryCount = 0;
            snapshotCooldownUntil = 0f;
            nextHelloTime = Time.unscaledTime;
            nextSnapshotRequestTime = Time.unscaledTime;
            nextPeriodicResyncTime = profile.periodicFullResyncIntervalSeconds > 0f ? Time.unscaledTime + profile.periodicFullResyncIntervalSeconds : float.PositiveInfinity;
            nextStatisticsLogTime = profile.enableStatisticsLog ? Time.unscaledTime + profile.statisticsLogIntervalSeconds : float.PositiveInfinity;
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
            if (server != null)
            {
                server.onDataReceived.RemoveListener(OnOscMessageReceived);
            }

            foreach (var binding in bindings.Values)
            {
                binding.Dispose();
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
            foreach (var pair in pendingRemoteCommits)
            {
                if (!bindings.TryGetValue(pair.Key, out var binding))
                {
                    continue;
                }

                var pending = pair.Value;
                if (!string.Equals(binding.ValueType, pending.ValueType, StringComparison.Ordinal))
                {
                    HandleTypeMismatch(pair.Key, binding.ValueType, pending.ValueType);
                    stateCacheKeysToRemove.Add(pair.Key);
                    continue;
                }

                ApplyRemoteState(pair.Key, pending.ValueType, pending.Value, pending.Stamp, false);
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
                if (!bindings.ContainsKey(pair.Key))
                {
                    continue;
                }

                if (!bindings.TryGetValue(pair.Key, out var binding))
                {
                    continue;
                }

                ApplyButtonCommit(binding, pair.Key, pair.Value.Stamp);
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
                if (pair.Value.ReceivedAt > 0f && now - pair.Value.ReceivedAt <= PendingRemoteCommitTimeoutSeconds)
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
                if (pair.Value.ReceivedAt > 0f && now - pair.Value.ReceivedAt <= PendingRemoteCommitTimeoutSeconds)
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
            if (component == null || excludedComponents == null || excludedComponents.Count == 0)
            {
                return false;
            }

            for (var index = 0; index < excludedComponents.Count; index++)
            {
                if (excludedComponents[index] == component)
                {
                    return true;
                }
            }

            return false;
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

        internal void RefreshBindingsIfHierarchyChanged(bool force)
        {
            CanvasUiSyncBindingsService.RefreshBindingsIfHierarchyChanged(this, force);
        }

        internal int ComputeBindingHierarchySignature()
        {
            return CanvasUiSyncBindingsService.ComputeBindingHierarchySignature(this);
        }

        internal int ComputeStableHash(string value)
        {
            return CanvasUiSyncBindingsService.ComputeStableHash(value);
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
            CanvasUiSyncTransportService.BroadcastCommit(this, syncId, valueType, value, stamp);
        }

        internal void BroadcastButton(string syncId, StateStamp stamp)
        {
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
