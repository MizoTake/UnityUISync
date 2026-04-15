using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncStateService
    {
        internal static void InitializeLocalState(CanvasUiSync owner)
        {
            var previousLocalStates = new Dictionary<string, CanvasUiSync.LocalStateRecord>(owner.localStates);
            var previousButtonStamps = new Dictionary<string, CanvasUiSync.StateStamp>(owner.latestAppliedButtonStamps);
            owner.localStates.Clear();
            owner.latestAppliedButtonStamps.Clear();
            foreach (var pair in owner.bindings)
            {
                if (pair.Value.ValueType == "Button")
                {
                    if (previousButtonStamps.TryGetValue(pair.Key, out var stamp))
                    {
                        owner.latestAppliedButtonStamps[pair.Key] = stamp;
                    }

                    continue;
                }

                var currentValue = pair.Value.ReadValue();
                if (previousLocalStates.TryGetValue(pair.Key, out var existingState) && string.Equals(existingState.ValueType, pair.Value.ValueType, StringComparison.Ordinal))
                {
                    existingState.Value = currentValue;
                    existingState.PendingValue = currentValue;
                    owner.localStates[pair.Key] = existingState;
                    continue;
                }

                owner.localStates[pair.Key] = new CanvasUiSync.LocalStateRecord(currentValue, pair.Value.ValueType, default);
            }

            owner.PruneTransientStateCaches();
            owner.ApplyPendingRemoteCommits();
            owner.ApplyPendingRemoteButtonCommits();
        }

        internal static void OnLocalStateChanged(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, object value, bool force)
        {
            if (owner.suppressionCount > 0)
            {
                return;
            }

            if (binding.IsContinuous && !force)
            {
                var now = Time.unscaledTime;
                if (owner.lastProposeTimes.TryGetValue(binding.SyncId, out var lastProposeAt) && now - lastProposeAt < Mathf.Max(0f, owner.profile.minimumProposeIntervalSeconds))
                {
                    if (owner.profile.verboseLog)
                    {
                        Debug.Log("CanvasUiSync throttle propose: " + binding.SyncId, owner);
                    }

                    return;
                }

                if (owner.lastProposedValues.TryGetValue(binding.SyncId, out var lastValue) && Mathf.Abs(Convert.ToSingle(lastValue) - Convert.ToSingle(value)) < Mathf.Max(0f, owner.profile.sliderEpsilon))
                {
                    return;
                }
            }

            owner.lastProposeTimes[binding.SyncId] = Time.unscaledTime;
            owner.lastProposedValues[binding.SyncId] = value;
            owner.CommitLocalState(binding, value, false, owner.CreateLocalStamp());
        }

        internal static void OnLocalButtonClicked(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            if (owner.suppressionCount > 0)
            {
                return;
            }

            owner.CommitLocalButton(binding, owner.CreateLocalStamp());
        }

        internal static void OnInteractionStarted(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            binding.IsInteracting = true;
        }

        internal static void OnInteractionEnded(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            binding.IsInteracting = false;
            owner.OnLocalStateChanged(binding, binding.ReadValue(), true);
            if (owner.deferredCommits.TryGetValue(binding.SyncId, out var deferred))
            {
                owner.deferredCommits.Remove(binding.SyncId);
                owner.ApplyRemoteState(binding.SyncId, deferred.ValueType, deferred.Value, deferred.Stamp, false);
            }
        }

        internal static void UpdateContinuousInteractions(CanvasUiSync owner)
        {
            if (owner.bindings.Count == 0)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var eventCamera = owner.canvasComponent != null && owner.canvasComponent.renderMode != RenderMode.ScreenSpaceOverlay ? owner.canvasComponent.worldCamera : null;
                foreach (var binding in owner.bindings.Values)
                {
                    if (!binding.IsContinuous || binding.Component is not RectTransform rectTransform)
                    {
                        continue;
                    }

                    if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera))
                    {
                        owner.OnInteractionStarted(binding);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                foreach (var binding in owner.bindings.Values)
                {
                    if (binding.IsContinuous && binding.IsInteracting)
                    {
                        owner.OnInteractionEnded(binding);
                    }
                }
            }
        }

        internal static void UpdatePolledBindings(CanvasUiSync owner)
        {
            foreach (var binding in owner.bindings.Values)
            {
                if (!binding.RequiresPolling)
                {
                    continue;
                }

                var currentValue = binding.ReadValue();
                if (owner.localStates.TryGetValue(binding.SyncId, out var state) && AreEquivalent(state.Value, currentValue))
                {
                    continue;
                }

                owner.OnLocalStateChanged(binding, currentValue, false);
            }
        }

        internal static void CommitLocalState(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, object value, bool applyToLocalUi, CanvasUiSync.StateStamp stamp)
        {
            if (!owner.localStates.TryGetValue(binding.SyncId, out var state))
            {
                state = new CanvasUiSync.LocalStateRecord(value, binding.ValueType, stamp);
                owner.localStates[binding.SyncId] = state;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            if (applyToLocalUi)
            {
                owner.ApplyValueToBinding(binding, value);
            }

            var now = Time.unscaledTime;
            if (now - state.LastBroadcastAt >= Mathf.Max(0f, owner.profile.minimumCommitBroadcastIntervalSeconds))
            {
                owner.BroadcastCommit(binding.SyncId, binding.ValueType, value, stamp);
                state.LastBroadcastAt = now;
                state.HasPendingBroadcast = false;
            }
            else
            {
                state.HasPendingBroadcast = true;
                state.NextBroadcastAt = state.LastBroadcastAt + Mathf.Max(0f, owner.profile.minimumCommitBroadcastIntervalSeconds);
            }
        }

        internal static void CommitLocalButton(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, CanvasUiSync.StateStamp stamp)
        {
            owner.latestAppliedButtonStamps[binding.SyncId] = stamp;
            owner.BroadcastButton(binding.SyncId, stamp);
        }

        internal static void ApplyRemoteState(CanvasUiSync owner, string syncId, string valueType, object value, CanvasUiSync.StateStamp stamp, bool isSnapshot)
        {
            if (!owner.bindings.TryGetValue(syncId, out var binding) && (!owner.TryRefreshBindingsForSyncId(syncId) || !owner.bindings.TryGetValue(syncId, out binding)))
            {
                if (owner.pendingRemoteCommits.TryGetValue(syncId, out var existing) && !owner.IsIncomingStampNewer(existing.Stamp, stamp))
                {
                    return;
                }

                owner.pendingRemoteCommits[syncId] = new CanvasUiSync.DeferredStateCommit(valueType, value, stamp, Time.unscaledTime);
                owner.HandleUnknownSyncId(syncId);
                return;
            }

            if (!string.Equals(binding.ValueType, valueType, StringComparison.Ordinal))
            {
                owner.HandleTypeMismatch(syncId, binding.ValueType, valueType);
                return;
            }

            if (!owner.localStates.TryGetValue(syncId, out var state))
            {
                state = new CanvasUiSync.LocalStateRecord(value, valueType, stamp);
                owner.localStates[syncId] = state;
            }

            if (!owner.IsIncomingStampNewer(state.Stamp, stamp))
            {
                if (owner.profile.verboseLog)
                {
                    Debug.Log("CanvasUiSync stale state discard: " + syncId + " ticks=" + stamp.LogicalTicks + " node=" + stamp.NodeId, owner);
                }

                return;
            }

            if (binding.IsContinuous && binding.IsInteracting && !isSnapshot)
            {
                owner.deferredCommits[syncId] = new CanvasUiSync.DeferredStateCommit(valueType, value, stamp);
                return;
            }

            if (binding.RequiresPolling)
            {
                owner.ApplyValueToBinding(binding, value);
                var observedValue = binding.ReadValue();
                state.Value = observedValue;
                state.Stamp = stamp;
                state.PendingValue = observedValue;
                state.PendingStamp = stamp;
                return;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            owner.ApplyValueToBinding(binding, value);
        }

        internal static void FlushPendingCommits(CanvasUiSync owner, float now)
        {
            foreach (var pair in owner.localStates)
            {
                var state = pair.Value;
                if (!state.HasPendingBroadcast || now < state.NextBroadcastAt)
                {
                    continue;
                }

                owner.BroadcastCommit(pair.Key, state.ValueType, state.PendingValue, state.PendingStamp);
                state.HasPendingBroadcast = false;
                state.LastBroadcastAt = now;
            }
        }

        private static bool AreEquivalent(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            if (left is float || right is float)
            {
                return Mathf.Approximately(Convert.ToSingle(left), Convert.ToSingle(right));
            }

            return Equals(left, right);
        }
    }
}
