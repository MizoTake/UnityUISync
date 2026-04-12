using System;
using System.Collections.Generic;
using uOSC;
using UnityEngine;

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
    }
}
