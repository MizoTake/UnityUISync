using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync.Tests.PlayMode
{
    public sealed class CanvasUiSyncPlayModeTests
    {
        private const string SampleScenePath = "Assets/Scenes/UnityUiSyncSample.unity";
        private static int nextTestPort = 19000;

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            foreach (var canvas in Object.FindObjectsOfType<CanvasUiSync>(true))
            {
                Object.Destroy(canvas.gameObject);
            }

            yield return WaitFrames(3);
        }

        [UnityTest]
        public IEnumerator SampleScene_ToggleSyncsAndPresenterReflectsRemoteState()
        {
            yield return LoadSampleScene();
            var peerAPowerToggle = FindControl<Toggle>("PeerACanvas", "PowerToggle");
            var peerBPowerToggle = FindControl<Toggle>("PeerBCanvas", "PowerToggle");
            var peerBPresenter = FindPresenter("PeerBCanvas");
            Assert.That(peerAPowerToggle, Is.Not.Null);
            Assert.That(peerBPowerToggle, Is.Not.Null);
            Assert.That(peerBPresenter, Is.Not.Null);

            peerAPowerToggle.isOn = true;
            yield return WaitUntil(() => peerBPowerToggle.isOn && peerBPresenter.powerValueText.text == "ON" && peerBPresenter.powerToggleCheckmark.enabled, 30);

            Assert.That(peerBPowerToggle.isOn, Is.True);
            Assert.That(peerBPresenter.powerValueText.text, Is.EqualTo("ON"));
            Assert.That(peerBPresenter.powerToggleCheckmark.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SampleScene_FormControlsAndButtonRemainOperationalAcrossPeers()
        {
            yield return LoadSampleScene();
            var peerAMasterSlider = FindControl<Slider>("PeerACanvas", "MasterSlider");
            var peerBMasterSlider = FindControl<Slider>("PeerBCanvas", "MasterSlider");
            var peerAIntensityScrollbar = FindControl<Scrollbar>("PeerACanvas", "IntensityScrollbar");
            var peerBIntensityScrollbar = FindControl<Scrollbar>("PeerBCanvas", "IntensityScrollbar");
            var peerAModeDropdown = FindControl<Dropdown>("PeerACanvas", "ModeDropdown");
            var peerBModeDropdown = FindControl<Dropdown>("PeerBCanvas", "ModeDropdown");
            var peerAOperatorInput = FindControl<InputField>("PeerACanvas", "OperatorInput");
            var peerBOperatorInput = FindControl<InputField>("PeerBCanvas", "OperatorInput");
            var peerASyncButton = FindControl<Button>("PeerACanvas", "SyncButton");
            var peerATargetToggle = FindControl<Toggle>("PeerACanvas", "TargetToggle");
            var peerBTargetToggle = FindControl<Toggle>("PeerBCanvas", "TargetToggle");
            var peerAPresenter = FindPresenter("PeerACanvas");
            var peerBPresenter = FindPresenter("PeerBCanvas");
            Assert.That(peerAMasterSlider, Is.Not.Null);
            Assert.That(peerBMasterSlider, Is.Not.Null);
            Assert.That(peerAIntensityScrollbar, Is.Not.Null);
            Assert.That(peerBIntensityScrollbar, Is.Not.Null);
            Assert.That(peerAModeDropdown, Is.Not.Null);
            Assert.That(peerBModeDropdown, Is.Not.Null);
            Assert.That(peerAOperatorInput, Is.Not.Null);
            Assert.That(peerBOperatorInput, Is.Not.Null);
            Assert.That(peerASyncButton, Is.Not.Null);
            Assert.That(peerATargetToggle, Is.Not.Null);
            Assert.That(peerBTargetToggle, Is.Not.Null);
            Assert.That(peerAPresenter, Is.Not.Null);
            Assert.That(peerBPresenter, Is.Not.Null);

            peerAMasterSlider.value = 0.9f;
            yield return WaitUntil(() => Mathf.Abs(peerBMasterSlider.value - 0.9f) < 0.0001f && peerBPresenter.sliderValueText.text == "0.90", 30);
            Assert.That(peerBMasterSlider.value, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(peerBPresenter.sliderValueText.text, Is.EqualTo("0.90"));

            peerAIntensityScrollbar.value = 0.15f;
            yield return WaitUntil(() => Mathf.Abs(peerBIntensityScrollbar.value - 0.15f) < 0.0001f && peerBPresenter.scrollbarValueText.text == "0.15", 30);
            Assert.That(peerBIntensityScrollbar.value, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(peerBPresenter.scrollbarValueText.text, Is.EqualTo("0.15"));

            peerAModeDropdown.value = 2;
            yield return WaitUntil(() => peerBModeDropdown.value == 2 && peerBPresenter.modeValueText.text == "Bypass", 30);
            Assert.That(peerBModeDropdown.value, Is.EqualTo(2));
            Assert.That(peerBPresenter.modeValueText.text, Is.EqualTo("Bypass"));

            peerAOperatorInput.text = "Operator Z";
            peerAOperatorInput.onEndEdit.Invoke("Operator Z");
            yield return WaitUntil(() => peerBOperatorInput.text == "Operator Z" && peerBPresenter.operatorValueText.text == "Operator Z", 30);
            Assert.That(peerBOperatorInput.text, Is.EqualTo("Operator Z"));
            Assert.That(peerBPresenter.operatorValueText.text, Is.EqualTo("Operator Z"));

            peerASyncButton.onClick.Invoke();
            yield return WaitUntil(() => peerATargetToggle.isOn && peerBTargetToggle.isOn && peerAPresenter.buttonValueText.text == "TRIGGERED" && peerAPresenter.targetValueText.text == "ARMED" && peerBPresenter.buttonValueText.text == "TRIGGERED" && peerBPresenter.targetValueText.text == "ARMED", 30);
            Assert.That(peerATargetToggle.isOn, Is.True);
            Assert.That(peerAPresenter.buttonValueText.text, Is.EqualTo("TRIGGERED"));
            Assert.That(peerAPresenter.targetValueText.text, Is.EqualTo("ARMED"));
            Assert.That(peerBTargetToggle.isOn, Is.True);
            Assert.That(peerBPresenter.buttonValueText.text, Is.EqualTo("TRIGGERED"));
            Assert.That(peerBPresenter.targetValueText.text, Is.EqualTo("ARMED"));
        }

        [UnityTest]
        public IEnumerator SampleScene_DropdownShowHide_SyncsAcrossPeers_WithoutRegisteringRuntimeItems()
        {
            yield return LoadSampleScene();
            var peerADropdown = FindControl<Dropdown>("PeerACanvas", "ModeDropdown");
            var peerBDropdown = FindControl<Dropdown>("PeerBCanvas", "ModeDropdown");
            var peerASync = FindSync("PeerACanvas");
            var peerBSync = FindSync("PeerBCanvas");
            Assert.That(peerADropdown, Is.Not.Null);
            Assert.That(peerBDropdown, Is.Not.Null);
            Assert.That(peerASync, Is.Not.Null);
            Assert.That(peerBSync, Is.Not.Null);
            peerADropdown.alphaFadeSpeed = 0f;
            peerBDropdown.alphaFadeSpeed = 0f;

            var peerABindingCount = GetBindingCount(peerASync);
            var peerBBindingCount = GetBindingCount(peerBSync);

            peerADropdown.Show();
            yield return WaitUntil(() => IsDropdownExpanded(peerADropdown) && IsDropdownExpanded(peerBDropdown), 30);

            Assert.That(IsDropdownExpanded(peerADropdown), Is.True);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.True);
            Assert.That(GetBindingCount(peerASync), Is.EqualTo(peerABindingCount));
            Assert.That(GetBindingCount(peerBSync), Is.EqualTo(peerBBindingCount));

            peerADropdown.Hide();
            yield return WaitUntil(() => !IsDropdownExpanded(peerADropdown) && !IsDropdownExpanded(peerBDropdown), 60);

            Assert.That(IsDropdownExpanded(peerADropdown), Is.False);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.False);
            Assert.That(GetBindingCount(peerASync), Is.EqualTo(peerABindingCount));
            Assert.That(GetBindingCount(peerBSync), Is.EqualTo(peerBBindingCount), DescribeBindings(peerBSync));
            foreach (var sync in Object.FindObjectsOfType<CanvasUiSync>(true))
            {
                Object.Destroy(sync.gameObject);
            }

            foreach (var eventSystem in Object.FindObjectsOfType<EventSystem>(true))
            {
                Object.Destroy(eventSystem.gameObject);
            }

            yield return WaitFrames(3);
        }

        [UnityTest]
        public IEnumerator Toggle_SyncsBothDirections_WithoutOscTransport()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return WaitUntil(() => peerA.toggle.isOn && peerB.toggle.isOn, 30);
            Assert.That(peerA.toggle.isOn, Is.True);
            Assert.That(peerB.toggle.isOn, Is.True);

            peerB.toggle.isOn = false;
            yield return WaitUntil(() => !peerA.toggle.isOn && !peerB.toggle.isOn, 30);
            Assert.That(peerA.toggle.isOn, Is.False);
            Assert.That(peerB.toggle.isOn, Is.False);
        }

        [UnityTest]
        public IEnumerator Toggle_LocalOperation_DoesNotOscillateBackAfterSeveralFrames()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return WaitUntil(() => peerA.toggle.isOn && peerB.toggle.isOn, 30);

            Assert.That(peerA.toggle.isOn, Is.True);
            Assert.That(peerB.toggle.isOn, Is.True);
        }

        [UnityTest]
        public IEnumerator Toggle_RemoteSync_UpdatesSamplePresenterText()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort, true);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return WaitUntil(() => peerB.toggle.isOn && peerB.presenter != null && peerB.presenter.powerValueText.text == "ON", 30);

            Assert.That(peerB.toggle.isOn, Is.True);
            Assert.That(peerB.presenter, Is.Not.Null);
            Assert.That(peerB.presenter.powerValueText.text, Is.EqualTo("ON"));
        }

        [UnityTest]
        public IEnumerator Toggle_RemoteSync_UpdatesSamplePresenterCheckmark()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort, true);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return WaitUntil(() => peerB.toggle.isOn && peerB.presenter != null && peerB.presenter.powerToggleCheckmark != null && peerB.presenter.powerToggleCheckmark.enabled, 30);

            Assert.That(peerB.toggle.isOn, Is.True);
            Assert.That(peerB.presenter, Is.Not.Null);
            Assert.That(peerB.presenter.powerToggleCheckmark, Is.Not.Null);
            Assert.That(peerB.presenter.powerToggleCheckmark.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Toggle_RepeatedBidirectionalSync_RemainsConsistentOverManyFrames()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;
            var expected = false;
            for (var index = 0; index < 120; index++)
            {
                expected = index % 2 == 0;
                if (index % 2 == 0)
                {
                    peerA.toggle.isOn = expected;
                }
                else
                {
                    peerB.toggle.isOn = expected;
                }

                yield return null;
            }

            yield return WaitUntil(() => peerA.toggle.isOn == expected && peerB.toggle.isOn == expected, 30);
            Assert.That(peerA.toggle.isOn, Is.EqualTo(expected));
            Assert.That(peerB.toggle.isOn, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator Toggle_RepeatedEnableDisableWithRescanOnEnable_KeepsSyncStable()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            SetPrivateField(peerA.sync, "rescanOnEnable", true);
            SetPrivateField(peerB.sync, "rescanOnEnable", true);
            yield return null;
            yield return null;
            for (var index = 0; index < 20; index++)
            {
                peerA.sync.gameObject.SetActive(false);
                peerB.sync.gameObject.SetActive(false);
                yield return null;
                peerA.sync.gameObject.SetActive(true);
                peerB.sync.gameObject.SetActive(true);
                yield return null;
            }

            peerA.toggle.isOn = true;
            yield return null;
            yield return null;
            Assert.That(peerB.toggle.isOn, Is.True);
            peerB.toggle.isOn = false;
            yield return null;
            yield return null;
            Assert.That(peerA.toggle.isOn, Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedToggle_SyncsAcrossPeers()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeToggle = CreateRuntimeToggle(peerA.sync.transform, "RuntimeToggle");
            var peerBRuntimeToggle = CreateRuntimeToggle(peerB.sync.transform, "RuntimeToggle");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeToggle:Toggle") && HasBinding(peerB.sync, "DemoCanvas/RuntimeToggle:Toggle"), 60);

            peerARuntimeToggle.isOn = true;
            yield return WaitUntil(() => peerBRuntimeToggle.isOn, 60);

            Assert.That(peerBRuntimeToggle.isOn, Is.True);
        }

        private static (CanvasUiSync sync, Toggle toggle, CanvasUiSyncSamplePresenter presenter) CreatePeer(string canvasName, string nodeId, string remoteNodeId, int listenPort, int remotePort, bool attachPresenter = false)
        {
            var canvasObject = new GameObject(canvasName, typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.SetActive(false);
            var toggleObject = new GameObject("PowerToggle", typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(canvasObject.transform, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            CanvasUiSyncSamplePresenter presenter = null;
            if (attachPresenter)
            {
                var presenterObject = new GameObject("SamplePresenter", typeof(CanvasUiSyncSamplePresenter));
                presenterObject.transform.SetParent(canvasObject.transform, false);
                presenter = presenterObject.GetComponent<CanvasUiSyncSamplePresenter>();
                presenter.powerToggle = toggle;
                presenter.powerValueText = new GameObject("PowerValueText", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                presenter.powerValueText.transform.SetParent(presenterObject.transform, false);
                presenter.powerLamp = new GameObject("PowerLamp", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                presenter.powerLamp.transform.SetParent(presenterObject.transform, false);
                presenter.powerToggleBackground = new GameObject("PowerToggleBackground", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                presenter.powerToggleBackground.transform.SetParent(presenterObject.transform, false);
                presenter.powerToggleCheckmark = new GameObject("PowerToggleCheckmark", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                presenter.powerToggleCheckmark.transform.SetParent(presenterObject.transform, false);
            }

            var sync = canvasObject.AddComponent<CanvasUiSync>();
            var profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
            profile.profileName = nodeId;
            profile.nodeId = nodeId;
            profile.enableOscTransport = false;
            profile.listenPort = listenPort;
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.allowedPeers.Add(remoteNodeId);
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = remoteNodeId, ipAddress = "127.0.0.1", port = remotePort, enabled = true });
            SetPrivateField(sync, "profile", profile);
            SetPrivateField(sync, "canvasIdOverride", "DemoCanvas");
            canvasObject.SetActive(true);
            return (sync, toggle, presenter);
        }

        private static (int peerAPort, int peerBPort) AllocatePortPair()
        {
            var peerAPort = nextTestPort;
            var peerBPort = nextTestPort + 1;
            nextTestPort += 10;
            return (peerAPort, peerBPort);
        }

        private static Toggle CreateRuntimeToggle(Transform parent, string toggleName)
        {
            var toggleObject = new GameObject(toggleName, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"field {fieldName} was not found");
            field.SetValue(instance, value);
        }

        private static IEnumerator LoadSampleScene()
        {
            SceneManager.LoadScene(SampleScenePath, LoadSceneMode.Single);
            yield return WaitFrames(3);
        }

        private static IEnumerator WaitFrames(int frameCount)
        {
            for (var index = 0; index < frameCount; index++)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition, int maxFrameCount)
        {
            for (var index = 0; index < maxFrameCount; index++)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static T FindControl<T>(string canvasName, string controlName) where T : Component
        {
            return Object.FindObjectsOfType<T>(true).FirstOrDefault(component => component.name == controlName && component.GetComponentsInParent<Canvas>(true).Any(canvas => canvas.name == canvasName));
        }

        private static CanvasUiSync FindSync(string canvasName)
        {
            return Object.FindObjectsOfType<CanvasUiSync>(true).FirstOrDefault(component => component.name == canvasName);
        }

        private static int GetBindingCount(CanvasUiSync sync)
        {
            return ((IDictionary)GetPrivateField(sync, "bindings")).Count;
        }

        private static bool HasBinding(CanvasUiSync sync, string syncId)
        {
            return ((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId);
        }

        private static string DescribeBindings(CanvasUiSync sync)
        {
            return string.Join(", ", ((IDictionary)GetPrivateField(sync, "bindings")).Keys.Cast<object>().Select(key => key.ToString()).OrderBy(key => key));
        }

        private static bool IsDropdownExpanded(Dropdown dropdown)
        {
            var field = typeof(Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(dropdown) != null;
        }

        private static object GetPrivateField(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"field {fieldName} was not found");
            return field.GetValue(instance);
        }

        private static CanvasUiSyncSamplePresenter FindPresenter(string canvasName)
        {
            return Object.FindObjectsOfType<CanvasUiSyncSamplePresenter>(true).FirstOrDefault(component => component.GetComponentsInParent<Canvas>(true).Any(canvas => canvas.name == canvasName));
        }
    }
}
