using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mizotake.UnityUiSync
{
    public sealed partial class CanvasUiSync
    {
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
    }
}
