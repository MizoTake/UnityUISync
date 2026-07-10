using System;
using uOSC;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncProtocolService
    {
        private const float SessionUptimeComparisonToleranceSeconds = 0.25f;

        internal static void OnOscMessageReceived(CanvasUiSync owner, Message message)
        {
            owner.HandleReceivedPayload(message.address, message.values);
        }

        internal static void HandleReceivedPayload(CanvasUiSync owner, string address, object[] values)
        {
            if (!owner.CanProcessRuntimeEvents() || values == null)
            {
                return;
            }

            try
            {
                owner.RecordReceivedPayload(address, values);
                owner.DispatchReceivedPayload(address, values);
            }
            catch (Exception exception)
            {
                if (owner.ShouldDebugLog())
                {
                    LogMalformedPayload(owner, address, exception.Message);
                }
            }
        }

        internal static void RecordReceivedPayload(CanvasUiSync owner, string address, object[] values)
        {
            owner.receivedMessageCount++;
            if (!owner.ShouldStatisticsLog())
            {
                return;
            }

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
            if (!owner.syncEnabled)
            {
                return;
            }

            var incomingCanvasId = values.Length > 2 ? ReadString(values, 2) : null;
            if (values.Length < 4 || !string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = ReadString(values, 0);
            var protocolVersion = Convert.ToInt32(values[1]);
            var incomingSessionId = ReadString(values, 3);
            var incomingSessionStartedAtTicks = ReadOptionalInt64(values, 4);
            var incomingSessionUptimeSeconds = ReadOptionalSingle(values, 5, -1f);
            if (owner.ShouldIgnoreIncomingPeer(nodeId))
            {
                return;
            }

            if (owner.ShouldDebugLog() && protocolVersion != owner.profile.protocolVersion)
            {
                LogProtocolVersionMismatch(owner, protocolVersion);
            }

            if (owner.nodes.TryGetValue(nodeId, out var node))
            {
                if (!string.Equals(node.SessionId, incomingSessionId, StringComparison.Ordinal))
                {
                    node.SessionId = incomingSessionId;
                    node.SessionStartedAtTicks = incomingSessionStartedAtTicks;
                    node.SessionUptimeSeconds = incomingSessionUptimeSeconds;
                    SynchronizeWithHelloPeer(owner, nodeId, incomingSessionStartedAtTicks, incomingSessionUptimeSeconds);
                }

                node.LastSeenAt = Time.unscaledTime;
                if (incomingSessionStartedAtTicks > 0L)
                {
                    node.SessionStartedAtTicks = incomingSessionStartedAtTicks;
                }

                if (incomingSessionUptimeSeconds >= 0f)
                {
                    node.SessionUptimeSeconds = incomingSessionUptimeSeconds;
                }
            }
            else
            {
                owner.nodes[nodeId] = new CanvasUiSync.NodeState(nodeId, incomingSessionId, Time.unscaledTime, incomingSessionStartedAtTicks, incomingSessionUptimeSeconds);
                if (owner.ShouldDebugLog())
                {
                    LogPeerJoin(owner, nodeId);
                }
                SynchronizeWithHelloPeer(owner, nodeId, incomingSessionStartedAtTicks, incomingSessionUptimeSeconds);
            }
        }

        internal static void HandleRequestSnapshot(CanvasUiSync owner, object[] values)
        {
            if (!owner.syncEnabled)
            {
                return;
            }

            var incomingCanvasId = values.Length > 1 ? ReadString(values, 1) : null;
            if (values.Length < 3 || !string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = ReadString(values, 0);
            var incomingRegistryHash = ReadString(values, 2);
            if (owner.ShouldIgnoreIncomingPeer(nodeId))
            {
                return;
            }

            if (owner.ShouldDebugLog() && owner.profile.logRegistryHashMismatch && !string.Equals(incomingRegistryHash, owner.registryHash, StringComparison.Ordinal))
            {
                LogRegistryHashMismatch(owner, incomingRegistryHash);
            }

            var endpoint = owner.FindPeerTarget(nodeId);
            if (endpoint == null)
            {
                if (owner.ShouldDebugLog())
                {
                    LogRequestSnapshotTargetMissing(owner, nodeId);
                }
                return;
            }

            owner.SendSnapshot(endpoint.ipAddress, endpoint.port);
        }

        internal static void HandleBeginSnapshot(CanvasUiSync owner, object[] values)
        {
            if (!owner.syncEnabled)
            {
                return;
            }

            var incomingCanvasId = values.Length > 1 ? ReadString(values, 1) : null;
            if (values.Length < 4 || !string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = ReadString(values, 2);
            if (owner.ShouldIgnoreIncomingPeer(nodeId))
            {
                return;
            }

            var snapshotId = ReadString(values, 0);
            var incomingSessionId = ReadString(values, 3);
            var incomingSessionStartedAtTicks = ReadOptionalInt64(values, 4);
            var expectedStateCount = ReadOptionalInt32(values, 5, -1);
            var incomingSessionUptimeSeconds = ReadOptionalSingle(values, 6, -1f);
            var incomingSnapshotSequence = ReadOptionalInt32(values, 7, -1);
            var sourceCanInitializeLocalState = CanSnapshotInitializeLocalState(owner, nodeId, incomingSessionId, incomingSessionStartedAtTicks, incomingSessionUptimeSeconds);
            var acceptRequestedSnapshot = owner.CanAcceptRequestedSnapshotFromNewerPeer();
            if (!sourceCanInitializeLocalState && !acceptRequestedSnapshot)
            {
                return;
            }

            var sourceSessionKey = nodeId + "\n" + incomingSessionId;
            if (incomingSnapshotSequence >= 0 && owner.latestSnapshotSequenceBySourceSession.TryGetValue(sourceSessionKey, out var latestSnapshotSequence) && incomingSnapshotSequence <= latestSnapshotSequence)
            {
                return;
            }

            if (incomingSnapshotSequence >= 0)
            {
                owner.latestSnapshotSequenceBySourceSession[sourceSessionKey] = incomingSnapshotSequence;
            }

            var canInitializeLocalState = sourceCanInitializeLocalState || acceptRequestedSnapshot;
            ResetActiveSnapshotReception(owner);
            owner.activeSnapshotIds[snapshotId] = Time.unscaledTime + Mathf.Max(0.5f, owner.profile.snapshotStateTimeoutSeconds);
            owner.activeSnapshotCanInitializeLocalState[snapshotId] = canInitializeLocalState;
            owner.snapshotReceiveStates[snapshotId] = new CanvasUiSync.SnapshotReceiveState(nodeId, incomingSessionId, canInitializeLocalState, expectedStateCount);
            owner.hasSnapshot = false;
            if (owner.ShouldDebugLog())
            {
                LogSnapshotBegin(owner, snapshotId);
            }
        }

        internal static void HandleSnapshotState(CanvasUiSync owner, object[] values)
        {
            if (!owner.syncEnabled)
            {
                return;
            }

            if (values.Length < 8)
            {
                return;
            }

            var snapshotId = ReadString(values, 0);
            var incomingCanvasId = ReadString(values, 1);
            if (!owner.activeSnapshotIds.ContainsKey(snapshotId) || !owner.snapshotReceiveStates.TryGetValue(snapshotId, out var snapshotState) || !string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            if (!owner.TryReadStamp(values, 5, out var snapshotStamp))
            {
                return;
            }

            var syncId = ReadString(values, 2);
            var valueType = ReadString(values, 3);
            snapshotState.ReceivedSyncIds.Add(syncId);
            snapshotState.PendingSyncIds.Add(syncId);
            owner.ApplyRemoteState(syncId, valueType, owner.DeserializeValue(values[4], valueType), snapshotStamp, true, snapshotState.CanInitializeLocalState);
            if (owner.bindings.TryGetValue(syncId, out var binding) && string.Equals(binding.ValueType, valueType, StringComparison.Ordinal))
            {
                snapshotState.PendingSyncIds.Remove(syncId);
            }

            TryCompleteSnapshot(owner, snapshotId);
        }

        internal static void HandleEndSnapshot(CanvasUiSync owner, object[] values)
        {
            if (!owner.syncEnabled)
            {
                return;
            }

            if (values.Length < 4)
            {
                return;
            }

            var incomingCanvasId = ReadString(values, 1);
            if (!string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var nodeId = ReadString(values, 2);
            if (owner.ShouldIgnoreIncomingPeer(nodeId))
            {
                return;
            }

            var snapshotId = ReadString(values, 0);
            var incomingSessionId = ReadString(values, 3);
            if (!owner.activeSnapshotIds.ContainsKey(snapshotId) || !owner.snapshotReceiveStates.TryGetValue(snapshotId, out var snapshotState) || !string.Equals(snapshotState.SourceNodeId, nodeId, StringComparison.Ordinal) || !string.Equals(snapshotState.SourceSessionId, incomingSessionId, StringComparison.Ordinal))
            {
                return;
            }

            snapshotState.EndReceived = true;
            snapshotState.CompletionDeadline = Time.unscaledTime + Mathf.Max(0f, owner.profile.initialSyncPendingTimeoutSeconds);
            owner.activeSnapshotIds[snapshotId] = snapshotState.CompletionDeadline;
            TryCompleteSnapshot(owner, snapshotId);
        }

        internal static void HandleCommitState(CanvasUiSync owner, object[] values)
        {
            if (!owner.syncEnabled)
            {
                return;
            }

            var incomingCanvasId = values.Length > 2 ? ReadString(values, 2) : null;
            if (values.Length < 8 || !string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var senderNodeId = ReadString(values, 0);
            if (owner.ShouldIgnoreIncomingPeer(senderNodeId))
            {
                return;
            }

            if (!owner.TryReadStamp(values, 6, out var stateStamp))
            {
                return;
            }

            var syncId = ReadString(values, 3);
            var valueType = ReadString(values, 4);
            owner.ApplyRemoteState(syncId, valueType, owner.DeserializeValue(values[5], valueType), stateStamp, false);
        }

        internal static void HandleCommitButton(CanvasUiSync owner, object[] values)
        {
            if (!owner.syncEnabled)
            {
                return;
            }

            var incomingCanvasId = values.Length > 2 ? ReadString(values, 2) : null;
            if (values.Length < 7 || !string.Equals(incomingCanvasId, owner.canvasId, StringComparison.Ordinal))
            {
                return;
            }

            var senderNodeId = ReadString(values, 0);
            if (owner.ShouldIgnoreIncomingPeer(senderNodeId))
            {
                return;
            }

            var syncId = ReadString(values, 3);
            if (!owner.TryReadStamp(values, 4, out var stamp))
            {
                return;
            }

            if (!owner.bindings.TryGetValue(syncId, out var binding))
            {
                if (owner.pendingRemoteButtonCommits.TryGetValue(syncId, out var existing))
                {
                    if (!owner.IsIncomingStampNewer(existing.Stamp, stamp))
                    {
                        return;
                    }

                    owner.pendingRemoteButtonCommits[syncId] = new CanvasUiSync.PendingButtonCommit(stamp, Time.unscaledTime);
                    return;
                }

                if (!owner.TryRefreshBindingsForSyncId(syncId) || !owner.bindings.TryGetValue(syncId, out binding))
                {
                    owner.pendingRemoteButtonCommits[syncId] = new CanvasUiSync.PendingButtonCommit(stamp, Time.unscaledTime);
                    owner.HandleUnknownSyncId(syncId);
                    return;
                }
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
                if (owner.ShouldVerboseLog())
                {
                    LogStaleButtonDiscard(owner, syncId, stamp);
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

            if (owner.ShouldDebugLog())
            {
                LogUnauthorizedPeerReject(owner, nodeId);
            }
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
            return new CanvasUiSync.StateStamp(incomingLogicalTicks, ReadString(values, startIndex + 1), Convert.ToInt32(values[startIndex + 2]));
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
                if (owner.ShouldDebugLog())
                {
                    LogMalformedStamp(owner, exception.Message);
                }
                return false;
            }
        }

        internal static bool IsIncomingStampNewer(CanvasUiSync.StateStamp current, CanvasUiSync.StateStamp incoming)
        {
            if (IsDefaultStamp(current) && IsDefaultStamp(incoming))
            {
                return false;
            }

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

        private static bool IsDefaultStamp(CanvasUiSync.StateStamp stamp)
        {
            return stamp.LogicalTicks == 0L && stamp.Sequence == 0 && string.IsNullOrEmpty(stamp.NodeId);
        }

        internal static object SerializeValue(CanvasUiSync owner, object value, string valueType)
        {
            if (valueType == "Toggle" || valueType == "DropdownExpanded" || valueType == "TMP_DropdownExpanded")
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
            if (owner.ShouldDebugLog() && owner.profile.logUnknownSyncId)
            {
                LogUnknownSyncId(owner, syncId);
            }
        }

        internal static void HandleTypeMismatch(CanvasUiSync owner, string syncId, string localType, string remoteType)
        {
            if (owner.ShouldDebugLog() && owner.profile.logTypeMismatch)
            {
                LogTypeMismatch(owner, syncId, localType, remoteType);
            }
        }

        internal static void HandlePendingSnapshotStateApplied(CanvasUiSync owner, string syncId)
        {
            if (owner.snapshotReceiveStates.Count == 0)
            {
                return;
            }

            owner.snapshotIdScratch.Clear();
            foreach (var pair in owner.snapshotReceiveStates)
            {
                if (pair.Value.PendingSyncIds.Remove(syncId) && pair.Value.IsReady)
                {
                    owner.snapshotIdScratch.Add(pair.Key);
                }
            }

            for (var index = 0; index < owner.snapshotIdScratch.Count; index++)
            {
                CompleteSnapshot(owner, owner.snapshotIdScratch[index]);
            }

            owner.snapshotIdScratch.Clear();
        }

        internal static void HandleSnapshotTimeout(CanvasUiSync owner, string snapshotId)
        {
            if (!owner.snapshotReceiveStates.TryGetValue(snapshotId, out var snapshotState))
            {
                owner.activeSnapshotIds.Remove(snapshotId);
                owner.activeSnapshotCanInitializeLocalState.Remove(snapshotId);
                return;
            }

            foreach (var syncId in snapshotState.PendingSyncIds)
            {
                if (owner.pendingRemoteCommits.TryGetValue(syncId, out var pending) && pending.IsSnapshot && !IsPendingInAnotherSnapshot(owner, snapshotId, syncId))
                {
                    owner.pendingRemoteCommits.Remove(syncId);
                }
            }

            owner.snapshotReceiveStates.Remove(snapshotId);
            owner.activeSnapshotIds.Remove(snapshotId);
            owner.activeSnapshotCanInitializeLocalState.Remove(snapshotId);
            owner.hasSnapshot = false;

            if (owner.ShouldDebugLog())
            {
                var builder = owner.stringBuilderScratch;
                builder.Length = 0;
                builder.Append("CanvasUiSync initial snapshot wait timed out: ");
                builder.Append(snapshotId);
                builder.Append(" received=");
                builder.Append(snapshotState.ReceivedSyncIds.Count);
                builder.Append('/');
                builder.Append(snapshotState.ExpectedStateCount);
                builder.Append(" pendingUi=");
                builder.Append(snapshotState.PendingSyncIds.Count);
                Debug.LogWarning(builder.ToString(), owner);
                builder.Length = 0;
            }
        }

        private static void TryCompleteSnapshot(CanvasUiSync owner, string snapshotId)
        {
            if (owner.snapshotReceiveStates.TryGetValue(snapshotId, out var snapshotState) && snapshotState.IsReady)
            {
                CompleteSnapshot(owner, snapshotId);
            }
        }

        private static void ResetActiveSnapshotReception(CanvasUiSync owner)
        {
            owner.snapshotIdScratch.Clear();
            foreach (var pair in owner.pendingRemoteCommits)
            {
                if (pair.Value.IsSnapshot)
                {
                    owner.snapshotIdScratch.Add(pair.Key);
                }
            }

            for (var index = 0; index < owner.snapshotIdScratch.Count; index++)
            {
                owner.pendingRemoteCommits.Remove(owner.snapshotIdScratch[index]);
            }

            owner.snapshotIdScratch.Clear();
            owner.snapshotReceiveStates.Clear();
            owner.activeSnapshotIds.Clear();
            owner.activeSnapshotCanInitializeLocalState.Clear();
        }

        internal static void CancelActiveSnapshotReception(CanvasUiSync owner)
        {
            ResetActiveSnapshotReception(owner);
            owner.ClearRequestedSnapshotFromNewerPeer();
        }

        private static void CompleteSnapshot(CanvasUiSync owner, string snapshotId)
        {
            owner.snapshotReceiveStates.Remove(snapshotId);
            owner.activeSnapshotIds.Remove(snapshotId);
            owner.activeSnapshotCanInitializeLocalState.Remove(snapshotId);
            owner.hasSnapshot = owner.snapshotReceiveStates.Count == 0;
            if (owner.hasSnapshot)
            {
                owner.snapshotRetryCount = 0;
                owner.ClearRequestedSnapshotFromNewerPeer();
            }

            if (owner.ShouldDebugLog())
            {
                LogSnapshotEnd(owner);
            }
        }

        private static bool IsPendingInAnotherSnapshot(CanvasUiSync owner, string snapshotId, string syncId)
        {
            foreach (var pair in owner.snapshotReceiveStates)
            {
                if (!string.Equals(pair.Key, snapshotId, StringComparison.Ordinal) && pair.Value.PendingSyncIds.Contains(syncId))
                {
                    return true;
                }
            }

            return false;
        }

        private static void SynchronizeWithHelloPeer(CanvasUiSync owner, string nodeId, long incomingSessionStartedAtTicks, float incomingSessionUptimeSeconds)
        {
            if (IsLocalSessionOlder(owner, incomingSessionStartedAtTicks, incomingSessionUptimeSeconds))
            {
                var endpoint = owner.FindPeerTarget(nodeId);
                if (endpoint != null)
                {
                    owner.SendSnapshot(endpoint.ipAddress, endpoint.port);
                    owner.hasSnapshot = true;
                    owner.snapshotRetryCount = 0;
                    owner.ClearRequestedSnapshotFromNewerPeer();
                }

                return;
            }

            owner.hasSnapshot = false;
            owner.snapshotRetryCount = 0;
            owner.RequestSnapshotIfNeeded(true);
        }

        private static bool CanSnapshotInitializeLocalState(CanvasUiSync owner, string nodeId, string incomingSessionId, long incomingSessionStartedAtTicks, float incomingSessionUptimeSeconds)
        {
            var sourceStartedAtTicks = incomingSessionStartedAtTicks;
            var sourceUptimeSeconds = incomingSessionUptimeSeconds;
            if (sourceStartedAtTicks <= 0L && owner.nodes.TryGetValue(nodeId, out var node) && string.Equals(node.SessionId, incomingSessionId, StringComparison.Ordinal))
            {
                sourceStartedAtTicks = node.SessionStartedAtTicks;
            }

            if (sourceUptimeSeconds < 0f && owner.nodes.TryGetValue(nodeId, out var uptimeNode) && string.Equals(uptimeNode.SessionId, incomingSessionId, StringComparison.Ordinal))
            {
                sourceUptimeSeconds = uptimeNode.SessionUptimeSeconds;
            }

            if (sourceUptimeSeconds >= 0f)
            {
                var localUptimeSeconds = owner.GetSessionUptimeSeconds();
                if (Mathf.Abs(sourceUptimeSeconds - localUptimeSeconds) > SessionUptimeComparisonToleranceSeconds)
                {
                    return sourceUptimeSeconds > localUptimeSeconds;
                }
            }

            return sourceStartedAtTicks > 0L && owner.sessionStartedAtTicks > 0L && sourceStartedAtTicks < owner.sessionStartedAtTicks;
        }

        private static bool IsLocalSessionOlder(CanvasUiSync owner, long incomingSessionStartedAtTicks, float incomingSessionUptimeSeconds)
        {
            if (incomingSessionUptimeSeconds >= 0f)
            {
                var localUptimeSeconds = owner.GetSessionUptimeSeconds();
                if (Mathf.Abs(localUptimeSeconds - incomingSessionUptimeSeconds) > SessionUptimeComparisonToleranceSeconds)
                {
                    return localUptimeSeconds > incomingSessionUptimeSeconds;
                }
            }

            return incomingSessionStartedAtTicks > 0L && owner.sessionStartedAtTicks > 0L && incomingSessionStartedAtTicks >= owner.sessionStartedAtTicks;
        }

        private static long ReadOptionalInt64(object[] values, int index)
        {
            if (values == null || values.Length <= index || values[index] == null)
            {
                return 0L;
            }

            try
            {
                return Convert.ToInt64(values[index]);
            }
            catch
            {
                return 0L;
            }
        }

        private static int ReadOptionalInt32(object[] values, int index, int fallback)
        {
            if (values == null || values.Length <= index || values[index] == null)
            {
                return fallback;
            }

            try
            {
                return Mathf.Max(0, Convert.ToInt32(values[index]));
            }
            catch
            {
                return fallback;
            }
        }

        private static float ReadOptionalSingle(object[] values, int index, float fallback)
        {
            if (values == null || values.Length <= index || values[index] == null)
            {
                return fallback;
            }

            try
            {
                return Mathf.Max(0f, Convert.ToSingle(values[index]));
            }
            catch
            {
                return fallback;
            }
        }

        private static void LogMalformedPayload(CanvasUiSync owner, string address, string reason)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync ignored malformed payload: address=");
            builder.Append(address);
            builder.Append(" reason=");
            builder.Append(reason);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogProtocolVersionMismatch(CanvasUiSync owner, int remoteProtocolVersion)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync protocol version mismatch: local=");
            builder.Append(owner.profile.protocolVersion);
            builder.Append(" remote=");
            builder.Append(remoteProtocolVersion);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogPeerJoin(CanvasUiSync owner, string nodeId)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync peer join: ");
            builder.Append(nodeId);
            builder.Append(" canvas=");
            builder.Append(owner.canvasId);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogRegistryHashMismatch(CanvasUiSync owner, string incomingRegistryHash)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync registryHash mismatch: local=");
            builder.Append(owner.registryHash);
            builder.Append(" remote=");
            builder.Append(incomingRegistryHash);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogRequestSnapshotTargetMissing(CanvasUiSync owner, string nodeId)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync requestSnapshot target was not configured: ");
            builder.Append(nodeId);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogSnapshotBegin(CanvasUiSync owner, string snapshotId)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync snapshot begin: ");
            builder.Append(snapshotId);
            builder.Append(" canvas=");
            builder.Append(owner.canvasId);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogSnapshotEnd(CanvasUiSync owner)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync snapshot end: canvas=");
            builder.Append(owner.canvasId);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogStaleButtonDiscard(CanvasUiSync owner, string syncId, CanvasUiSync.StateStamp stamp)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync stale button discard: ");
            builder.Append(syncId);
            builder.Append(" ticks=");
            builder.Append(stamp.LogicalTicks);
            builder.Append(" node=");
            builder.Append(stamp.NodeId);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogUnauthorizedPeerReject(CanvasUiSync owner, string nodeId)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync unauthorized peer reject: ");
            builder.Append(nodeId);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogMalformedStamp(CanvasUiSync owner, string reason)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync ignored malformed stamp: ");
            builder.Append(reason);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogUnknownSyncId(CanvasUiSync owner, string syncId)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync unknown syncId: ");
            builder.Append(syncId);
            Debug.LogWarning(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogTypeMismatch(CanvasUiSync owner, string syncId, string localType, string remoteType)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync type mismatch: syncId=");
            builder.Append(syncId);
            builder.Append(" local=");
            builder.Append(localType);
            builder.Append(" remote=");
            builder.Append(remoteType);
            Debug.LogError(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static string ReadString(object[] values, int index)
        {
            return values[index] as string ?? Convert.ToString(values[index]);
        }
    }
}
