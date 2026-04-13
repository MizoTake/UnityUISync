using System;
using UnityEngine;

namespace Mizotake.UnityUiSync
{
    public sealed partial class CanvasUiSync
    {
        private void OnLocalStateChanged(UiSyncBinding binding, object value, bool force)
        {
            if (suppressionCount > 0)
            {
                return;
            }

            if (binding.IsContinuous && !force)
            {
                var now = Time.unscaledTime;
                if (lastProposeTimes.TryGetValue(binding.SyncId, out var lastProposeAt) && now - lastProposeAt < Mathf.Max(0f, profile.minimumProposeIntervalSeconds))
                {
                    if (profile.verboseLog)
                    {
                        Debug.Log("CanvasUiSync throttle propose: " + binding.SyncId, this);
                    }

                    return;
                }

                if (lastProposedValues.TryGetValue(binding.SyncId, out var lastValue) && Mathf.Abs(Convert.ToSingle(lastValue) - Convert.ToSingle(value)) < Mathf.Max(0f, profile.sliderEpsilon))
                {
                    return;
                }
            }

            lastProposeTimes[binding.SyncId] = Time.unscaledTime;
            lastProposedValues[binding.SyncId] = value;
            CommitLocalState(binding, value, false, CreateLocalStamp());
        }

        private void OnLocalButtonClicked(UiSyncBinding binding)
        {
            if (suppressionCount > 0)
            {
                return;
            }

            CommitLocalButton(binding, CreateLocalStamp());
        }

        private void OnInteractionStarted(UiSyncBinding binding)
        {
            binding.IsInteracting = true;
        }

        private void OnInteractionEnded(UiSyncBinding binding)
        {
            binding.IsInteracting = false;
            OnLocalStateChanged(binding, binding.ReadValue(), true);
            if (deferredCommits.TryGetValue(binding.SyncId, out var deferred))
            {
                deferredCommits.Remove(binding.SyncId);
                ApplyRemoteState(binding.SyncId, deferred.ValueType, deferred.Value, deferred.Stamp, false);
            }
        }

        private void UpdateContinuousInteractions()
        {
            if (bindings.Count == 0)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var eventCamera = canvasComponent != null && canvasComponent.renderMode != RenderMode.ScreenSpaceOverlay ? canvasComponent.worldCamera : null;
                foreach (var binding in bindings.Values)
                {
                    if (!binding.IsContinuous || binding.Component is not RectTransform rectTransform)
                    {
                        continue;
                    }

                    if (RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera))
                    {
                        OnInteractionStarted(binding);
                    }
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                foreach (var binding in bindings.Values)
                {
                    if (binding.IsContinuous && binding.IsInteracting)
                    {
                        OnInteractionEnded(binding);
                    }
                }
            }
        }

        private void CommitLocalState(UiSyncBinding binding, object value, bool applyToLocalUi, StateStamp stamp)
        {
            if (!localStates.TryGetValue(binding.SyncId, out var state))
            {
                state = new LocalStateRecord(value, binding.ValueType, stamp);
                localStates[binding.SyncId] = state;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            if (applyToLocalUi)
            {
                ApplyValueToBinding(binding, value);
            }

            var now = Time.unscaledTime;
            if (now - state.LastBroadcastAt >= Mathf.Max(0f, profile.minimumCommitBroadcastIntervalSeconds))
            {
                BroadcastCommit(binding.SyncId, binding.ValueType, value, stamp);
                state.LastBroadcastAt = now;
                state.HasPendingBroadcast = false;
            }
            else
            {
                state.HasPendingBroadcast = true;
                state.NextBroadcastAt = state.LastBroadcastAt + Mathf.Max(0f, profile.minimumCommitBroadcastIntervalSeconds);
            }
        }

        private void CommitLocalButton(UiSyncBinding binding, StateStamp stamp)
        {
            latestAppliedButtonStamps[binding.SyncId] = stamp;
            BroadcastButton(binding.SyncId, stamp);
        }

        private void ApplyRemoteState(string syncId, string valueType, object value, StateStamp stamp, bool isSnapshot)
        {
            if (!bindings.TryGetValue(syncId, out var binding) && (!TryRefreshBindingsForSyncId(syncId) || !bindings.TryGetValue(syncId, out binding)))
            {
                HandleUnknownSyncId(syncId);
                return;
            }

            if (!string.Equals(binding.ValueType, valueType, StringComparison.Ordinal))
            {
                HandleTypeMismatch(syncId, binding.ValueType, valueType);
                return;
            }

            if (!localStates.TryGetValue(syncId, out var state))
            {
                state = new LocalStateRecord(value, valueType, stamp);
                localStates[syncId] = state;
            }

            if (!IsIncomingStampNewer(state.Stamp, stamp))
            {
                if (profile.verboseLog)
                {
                    Debug.Log("CanvasUiSync stale state discard: " + syncId + " ticks=" + stamp.LogicalTicks + " node=" + stamp.NodeId, this);
                }

                return;
            }

            if (binding.IsContinuous && binding.IsInteracting && !isSnapshot)
            {
                deferredCommits[syncId] = new DeferredStateCommit(valueType, value, stamp);
                return;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            ApplyValueToBinding(binding, value);
        }

        private void FlushPendingCommits(float now)
        {
            foreach (var pair in localStates)
            {
                var state = pair.Value;
                if (!state.HasPendingBroadcast || now < state.NextBroadcastAt)
                {
                    continue;
                }

                BroadcastCommit(pair.Key, state.ValueType, state.PendingValue, state.PendingStamp);
                state.HasPendingBroadcast = false;
                state.LastBroadcastAt = now;
            }
        }
    }
}
