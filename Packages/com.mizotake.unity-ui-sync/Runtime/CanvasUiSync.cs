using System;
using System.Collections.Generic;
using uOSC;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed partial class CanvasUiSync : MonoBehaviour
    {
        private static readonly HashSet<CanvasUiSync> ActiveInstances = new HashSet<CanvasUiSync>();
        private const string HelloAddress = "/uisync/hello";
        private const string RequestSnapshotAddress = "/uisync/requestSnapshot";
        private const string BeginSnapshotAddress = "/uisync/beginSnapshot";
        private const string SnapshotStateAddress = "/uisync/snapshotState";
        private const string EndSnapshotAddress = "/uisync/endSnapshot";
        private const string ProposeStateAddress = "/uisync/proposeState";
        private const string CommitStateAddress = "/uisync/commitState";
        private const string ProposeButtonAddress = "/uisync/proposeButton";
        private const string CommitButtonAddress = "/uisync/commitButton";

        [SerializeField] private CanvasUiSyncProfile profile;
        [SerializeField] private string canvasIdOverride = string.Empty;
        [SerializeField] private bool rescanOnEnable;

        private readonly Dictionary<string, UiSyncBinding> bindings = new Dictionary<string, UiSyncBinding>();
        private readonly Dictionary<string, NodeState> nodes = new Dictionary<string, NodeState>();
        private readonly Dictionary<string, LocalStateRecord> localStates = new Dictionary<string, LocalStateRecord>();
        private readonly Dictionary<string, StateStamp> latestAppliedButtonStamps = new Dictionary<string, StateStamp>();
        private readonly Dictionary<string, object> lastProposedValues = new Dictionary<string, object>();
        private readonly Dictionary<string, float> lastProposeTimes = new Dictionary<string, float>();
        private readonly Dictionary<string, DeferredStateCommit> deferredCommits = new Dictionary<string, DeferredStateCommit>();
        private readonly Dictionary<string, float> activeSnapshotIds = new Dictionary<string, float>();
        private readonly List<string> stateCacheKeysToRemove = new List<string>();
        private readonly List<string> expiredSnapshotIds = new List<string>();
        private readonly List<string> expiredNodeIds = new List<string>();
        private string registryHash = string.Empty;
        private string canvasId = string.Empty;
        private string sessionId = string.Empty;
        private float nextHelloTime;
        private float nextSnapshotRequestTime;
        private float nextPeriodicResyncTime;
        private float nextStatisticsLogTime;
        private float snapshotCooldownUntil;
        private int snapshotRetryCount;
        private int suppressionCount;
        private int localSequence;
        private long logicalTicks;
        private int sentMessageCount;
        private int receivedMessageCount;
        private int sentValueCount;
        private int receivedValueCount;
        private long sentApproxBytes;
        private long receivedApproxBytes;
        private int lastGcCollectionCount0;
        private int lastGcCollectionCount1;
        private int lastGcCollectionCount2;
        private bool initialized;
        private bool hasSnapshot;
        private uOscServer server;
        private uOscClient client;
        private Canvas canvasComponent;

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
            ActiveInstances.Add(this);
            InitializeTransport();
            ScanBindings();
            InitializeLocalState();
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
            }
        }

        private void Update()
        {
            if (!initialized)
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
            TickNodeTimeout(now);
            FlushPendingCommits(now);
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

            ActiveInstances.Remove(this);
        }

        private void InitializeTransport()
        {
            if (!profile.enableOscTransport)
            {
                return;
            }

            server = GetComponent<uOscServer>() ?? gameObject.AddComponent<uOscServer>();
            server.port = profile.listenPort;
            server.autoStart = true;
            server.onDataReceived.RemoveListener(OnOscMessageReceived);
            server.onDataReceived.AddListener(OnOscMessageReceived);

            client = GetComponent<uOscClient>() ?? gameObject.AddComponent<uOscClient>();
            client.address = "127.0.0.1";
            client.port = profile.listenPort;
        }

        private void InitializeLocalState()
        {
            localStates.Clear();
            latestAppliedButtonStamps.Clear();
            PruneTransientStateCaches();
            foreach (var pair in bindings)
            {
                if (pair.Value.ValueType == "Button")
                {
                    continue;
                }

                localStates[pair.Key] = new LocalStateRecord(pair.Value.ReadValue(), pair.Value.ValueType, default);
            }
        }

        private void PruneTransientStateCaches()
        {
            RemoveMissingBindingState(lastProposedValues);
            RemoveMissingBindingState(lastProposeTimes);
            RemoveMissingBindingState(deferredCommits);
        }

        private void RemoveMissingBindingState<TValue>(Dictionary<string, TValue> valuesBySyncId)
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

        private void OnOscMessageReceived(Message message)
        {
            HandleReceivedPayload(message.address, message.values);
        }

        private void HandleReceivedPayload(string address, object[] values)
        {
            if (values == null)
            {
                return;
            }

            RecordReceivedPayload(address, values);
            DispatchReceivedPayload(address, values);
        }

        private void RecordReceivedPayload(string address, object[] values)
        {
            receivedMessageCount++;
            receivedValueCount += values.Length;
            receivedApproxBytes += EstimatePayloadBytes(address, values);
        }

        private void DispatchReceivedPayload(string address, object[] values)
        {
            switch (address)
            {
                case HelloAddress: HandleHello(values); break;
                case RequestSnapshotAddress: HandleRequestSnapshot(values); break;
                case BeginSnapshotAddress: HandleBeginSnapshot(values); break;
                case SnapshotStateAddress: HandleSnapshotState(values); break;
                case EndSnapshotAddress: HandleEndSnapshot(values); break;
                case ProposeStateAddress:
                case CommitStateAddress:
                    HandleCommitState(values);
                    break;
                case ProposeButtonAddress:
                case CommitButtonAddress:
                    HandleCommitButton(values);
                    break;
            }
        }

        private void HandleHello(object[] values)
        {
            if (values.Length < 4 || !string.Equals(Convert.ToString(values[2]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = Convert.ToString(values[0]);
            var protocolVersion = Convert.ToInt32(values[1]);
            var incomingSessionId = Convert.ToString(values[3]);
            if (string.Equals(nodeId, profile.nodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (protocolVersion != profile.protocolVersion)
            {
                Debug.LogWarning("CanvasUiSync protocol version mismatch: local=" + profile.protocolVersion + " remote=" + protocolVersion, this);
            }

            if (nodes.TryGetValue(nodeId, out var node))
            {
                if (!string.Equals(node.SessionId, incomingSessionId, StringComparison.Ordinal))
                {
                    node.SessionId = incomingSessionId;
                    hasSnapshot = false;
                    snapshotRetryCount = 0;
                    RequestSnapshotIfNeeded(true);
                }

                node.LastSeenAt = Time.unscaledTime;
            }
            else
            {
                nodes[nodeId] = new NodeState(nodeId, incomingSessionId, Time.unscaledTime);
                Debug.Log("CanvasUiSync peer join: " + nodeId + " canvas=" + canvasId, this);
                hasSnapshot = false;
                snapshotRetryCount = 0;
                RequestSnapshotIfNeeded(true);
            }
        }

        private void HandleRequestSnapshot(object[] values)
        {
            if (values.Length < 3 || !string.Equals(Convert.ToString(values[1]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = Convert.ToString(values[0]);
            var incomingRegistryHash = Convert.ToString(values[2]);
            if (!IsPeerAuthorized(nodeId))
            {
                Debug.LogWarning("CanvasUiSync unauthorized peer reject: " + nodeId, this);
                return;
            }

            if (profile.logRegistryHashMismatch && !string.Equals(incomingRegistryHash, registryHash, StringComparison.Ordinal))
            {
                Debug.LogWarning("CanvasUiSync registryHash mismatch: local=" + registryHash + " remote=" + incomingRegistryHash, this);
            }

            var localTarget = FindLocalPeerTarget(nodeId);
            if (localTarget != null)
            {
                SendSnapshot(localTarget);
                return;
            }

            var endpoint = FindPeerTarget(nodeId);
            if (endpoint == null)
            {
                Debug.LogWarning("CanvasUiSync requestSnapshot target was not configured: " + nodeId, this);
                return;
            }

            SendSnapshot(endpoint.ipAddress, endpoint.port);
        }

        private void HandleBeginSnapshot(object[] values)
        {
            if (values.Length < 4 || !string.Equals(Convert.ToString(values[1]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            activeSnapshotIds[Convert.ToString(values[0])] = Time.unscaledTime + Mathf.Max(0.5f, profile.snapshotStateTimeoutSeconds);
            Debug.Log("CanvasUiSync snapshot begin: " + Convert.ToString(values[0]) + " canvas=" + canvasId, this);
        }

        private void HandleSnapshotState(object[] values)
        {
            if (values.Length < 8)
            {
                return;
            }

            var snapshotId = Convert.ToString(values[0]);
            if (!activeSnapshotIds.ContainsKey(snapshotId) || !string.Equals(Convert.ToString(values[1]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            if (!TryReadStamp(values, 5, out var snapshotStamp))
            {
                return;
            }

            ApplyRemoteState(Convert.ToString(values[2]), Convert.ToString(values[3]), DeserializeValue(values[4], Convert.ToString(values[3])), snapshotStamp, true);
        }

        private void HandleEndSnapshot(object[] values)
        {
            if (values.Length < 4)
            {
                return;
            }

            var snapshotId = Convert.ToString(values[0]);
            if (!activeSnapshotIds.Remove(snapshotId) || !string.Equals(Convert.ToString(values[1]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            hasSnapshot = true;
            snapshotRetryCount = 0;
            Debug.Log("CanvasUiSync snapshot end: canvas=" + canvasId, this);
        }

        private void HandleCommitState(object[] values)
        {
            if (values.Length < 8 || !string.Equals(Convert.ToString(values[2]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var senderNodeId = Convert.ToString(values[0]);
            if (string.Equals(senderNodeId, profile.nodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (!IsPeerAuthorized(senderNodeId))
            {
                Debug.LogWarning("CanvasUiSync unauthorized peer reject: " + senderNodeId, this);
                return;
            }

            if (!TryReadStamp(values, 6, out var stateStamp))
            {
                return;
            }

            ApplyRemoteState(Convert.ToString(values[3]), Convert.ToString(values[4]), DeserializeValue(values[5], Convert.ToString(values[4])), stateStamp, false);
        }

        private void HandleCommitButton(object[] values)
        {
            if (values.Length < 7 || !string.Equals(Convert.ToString(values[2]), canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var senderNodeId = Convert.ToString(values[0]);
            if (string.Equals(senderNodeId, profile.nodeId, StringComparison.Ordinal))
            {
                return;
            }

            if (!IsPeerAuthorized(senderNodeId))
            {
                Debug.LogWarning("CanvasUiSync unauthorized peer reject: " + senderNodeId, this);
                return;
            }

            var syncId = Convert.ToString(values[3]);
            if (!bindings.TryGetValue(syncId, out var binding))
            {
                HandleUnknownSyncId(syncId);
                return;
            }

            if (!TryReadStamp(values, 4, out var stamp))
            {
                return;
            }

            if (latestAppliedButtonStamps.TryGetValue(syncId, out var lastStamp) && !IsIncomingStampNewer(lastStamp, stamp))
            {
                if (profile.verboseLog)
                {
                    Debug.Log("CanvasUiSync stale button discard: " + syncId + " ticks=" + stamp.LogicalTicks + " node=" + stamp.NodeId, this);
                }

                return;
            }

            latestAppliedButtonStamps[syncId] = stamp;
            using (new SuppressionScope(this))
            {
                if (binding.Component is Button button)
                {
                    button.onClick.Invoke();
                }
            }
        }

        private void OnLocalStateChanged(UiSyncBinding binding, object value, bool force)
        {
            if (suppressionCount > 0)
            {
                return;
            }

            if (binding.IsContinuous && !force)
            {
                var now = Time.unscaledTime;
                if (lastProposeTimes.TryGetValue(binding.SyncId, out var lastProposeAt) && now - lastProposeAt < Mathf.Max(0f, profile.minimumProposeIntervalSeconds))
                {
                    if (profile.verboseLog)
                    {
                        Debug.Log("CanvasUiSync throttle propose: " + binding.SyncId, this);
                    }

                    return;
                }

                if (lastProposedValues.TryGetValue(binding.SyncId, out var lastValue) && Mathf.Abs(Convert.ToSingle(lastValue) - Convert.ToSingle(value)) < Mathf.Max(0f, profile.sliderEpsilon))
                {
                    return;
                }
            }

            lastProposeTimes[binding.SyncId] = Time.unscaledTime;
            lastProposedValues[binding.SyncId] = value;
            CommitLocalState(binding, value, false, CreateLocalStamp());
        }

        private void OnLocalButtonClicked(UiSyncBinding binding)
        {
            if (suppressionCount > 0)
            {
                return;
            }

            CommitLocalButton(binding, CreateLocalStamp());
        }

        private void OnInteractionStarted(UiSyncBinding binding)
        {
            binding.IsInteracting = true;
        }

        private void OnInteractionEnded(UiSyncBinding binding)
        {
            binding.IsInteracting = false;
            OnLocalStateChanged(binding, binding.ReadValue(), true);
            if (deferredCommits.TryGetValue(binding.SyncId, out var deferred))
            {
                deferredCommits.Remove(binding.SyncId);
                ApplyRemoteState(binding.SyncId, deferred.ValueType, deferred.Value, deferred.Stamp, false);
            }
        }

        private void UpdateContinuousInteractions()
        {
            if (bindings.Count == 0)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var eventCamera = canvasComponent != null && canvasComponent.renderMode != RenderMode.ScreenSpaceOverlay ? canvasComponent.worldCamera : null;
                foreach (var binding in bindings.Values)
                {
                    if (!binding.IsContinuous || binding.Component is not RectTransform rectTransform)
                    {
                        continue;
                    }

                    if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera))
                    {
                        OnInteractionStarted(binding);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                foreach (var binding in bindings.Values)
                {
                    if (binding.IsContinuous && binding.IsInteracting)
                    {
                        OnInteractionEnded(binding);
                    }
                }
            }
        }

        private void CommitLocalState(UiSyncBinding binding, object value, bool applyToLocalUi, StateStamp stamp)
        {
            if (!localStates.TryGetValue(binding.SyncId, out var state))
            {
                state = new LocalStateRecord(value, binding.ValueType, stamp);
                localStates[binding.SyncId] = state;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            if (applyToLocalUi)
            {
                ApplyValueToBinding(binding, value);
            }

            var now = Time.unscaledTime;
            if (now - state.LastBroadcastAt >= Mathf.Max(0f, profile.minimumCommitBroadcastIntervalSeconds))
            {
                BroadcastCommit(binding.SyncId, binding.ValueType, value, stamp);
                state.LastBroadcastAt = now;
                state.HasPendingBroadcast = false;
            }
            else
            {
                state.HasPendingBroadcast = true;
                state.NextBroadcastAt = state.LastBroadcastAt + Mathf.Max(0f, profile.minimumCommitBroadcastIntervalSeconds);
            }
        }

        private void CommitLocalButton(UiSyncBinding binding, StateStamp stamp)
        {
            latestAppliedButtonStamps[binding.SyncId] = stamp;
            BroadcastButton(binding.SyncId, stamp);
        }

        private void ApplyRemoteState(string syncId, string valueType, object value, StateStamp stamp, bool isSnapshot)
        {
            if (!bindings.TryGetValue(syncId, out var binding))
            {
                HandleUnknownSyncId(syncId);
                return;
            }

            if (!string.Equals(binding.ValueType, valueType, StringComparison.Ordinal))
            {
                HandleTypeMismatch(syncId, binding.ValueType, valueType);
                return;
            }

            if (!localStates.TryGetValue(syncId, out var state))
            {
                state = new LocalStateRecord(value, valueType, stamp);
                localStates[syncId] = state;
            }

            if (!IsIncomingStampNewer(state.Stamp, stamp))
            {
                if (profile.verboseLog)
                {
                    Debug.Log("CanvasUiSync stale state discard: " + syncId + " ticks=" + stamp.LogicalTicks + " node=" + stamp.NodeId, this);
                }

                return;
            }

            if (binding.IsContinuous && binding.IsInteracting && !isSnapshot)
            {
                deferredCommits[syncId] = new DeferredStateCommit(valueType, value, stamp);
                return;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            ApplyValueToBinding(binding, value);
        }

        private void TickSnapshotRetry(float now)
        {
            if (!hasSnapshot && now >= snapshotCooldownUntil && now >= nextSnapshotRequestTime && snapshotRetryCount < Mathf.Max(1, profile.snapshotRequestRetryCount))
            {
                RequestSnapshotIfNeeded(false);
            }
        }

        private void RequestSnapshotIfNeeded(bool force)
        {
            if (!HasActivePeerTarget())
            {
                hasSnapshot = true;
                return;
            }

            var now = Time.unscaledTime;
            if (!force && now < snapshotCooldownUntil)
            {
                return;
            }

            foreach (var endpoint in GetActivePeerTargets())
            {
                if (TrySendToLocalPeer(endpoint.name, RequestSnapshotAddress, profile.nodeId, canvasId, registryHash))
                {
                    continue;
                }

                SendTo(endpoint.ipAddress, endpoint.port, RequestSnapshotAddress, profile.nodeId, canvasId, registryHash);
            }

            snapshotRetryCount++;
            nextSnapshotRequestTime = now + Mathf.Max(0.1f, profile.snapshotRequestIntervalSeconds);
            snapshotCooldownUntil = now + Mathf.Max(0.1f, profile.snapshotRetryCooldownSeconds);
        }

        private void TickSnapshotCleanup(float now)
        {
            if (activeSnapshotIds.Count == 0)
            {
                return;
            }

            expiredSnapshotIds.Clear();
            foreach (var pair in activeSnapshotIds)
            {
                if (pair.Value <= now)
                {
                    expiredSnapshotIds.Add(pair.Key);
                }
            }

            foreach (var snapshotId in expiredSnapshotIds)
            {
                activeSnapshotIds.Remove(snapshotId);
                if (profile.verboseLog)
                {
                    Debug.LogWarning("CanvasUiSync snapshot timeout cleanup: " + snapshotId + " canvas=" + canvasId, this);
                }
            }

            expiredSnapshotIds.Clear();
        }

        private void TickPeriodicResync(float now)
        {
            if (profile.periodicFullResyncIntervalSeconds <= 0f || now < nextPeriodicResyncTime)
            {
                return;
            }

            hasSnapshot = false;
            snapshotRetryCount = 0;
            RequestSnapshotIfNeeded(true);
            nextPeriodicResyncTime = now + profile.periodicFullResyncIntervalSeconds;
        }

        private void TickStatisticsLog(float now)
        {
            if (!profile.enableStatisticsLog || now < nextStatisticsLogTime)
            {
                return;
            }

            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);
            Debug.Log("CanvasUiSync stats: canvas=" + canvasId + " sentMessages=" + sentMessageCount + " receivedMessages=" + receivedMessageCount + " sentValues=" + sentValueCount + " receivedValues=" + receivedValueCount + " sentBytes~=" + sentApproxBytes + " receivedBytes~=" + receivedApproxBytes + " gc0Delta=" + (gc0 - lastGcCollectionCount0) + " gc1Delta=" + (gc1 - lastGcCollectionCount1) + " gc2Delta=" + (gc2 - lastGcCollectionCount2), this);
            sentMessageCount = 0;
            receivedMessageCount = 0;
            sentValueCount = 0;
            receivedValueCount = 0;
            sentApproxBytes = 0;
            receivedApproxBytes = 0;
            lastGcCollectionCount0 = gc0;
            lastGcCollectionCount1 = gc1;
            lastGcCollectionCount2 = gc2;
            nextStatisticsLogTime = now + profile.statisticsLogIntervalSeconds;
        }

        private void SendHello()
        {
            foreach (var endpoint in GetActivePeerTargets())
            {
                if (TrySendToLocalPeer(endpoint.name, HelloAddress, profile.nodeId, profile.protocolVersion, canvasId, sessionId))
                {
                    continue;
                }

                SendTo(endpoint.ipAddress, endpoint.port, HelloAddress, profile.nodeId, profile.protocolVersion, canvasId, sessionId);
            }
        }

        private IEnumerable<CanvasUiSyncRemoteEndpoint> GetActivePeerTargets()
        {
            if (profile.peerEndpoints == null)
            {
                yield break;
            }

            foreach (var endpoint in profile.peerEndpoints)
            {
                if (IsPeerTargetActive(endpoint))
                {
                    yield return endpoint;
                }
            }
        }

        private CanvasUiSyncRemoteEndpoint FindPeerTarget(string nodeId)
        {
            if (profile.peerEndpoints == null)
            {
                return null;
            }

            foreach (var endpoint in profile.peerEndpoints)
            {
                if (IsPeerTargetActive(endpoint) && string.Equals(endpoint.name, nodeId, StringComparison.Ordinal))
                {
                    return endpoint;
                }
            }

            return null;
        }

        private void TickNodeTimeout(float now)
        {
            expiredNodeIds.Clear();
            foreach (var node in nodes.Values)
            {
                if (now - node.LastSeenAt > Mathf.Max(0.1f, profile.nodeTimeoutSeconds))
                {
                    expiredNodeIds.Add(node.NodeId);
                }
            }

            foreach (var nodeId in expiredNodeIds)
            {
                nodes.Remove(nodeId);
                Debug.LogWarning("CanvasUiSync peer leave: " + nodeId + " canvas=" + canvasId, this);
            }

            expiredNodeIds.Clear();
        }

        private void FlushPendingCommits(float now)
        {
            foreach (var pair in localStates)
            {
                var state = pair.Value;
                if (!state.HasPendingBroadcast || now < state.NextBroadcastAt)
                {
                    continue;
                }

                BroadcastCommit(pair.Key, state.ValueType, state.PendingValue, state.PendingStamp);
                state.HasPendingBroadcast = false;
                state.LastBroadcastAt = now;
            }
        }

        private void SendSnapshot(CanvasUiSync target)
        {
            SendSnapshotCore(values => target.HandleBeginSnapshot(values), values => target.HandleSnapshotState(values), values => target.HandleEndSnapshot(values));
        }

        private void SendSnapshot(string ipAddress, int port)
        {
            SendSnapshotCore(values => SendTo(ipAddress, port, BeginSnapshotAddress, values), values => SendTo(ipAddress, port, SnapshotStateAddress, values), values => SendTo(ipAddress, port, EndSnapshotAddress, values));
        }

        private void SendSnapshotCore(Action<object[]> sendBegin, Action<object[]> sendState, Action<object[]> sendEnd)
        {
            var snapshotId = Guid.NewGuid().ToString("N");
            sendBegin(new object[] { snapshotId, canvasId, profile.nodeId, sessionId });
            foreach (var values in EnumerateSnapshotStateValues(snapshotId))
            {
                sendState(values);
            }

            sendEnd(new object[] { snapshotId, canvasId, profile.nodeId, sessionId });
            Debug.Log("CanvasUiSync snapshot served: " + canvasId + " snapshotId=" + snapshotId, this);
        }

        private IEnumerable<object[]> EnumerateSnapshotStateValues(string snapshotId)
        {
            foreach (var pair in bindings)
            {
                if (pair.Value.ValueType != "Button" && localStates.TryGetValue(pair.Key, out var state))
                {
                    yield return new object[] { snapshotId, canvasId, pair.Key, pair.Value.ValueType, SerializeValue(state.Value, pair.Value.ValueType), state.Stamp.LogicalTicks, state.Stamp.NodeId, state.Stamp.Sequence };
                }
            }
        }

        private void BroadcastCommit(string syncId, string valueType, object value, StateStamp stamp)
        {
            foreach (var endpoint in GetActivePeerTargets())
            {
                if (TrySendToLocalPeer(endpoint.name, CommitStateAddress, profile.nodeId, sessionId, canvasId, syncId, valueType, SerializeValue(value, valueType), stamp.LogicalTicks, stamp.NodeId, stamp.Sequence))
                {
                    continue;
                }

                SendTo(endpoint.ipAddress, endpoint.port, CommitStateAddress, profile.nodeId, sessionId, canvasId, syncId, valueType, SerializeValue(value, valueType), stamp.LogicalTicks, stamp.NodeId, stamp.Sequence);
            }
        }

        private void BroadcastButton(string syncId, StateStamp stamp)
        {
            foreach (var endpoint in GetActivePeerTargets())
            {
                if (TrySendToLocalPeer(endpoint.name, CommitButtonAddress, profile.nodeId, sessionId, canvasId, syncId, stamp.LogicalTicks, stamp.NodeId, stamp.Sequence))
                {
                    continue;
                }

                SendTo(endpoint.ipAddress, endpoint.port, CommitButtonAddress, profile.nodeId, sessionId, canvasId, syncId, stamp.LogicalTicks, stamp.NodeId, stamp.Sequence);
            }
        }

        private bool IsPeerAuthorized(string nodeId)
        {
            return profile.allowDynamicPeerJoin || profile.allowedPeers == null || profile.allowedPeers.Count == 0 || profile.allowedPeers.Contains(nodeId);
        }

        private StateStamp CreateLocalStamp()
        {
            localSequence++;
            logicalTicks = Math.Max(logicalTicks + 1, 1);
            return new StateStamp(logicalTicks, profile.nodeId, localSequence);
        }

        private StateStamp ReadStamp(object[] values, int startIndex)
        {
            var incomingLogicalTicks = Convert.ToInt64(values[startIndex]);
            logicalTicks = Math.Max(logicalTicks, incomingLogicalTicks);
            return new StateStamp(incomingLogicalTicks, Convert.ToString(values[startIndex + 1]), Convert.ToInt32(values[startIndex + 2]));
        }

        private bool TryReadStamp(object[] values, int startIndex, out StateStamp stamp)
        {
            stamp = default;
            if (values == null || values.Length <= startIndex + 2)
            {
                return false;
            }

            try
            {
                stamp = ReadStamp(values, startIndex);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("CanvasUiSync ignored malformed stamp: " + exception.Message, this);
                return false;
            }
        }

        private static bool IsIncomingStampNewer(StateStamp current, StateStamp incoming)
        {
            if (incoming.LogicalTicks != current.LogicalTicks)
            {
                return incoming.LogicalTicks > current.LogicalTicks;
            }

            var nodeCompare = string.CompareOrdinal(incoming.NodeId, current.NodeId);
            if (nodeCompare != 0)
            {
                return nodeCompare > 0;
            }

            return incoming.Sequence > current.Sequence;
        }

        private void SendTo(string ipAddress, int port, string address, params object[] values)
        {
            if (profile.enableOscTransport && client != null && !string.IsNullOrWhiteSpace(ipAddress) && port > 0)
            {
                client.address = ipAddress;
                client.port = port;
                client.Send(address, values);
                sentMessageCount++;
                sentValueCount += values.Length;
                sentApproxBytes += EstimatePayloadBytes(address, values);
            }
        }

        private static long EstimatePayloadBytes(string address, object[] values)
        {
            var bytes = string.IsNullOrEmpty(address) ? 0 : address.Length;
            for (var index = 0; index < values.Length; index++)
            {
                var value = values[index];
                if (value == null)
                {
                    continue;
                }

                if (value is string text)
                {
                    bytes += text.Length * 2;
                }
                else
                {
                    bytes += 8;
                }
            }

            return bytes;
        }

        private object SerializeValue(object value, string valueType)
        {
            if (valueType == "Toggle")
            {
                return Convert.ToBoolean(value);
            }

            if (valueType == "Dropdown" || valueType == "TMP_Dropdown")
            {
                return Convert.ToInt32(value);
            }

            if (valueType == "Slider" || valueType == "Scrollbar")
            {
                return Convert.ToSingle(value);
            }

            return Convert.ToString(value);
        }

        private object DeserializeValue(object value, string valueType)
        {
            return SerializeValue(value, valueType);
        }

        private void HandleUnknownSyncId(string syncId)
        {
            if (profile.logUnknownSyncId)
            {
                Debug.LogWarning("CanvasUiSync unknown syncId: " + syncId, this);
            }
        }

        private void HandleTypeMismatch(string syncId, string localType, string remoteType)
        {
            if (profile.logTypeMismatch)
            {
                Debug.LogError("CanvasUiSync type mismatch: syncId=" + syncId + " local=" + localType + " remote=" + remoteType, this);
            }
        }

        private CanvasUiSync FindLocalPeerTarget(string nodeId)
        {
            foreach (var instance in ActiveInstances)
            {
                if (instance != null && instance != this && instance.initialized && instance.profile != null && string.Equals(instance.profile.nodeId, nodeId, StringComparison.Ordinal) && string.Equals(instance.canvasId, canvasId, StringComparison.Ordinal))
                {
                    return instance;
                }
            }

            return null;
        }

        private bool TrySendToLocalPeer(string nodeId, string address, params object[] values)
        {
            var target = FindLocalPeerTarget(nodeId);
            if (target == null)
            {
                return false;
            }

            target.ReceiveLocalMessage(address, values);
            sentMessageCount++;
            sentValueCount += values.Length;
            sentApproxBytes += EstimatePayloadBytes(address, values);
            return true;
        }

        private void ReceiveLocalMessage(string address, object[] values)
        {
            HandleReceivedPayload(address, values);
        }

        private bool HasActivePeerTarget()
        {
            if (profile.peerEndpoints == null)
            {
                return false;
            }

            foreach (var endpoint in profile.peerEndpoints)
            {
                if (IsPeerTargetActive(endpoint))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPeerTargetActive(CanvasUiSyncRemoteEndpoint endpoint)
        {
            return endpoint != null && endpoint.enabled && endpoint.port > 0 && !string.IsNullOrWhiteSpace(endpoint.ipAddress) && !string.Equals(endpoint.name, profile.nodeId, StringComparison.Ordinal);
        }
    }
}
