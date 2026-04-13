using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    public sealed partial class CanvasUiSync
    {
        private void ScanBindings()
        {
            foreach (var binding in bindings.Values)
            {
                binding.Dispose();
            }

            bindings.Clear();
            RegisterToggles();
            RegisterSliders();
            RegisterScrollbars();
            RegisterDropdowns();
            RegisterTmpDropdowns();
            RegisterInputFields();
            RegisterTmpInputFields();
            RegisterButtons();
            registryHash = ComputeRegistryHash();
        }

        private void RegisterToggles()
        {
            foreach (var component in GetComponentsInChildren<Toggle>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "Toggle"), "Toggle", () => component.isOn, value => component.SetIsOnWithoutNotify(Convert.ToBoolean(value)), false);
                UnityEngine.Events.UnityAction<bool> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                RegisterBinding(binding);
            }
        }

        private void RegisterSliders()
        {
            foreach (var component in GetComponentsInChildren<Slider>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "Slider"), "Slider", () => component.value, value => component.SetValueWithoutNotify(Convert.ToSingle(value)), true);
                UnityEngine.Events.UnityAction<float> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                RegisterBinding(binding);
            }
        }

        private void RegisterScrollbars()
        {
            foreach (var component in GetComponentsInChildren<Scrollbar>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "Scrollbar"), "Scrollbar", () => component.value, value => component.SetValueWithoutNotify(Convert.ToSingle(value)), true);
                UnityEngine.Events.UnityAction<float> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                RegisterBinding(binding);
            }
        }

        private void RegisterDropdowns()
        {
            foreach (var component in GetComponentsInChildren<Dropdown>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "Dropdown"), "Dropdown", () => component.value, value => component.SetValueWithoutNotify(Convert.ToInt32(value)), false);
                UnityEngine.Events.UnityAction<int> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                RegisterBinding(binding);
            }
        }

        private void RegisterTmpDropdowns()
        {
            foreach (var component in GetComponentsInChildren<TMP_Dropdown>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "TMP_Dropdown"), "TMP_Dropdown", () => component.value, value => component.SetValueWithoutNotify(Convert.ToInt32(value)), false);
                UnityEngine.Events.UnityAction<int> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                RegisterBinding(binding);
            }
        }

        private void RegisterInputFields()
        {
            foreach (var component in GetComponentsInChildren<InputField>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "InputField"), "InputField", () => component.text, value => component.SetTextWithoutNotify(Convert.ToString(value)), false);
                UnityEngine.Events.UnityAction<string> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => { component.onEndEdit.RemoveListener(listener); component.onValueChanged.RemoveListener(listener); };
                if (profile.stringSendMode == CanvasUiSyncStringSendMode.OnValueChanged)
                {
                    component.onValueChanged.AddListener(listener);
                }
                else
                {
                    component.onEndEdit.AddListener(listener);
                }

                RegisterBinding(binding);
            }
        }

        private void RegisterTmpInputFields()
        {
            foreach (var component in GetComponentsInChildren<TMP_InputField>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "TMP_InputField"), "TMP_InputField", () => component.text, value => component.SetTextWithoutNotify(Convert.ToString(value)), false);
                UnityEngine.Events.UnityAction<string> listener = value => OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => { component.onEndEdit.RemoveListener(listener); component.onValueChanged.RemoveListener(listener); };
                if (profile.stringSendMode == CanvasUiSyncStringSendMode.OnValueChanged)
                {
                    component.onValueChanged.AddListener(listener);
                }
                else
                {
                    component.onEndEdit.AddListener(listener);
                }

                RegisterBinding(binding);
            }
        }

        private void RegisterButtons()
        {
            foreach (var component in GetComponentsInChildren<Button>(true))
            {
                var binding = new UiSyncBinding(component, BuildSyncId(component.transform, "Button"), "Button", null, null, false);
                UnityEngine.Events.UnityAction listener = () => OnLocalButtonClicked(binding);
                binding.Unsubscribe = () => component.onClick.RemoveListener(listener);
                component.onClick.AddListener(listener);
                RegisterBinding(binding);
            }
        }

        private void RegisterBinding(UiSyncBinding binding)
        {
            if (bindings.ContainsKey(binding.SyncId))
            {
                if (profile.logDuplicateSyncId)
                {
                    Debug.LogError("Duplicate syncId detected: " + binding.SyncId, binding.Component);
                }

                binding.Dispose();
                return;
            }

            bindings.Add(binding.SyncId, binding);
        }

        private string BuildSyncId(Transform target, string componentType)
        {
            return canvasId + "/" + BuildPath(target) + ":" + componentType;
        }

        private string BuildPath(Transform target)
        {
            var stack = new Stack<string>();
            var current = target;
            while (current != null && current != transform)
            {
                stack.Push(current.name + "[" + current.GetSiblingIndex() + "]");
                current = current.parent;
            }

            return stack.Count == 0 ? transform.name + "[0]" : string.Join("/", stack.ToArray());
        }

        private string ComputeRegistryHash()
        {
            using (var sha = SHA256.Create())
            {
                var source = string.Join("|", bindings.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value.ValueType));
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
                var builder = new StringBuilder(16);
                for (var index = 0; index < 8 && index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private void ApplyValueToBinding(UiSyncBinding binding, object value)
        {
            using (new SuppressionScope(this))
            {
                binding.ApplyValue(value);
            }
        }

        private void TickRuntimeHierarchyRescan(float now)
        {
            if (now < nextHierarchyRescanTime)
            {
                return;
            }

            nextHierarchyRescanTime = now + RuntimeHierarchyRescanIntervalSeconds;
            RefreshBindingsIfHierarchyChanged(false);
        }

        private bool TryRefreshBindingsForSyncId(string syncId)
        {
            if (bindings.ContainsKey(syncId))
            {
                return true;
            }

            RefreshBindingsIfHierarchyChanged(true);
            return bindings.ContainsKey(syncId);
        }

        private void RefreshBindingsIfHierarchyChanged(bool force)
        {
            var signature = ComputeBindingHierarchySignature();
            if (!force && signature == bindingHierarchySignature)
            {
                return;
            }

            var previousRegistryHash = registryHash;
            ScanBindings();
            InitializeLocalState();
            bindingHierarchySignature = ComputeBindingHierarchySignature();
            if (!initialized || string.Equals(previousRegistryHash, registryHash, StringComparison.Ordinal))
            {
                return;
            }

            hasSnapshot = false;
            snapshotRetryCount = 0;
            nextSnapshotRequestTime = Time.unscaledTime;
            snapshotCooldownUntil = 0f;
            SendHello();
            RequestSnapshotIfNeeded(true);
        }

        private int ComputeBindingHierarchySignature()
        {
            unchecked
            {
                var hash = 17;
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<Toggle>(true), "Toggle");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<Slider>(true), "Slider");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<Scrollbar>(true), "Scrollbar");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<Dropdown>(true), "Dropdown");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<TMP_Dropdown>(true), "TMP_Dropdown");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<InputField>(true), "InputField");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<TMP_InputField>(true), "TMP_InputField");
                AppendBindingHierarchySignature(ref hash, GetComponentsInChildren<Button>(true), "Button");
                return hash;
            }
        }

        private void AppendBindingHierarchySignature<TComponent>(ref int hash, IEnumerable<TComponent> components, string componentType) where TComponent : Component
        {
            foreach (var component in components)
            {
                hash = (hash * 31) + ComputeStableHash(BuildSyncId(component.transform, componentType));
            }
        }

        private static int ComputeStableHash(string value)
        {
            unchecked
            {
                var hash = 23;
                if (string.IsNullOrEmpty(value))
                {
                    return hash;
                }

                for (var index = 0; index < value.Length; index++)
                {
                    hash = (hash * 31) + value[index];
                }

                return hash;
            }
        }
    }
}
