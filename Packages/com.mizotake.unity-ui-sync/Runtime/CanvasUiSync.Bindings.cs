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
        internal sealed class BindingScanContext
        {
            public BindingScanContext(CanvasUiSync owner)
            {
                Dropdowns = owner.GetComponentsInChildren<Dropdown>(true);
                TmpDropdowns = owner.GetComponentsInChildren<TMP_Dropdown>(true);
                dropdownTemplateRoots = new List<Transform>(Dropdowns.Length + TmpDropdowns.Length);
                dropdownRuntimeRoots = new List<Transform>((Dropdowns.Length + TmpDropdowns.Length) * 2);
                CacheDropdownRoots(Dropdowns, dropdownTemplateRoots, dropdownRuntimeRoots);
                CacheDropdownRoots(TmpDropdowns, dropdownTemplateRoots, dropdownRuntimeRoots);
            }

            private readonly List<Transform> dropdownTemplateRoots;
            private readonly List<Transform> dropdownRuntimeRoots;
            public Dropdown[] Dropdowns { get; }
            public TMP_Dropdown[] TmpDropdowns { get; }

            public bool IsTemplateComponent(Transform target)
            {
                return IsUnderRoots(target, dropdownTemplateRoots);
            }

            public bool IsRuntimeComponent(Transform target)
            {
                return IsUnderRoots(target, dropdownRuntimeRoots);
            }

            private static void CacheDropdownRoots(IEnumerable<Dropdown> dropdowns, List<Transform> templateRoots, List<Transform> runtimeRoots)
            {
                foreach (var dropdown in dropdowns)
                {
                    if (dropdown == null)
                    {
                        continue;
                    }

                    if (dropdown.template != null)
                    {
                        templateRoots.Add(dropdown.template);
                    }

                    if (UiDropdownListField?.GetValue(dropdown) is GameObject dropdownList && dropdownList != null)
                    {
                        runtimeRoots.Add(dropdownList.transform);
                    }

                    if (UiDropdownBlockerField?.GetValue(dropdown) is GameObject blocker && blocker != null)
                    {
                        runtimeRoots.Add(blocker.transform);
                    }
                }
            }

            private static void CacheDropdownRoots(IEnumerable<TMP_Dropdown> dropdowns, List<Transform> templateRoots, List<Transform> runtimeRoots)
            {
                foreach (var dropdown in dropdowns)
                {
                    if (dropdown == null)
                    {
                        continue;
                    }

                    if (dropdown.template != null)
                    {
                        templateRoots.Add(dropdown.template);
                    }

                    if (TmpDropdownListField?.GetValue(dropdown) is GameObject dropdownList && dropdownList != null)
                    {
                        runtimeRoots.Add(dropdownList.transform);
                    }

                    if (TmpDropdownBlockerField?.GetValue(dropdown) is GameObject blocker && blocker != null)
                    {
                        runtimeRoots.Add(blocker.transform);
                    }
                }
            }

            private static bool IsUnderRoots(Transform target, List<Transform> roots)
            {
                if (target == null)
                {
                    return false;
                }

                for (var index = 0; index < roots.Count; index++)
                {
                    var root = roots[index];
                    if (root != null && (target == root || target.IsChildOf(root)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        internal static void ScanBindings(CanvasUiSync owner)
        {
            foreach (var binding in owner.bindings.Values)
            {
                binding.Dispose();
            }

            owner.bindings.Clear();
            owner.continuousBindings.Clear();
            owner.polledBindings.Clear();
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
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "Dropdown"), "Dropdown", () => component.value, value => SetDropdownValue(component, Convert.ToInt32(value)), false);
                UnityEngine.Events.UnityAction<int> listener = value => { SyncOpenDropdownItemToggles(component, value); owner.OnLocalStateChanged(binding, value, false); };
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "DropdownExpanded"), "DropdownExpanded", () => IsDropdownExpanded(component), value => SetDropdownExpanded(component, Convert.ToBoolean(value)), false, true));
                RegisterDropdownItemToggles(owner, component);
            }
        }

        internal static void RegisterTmpDropdowns(CanvasUiSync owner)
        {
            foreach (var component in owner.GetComponentsInChildren<TMP_Dropdown>(true))
            {
                var binding = new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "TMP_Dropdown"), "TMP_Dropdown", () => component.value, value => SetDropdownValue(component, Convert.ToInt32(value)), false);
                UnityEngine.Events.UnityAction<int> listener = value => { SyncOpenDropdownItemToggles(component, value); owner.OnLocalStateChanged(binding, value, false); };
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(component, owner.BuildSyncId(component.transform, "TMP_DropdownExpanded"), "TMP_DropdownExpanded", () => IsDropdownExpanded(component), value => SetDropdownExpanded(component, Convert.ToBoolean(value)), false, true));
                RegisterTmpDropdownItemToggles(owner, component);
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
            if (binding.IsContinuous)
            {
                owner.continuousBindings.Add(binding);
            }

            if (binding.RequiresPolling)
            {
                owner.polledBindings.Add(binding);
            }
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
                stack.Push(BuildPathSegment(current));
                current = current.parent;
            }

            return stack.Count == 0 ? owner.transform.name : string.Join("/", stack.ToArray());
        }

        private static string BuildPathSegment(Transform target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (target.parent == null)
            {
                return target.name;
            }

            var duplicateCount = 0;
            var duplicateIndex = 0;
            for (var index = 0; index < target.parent.childCount; index++)
            {
                var sibling = target.parent.GetChild(index);
                if (!string.Equals(sibling.name, target.name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (sibling == target)
                {
                    duplicateIndex = duplicateCount;
                }

                duplicateCount++;
            }

            return duplicateCount > 1 ? target.name + "[" + duplicateIndex + "]" : target.name;
        }

        internal static string ReadExplicitBindingId(Component target)
        {
            return target != null && target.TryGetComponent<CanvasUiSyncBindingId>(out var bindingId) ? bindingId.BindingId : null;
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
                var context = new BindingScanContext(owner);
                var hash = 17;
                AppendBindingHierarchySignature(owner, ref hash, owner.GetComponentsInChildren<Toggle>(true), "Toggle", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.GetComponentsInChildren<Slider>(true), "Slider", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.GetComponentsInChildren<Scrollbar>(true), "Scrollbar", context);
                AppendBindingHierarchySignature(owner, ref hash, context.Dropdowns, "Dropdown", context);
                AppendDropdownItemToggleBindingSignatures(owner, ref hash, context);
                AppendBindingHierarchySignature(owner, ref hash, context.TmpDropdowns, "TMP_Dropdown", context);
                AppendTmpDropdownItemToggleBindingSignatures(owner, ref hash, context);
                AppendBindingHierarchySignature(owner, ref hash, owner.GetComponentsInChildren<InputField>(true), "InputField", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.GetComponentsInChildren<TMP_InputField>(true), "TMP_InputField", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.GetComponentsInChildren<Button>(true), "Button", context);
                return hash;
            }
        }

        internal static void AppendBindingHierarchySignature<TComponent>(CanvasUiSync owner, ref int hash, IEnumerable<TComponent> components, string componentType, BindingScanContext context = null) where TComponent : Component
        {
            foreach (var component in components)
            {
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                hash = (hash * 31) + ComputeStableHash(owner.BuildSyncId(component.transform, componentType));
            }
        }

        private static void RegisterDropdownItemToggles(CanvasUiSync owner, Dropdown dropdown)
        {
            for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
            {
                var capturedOptionIndex = optionIndex;
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(dropdown, owner.BuildSyncId(dropdown.transform, "DropdownItemToggle[" + capturedOptionIndex + "]"), "Toggle", () => ReadDropdownItemToggle(dropdown, capturedOptionIndex), value => SetDropdownItemToggle(dropdown, capturedOptionIndex, Convert.ToBoolean(value)), false, true));
            }
        }

        private static void RegisterTmpDropdownItemToggles(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
            {
                var capturedOptionIndex = optionIndex;
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(dropdown, owner.BuildSyncId(dropdown.transform, "TMP_DropdownItemToggle[" + capturedOptionIndex + "]"), "Toggle", () => ReadDropdownItemToggle(dropdown, capturedOptionIndex), value => SetDropdownItemToggle(dropdown, capturedOptionIndex, Convert.ToBoolean(value)), false, true));
            }
        }

        private static void AppendDropdownItemToggleBindingSignatures(CanvasUiSync owner, ref int hash, BindingScanContext context)
        {
            foreach (var dropdown in context.Dropdowns)
            {
                for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
                {
                    hash = (hash * 31) + ComputeStableHash(owner.BuildSyncId(dropdown.transform, "DropdownItemToggle[" + optionIndex + "]"));
                }
            }
        }

        private static void AppendTmpDropdownItemToggleBindingSignatures(CanvasUiSync owner, ref int hash, BindingScanContext context)
        {
            foreach (var dropdown in context.TmpDropdowns)
            {
                for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
                {
                    hash = (hash * 31) + ComputeStableHash(owner.BuildSyncId(dropdown.transform, "TMP_DropdownItemToggle[" + optionIndex + "]"));
                }
            }
        }

        private static bool ShouldSkipComponent(CanvasUiSync owner, Component component, BindingScanContext context = null)
        {
            return component != null && !(component is Dropdown) && !(component is TMP_Dropdown) && (IsDropdownTemplateComponent(owner, component.transform, context) || IsDropdownRuntimeComponent(owner, component.transform, context));
        }

        private static bool IsDropdownTemplateComponent(CanvasUiSync owner, Transform target, BindingScanContext context = null)
        {
            if (context != null)
            {
                return context.IsTemplateComponent(target);
            }

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

        private static bool IsDropdownRuntimeComponent(CanvasUiSync owner, Transform target, BindingScanContext context = null)
        {
            if (context != null)
            {
                return context.IsRuntimeComponent(target);
            }

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

        private static bool ReadDropdownItemToggle(Dropdown dropdown, int optionIndex)
        {
            return dropdown != null && optionIndex >= 0 && optionIndex < dropdown.options.Count && dropdown.value == optionIndex;
        }

        private static bool ReadDropdownItemToggle(TMP_Dropdown dropdown, int optionIndex)
        {
            return dropdown != null && optionIndex >= 0 && optionIndex < dropdown.options.Count && dropdown.value == optionIndex;
        }

        private static void SetDropdownItemToggle(Dropdown dropdown, int optionIndex, bool isOn)
        {
            if (dropdown == null || optionIndex < 0 || optionIndex >= dropdown.options.Count)
            {
                return;
            }

            if (isOn)
            {
                SetDropdownValue(dropdown, optionIndex);
                return;
            }

            var runtimeToggle = GetDropdownItemToggle(dropdown, optionIndex);
            if (runtimeToggle != null && dropdown.value != optionIndex)
            {
                runtimeToggle.SetIsOnWithoutNotify(false);
            }
        }

        private static void SetDropdownItemToggle(TMP_Dropdown dropdown, int optionIndex, bool isOn)
        {
            if (dropdown == null || optionIndex < 0 || optionIndex >= dropdown.options.Count)
            {
                return;
            }

            if (isOn)
            {
                SetDropdownValue(dropdown, optionIndex);
                return;
            }

            var runtimeToggle = GetDropdownItemToggle(dropdown, optionIndex);
            if (runtimeToggle != null && dropdown.value != optionIndex)
            {
                runtimeToggle.SetIsOnWithoutNotify(false);
            }
        }

        private static Toggle GetDropdownItemToggle(Dropdown dropdown, int optionIndex)
        {
            if (UiDropdownListField?.GetValue(dropdown) is not GameObject dropdownList || dropdownList == null)
            {
                return null;
            }

            return dropdownList.GetComponentsInChildren<Toggle>(true).Skip(1).Take(dropdown.options.Count).ElementAtOrDefault(optionIndex);
        }

        private static Toggle GetDropdownItemToggle(TMP_Dropdown dropdown, int optionIndex)
        {
            if (TmpDropdownListField?.GetValue(dropdown) is not GameObject dropdownList || dropdownList == null)
            {
                return null;
            }

            return dropdownList.GetComponentsInChildren<Toggle>(true).Skip(1).Take(dropdown.options.Count).ElementAtOrDefault(optionIndex);
        }

        private static void SetDropdownValue(Dropdown dropdown, int value)
        {
            if (dropdown.value != value)
            {
                dropdown.value = value;
                return;
            }

            dropdown.RefreshShownValue();
            SyncOpenDropdownItemToggles(dropdown, value);
        }

        private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
        {
            if (dropdown.value != value)
            {
                dropdown.value = value;
                return;
            }

            dropdown.RefreshShownValue();
            SyncOpenDropdownItemToggles(dropdown, value);
        }

        private static void SyncOpenDropdownItemToggles(Dropdown dropdown, int selectedOptionIndex)
        {
            if (UiDropdownListField?.GetValue(dropdown) is not GameObject dropdownList || dropdownList == null)
            {
                return;
            }

            var toggles = dropdownList.GetComponentsInChildren<Toggle>(true).Skip(1).Take(dropdown.options.Count).ToArray();
            for (var optionIndex = 0; optionIndex < toggles.Length; optionIndex++)
            {
                toggles[optionIndex].SetIsOnWithoutNotify(optionIndex == selectedOptionIndex);
            }
        }

        private static void SyncOpenDropdownItemToggles(TMP_Dropdown dropdown, int selectedOptionIndex)
        {
            if (TmpDropdownListField?.GetValue(dropdown) is not GameObject dropdownList || dropdownList == null)
            {
                return;
            }

            var toggles = dropdownList.GetComponentsInChildren<Toggle>(true).Skip(1).Take(dropdown.options.Count).ToArray();
            for (var optionIndex = 0; optionIndex < toggles.Length; optionIndex++)
            {
                toggles[optionIndex].SetIsOnWithoutNotify(optionIndex == selectedOptionIndex);
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




