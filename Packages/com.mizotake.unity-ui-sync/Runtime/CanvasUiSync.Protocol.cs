using System;
using uOSC;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncProtocolService
    {
        internal static void OnOscMessageReceived(CanvasUiSync owner, Message message)
        {
            owner.HandleReceivedPayload(message.address, message.values);
        }

        internal static void HandleReceivedPayload(CanvasUiSync owner, string address, object[] values)
        {
            if (values == null)
            {
                return;
            }

            owner.RecordReceivedPayload(address, values);
            owner.DispatchReceivedPayload(address, values);
        }

        internal static void RecordReceivedPayload(CanvasUiSync owner, string address, object[] values)
        {
            owner.receivedMessageCount++;
            owner.receivedValueCount += values.Length;
            owner.receivedApproxBytes += CanvasUiSync.EstimatePayloadBytes(address, values);
        }

        internal static void DispatchReceivedPayload(CanvasUiSync owner, string address, object[] values)
        {
            switch (address)
            {
                case CanvasUiSync.HelloAddress: owner.HandleHello(values); break;
                case CanvasUiSync.RequestSnapshotAddress: owner.HandleRequestSnapshot(values); break;
                case CanvasUiSync.BeginSnapshotAddress: owner.HandleBeginSnapshot(values); break;
                case CanvasUiSync.SnapshotStateAddress: owner.HandleSnapshotState(values); break;
                case CanvasUiSync.EndSnapshotAddress: owner.HandleEndSnapshot(values); break;
                case CanvasUiSync.ProposeStateAddress:
                case CanvasUiSync.CommitStateAddress:
                    owner.HandleCommitState(values);
                    break;
                case CanvasUiSync.ProposeButtonAddress:
                case CanvasUiSync.CommitButtonAddress:
                    owner.HandleCommitButton(values);
                    break;
            }
        }

        internal static void HandleHello(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 4 || !string.Equals(Convert.ToString(values[2]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = Convert.ToString(values[0]);
            var protocolVersion = Convert.ToInt32(values[1]);
            var incomingSessionId = Convert.ToString(values[3]);
            if (owner.ShouldIgnoreIncomingPeer(nodeId))
            {
                return;
            }

            if (protocolVersion != owner.profile.protocolVersion)
            {
                Debug.LogWarning("CanvasUiSync protocol version mismatch: local=" + owner.profile.protocolVersion + " remote=" + protocolVersion, owner);
            }

            if (owner.nodes.TryGetValue(nodeId, out var node))
            {
                if (!string.Equals(node.SessionId, incomingSessionId, StringComparison.Ordinal))
                {
                    node.SessionId = incomingSessionId;
                    owner.hasSnapshot = false;
                    owner.snapshotRetryCount = 0;
                    owner.RequestSnapshotIfNeeded(true);
                }

                node.LastSeenAt = Time.unscaledTime;
            }
            else
            {
                owner.nodes[nodeId] = new CanvasUiSync.NodeState(nodeId, incomingSessionId, Time.unscaledTime);
                Debug.Log("CanvasUiSync peer join: " + nodeId + " canvas=" + owner.canvasId, owner);
                owner.hasSnapshot = false;
                owner.snapshotRetryCount = 0;
                owner.RequestSnapshotIfNeeded(true);
            }
        }

        internal static void HandleRequestSnapshot(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 3 || !string.Equals(Convert.ToString(values[1]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = Convert.ToString(values[0]);
            var incomingRegistryHash = Convert.ToString(values[2]);
            if (owner.ShouldIgnoreIncomingPeer(nodeId))
            {
                return;
            }

            if (owner.profile.logRegistryHashMismatch && !string.Equals(incomingRegistryHash, owner.registryHash, StringComparison.Ordinal))
            {
                Debug.LogWarning("CanvasUiSync registryHash mismatch: local=" + owner.registryHash + " remote=" + incomingRegistryHash, owner);
            }

            var endpoint = owner.FindPeerTarget(nodeId);
            if (endpoint == null)
            {
                Debug.LogWarning("CanvasUiSync requestSnapshot target was not configured: " + nodeId, owner);
                return;
            }

            owner.SendSnapshot(endpoint.ipAddress, endpoint.port);
        }

        internal static void HandleBeginSnapshot(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 4 || !string.Equals(Convert.ToString(values[1]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            if (owner.ShouldIgnoreIncomingPeer(Convert.ToString(values[2])))
            {
                return;
            }

            owner.activeSnapshotIds[Convert.ToString(values[0])] = Time.unscaledTime + Mathf.Max(0.5f, owner.profile.snapshotStateTimeoutSeconds);
            Debug.Log("CanvasUiSync snapshot begin: " + Convert.ToString(values[0]) + " canvas=" + owner.canvasId, owner);
        }

        internal static void HandleSnapshotState(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 8)
            {
                return;
            }

            var snapshotId = Convert.ToString(values[0]);
            if (!owner.activeSnapshotIds.ContainsKey(snapshotId) || !string.Equals(Convert.ToString(values[1]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            if (!owner.TryReadStamp(values, 5, out var snapshotStamp))
            {
                return;
            }

            owner.ApplyRemoteState(Convert.ToString(values[2]), Convert.ToString(values[3]), owner.DeserializeValue(values[4], Convert.ToString(values[3])), snapshotStamp, true);
        }

        internal static void HandleEndSnapshot(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 4)
            {
                return;
            }

            if (owner.ShouldIgnoreIncomingPeer(Convert.ToString(values[2])))
            {
                return;
            }

            var snapshotId = Convert.ToString(values[0]);
            if (!owner.activeSnapshotIds.Remove(snapshotId) || !string.Equals(Convert.ToString(values[1]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            owner.hasSnapshot = true;
            owner.snapshotRetryCount = 0;
            Debug.Log("CanvasUiSync snapshot end: canvas=" + owner.canvasId, owner);
        }

        internal static void HandleCommitState(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 8 || !string.Equals(Convert.ToString(values[2]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var senderNodeId = Convert.ToString(values[0]);
            if (owner.ShouldIgnoreIncomingPeer(senderNodeId))
            {
                return;
            }

            if (!owner.TryReadStamp(values, 6, out var stateStamp))
            {
                return;
            }

            owner.ApplyRemoteState(Convert.ToString(values[3]), Convert.ToString(values[4]), owner.DeserializeValue(values[5], Convert.ToString(values[4])), stateStamp, false);
        }

        internal static void HandleCommitButton(CanvasUiSync owner, object[] values)
        {
            if (values.Length < 7 || !string.Equals(Convert.ToString(values[2]), owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var senderNodeId = Convert.ToString(values[0]);
            if (owner.ShouldIgnoreIncomingPeer(senderNodeId))
            {
                return;
            }

            var syncId = Convert.ToString(values[3]);
            if (!owner.TryReadStamp(values, 4, out var stamp))
            {
                return;
            }

            if (!owner.bindings.TryGetValue(syncId, out var binding) && (!owner.TryRefreshBindingsForSyncId(syncId) || !owner.bindings.TryGetValue(syncId, out binding)))
            {
                if (owner.pendingRemoteButtonCommits.TryGetValue(syncId, out var existing) && !owner.IsIncomingStampNewer(existing.Stamp, stamp))
                {
                    return;
                }

                owner.pendingRemoteButtonCommits[syncId] = new CanvasUiSync.PendingButtonCommit(stamp, Time.unscaledTime);
                owner.HandleUnknownSyncId(syncId);
                return;
            }

            owner.ApplyButtonCommit(binding, syncId, stamp);
            owner.pendingRemoteButtonCommits.Remove(syncId);
        }

        internal static bool ApplyButtonCommit(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, string syncId, CanvasUiSync.StateStamp stamp)
        {
            if (binding.Component is not Button button)
            {
                owner.HandleTypeMismatch(syncId, binding.ValueType, "Button");
                return false;
            }

            if (owner.latestAppliedButtonStamps.TryGetValue(syncId, out var lastStamp) && !owner.IsIncomingStampNewer(lastStamp, stamp))
            {
                if (owner.profile.verboseLog)
                {
                    Debug.Log("CanvasUiSync stale button discard: " + syncId + " ticks=" + stamp.LogicalTicks + " node=" + stamp.NodeId, owner);
                }

                return false;
            }

            owner.latestAppliedButtonStamps[syncId] = stamp;
            using (new CanvasUiSync.SuppressionScope(owner))
            {
                button.onClick.Invoke();
            }

            return true;
        }

        internal static bool IsPeerAuthorized(CanvasUiSync owner, string nodeId)
        {
            return owner.profile.allowDynamicPeerJoin || owner.profile.allowedPeers == null || owner.profile.allowedPeers.Count == 0 || owner.profile.allowedPeers.Contains(nodeId);
        }

        internal static bool ShouldIgnoreIncomingPeer(CanvasUiSync owner, string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || string.Equals(nodeId, owner.profile.nodeId, StringComparison.Ordinal))
            {
                return true;
            }

            if (IsPeerAuthorized(owner, nodeId))
            {
                return false;
            }

            Debug.LogWarning("CanvasUiSync unauthorized peer reject: " + nodeId, owner);
            return true;
        }

        internal static CanvasUiSync.StateStamp CreateLocalStamp(CanvasUiSync owner)
        {
            owner.localSequence++;
            owner.logicalTicks = Math.Max(owner.logicalTicks + 1, 1);
            return new CanvasUiSync.StateStamp(owner.logicalTicks, owner.profile.nodeId, owner.localSequence);
        }

        internal static CanvasUiSync.StateStamp ReadStamp(CanvasUiSync owner, object[] values, int startIndex)
        {
            var incomingLogicalTicks = Convert.ToInt64(values[startIndex]);
            owner.logicalTicks = Math.Max(owner.logicalTicks, incomingLogicalTicks);
            return new CanvasUiSync.StateStamp(incomingLogicalTicks, Convert.ToString(values[startIndex + 1]), Convert.ToInt32(values[startIndex + 2]));
        }

        internal static bool TryReadStamp(CanvasUiSync owner, object[] values, int startIndex, out CanvasUiSync.StateStamp stamp)
        {
            stamp = default;
            if (values == null || values.Length <= startIndex + 2)
            {
                return false;
            }

            try
            {
                stamp = owner.ReadStamp(values, startIndex);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("CanvasUiSync ignored malformed stamp: " + exception.Message, owner);
                return false;
            }
        }

        internal static bool IsIncomingStampNewer(CanvasUiSync.StateStamp current, CanvasUiSync.StateStamp incoming)
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

        internal static object SerializeValue(CanvasUiSync owner, object value, string valueType)
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

        internal static object DeserializeValue(CanvasUiSync owner, object value, string valueType)
        {
            return owner.SerializeValue(value, valueType);
        }

        internal static void HandleUnknownSyncId(CanvasUiSync owner, string syncId)
        {
            if (owner.profile.logUnknownSyncId)
            {
                Debug.LogWarning("CanvasUiSync unknown syncId: " + syncId, owner);
            }
        }

        internal static void HandleTypeMismatch(CanvasUiSync owner, string syncId, string localType, string remoteType)
        {
            if (owner.profile.logTypeMismatch)
            {
                Debug.LogError("CanvasUiSync type mismatch: syncId=" + syncId + " local=" + localType + " remote=" + remoteType, owner);
            }
        }
    }
}
