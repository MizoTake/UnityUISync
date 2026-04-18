using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync
{
    internal static class CanvasUiSyncBindingsService
    {
        private const string DropdownListObjectName = "Dropdown List";
        private const string DropdownBlockerObjectName = "Blocker";
        internal readonly struct BindingScanContext
        {
            public BindingScanContext(CanvasUiSync owner)
            {
                this.owner = owner;
                Dropdowns = owner.dropdownScratch;
                TmpDropdowns = owner.tmpDropdownScratch;
                pathCache = owner.pathCacheScratch;
                dropdownTemplateRoots = owner.dropdownTemplateRootScratch;
                dropdownRuntimeRoots = owner.dropdownRuntimeRootScratch;
                pathCache.Clear();
                CollectComponentsInChildren(owner, Dropdowns);
                CollectComponentsInChildren(owner, TmpDropdowns);
                EnsureDropdownTemplateMarkers(owner, Dropdowns);
                EnsureDropdownTemplateMarkers(owner, TmpDropdowns);
                dropdownTemplateRoots.Clear();
                dropdownRuntimeRoots.Clear();
                CacheDropdownRoots(owner, Dropdowns, dropdownTemplateRoots, dropdownRuntimeRoots);
                CacheDropdownRoots(owner, TmpDropdowns, dropdownTemplateRoots, dropdownRuntimeRoots);
            }

            private readonly CanvasUiSync owner;
            private readonly Dictionary<Transform, string> pathCache;
            private readonly List<Transform> dropdownTemplateRoots;
            private readonly List<Transform> dropdownRuntimeRoots;
            public List<Dropdown> Dropdowns { get; }
            public List<TMP_Dropdown> TmpDropdowns { get; }

            public string BuildSyncId(Transform target, string componentType)
            {
                return CanvasUiSyncBindingsService.BuildSyncId(owner, target, componentType, pathCache);
            }

            public bool IsTemplateComponent(Transform target)
            {
                return IsUnderRoots(target, dropdownTemplateRoots);
            }

            public bool IsRuntimeComponent(Transform target)
            {
                return IsUnderRoots(target, dropdownRuntimeRoots);
            }

            private static void CacheDropdownRoots(CanvasUiSync owner, IEnumerable<Dropdown> dropdowns, List<Transform> templateRoots, List<Transform> runtimeRoots)
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

                    var runtimeRoot = FindRuntimeDropdownRoot(owner, dropdown);
                    if (runtimeRoot != null)
                    {
                        runtimeRoots.Add(runtimeRoot);
                    }
                }
            }

            private static void CacheDropdownRoots(CanvasUiSync owner, IEnumerable<TMP_Dropdown> dropdowns, List<Transform> templateRoots, List<Transform> runtimeRoots)
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

                    var runtimeRoot = FindRuntimeDropdownRoot(owner, dropdown);
                    if (runtimeRoot != null)
                    {
                        runtimeRoots.Add(runtimeRoot);
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
            var context = new BindingScanContext(owner);
            RegisterToggles(owner, context);
            RegisterSliders(owner, context);
            RegisterScrollbars(owner, context);
            RegisterDropdowns(owner, context);
            RegisterTmpDropdowns(owner, context);
            RegisterInputFields(owner, context);
            RegisterTmpInputFields(owner, context);
            RegisterButtons(owner, context);
            owner.registryHash = ComputeRegistryHash(owner);
        }

        internal static void RegisterToggles(CanvasUiSync owner)
        {
            RegisterToggles(owner, new BindingScanContext(owner));
        }

        private static void RegisterToggles(CanvasUiSync owner, BindingScanContext context)
        {
            CollectComponentsInChildren(owner, owner.toggleScratch);
            for (var index = 0; index < owner.toggleScratch.Count; index++)
            {
                var component = owner.toggleScratch[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "Toggle"), "Toggle", () => component.isOn, value => component.SetIsOnWithoutNotify(Convert.ToBoolean(value)), false);
                UnityEngine.Events.UnityAction<bool> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterSliders(CanvasUiSync owner)
        {
            RegisterSliders(owner, new BindingScanContext(owner));
        }

        private static void RegisterSliders(CanvasUiSync owner, BindingScanContext context)
        {
            CollectComponentsInChildren(owner, owner.sliderScratch);
            for (var index = 0; index < owner.sliderScratch.Count; index++)
            {
                var component = owner.sliderScratch[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "Slider"), "Slider", () => component.value, value => component.SetValueWithoutNotify(Convert.ToSingle(value)), true);
                UnityEngine.Events.UnityAction<float> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterScrollbars(CanvasUiSync owner)
        {
            RegisterScrollbars(owner, new BindingScanContext(owner));
        }

        private static void RegisterScrollbars(CanvasUiSync owner, BindingScanContext context)
        {
            CollectComponentsInChildren(owner, owner.scrollbarScratch);
            for (var index = 0; index < owner.scrollbarScratch.Count; index++)
            {
                var component = owner.scrollbarScratch[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "Scrollbar"), "Scrollbar", () => component.value, value => component.SetValueWithoutNotify(Convert.ToSingle(value)), true);
                UnityEngine.Events.UnityAction<float> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
            }
        }

        internal static void RegisterDropdowns(CanvasUiSync owner)
        {
            RegisterDropdowns(owner, new BindingScanContext(owner));
        }

        private static void RegisterDropdowns(CanvasUiSync owner, BindingScanContext context)
        {
            for (var index = 0; index < context.Dropdowns.Count; index++)
            {
                var component = context.Dropdowns[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "Dropdown"), "Dropdown", () => component.value, value => SetDropdownValue(owner, component, Convert.ToInt32(value)), false, true);
                UnityEngine.Events.UnityAction<int> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "DropdownExpanded"), "DropdownExpanded", () => IsDropdownExpanded(owner, component), value => SetDropdownExpanded(owner, component, Convert.ToBoolean(value)), false, true));
                RegisterDropdownItemToggles(owner, component, context);
            }
        }

        internal static void RegisterTmpDropdowns(CanvasUiSync owner)
        {
            RegisterTmpDropdowns(owner, new BindingScanContext(owner));
        }

        private static void RegisterTmpDropdowns(CanvasUiSync owner, BindingScanContext context)
        {
            for (var index = 0; index < context.TmpDropdowns.Count; index++)
            {
                var component = context.TmpDropdowns[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "TMP_Dropdown"), "TMP_Dropdown", () => component.value, value => SetDropdownValue(owner, component, Convert.ToInt32(value)), false, true);
                UnityEngine.Events.UnityAction<int> listener = value => owner.OnLocalStateChanged(binding, value, false);
                binding.Unsubscribe = () => component.onValueChanged.RemoveListener(listener);
                component.onValueChanged.AddListener(listener);
                owner.RegisterBinding(binding);
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "TMP_DropdownExpanded"), "TMP_DropdownExpanded", () => IsDropdownExpanded(owner, component), value => SetDropdownExpanded(owner, component, Convert.ToBoolean(value)), false, true));
                RegisterTmpDropdownItemToggles(owner, component, context);
            }
        }

        internal static void RegisterInputFields(CanvasUiSync owner)
        {
            RegisterInputFields(owner, new BindingScanContext(owner));
        }

        private static void RegisterInputFields(CanvasUiSync owner, BindingScanContext context)
        {
            CollectComponentsInChildren(owner, owner.inputFieldScratch);
            for (var index = 0; index < owner.inputFieldScratch.Count; index++)
            {
                var component = owner.inputFieldScratch[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "InputField"), "InputField", () => component.text, value => component.SetTextWithoutNotify(Convert.ToString(value)), false);
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
            RegisterTmpInputFields(owner, new BindingScanContext(owner));
        }

        private static void RegisterTmpInputFields(CanvasUiSync owner, BindingScanContext context)
        {
            CollectComponentsInChildren(owner, owner.tmpInputFieldScratch);
            for (var index = 0; index < owner.tmpInputFieldScratch.Count; index++)
            {
                var component = owner.tmpInputFieldScratch[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "TMP_InputField"), "TMP_InputField", () => component.text, value => component.SetTextWithoutNotify(Convert.ToString(value)), false);
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
            RegisterButtons(owner, new BindingScanContext(owner));
        }

        private static void RegisterButtons(CanvasUiSync owner, BindingScanContext context)
        {
            CollectComponentsInChildren(owner, owner.buttonScratch);
            for (var index = 0; index < owner.buttonScratch.Count; index++)
            {
                var component = owner.buttonScratch[index];
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                var binding = new CanvasUiSync.UiSyncBinding(component, context.BuildSyncId(component.transform, "Button"), "Button", null, null, false);
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
            return BuildSyncId(owner, target, componentType, null);
        }

        private static string BuildSyncId(CanvasUiSync owner, Transform target, string componentType, Dictionary<Transform, string> pathCache)
        {
            var bindingId = ReadExplicitBindingId(target);
            if (!string.IsNullOrWhiteSpace(bindingId))
            {
                return owner.canvasId + "/" + bindingId + ":" + componentType;
            }

            return owner.canvasId + "/" + BuildPath(owner, target, pathCache) + ":" + componentType;
        }

        internal static string BuildPath(CanvasUiSync owner, Transform target)
        {
            return BuildPath(owner, target, null);
        }

        private static string BuildPath(CanvasUiSync owner, Transform target, Dictionary<Transform, string> pathCache)
        {
            if (target == null)
            {
                return string.Empty;
            }

            if (pathCache != null && pathCache.TryGetValue(target, out var cachedPath))
            {
                return cachedPath;
            }

            string path;
            if (target == owner.transform)
            {
                path = owner.transform.name;
            }
            else
            {
                var segment = BuildPathSegment(target);
                path = target.parent == null || target.parent == owner.transform ? segment : BuildPath(owner, target.parent, pathCache) + "/" + segment;
            }

            if (pathCache != null)
            {
                pathCache[target] = path;
            }

            return path;
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
            owner.bindingKeyScratch.Clear();
            foreach (var pair in owner.bindings)
            {
                owner.bindingKeyScratch.Add(pair.Key);
            }

            owner.bindingKeyScratch.Sort(StringComparer.Ordinal);
            var builder = owner.stringBuilderScratch;
            builder.Length = 0;
            for (var index = 0; index < owner.bindingKeyScratch.Count; index++)
            {
                var key = owner.bindingKeyScratch[index];
                if (index > 0)
                {
                    builder.Append('|');
                }

                builder.Append(key);
                builder.Append(':');
                builder.Append(owner.bindings[key].ValueType);
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                builder.Length = 0;
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
            owner.bindingHierarchySignature = signature;
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
                CollectComponentsInChildren(owner, owner.toggleScratch);
                CollectComponentsInChildren(owner, owner.sliderScratch);
                CollectComponentsInChildren(owner, owner.scrollbarScratch);
                CollectComponentsInChildren(owner, owner.inputFieldScratch);
                CollectComponentsInChildren(owner, owner.tmpInputFieldScratch);
                CollectComponentsInChildren(owner, owner.buttonScratch);
                AppendBindingHierarchySignature(owner, ref hash, owner.toggleScratch, "Toggle", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.sliderScratch, "Slider", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.scrollbarScratch, "Scrollbar", context);
                AppendBindingHierarchySignature(owner, ref hash, context.Dropdowns, "Dropdown", context);
                AppendDropdownItemToggleBindingSignatures(owner, ref hash, context);
                AppendBindingHierarchySignature(owner, ref hash, context.TmpDropdowns, "TMP_Dropdown", context);
                AppendTmpDropdownItemToggleBindingSignatures(owner, ref hash, context);
                AppendBindingHierarchySignature(owner, ref hash, owner.inputFieldScratch, "InputField", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.tmpInputFieldScratch, "TMP_InputField", context);
                AppendBindingHierarchySignature(owner, ref hash, owner.buttonScratch, "Button", context);
                return hash;
            }
        }

        internal static void AppendBindingHierarchySignature<TComponent>(CanvasUiSync owner, ref int hash, IEnumerable<TComponent> components, string componentType, BindingScanContext? context = null) where TComponent : Component
        {
            foreach (var component in components)
            {
                if (ShouldSkipComponent(owner, component, context))
                {
                    continue;
                }

                hash = (hash * 31) + ComputeStableHash(context.HasValue ? context.Value.BuildSyncId(component.transform, componentType) : owner.BuildSyncId(component.transform, componentType));
            }
        }

        private static void RegisterDropdownItemToggles(CanvasUiSync owner, Dropdown dropdown, BindingScanContext context)
        {
            for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
            {
                var capturedOptionIndex = optionIndex;
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(dropdown, context.BuildSyncId(dropdown.transform, "DropdownItemToggle[" + capturedOptionIndex + "]"), "Toggle", () => ReadDropdownItemToggle(owner, dropdown, capturedOptionIndex), value => SetDropdownItemToggle(owner, dropdown, capturedOptionIndex, Convert.ToBoolean(value)), false));
            }
        }

        private static void RegisterTmpDropdownItemToggles(CanvasUiSync owner, TMP_Dropdown dropdown, BindingScanContext context)
        {
            for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
            {
                var capturedOptionIndex = optionIndex;
                owner.RegisterBinding(new CanvasUiSync.UiSyncBinding(dropdown, context.BuildSyncId(dropdown.transform, "TMP_DropdownItemToggle[" + capturedOptionIndex + "]"), "Toggle", () => ReadDropdownItemToggle(owner, dropdown, capturedOptionIndex), value => SetDropdownItemToggle(owner, dropdown, capturedOptionIndex, Convert.ToBoolean(value)), false));
            }
        }

        private static void AppendDropdownItemToggleBindingSignatures(CanvasUiSync owner, ref int hash, BindingScanContext context)
        {
            foreach (var dropdown in context.Dropdowns)
            {
                if (ShouldSkipComponent(owner, dropdown, context))
                {
                    continue;
                }

                for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
                {
                    hash = (hash * 31) + ComputeStableHash(context.BuildSyncId(dropdown.transform, "DropdownItemToggle[" + optionIndex + "]"));
                }
            }
        }

        private static void AppendTmpDropdownItemToggleBindingSignatures(CanvasUiSync owner, ref int hash, BindingScanContext context)
        {
            foreach (var dropdown in context.TmpDropdowns)
            {
                if (ShouldSkipComponent(owner, dropdown, context))
                {
                    continue;
                }

                for (var optionIndex = 0; optionIndex < dropdown.options.Count; optionIndex++)
                {
                    hash = (hash * 31) + ComputeStableHash(context.BuildSyncId(dropdown.transform, "TMP_DropdownItemToggle[" + optionIndex + "]"));
                }
            }
        }

        private static bool ShouldSkipComponent(CanvasUiSync owner, Component component, BindingScanContext? context = null)
        {
            return component == null || owner.IsComponentExcluded(component) || IsDropdownBlockerComponent(component) || !(component is Dropdown) && !(component is TMP_Dropdown) && (IsDropdownTemplateComponent(owner, component.transform, context) || IsDropdownRuntimeComponent(owner, component.transform, context));
        }

        private static bool IsDropdownTemplateComponent(CanvasUiSync owner, Transform target, BindingScanContext? context = null)
        {
            if (context.HasValue)
            {
                return context.Value.IsTemplateComponent(target);
            }

            CollectComponentsInChildren(owner, owner.dropdownScratch);
            for (var index = 0; index < owner.dropdownScratch.Count; index++)
            {
                var dropdown = owner.dropdownScratch[index];
                if (dropdown.template != null && (target == dropdown.template || target.IsChildOf(dropdown.template)))
                {
                    return true;
                }
            }

            CollectComponentsInChildren(owner, owner.tmpDropdownScratch);
            for (var index = 0; index < owner.tmpDropdownScratch.Count; index++)
            {
                var dropdown = owner.tmpDropdownScratch[index];
                if (dropdown.template != null && (target == dropdown.template || target.IsChildOf(dropdown.template)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDropdownRuntimeComponent(CanvasUiSync owner, Transform target, BindingScanContext? context = null)
        {
            if (context.HasValue)
            {
                return context.Value.IsRuntimeComponent(target);
            }

            CollectComponentsInChildren(owner, owner.dropdownScratch);
            EnsureDropdownTemplateMarkers(owner, owner.dropdownScratch);
            for (var index = 0; index < owner.dropdownScratch.Count; index++)
            {
                var dropdown = owner.dropdownScratch[index];
                var runtimeRoot = FindRuntimeDropdownRoot(owner, dropdown);
                if (runtimeRoot != null && (target == runtimeRoot || target.IsChildOf(runtimeRoot)))
                {
                    return true;
                }
            }

            CollectComponentsInChildren(owner, owner.tmpDropdownScratch);
            EnsureDropdownTemplateMarkers(owner, owner.tmpDropdownScratch);
            for (var index = 0; index < owner.tmpDropdownScratch.Count; index++)
            {
                var dropdown = owner.tmpDropdownScratch[index];
                var runtimeRoot = FindRuntimeDropdownRoot(owner, dropdown);
                if (runtimeRoot != null && (target == runtimeRoot || target.IsChildOf(runtimeRoot)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDropdownExpanded(CanvasUiSync owner, Dropdown dropdown)
        {
            return GetRuntimeDropdownList(owner, dropdown) != null;
        }

        private static bool IsDropdownExpanded(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            return GetRuntimeDropdownList(owner, dropdown) != null;
        }

        private static void SetDropdownExpanded(CanvasUiSync owner, Dropdown dropdown, bool isExpanded)
        {
            if (isExpanded)
            {
                if (!IsDropdownExpanded(owner, dropdown))
                {
                    dropdown.Show();
                }

                return;
            }

            if (IsDropdownExpanded(owner, dropdown))
            {
                dropdown.Hide();
            }
        }

        private static bool ReadDropdownItemToggle(CanvasUiSync owner, Dropdown dropdown, int optionIndex)
        {
            return dropdown != null && optionIndex >= 0 && optionIndex < dropdown.options.Count && dropdown.value == optionIndex;
        }

        private static bool ReadDropdownItemToggle(CanvasUiSync owner, TMP_Dropdown dropdown, int optionIndex)
        {
            return dropdown != null && optionIndex >= 0 && optionIndex < dropdown.options.Count && dropdown.value == optionIndex;
        }

        private static void SetDropdownItemToggle(CanvasUiSync owner, Dropdown dropdown, int optionIndex, bool isOn)
        {
            if (dropdown == null || optionIndex < 0 || optionIndex >= dropdown.options.Count)
            {
                return;
            }

            if (isOn)
            {
                SetDropdownValue(owner, dropdown, optionIndex);
                return;
            }

            var runtimeToggle = GetDropdownItemToggle(owner, dropdown, optionIndex);
            if (runtimeToggle != null && dropdown.value != optionIndex)
            {
                runtimeToggle.SetIsOnWithoutNotify(false);
            }
        }

        private static void SetDropdownItemToggle(CanvasUiSync owner, TMP_Dropdown dropdown, int optionIndex, bool isOn)
        {
            if (dropdown == null || optionIndex < 0 || optionIndex >= dropdown.options.Count)
            {
                return;
            }

            if (isOn)
            {
                SetDropdownValue(owner, dropdown, optionIndex);
                return;
            }

            var runtimeToggle = GetDropdownItemToggle(owner, dropdown, optionIndex);
            if (runtimeToggle != null && dropdown.value != optionIndex)
            {
                runtimeToggle.SetIsOnWithoutNotify(false);
            }
        }

        private static Toggle GetDropdownItemToggle(CanvasUiSync owner, Dropdown dropdown, int optionIndex)
        {
            var dropdownList = GetRuntimeDropdownList(owner, dropdown);
            if (dropdownList == null)
            {
                return null;
            }

            return GetDropdownItemToggle(owner, dropdownList, dropdown.options.Count, optionIndex);
        }

        private static Toggle GetDropdownItemToggle(CanvasUiSync owner, TMP_Dropdown dropdown, int optionIndex)
        {
            var dropdownList = GetRuntimeDropdownList(owner, dropdown);
            if (dropdownList == null)
            {
                return null;
            }

            return GetDropdownItemToggle(owner, dropdownList, dropdown.options.Count, optionIndex);
        }

        private static void SetDropdownValue(CanvasUiSync owner, Dropdown dropdown, int value)
        {
            if (dropdown.value != value)
            {
                dropdown.value = value;
                return;
            }

            dropdown.RefreshShownValue();
            SyncOpenDropdownItemToggles(owner, dropdown, value);
        }

        private static void SetDropdownValue(CanvasUiSync owner, TMP_Dropdown dropdown, int value)
        {
            if (dropdown.value != value)
            {
                dropdown.value = value;
                return;
            }

            dropdown.RefreshShownValue();
            SyncOpenDropdownItemToggles(owner, dropdown, value);
        }

        private static void SyncOpenDropdownItemToggles(CanvasUiSync owner, Dropdown dropdown, int selectedOptionIndex)
        {
            var dropdownList = GetRuntimeDropdownList(owner, dropdown);
            if (dropdownList == null)
            {
                return;
            }

            SyncOpenDropdownItemToggles(owner, dropdownList, dropdown.options.Count, selectedOptionIndex);
        }

        private static void SyncOpenDropdownItemToggles(CanvasUiSync owner, TMP_Dropdown dropdown, int selectedOptionIndex)
        {
            var dropdownList = GetRuntimeDropdownList(owner, dropdown);
            if (dropdownList == null)
            {
                return;
            }

            SyncOpenDropdownItemToggles(owner, dropdownList, dropdown.options.Count, selectedOptionIndex);
        }

        private static void SetDropdownExpanded(CanvasUiSync owner, TMP_Dropdown dropdown, bool isExpanded)
        {
            if (isExpanded)
            {
                if (!IsDropdownExpanded(owner, dropdown))
                {
                    dropdown.Show();
                }

                return;
            }

            if (IsDropdownExpanded(owner, dropdown))
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

        private static void CollectComponentsInChildren<TComponent>(CanvasUiSync owner, List<TComponent> results) where TComponent : Component
        {
            results.Clear();
            owner.GetComponentsInChildren(true, results);
        }

        private static void EnsureDropdownTemplateMarkers(CanvasUiSync owner, IEnumerable<Dropdown> dropdowns)
        {
            foreach (var dropdown in dropdowns)
            {
                if (dropdown?.template == null)
                {
                    continue;
                }

                CanvasUiSyncDropdownRuntimeMarker.GetOrAdd(dropdown.template.gameObject).Configure(owner, dropdown);
            }
        }

        private static void EnsureDropdownTemplateMarkers(CanvasUiSync owner, IEnumerable<TMP_Dropdown> dropdowns)
        {
            foreach (var dropdown in dropdowns)
            {
                if (dropdown?.template == null)
                {
                    continue;
                }

                CanvasUiSyncDropdownRuntimeMarker.GetOrAdd(dropdown.template.gameObject).Configure(owner, dropdown);
            }
        }

        private static Transform FindRuntimeDropdownRoot(CanvasUiSync owner, Dropdown dropdown)
        {
            if (dropdown?.template == null)
            {
                return null;
            }

            CanvasUiSyncDropdownRuntimeMarker.GetOrAdd(dropdown.template.gameObject).Configure(owner, dropdown);
            var markedRoot = FindMarkedRuntimeDropdownRoot(owner, dropdown);
            return markedRoot != null ? markedRoot : FindRuntimeDropdownRootByHeuristic(dropdown.template);
        }

        private static Transform FindRuntimeDropdownRoot(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            if (dropdown?.template == null)
            {
                return null;
            }

            CanvasUiSyncDropdownRuntimeMarker.GetOrAdd(dropdown.template.gameObject).Configure(owner, dropdown);
            var markedRoot = FindMarkedRuntimeDropdownRoot(owner, dropdown);
            return markedRoot != null ? markedRoot : FindRuntimeDropdownRootByHeuristic(dropdown.template);
        }

        private static Transform FindMarkedRuntimeDropdownRoot(CanvasUiSync owner, Dropdown dropdown)
        {
            var templateParent = dropdown.template.parent;
            if (templateParent == null)
            {
                return null;
            }

            for (var index = 0; index < templateParent.childCount; index++)
            {
                var child = templateParent.GetChild(index);
                if (child == dropdown.template)
                {
                    continue;
                }

                if (child.TryGetComponent<CanvasUiSyncDropdownRuntimeMarker>(out var marker) && marker.Matches(owner, dropdown))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindMarkedRuntimeDropdownRoot(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            var templateParent = dropdown.template.parent;
            if (templateParent == null)
            {
                return null;
            }

            for (var index = 0; index < templateParent.childCount; index++)
            {
                var child = templateParent.GetChild(index);
                if (child == dropdown.template)
                {
                    continue;
                }

                if (child.TryGetComponent<CanvasUiSyncDropdownRuntimeMarker>(out var marker) && marker.Matches(owner, dropdown))
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindRuntimeDropdownRootByHeuristic(Transform templateRoot)
        {
            if (templateRoot == null || templateRoot.parent == null)
            {
                return null;
            }

            var templateParent = templateRoot.parent;
            for (var index = 0; index < templateParent.childCount; index++)
            {
                var child = templateParent.GetChild(index);
                if (child == templateRoot || !string.Equals(child.name, DropdownListObjectName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (child.TryGetComponent<Canvas>(out var popupCanvas) && popupCanvas.overrideSorting)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject GetRuntimeDropdownList(CanvasUiSync owner, Dropdown dropdown)
        {
            var runtimeRoot = FindRuntimeDropdownRoot(owner, dropdown);
            return runtimeRoot != null ? runtimeRoot.gameObject : null;
        }

        private static GameObject GetRuntimeDropdownList(CanvasUiSync owner, TMP_Dropdown dropdown)
        {
            var runtimeRoot = FindRuntimeDropdownRoot(owner, dropdown);
            return runtimeRoot != null ? runtimeRoot.gameObject : null;
        }

        private static bool IsDropdownBlockerComponent(Component component)
        {
            return component is Button button && IsDropdownBlocker(button.gameObject);
        }

        private static bool IsDropdownBlocker(GameObject gameObject)
        {
            return gameObject != null && string.Equals(gameObject.name, DropdownBlockerObjectName, StringComparison.Ordinal) && gameObject.GetComponent<Canvas>() != null && gameObject.GetComponent<Image>() != null && gameObject.GetComponent<Button>() != null;
        }

        private static Toggle GetDropdownItemToggle(CanvasUiSync owner, GameObject dropdownList, int optionCount, int optionIndex)
        {
            CollectDropdownItemToggles(dropdownList, owner.dropdownItemToggleScratch);
            var runtimeToggleIndex = 0;
            for (var index = 1; index < owner.dropdownItemToggleScratch.Count && runtimeToggleIndex < optionCount; index++, runtimeToggleIndex++)
            {
                if (runtimeToggleIndex == optionIndex)
                {
                    return owner.dropdownItemToggleScratch[index];
                }
            }

            return null;
        }

        private static void SyncOpenDropdownItemToggles(CanvasUiSync owner, GameObject dropdownList, int optionCount, int selectedOptionIndex)
        {
            CollectDropdownItemToggles(dropdownList, owner.dropdownItemToggleScratch);
            var runtimeToggleIndex = 0;
            for (var index = 1; index < owner.dropdownItemToggleScratch.Count && runtimeToggleIndex < optionCount; index++, runtimeToggleIndex++)
            {
                var toggle = owner.dropdownItemToggleScratch[index];
                if (toggle != null)
                {
                    toggle.SetIsOnWithoutNotify(runtimeToggleIndex == selectedOptionIndex);
                }
            }
        }

        private static void CollectDropdownItemToggles(GameObject dropdownList, List<Toggle> results)
        {
            results.Clear();
            dropdownList.GetComponentsInChildren(true, results);
        }
    }
}




