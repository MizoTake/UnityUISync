using System.Collections;
using System.Collections.Generic;
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
using Unity.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
        public void PublicApi_DynamicBindingCanBeRefreshedReadAndUpdated()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var toggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);

            var bindingsEvents = 0;
            var stateEvents = 0;
            CanvasUiSyncStateEvent lastStateEvent = default;
            var nestedRefreshResult = CanvasUiSyncApiResult.Succeeded;
            var nestedProfileResult = CanvasUiSyncApiResult.Succeeded;
            var nestedCanvasIdResult = CanvasUiSyncApiResult.Succeeded;
            sync.BindingsRefreshed += _ =>
            {
                bindingsEvents++;
                nestedRefreshResult = sync.RefreshBindingsNow();
                nestedProfileResult = sync.ApplyProfile(sync.Profile);
                nestedCanvasIdResult = sync.SetCanvasId("NestedCanvasId");
            };
            sync.StateApplied += value =>
            {
                stateEvents++;
                lastStateEvent = value;
            };

            Assert.That(sync.NotifyHierarchyChanged(), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(sync.RefreshBindingsNow(), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            var bindingInfos = new List<CanvasUiSyncBindingInfo>();
            Assert.That(sync.CopyBindings(bindingInfos), Is.EqualTo(1));
            Assert.That(bindingsEvents, Is.EqualTo(1));
            Assert.That(nestedRefreshResult, Is.EqualTo(CanvasUiSyncApiResult.Busy));
            Assert.That(nestedProfileResult, Is.EqualTo(CanvasUiSyncApiResult.Busy));
            Assert.That(nestedCanvasIdResult, Is.EqualTo(CanvasUiSyncApiResult.Busy));
            Assert.That(sync.TrySetValue(bindingInfos[0].SyncId, true), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(toggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That(sync.TryGetSynchronizedValue(bindingInfos[0].SyncId, out var synchronizedValue), Is.True);
            Assert.That(synchronizedValue, Is.EqualTo(true));
            Assert.That(stateEvents, Is.EqualTo(1));
            Assert.That(lastStateEvent.Origin, Is.EqualTo(CanvasUiSyncValueOrigin.Local));
            Assert.That(lastStateEvent.SyncId, Is.EqualTo(bindingInfos[0].SyncId));
            Assert.That(sync.TrySetValue(toggleObject.GetComponent<Toggle>(), false), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(toggleObject.GetComponent<Toggle>().isOn, Is.False);
            Assert.That(stateEvents, Is.EqualTo(2));
            Assert.That(sync.TrySetValue("missing", true), Is.EqualTo(CanvasUiSyncApiResult.BindingNotFound));
        }

        [Test]
        public void PublicApi_ButtonInvocationAndSubscriberFailureAreIsolated()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var buttonObject = new GameObject("RuntimeButton", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var invokedCount = 0;
            var notificationCount = 0;
            buttonObject.GetComponent<Button>().onClick.AddListener(() => invokedCount++);
            sync.ButtonInvoked += _ => throw new global::System.InvalidOperationException("expected subscriber failure");
            sync.ButtonInvoked += value =>
            {
                Assert.That(value.Origin, Is.EqualTo(CanvasUiSyncValueOrigin.Local));
                notificationCount++;
            };
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: expected subscriber failure");

            Assert.That(sync.TryInvokeButton(buttonObject.GetComponent<Button>()), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(invokedCount, Is.EqualTo(1));
            Assert.That(notificationCount, Is.EqualTo(1));
        }

        [Test]
        public void PublicApi_PeerAndSynchronizationNotificationsDoNotDuplicateHeartbeats()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var peerChanges = new List<CanvasUiSyncPeerEvent>();
            var syncChanges = 0;
            sync.PeerStatusChanged += value => peerChanges.Add(value);
            sync.SynchronizationStateChanged += _ => syncChanges++;

            InvokePrivate(sync, "HandleHello", "PeerB", 1, "OperationCanvas", "SessionB", 100L, 1f, string.Empty, string.Empty, 0);
            InvokePrivate(sync, "HandleHello", "PeerB", 1, "OperationCanvas", "SessionB", 100L, 2f, string.Empty, string.Empty, 0);
            Assert.That(peerChanges.Count, Is.EqualTo(1));
            Assert.That(peerChanges[0].Kind, Is.EqualTo(CanvasUiSyncPeerChangeKind.Joined));
            Assert.That(sync.ConnectedPeerCount, Is.EqualTo(1));

            sync.DisableSync();
            sync.DisableSync();
            sync.EnableSync();
            Assert.That(syncChanges, Is.EqualTo(2));
            Assert.That(sync.GetStatus().SyncEnabled, Is.True);
        }

        [Test]
        public void PublicApi_ApplyProfileStartsNewSessionAndCommunicationReportsMissingPeer()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var firstProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            firstProfile.nodeId = "PeerA";
            firstProfile.listenPort = 29100;
            firstProfile.allowedPeers.Add("PeerB");
            AssignProfile(sync, firstProfile);
            InvokePrivate(sync, "Awake");
            var previousSessionId = sync.SessionId;
            InvokePrivate(sync, "HandleHello", "PeerB", 1, "OperationCanvas", "SessionB", 100L, 1f, string.Empty, string.Empty, 0);
            var peerResetCount = 0;
            sync.PeerStatusChanged += value => peerResetCount += value.Kind == CanvasUiSyncPeerChangeKind.ConfigurationReset ? 1 : 0;
            var secondProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            secondProfile.nodeId = "PeerC";
            secondProfile.listenPort = 29101;

            Assert.That(sync.SendSnapshotToPeer("missing"), Is.EqualTo(CanvasUiSyncApiResult.PeerNotFound));
            Assert.That(sync.ApplyProfile(secondProfile), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(sync.Profile, Is.SameAs(secondProfile));
            Assert.That(sync.NodeId, Is.EqualTo("PeerC"));
            Assert.That(sync.SessionId, Is.Not.EqualTo(previousSessionId));
            Assert.That(peerResetCount, Is.EqualTo(1));
            Assert.That(sync.SendHelloNow(), Is.EqualTo(CanvasUiSyncApiResult.Busy));
            Assert.That(sync.RequestSnapshotNow(), Is.EqualTo(CanvasUiSyncApiResult.Busy));
            Assert.That(sync.SendSnapshotToPeer("missing"), Is.EqualTo(CanvasUiSyncApiResult.Busy));
        }

        [Test]
        public void PublicApi_ApplyProfileRecoversProgrammaticallyAddedComponent()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "RuntimePeer";
            profile.listenPort = 29102;

            Assert.That(sync.IsInitialized, Is.False);
            Assert.That(sync.ApplyProfile(profile), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(sync.IsInitialized, Is.True);
            Assert.That(sync.enabled, Is.True);
            Assert.That(sync.NodeId, Is.EqualTo("RuntimePeer"));
        }

        [Test]
        public void PublicApi_SnapshotNotificationsReportStartedAndCompleted()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var snapshotEvents = new List<CanvasUiSyncSnapshotEvent>();
            sync.SnapshotStatusChanged += value => snapshotEvents.Add(value);

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-api", "OperationCanvas", "PeerB", "SessionB", 1L, 0, 100f, 1, string.Empty, 0, string.Empty);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-api", "OperationCanvas", "PeerB", "SessionB", 1L);

            Assert.That(snapshotEvents.Select(value => value.Status), Is.EqualTo(new[] { CanvasUiSyncSnapshotStatus.Started, CanvasUiSyncSnapshotStatus.Completed }));
            Assert.That(snapshotEvents.All(value => value.SnapshotId == "snapshot-api"), Is.True);
        }

        [Test]
        public void PublicApi_SnapshotReplacementAndDisableReportCancelledOnce()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var snapshotEvents = new List<CanvasUiSyncSnapshotEvent>();
            sync.SnapshotStatusChanged += value => snapshotEvents.Add(value);

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-a", "OperationCanvas", "PeerB", "SessionB", 1L, 1, 100f, 1, string.Empty, 0, string.Empty);
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-b", "OperationCanvas", "PeerB", "SessionB", 1L, 1, 100f, 2, string.Empty, 0, string.Empty);
            sync.DisableSync();

            Assert.That(snapshotEvents.Count(value => value.SnapshotId == "snapshot-a" && value.Status == CanvasUiSyncSnapshotStatus.Started), Is.EqualTo(1));
            Assert.That(snapshotEvents.Count(value => value.SnapshotId == "snapshot-a" && value.Status == CanvasUiSyncSnapshotStatus.Cancelled), Is.EqualTo(1));
            Assert.That(snapshotEvents.Count(value => value.SnapshotId == "snapshot-b" && value.Status == CanvasUiSyncSnapshotStatus.Started), Is.EqualTo(1));
            Assert.That(snapshotEvents.Count(value => value.SnapshotId == "snapshot-b" && value.Status == CanvasUiSyncSnapshotStatus.Cancelled), Is.EqualTo(1));
        }

        [Test]
        public void PublicApi_TrySetValueCommitsObservedClampedValues()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sliderObject = new GameObject("RuntimeSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var dropdownObject = new GameObject("RuntimeDropdown", typeof(RectTransform), typeof(Dropdown));
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Add(new Dropdown.OptionData("A"));
            dropdown.options.Add(new Dropdown.OptionData("B"));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var bindingInfos = new List<CanvasUiSyncBindingInfo>();
            sync.CopyBindings(bindingInfos);
            var sliderSyncId = bindingInfos.Single(value => value.Component == slider).SyncId;
            var dropdownSyncId = bindingInfos.Single(value => value.Component == dropdown && value.ValueType == "Dropdown").SyncId;
            var stateEvents = new List<CanvasUiSyncStateEvent>();
            sync.StateApplied += value => stateEvents.Add(value);

            Assert.That(sync.TrySetValue(sliderSyncId, 100f), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(sync.TrySetValue(dropdownSyncId, 100), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));

            Assert.That(slider.value, Is.EqualTo(1f));
            Assert.That(dropdown.value, Is.EqualTo(1));
            Assert.That(sync.TryGetSynchronizedValue(sliderSyncId, out var sliderState), Is.True);
            Assert.That(sync.TryGetSynchronizedValue(dropdownSyncId, out var dropdownState), Is.True);
            Assert.That(sliderState, Is.EqualTo(1f));
            Assert.That(dropdownState, Is.EqualTo(1));
            Assert.That(stateEvents.Single(value => value.SyncId == sliderSyncId).Value, Is.EqualTo(1f));
            Assert.That(stateEvents.Single(value => value.SyncId == dropdownSyncId).Value, Is.EqualTo(1));
        }

        [Test]
        public void PublicApi_SyncChangeRequestedDuringBindingNotificationIsDeferred()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            sync.BindingsRefreshed += _ => sync.DisableSync();

            Assert.That(sync.RefreshBindingsNow(), Is.EqualTo(CanvasUiSyncApiResult.Succeeded));
            Assert.That(sync.SyncEnabled, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasDeferredSyncEnabled"), Is.True);

            InvokePrivate(sync, "Update");

            Assert.That(sync.SyncEnabled, Is.False);
            Assert.That((bool)GetPrivateField(sync, "hasDeferredSyncEnabled"), Is.False);
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
        public void Lifecycle_DestroyingCanvasUiSyncRemovesOwnedTransportHostButPreservesCanvas()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.listenPort = 0;
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            Assert.That(canvasObject.transform.Find("__CanvasUiSyncTransport"), Is.Not.Null);

            InvokePrivate(sync, "OnDestroy");

            Assert.That(canvasObject, Is.Not.Null);
            Assert.That(canvasObject.transform.Find("__CanvasUiSyncTransport"), Is.Null);
        }

        [Test]
        public void Lifecycle_DestroyingCanvasUiSyncPreservesBorrowedAttachedTransport()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            canvasObject.SetActive(false);
            var server = canvasObject.AddComponent<uOscServer>();
            var client = canvasObject.AddComponent<uOscClient>();
            server.autoStart = false;
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.listenPort = 0;
            AssignProfile(sync, profile);
            canvasObject.SetActive(true);

            Assert.That(GetPrivateField(sync, "transportHost"), Is.Null);
            Assert.That((bool)GetPrivateField(sync, "ownsServer"), Is.False);
            Assert.That((bool)GetPrivateField(sync, "ownsClient"), Is.False);

            InvokePrivate(sync, "OnDestroy");

            Assert.That(canvasObject.GetComponent<uOscServer>(), Is.SameAs(server));
            Assert.That(canvasObject.GetComponent<uOscClient>(), Is.SameAs(client));
        }

        [Test]
        public void Lifecycle_ReinitializingWithAttachedTransportReleasesPreviouslyOwnedHost()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.listenPort = 0;
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var ownedHost = (GameObject)GetPrivateField(sync, "transportHost");
            var attachedServer = canvasObject.AddComponent<uOscServer>();
            var attachedClient = canvasObject.AddComponent<uOscClient>();
            attachedServer.autoStart = false;

            InvokePrivate(sync, "InitializeTransport");

            Assert.That(ownedHost == null, Is.True);
            Assert.That(GetPrivateField(sync, "transportHost"), Is.Null);
            Assert.That(GetPrivateField(sync, "server"), Is.SameAs(attachedServer));
            Assert.That(GetPrivateField(sync, "client"), Is.SameAs(attachedClient));
            Assert.That((bool)GetPrivateField(sync, "ownsServer"), Is.False);
            Assert.That((bool)GetPrivateField(sync, "ownsClient"), Is.False);
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
        public void HandleCommitState_RemoteToggleValue_InvokesReceiverOnValueChanged()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var invocationCount = 0;
            var receivedValue = false;
            toggle.onValueChanged.AddListener(value =>
            {
                invocationCount++;
                receivedValue = value;
            });

            var syncId = (string)InvokePrivate(sync, "BuildSyncId", toggleObject.transform, "Toggle");
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", true, 100L, "PeerB", 1);

            Assert.That(toggle.isOn, Is.True);
            Assert.That(invocationCount, Is.EqualTo(1));
            Assert.That(receivedValue, Is.True);
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
        public void ScanBindings_ExcludedRectTransform_ExcludesUiOnSameGameObject()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<RectTransform>());

            InvokePrivate(sync, "Awake");

            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/ExcludedToggle:Toggle"), Is.False);
        }

        [Test]
        public void ExcludedToggle_DoesNotExcludeButtonSyncIdAtSameLocator()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sharedUiObject = new GameObject("SharedUi", typeof(RectTransform), typeof(Toggle));
            sharedUiObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            AssignExcludedComponents(sync, sharedUiObject.GetComponent<Toggle>());

            InvokePrivate(sync, "Awake");

            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/SharedUi:Toggle"), Is.False);
            Assert.That((bool)InvokePrivate(sync, "IsSyncIdExcluded", "OperationCanvas/SharedUi:Button"), Is.False);
        }

        [Test]
        public void Awake_LegacyExcludedComponent_MigratesToStableExclusion()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            AssignLegacyExcludedComponents(sync, excludedToggleObject.GetComponent<Toggle>());

            InvokePrivate(sync, "Awake");

            Assert.That(((IList)GetPrivateField(sync, "excludedComponents")).Count, Is.EqualTo(0));
            Assert.That(((IList)GetPrivateField(sync, "excludedUi")).Count, Is.EqualTo(1));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/ExcludedToggle:Toggle"), Is.False);
        }

        [Test]
        public void LocalStateChange_ExcludedAfterBindingCreation_DoesNotBroadcastBeforeRescan()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var binding = ((IDictionary)GetPrivateField(sync, "bindings"))["OperationCanvas/ExcludedToggle:Toggle"];
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<RectTransform>());

            InvokePrivate(sync, "OnLocalStateChanged", binding, true, false);

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));
        }

        [Test]
        public void RemoteStateChange_ExcludedAfterBindingCreation_DoesNotApplyBeforeRescan()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var excludedToggle = excludedToggleObject.GetComponent<Toggle>();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<RectTransform>());

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/ExcludedToggle:Toggle", "Toggle", true, 100L, "PeerB", 1);

            Assert.That(excludedToggle.isOn, Is.False);
        }

        [Test]
        public void RemoteStateChange_StableExcludedPath_DoesNotQueuePendingCommit()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<RectTransform>());
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/ExcludedToggle:Toggle", "Toggle", true, 100L, "PeerB", 1);

            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Contains("OperationCanvas/ExcludedToggle:Toggle"), Is.False);
        }

        [Test]
        public void ExcludedStateAndButton_DoNotQueueOrApplyAfterExclusionIsRemoved()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var excludedToggle = excludedToggleObject.GetComponent<Toggle>();
            var excludedButtonObject = new GameObject("ExcludedButton", typeof(RectTransform), typeof(Button));
            excludedButtonObject.transform.SetParent(canvasObject.transform, false);
            var excludedButton = excludedButtonObject.GetComponent<Button>();
            var buttonInvocationCount = 0;
            excludedButton.onClick.AddListener(() => buttonInvocationCount++);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<RectTransform>(), excludedButtonObject.GetComponent<RectTransform>());
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/ExcludedToggle:Toggle", "Toggle", true, 100L, "PeerB", 1);
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/ExcludedButton:Button", 100L, "PeerB", 1);

            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits")).Count, Is.EqualTo(0));
            ((IList)GetPrivateField(sync, "excludedUi")).Clear();
            sync.RefreshExclusions();
            Assert.That(excludedToggle.isOn, Is.False);
            Assert.That(buttonInvocationCount, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/ExcludedToggle:Toggle"), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/ExcludedButton:Button"), Is.True);
        }

        [Test]
        public void PendingStateAndButton_AreDiscardedWhenTargetsBecomeExcluded()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/LateToggle:Toggle", "Toggle", true, 100L, "PeerB", 1);
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/LateButton:Button", 100L, "PeerB", 1);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(1));
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits")).Count, Is.EqualTo(1));
            var lateToggleObject = new GameObject("LateToggle", typeof(RectTransform), typeof(Toggle));
            lateToggleObject.transform.SetParent(canvasObject.transform, false);
            var lateToggle = lateToggleObject.GetComponent<Toggle>();
            var lateButtonObject = new GameObject("LateButton", typeof(RectTransform), typeof(Button));
            lateButtonObject.transform.SetParent(canvasObject.transform, false);
            var buttonInvocationCount = 0;
            lateButtonObject.GetComponent<Button>().onClick.AddListener(() => buttonInvocationCount++);
            AssignExcludedComponents(sync, lateToggleObject.GetComponent<RectTransform>(), lateButtonObject.GetComponent<RectTransform>());

            sync.RefreshExclusions();

            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits")).Count, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/LateToggle:Toggle"), Is.False);
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/LateButton:Button"), Is.False);
            ((IList)GetPrivateField(sync, "excludedUi")).Clear();
            sync.RefreshExclusions();
            Assert.That(lateToggle.isOn, Is.False);
            Assert.That(buttonInvocationCount, Is.EqualTo(0));
        }

        [Test]
        public void ButtonCommit_ExcludedAfterBindingCreation_DoesNotSendOrApplyBeforeRescan()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedButtonObject = new GameObject("ExcludedButton", typeof(RectTransform), typeof(Button));
            excludedButtonObject.transform.SetParent(canvasObject.transform, false);
            var excludedButton = excludedButtonObject.GetComponent<Button>();
            var buttonInvocationCount = 0;
            excludedButton.onClick.AddListener(() => buttonInvocationCount++);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var binding = ((IDictionary)GetPrivateField(sync, "bindings"))["OperationCanvas/ExcludedButton:Button"];
            AssignExcludedComponents(sync, excludedButtonObject.GetComponent<RectTransform>());

            InvokePrivate(sync, "OnLocalButtonClicked", binding);
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/ExcludedButton:Button", 100L, "PeerB", 1);

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));
            Assert.That(buttonInvocationCount, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotState_ExcludedPath_CompletesWithoutPendingUi()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<RectTransform>());
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/ExcludedToggle:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "snapshotReceiveStates")).Count, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotState_RegistryMismatchWithLocalExclusion_StillAppliesCommonUi()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var includedToggleObject = new GameObject("IncludedToggle", typeof(RectTransform), typeof(Toggle));
            includedToggleObject.transform.SetParent(canvasObject.transform, false);
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<Toggle>());
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 1f, 1, "different-registry", 0, GetPrivateField(sync, "fullRegistryHash"));
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/IncludedToggle:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That(includedToggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "ignoredPeerSessions")).Count, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotState_RegistryMismatchWithRemoteExclusion_StillAppliesCommonUi()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var includedToggleObject = new GameObject("IncludedToggle", typeof(RectTransform), typeof(Toggle));
            includedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 1f, 1, "different-registry", 1, GetPrivateField(sync, "fullRegistryHash"));
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/IncludedToggle:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That(includedToggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "ignoredPeerSessions")).Count, Is.EqualTo(0));
        }

        [Test]
        public void SnapshotState_EndBeforeExcludedFinalState_AppliesBufferedCommonUi()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var includedToggleObject = new GameObject("IncludedToggle", typeof(RectTransform), typeof(Toggle));
            includedToggleObject.transform.SetParent(canvasObject.transform, false);
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<Toggle>());
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 2, 1f, 1, "different-registry", 1, GetPrivateField(sync, "fullRegistryHash"));
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/IncludedToggle:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/ExcludedToggle:Toggle", "Toggle", true, 0L, "", 0);

            Assert.That(includedToggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
        }

        [Test]
        public void SnapshotState_RegistryMismatchBeyondConfiguredExclusion_IgnoresPeerSession()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var includedToggleObject = new GameObject("IncludedToggle", typeof(RectTransform), typeof(Toggle));
            includedToggleObject.transform.SetParent(canvasObject.transform, false);
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            new GameObject("ExtraLocalToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<Toggle>());
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 1f, 1, "different-registry", 1, "different-full-registry");
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/IncludedToggle:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That(includedToggleObject.GetComponent<Toggle>().isOn, Is.False);
            ((IDictionary)GetPrivateField(sync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;
            InvokePrivate(sync, "TickSnapshotCleanup", Time.unscaledTime);
            Assert.That((bool)InvokePrivate(sync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.True);
        }

        [Test]
        public void ScanBindings_ExcludedHierarchyPath_RemainsExcludedAfterRecreation()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<Toggle>());
            InvokePrivate(sync, "Awake");
            var poolObject = new GameObject("Pool");
            excludedToggleObject.transform.SetParent(poolObject.transform, false);
            InvokePrivate(sync, "RefreshBindingsIfHierarchyChanged", true);
            Object.DestroyImmediate(excludedToggleObject);
            new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);

            InvokePrivate(sync, "RefreshBindingsIfHierarchyChanged", true);

            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/ExcludedToggle:Toggle"), Is.False);
        }

        [Test]
        public void ScanBindings_ExcludedBindingId_RemainsExcludedAfterRecreationAtDifferentPath()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var originalParent = new GameObject("OriginalParent", typeof(RectTransform));
            originalParent.transform.SetParent(canvasObject.transform, false);
            var replacementParent = new GameObject("ReplacementParent", typeof(RectTransform));
            replacementParent.transform.SetParent(canvasObject.transform, false);
            var excludedToggleObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            excludedToggleObject.transform.SetParent(originalParent.transform, false);
            AddBindingId(excludedToggleObject, "StableExcludedToggle");
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            AssignExcludedComponents(sync, excludedToggleObject.GetComponent<Toggle>());
            InvokePrivate(sync, "Awake");
            Object.DestroyImmediate(excludedToggleObject);
            var oldPathReplacementObject = new GameObject("ExcludedToggle", typeof(RectTransform), typeof(Toggle));
            oldPathReplacementObject.transform.SetParent(originalParent.transform, false);
            var replacementToggleObject = new GameObject("ReplacementToggle", typeof(RectTransform), typeof(Toggle));
            replacementToggleObject.transform.SetParent(replacementParent.transform, false);
            AddBindingId(replacementToggleObject, "StableExcludedToggle");

            InvokePrivate(sync, "RefreshBindingsIfHierarchyChanged", true);

            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/StableExcludedToggle:Toggle"), Is.False);
            Assert.That(((IDictionary)GetPrivateField(sync, "bindings")).Contains("OperationCanvas/OriginalParent/ExcludedToggle:Toggle"), Is.True);
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
        public void ScanBindings_DropdownPolledBinding_UsesIntReader()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "ModeDropdown";
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "ScanBindings");

            var binding = ((IEnumerable)GetPrivateField(sync, "polledBindings")).Cast<object>().Single();
            var tryReadIntValue = binding.GetType().GetMethod("TryReadIntValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var arguments = new object[] { 0 };

            Assert.That(tryReadIntValue, Is.Not.Null);
            Assert.That((bool)tryReadIntValue.Invoke(binding, arguments), Is.True);
            Assert.That((int)arguments[0], Is.EqualTo(0));

            dropdown.SetValueWithoutNotify(1);
            Assert.That((bool)tryReadIntValue.Invoke(binding, arguments), Is.True);
            Assert.That((int)arguments[0], Is.EqualTo(1));
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
        public void HotPathProfiling_LargeHierarchyReportsSteadyStateAllocationAndMarkerBaselines()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            for (var index = 0; index < 48; index++)
            {
                new GameObject("Toggle" + index, typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
                var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
                sliderObject.name = "Slider" + index;
                sliderObject.transform.SetParent(canvasObject.transform, false);
                var scrollbarObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
                scrollbarObject.name = "Scrollbar" + index;
                scrollbarObject.transform.SetParent(canvasObject.transform, false);
            }

            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var initialSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            for (var index = 0; index < 5; index++)
            {
                InvokePrivate(sync, "ComputeBindingHierarchySignature");
            }

            var signatureMarkerRecorder = StartMarkerRecorder("CanvasUiSync.ComputeBindingHierarchySignature");
            var signatureStopwatch = Stopwatch.StartNew();
            var signatureAllocatedBefore = global::System.GC.GetAllocatedBytesForCurrentThread();
            using var signatureRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "CanvasUiSync.ComputeBindingHierarchySignature");
            var signature = initialSignature;
            for (var index = 0; index < 100; index++)
            {
                signature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            }

            var signatureAllocatedBytes = global::System.GC.GetAllocatedBytesForCurrentThread() - signatureAllocatedBefore;
            signatureStopwatch.Stop();
            signatureMarkerRecorder.enabled = false;
            var bindings = (IDictionary)GetPrivateField(sync, "bindings");
            var sliderBinding = bindings.Values.Cast<object>().First(binding => string.Equals((string)binding.GetType().GetProperty("ValueType").GetValue(binding), "Slider", System.StringComparison.Ordinal));
            var broadcastMarkerRecorder = StartMarkerRecorder("CanvasUiSync.BroadcastCommit");
            var broadcastAllocatedBefore = global::System.GC.GetAllocatedBytesForCurrentThread();
            using var broadcastRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "CanvasUiSync.BroadcastCommit");
            for (var index = 0; index < 10; index++)
            {
                InvokePrivate(sync, "BroadcastCommit", GetBindingSyncId(sliderBinding), "Slider", 0.5f, CreateStamp(sync, 100L + index, "PeerA", index + 1));
            }

            var broadcastAllocatedBytes = global::System.GC.GetAllocatedBytesForCurrentThread() - broadcastAllocatedBefore;
            broadcastMarkerRecorder.enabled = false;
            TestContext.WriteLine("CanvasUiSync hot path baseline: bindings=" + bindings.Count + " signature100CallsMs=" + signatureStopwatch.Elapsed.TotalMilliseconds.ToString("F3") + " signatureAllocBytes=" + signatureAllocatedBytes + " signatureProfilerRecorderSamples=" + (signatureRecorder.Valid ? signatureRecorder.Count : -1) + " signatureMarkerSamples=" + signatureMarkerRecorder.sampleBlockCount + " broadcast10AllocBytes=" + broadcastAllocatedBytes + " broadcastProfilerRecorderSamples=" + (broadcastRecorder.Valid ? broadcastRecorder.Count : -1) + " broadcastMarkerSamples=" + broadcastMarkerRecorder.sampleBlockCount + " sentMessages=" + (int)GetPrivateField(sync, "sentMessageCount"));

            Assert.That(signature, Is.EqualTo(initialSignature));
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(10));
            Assert.That(signatureRecorder.Valid, Is.True);
            Assert.That(broadcastRecorder.Valid, Is.True);
            Assert.That(signatureMarkerRecorder.sampleBlockCount, Is.GreaterThan(0));
            Assert.That(broadcastMarkerRecorder.sampleBlockCount, Is.GreaterThan(0));
            Assert.That(signatureAllocatedBytes, Is.LessThan(2_000_000L));
            Assert.That(broadcastAllocatedBytes, Is.LessThan(1_000_000L));
        }

        [Test]
        public void BindingDiscovery_CollectsSupportedHierarchyOncePerOperation()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");

            var signatureCollectorRecorder = StartMarkerRecorder("CanvasUiSync.CollectSupportedComponents");
            InvokePrivate(sync, "ComputeBindingHierarchySignature");
            signatureCollectorRecorder.enabled = false;
            Assert.That(signatureCollectorRecorder.sampleBlockCount, Is.EqualTo(1));

            var scanCollectorRecorder = StartMarkerRecorder("CanvasUiSync.CollectSupportedComponents");
            InvokePrivate(sync, "ScanBindings");
            scanCollectorRecorder.enabled = false;
            Assert.That(scanCollectorRecorder.sampleBlockCount, Is.EqualTo(1));
        }

        [Test]
        public void HotPathProfiling_SendSnapshotAndSendToReportMarkerBaselines()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var targetObject = new GameObject("TargetCanvas", typeof(Canvas));
            var targetSync = targetObject.AddComponent<CanvasUiSync>();
            var targetProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            targetProfile.nodeId = "PeerB";
            targetProfile.allowedPeers.Add("PeerA");
            AssignProfile(targetSync, targetProfile);
            AssignCanvasIdOverride(targetSync, "OperationCanvas");
            InvokePrivate(targetSync, "Awake");

            var scanMarkerRecorder = StartMarkerRecorder("CanvasUiSync.ScanBindings");
            using var scanRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "CanvasUiSync.ScanBindings");
            for (var index = 0; index < 5; index++)
            {
                InvokePrivate(sync, "ScanBindings");
            }

            scanMarkerRecorder.enabled = false;
            var snapshotMarkerRecorder = StartMarkerRecorder("CanvasUiSync.SendSnapshot");
            using var snapshotRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "CanvasUiSync.SendSnapshot");
            InvokePrivate(sync, "SendSnapshot", targetSync);
            snapshotMarkerRecorder.enabled = false;
            var sendToMarkerRecorder = StartMarkerRecorder("CanvasUiSync.SendTo");
            using var sendToRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "CanvasUiSync.SendTo");
            InvokePrivate(sync, "SendTo", "127.0.0.1", 9001, "/uisync/test", new object[] { "value" });
            sendToMarkerRecorder.enabled = false;

            TestContext.WriteLine("CanvasUiSync marker baseline: scanProfilerRecorderSamples=" + (scanRecorder.Valid ? scanRecorder.Count : -1) + " scanMarkerSamples=" + scanMarkerRecorder.sampleBlockCount + " snapshotProfilerRecorderSamples=" + (snapshotRecorder.Valid ? snapshotRecorder.Count : -1) + " snapshotMarkerSamples=" + snapshotMarkerRecorder.sampleBlockCount + " sendToProfilerRecorderSamples=" + (sendToRecorder.Valid ? sendToRecorder.Count : -1) + " sendToMarkerSamples=" + sendToMarkerRecorder.sampleBlockCount + " sentMessages=" + (int)GetPrivateField(sync, "sentMessageCount"));
            Assert.That(scanRecorder.Valid, Is.True);
            Assert.That(snapshotRecorder.Valid, Is.True);
            Assert.That(sendToRecorder.Valid, Is.True);
            Assert.That(scanMarkerRecorder.sampleBlockCount, Is.GreaterThan(0));
            Assert.That(snapshotMarkerRecorder.sampleBlockCount, Is.GreaterThan(0));
            Assert.That(sendToMarkerRecorder.sampleBlockCount, Is.GreaterThan(0));
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));
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
        public void ComputeBindingHierarchySignature_ReusesBindingScanContext()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("ModeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");

            var context = GetPrivateField(sync, "bindingScanContext");
            var initialSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");
            var repeatedSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");

            Assert.That(GetPrivateField(sync, "bindingScanContext"), Is.SameAs(context));
            Assert.That(repeatedSignature, Is.EqualTo(initialSignature));
        }

        [Test]
        public void ComputeBindingHierarchySignature_RecreatedComponentAtSamePath_ChangesAndRefreshesBinding()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var originalToggleObject = new GameObject("ModeToggle", typeof(RectTransform), typeof(Toggle));
            originalToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            const string syncId = "OperationCanvas/ModeToggle:Toggle";
            var initialSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");

            Object.DestroyImmediate(originalToggleObject);
            var replacementToggleObject = new GameObject("ModeToggle", typeof(RectTransform), typeof(Toggle));
            replacementToggleObject.transform.SetParent(canvasObject.transform, false);
            var replacementToggle = replacementToggleObject.GetComponent<Toggle>();
            var replacementSignature = (int)InvokePrivate(sync, "ComputeBindingHierarchySignature");

            Assert.That(replacementSignature, Is.Not.EqualTo(initialSignature));
            Assert.That((bool)InvokePrivate(sync, "RefreshBindingsIfHierarchyChanged", false), Is.True);
            var refreshedBinding = ((IDictionary)GetPrivateField(sync, "bindings"))[syncId];
            Assert.That(refreshedBinding.GetType().GetProperty("Component").GetValue(refreshedBinding), Is.SameAs(replacementToggle));
        }

        [Test]
        public void ComputeBindingHierarchySignature_ManyHierarchyPathExclusionsReusePathsWithinOperation()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var parentObject = new GameObject("NestedParent", typeof(RectTransform));
            parentObject.transform.SetParent(canvasObject.transform, false);
            for (var index = 0; index < 48; index++)
            {
                new GameObject("Toggle" + index, typeof(RectTransform), typeof(Toggle)).transform.SetParent(parentObject.transform, false);
            }

            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var exclusions = (IList)GetPrivateField(sync, "excludedUi");
            var exclusionType = typeof(CanvasUiSync).Assembly.GetType("Mizotake.UnityUiSync.CanvasUiSyncExclusion");
            Assert.That(exclusionType, Is.Not.Null);
            var firstExclusion = global::System.Activator.CreateInstance(exclusionType);
            exclusionType.GetField("hierarchyPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(firstExclusion, "Missing0");
            exclusionType.GetField("componentType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(firstExclusion, "Toggle");
            exclusions.Add(firstExclusion);
            InvokePrivate(sync, "ComputeBindingHierarchySignature");
            var singleExclusionStopwatch = Stopwatch.StartNew();
            for (var index = 0; index < 20; index++)
            {
                InvokePrivate(sync, "ComputeBindingHierarchySignature");
            }
            singleExclusionStopwatch.Stop();
            for (var index = 1; index < 32; index++)
            {
                var exclusion = global::System.Activator.CreateInstance(exclusionType);
                exclusionType.GetField("hierarchyPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(exclusion, "Missing" + index);
                exclusionType.GetField("componentType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(exclusion, "Toggle");
                exclusions.Add(exclusion);
            }

            InvokePrivate(sync, "ComputeBindingHierarchySignature");
            var manyExclusionsStopwatch = Stopwatch.StartNew();
            for (var index = 0; index < 20; index++)
            {
                InvokePrivate(sync, "ComputeBindingHierarchySignature");
            }
            manyExclusionsStopwatch.Stop();
            Assert.That(manyExclusionsStopwatch.ElapsedTicks, Is.LessThanOrEqualTo(singleExclusionStopwatch.ElapsedTicks * 8L), "singleTicks=" + singleExclusionStopwatch.ElapsedTicks + " manyTicks=" + manyExclusionsStopwatch.ElapsedTicks);
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
        public void OnTransformChildrenChanged_DirectRuntimeHierarchyChangeSchedulesImmediateRescan()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            SetPrivateField(sync, "nextHierarchyRescanTime", Time.unscaledTime + 10f);

            InvokePrivate(sync, "OnTransformChildrenChanged");

            Assert.That((float)GetPrivateField(sync, "nextHierarchyRescanTime"), Is.LessThanOrEqualTo(Time.unscaledTime));
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
        public void HandleHello_UsesSessionUptimeWhenWallClocksDisagree()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            SetPrivateField(sync, "sessionStartedAtRealtime", Time.realtimeSinceStartup - 10f);
            SetPrivateField(sync, "sessionStartedAtTicks", 200L);
            SetPrivateField(sync, "sentMessageCount", 0);

            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 1f);

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(3));
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
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
        public void HandleBeginSnapshot_UnrequestedNewerSource_IsNotAcceptedByAlreadyRunningPeer()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            SetPrivateField(sync, "sessionStartedAtRealtime", Time.realtimeSinceStartup - 10f);
            SetPrivateField(sync, "sessionStartedAtTicks", 100L);

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 200L, 0, 1f);

            Assert.That(((IDictionary)GetPrivateField(sync, "activeSnapshotIds")).Count, Is.EqualTo(0));
        }

        [Test]
        public void HandleBeginSnapshot_RequestedNewerSource_CanRestoreDefaultStampedRemoteState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            toggleObject.GetComponent<Toggle>().SetIsOnWithoutNotify(true);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            SetPrivateField(sync, "sessionStartedAtRealtime", Time.realtimeSinceStartup - 10f);
            SetPrivateField(sync, "sessionStartedAtTicks", 100L);
            InvokePrivate(sync, "AllowRequestedSnapshotFromNewerPeer");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 200L, 1, 1f);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/PowerToggle:Toggle", "Toggle", false, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 200L);

            Assert.That(toggleObject.GetComponent<Toggle>().isOn, Is.False);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
        }

        [Test]
        public void RequestedNewerSnapshotAcceptance_ExpiresAfterRetryWindow()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "AllowRequestedSnapshotFromNewerPeer");
            SetPrivateField(sync, "acceptRequestedSnapshotFromNewerPeerUntil", Time.unscaledTime - 1f);

            Assert.That((bool)InvokePrivate(sync, "CanAcceptRequestedSnapshotFromNewerPeer"), Is.False);
            Assert.That((bool)GetPrivateField(sync, "acceptRequestedSnapshotFromNewerPeer"), Is.False);
        }

        [Test]
        public void HandleSnapshotState_UnknownSyncId_AppliesInitialSnapshotAfterRuntimeGeneratedBinding()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);

            var toggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);

            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");

            Assert.That(toggle.isOn, Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
        }

        [Test]
        public void HandleEndSnapshot_UnknownSyncId_WaitsForRuntimeGeneratedBindingBeforeCompleting()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);

            var toggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);

            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");

            Assert.That(toggle.isOn, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
        }

        [Test]
        public void HandleEndSnapshot_UnknownSyncId_BlocksLocalCommitUntilSnapshotCompletes()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var localToggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            localToggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string pendingSyncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", pendingSyncId, "Toggle", true, 0L, "", 0);
            SetPrivateField(sync, "sentMessageCount", 0);
            var localBinding = ((IDictionary)GetPrivateField(sync, "bindings"))["OperationCanvas/PowerToggle:Toggle"];

            InvokePrivate(sync, "OnLocalStateChanged", localBinding, true, false);

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            var runtimeToggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            runtimeToggleObject.transform.SetParent(canvasObject.transform, false);
            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");
            InvokePrivate(sync, "OnLocalStateChanged", localBinding, true, false);

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));
        }

        [Test]
        public void HandleEndSnapshot_BeforeAnnouncedState_WaitsForLateSnapshotState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/PowerToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);

            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);

            Assert.That(toggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
        }

        [Test]
        public void HandleSnapshotState_EndArrivedWithTwoDynamicBindings_WaitsForBothBindings()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string firstSyncId = "OperationCanvas/FirstRuntimeToggle:Toggle";
            const string secondSyncId = "OperationCanvas/SecondRuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 2);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", firstSyncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            var firstToggleObject = new GameObject("FirstRuntimeToggle", typeof(RectTransform), typeof(Toggle));
            firstToggleObject.transform.SetParent(canvasObject.transform, false);

            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", secondSyncId, "Toggle", true, 0L, "", 0);

            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);

            var secondToggleObject = new GameObject("SecondRuntimeToggle", typeof(RectTransform), typeof(Toggle));
            secondToggleObject.transform.SetParent(canvasObject.transform, false);
            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");

            Assert.That(firstToggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That(secondToggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
        }

        [Test]
        public void HandleSnapshotState_MultipleUnknownSyncIds_DoesNotForceFullBindingScanPerState()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var scanMarkerRecorder = StartMarkerRecorder("CanvasUiSync.ScanBindings");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 3);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/RuntimeToggle0:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/RuntimeToggle1:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/RuntimeToggle2:Toggle", "Toggle", true, 0L, "", 0);
            scanMarkerRecorder.enabled = false;

            Assert.That(scanMarkerRecorder.sampleBlockCount, Is.EqualTo(0));
        }

        [Test]
        public void HandleBeginSnapshot_NewFullSnapshot_SupersedesPreviousPendingSnapshot()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-2", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-2", "OperationCanvas", syncId, "Toggle", false, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-2", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That(((IDictionary)GetPrivateField(sync, "activeSnapshotIds")).Count, Is.EqualTo(1));
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(1));

            var toggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            toggleObject.GetComponent<Toggle>().SetIsOnWithoutNotify(true);
            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");

            Assert.That(toggleObject.GetComponent<Toggle>().isOn, Is.False);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
        }

        [Test]
        public void HandleBeginSnapshot_OlderSequenceArrivingLate_DoesNotReplaceNewerSnapshot()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-2", "OperationCanvas", "PeerB", "SessionB", 100L, 0, 10f, 2);
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 0, 10f, 1);

            var activeSnapshotIds = (IDictionary)GetPrivateField(sync, "activeSnapshotIds");
            Assert.That(activeSnapshotIds.Contains("snapshot-2"), Is.True);
            Assert.That(activeSnapshotIds.Contains("snapshot-1"), Is.False);
        }

        [Test]
        public void SendSnapshotCore_AnnouncesSnapshotStateCount()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var client = (uOSC.uOscClient)GetPrivateField(sync, "client");
            client.maxQueueSize = 1;
            object[] beginValues = null;

            InvokePrivate(sync, "SendSnapshotCore", new global::System.Action<object[]>(values => beginValues = values), new global::System.Action<object[]>(values => { }), new global::System.Action<object[]>(values => { }));

            Assert.That(beginValues, Is.Not.Null);
            Assert.That(beginValues.Length, Is.GreaterThanOrEqualTo(6));
            Assert.That(global::System.Convert.ToInt32(beginValues[5]), Is.EqualTo(1));
            Assert.That(client.maxQueueSize, Is.GreaterThanOrEqualTo(17));
        }

        [Test]
        public void SendSnapshotCore_AnnouncesRegistryHash()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            object[] beginValues = null;

            InvokePrivate(sync, "SendSnapshotCore", new global::System.Action<object[]>(values => beginValues = values), new global::System.Action<object[]>(values => { }), new global::System.Action<object[]>(values => { }));

            Assert.That(beginValues, Is.Not.Null);
            Assert.That(beginValues.Length, Is.GreaterThanOrEqualTo(9));
            Assert.That(beginValues[8], Is.EqualTo(GetPrivateField(sync, "registryHash")));
            Assert.That(beginValues.Length, Is.GreaterThanOrEqualTo(11));
            Assert.That(beginValues[10], Is.EqualTo(GetPrivateField(sync, "fullRegistryHash")));
        }

        [Test]
        public void HandleSnapshotTimeout_CompletePayloadWithDifferentRegistry_IgnoresPeerSessionWithoutPartialApply()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/PowerToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 10f, 1, "different-registry");
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            Assert.That(toggle.isOn, Is.False);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);

            ((IDictionary)GetPrivateField(sync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;
            InvokePrivate(sync, "TickSnapshotCleanup", Time.unscaledTime);

            Assert.That((bool)InvokePrivate(sync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.True);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", true, 1L, "PeerB", 1);
            Assert.That(toggle.isOn, Is.False);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionC", "OperationCanvas", syncId, "Toggle", true, 2L, "PeerB", 2);
            Assert.That(toggle.isOn, Is.False);
        }

        [Test]
        public void HandleSnapshotTimeout_RemovesBufferedCommitOutsideAnnouncedSnapshotIds()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 10f, 1, "different-registry");
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/PowerToggle:Toggle", "Toggle", false, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/LateToggle:Toggle", "Toggle", true, 1L, "PeerB", 1);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(2));
            ((IDictionary)GetPrivateField(sync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;

            InvokePrivate(sync, "TickSnapshotCleanup", Time.unscaledTime);

            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
        }

        [Test]
        public void HandleSnapshotTimeout_RegistryMismatchRemovesButtonBufferedBeforeSnapshot()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 1f, "different-registry", "different-full-registry", 0);
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/LateButton:Button", 1L, "PeerB", 1);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits")).Count, Is.EqualTo(1));
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 0, 10f, 1, "different-registry", 0, "different-full-registry");
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            ((IDictionary)GetPrivateField(sync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;

            InvokePrivate(sync, "TickSnapshotCleanup", Time.unscaledTime);

            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits")).Count, Is.EqualTo(0));
            Assert.That((bool)InvokePrivate(sync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.True);
        }

        [Test]
        public void HandleSnapshotTimeout_MissingAnnouncedState_DoesNotIgnorePeerSession()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var registryHash = (string)GetPrivateField(sync, "registryHash");
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 2, 10f, 1, registryHash);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", "OperationCanvas/PowerToggle:Toggle", "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            ((IDictionary)GetPrivateField(sync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;

            InvokePrivate(sync, "TickSnapshotCleanup", Time.unscaledTime);

            Assert.That((bool)InvokePrivate(sync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.False);
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);
        }

        [Test]
        public void HandleEndSnapshot_DifferentRegistryThatMatchesAfterDynamicGeneration_AppliesAndCompletes()
        {
            var sourceCanvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(sourceCanvasObject.transform, false);
            var sourceSync = sourceCanvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sourceSync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sourceSync, "Awake");
            var sourceRegistryHash = (string)GetPrivateField(sourceSync, "registryHash");

            var targetCanvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var targetSync = targetCanvasObject.AddComponent<CanvasUiSync>();
            var targetProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            targetProfile.allowedPeers.Add("PeerB");
            AssignProfile(targetSync, targetProfile);
            InvokePrivate(targetSync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(targetSync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 10f, 1, sourceRegistryHash);
            InvokePrivate(targetSync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(targetSync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);

            var runtimeToggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            runtimeToggleObject.transform.SetParent(targetCanvasObject.transform, false);
            var runtimeToggle = runtimeToggleObject.GetComponent<Toggle>();
            runtimeToggle.SetIsOnWithoutNotify(false);
            InvokePrivate(targetSync, "ScanBindings");
            InvokePrivate(targetSync, "InitializeLocalState");

            Assert.That(runtimeToggle.isOn, Is.True);
            Assert.That((bool)GetPrivateField(targetSync, "hasSnapshot"), Is.True);
            Assert.That((bool)InvokePrivate(targetSync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.False);
        }

        [Test]
        public void IgnoredPeerSession_StopsSynchronizationTrafficButAcceptsNewSessionHello()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            profile.allowedPeers.Add("PeerB");
            var endpoint = new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true };
            profile.peerEndpoints.Add(endpoint);
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            ((IDictionary)GetPrivateField(sync, "ignoredPeerSessions"))["PeerB"] = "SessionB";
            SetPrivateField(sync, "sentMessageCount", 0);

            Assert.That((bool)InvokePrivate(sync, "IsPeerTargetActive", endpoint), Is.False);
            InvokePrivate(sync, "SendHello");
            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(1));

            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionC", 200L, 1f);

            Assert.That((bool)InvokePrivate(sync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.False);
            Assert.That((bool)InvokePrivate(sync, "IsPeerTargetActive", endpoint), Is.True);
        }

        [Test]
        public void HandleHello_ChangedRegistryHash_ReplacesStaleSnapshotRequest()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(canvasObject.transform, false);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.nodeId = "PeerA";
            profile.allowedPeers.Add("PeerB");
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-old", "OperationCanvas", "PeerB", "SessionB", 100L, 0, 10f, 1, "old-registry");
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-old", "OperationCanvas", "PeerB", "SessionB", 100L);
            Assert.That(((IDictionary)GetPrivateField(sync, "snapshotReceiveStates")).Contains("snapshot-old"), Is.True);

            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 1f, GetPrivateField(sync, "registryHash"));

            Assert.That(((IDictionary)GetPrivateField(sync, "snapshotReceiveStates")).Contains("snapshot-old"), Is.False);
            Assert.That((int)GetPrivateField(sync, "snapshotRetryCount"), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void HandleSnapshotTimeout_DynamicUiCreatedBeforeDeadline_FinalRescanCompletesWithoutIgnoringPeer()
        {
            var sourceCanvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle)).transform.SetParent(sourceCanvasObject.transform, false);
            var sourceSync = sourceCanvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sourceSync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sourceSync, "Awake");
            var sourceRegistryHash = (string)GetPrivateField(sourceSync, "registryHash");

            var targetCanvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var targetSync = targetCanvasObject.AddComponent<CanvasUiSync>();
            var targetProfile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            targetProfile.allowedPeers.Add("PeerB");
            AssignProfile(targetSync, targetProfile);
            InvokePrivate(targetSync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(targetSync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1, 10f, 1, sourceRegistryHash);
            InvokePrivate(targetSync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(targetSync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            var runtimeToggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            runtimeToggleObject.transform.SetParent(targetCanvasObject.transform, false);
            runtimeToggleObject.GetComponent<Toggle>().SetIsOnWithoutNotify(false);
            ((IDictionary)GetPrivateField(targetSync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;

            InvokePrivate(targetSync, "TickSnapshotCleanup", Time.unscaledTime);

            Assert.That(runtimeToggleObject.GetComponent<Toggle>().isOn, Is.True);
            Assert.That((bool)GetPrivateField(targetSync, "hasSnapshot"), Is.True);
            Assert.That((bool)InvokePrivate(targetSync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.False);
        }

        [Test]
        public void HandleCommitState_DifferentFromEstablishedPeerSession_IsIgnored()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 1f, GetPrivateField(sync, "registryHash"));

            InvokePrivate(sync, "HandleCommitState", "PeerB", "UnexpectedSession", "OperationCanvas", "OperationCanvas/PowerToggle:Toggle", "Toggle", true, 1L, "PeerB", 1);
            Assert.That(toggle.isOn, Is.False);

            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/PowerToggle:Toggle", "Toggle", true, 2L, "PeerB", 2);
            Assert.That(toggle.isOn, Is.True);
        }

        [Test]
        public void HandleCommitState_RegistryMismatchKnownFromHelloBeforeSnapshot_IsIgnored()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 1f, "different-registry", "different-full-registry", 0);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/PowerToggle:Toggle", "Toggle", true, 1L, "PeerB", 1);

            Assert.That(toggle.isOn, Is.False);
        }

        [Test]
        public void HandleHello_DelayedOlderSession_DoesNotReplaceNewerSession()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var registryHash = GetPrivateField(sync, "registryHash");
            var fullRegistryHash = GetPrivateField(sync, "fullRegistryHash");

            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 1f, registryHash, fullRegistryHash, 0);
            ((IDictionary)GetPrivateField(sync, "ignoredPeerSessions"))["PeerB"] = "SessionB";
            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionC", 200L, 1f, registryHash, fullRegistryHash, 0);
            InvokePrivate(sync, "HandleHello", "PeerB", profile.protocolVersion, "OperationCanvas", "SessionB", 100L, 2f, registryHash, fullRegistryHash, 0);

            var node = ((IDictionary)GetPrivateField(sync, "nodes"))["PeerB"];
            Assert.That(node.GetType().GetProperty("SessionId").GetValue(node), Is.EqualTo("SessionC"));
            Assert.That((bool)InvokePrivate(sync, "IsPeerSessionIgnored", "PeerB", "SessionB"), Is.False);
        }

        [Test]
        public void HandleSnapshotState_UnknownSyncId_TimesOutAndKeepsSynchronizationIncomplete()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.initialSyncPendingTimeoutSeconds = 0.5f;
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            ((IDictionary)GetPrivateField(sync, "activeSnapshotIds"))["snapshot-1"] = Time.unscaledTime - 1f;
            InvokePrivate(sync, "TickSnapshotCleanup", Time.unscaledTime);

            var toggleObject = new GameObject("RuntimeToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);

            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");

            Assert.That(toggle.isOn, Is.False);
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(0));
            Assert.That((bool)GetPrivateField(sync, "hasSnapshot"), Is.False);
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
        public void HandleCommitState_MultipleUnknownSyncIdsWithoutHierarchyChange_DoesNotRebuildBindingsAndSchedulesPromptRescan()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            var scanMarkerRecorder = StartMarkerRecorder("CanvasUiSync.ScanBindings");
            var firstCollectorMarkerRecorder = StartMarkerRecorder("CanvasUiSync.CollectSupportedComponents");
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/RuntimeToggleA:Toggle", "Toggle", true, 101L, "PeerB", 1);
            firstCollectorMarkerRecorder.enabled = false;
            Assert.That(firstCollectorMarkerRecorder.sampleBlockCount, Is.EqualTo(1));
            Assert.That((float)GetPrivateField(sync, "pendingHierarchyFastRescanUntil"), Is.GreaterThan(Time.unscaledTime));
            var firstNextHierarchyRescanTime = (float)GetPrivateField(sync, "nextHierarchyRescanTime");
            var secondCollectorMarkerRecorder = StartMarkerRecorder("CanvasUiSync.CollectSupportedComponents");
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/RuntimeToggleB:Toggle", "Toggle", true, 102L, "PeerB", 2);
            secondCollectorMarkerRecorder.enabled = false;
            Assert.That(secondCollectorMarkerRecorder.sampleBlockCount, Is.EqualTo(0));
            Assert.That((float)GetPrivateField(sync, "nextHierarchyRescanTime"), Is.LessThanOrEqualTo(firstNextHierarchyRescanTime));
            scanMarkerRecorder.enabled = false;

            Assert.That(scanMarkerRecorder.sampleBlockCount, Is.EqualTo(0));
            Assert.That(((IDictionary)GetPrivateField(sync, "pendingRemoteCommits")).Count, Is.EqualTo(2));
            Assert.That((float)GetPrivateField(sync, "nextHierarchyRescanTime"), Is.LessThanOrEqualTo(Time.unscaledTime + 0.02f));
            var firstFastRescanUntil = (float)GetPrivateField(sync, "pendingHierarchyFastRescanUntil");
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/RuntimeToggleA:Toggle", "Toggle", false, 103L, "PeerB", 3);
            Assert.That((float)GetPrivateField(sync, "pendingHierarchyFastRescanUntil"), Is.EqualTo(firstFastRescanUntil));
            SetPrivateField(sync, "pendingHierarchyFastRescanUntil", Time.unscaledTime + 0.01f);
            SetPrivateField(sync, "nextHierarchyRescanTime", Time.unscaledTime + 0.02f);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", "OperationCanvas/RuntimeToggleC:Toggle", "Toggle", true, 104L, "PeerB", 4);
            Assert.That((float)GetPrivateField(sync, "pendingHierarchyFastRescanUntil"), Is.GreaterThanOrEqualTo(Time.unscaledTime + 0.9f));
            InvokePrivate(sync, "TickRuntimeHierarchyRescan", (float)GetPrivateField(sync, "nextHierarchyRescanTime"));
            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.LessThanOrEqualTo(0.05f));

            var afterFastRescanWindow = Time.unscaledTime + 1.1f;
            SetPrivateField(sync, "nextHierarchyRescanTime", afterFastRescanWindow);
            InvokePrivate(sync, "TickRuntimeHierarchyRescan", afterFastRescanWindow);
            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.GreaterThan(0.05f));
        }

        [Test]
        public void ArmPendingBindingDiscovery_RearmExtendsWindowWithoutResettingFastInterval()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var now = Time.unscaledTime;
            SetPrivateField(sync, "pendingHierarchyFastRescanUntil", now + 0.5f);
            SetPrivateField(sync, "currentHierarchyRescanIntervalSeconds", 0.05f);
            SetPrivateField(sync, "nextHierarchyRescanTime", now + 0.05f);

            InvokePrivate(sync, "ArmPendingBindingDiscovery", now + 0.1f);

            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That((float)GetPrivateField(sync, "pendingHierarchyFastRescanUntil"), Is.EqualTo(now + 1.1f).Within(0.0001f));
        }

        [Test]
        public void ArmPendingBindingDiscovery_ExpiredWindowRestartsFastInterval()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, ScriptableObject.CreateInstance<CanvasUiSyncProfile>());
            InvokePrivate(sync, "Awake");
            var now = Time.unscaledTime;
            SetPrivateField(sync, "pendingHierarchyFastRescanUntil", now);
            SetPrivateField(sync, "currentHierarchyRescanIntervalSeconds", 0.05f);

            InvokePrivate(sync, "ArmPendingBindingDiscovery", now);

            Assert.That((float)GetPrivateField(sync, "currentHierarchyRescanIntervalSeconds"), Is.EqualTo(0.01f).Within(0.0001f));
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
        public void Update_RemovesExpiredRemoteStateAndButtonWithoutHierarchyRefresh()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string stateSyncId = "OperationCanvas/RuntimeToggle:Toggle";
            const string buttonSyncId = "OperationCanvas/RuntimeButton:Button";
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", stateSyncId, "Toggle", true, 101L, "PeerB", 2);
            InvokePrivate(sync, "HandleCommitButton", "PeerB", "SessionB", "OperationCanvas", buttonSyncId, 102L, "PeerB", 3);

            var pendingRemoteCommits = (IDictionary)GetPrivateField(sync, "pendingRemoteCommits");
            var statePending = pendingRemoteCommits[stateSyncId];
            var stateStamp = statePending.GetType().GetProperty("Stamp").GetValue(statePending);
            pendingRemoteCommits[stateSyncId] = global::System.Activator.CreateInstance(statePending.GetType(), "Toggle", true, stateStamp, Time.unscaledTime - 31f, false, false, 30f);
            var pendingRemoteButtonCommits = (IDictionary)GetPrivateField(sync, "pendingRemoteButtonCommits");
            var buttonPending = pendingRemoteButtonCommits[buttonSyncId];
            var buttonStamp = buttonPending.GetType().GetProperty("Stamp").GetValue(buttonPending);
            pendingRemoteButtonCommits[buttonSyncId] = global::System.Activator.CreateInstance(buttonPending.GetType(), buttonStamp, Time.unscaledTime - 31f, "PeerB", "SessionB", false);
            SetPrivateField(sync, "nextPendingRemoteCommitCleanupTime", Time.unscaledTime - 1f);

            InvokePrivate(sync, "Update");

            Assert.That(pendingRemoteCommits.Count, Is.EqualTo(0));
            Assert.That(pendingRemoteButtonCommits.Count, Is.EqualTo(0));
        }

        [Test]
        public void TickPendingRemoteCommitCleanup_KeepsActiveSnapshotStateUntilSnapshotTimeout()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L, 1);
            InvokePrivate(sync, "HandleSnapshotState", "snapshot-1", "OperationCanvas", syncId, "Toggle", true, 0L, "", 0);
            InvokePrivate(sync, "HandleEndSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB", 100L);
            var pendingRemoteCommits = (IDictionary)GetPrivateField(sync, "pendingRemoteCommits");
            var pending = pendingRemoteCommits[syncId];
            var stamp = pending.GetType().GetProperty("Stamp").GetValue(pending);
            pendingRemoteCommits[syncId] = global::System.Activator.CreateInstance(pending.GetType(), "Toggle", true, stamp, Time.unscaledTime - 31f, true, true, 1f);
            SetPrivateField(sync, "nextPendingRemoteCommitCleanupTime", Time.unscaledTime - 1f);

            InvokePrivate(sync, "TickPendingRemoteCommitCleanup", Time.unscaledTime);

            Assert.That(pendingRemoteCommits.Contains(syncId), Is.True);
            Assert.That(((IDictionary)GetPrivateField(sync, "snapshotReceiveStates")).Contains("snapshot-1"), Is.True);
        }

        [Test]
        public void TickPendingRemoteCommitCleanup_NewerPendingStateExtendsDeadline()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            const string syncId = "OperationCanvas/RuntimeToggle:Toggle";
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", false, 100L, "PeerB", 1);
            var pendingRemoteCommits = (IDictionary)GetPrivateField(sync, "pendingRemoteCommits");
            var pending = pendingRemoteCommits[syncId];
            var stamp = pending.GetType().GetProperty("Stamp").GetValue(pending);
            var now = Time.unscaledTime;
            pendingRemoteCommits[syncId] = global::System.Activator.CreateInstance(pending.GetType(), "Toggle", false, stamp, now - 29f, false, false, 30f);
            SetPrivateField(sync, "nextPendingRemoteCommitCleanupTime", now + 1f);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Toggle", true, 101L, "PeerB", 2);

            InvokePrivate(sync, "TickPendingRemoteCommitCleanup", now + 2f);

            Assert.That(pendingRemoteCommits.Contains(syncId), Is.True);
            Assert.That((float)GetPrivateField(sync, "nextPendingRemoteCommitCleanupTime"), Is.GreaterThan(now + 20f));
        }

        [Test]
        public void HandleBeginSnapshot_DelayedOlderSessionDoesNotForgetItsSequenceHistory()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");

            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-1", "OperationCanvas", "PeerB", "SessionB1", 100L, 0, 10f, 1);
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-2", "OperationCanvas", "PeerB", "SessionB2", 200L, 0, 20f, 1);
            InvokePrivate(sync, "HandleBeginSnapshot", "snapshot-delayed", "OperationCanvas", "PeerB", "SessionB1", 100L, 0, 10f, 1);

            var latestSnapshotSequences = (IDictionary)GetPrivateField(sync, "latestSnapshotSequenceBySourceSession");
            var activeSnapshotIds = (IDictionary)GetPrivateField(sync, "activeSnapshotIds");
            Assert.That(latestSnapshotSequences.Count, Is.EqualTo(2));
            Assert.That(activeSnapshotIds.Contains("snapshot-2"), Is.True);
            Assert.That(activeSnapshotIds.Contains("snapshot-delayed"), Is.False);
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
        public void ScanBindings_Repeatedly_DoesNotDuplicateContinuousListenersOrTrackers()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = "MasterSlider";
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            var scrollbarObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
            scrollbarObject.name = "MasterScrollbar";
            scrollbarObject.transform.SetParent(canvasObject.transform, false);
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.minimumProposeIntervalSeconds = 0f;
            profile.sliderEpsilon = 0f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            for (var index = 0; index < 12; index++)
            {
                InvokePrivate(sync, "ScanBindings");
                InvokePrivate(sync, "InitializeLocalState");
            }

            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            slider.value = 0.35f;
            scrollbar.value = 0.45f;

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(2));
            Assert.That(CountContinuousInteractionTrackers(sliderObject), Is.EqualTo(1));
            Assert.That(CountContinuousInteractionTrackers(scrollbarObject), Is.EqualTo(1));
        }

        [Test]
        public void ScanBindings_DuringContinuousInteraction_CancelsDeferredCommitWithoutBroadcasting()
        {
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sliderObject = new GameObject("MasterSlider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(canvasObject.transform, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.value = 0.25f;
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.allowedPeers.Add("PeerB");
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
            AssignProfile(sync, profile);
            InvokePrivate(sync, "Awake");
            var binding = ((IDictionary)GetPrivateField(sync, "bindings")).Values.Cast<object>().Single();
            var syncId = (string)binding.GetType().GetProperty("SyncId").GetValue(binding);
            binding.GetType().GetProperty("IsInteracting").SetValue(binding, true);
            InvokePrivate(sync, "HandleCommitState", "PeerB", "SessionB", "OperationCanvas", syncId, "Slider", 0.9f, 100L, "PeerB", 1);
            Assert.That(((IDictionary)GetPrivateField(sync, "deferredCommits")).Contains(syncId), Is.True);

            sync.GetType().GetField("sentMessageCount", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sync, 0);
            InvokePrivate(sync, "ScanBindings");
            InvokePrivate(sync, "InitializeLocalState");

            Assert.That((int)GetPrivateField(sync, "sentMessageCount"), Is.EqualTo(0));
            Assert.That(slider.value, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(((IDictionary)GetPrivateField(sync, "deferredCommits")).Contains(syncId), Is.False);
            var refreshedBinding = ((IEnumerable)GetPrivateField(sync, "continuousBindings")).Cast<object>().Single();
            Assert.That((bool)refreshedBinding.GetType().GetProperty("IsInteracting").GetValue(refreshedBinding), Is.False);
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
            Assert.That(peerA.enableDebugLog, Is.False);
            Assert.That(peerA.listenPort, Is.EqualTo(9000));
            Assert.That(peerA.allowedPeers, Is.EquivalentTo(new[] { "PeerB" }));
            Assert.That(peerA.peerEndpoints.Count, Is.EqualTo(1));
            Assert.That(peerA.peerEndpoints[0].name, Is.EqualTo("PeerB"));
            Assert.That(peerA.peerEndpoints[0].port, Is.EqualTo(9001));
            Assert.That(peerB.nodeId, Is.EqualTo("PeerB"));
            Assert.That(peerB.enableOscTransport, Is.True);
            Assert.That(peerB.enableDebugLog, Is.False);
            Assert.That(peerB.listenPort, Is.EqualTo(9001));
            Assert.That(peerB.allowedPeers, Is.EquivalentTo(new[] { "PeerA" }));
            Assert.That(peerB.peerEndpoints.Count, Is.EqualTo(1));
            Assert.That(peerB.peerEndpoints[0].name, Is.EqualTo("PeerA"));
            Assert.That(peerB.peerEndpoints[0].port, Is.EqualTo(9000));
        }

        [Test]
        public void Profile_DefaultDebugLog_IsDisabled()
        {
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            var canvasObject = new GameObject("OperationCanvas", typeof(Canvas));
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignProfile(sync, profile);

            Assert.That(profile.enableDebugLog, Is.False);
            Assert.That((bool)InvokePrivate(sync, "ShouldDebugLog"), Is.False);
            Assert.That((bool)InvokePrivate(sync, "ShouldStatisticsLog"), Is.False);

            profile.enableDebugLog = true;
            Assert.That((bool)InvokePrivate(sync, "ShouldDebugLog"), Is.True);
            Assert.That((bool)InvokePrivate(sync, "ShouldStatisticsLog"), Is.False);

            profile.enableStatisticsLog = true;
            Assert.That((bool)InvokePrivate(sync, "ShouldStatisticsLog"), Is.True);
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
            profile.initialSyncPendingTimeoutSeconds = -11f;
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
            Assert.That(profile.initialSyncPendingTimeoutSeconds, Is.EqualTo(0f));
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
        public void Profile_DefaultInitialSyncPendingTimeoutSeconds_IsOneSecond()
        {
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            Assert.That(profile.initialSyncPendingTimeoutSeconds, Is.EqualTo(1f));
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
            var excludedUiProperty = serializedObject.FindProperty("excludedUi");
            Assert.That(excludedUiProperty, Is.Not.Null);
            excludedUiProperty.arraySize = components.Length;
            for (var index = 0; index < components.Length; index++)
            {
                excludedUiProperty.GetArrayElementAtIndex(index).FindPropertyRelative("target").objectReferenceValue = components[index];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignLegacyExcludedComponents(CanvasUiSync sync, params Component[] components)
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

        private static int CountContinuousInteractionTrackers(GameObject target)
        {
            return target.GetComponents<Component>().Count(component => component.GetType().FullName == "Mizotake.UnityUiSync.CanvasUiSyncContinuousInteractionTracker");
        }

        private static UnityEngine.Profiling.Recorder StartMarkerRecorder(string markerName)
        {
            var recorder = UnityEngine.Profiling.Recorder.Get(markerName);
            recorder.enabled = false;
            recorder.enabled = true;
            return recorder;
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

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"field {fieldName} was not found");
            field.SetValue(instance, value);
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
