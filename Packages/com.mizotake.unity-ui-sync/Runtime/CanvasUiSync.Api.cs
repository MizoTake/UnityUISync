using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    public sealed partial class CanvasUiSync
    {
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncStatus> synchronizationStateChangedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncStatus>();
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncBindingsEvent> bindingsRefreshedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncBindingsEvent>();
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncPeerEvent> peerStatusChangedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncPeerEvent>();
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncStateEvent> stateAppliedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncStateEvent>();
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncButtonEvent> buttonInvokedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncButtonEvent>();
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncSnapshotEvent> snapshotStatusChangedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncSnapshotEvent>();
        private readonly CanvasUiSyncApiEvent<CanvasUiSyncDiagnosticEvent> diagnosticRaisedEvent = new CanvasUiSyncApiEvent<CanvasUiSyncDiagnosticEvent>();
        private int apiNotificationDepth;
        private bool apiStructuralOperationInProgress;
        private bool hasDeferredSyncEnabled;
        private bool deferredSyncEnabled;

        public event Action<CanvasUiSyncStatus> SynchronizationStateChanged
        {
            add { EnsureApiReadThread(); synchronizationStateChangedEvent.Add(value); }
            remove { EnsureApiReadThread(); synchronizationStateChangedEvent.Remove(value); }
        }

        public event Action<CanvasUiSyncBindingsEvent> BindingsRefreshed
        {
            add { EnsureApiReadThread(); bindingsRefreshedEvent.Add(value); }
            remove { EnsureApiReadThread(); bindingsRefreshedEvent.Remove(value); }
        }

        public event Action<CanvasUiSyncPeerEvent> PeerStatusChanged
        {
            add { EnsureApiReadThread(); peerStatusChangedEvent.Add(value); }
            remove { EnsureApiReadThread(); peerStatusChangedEvent.Remove(value); }
        }

        public event Action<CanvasUiSyncStateEvent> StateApplied
        {
            add { EnsureApiReadThread(); stateAppliedEvent.Add(value); }
            remove { EnsureApiReadThread(); stateAppliedEvent.Remove(value); }
        }

        public event Action<CanvasUiSyncButtonEvent> ButtonInvoked
        {
            add { EnsureApiReadThread(); buttonInvokedEvent.Add(value); }
            remove { EnsureApiReadThread(); buttonInvokedEvent.Remove(value); }
        }

        public event Action<CanvasUiSyncSnapshotEvent> SnapshotStatusChanged
        {
            add { EnsureApiReadThread(); snapshotStatusChangedEvent.Add(value); }
            remove { EnsureApiReadThread(); snapshotStatusChangedEvent.Remove(value); }
        }

        public event Action<CanvasUiSyncDiagnosticEvent> DiagnosticRaised
        {
            add { EnsureApiReadThread(); diagnosticRaisedEvent.Add(value); }
            remove { EnsureApiReadThread(); diagnosticRaisedEvent.Remove(value); }
        }

        public CanvasUiSyncProfile Profile { get { EnsureApiReadThread(); return profile; } }
        public string CanvasId { get { EnsureApiReadThread(); return canvasId; } }
        public string NodeId { get { EnsureApiReadThread(); return profile != null ? profile.nodeId : string.Empty; } }
        public string SessionId { get { EnsureApiReadThread(); return sessionId; } }
        public bool IsInitialized { get { EnsureApiReadThread(); return initialized; } }
        public bool HasSnapshot { get { EnsureApiReadThread(); return hasSnapshot; } }
        public int BindingCount { get { EnsureApiReadThread(); return bindings.Count; } }
        public int ConnectedPeerCount { get { EnsureApiReadThread(); return nodes.Count; } }
        internal bool HasDiagnosticSubscribers => diagnosticRaisedEvent.HasSubscribers;

        public CanvasUiSyncStatus GetStatus()
        {
            EnsureApiReadThread();
            return new CanvasUiSyncStatus(initialized, isActiveAndEnabled, syncEnabled, !transportRestartPending && (server == null || server.isRunning) && (client == null || client.isRunning), hasSnapshot, canvasId, profile != null ? profile.nodeId : string.Empty, sessionId, bindings.Count, nodes.Count);
        }

        public int CopyBindings(List<CanvasUiSyncBindingInfo> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
            EnsureApiReadThread();

            results.Clear();
            foreach (var binding in bindings.Values)
            {
                results.Add(new CanvasUiSyncBindingInfo(binding.SyncId, binding.ValueType, binding.Component, binding.IsContinuous, binding.RequiresPolling));
            }

            return results.Count;
        }

        public int CopyPeers(List<CanvasUiSyncPeerInfo> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
            EnsureApiReadThread();

            results.Clear();
            foreach (var node in nodes.Values)
            {
                results.Add(CreatePeerInfo(node));
            }

            return results.Count;
        }

        public CanvasUiSyncApiResult NotifyHierarchyChanged()
        {
            var validationResult = ValidateApiThreadAndInitialization();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            var now = Time.unscaledTime;
            nextHierarchyRescanTime = Mathf.Min(nextHierarchyRescanTime, now);
            if (HasPendingMissingBinding())
            {
                ArmPendingBindingDiscovery(now);
                nextHierarchyRescanTime = Mathf.Min(nextHierarchyRescanTime, now);
            }
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult RefreshBindingsNow()
        {
            var validationResult = ValidateApiThreadAndInitialization();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            if (apiNotificationDepth > 0 || apiStructuralOperationInProgress)
            {
                return CanvasUiSyncApiResult.Busy;
            }

            RefreshBindingsIfHierarchyChanged(true);
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult TrySetValue(string syncId, object value)
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            if (string.IsNullOrWhiteSpace(syncId))
            {
                return CanvasUiSyncApiResult.InvalidArgument;
            }

            if (!bindings.TryGetValue(syncId, out var binding))
            {
                return CanvasUiSyncApiResult.BindingNotFound;
            }

            if (string.Equals(binding.ValueType, "Button", StringComparison.Ordinal))
            {
                return CanvasUiSyncApiResult.TypeMismatch;
            }
            if (IsComponentExcluded(binding.Component) || IsSyncIdExcluded(binding.SyncId))
            {
                return CanvasUiSyncApiResult.Excluded;
            }

            object convertedValue;
            try
            {
                convertedValue = DeserializeValue(value, binding.ValueType);
            }
            catch (Exception exception)
            {
                NotifyDiagnosticRaised(CanvasUiSyncDiagnosticCode.TypeMismatch, exception.Message, syncId, NodeId);
                return CanvasUiSyncApiResult.TypeMismatch;
            }

            ApplyValueToBinding(binding, convertedValue);
            var appliedValue = binding.ReadValue();
            OnLocalStateChanged(binding, appliedValue, true);
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult TrySetValue(Component component, object value)
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            if (component == null)
            {
                return CanvasUiSyncApiResult.InvalidArgument;
            }

            foreach (var binding in bindings.Values)
            {
                if (binding.Component == component)
                {
                    return TrySetValue(binding.SyncId, value);
                }
            }
            return CanvasUiSyncApiResult.BindingNotFound;
        }

        public bool TryGetBindingInfo(string syncId, out CanvasUiSyncBindingInfo bindingInfo)
        {
            EnsureApiReadThread();
            if (!string.IsNullOrWhiteSpace(syncId) && bindings.TryGetValue(syncId, out var binding))
            {
                bindingInfo = new CanvasUiSyncBindingInfo(binding.SyncId, binding.ValueType, binding.Component, binding.IsContinuous, binding.RequiresPolling);
                return true;
            }

            bindingInfo = default;
            return false;
        }

        public bool TryGetSynchronizedValue(string syncId, out object value)
        {
            EnsureApiReadThread();
            value = null;
            if (string.IsNullOrWhiteSpace(syncId) || !localStates.TryGetValue(syncId, out var state))
            {
                return false;
            }

            value = state.Value;
            return true;
        }

        public CanvasUiSyncApiResult TryInvokeButton(string syncId)
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            if (string.IsNullOrWhiteSpace(syncId))
            {
                return CanvasUiSyncApiResult.InvalidArgument;
            }

            if (!bindings.TryGetValue(syncId, out var binding))
            {
                return CanvasUiSyncApiResult.BindingNotFound;
            }

            if (binding.Component is not Button button)
            {
                return CanvasUiSyncApiResult.TypeMismatch;
            }
            if (IsComponentExcluded(binding.Component) || IsSyncIdExcluded(binding.SyncId))
            {
                return CanvasUiSyncApiResult.Excluded;
            }

            if (!CanPublishLocalEvents())
            {
                return CanvasUiSyncApiResult.Busy;
            }

            button.onClick.Invoke();
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult TryInvokeButton(Button button)
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            if (button == null)
            {
                return CanvasUiSyncApiResult.InvalidArgument;
            }

            foreach (var binding in bindings.Values)
            {
                if (binding.Component == button)
                {
                    return TryInvokeButton(binding.SyncId);
                }
            }
            return CanvasUiSyncApiResult.BindingNotFound;
        }

        public CanvasUiSyncApiResult SendHelloNow()
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            if (!HasActivePeerTarget())
            {
                return CanvasUiSyncApiResult.NoPeerTarget;
            }

            SendHello();
            nextHelloTime = Time.unscaledTime + Mathf.Max(0.1f, profile.helloIntervalSeconds);
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult RequestSnapshotNow()
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            if (!HasActivePeerTarget())
            {
                return CanvasUiSyncApiResult.NoPeerTarget;
            }

            hasSnapshot = false;
            snapshotRetryCount = 0;
            snapshotCooldownUntil = 0f;
            AllowRequestedSnapshotFromNewerPeer();
            RequestSnapshotIfNeeded(true);
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult ResynchronizeNow()
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            ScheduleSynchronizationNow(Time.unscaledTime);
            var helloResult = SendHelloNow();
            if (helloResult != CanvasUiSyncApiResult.Succeeded)
            {
                return helloResult;
            }
            return RequestSnapshotNow();
        }

        public CanvasUiSyncApiResult SendSnapshotToPeer(string nodeId)
        {
            var validationResult = ValidateApiRuntimeOperation();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return CanvasUiSyncApiResult.InvalidArgument;
            }

            var endpoint = FindPeerTarget(nodeId);
            if (endpoint == null)
            {
                return CanvasUiSyncApiResult.PeerNotFound;
            }

            SendSnapshot(endpoint.ipAddress, endpoint.port);
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult ApplyProfile(CanvasUiSyncProfile newProfile)
        {
            var validationResult = ValidateApiThread();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            if (newProfile == null || string.IsNullOrWhiteSpace(newProfile.nodeId) || newProfile.listenPort <= 0 || newProfile.listenPort > 65535)
            {
                return CanvasUiSyncApiResult.InvalidArgument;
            }

            if (apiNotificationDepth > 0 || apiStructuralOperationInProgress)
            {
                return CanvasUiSyncApiResult.Busy;
            }

            profile = newProfile;
            if (!initialized)
            {
                apiMainThreadId = global::System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (!TryInitializeRuntime())
                {
                    return CanvasUiSyncApiResult.NotInitialized;
                }
                enabled = true;
                if (startCompleted)
                {
                    ScheduleSynchronizationNow(Time.unscaledTime);
                    if (syncEnabled && isActiveAndEnabled)
                    {
                        AllowRequestedSnapshotFromNewerPeer();
                        SendHello();
                        RequestSnapshotIfNeeded(true);
                    }
                }
                NotifySynchronizationStateChanged();
                return CanvasUiSyncApiResult.Succeeded;
            }

            ReconfigureRuntimeSession();
            return CanvasUiSyncApiResult.Succeeded;
        }

        public CanvasUiSyncApiResult SetCanvasId(string newCanvasId)
        {
            var validationResult = ValidateApiThreadAndInitialization();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            if (apiNotificationDepth > 0 || apiStructuralOperationInProgress)
            {
                return CanvasUiSyncApiResult.Busy;
            }

            canvasIdOverride = string.IsNullOrWhiteSpace(newCanvasId) ? string.Empty : newCanvasId.Trim();
            ReconfigureRuntimeSession();
            return CanvasUiSyncApiResult.Succeeded;
        }

        private void ReconfigureRuntimeSession()
        {
            var wasSyncEnabled = syncEnabled;
            syncEnabled = false;
            CanvasUiSyncProtocolService.CancelActiveSnapshotReception(this);
            ClearContinuousInteractionStates();
            apiNodeScratch.Clear();
            foreach (var node in nodes.Values)
            {
                apiNodeScratch.Add(node);
            }
            nodes.Clear();
            ignoredPeerSessions.Clear();
            latestSnapshotSequenceBySourceSession.Clear();
            pendingRemoteCommits.Clear();
            pendingRemoteButtonCommits.Clear();
            deferredCommits.Clear();
            localStates.Clear();
            latestAppliedButtonStamps.Clear();
            lastProposedValues.Clear();
            lastContinuousProposedValues.Clear();
            lastProposeTimes.Clear();
            nextPendingRemoteCommitCleanupTime = float.PositiveInfinity;
            sessionId = Guid.NewGuid().ToString("N");
            sessionStartedAtTicks = DateTime.UtcNow.Ticks;
            sessionStartedAtRealtime = Time.realtimeSinceStartup;
            canvasId = string.IsNullOrWhiteSpace(canvasIdOverride) ? gameObject.name : canvasIdOverride.Trim();
            hasSnapshot = false;
            snapshotRetryCount = 0;
            snapshotCooldownUntil = 0f;
            RefreshExclusionRules();
            RefreshBindingsIfHierarchyChanged(true);
            RestartTransport();
            syncEnabled = wasSyncEnabled;
            ScheduleSynchronizationNow(Time.unscaledTime);
            if (syncEnabled && isActiveAndEnabled && !transportRestartPending)
            {
                AllowRequestedSnapshotFromNewerPeer();
                SendHello();
                RequestSnapshotIfNeeded(true);
            }
            for (var index = 0; index < apiNodeScratch.Count; index++)
            {
                NotifyPeerStatusChanged(CanvasUiSyncPeerChangeKind.ConfigurationReset, apiNodeScratch[index]);
            }
            apiNodeScratch.Clear();
            NotifySynchronizationStateChanged();
        }

        private CanvasUiSyncApiResult ValidateApiRuntimeOperation()
        {
            var validationResult = ValidateApiThreadAndInitialization();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            if (!isActiveAndEnabled)
            {
                return CanvasUiSyncApiResult.Inactive;
            }
            if (apiStructuralOperationInProgress || transportRestartPending)
            {
                return CanvasUiSyncApiResult.Busy;
            }
            return syncEnabled ? CanvasUiSyncApiResult.Succeeded : CanvasUiSyncApiResult.SyncDisabled;
        }

        private CanvasUiSyncApiResult ValidateApiThreadAndInitialization()
        {
            var validationResult = ValidateApiThread();
            if (validationResult != CanvasUiSyncApiResult.Succeeded)
            {
                return validationResult;
            }
            return initialized ? CanvasUiSyncApiResult.Succeeded : CanvasUiSyncApiResult.NotInitialized;
        }

        private CanvasUiSyncApiResult ValidateApiThread()
        {
            return apiMainThreadId == 0 || apiMainThreadId == global::System.Threading.Thread.CurrentThread.ManagedThreadId ? CanvasUiSyncApiResult.Succeeded : CanvasUiSyncApiResult.WrongThread;
        }

        private void EnsureApiReadThread()
        {
            if (ValidateApiThread() != CanvasUiSyncApiResult.Succeeded)
            {
                throw new InvalidOperationException("CanvasUiSync public API must be called from the Unity main thread.");
            }
        }

        internal void NotifySynchronizationStateChanged()
        {
            InvokeApiEvent(synchronizationStateChangedEvent, GetStatus());
        }

        internal void NotifyBindingsRefreshed(int previousCount, string previousRegistryHash, string previousFullRegistryHash = null)
        {
            InvokeApiEvent(bindingsRefreshedEvent, new CanvasUiSyncBindingsEvent(previousCount, bindings.Count, previousRegistryHash, registryHash, previousFullRegistryHash, fullRegistryHash));
        }

        internal void NotifyPeerStatusChanged(CanvasUiSyncPeerChangeKind kind, NodeState node, string previousSessionId = null)
        {
            InvokeApiEvent(peerStatusChangedEvent, new CanvasUiSyncPeerEvent(kind, CreatePeerInfo(node), previousSessionId));
        }

        internal void NotifyStateApplied(UiSyncBinding binding, object value, CanvasUiSyncValueOrigin origin, StateStamp stamp)
        {
            InvokeApiEvent(stateAppliedEvent, new CanvasUiSyncStateEvent(binding.SyncId, binding.ValueType, value, binding.Component, origin, new CanvasUiSyncStamp(stamp.LogicalTicks, stamp.NodeId, stamp.Sequence)));
        }

        internal void NotifyButtonInvoked(UiSyncBinding binding, CanvasUiSyncValueOrigin origin, StateStamp stamp)
        {
            InvokeApiEvent(buttonInvokedEvent, new CanvasUiSyncButtonEvent(binding.SyncId, binding.Component, origin, new CanvasUiSyncStamp(stamp.LogicalTicks, stamp.NodeId, stamp.Sequence)));
        }

        internal void NotifySnapshotStatusChanged(CanvasUiSyncSnapshotStatus status, string snapshotId, SnapshotReceiveState state)
        {
            NotifySnapshotStatusChanged(new CanvasUiSyncSnapshotEvent(status, snapshotId, state != null ? state.SourceNodeId : string.Empty, state != null ? state.SourceSessionId : string.Empty, state != null ? state.ExpectedStateCount : -1, state != null ? state.ReceivedSyncIds.Count : 0, state != null ? state.PendingSyncIds.Count : 0, state != null ? state.RemoteRegistryHash : string.Empty, state != null ? state.RemoteFullRegistryHash : string.Empty, state == null || state.IsRegistryCompatible(registryHash, fullRegistryHash, HasConfiguredExclusions())));
        }

        internal void NotifySnapshotStatusChanged(CanvasUiSyncSnapshotEvent value)
        {
            InvokeApiEvent(snapshotStatusChangedEvent, value);
        }

        internal void NotifyDiagnosticRaised(CanvasUiSyncDiagnosticCode code, string message, string syncId = null, string nodeId = null)
        {
            InvokeApiEvent(diagnosticRaisedEvent, new CanvasUiSyncDiagnosticEvent(code, message, syncId, nodeId));
        }

        private void InvokeApiEvent<T>(CanvasUiSyncApiEvent<T> targetEvent, T value)
        {
            apiNotificationDepth++;
            try
            {
                targetEvent.Invoke(value, this);
            }
            finally
            {
                apiNotificationDepth = Mathf.Max(0, apiNotificationDepth - 1);
            }
        }

        private static CanvasUiSyncPeerInfo CreatePeerInfo(NodeState node)
        {
            return node == null ? default : new CanvasUiSyncPeerInfo(node.NodeId, node.SessionId, node.LastSeenAt, node.RegistryHash, node.FullRegistryHash, node.HasExclusions);
        }
    }

    internal sealed class CanvasUiSyncApiEvent<T>
    {
        private Action<T>[] handlers = Array.Empty<Action<T>>();
        internal bool HasSubscribers => handlers.Length > 0;

        internal void Add(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            var previous = handlers;
            var next = new Action<T>[previous.Length + 1];
            Array.Copy(previous, next, previous.Length);
            next[previous.Length] = handler;
            handlers = next;
        }

        internal void Remove(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            var previous = handlers;
            for (var index = previous.Length - 1; index >= 0; index--)
            {
                if (previous[index] != handler)
                {
                    continue;
                }

                var next = new Action<T>[previous.Length - 1];
                if (index > 0)
                {
                    Array.Copy(previous, 0, next, 0, index);
                }
                if (index < previous.Length - 1)
                {
                    Array.Copy(previous, index + 1, next, index, previous.Length - index - 1);
                }
                handlers = next;
                return;
            }
        }

        internal void Invoke(T value, UnityEngine.Object context)
        {
            var current = handlers;
            for (var index = 0; index < current.Length; index++)
            {
                try
                {
                    current[index](value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, context);
                }
            }
        }
    }
}
