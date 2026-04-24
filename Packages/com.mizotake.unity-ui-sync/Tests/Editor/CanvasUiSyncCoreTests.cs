using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using uOSC;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync.Tests.Editor
{
    public sealed class CanvasUiSyncCoreTests
    {
        private const string SampleScenePath = "Assets/Scenes/UnityUiSyncSample.unity";
        private const string PerformanceScenePath = "Assets/Scenes/UnityUiSyncPerformanceSample.unity";
        private const string AssetProfileDirectoryPath = "Assets/UnityUISyncSamples/Profiles";
        private const string PackageSampleRootPath = "Packages/com.mizotake.unity-ui-sync/Samples~/Basic Setup";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void RegistryHash_IsCreatedFromScannedBindings()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var panelObject = new GameObject("LightingPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var sliderObject = new GameObject("MasterFader", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(panelObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");
            var registryHash = (string)GetPrivateField(sync, "registryHash");
            Assert.That(registryHash, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void SameNameSiblings_AreRegisteredAsDifferentBindings()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            var panelObject0 = new GameObject("Panel", typeof(RectTransform), typeof(Toggle));
            panelObject0.transform.SetParent(canvasObject.transform, false);
            var panelObject1 = new GameObject("Panel", typeof(RectTransform), typeof(Toggle));
            panelObject1.transform.SetParent(canvasObject.transform, false);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");
            var bindings = (IDictionary)GetPrivateField(sync, "bindings");
            Assert.That(bindings.Count, Is.EqualTo(2));
            Assert.That(bindings.Contains("OperationCanvas/Panel[0]:Toggle"), Is.True);
            Assert.That(bindings.Contains("OperationCanvas/Panel[1]:Toggle"), Is.True);
        }

        [Test]
        public void BuildSyncId_UsesCanvasRelativePath_WhenNamesAreUnique()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            var panelObject = new GameObject("LightingPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var sliderObject = new GameObject("MasterFader", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(panelObject.transform, false);
            InvokePrivate(sync, "Awake");
            var result = (string)InvokePrivate(sync, "BuildSyncId", sliderObject.transform, "Slider");
            Assert.That(result, Is.EqualTo("OperationCanvas/LightingPanel/MasterFader:Slider"));
        }

        [Test]
        public void BuildSyncId_UsesSiblingOrder_WhenNamesOverlap()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            var panelObject0 = new GameObject("LightingPanel", typeof(RectTransform));
            panelObject0.transform.SetParent(canvasObject.transform, false);
            var panelObject1 = new GameObject("LightingPanel", typeof(RectTransform));
            panelObject1.transform.SetParent(canvasObject.transform, false);
            var sliderObject0 = new GameObject("MasterFader", typeof(RectTransform), typeof(Slider));
            sliderObject0.transform.SetParent(panelObject0.transform, false);
            var sliderObject1 = new GameObject("MasterFader", typeof(RectTransform), typeof(Slider));
            sliderObject1.transform.SetParent(panelObject1.transform, false);
            InvokePrivate(sync, "Awake");
            var result0 = (string)InvokePrivate(sync, "BuildSyncId", sliderObject0.transform, "Slider");
            var result1 = (string)InvokePrivate(sync, "BuildSyncId", sliderObject1.transform, "Slider");
            Assert.That(result0, Is.EqualTo("OperationCanvas/LightingPanel[0]/MasterFader:Slider"));
            Assert.That(result1, Is.EqualTo("OperationCanvas/LightingPanel[1]/MasterFader:Slider"));
        }

        [Test]
        public void BuildSyncId_UsesBindingIdComponentWhenProvided()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            var panelObject = new GameObject("LightingPanel", typeof(RectTransform));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var sliderObject = new GameObject("MasterFader", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(panelObject.transform, false);
            var bindingId = AddBindingId(sliderObject, "CustomSlider");
            Assert.That(bindingId, Is.Not.Null);
            InvokePrivate(sync, "Awake");
            var result = (string)InvokePrivate(sync, "BuildSyncId", sliderObject.transform, "Slider");
            Assert.That(result, Is.EqualTo("OperationCanvas/CustomSlider:Slider"));
        }

        [Test]
        public void FindPeerTarget_ReturnsEndpointMatchingNodeId()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerA", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9002, enabled = true });
            AssignProfile(sync, profile);
            var endpoint = InvokePrivate(sync, "FindPeerTarget", "PeerB");
            Assert.That(endpoint, Is.Not.Null);
            Assert.That((string)endpoint.GetType().GetField("name").GetValue(endpoint), Is.EqualTo("PeerB"));
        }

        [Test]
        public void HandleRequestSnapshot_UpdatesTransportToRequestedEndpoint()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerA");
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerA", ipAddress = "192.168.0.10", port = 10001, enabled = true });
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "192.168.0.20", port = 10002, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "HandleRequestSnapshot", "PeerB", "OperationCanvas", "hash");
            var client = (uOscClient)GetPrivateField(sync, "client");
            Assert.That(client.address, Is.EqualTo("192.168.0.20"));
            Assert.That(client.port, Is.EqualTo(10002));
        }

        [Test]
        public void Awake_InitializesOscTransport_EvenWhenProfileDisablesOscTransport()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.enableOscTransport = false;
            profile.listenPort = 9012;
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var server = (uOscServer)GetPrivateField(sync, "server");
            var client = (uOscClient)GetPrivateField(sync, "client");
            Assert.That(server, Is.Not.Null);
            Assert.That(client, Is.Not.Null);
            Assert.That(server.port, Is.EqualTo(9012));
            Assert.That(client.address, Is.EqualTo("127.0.0.1"));
            Assert.That(client.port, Is.EqualTo(9012));
        }

        [Test]
        public void Awake_AttachesMissingTransportComponentsToDedicatedTransportHost()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.listenPort = 9013;
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var server = (uOscServer)GetPrivateField(sync, "server");
            var client = (uOscClient)GetPrivateField(sync, "client");
            Assert.That(canvasObject.GetComponent<uOscServer>(), Is.Null);
            Assert.That(canvasObject.GetComponent<uOscClient>(), Is.Null);
            Assert.That(server, Is.Not.Null);
            Assert.That(client, Is.Not.Null);
            Assert.That(server.gameObject, Is.SameAs(client.gameObject));
            Assert.That(server.gameObject.name, Is.EqualTo("__CanvasUiSyncTransport"));
            Assert.That(server.transform.parent, Is.EqualTo(canvasObject.transform));
        }

        [Test]
        public void HandleRequestSnapshot_UsesOscEndpointEvenWhenPeerIsLocal()
        {
            var peerACanvas = new GameObject("PeerACanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(peerACanvas.transform, false);
            var peerASync = peerACanvas.AddComponent<CanvasUiSync>();
            var peerAProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            peerAProfile.nodeId = "PeerA";
            peerAProfile.enableOscTransport = false;
            peerAProfile.listenPort = 9000;
            peerAProfile.allowedPeers.Add("PeerB");
            peerAProfile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(peerASync, peerAProfile);
            AssignCanvasIdOverride(peerASync, "DemoCanvas");

            var peerBCanvas = new GameObject("PeerBCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(peerBCanvas.transform, false);
            var peerBSync = peerBCanvas.AddComponent<CanvasUiSync>();
            var peerBProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            peerBProfile.nodeId = "PeerB";
            peerBProfile.enableOscTransport = false;
            peerBProfile.listenPort = 9001;
            peerBProfile.allowedPeers.Add("PeerA");
            peerBProfile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerA", ipAddress = "127.0.0.1", port = 9000, enabled = true });
            AssignProfile(peerBSync, peerBProfile);
            AssignCanvasIdOverride(peerBSync, "DemoCanvas");

            InvokePrivate(peerASync, "Awake");
            InvokePrivate(peerBSync, "Awake");
            InvokePrivate(peerASync, "HandleRequestSnapshot", "PeerB", "DemoCanvas", "hash");

            var client = (uOscClient)GetPrivateField(peerASync, "client");
            Assert.That(client.address, Is.EqualTo("127.0.0.1"));
            Assert.That(client.port, Is.EqualTo(9001));
        }

        [Test]
        public void HandleCommitState_AfterRuntimeGeneratedToggle_RescansAndAppliesRemoteState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var toggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            var syncId = (string)InvokePrivate(sync, "BuildSyncId", toggleObject.transform, "Toggle");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", true, 100L, "PeerB", 1);

            Assert.That(toggle.isOn, Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId), Is.True);
        }

        [Test]
        public void HandleCommitState_AfterRuntimeGeneratedDropdown_RescansAndAppliesRemoteState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "RuntimeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            dropdown.options.Add(new Dropdown.OptionData("Bypass"));
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            var syncId = (string)InvokePrivate(sync, "BuildSyncId", dropdownObject.transform, "Dropdown");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Dropdown", 2, 100L, "PeerB", 1);

            Assert.That(dropdown.value, Is.EqualTo(2));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId), Is.True);
        }

        [Test]
        public void HandleCommitState_AfterRuntimeGeneratedSlider_RescansAndAppliesRemoteState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "RuntimeSlider";
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.SetValueWithoutNotify(0f);
            var syncId = (string)InvokePrivate(sync, "BuildSyncId", sliderObject.transform, "Slider");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Slider", 0.8f, 100L, "PeerB", 1);

            Assert.That(slider.value, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId), Is.True);
        }

        [Test]
        public void HandleCommitState_AfterRuntimeGeneratedScrollbar_RescansAndAppliesRemoteState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var scrollbarObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
            scrollbarObject.name = "RuntimeScrollbar";
            scrollbarObject.transform.SetParent(canvasObject.transform, false);
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.SetValueWithoutNotify(0f);
            var syncId = (string)InvokePrivate(sync, "BuildSyncId", scrollbarObject.transform, "Scrollbar");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Scrollbar", 0.35f, 100L, "PeerB", 1);

            Assert.That(scrollbar.value, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId), Is.True);
        }

        [Test]
        public void HandleCommitState_AfterRuntimeGeneratedInputField_RescansAndAppliesRemoteState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var inputObject = DefaultControls.CreateInputField(new DefaultControls.Resources());
            inputObject.name = "RuntimeInput";
            inputObject.transform.SetParent(canvasObject.transform, false);
            var inputField = inputObject.GetComponent<InputField>();
            inputField.SetTextWithoutNotify(string.Empty);
            var syncId = (string)InvokePrivate(sync, "BuildSyncId", inputObject.transform, "InputField");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "InputField", "Remote operator", 100L, "PeerB", 1);

            Assert.That(inputField.text, Is.EqualTo("Remote operator"));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId), Is.True);
        }

        [Test]
        public void ScanBindings_DropdownInternalTemplateControls_AreNotRegistered()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ModeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");

            var bindingKeys = ((IDictionary)GetPrivateField(sync, "bindings")).Keys.Cast<string>().ToArray();

            Assert.That(bindingKeys.Count(key => key.EndsWith(":Dropdown", System.StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(bindingKeys.Count(key => key.EndsWith(":DropdownExpanded", System.StringComparison.Ordinal)), Is.EqualTo(1));
            Assert.That(bindingKeys.Count(key => key.Contains(":DropdownItemToggle[", System.StringComparison.Ordinal)), Is.EqualTo(dropdownObject.GetComponent<Dropdown>().options.Count));
            Assert.That(bindingKeys.Any(key => key.EndsWith(":Toggle", System.StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void ScanBindings_ExcludedComponents_AreNotRegistered()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var includedToggleObject = new GameObject("IncludedToggle", typeof(RectTransform), typeof(Toggle));
            includedToggleObject.transform.SetParent(canvasObject.transform, false);
            var excludedDropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            excludedDropdownObject.name = "ExcludedDropdown";
            excludedDropdownObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            AssignExcludedComponents(sync, excludedDropdownObject.GetComponent<Dropdown>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");

            var bindingKeys = ((IDictionary)GetPrivateField(sync, "bindings")).Keys.Cast<string>().ToArray();

            Assert.That(bindingKeys, Does.Contain("OperationCanvas/IncludedToggle:Toggle"));
            Assert.That(bindingKeys.Any(key => key.Contains("ExcludedDropdown", System.StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void ScanBindings_DropdownOptionToggles_AreNotPolledPerFrame()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ModeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            dropdown.options.Add(new Dropdown.OptionData("Bypass"));
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");

            var polledBindingSyncIds = ((IEnumerable)GetPrivateField(sync, "polledBindings")).Cast<object>().Select(GetBindingSyncId).ToArray();

            Assert.That(polledBindingSyncIds, Does.Contain("OperationCanvas/ModeDropdown:Dropdown"));
            Assert.That(polledBindingSyncIds, Does.Not.Contain("OperationCanvas/ModeDropdown:DropdownExpanded"));
            Assert.That(polledBindingSyncIds.Any(syncId => syncId.Contains("DropdownItemToggle", System.StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void UpdatePolledBindings_ProgrammaticDropdownSelection_BroadcastsOnlyDropdownAndChangedOptions()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ModeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            dropdown.options.Add(new Dropdown.OptionData("Bypass"));
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "Start");

            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            dropdown.SetValueWithoutNotify(2);
            dropdown.RefreshShownValue();
            InvokePrivate(sync, "UpdatePolledBindings");

            var localStates = (IDictionary)GetPrivateField(sync, "localStates");
            var selectedOptionState = localStates["OperationCanvas/ModeDropdown:DropdownItemToggle[2]"];
            var previousOptionState = localStates["OperationCanvas/ModeDropdown:DropdownItemToggle[0]"];

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(3));
            Assert.That((bool)selectedOptionState.GetType().GetProperty("Value").GetValue(selectedOptionState), Is.True);
            Assert.That((bool)previousOptionState.GetType().GetProperty("Value").GetValue(previousOptionState), Is.False);
        }

        [Test]
        public void UpdatePolledBindings_LargeDropdown_ReportsReducedPerFrameWork()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "LargeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            for (var index = 0; index < 300; index++)
            {
                dropdown.options.Add(new Dropdown.OptionData("Option" + index));
            }

            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "Start");

            var polledBindings = (ICollection)GetPrivateField(sync, "polledBindings");
            var stopwatch = Stopwatch.StartNew();
            for (var index = 0; index < 1000; index++)
            {
                InvokePrivate(sync, "UpdatePolledBindings");
            }

            stopwatch.Stop();
            TestContext.WriteLine("LargeDropdown polling benchmark: polledBindings=" + polledBindings.Count + " elapsedMs=" + stopwatch.Elapsed.TotalMilliseconds.ToString("F3"));

            Assert.That(polledBindings.Count, Is.EqualTo(1));
        }

        [Test]
        public void LargeDropdown_BindingDiscoveryBenchmarks_AreReported()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "LargeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            for (var index = 0; index < 300; index++)
            {
                dropdown.options.Add(new Dropdown.OptionData("Option" + index));
            }

            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");

            var initialSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            var signature = initialSignature;
            var signatureStopwatch = Stopwatch.StartNew();
            for (var index = 0; index < 100; index++)
            {
                signature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            }

            signatureStopwatch.Stop();
            var scanStopwatch = Stopwatch.StartNew();
            for (var index = 0; index < 100; index++)
            {
                InvokePrivate(sync, "ScanBindings");
            }

            scanStopwatch.Stop();
            TestContext.WriteLine("LargeDropdown binding discovery benchmark: optionCount=300 signature100CallsMs=" + signatureStopwatch.Elapsed.TotalMilliseconds.ToString("F3") + " scan100CallsMs=" + scanStopwatch.Elapsed.TotalMilliseconds.ToString("F3"));

            Assert.That(signature, Is.EqualTo(initialSignature));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Count, Is.EqualTo(302));
        }

        [Test]
        public void ComputeBindingHierarchySignature_ReparentedExplicitBindingId_DoesNotChange()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var parentA = new GameObject("ParentA").transform;
            parentA.SetParent(canvasObject.transform, false);
            var parentB = new GameObject("ParentB").transform;
            parentB.SetParent(canvasObject.transform, false);
            var toggleObject = new GameObject("ModeToggle", typeof(RectTransform), typeof(Toggle), typeof(CanvasUiSyncBindingId));
            toggleObject.transform.SetParent(parentA, false);
            toggleObject.GetComponent<CanvasUiSyncBindingId>().BindingId = "ExplicitModeToggle";
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");

            var initialSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            toggleObject.transform.SetParent(parentB, false);
            var movedSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");

            Assert.That(movedSignature, Is.EqualTo(initialSignature));
        }

        [Test]
        public void ComputeBindingHierarchySignature_RenamedBoundObject_Changes()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("ModeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");

            var initialSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            toggleObject.name = "ModeToggleRenamed";
            var renamedSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");

            Assert.That(renamedSignature, Is.Not.EqualTo(initialSignature));
        }

        [Test]
        public void TickRuntimeHierarchyRescan_StableHierarchy_BacksOffAndResetsAfterBindingChange()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("Toggle0", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "Start");

            InvokePrivate(sync, "TickRuntimeHierarchyRescan", 0.2f);
            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.EqualTo(0.2f).Within(0.0001f));
            InvokePrivate(sync, "TickRuntimeHierarchyRescan", 0.4f);
            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.EqualTo(0.4f).Within(0.0001f));
            InvokePrivate(sync, "TickRuntimeHierarchyRescan", 0.8f);
            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.EqualTo(0.5f).Within(0.0001f));

            new GameObject("Toggle1", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            InvokePrivate(sync, "TickRuntimeHierarchyRescan", 1.3f);

            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Count, Is.EqualTo(2));
        }

        [Test]
        public void ScanBindings_ClassifiesContinuousAndPolledBindingsWithoutDuplication()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "MasterSlider";
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ModeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");

            var continuousBindings = ((IEnumerable)GetPrivateField(sync, "continuousBindings")).Cast<object>().ToArray();
            var polledBindings = ((IEnumerable)GetPrivateField(sync, "polledBindings")).Cast<object>().ToArray();

            Assert.That(continuousBindings.Length, Is.EqualTo(1));
            Assert.That(polledBindings.Length, Is.EqualTo(1));

            InvokePrivate(sync, "ScanBindings");

            Assert.That(((IEnumerable)GetPrivateField(sync, "continuousBindings")).Cast<object>().Count(), Is.EqualTo(1));
            Assert.That(((IEnumerable)GetPrivateField(sync, "polledBindings")).Cast<object>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void DropdownExpanded_RuntimeRootNotificationBroadcastsWithoutPolling()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ModeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "Start");
            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);

            var runtimeRoot = new GameObject("Dropdown List");
            InvokePrivate(sync, "HandleDropdownRuntimeRootChanged", dropdown, runtimeRoot);

            var localStates = (IDictionary)GetPrivateField(sync, "localStates");
            var expandedState = localStates["OperationCanvas/ModeDropdown:DropdownExpanded"];
            Assert.That(expandedState.GetType().GetProperty("Value").GetValue(expandedState), Is.EqualTo(true));
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));

            InvokePrivate(sync, "HandleDropdownRuntimeRootChanged", dropdown, null);
            expandedState = localStates["OperationCanvas/ModeDropdown:DropdownExpanded"];
            Assert.That(expandedState.GetType().GetProperty("Value").GetValue(expandedState), Is.EqualTo(false));
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(2));

            Object.DestroyImmediate(runtimeRoot);
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(2));
        }

        [Test]
        public void SerializeLogicalTicks_ReturnsOscSerializableString()
        {
            var method = typeof(CanvasUiSync).GetMethod("SerializeLogicalTicks", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var result = method.Invoke(null, new object[] { 1234567890123L });
            Assert.That(result, Is.TypeOf<string>());
            Assert.That((string)result, Is.EqualTo("1234567890123"));
        }

        [Test]
        public void HandleHello_DoesNotRegisterUnauthorizedPeer()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerA");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "HandleHello", "Intruder", profile.protocolVersion, "OperationCanvas", "SessionX");
            Assert.That(((IDictionary)GetPrivateField(sync, "nodes")).Count, Is.EqualTo(0));
        }

        [Test]
        public void HandleReceivedPayload_IgnoresMalformedHelloPayload()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            Assert.DoesNotThrow(() => InvokePrivate(sync, "HandleReceivedPayload", "/uisync/hello", new object[] { "PeerB", "bad-version", "OperationCanvas", "SessionB" }));
            Assert.That(((IDictionary)GetPrivateField(sync, "nodes")).Count, Is.EqualTo(0));
        }

        [Test]
        public void HandleBeginSnapshot_DoesNotTrackUnauthorizedPeer()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerA");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "Intruder", "SessionX");
            Assert.That(((IDictionary)GetPrivateField(sync, "activeSnapshotIds")).Count, Is.EqualTo(0));
        }

        [Test]
        public void HandleCommitState_UnknownSyncId_OlderPendingCommitDoesNotReplaceNewerOne()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", true, 101L, "PeerB", 2);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", false, 100L, "PeerB", 1);

            var pendingRemoteCommits = (IDictionary)GetPrivateField(sync, "pendingRemoteCommits");
            var pending = pendingRemoteCommits[syncId];
            var stamp = pending.GetType().GetProperty("Stamp").GetValue(pending);

            Assert.That(pendingRemoteCommits.Count, Is.EqualTo(1));
            Assert.That(pending.GetType().GetProperty("Value").GetValue(pending), Is.EqualTo(true));
            Assert.That((long)stamp.GetType().GetProperty("LogicalTicks").GetValue(stamp), Is.EqualTo(101L));
            Assert.That((int)stamp.GetType().GetProperty("Sequence").GetValue(stamp), Is.EqualTo(2));
        }

        [Test]
        public void HandleCommitButton_UnknownSyncId_OlderPendingCommitDoesNotReplaceNewerOne()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeButton:Button";
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", syncId, 101L, "PeerB", 2);
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", syncId, 100L, "PeerB", 1);

            var pendingRemoteButtonCommits = (IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits");
            var pending = pendingRemoteButtonCommits[syncId];
            var stamp = pending.GetType().GetProperty("Stamp").GetValue(pending);

            Assert.That(pendingRemoteButtonCommits.Count, Is.EqualTo(1));
            Assert.That((long)stamp.GetType().GetProperty("LogicalTicks").GetValue(stamp), Is.EqualTo(101L));
            Assert.That((int)stamp.GetType().GetProperty("Sequence").GetValue(stamp), Is.EqualTo(2));
        }

        [Test]
        public void InitializeLocalState_RepeatedRescan_DoesNotAccumulateTransientSyncState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            new GameObject("PowerToggle0", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            InvokePrivate(sync, "Awake");
            for (var index = 0; index < 8; index++)
            {
                foreach (var child in canvasObject.transform.Cast<Transform>().ToArray())
                {
                    Object.DestroyImmediate(child.gameObject);
                }

                new GameObject("PowerToggle" + index, typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
                InvokePrivate(sync, "ScanBindings");
                InvokePrivate(sync, "InitializeLocalState");
                var bindings = (IDictionary)GetPrivateField(sync, "bindings");
                var binding = bindings.Values.Cast<object>().Single();
                InvokePrivate(sync, "OnLocalStateChanged", binding, index % 2 == 0, false);
            }

            Assert.That(((IDictionary)GetPrivateField(sync, "lastProposedValues")).Count, Is.EqualTo(1));
            Assert.That(((IDictionary)GetPrivateField(sync, "lastProposeTimes")).Count, Is.EqualTo(1));
            Assert.That(((IDictionary)GetPrivateField(sync, "deferredCommits")).Count, Is.EqualTo(0));
        }

        [Test]
        public void ScanBindings_Repeatedly_DoesNotDuplicateToggleListeners()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            for (var index = 0; index < 12; index++)
            {
                InvokePrivate(sync, "ScanBindings");
                InvokePrivate(sync, "InitializeLocalState");
            }

            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            toggle.isOn = true;
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));
        }

        [Test]
        public void ApplyRemoteState_DefersContinuousCommitWhileInteracting()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sliderObject = new GameObject("MasterSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.value = 0.25f;
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");
            var bindings = (IDictionary)GetPrivateField(sync, "bindings");
            var binding = bindings.Values.Cast<object>().Single();
            binding.GetType().GetProperty("IsInteracting").SetValue(binding, true);
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Slider", 0.9f, 100L, "PeerB", 1);
            Assert.That(slider.value, Is.EqualTo(0.25f).Within(0.0001f));
            var deferredCommits = (IDictionary)GetPrivateField(sync, "deferredCommits");
            Assert.That(deferredCommits.Count, Is.EqualTo(1));
            InvokePrivate(sync, "OnInteractionEnded", binding);
            Assert.That(slider.value, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void ApplyRemoteState_LastWriteWinsByTimestamp()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");
            var bindings = (IDictionary)GetPrivateField(sync, "bindings");
            var binding = bindings.Values.Cast<object>().Single();
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
            InvokePrivate(sync, "ApplyRemoteState", syncId, "Toggle", true, CreateStamp(sync, 200, "PeerA", 1), false);
            InvokePrivate(sync, "ApplyRemoteState", syncId, "Toggle", false, CreateStamp(sync, 100, "PeerB", 1), false);
            Assert.That(toggle.isOn, Is.True);
        }

        [Test]
        public void HandleCommitButton_IgnoresDuplicateStamp()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var buttonObject = new GameObject("SyncButton", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var button = buttonObject.GetComponent<Button>();
            var invokeCount = 0;
            button.onClick.AddListener(() => invokeCount++);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerA");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");
            var bindings = (IDictionary)GetPrivateField(sync, "bindings");
            var binding = bindings.Values.Cast<object>().Single();
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
            InvokePrivate(sync, "HandleCommitButton", "PeerA", "SessionA", "OperationCanvas", syncId, 100L, "PeerA", 1);
            InvokePrivate(sync, "HandleCommitButton", "PeerA", "SessionA", "OperationCanvas", syncId, 100L, "PeerA", 1);
            Assert.That(invokeCount, Is.EqualTo(1));
        }

        [Test]
        public void CommitLocalState_TracksNextPendingCommitTimeAndFlushesWhenDue()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.minimumCommitBroadcastIntervalSeconds = 0.5f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var binding = ((IDictionary)GetPrivateField(sync, "bindings")).Values.Cast<object>().Single();
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
            var state = ((IDictionary)GetPrivateField(sync, "localStates"))[syncId];
            var beforeCommitTime = Time.unscaledTime;
            state.GetType().GetProperty("LastBroadcastAt").SetValue(state, beforeCommitTime);
            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            InvokePrivate(sync, "CommitLocalState", binding, true, false, CreateStamp(sync, 10L, "PeerA", 1));

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));
            Assert.That((bool)state.GetType().GetProperty("HasPendingBroadcast").GetValue(state), Is.True);
            Assert.That((float)GetPrivateField(sync, "nextPendingCommitTime"), Is.EqualTo(beforeCommitTime + 0.5f).Within(0.05f));

            InvokePrivate(sync, "FlushPendingCommits", beforeCommitTime + 0.25f);
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));

            InvokePrivate(sync, "FlushPendingCommits", beforeCommitTime + 0.51f);
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));
            Assert.That((bool)state.GetType().GetProperty("HasPendingBroadcast").GetValue(state), Is.False);
            Assert.That((float)GetPrivateField(sync, "nextPendingCommitTime"), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void RebuildSampleAssets_CreatesAssetsAndPackageSamples()
        {
            Mizotake.UnityUiSync.Editor.CanvasUiSyncSampleBuilder.RebuildSampleAssets();
            Assert.That(File.Exists(SampleScenePath), Is.True);
            Assert.That(File.Exists(PerformanceScenePath), Is.True);
            Assert.That(File.Exists(AssetProfileDirectoryPath + "/PeerA.asset"), Is.True);
            Assert.That(File.Exists(AssetProfileDirectoryPath + "/PeerB.asset"), Is.True);
            Assert.That(File.Exists(PackageSampleRootPath + "/Scenes/UnityUiSyncSample.unity"), Is.True);
            Assert.That(File.Exists(PackageSampleRootPath + "/Scenes/UnityUiSyncPerformanceSample.unity"), Is.True);
            Assert.That(File.Exists(PackageSampleRootPath + "/Profiles/PeerA.asset"), Is.True);
            Assert.That(File.Exists(PackageSampleRootPath + "/Profiles/PeerB.asset"), Is.True);
        }

        [Test]
        public void GeneratedSampleScene_HasSingleCanvasUiSyncAndRequiredControls()
        {
            Mizotake.UnityUiSync.Editor.CanvasUiSyncSampleBuilder.RebuildSampleAssets();
            var scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            var syncs = UnityEngine.Object.FindObjectsOfType<CanvasUiSync>(true);
            Assert.That(syncs, Has.Length.EqualTo(2));
            Assert.That(GameObject.Find("PeerACanvas"), Is.Not.Null);
            Assert.That(GameObject.Find("PeerBCanvas"), Is.Not.Null);
            Assert.That(FindControl<Toggle>("PeerACanvas", "PowerToggle"), Is.Not.Null);
            Assert.That(FindControl<Slider>("PeerACanvas", "MasterSlider"), Is.Not.Null);
            Assert.That(FindControl<Scrollbar>("PeerACanvas", "IntensityScrollbar"), Is.Not.Null);
            Assert.That(FindControl<Dropdown>("PeerACanvas", "ModeDropdown"), Is.Not.Null);
            Assert.That(FindControl<InputField>("PeerACanvas", "OperatorInput"), Is.Not.Null);
            Assert.That(FindControl<Button>("PeerACanvas", "SyncButton"), Is.Not.Null);
            Assert.That(FindControl<Toggle>("PeerACanvas", "TargetToggle"), Is.Not.Null);
            Assert.That(FindControl<Toggle>("PeerBCanvas", "PowerToggle"), Is.Not.Null);
            Assert.That(FindControl<Slider>("PeerBCanvas", "MasterSlider"), Is.Not.Null);
            Assert.That(FindControl<Scrollbar>("PeerBCanvas", "IntensityScrollbar"), Is.Not.Null);
            Assert.That(FindControl<Dropdown>("PeerBCanvas", "ModeDropdown"), Is.Not.Null);
            Assert.That(FindControl<InputField>("PeerBCanvas", "OperatorInput"), Is.Not.Null);
            Assert.That(FindControl<Button>("PeerBCanvas", "SyncButton"), Is.Not.Null);
            Assert.That(FindControl<Toggle>("PeerBCanvas", "TargetToggle"), Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectOfType<EventSystem>(), Is.Not.Null);
            Assert.That(GetProfile(syncs, "PeerA"), Is.Not.Null);
            Assert.That(GetProfile(syncs, "PeerB"), Is.Not.Null);
            Assert.That(GetCanvasIdOverride(syncs, "PeerA"), Is.EqualTo("DemoCanvas"));
            Assert.That(GetCanvasIdOverride(syncs, "PeerB"), Is.EqualTo("DemoCanvas"));
            AssertPanelAnchors("PeerACanvas", 0.04f, 0.48f);
            AssertPanelAnchors("PeerBCanvas", 0.52f, 0.96f);
        }

        [Test]
        public void GeneratedSampleScene_HasExpectedInitialValues()
        {
            Mizotake.UnityUiSync.Editor.CanvasUiSyncSampleBuilder.RebuildSampleAssets();
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            Assert.That(FindControl<Toggle>("PeerACanvas", "PowerToggle")?.isOn, Is.False);
            Assert.That(FindControl<Slider>("PeerACanvas", "MasterSlider")?.value, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(FindControl<Scrollbar>("PeerACanvas", "IntensityScrollbar")?.value, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(FindControl<Dropdown>("PeerACanvas", "ModeDropdown")?.value, Is.EqualTo(1));
            Assert.That(FindControl<InputField>("PeerACanvas", "OperatorInput")?.text, Is.EqualTo("Sample operator"));
            Assert.That(FindControl<Toggle>("PeerACanvas", "TargetToggle")?.isOn, Is.False);
            Assert.That(FindControl<Toggle>("PeerBCanvas", "PowerToggle")?.isOn, Is.False);
            Assert.That(FindControl<Slider>("PeerBCanvas", "MasterSlider")?.value, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(FindControl<Scrollbar>("PeerBCanvas", "IntensityScrollbar")?.value, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(FindControl<Dropdown>("PeerBCanvas", "ModeDropdown")?.value, Is.EqualTo(1));
            Assert.That(FindControl<InputField>("PeerBCanvas", "OperatorInput")?.text, Is.EqualTo("Sample operator"));
            Assert.That(FindControl<Toggle>("PeerBCanvas", "TargetToggle")?.isOn, Is.False);
        }

        [Test]
        public void GeneratedSampleScene_ButtonPersistentCall_TargetsIndicatorToggle()
        {
            Mizotake.UnityUiSync.Editor.CanvasUiSyncSampleBuilder.RebuildSampleAssets();
            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            AssertButtonTargetsLocalToggle("PeerACanvas");
            AssertButtonTargetsLocalToggle("PeerBCanvas");
        }

        [Test]
        public void GeneratedPerformanceScene_HasMeasurementOverlayAndLargeBindingSet()
        {
            Mizotake.UnityUiSync.Editor.CanvasUiSyncSampleBuilder.RebuildSampleAssets();
            var scene = EditorSceneManager.OpenScene(PerformanceScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid(), Is.True);
            var syncs = UnityEngine.Object.FindObjectsOfType<CanvasUiSync>(true);
            Assert.That(syncs, Has.Length.EqualTo(2));
            foreach (var sync in syncs)
            {
                InvokePrivate(sync, "Awake");
                InvokePrivate(sync, "Start");
            }

            Assert.That(Resources.FindObjectsOfTypeAll<CanvasUiSyncPerformanceOverlay>().Length, Is.EqualTo(1));
            Assert.That(Resources.FindObjectsOfTypeAll<Text>().Any(text => text.name == "PerformanceStatusText"), Is.True);
            foreach (var sync in syncs)
            {
                Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Count, Is.EqualTo(240));
                Assert.That(((ICollection)GetPrivateField(sync, "continuousBindings")).Count, Is.EqualTo(48));
                Assert.That(((ICollection)GetPrivateField(sync, "polledBindings")).Count, Is.EqualTo(16));
            }
        }

        [Test]
        public void GeneratedProfiles_HaveExpectedPeerSettings()
        {
            Mizotake.UnityUiSync.Editor.CanvasUiSyncSampleBuilder.RebuildSampleAssets();
            var peerA = AssetDatabase.LoadAssetAtPath<CanvasUiSyncProfile>(AssetProfileDirectoryPath + "/PeerA.asset");
            var peerB = AssetDatabase.LoadAssetAtPath<CanvasUiSyncProfile>(AssetProfileDirectoryPath + "/PeerB.asset");
            Assert.That(peerA, Is.Not.Null);
            Assert.That(peerB, Is.Not.Null);
            Assert.That(peerA.nodeId, Is.EqualTo("PeerA"));
            Assert.That(peerA.enableOscTransport, Is.True);
            Assert.That(peerA.listenPort, Is.EqualTo(9000));
            Assert.That(peerA.allowedPeers, Is.EquivalentTo(new[] { "PeerB" }));
            Assert.That(peerA.peerEndpoints.Count, Is.EqualTo(1));
            Assert.That(peerA.peerEndpoints[0].name, Is.EqualTo("PeerB"));
            Assert.That(peerA.peerEndpoints[0].port, Is.EqualTo(9001));
            Assert.That(peerB.nodeId, Is.EqualTo("PeerB"));
            Assert.That(peerB.enableOscTransport, Is.True);
            Assert.That(peerB.listenPort, Is.EqualTo(9001));
            Assert.That(peerB.allowedPeers, Is.EquivalentTo(new[] { "PeerA" }));
            Assert.That(peerB.peerEndpoints.Count, Is.EqualTo(1));
            Assert.That(peerB.peerEndpoints[0].name, Is.EqualTo("PeerA"));
            Assert.That(peerB.peerEndpoints[0].port, Is.EqualTo(9000));
        }

        [Test]
        public void Profile_OnValidate_NormalizesInvalidValues()
        {
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.profileName = " ";
            profile.nodeId = "";
            profile.protocolVersion = 0;
            profile.helloIntervalSeconds = -1f;
            profile.nodeTimeoutSeconds = -2f;
            profile.snapshotRequestIntervalSeconds = -3f;
            profile.snapshotRequestRetryCount = 0;
            profile.snapshotRetryCooldownSeconds = -4f;
            profile.snapshotStateTimeoutSeconds = -8f;
            profile.periodicFullResyncIntervalSeconds = -9f;
            profile.sliderEpsilon = -5f;
            profile.minimumProposeIntervalSeconds = -6f;
            profile.minimumCommitBroadcastIntervalSeconds = -7f;
            profile.statisticsLogIntervalSeconds = -10f;
            profile.listenPort = -1;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "", port = 99999, enabled = true });
            InvokePrivate(profile, "OnValidate");
            Assert.That(profile.profileName, Is.EqualTo("Default"));
            Assert.That(profile.nodeId, Is.EqualTo("Node"));
            Assert.That(profile.protocolVersion, Is.EqualTo(1));
            Assert.That(profile.helloIntervalSeconds, Is.EqualTo(0.1f));
            Assert.That(profile.nodeTimeoutSeconds, Is.EqualTo(0.1f));
            Assert.That(profile.snapshotRequestIntervalSeconds, Is.EqualTo(0.1f));
            Assert.That(profile.snapshotRequestRetryCount, Is.EqualTo(1));
            Assert.That(profile.snapshotRetryCooldownSeconds, Is.EqualTo(0.1f));
            Assert.That(profile.snapshotStateTimeoutSeconds, Is.EqualTo(0.5f));
            Assert.That(profile.periodicFullResyncIntervalSeconds, Is.EqualTo(0f));
            Assert.That(profile.sliderEpsilon, Is.EqualTo(0f));
            Assert.That(profile.minimumProposeIntervalSeconds, Is.EqualTo(0f));
            Assert.That(profile.minimumCommitBroadcastIntervalSeconds, Is.EqualTo(0f));
            Assert.That(profile.statisticsLogIntervalSeconds, Is.EqualTo(1f));
            Assert.That(profile.enableOscTransport, Is.True);
            Assert.That(profile.listenPort, Is.EqualTo(1));
            Assert.That(profile.peerEndpoints[0].port, Is.EqualTo(65535));
            Assert.That(profile.peerEndpoints[0].ipAddress, Is.EqualTo("127.0.0.1"));
        }

        [Test]
        public void CreateLocalStamp_UsesMonotonicLogicalClock()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var first = InvokePrivate(sync, "CreateLocalStamp");
            var second = InvokePrivate(sync, "CreateLocalStamp");
            Assert.That((long)first.GetType().GetProperty("LogicalTicks").GetValue(first), Is.EqualTo(1L));
            Assert.That((long)second.GetType().GetProperty("LogicalTicks").GetValue(second), Is.EqualTo(2L));
            Assert.That((int)second.GetType().GetProperty("Sequence").GetValue(second), Is.EqualTo(2));
            Assert.That((string)second.GetType().GetProperty("NodeId").GetValue(second), Is.EqualTo("PeerA"));
        }

        [Test]
        public void TickSnapshotCleanup_RemovesExpiredSnapshotIds()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var activeSnapshotIds = (IDictionary)GetPrivateField(sync, "activeSnapshotIds");
            activeSnapshotIds["expired"] = 5f;
            activeSnapshotIds["alive"] = 15f;
            InvokePrivate(sync, "TickSnapshotCleanup", 10f);
            Assert.That(activeSnapshotIds.Contains("expired"), Is.False);
            Assert.That(activeSnapshotIds.Contains("alive"), Is.True);
        }

        [Test]
        public void TickNodeTimeout_RemovesExpiredNodesAndKeepsRecentNodes()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeTimeoutSeconds = 5f;
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "HandleHello", "PeerA", profile.protocolVersion, "OperationCanvas", "SessionA");
            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB");
            var nodes = (IDictionary)GetPrivateField(sync, "nodes");
            nodes["PeerA"].GetType().GetProperty("LastSeenAt").SetValue(nodes["PeerA"], 1f);
            nodes["PeerB"].GetType().GetProperty("LastSeenAt").SetValue(nodes["PeerB"], 8f);
            InvokePrivate(sync, "TickNodeTimeout", 10f);
            Assert.That(nodes.Contains("PeerA"), Is.False);
            Assert.That(nodes.Contains("PeerB"), Is.True);
        }

        [Test]
        public void InitializeLocalState_PrunesStaleCachesAfterBindingSetChanges()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sliderObject = new GameObject("MasterSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            sliderObject.GetComponent<Slider>().value = 0.25f;
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");
            var bindings = (IDictionary)GetPrivateField(sync, "bindings");
            var binding = bindings.Values.Cast<object>().Single();
            binding.GetType().GetProperty("IsInteracting").SetValue(binding, true);
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
            InvokePrivate(sync, "OnLocalStateChanged", binding, 0.5f, false);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Slider", 0.9f, 100L, "PeerB", 1);
            Assert.That(((IDictionary)GetPrivateField(sync, "lastProposedValues")).Contains(syncId), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "lastProposeTimes")).Contains(syncId), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "deferredCommits")).Contains(syncId), Is.True);
            Object.DestroyImmediate(sliderObject);
            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Count, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "lastProposedValues")).Contains(syncId), Is.False);
            Assert.That(((IDictionary)GetPrivateField(sync, "lastProposeTimes")).Contains(syncId), Is.False);
            Assert.That(((IDictionary)GetPrivateField(sync, "deferredCommits")).Contains(syncId), Is.False);
        }

        [Test]
        public void TickPeriodicResync_RequestsSnapshotFromPeers()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.periodicFullResyncIntervalSeconds = 30f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "192.168.0.20", port = 10002, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            sync.GetType().GetField("hasSnapshot", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, true);
            sync.GetType().GetField("nextPeriodicResyncTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 5f);
            InvokePrivate(sync, "TickPeriodicResync", 10f);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);
            Assert.That((int)GetPrivateField(sync, "snapshotRetryCount"), Is.EqualTo(1));
            Assert.That((float)GetPrivateField(sync, "nextPeriodicResyncTime"), Is.EqualTo(40f).Within(0.0001f));
            var client = (uOscClient)GetPrivateField(sync, "client");
            Assert.That(client.address, Is.EqualTo("192.168.0.20"));
            Assert.That(client.port, Is.EqualTo(10002));
        }

        [Test]
        public void OnLocalStateChanged_UsesOscLoopbackEndpointWhenLegacyFlagIsFalse()
        {
            var canvasObject = new GameObject("PeerACanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            profile.enableOscTransport = false;
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            AssignCanvasIdOverride(sync, "DemoCanvas");
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "Start");
            var binding = ((IDictionary)GetPrivateField(sync, "bindings")).Values.Cast<object>().Single();
            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            InvokePrivate(sync, "OnLocalStateChanged", binding, true, false);
            var client = (uOscClient)GetPrivateField(sync, "client");
            Assert.That(client.address, Is.EqualTo("127.0.0.1"));
            Assert.That(client.port, Is.EqualTo(9001));
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));
        }

        [Test]
        public void SetSyncEnabled_TogglesLocalAndRemoteSynchronization()
        {
            var canvasObject = new GameObject("PeerACanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            AssignCanvasIdOverride(sync, "DemoCanvas");
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "Start");

            var binding = ((IDictionary)GetPrivateField(sync, "bindings")).Values.Cast<object>().Single();
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);

            sync.SetSyncEnabled(false);
            Assert.That(sync.SyncEnabled, Is.False);
            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            InvokePrivate(sync, "OnLocalStateChanged", binding, true, false);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "DemoCanvas", syncId, "Toggle", true, 100L, "PeerB", 1);
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));
            Assert.That(toggle.isOn, Is.False);

            sync.SetSyncEnabled(true);
            Assert.That(sync.SyncEnabled, Is.True);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "DemoCanvas", syncId, "Toggle", true, 101L, "PeerB", 2);
            Assert.That(toggle.isOn, Is.True);

            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            InvokePrivate(sync, "OnLocalStateChanged", binding, false, false);
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));
        }

        private static void AssignProfile(CanvasUiSync sync, CanvasUiSyncProfile profile)
        {
            var serializedObject = new SerializedObject(sync);
            serializedObject.FindProperty("profile").objectReferenceValue = profile;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignExcludedComponents(CanvasUiSync sync, params Component[] components)
        {
            var serializedObject = new SerializedObject(sync);
            var excludedComponentsProperty = serializedObject.FindProperty("excludedComponents");
            Assert.That(excludedComponentsProperty, Is.Not.Null);
            excludedComponentsProperty.arraySize = components.Length;
            for (var index = 0; index < components.Length; index++)
            {
                excludedComponentsProperty.GetArrayElementAtIndex(index).objectReferenceValue = components[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignCanvasIdOverride(CanvasUiSync sync, string canvasIdOverride)
        {
            var serializedObject = new SerializedObject(sync);
            serializedObject.FindProperty("canvasIdOverride").stringValue = canvasIdOverride;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T FindControl<T>(string canvasName, string controlName) where T : Component
        {
            return Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(component => component.name == controlName && component.GetComponentsInParent<Canvas>(true).Any(canvas => canvas.name == canvasName));
        }

        private static CanvasUiSyncProfile GetProfile(CanvasUiSync[] syncs, string profileName)
        {
            return syncs.Select(sync => (CanvasUiSyncProfile)GetPrivateField(sync, "profile")).FirstOrDefault(profile => profile != null && profile.profileName == profileName);
        }

        private static string GetCanvasIdOverride(CanvasUiSync[] syncs, string profileName)
        {
            var sync = syncs.First(candidate =>
            {
                var profile = (CanvasUiSyncProfile)GetPrivateField(candidate, "profile");
                return profile != null && profile.profileName == profileName;
            });

            return (string)GetPrivateField(sync, "canvasIdOverride");
        }

        private static string GetBindingSyncId(object binding)
        {
            return (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
        }

        private static void AssertButtonTargetsLocalToggle(string canvasName)
        {
            var button = FindControl<Button>(canvasName, "SyncButton");
            var targetToggle = FindControl<Toggle>(canvasName, "TargetToggle");
            Assert.That(button, Is.Not.Null);
            Assert.That(targetToggle, Is.Not.Null);
            Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(targetToggle));
            Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("SetIsOnWithoutNotify"));
        }

        private static void AssertPanelAnchors(string canvasName, float expectedMinX, float expectedMaxX)
        {
            var panel = Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(component => component.name == "SyncPanel" && component.GetComponentsInParent<Canvas>(true).Any(canvas => canvas.name == canvasName));
            Assert.That(panel, Is.Not.Null, $"panel on {canvasName} was not found");
            Assert.That(panel.anchorMin.x, Is.EqualTo(expectedMinX).Within(0.0001f));
            Assert.That(panel.anchorMax.x, Is.EqualTo(expectedMaxX).Within(0.0001f));
        }

        private static object CreateStamp(CanvasUiSync sync, long timestampTicks, string nodeId, int sequence)
        {
            var stampType = sync.GetType().GetNestedType("StateStamp", BindingFlags.NonPublic);
            Assert.That(stampType, Is.Not.Null);
            return global::System.Activator.CreateInstance(stampType, timestampTicks, nodeId, sequence);
        }

        private static object InvokePrivate(object instance, string methodName, params object[] arguments)
        {
            var methods = instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Where(method => method.Name == methodName).ToArray();
            Assert.That(methods.Length, Is.GreaterThan(0), $"method {methodName} was not found");

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]))
                {
                    return method.Invoke(instance, new object[] { arguments });
                }

                if (parameters.Length != arguments.Length)
                {
                    continue;
                }

                var matched = true;
                for (var index = 0; index < parameters.Length; index++)
                {
                    if (arguments[index] == null)
                    {
                        continue;
                    }

                    if (!parameters[index].ParameterType.IsInstanceOfType(arguments[index]) && parameters[index].ParameterType != arguments[index].GetType())
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return method.Invoke(instance, arguments);
                }
            }

            Assert.Fail($"method {methodName} did not match supplied arguments");
            return null;
        }

        private static object GetPrivateField(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"field {fieldName} was not found");
            return field.GetValue(instance);
        }

        private static Component AddBindingId(GameObject target, string bindingIdValue)
        {
            var bindingIdType = typeof(CanvasUiSync).Assembly.GetType("Mizotake.UnityUiSync.CanvasUiSyncBindingId");
            if (bindingIdType == null)
            {
                return null;
            }

            var bindingId = target.AddComponent(bindingIdType);
            var property = bindingIdType.GetProperty("BindingId", BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                property.SetValue(bindingId, bindingIdValue);
            }

            return (Component)bindingId;
        }
    }
}
