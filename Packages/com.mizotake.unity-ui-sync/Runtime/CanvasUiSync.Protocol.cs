using System;
using uOSC;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    public sealed partial class CanvasUiSync
    {
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
            if (ShouldIgnoreIncomingPeer(nodeId))
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
            if (ShouldIgnoreIncomingPeer(nodeId))
            {
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

            if (ShouldIgnoreIncomingPeer(Convert.ToString(values[2])))
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

            if (ShouldIgnoreIncomingPeer(Convert.ToString(values[2])))
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
            if (ShouldIgnoreIncomingPeer(senderNodeId))
            {
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
            if (ShouldIgnoreIncomingPeer(senderNodeId))
            {
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

        private bool IsPeerAuthorized(string nodeId)
        {
            return profile.allowDynamicPeerJoin || profile.allowedPeers == null || profile.allowedPeers.Count == 0 || profile.allowedPeers.Contains(nodeId);
        }

        private bool ShouldIgnoreIncomingPeer(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.Equals(nodeId, profile.nodeId, StringComparison.Ordinal))
            {
                return true;
            }

            if (IsPeerAuthorized(nodeId))
            {
                return false;
            }

            Debug.LogWarning("CanvasUiSync unauthorized peer reject: " + nodeId, this);
            return true;
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
    }
}
