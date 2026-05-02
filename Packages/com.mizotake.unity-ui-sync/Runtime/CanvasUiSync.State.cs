using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            RecalculateNextPendingCommitTime(owner);
            owner.ApplyPendingRemoteCommits();
            owner.ApplyPendingRemoteButtonCommits();
        }

        internal static void OnLocalStateChanged(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, object value, bool force)
        {
            if (!owner.syncEnabled || owner.suppressionCount > 0)
            {
                return;
            }

            if (binding.IsContinuous && !force)
            {
                var now = Time.unscaledTime;
                if (owner.lastProposeTimes.TryGetValue(binding.SyncId, out var lastProposeAt) && now - lastProposeAt < Mathf.Max(0f, owner.profile.minimumProposeIntervalSeconds))
                {
                    if (owner.ShouldVerboseLog())
                    {
                        LogThrottlePropose(owner, binding.SyncId);
                    }

                    return;
                }

                var currentValue = ReadContinuousValue(value);
                if (owner.lastContinuousProposedValues.TryGetValue(binding.SyncId, out var lastValue) && Mathf.Abs(lastValue - currentValue) < Mathf.Max(0f, owner.profile.sliderEpsilon))
                {
                    return;
                }

                owner.lastContinuousProposedValues[binding.SyncId] = currentValue;
            }

            owner.lastProposeTimes[binding.SyncId] = Time.unscaledTime;
            owner.lastProposedValues[binding.SyncId] = value;
            owner.CommitLocalState(binding, value, false, owner.CreateLocalStamp());
        }

        internal static void OnLocalButtonClicked(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            if (!owner.syncEnabled || owner.suppressionCount > 0)
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
            if (owner.continuousBindings.Count == 0)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                var eventCamera = owner.canvasComponent != null && owner.canvasComponent.renderMode != RenderMode.ScreenSpaceOverlay ? owner.canvasComponent.worldCamera : null;
                foreach (var binding in owner.continuousBindings)
                {
                    if (binding.Component is not RectTransform rectTransform)
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
                foreach (var binding in owner.continuousBindings)
                {
                    if (binding.IsInteracting)
                    {
                        owner.OnInteractionEnded(binding);
                    }
                }
            }
        }

        internal static void UpdatePolledBindings(CanvasUiSync owner)
        {
            if (owner.polledBindings.Count == 0)
            {
                return;
            }

            foreach (var binding in owner.polledBindings)
            {
                if (binding.TryReadIntValue(out var currentIntValue))
                {
                    if (owner.localStates.TryGetValue(binding.SyncId, out var intState) && intState.Value is int previousIntValue && previousIntValue == currentIntValue)
                    {
                        continue;
                    }

                    owner.OnLocalStateChanged(binding, currentIntValue, false);
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
            object previousValue = null;
            if (!owner.localStates.TryGetValue(binding.SyncId, out var state))
            {
                state = new CanvasUiSync.LocalStateRecord(value, binding.ValueType, stamp);
                owner.localStates[binding.SyncId] = state;
            }
            else
            {
                previousValue = state.Value;
            }

            var hadPendingBroadcast = state.HasPendingBroadcast;
            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            if (applyToLocalUi)
            {
                owner.ApplyValueToBinding(binding, value);
            }

            var now = Time.unscaledTime;
            var minimumCommitBroadcastIntervalSeconds = Mathf.Max(0f, owner.profile.minimumCommitBroadcastIntervalSeconds);
            if (now - state.LastBroadcastAt >= minimumCommitBroadcastIntervalSeconds)
            {
                owner.BroadcastCommit(binding.SyncId, binding.ValueType, value, stamp);
                state.LastBroadcastAt = now;
                state.HasPendingBroadcast = false;
                if (hadPendingBroadcast)
                {
                    RecalculateNextPendingCommitTime(owner);
                }
            }
            else
            {
                state.HasPendingBroadcast = true;
                state.NextBroadcastAt = state.LastBroadcastAt + minimumCommitBroadcastIntervalSeconds;
                owner.nextPendingCommitTime = Mathf.Min(owner.nextPendingCommitTime, state.NextBroadcastAt);
            }

            if (IsDropdownBinding(binding))
            {
                SyncDropdownItemToggleStates(owner, binding, previousValue, value, stamp, true);
            }
        }

        internal static void CommitLocalButton(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, CanvasUiSync.StateStamp stamp)
        {
            owner.latestAppliedButtonStamps[binding.SyncId] = stamp;
            owner.BroadcastButton(binding.SyncId, stamp);
        }

        internal static void ApplyRemoteState(CanvasUiSync owner, string syncId, string valueType, object value, CanvasUiSync.StateStamp stamp, bool isSnapshot)
        {
            if (!owner.bindings.TryGetValue(syncId, out var binding))
            {
                if (owner.pendingRemoteCommits.TryGetValue(syncId, out var existing))
                {
                    if (!owner.IsIncomingStampNewer(existing.Stamp, stamp))
                    {
                        return;
                    }

                    owner.pendingRemoteCommits[syncId] = new CanvasUiSync.DeferredStateCommit(valueType, value, stamp, Time.unscaledTime);
                    return;
                }

                if (!owner.TryRefreshBindingsForSyncId(syncId) || !owner.bindings.TryGetValue(syncId, out binding))
                {
                    owner.pendingRemoteCommits[syncId] = new CanvasUiSync.DeferredStateCommit(valueType, value, stamp, Time.unscaledTime);
                    owner.HandleUnknownSyncId(syncId);
                    return;
                }
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

            var previousValue = state.Value;

            if (!owner.IsIncomingStampNewer(state.Stamp, stamp))
            {
                if (owner.ShouldVerboseLog())
                {
                    LogStaleStateDiscard(owner, syncId, stamp);
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
                if (IsDropdownBinding(binding))
                {
                    SyncDropdownItemToggleStates(owner, binding, previousValue, observedValue, stamp, false);
                }
                return;
            }

            state.Value = value;
            state.Stamp = stamp;
            state.PendingValue = value;
            state.PendingStamp = stamp;
            owner.ApplyValueToBinding(binding, value);
            if (IsDropdownBinding(binding))
            {
                SyncDropdownItemToggleStates(owner, binding, previousValue, value, stamp, false);
            }
        }

        internal static void FlushPendingCommits(CanvasUiSync owner, float now)
        {
            if (now < owner.nextPendingCommitTime)
            {
                return;
            }

            var nextPendingCommitTime = float.PositiveInfinity;
            foreach (var pair in owner.localStates)
            {
                var state = pair.Value;
                if (!state.HasPendingBroadcast)
                {
                    continue;
                }

                if (now < state.NextBroadcastAt)
                {
                    nextPendingCommitTime = Mathf.Min(nextPendingCommitTime, state.NextBroadcastAt);
                    continue;
                }

                owner.BroadcastCommit(pair.Key, state.ValueType, state.PendingValue, state.PendingStamp);
                state.HasPendingBroadcast = false;
                state.LastBroadcastAt = now;
            }

            owner.nextPendingCommitTime = nextPendingCommitTime;
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

        private static void LogThrottlePropose(CanvasUiSync owner, string syncId)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync throttle propose: ");
            builder.Append(syncId);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static void LogStaleStateDiscard(CanvasUiSync owner, string syncId, CanvasUiSync.StateStamp stamp)
        {
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            builder.Append("CanvasUiSync stale state discard: ");
            builder.Append(syncId);
            builder.Append(" ticks=");
            builder.Append(stamp.LogicalTicks);
            builder.Append(" node=");
            builder.Append(stamp.NodeId);
            Debug.Log(builder.ToString(), owner);
            builder.Length = 0;
        }

        private static bool IsDropdownBinding(CanvasUiSync.UiSyncBinding binding)
        {
            return binding != null && (string.Equals(binding.ValueType, "Dropdown", StringComparison.Ordinal) || string.Equals(binding.ValueType, "TMP_Dropdown", StringComparison.Ordinal));
        }

        private static void SyncDropdownItemToggleStates(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, object previousValue, object nextValue, CanvasUiSync.StateStamp stamp, bool broadcastChanges)
        {
            if (binding.Component is Dropdown dropdown)
            {
                SyncDropdownItemToggleStates(owner, dropdown.transform, dropdown.options.Count, "DropdownItemToggle[", TryReadDropdownSelectionIndex(previousValue, dropdown.options.Count), TryReadDropdownSelectionIndex(nextValue, dropdown.options.Count), stamp, broadcastChanges);
                return;
            }

            if (binding.Component is TMP_Dropdown tmpDropdown)
            {
                SyncDropdownItemToggleStates(owner, tmpDropdown.transform, tmpDropdown.options.Count, "TMP_DropdownItemToggle[", TryReadDropdownSelectionIndex(previousValue, tmpDropdown.options.Count), TryReadDropdownSelectionIndex(nextValue, tmpDropdown.options.Count), stamp, broadcastChanges);
            }
        }

        private static void SyncDropdownItemToggleStates(CanvasUiSync owner, Transform dropdownTransform, int optionCount, string optionBindingPrefix, int previousSelectedIndex, int nextSelectedIndex, CanvasUiSync.StateStamp stamp, bool broadcastChanges)
        {
            if (optionCount <= 0 || nextSelectedIndex < 0 || nextSelectedIndex >= optionCount)
            {
                return;
            }

            if (previousSelectedIndex >= 0 && previousSelectedIndex < optionCount)
            {
                if (previousSelectedIndex == nextSelectedIndex)
                {
                    SyncDropdownItemToggleState(owner, dropdownTransform, optionBindingPrefix, nextSelectedIndex, true, stamp, broadcastChanges);
                    return;
                }

                SyncDropdownItemToggleState(owner, dropdownTransform, optionBindingPrefix, previousSelectedIndex, false, stamp, broadcastChanges);
                SyncDropdownItemToggleState(owner, dropdownTransform, optionBindingPrefix, nextSelectedIndex, true, stamp, broadcastChanges);
                return;
            }

            for (var optionIndex = 0; optionIndex < optionCount; optionIndex++)
            {
                SyncDropdownItemToggleState(owner, dropdownTransform, optionBindingPrefix, optionIndex, optionIndex == nextSelectedIndex, stamp, broadcastChanges);
            }
        }

        private static void SyncDropdownItemToggleState(CanvasUiSync owner, Transform dropdownTransform, string optionBindingPrefix, int optionIndex, bool targetValue, CanvasUiSync.StateStamp stamp, bool broadcastChanges)
        {
            var syncId = owner.BuildSyncId(dropdownTransform, optionBindingPrefix + optionIndex + "]");
            if (!owner.bindings.TryGetValue(syncId, out var optionBinding))
            {
                return;
            }

            if (broadcastChanges)
            {
                if (owner.localStates.TryGetValue(syncId, out var currentState) && AreEquivalent(currentState.Value, targetValue))
                {
                    return;
                }

                owner.CommitLocalState(optionBinding, targetValue, true, stamp);
                return;
            }

            var pendingCommitScheduleChanged = false;
            if (!owner.localStates.TryGetValue(syncId, out var state))
            {
                state = new CanvasUiSync.LocalStateRecord(targetValue, optionBinding.ValueType, stamp);
                owner.localStates[syncId] = state;
            }
            else
            {
                if (AreEquivalent(state.Value, targetValue) && !state.HasPendingBroadcast)
                {
                    return;
                }

                if (state.HasPendingBroadcast)
                {
                    state.HasPendingBroadcast = false;
                    pendingCommitScheduleChanged = true;
                }

                state.Value = targetValue;
                state.Stamp = stamp;
                state.PendingValue = targetValue;
                state.PendingStamp = stamp;
            }

            owner.ApplyValueToBinding(optionBinding, targetValue);
            if (pendingCommitScheduleChanged)
            {
                RecalculateNextPendingCommitTime(owner);
            }
        }

        private static int TryReadDropdownSelectionIndex(object value, int optionCount)
        {
            if (value == null || optionCount <= 0)
            {
                return -1;
            }

            try
            {
                var selectionIndex = Convert.ToInt32(value);
                return selectionIndex >= 0 && selectionIndex < optionCount ? selectionIndex : -1;
            }
            catch
            {
                return -1;
            }
        }

        private static void RecalculateNextPendingCommitTime(CanvasUiSync owner)
        {
            var nextPendingCommitTime = float.PositiveInfinity;
            foreach (var pair in owner.localStates)
            {
                var state = pair.Value;
                if (!state.HasPendingBroadcast)
                {
                    continue;
                }

                nextPendingCommitTime = Mathf.Min(nextPendingCommitTime, state.NextBroadcastAt);
            }

            owner.nextPendingCommitTime = nextPendingCommitTime;
        }

        private static float ReadContinuousValue(object value)
        {
            return value is float singleValue ? singleValue : Convert.ToSingle(value);
        }
    }
}
