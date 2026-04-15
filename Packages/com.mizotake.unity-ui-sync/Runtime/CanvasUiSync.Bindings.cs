using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncBindingsService
    {
        internal static void ScanBindings(CanvasUiSync owner)
        {
            foreach (var binding in owner.bindings.Values)
            {
                binding.Dispose();
            }

            owner.bindings.Clear();
            RegisterToggles(owner);
            RegisterSliders(owner);
            RegisterScrollbars(owner);
            RegisterDropdowns(owner);
            RegisterTmpDropdowns(owner);
            RegisterInputFields(owner);
            RegisterTmpInputFields(owner);
            RegisterButtons(owner);
            owner.registryHash = ComputeRegistryHash(owner);
        }

        internal static void RegisterToggles(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<Toggle>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "Toggle"), "Toggle", () => component.isOn, value => component.SetIsOnWithoutNotify(Convert.ToBoolean(value)), false);
                UnityEngine.Events.UnityAction<bool> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterSliders(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<Slider>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "Slider"), "Slider", () => component.value, value => component.SetValueWithoutNotify(Convert.ToSingle(value)), true);
                UnityEngine.Events.UnityAction<float> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterScrollbars(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<Scrollbar>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "Scrollbar"), "Scrollbar", () => component.value, value => component.SetValueWithoutNotify(Convert.ToSingle(value)), true);
                UnityEngine.Events.UnityAction<float> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterDropdowns(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<Dropdown>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "Dropdown"), "Dropdown", () => component.value, value => component.SetValueWithoutNotify(Convert.ToInt32(value)), false);
                UnityEngine.Events.UnityAction<int> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterTmpDropdowns(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "TMP_Dropdown"), "TMP_Dropdown", () => component.value, value => component.SetValueWithoutNotify(Convert.ToInt32(value)), false);
                UnityEngine.Events.UnityAction<int> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterInputFields(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<InputField>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "InputField"), "InputField", () => component.text, value => component.SetTextWithoutNotify(Convert.ToString(value)), false);
                UnityEngine.Events.UnityAction<string> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => { component.onEndEdit.RemoveListener(listener); component.onValueChanged.RemoveListener(listener); };
                if (owner.profile.stringSendMode == CanvasUiSyncStringSendMode.OnValueChanged)
                {
                    component.onValueChanged.AddListener(listener);
                }
                else
                {
                    component.onEndEdit.AddListener(listener);
                }

                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterTmpInputFields(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<TMP_InputField>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "TMP_InputField"), "TMP_InputField", () => component.text, value => component.SetTextWithoutNotify(Convert.ToString(value)), false);
                UnityEngine.Events.UnityAction<string> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => { component.onEndEdit.RemoveListener(listener); component.onValueChanged.RemoveListener(listener); };
                if (owner.profile.stringSendMode == CanvasUiSyncStringSendMode.OnValueChanged)
                {
                    component.onValueChanged.AddListener(listener);
                }
                else
                {
                    component.onEndEdit.AddListener(listener);
                }

                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterButtons(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<Button>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "Button"), "Button", null, null, false);
                UnityEngine.Events.UnityAction listener = () => owner.OnLocalButtonClicked(binding);
                binding.Unsubscribe = () => component.onClick.RemoveListener(listener);
                component.onClick.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterBinding(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding)
        {
            if (owner.bindings.ContainsKey(binding.SyncId))
            {
                if (owner.profile.logDuplicateSyncId)
                {
                    Debug.LogError("Duplicate syncId detected: " + binding.SyncId, binding.Component);
                }

                binding.Dispose();
                return;
            }

            owner.bindings.Add(binding.SyncId, binding);
        }

        internal static string BuildSyncId(CanvasUiSync owner, Transform target, string componentType)
        {
            var bindingId = ReadExplicitBindingId(target);
            if (!string.IsNullOrWhiteSpace(bindingId))
            {
                return owner.canvasId + "/" + bindingId + ":" + componentType;
            }

            return owner.canvasId + "/" + BuildPath(owner, target) + ":" + componentType;
        }

        internal static string BuildPath(CanvasUiSync owner, Transform target)
        {
            var stack = new Stack<string>();
            var current = target;
            while (current != null && current != owner.transform)
            {
                stack.Push(current.name);
                current = current.parent;
            }

            return stack.Count == 0 ? owner.transform.name : string.Join("/", stack.ToArray());
        }

        internal static string ReadExplicitBindingId(Component target)
        {
            var bindingIdType = typeof(CanvasUiSync).Assembly.GetType("Mizotake.UnityUiSync.CanvasUiSyncBindingId");
            if (bindingIdType == null)
            {
                return null;
            }

            var bindingId = target.GetComponent(bindingIdType);
            if (bindingId == null)
            {
                return null;
            }

            var property = bindingIdType.GetProperty("BindingId", BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return null;
            }

            return property.GetValue(bindingId) as string;
        }

        internal static string ComputeRegistryHash(CanvasUiSync owner)
        {
            using (var sha = SHA256.Create())
            {
                var source = string.Join("|", owner.bindings.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value.ValueType));
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(source));
                var builder = new StringBuilder(16);
                for (var index = 0; index < 8 && index < bytes.Length; index++)
                {
                    builder.Append(bytes[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        internal static void ApplyValueToBinding(CanvasUiSync owner, CanvasUiSync.UiSyncBinding binding, object value)
        {
            using (new CanvasUiSync.SuppressionScope(owner))
            {
                binding.ApplyValue(value);
            }
        }

        internal static void TickRuntimeHierarchyRescan(CanvasUiSync owner, float now)
        {
            if (now < owner.nextHierarchyRescanTime)
            {
                return;
            }

            owner.nextHierarchyRescanTime = now + CanvasUiSync.RuntimeHierarchyRescanIntervalSeconds;
            owner.RefreshBindingsIfHierarchyChanged(false);
        }

        internal static bool TryRefreshBindingsForSyncId(CanvasUiSync owner, string syncId)
        {
            if (owner.bindings.ContainsKey(syncId))
            {
                return true;
            }

            owner.RefreshBindingsIfHierarchyChanged(true);
            return owner.bindings.ContainsKey(syncId);
        }

        internal static void RefreshBindingsIfHierarchyChanged(CanvasUiSync owner, bool force)
        {
            var signature = ComputeBindingHierarchySignature(owner);
            if (!force && signature == owner.bindingHierarchySignature)
            {
                return;
            }

            var previousRegistryHash = owner.registryHash;
            owner.ScanBindings();
            owner.InitializeLocalState();
            owner.bindingHierarchySignature = ComputeBindingHierarchySignature(owner);
            if (!owner.initialized || string.Equals(previousRegistryHash, owner.registryHash, StringComparison.Ordinal))
            {
                return;
            }

            owner.hasSnapshot = false;
            owner.snapshotRetryCount = 0;
            owner.nextSnapshotRequestTime = Time.unscaledTime;
            owner.snapshotCooldownUntil = 0f;
            owner.SendHello();
            owner.RequestSnapshotIfNeeded(true);
        }

        internal static int ComputeBindingHierarchySignature(CanvasUiSync owner)
        {
            unchecked
            {
                var hash = 17;
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<Toggle>(true), "Toggle");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<Slider>(true), "Slider");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<Scrollbar>(true), "Scrollbar");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<Dropdown>(true), "Dropdown");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<TMP_Dropdown>(true), "TMP_Dropdown");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<InputField>(true), "InputField");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<TMP_InputField>(true), "TMP_InputField");
                owner.AppendBindingHierarchySignature(ref hash, owner.GetComponentsInChildren<Button>(true), "Button");
                return hash;
            }
        }

        internal static void AppendBindingHierarchySignature<TComponent>(CanvasUiSync owner, ref int hash, IEnumerable<TComponent> components, string componentType) where TComponent : Component
        {
            foreach (var component in components)
            {
                hash = (hash * 31) + ComputeStableHash(owner.BuildSyncId(component.transform, componentType));
            }
        }

        internal static int ComputeStableHash(string value)
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




