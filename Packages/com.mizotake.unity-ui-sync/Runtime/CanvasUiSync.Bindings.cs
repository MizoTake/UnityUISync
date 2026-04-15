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
        private static readonly FieldInfo UiDropdownListField = typeof(Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UiDropdownBlockerField = typeof(Dropdown).GetField("m_Blocker", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TmpDropdownListField = typeof(TMP_Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo TmpDropdownBlockerField = typeof(TMP_Dropdown).GetField("m_Blocker", BindingFlags.Instance | BindingFlags.NonPublic);

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
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

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
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

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
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

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
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "DropdownExpanded"), "DropdownExpanded", () => IsDropdownExpanded(component), value => SetDropdownExpanded(component, Convert.ToBoolean(value)), false, true));
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
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "TMP_DropdownExpanded"), "TMP_DropdownExpanded", () => IsDropdownExpanded(component), value => SetDropdownExpanded(component, Convert.ToBoolean(value)), false, true));
            }
        }

        internal static void RegisterInputFields(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<InputField>(true))
            {
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

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
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

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
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

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
                if (ShouldSkipComponent(owner, component))
                {
                    continue;
                }

                hash = (hash * 31) + ComputeStableHash(owner.BuildSyncId(component.transform, componentType));
            }
        }

        private static bool ShouldSkipComponent(CanvasUiSync owner, Component component)
        {
            return component != null && !(component is Dropdown) && !(component is TMP_Dropdown) && (IsDropdownTemplateComponent(owner, component.transform) || IsDropdownRuntimeComponent(owner, component.transform));
        }

        private static bool IsDropdownTemplateComponent(CanvasUiSync owner, Transform target)
        {
            foreach (var dropdown in owner.GetComponentsInChildren<Dropdown>(true))
            {
                if (dropdown.template != null && (target == dropdown.template || target.IsChildOf(dropdown.template)))
                {
                    return true;
                }
            }

            foreach (var dropdown in owner.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if (dropdown.template != null && (target == dropdown.template || target.IsChildOf(dropdown.template)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDropdownRuntimeComponent(CanvasUiSync owner, Transform target)
        {
            foreach (var dropdown in owner.GetComponentsInChildren<Dropdown>(true))
            {
                if (UiDropdownListField?.GetValue(dropdown) is GameObject dropdownList && dropdownList != null && (target == dropdownList.transform || target.IsChildOf(dropdownList.transform)))
                {
                    return true;
                }

                if (UiDropdownBlockerField?.GetValue(dropdown) is GameObject blocker && blocker != null && (target == blocker.transform || target.IsChildOf(blocker.transform)))
                {
                    return true;
                }
            }

            foreach (var dropdown in owner.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                if (TmpDropdownListField?.GetValue(dropdown) is GameObject dropdownList && dropdownList != null && (target == dropdownList.transform || target.IsChildOf(dropdownList.transform)))
                {
                    return true;
                }

                if (TmpDropdownBlockerField?.GetValue(dropdown) is GameObject blocker && blocker != null && (target == blocker.transform || target.IsChildOf(blocker.transform)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDropdownExpanded(Dropdown dropdown)
        {
            return UiDropdownListField?.GetValue(dropdown) is GameObject dropdownList && dropdownList != null;
        }

        private static bool IsDropdownExpanded(TMP_Dropdown dropdown)
        {
            return TmpDropdownListField?.GetValue(dropdown) is GameObject dropdownList && dropdownList != null;
        }

        private static void SetDropdownExpanded(Dropdown dropdown, bool isExpanded)
        {
            if (isExpanded)
            {
                if (!IsDropdownExpanded(dropdown))
                {
                    dropdown.Show();
                }

                return;
            }

            if (IsDropdownExpanded(dropdown))
            {
                dropdown.Hide();
            }
        }

        private static void SetDropdownExpanded(TMP_Dropdown dropdown, bool isExpanded)
        {
            if (isExpanded)
            {
                if (!IsDropdownExpanded(dropdown))
                {
                    dropdown.Show();
                }

                return;
            }

            if (IsDropdownExpanded(dropdown))
            {
                dropdown.Hide();
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




