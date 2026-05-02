using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncTransportService
    {
        internal static void InitializeTransport(CanvasUiSync owner)
        {
            owner.server = owner.GetComponent<uOSC.uOscServer>();
            owner.client = owner.GetComponent<uOSC.uOscClient>();
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
                    owner.server = configuredTransportHost.GetComponent<uOSC.uOscServer>() ?? configuredTransportHost.AddComponent<uOSC.uOscServer>();
                }

                if (owner.client == null)
                {
                    owner.client = configuredTransportHost.GetComponent<uOSC.uOscClient>() ?? configuredTransportHost.AddComponent<uOSC.uOscClient>();
                }
            }

            owner.server.port = owner.profile.listenPort;
            owner.server.autoStart = true;
            owner.server.onDataReceived.RemoveListener(owner.OnOscMessageReceived);
            owner.server.onDataReceived.AddListener(owner.OnOscMessageReceived);
            owner.client.address = "127.0.0.1";
            owner.client.port = owner.profile.listenPort;
            if (configuredTransportHost != null)
            {
                configuredTransportHost.SetActive(true);
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
                return owner.transportHost;
            }

            owner.transportHost = new GameObject(CanvasUiSync.TransportHostName);
            owner.transportHost.hideFlags = HideFlags.HideInHierarchy;
            owner.transportHost.transform.SetParent(owner.transform, false);
            owner.transportHost.SetActive(false);
            return owner.transportHost;
        }

        internal static void TickSnapshotRetry(CanvasUiSync owner, float now)
        {
            if (!owner.syncEnabled)
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
            if (!owner.syncEnabled)
            {
                return;
            }

            if (!owner.HasActivePeerTarget())
            {
                owner.hasSnapshot = true;
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
                    owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.RequestSnapshotAddress, owner.profile.nodeId, owner.canvasId, owner.registryHash);
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
                owner.activeSnapshotIds.Remove(snapshotId);
                if (owner.ShouldVerboseLog())
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
            if (!owner.syncEnabled)
            {
                return;
            }

            if (owner.profile.periodicFullResyncIntervalSeconds <= 0f || now < owner.nextPeriodicResyncTime)
            {
                return;
            }

            owner.hasSnapshot = false;
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
            if (!owner.syncEnabled)
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
                    owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.HelloAddress, owner.profile.nodeId, owner.profile.protocolVersion, owner.canvasId, owner.sessionId);
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
                owner.nodes.Remove(nodeId);
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
            if (!owner.syncEnabled)
            {
                return;
            }

            var snapshotId = Guid.NewGuid().ToString("N");
            sendBegin(new object[] { snapshotId, owner.canvasId, owner.profile.nodeId, owner.sessionId });
            foreach (var values in owner.EnumerateSnapshotStateValues(snapshotId))
            {
                sendState(values);
            }

            sendEnd(new object[] { snapshotId, owner.canvasId, owner.profile.nodeId, owner.sessionId });
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

        internal static IEnumerable<object[]> EnumerateSnapshotStateValues(CanvasUiSync owner, string snapshotId)
        {
            foreach (var pair in owner.bindings)
            {
                if (pair.Value.ValueType != "Button" && owner.localStates.TryGetValue(pair.Key, out var state))
                {
                    yield return new object[] { snapshotId, owner.canvasId, pair.Key, pair.Value.ValueType, owner.SerializeValue(state.Value, pair.Value.ValueType), SerializeLogicalTicks(state.Stamp.LogicalTicks), state.Stamp.NodeId, state.Stamp.Sequence };
                }
            }
        }

        internal static void BroadcastCommit(CanvasUiSync owner, string syncId, string valueType, object value, CanvasUiSync.StateStamp stamp)
        {
            if (!owner.syncEnabled)
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
                    owner.SendTo(endpoint.ipAddress, endpoint.port, CanvasUiSync.CommitStateAddress, owner.profile.nodeId, owner.sessionId, owner.canvasId, syncId, valueType, owner.SerializeValue(value, valueType), SerializeLogicalTicks(stamp.LogicalTicks), stamp.NodeId, stamp.Sequence);
                }
            }
        }

        internal static void BroadcastButton(CanvasUiSync owner, string syncId, CanvasUiSync.StateStamp stamp)
        {
            if (!owner.syncEnabled)
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
            return endpoint != null && endpoint.enabled && endpoint.port > 0 && !string.IsNullOrWhiteSpace(endpoint.ipAddress) && !string.Equals(endpoint.name, owner.profile.nodeId, StringComparison.Ordinal);
        }

        internal static void SendTo(CanvasUiSync owner, string ipAddress, int port, string address, params object[] values)
        {
            if (owner.client != null && !string.IsNullOrWhiteSpace(ipAddress) && port > 0)
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
