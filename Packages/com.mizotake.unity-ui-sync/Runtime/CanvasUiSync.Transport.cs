using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Unity.Profiling;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncTransportService
    {
        internal const string SendSnapshotMarkerName = "CanvasUiSync.SendSnapshot";
        internal const string BroadcastCommitMarkerName = "CanvasUiSync.BroadcastCommit";
        internal const string SendToMarkerName = "CanvasUiSync.SendTo";
        private static readonly ProfilerMarker SendSnapshotMarker = new ProfilerMarker(SendSnapshotMarkerName);
        private static readonly ProfilerMarker BroadcastCommitMarker = new ProfilerMarker(BroadcastCommitMarkerName);
        private static readonly ProfilerMarker SendToMarker = new ProfilerMarker(SendToMarkerName);

        internal static void InitializeTransport(CanvasUiSync owner)
        {
            var attachedServer = owner.GetComponent<uOSC.uOscServer>();
            var attachedClient = owner.GetComponent<uOSC.uOscClient>();
            if (attachedServer != null && attachedClient != null && owner.ownsTransportHost && owner.transportHost != null)
            {
                owner.ReleaseOwnedTransport();
            }
            var previousServer = owner.server;
            var previousClient = owner.client;
            var previouslyOwnedServer = owner.ownsServer;
            var previouslyOwnedClient = owner.ownsClient;
            owner.server = attachedServer;
            owner.client = attachedClient;
            if (owner.server != null)
            {
                owner.ownsServer = false;
            }
            if (owner.client != null)
            {
                owner.ownsClient = false;
            }
            GameObject configuredTransportHost = null;
            if (owner.server == null || owner.client == null)
            {
                configuredTransportHost = owner.GetOrCreateTransportHost();
                if (configuredTransportHost.activeSelf)
                {
                    configuredTransportHost.SetActive(false);
                }

                if (owner.server == null)
                {
                    owner.server = configuredTransportHost.GetComponent<uOSC.uOscServer>();
                    if (owner.server == null)
                    {
                        owner.server = configuredTransportHost.AddComponent<uOSC.uOscServer>();
                        owner.ownsServer = true;
                    }
                    else
                    {
                        owner.ownsServer = previouslyOwnedServer && owner.server == previousServer;
                    }
                }

                if (owner.client == null)
                {
                    owner.client = configuredTransportHost.GetComponent<uOSC.uOscClient>();
                    if (owner.client == null)
                    {
                        owner.client = configuredTransportHost.AddComponent<uOSC.uOscClient>();
                        owner.ownsClient = true;
                    }
                    else
                    {
                        owner.ownsClient = previouslyOwnedClient && owner.client == previousClient;
                    }
                }
            }

            owner.server.port = owner.profile.listenPort;
            owner.server.autoStart = true;
            owner.SubscribeTransportListener();
            owner.client.address = "127.0.0.1";
            owner.client.port = owner.profile.listenPort;
            if (configuredTransportHost != null)
            {
                configuredTransportHost.SetActive(true);
            }
        }

        internal static void ReleaseOwnedTransport(CanvasUiSync owner)
        {
            if (owner.ownsTransportHost && owner.transportHost != null)
            {
                var ownedHost = owner.transportHost;
                if (owner.server != null && owner.server.gameObject == ownedHost)
                {
                    owner.server = null;
                }
                if (owner.client != null && owner.client.gameObject == ownedHost)
                {
                    owner.client = null;
                }
                DestroyOwnedObject(ownedHost);
                owner.transportHost = null;
            }
            else
            {
                if (owner.ownsServer && owner.server != null)
                {
                    var ownedServer = owner.server;
                    owner.server = null;
                    DestroyOwnedObject(ownedServer);
                }
                if (owner.ownsClient && owner.client != null)
                {
                    var ownedClient = owner.client;
                    owner.client = null;
                    DestroyOwnedObject(ownedClient);
                }
            }
            owner.ownsTransportHost = false;
            owner.ownsServer = false;
            owner.ownsClient = false;
        }

        private static void DestroyOwnedObject(UnityEngine.Object target)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        internal static void RestartTransport(CanvasUiSync owner)
        {
            var hadServer = owner.server != null;
            var hadClient = owner.client != null;
            var serverPortChanged = hadServer && owner.server.port != owner.profile.listenPort;
            owner.transportRestartPending = false;
            owner.UnsubscribeTransportListener();
            if (owner.server != null)
            {
                owner.server.StopServer();
            }
            if (owner.client != null)
            {
                owner.client.StopClient();
            }

            InitializeTransport(owner);
            if (hadServer && !serverPortChanged && owner.server.isActiveAndEnabled && !owner.server.isRunning)
            {
                owner.server.StartServer();
            }
            else if (hadServer && serverPortChanged && owner.server.isActiveAndEnabled)
            {
                owner.transportRestartPending = true;
            }
            if (hadClient && owner.client.isActiveAndEnabled && !owner.client.isRunning)
            {
                owner.client.StartClient();
            }
            if (owner.isActiveAndEnabled)
            {
                owner.SubscribeTransportListener();
            }
        }

        internal static GameObject GetOrCreateTransportHost(CanvasUiSync owner)
        {
            if (owner.transportHost != null)
            {
                return owner.transportHost;
            }

            var existingHost = owner.transform.Find(CanvasUiSync.TransportHostName);
            if (existingHost != null)
            {
                owner.transportHost = existingHost.gameObject;
                owner.ownsTransportHost = false;
                return owner.transportHost;
            }

            owner.transportHost = new GameObject(CanvasUiSync.TransportHostName);
            owner.ownsTransportHost = true;
            owner.transportHost.hideFlags = HideFlags.HideInHierarchy;
            owner.transportHost.transform.SetParent(owner.transform, false);
            owner.transportHost.SetActive(false);
            return owner.transportHost;
        }

        internal static void TickSnapshotRetry(CanvasUiSync owner, float now)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            if (!owner.hasSnapshot && now >= owner.snapshotCooldownUntil && now >= owner.nextSnapshotRequestTime && owner.snapshotRetryCount < Mathf.Max(1, owner.profile.snapshotRequestRetryCount))
            {
                owner.RequestSnapshotIfNeeded(false);
            }
        }

        internal static void RequestSnapshotIfNeeded(CanvasUiSync owner, bool force)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            if (!owner.HasActivePeerTarget())
            {
                owner.hasSnapshot = true;
                owner.ClearRequestedSnapshotFromNewerPeer();
                return;
            }

            var now = Time.unscaledTime;
            if (!force && now < owner.snapshotCooldownUntil)
            {
                return;
            }

            if (owner.profile.peerEndpoints == null)
            {
                return;
            }

            for (var index = 0; index < owner.profile.peerEndpoints.Count; index++)
            {
                var endpoint = owner.profile.peerEndpoints[index];
                if (IsPeerTargetActive(owner, endpoint))
                {
                    owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.RequestSnapshotAddress, owner.profile.nodeId, owner.canvasId, owner.registryHash, owner.sessionId);
                }
            }

            owner.snapshotRetryCount++;
            owner.nextSnapshotRequestTime = now + Mathf.Max(0.1f, owner.profile.snapshotRequestIntervalSeconds);
            owner.snapshotCooldownUntil = now + Mathf.Max(0.1f, owner.profile.snapshotRetryCooldownSeconds);
        }

        internal static void TickSnapshotCleanup(CanvasUiSync owner, float now)
        {
            if (owner.activeSnapshotIds.Count == 0)
            {
                return;
            }

            owner.expiredSnapshotIds.Clear();
            foreach (var pair in owner.activeSnapshotIds)
            {
                if (pair.Value <= now)
                {
                    owner.expiredSnapshotIds.Add(pair.Key);
                }
            }

            foreach (var snapshotId in owner.expiredSnapshotIds)
            {
                var timedOut = CanvasUiSyncProtocolService.HandleSnapshotTimeout(owner, snapshotId);
                if (timedOut && owner.ShouldVerboseLog())
                {
                    var builder = owner.stringBuilderScratch;
                    builder.Length = 0;
                    builder.Append("CanvasUiSync snapshot timeout cleanup: ");
                    builder.Append(snapshotId);
                    builder.Append(" canvas=");
                    builder.Append(owner.canvasId);
                    Debug.LogWarning(builder.ToString(), owner);
                    builder.Length = 0;
                }
            }

            owner.expiredSnapshotIds.Clear();
        }

        internal static void TickPeriodicResync(CanvasUiSync owner, float now)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            if (owner.profile.periodicFullResyncIntervalSeconds <= 0f || now < owner.nextPeriodicResyncTime)
            {
                return;
            }

            owner.hasSnapshot = false;
            owner.AllowRequestedSnapshotFromNewerPeer();
            owner.snapshotRetryCount = 0;
            owner.RequestSnapshotIfNeeded(true);
            owner.nextPeriodicResyncTime = now + owner.profile.periodicFullResyncIntervalSeconds;
        }

        internal static void TickStatisticsLog(CanvasUiSync owner, float now)
        {
            if (!owner.ShouldStatisticsLog() || now < owner.nextStatisticsLogTime)
            {
                return;
            }

            var gc0 = GC.CollectionCount(0);
            var gc1 = GC.CollectionCount(1);
            var gc2 = GC.CollectionCount(2);
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync stats: canvas=");
            builder.Append(owner.canvasId);
            builder.Append(" sentMessages=");
            builder.Append(owner.sentMessageCount);
            builder.Append(" receivedMessages=");
            builder.Append(owner.receivedMessageCount);
            builder.Append(" sentValues=");
            builder.Append(owner.sentValueCount);
            builder.Append(" receivedValues=");
            builder.Append(owner.receivedValueCount);
            builder.Append(" sentBytes~=");
            builder.Append(owner.sentApproxBytes);
            builder.Append(" receivedBytes~=");
            builder.Append(owner.receivedApproxBytes);
            builder.Append(" gc0Delta=");
            builder.Append(gc0 - owner.lastGcCollectionCount0);
            builder.Append(" gc1Delta=");
            builder.Append(gc1 - owner.lastGcCollectionCount1);
            builder.Append(" gc2Delta=");
            builder.Append(gc2 - owner.lastGcCollectionCount2);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
            owner.sentMessageCount = 0;
            owner.receivedMessageCount = 0;
            owner.sentValueCount = 0;
            owner.receivedValueCount = 0;
            owner.sentApproxBytes = 0;
            owner.receivedApproxBytes = 0;
            owner.lastGcCollectionCount0 = gc0;
            owner.lastGcCollectionCount1 = gc1;
            owner.lastGcCollectionCount2 = gc2;
            owner.nextStatisticsLogTime = now + owner.profile.statisticsLogIntervalSeconds;
        }

        internal static void SendHello(CanvasUiSync owner)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            if (owner.profile.peerEndpoints == null)
            {
                return;
            }

            for (var index = 0; index < owner.profile.peerEndpoints.Count; index++)
            {
                var endpoint = owner.profile.peerEndpoints[index];
                if (IsPeerEndpointConfigured(owner, endpoint))
                {
                    owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.HelloAddress, owner.profile.nodeId, owner.profile.protocolVersion, owner.canvasId, owner.sessionId, SerializeLogicalTicks(owner.sessionStartedAtTicks), owner.GetSessionUptimeSeconds(), owner.registryHash, owner.fullRegistryHash, owner.HasConfiguredExclusions() ? 1 : 0);
                }
            }
        }

        internal static IEnumerable<CanvasUiSyncRemoteEndpoint> GetActivePeerTargets(CanvasUiSync owner)
        {
            if (owner.profile.peerEndpoints == null)
            {
                yield break;
            }

            foreach (var endpoint in owner.profile.peerEndpoints)
            {
                if (IsPeerTargetActive(owner, endpoint))
                {
                    yield return endpoint;
                }
            }
        }

        internal static CanvasUiSyncRemoteEndpoint FindPeerTarget(CanvasUiSync owner, string nodeId)
        {
            if (owner.profile.peerEndpoints == null)
            {
                return null;
            }

            foreach (var endpoint in owner.profile.peerEndpoints)
            {
                if (IsPeerTargetActive(owner, endpoint) && string.Equals(endpoint.name, nodeId, StringComparison.Ordinal))
                {
                    return endpoint;
                }
            }

            return null;
        }

        internal static void TickNodeTimeout(CanvasUiSync owner, float now)
        {
            owner.expiredNodeIds.Clear();
            foreach (var node in owner.nodes.Values)
            {
                if (now - node.LastSeenAt > Mathf.Max(0.1f, owner.profile.nodeTimeoutSeconds))
                {
                    owner.expiredNodeIds.Add(node.NodeId);
                }
            }

            foreach (var nodeId in owner.expiredNodeIds)
            {
                owner.nodes.TryGetValue(nodeId, out var expiredNode);
                owner.nodes.Remove(nodeId);
                if (expiredNode != null)
                {
                    owner.NotifyPeerStatusChanged(CanvasUiSyncPeerChangeKind.TimedOut, expiredNode);
                }
                if (owner.ShouldDebugLog())
                {
                    var builder = owner.stringBuilderScratch;
                    builder.Length = 0;
                    builder.Append("CanvasUiSync peer leave: ");
                    builder.Append(nodeId);
                    builder.Append(" canvas=");
                    builder.Append(owner.canvasId);
                    Debug.LogWarning(builder.ToString(), owner);
                    builder.Length = 0;
                }
            }

            if (owner.expiredNodeIds.Count > 0 && owner.nodes.Count == 0)
            {
                owner.ClearRequestedSnapshotFromNewerPeer();
            }

            owner.expiredNodeIds.Clear();
        }

        internal static void SendSnapshot(CanvasUiSync owner, CanvasUiSync target)
        {
            owner.SendSnapshotCore(values => target.HandleBeginSnapshot(values), values => target.HandleSnapshotState(values), values => target.HandleEndSnapshot(values));
        }

        internal static void SendSnapshot(CanvasUiSync owner, string ipAddress, int port)
        {
            owner.SendSnapshotCore(values => owner.SendTo(ipAddress, port, CanvasUiSync.BeginSnapshotAddress, values), values => owner.SendTo(ipAddress, port, CanvasUiSync.SnapshotStateAddress, values), values => owner.SendTo(ipAddress, port, CanvasUiSync.EndSnapshotAddress, values));
        }

        internal static void SendSnapshotCore(CanvasUiSync owner, Action<object[]> sendBegin, Action<object[]> sendState, Action<object[]> sendEnd)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            using (SendSnapshotMarker.Auto())
            {
                var snapshotId = Guid.NewGuid().ToString("N");
                var snapshotStateCount = CountSnapshotStateValues(owner);
                owner.snapshotSequence = owner.snapshotSequence == int.MaxValue ? 1 : owner.snapshotSequence + 1;
                if (owner.client != null)
                {
                    owner.client.maxQueueSize = Mathf.Max(owner.client.maxQueueSize, snapshotStateCount + 16);
                }

                sendBegin(new object[] { snapshotId, owner.canvasId, owner.profile.nodeId, owner.sessionId, SerializeLogicalTicks(owner.sessionStartedAtTicks), snapshotStateCount, owner.GetSessionUptimeSeconds(), owner.snapshotSequence, owner.registryHash, owner.HasConfiguredExclusions() ? 1 : 0, owner.fullRegistryHash });
                foreach (var values in owner.EnumerateSnapshotStateValues(snapshotId))
                {
                    sendState(values);
                }

                sendEnd(new object[] { snapshotId, owner.canvasId, owner.profile.nodeId, owner.sessionId, SerializeLogicalTicks(owner.sessionStartedAtTicks) });
                if (owner.ShouldDebugLog())
                {
                    var builder = owner.stringBuilderScratch;
                    builder.Length = 0;
                    builder.Append("CanvasUiSync snapshot served: ");
                    builder.Append(owner.canvasId);
                    builder.Append(" snapshotId=");
                    builder.Append(snapshotId);
                    Debug.Log(builder.ToString(), owner);
                    builder.Length = 0;
                }
            }
        }

        private static int CountSnapshotStateValues(CanvasUiSync owner)
        {
            var count = 0;
            foreach (var pair in owner.bindings)
            {
                if (pair.Value.ValueType != "Button" && !owner.IsSyncIdExcluded(pair.Key) && owner.localStates.ContainsKey(pair.Key))
                {
                    count++;
                }
            }

            return count;
        }

        internal static IEnumerable<object[]> EnumerateSnapshotStateValues(CanvasUiSync owner, string snapshotId)
        {
            foreach (var pair in owner.bindings)
            {
                if (pair.Value.ValueType != "Button" && !owner.IsSyncIdExcluded(pair.Key) && owner.localStates.TryGetValue(pair.Key, out var state))
                {
                    yield return new object[] { snapshotId, owner.canvasId, pair.Key, pair.Value.ValueType, owner.SerializeValue(state.Value, pair.Value.ValueType), SerializeLogicalTicks(state.Stamp.LogicalTicks), state.Stamp.NodeId ?? string.Empty, state.Stamp.Sequence };
                }
            }
        }

        internal static void BroadcastCommit(CanvasUiSync owner, string syncId, string valueType, object value, CanvasUiSync.StateStamp stamp)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            using (BroadcastCommitMarker.Auto())
            {
                if (owner.profile.peerEndpoints == null)
                {
                    return;
                }

                for (var index = 0; index < owner.profile.peerEndpoints.Count; index++)
                {
                    var endpoint = owner.profile.peerEndpoints[index];
                    if (IsPeerTargetActive(owner, endpoint))
                    {
                        owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.CommitStateAddress, owner.profile.nodeId, owner.sessionId, owner.canvasId, syncId, valueType, owner.SerializeValue(value, valueType), SerializeLogicalTicks(stamp.LogicalTicks), stamp.NodeId, stamp.Sequence);
                    }
                }
            }
        }

        internal static void BroadcastButton(CanvasUiSync owner, string syncId, CanvasUiSync.StateStamp stamp)
        {
            if (!owner.CanProcessRuntimeEvents())
            {
                return;
            }

            if (owner.profile.peerEndpoints == null)
            {
                return;
            }

            for (var index = 0; index < owner.profile.peerEndpoints.Count; index++)
            {
                var endpoint = owner.profile.peerEndpoints[index];
                if (IsPeerTargetActive(owner, endpoint))
                {
                    owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.CommitButtonAddress, owner.profile.nodeId, owner.sessionId, owner.canvasId, syncId, SerializeLogicalTicks(stamp.LogicalTicks), stamp.NodeId, stamp.Sequence);
                }
            }
        }

        internal static bool HasActivePeerTarget(CanvasUiSync owner)
        {
            if (owner.profile.peerEndpoints == null)
            {
                return false;
            }

            foreach (var endpoint in owner.profile.peerEndpoints)
            {
                if (IsPeerTargetActive(owner, endpoint))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsPeerTargetActive(CanvasUiSync owner, CanvasUiSyncRemoteEndpoint endpoint)
        {
            return IsPeerEndpointConfigured(owner, endpoint) && !owner.HasIgnoredPeerSession(endpoint.name);
        }

        private static bool IsPeerEndpointConfigured(CanvasUiSync owner, CanvasUiSyncRemoteEndpoint endpoint)
        {
            return endpoint != null && endpoint.enabled && endpoint.port > 0 && !string.IsNullOrWhiteSpace(endpoint.ipAddress) && !string.Equals(endpoint.name, owner.profile.nodeId, StringComparison.Ordinal);
        }

        internal static void SendTo(CanvasUiSync owner, string ipAddress, int port, string address, params object[] values)
        {
            if (owner.CanProcessRuntimeEvents() && owner.client != null && !string.IsNullOrWhiteSpace(ipAddress) && port > 0)
            {
                using (SendToMarker.Auto())
                {
                    owner.client.address = ipAddress;
                    owner.client.port = port;
                    owner.client.Send(address, values);
                    owner.sentMessageCount++;
                    if (owner.ShouldStatisticsLog())
                    {
                        owner.sentValueCount += values.Length;
                        owner.sentApproxBytes += EstimatePayloadBytes(address, values);
                    }
                }
            }
        }

        internal static long EstimatePayloadBytes(string address, object[] values)
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

        internal static string SerializeLogicalTicks(long logicalTicks)
        {
            return logicalTicks.ToString(CultureInfo.InvariantCulture);
        }
    }
}
