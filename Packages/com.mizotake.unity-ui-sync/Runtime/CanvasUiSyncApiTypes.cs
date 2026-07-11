using UnityEngine;

namespace Mizotake.UnityUiSync
{
    public enum CanvasUiSyncApiResult
    {
        Succeeded = 0,
        InvalidArgument = 1,
        NotInitialized = 2,
        Inactive = 3,
        SyncDisabled = 4,
        Busy = 5,
        BindingNotFound = 6,
        TypeMismatch = 7,
        NoPeerTarget = 8,
        PeerNotFound = 9,
        WrongThread = 10,
        Excluded = 11
    }

    public enum CanvasUiSyncValueOrigin
    {
        Local = 0,
        RemoteCommit = 1,
        RemoteSnapshot = 2
    }

    public enum CanvasUiSyncPeerChangeKind
    {
        Joined = 0,
        SessionChanged = 1,
        TimedOut = 2,
        ConfigurationReset = 3
    }

    public enum CanvasUiSyncSnapshotStatus
    {
        Started = 0,
        Completed = 1,
        TimedOut = 2,
        RegistryMismatch = 3,
        Cancelled = 4
    }

    public enum CanvasUiSyncDiagnosticCode
    {
        UnknownSyncId = 0,
        TypeMismatch = 1,
        DuplicateSyncId = 2,
        RegistryMismatch = 3,
        MalformedPayload = 4,
        MalformedStamp = 5,
        UnauthorizedPeer = 6,
        ProtocolVersionMismatch = 7,
        SnapshotTargetMissing = 8
    }

    public readonly struct CanvasUiSyncStatus
    {
        public CanvasUiSyncStatus(bool initialized, bool active, bool syncEnabled, bool transportReady, bool hasSnapshot, string canvasId, string nodeId, string sessionId, int bindingCount, int connectedPeerCount)
        {
            Initialized = initialized;
            Active = active;
            SyncEnabled = syncEnabled;
            TransportReady = transportReady;
            HasSnapshot = hasSnapshot;
            CanvasId = canvasId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            BindingCount = bindingCount;
            ConnectedPeerCount = connectedPeerCount;
        }

        public bool Initialized { get; }
        public bool Active { get; }
        public bool SyncEnabled { get; }
        public bool TransportReady { get; }
        public bool HasSnapshot { get; }
        public string CanvasId { get; }
        public string NodeId { get; }
        public string SessionId { get; }
        public int BindingCount { get; }
        public int ConnectedPeerCount { get; }
    }

    public readonly struct CanvasUiSyncBindingInfo
    {
        public CanvasUiSyncBindingInfo(string syncId, string valueType, Component component, bool continuous, bool requiresPolling)
        {
            SyncId = syncId ?? string.Empty;
            ValueType = valueType ?? string.Empty;
            Component = component;
            Continuous = continuous;
            RequiresPolling = requiresPolling;
        }

        public string SyncId { get; }
        public string ValueType { get; }
        public Component Component { get; }
        public bool Continuous { get; }
        public bool RequiresPolling { get; }
    }

    public readonly struct CanvasUiSyncPeerInfo
    {
        public CanvasUiSyncPeerInfo(string nodeId, string sessionId, float lastSeenAt, string registryHash, string fullRegistryHash, bool hasExclusions)
        {
            NodeId = nodeId ?? string.Empty;
            SessionId = sessionId ?? string.Empty;
            LastSeenAt = lastSeenAt;
            RegistryHash = registryHash ?? string.Empty;
            FullRegistryHash = fullRegistryHash ?? string.Empty;
            HasExclusions = hasExclusions;
        }

        public string NodeId { get; }
        public string SessionId { get; }
        public float LastSeenAt { get; }
        public string RegistryHash { get; }
        public string FullRegistryHash { get; }
        public bool HasExclusions { get; }
    }

    public readonly struct CanvasUiSyncPeerEvent
    {
        public CanvasUiSyncPeerEvent(CanvasUiSyncPeerChangeKind kind, CanvasUiSyncPeerInfo peer, string previousSessionId)
        {
            Kind = kind;
            Peer = peer;
            PreviousSessionId = previousSessionId ?? string.Empty;
        }

        public CanvasUiSyncPeerChangeKind Kind { get; }
        public CanvasUiSyncPeerInfo Peer { get; }
        public string PreviousSessionId { get; }
    }

    public readonly struct CanvasUiSyncStateEvent
    {
        public CanvasUiSyncStateEvent(string syncId, string valueType, object value, Component component, CanvasUiSyncValueOrigin origin, CanvasUiSyncStamp stamp)
        {
            SyncId = syncId ?? string.Empty;
            ValueType = valueType ?? string.Empty;
            Value = value;
            Component = component;
            Origin = origin;
            Stamp = stamp;
        }

        public string SyncId { get; }
        public string ValueType { get; }
        public object Value { get; }
        public Component Component { get; }
        public CanvasUiSyncValueOrigin Origin { get; }
        public CanvasUiSyncStamp Stamp { get; }
    }

    public readonly struct CanvasUiSyncButtonEvent
    {
        public CanvasUiSyncButtonEvent(string syncId, Component component, CanvasUiSyncValueOrigin origin, CanvasUiSyncStamp stamp)
        {
            SyncId = syncId ?? string.Empty;
            Component = component;
            Origin = origin;
            Stamp = stamp;
        }

        public string SyncId { get; }
        public Component Component { get; }
        public CanvasUiSyncValueOrigin Origin { get; }
        public CanvasUiSyncStamp Stamp { get; }
    }

    public readonly struct CanvasUiSyncStamp
    {
        public CanvasUiSyncStamp(long logicalTicks, string nodeId, int sequence)
        {
            LogicalTicks = logicalTicks;
            NodeId = nodeId ?? string.Empty;
            Sequence = sequence;
        }

        public long LogicalTicks { get; }
        public string NodeId { get; }
        public int Sequence { get; }
    }

    public readonly struct CanvasUiSyncBindingsEvent
    {
        public CanvasUiSyncBindingsEvent(int previousCount, int currentCount, string previousRegistryHash, string currentRegistryHash, string previousFullRegistryHash, string currentFullRegistryHash)
        {
            PreviousCount = previousCount;
            CurrentCount = currentCount;
            PreviousRegistryHash = previousRegistryHash ?? string.Empty;
            CurrentRegistryHash = currentRegistryHash ?? string.Empty;
            PreviousFullRegistryHash = previousFullRegistryHash ?? string.Empty;
            CurrentFullRegistryHash = currentFullRegistryHash ?? string.Empty;
        }

        public int PreviousCount { get; }
        public int CurrentCount { get; }
        public string PreviousRegistryHash { get; }
        public string CurrentRegistryHash { get; }
        public string PreviousFullRegistryHash { get; }
        public string CurrentFullRegistryHash { get; }
    }

    public readonly struct CanvasUiSyncSnapshotEvent
    {
        public CanvasUiSyncSnapshotEvent(CanvasUiSyncSnapshotStatus status, string snapshotId, string sourceNodeId, string sourceSessionId, int expectedStateCount, int receivedStateCount, int pendingStateCount, string remoteRegistryHash, string remoteFullRegistryHash, bool registryCompatible)
        {
            Status = status;
            SnapshotId = snapshotId ?? string.Empty;
            SourceNodeId = sourceNodeId ?? string.Empty;
            SourceSessionId = sourceSessionId ?? string.Empty;
            ExpectedStateCount = expectedStateCount;
            ReceivedStateCount = receivedStateCount;
            PendingStateCount = pendingStateCount;
            RemoteRegistryHash = remoteRegistryHash ?? string.Empty;
            RemoteFullRegistryHash = remoteFullRegistryHash ?? string.Empty;
            RegistryCompatible = registryCompatible;
        }

        public CanvasUiSyncSnapshotStatus Status { get; }
        public string SnapshotId { get; }
        public string SourceNodeId { get; }
        public string SourceSessionId { get; }
        public int ExpectedStateCount { get; }
        public int ReceivedStateCount { get; }
        public int PendingStateCount { get; }
        public string RemoteRegistryHash { get; }
        public string RemoteFullRegistryHash { get; }
        public bool RegistryCompatible { get; }
    }

    public readonly struct CanvasUiSyncDiagnosticEvent
    {
        public CanvasUiSyncDiagnosticEvent(CanvasUiSyncDiagnosticCode code, string message, string syncId, string nodeId)
        {
            Code = code;
            Message = message ?? string.Empty;
            SyncId = syncId ?? string.Empty;
            NodeId = nodeId ?? string.Empty;
        }

        public CanvasUiSyncDiagnosticCode Code { get; }
        public string Message { get; }
        public string SyncId { get; }
        public string NodeId { get; }
    }
}
