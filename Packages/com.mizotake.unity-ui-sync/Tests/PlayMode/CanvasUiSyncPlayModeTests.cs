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
        public IEnumerator SampleScene_DropdownClickSelection_SyncsAcrossPeers_WithoutRegisteringRuntimeItems()
        {
            var initialEventSystemCount = Object.FindObjectsOfType<EventSystem>(true).Length;
            yield return LoadSampleScene();
            var peerADropdown = FindControl<Dropdown>("PeerACanvas", "ModeDropdown");
            var peerBDropdown = FindControl<Dropdown>("PeerBCanvas", "ModeDropdown");
            var peerAPresenter = FindPresenter("PeerACanvas");
            var peerBPresenter = FindPresenter("PeerBCanvas");
            var peerASync = FindSync("PeerACanvas");
            var peerBSync = FindSync("PeerBCanvas");
            Assert.That(peerADropdown, Is.Not.Null);
            Assert.That(peerBDropdown, Is.Not.Null);
            Assert.That(peerAPresenter, Is.Not.Null);
            Assert.That(peerBPresenter, Is.Not.Null);
            Assert.That(peerASync, Is.Not.Null);
            Assert.That(peerBSync, Is.Not.Null);
            peerADropdown.alphaFadeSpeed = 0f;
            peerBDropdown.alphaFadeSpeed = 0f;

            var peerABindingCount = GetBindingCount(peerASync);
            var peerBBindingCount = GetBindingCount(peerBSync);

            ClickDropdown(peerADropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerADropdown) && IsDropdownExpanded(peerBDropdown), 30);

            Assert.That(IsDropdownExpanded(peerADropdown), Is.True);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.True);

            SelectDropdownItem(peerADropdown, 2);
            yield return WaitUntil(() => peerADropdown.value == 2 && peerBDropdown.value == 2 && !IsDropdownExpanded(peerADropdown) && !IsDropdownExpanded(peerBDropdown) && peerAPresenter.modeValueText.text == "Bypass" && peerBPresenter.modeValueText.text == "Bypass", 60);

            Assert.That(peerADropdown.value, Is.EqualTo(2));
            Assert.That(peerBDropdown.value, Is.EqualTo(2));
            Assert.That(peerAPresenter.modeValueText.text, Is.EqualTo("Bypass"));
            Assert.That(peerBPresenter.modeValueText.text, Is.EqualTo("Bypass"));
            Assert.That(IsDropdownExpanded(peerADropdown), Is.False);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.False);
            Assert.That(GetBindingCount(peerASync), Is.EqualTo(peerABindingCount));
            Assert.That(GetBindingCount(peerBSync), Is.EqualTo(peerBBindingCount), DescribeBindings(peerBSync));
            Assert.That(Object.FindObjectsOfType<EventSystem>(true).Length, Is.EqualTo(initialEventSystemCount + 1));
        }

        [UnityTest]
        public IEnumerator SampleScene_DropdownClickSelection_WithDefaultFade_DoesNotReopenAndAllowsSecondSelection()
        {
            yield return LoadSampleScene();
            var peerADropdown = FindControl<Dropdown>("PeerACanvas", "ModeDropdown");
            var peerBDropdown = FindControl<Dropdown>("PeerBCanvas", "ModeDropdown");
            var peerAPresenter = FindPresenter("PeerACanvas");
            var peerBPresenter = FindPresenter("PeerBCanvas");
            Assert.That(peerADropdown, Is.Not.Null);
            Assert.That(peerBDropdown, Is.Not.Null);
            Assert.That(peerAPresenter, Is.Not.Null);
            Assert.That(peerBPresenter, Is.Not.Null);

            ClickDropdown(peerADropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerADropdown) && IsDropdownExpanded(peerBDropdown), 30);
            SelectDropdownItem(peerADropdown, 2);
            yield return WaitUntil(() => peerADropdown.value == 2 && peerBDropdown.value == 2 && !IsDropdownExpanded(peerADropdown) && !IsDropdownExpanded(peerBDropdown), 120);

            yield return WaitFrames(20);
            Assert.That(IsDropdownExpanded(peerADropdown), Is.False);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.False);
            Assert.That(peerAPresenter.modeValueText.text, Is.EqualTo("Bypass"));
            Assert.That(peerBPresenter.modeValueText.text, Is.EqualTo("Bypass"));

            ClickDropdown(peerADropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerADropdown) && IsDropdownExpanded(peerBDropdown), 30);
            SelectDropdownItem(peerADropdown, 1);
            yield return WaitUntil(() => peerADropdown.value == 1 && peerBDropdown.value == 1 && !IsDropdownExpanded(peerADropdown) && !IsDropdownExpanded(peerBDropdown), 120);

            yield return WaitFrames(20);
            Assert.That(IsDropdownExpanded(peerADropdown), Is.False);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.False);
            Assert.That(peerAPresenter.modeValueText.text, Is.EqualTo("Live"));
            Assert.That(peerBPresenter.modeValueText.text, Is.EqualTo("Live"));
        }

        [UnityTest]
        public IEnumerator SampleScene_DropdownOpenItems_FollowRemoteSelectionWhileExpanded()
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

            ClickDropdown(peerADropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerADropdown), 30);
            ClickDropdown(peerBDropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerADropdown) && IsDropdownExpanded(peerBDropdown), 30);
            yield return WaitUntil(() => HasBinding(peerASync, "DemoCanvas/ModeDropdown:DropdownItemToggle[0]") && HasBinding(peerASync, "DemoCanvas/ModeDropdown:DropdownItemToggle[1]") && HasBinding(peerASync, "DemoCanvas/ModeDropdown:DropdownItemToggle[2]") && HasBinding(peerBSync, "DemoCanvas/ModeDropdown:DropdownItemToggle[0]") && HasBinding(peerBSync, "DemoCanvas/ModeDropdown:DropdownItemToggle[1]") && HasBinding(peerBSync, "DemoCanvas/ModeDropdown:DropdownItemToggle[2]"), 60);

            var peerBItemToggles = GetDropdownItemToggles(peerBDropdown);
            Assert.That(peerBItemToggles[1].isOn, Is.True);
            Assert.That(peerBItemToggles[2].isOn, Is.False);

            peerADropdown.value = 2;
            yield return WaitUntil(() => peerBDropdown.value == 2 && GetDropdownItemToggles(peerBDropdown)[2].isOn && !GetDropdownItemToggles(peerBDropdown)[1].isOn, 60);

            Assert.That(IsDropdownExpanded(peerADropdown), Is.True);
            Assert.That(IsDropdownExpanded(peerBDropdown), Is.True);
            Assert.That(GetDropdownItemToggles(peerBDropdown)[2].isOn, Is.True);
            Assert.That(GetDropdownItemToggles(peerBDropdown)[1].isOn, Is.False);
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

        [UnityTest]
        public IEnumerator SameNameSiblingToggles_SyncByHierarchyOrder()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerAGroup0 = CreateRuntimeContainer(peerA.sync.transform, "Group");
            var peerAGroup1 = CreateRuntimeContainer(peerA.sync.transform, "Group");
            var peerBGroup0 = CreateRuntimeContainer(peerB.sync.transform, "Group");
            var peerBGroup1 = CreateRuntimeContainer(peerB.sync.transform, "Group");
            var peerAToggle0 = CreateRuntimeToggle(peerAGroup0, "Option");
            var peerAToggle1 = CreateRuntimeToggle(peerAGroup1, "Option");
            var peerBToggle0 = CreateRuntimeToggle(peerBGroup0, "Option");
            var peerBToggle1 = CreateRuntimeToggle(peerBGroup1, "Option");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/Group[0]/Option:Toggle") && HasBinding(peerA.sync, "DemoCanvas/Group[1]/Option:Toggle") && HasBinding(peerB.sync, "DemoCanvas/Group[0]/Option:Toggle") && HasBinding(peerB.sync, "DemoCanvas/Group[1]/Option:Toggle"), 60);

            peerAToggle1.isOn = true;
            yield return WaitUntil(() => !peerBToggle0.isOn && peerBToggle1.isOn, 60);

            Assert.That(peerBToggle0.isOn, Is.False);
            Assert.That(peerBToggle1.isOn, Is.True);

            peerAToggle0.isOn = true;
            yield return WaitUntil(() => peerBToggle0.isOn && peerBToggle1.isOn, 60);

            Assert.That(peerBToggle0.isOn, Is.True);
            Assert.That(peerBToggle1.isOn, Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedSlider_SyncsAcrossPeers()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeSlider = CreateRuntimeSlider(peerA.sync.transform, "RuntimeSlider");
            var peerBRuntimeSlider = CreateRuntimeSlider(peerB.sync.transform, "RuntimeSlider");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeSlider:Slider") && HasBinding(peerB.sync, "DemoCanvas/RuntimeSlider:Slider"), 60);

            peerARuntimeSlider.value = 0.75f;
            yield return WaitUntil(() => Mathf.Abs(peerBRuntimeSlider.value - 0.75f) < 0.0001f, 60);

            Assert.That(peerBRuntimeSlider.value, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedScrollbar_SyncsAcrossPeers()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeScrollbar = CreateRuntimeScrollbar(peerA.sync.transform, "RuntimeScrollbar");
            var peerBRuntimeScrollbar = CreateRuntimeScrollbar(peerB.sync.transform, "RuntimeScrollbar");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeScrollbar:Scrollbar") && HasBinding(peerB.sync, "DemoCanvas/RuntimeScrollbar:Scrollbar"), 60);

            peerARuntimeScrollbar.value = 0.2f;
            yield return WaitUntil(() => Mathf.Abs(peerBRuntimeScrollbar.value - 0.2f) < 0.0001f, 60);

            Assert.That(peerBRuntimeScrollbar.value, Is.EqualTo(0.2f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedDropdown_ValueSyncsAcrossPeers()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeDropdown = CreateRuntimeDropdown(peerA.sync.transform, "RuntimeDropdown");
            var peerBRuntimeDropdown = CreateRuntimeDropdown(peerB.sync.transform, "RuntimeDropdown");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeDropdown:Dropdown") && HasBinding(peerB.sync, "DemoCanvas/RuntimeDropdown:Dropdown"), 60);

            peerARuntimeDropdown.value = 2;
            yield return WaitUntil(() => peerBRuntimeDropdown.value == 2, 60);

            Assert.That(peerBRuntimeDropdown.value, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedDropdown_RemoteApply_InvokesOnValueChangedOnReceiver()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeDropdown = CreateRuntimeDropdown(peerA.sync.transform, "RuntimeDropdown");
            var peerBRuntimeDropdown = CreateRuntimeDropdown(peerB.sync.transform, "RuntimeDropdown");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeDropdown:Dropdown") && HasBinding(peerB.sync, "DemoCanvas/RuntimeDropdown:Dropdown"), 60);

            var remoteEventCount = 0;
            var lastRemoteValue = -1;
            peerBRuntimeDropdown.onValueChanged.AddListener(value => { remoteEventCount++; lastRemoteValue = value; });

            peerARuntimeDropdown.value = 2;
            yield return WaitUntil(() => peerBRuntimeDropdown.value == 2 && remoteEventCount > 0 && lastRemoteValue == 2, 60);

            Assert.That(peerBRuntimeDropdown.value, Is.EqualTo(2));
            Assert.That(remoteEventCount, Is.EqualTo(1));
            Assert.That(lastRemoteValue, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedDropdown_ClickOpenAndBlockerClose_SyncsAcrossPeers()
        {
            EnsureEventSystemExists();
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeDropdown = CreateRuntimeDropdown(peerA.sync.transform, "ClickableDropdown");
            var peerBRuntimeDropdown = CreateRuntimeDropdown(peerB.sync.transform, "ClickableDropdown");
            peerARuntimeDropdown.alphaFadeSpeed = 0f;
            peerBRuntimeDropdown.alphaFadeSpeed = 0f;
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/ClickableDropdown:DropdownExpanded") && HasBinding(peerB.sync, "DemoCanvas/ClickableDropdown:DropdownExpanded"), 60);

            ClickDropdown(peerARuntimeDropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerARuntimeDropdown) && IsDropdownExpanded(peerBRuntimeDropdown), 60);

            Assert.That(IsDropdownExpanded(peerARuntimeDropdown), Is.True);
            Assert.That(IsDropdownExpanded(peerBRuntimeDropdown), Is.True);

            ClickDropdownBlocker(peerARuntimeDropdown);
            yield return WaitUntil(() => !IsDropdownExpanded(peerARuntimeDropdown) && !IsDropdownExpanded(peerBRuntimeDropdown), 60);

            Assert.That(IsDropdownExpanded(peerARuntimeDropdown), Is.False);
            Assert.That(IsDropdownExpanded(peerBRuntimeDropdown), Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedDropdown_ClickOption_SyncsValueAndClosesAcrossPeers()
        {
            EnsureEventSystemExists();
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeDropdown = CreateRuntimeDropdown(peerA.sync.transform, "SelectableDropdown");
            var peerBRuntimeDropdown = CreateRuntimeDropdown(peerB.sync.transform, "SelectableDropdown");
            peerARuntimeDropdown.alphaFadeSpeed = 0f;
            peerBRuntimeDropdown.alphaFadeSpeed = 0f;
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/SelectableDropdown:Dropdown") && HasBinding(peerB.sync, "DemoCanvas/SelectableDropdown:DropdownExpanded"), 60);

            ClickDropdown(peerARuntimeDropdown);
            yield return WaitUntil(() => IsDropdownExpanded(peerARuntimeDropdown) && IsDropdownExpanded(peerBRuntimeDropdown), 60);

            SelectDropdownItem(peerARuntimeDropdown, 2);
            yield return WaitUntil(() => peerARuntimeDropdown.value == 2 && peerBRuntimeDropdown.value == 2 && !IsDropdownExpanded(peerARuntimeDropdown) && !IsDropdownExpanded(peerBRuntimeDropdown), 60);

            Assert.That(peerARuntimeDropdown.value, Is.EqualTo(2));
            Assert.That(peerBRuntimeDropdown.value, Is.EqualTo(2));
            Assert.That(IsDropdownExpanded(peerARuntimeDropdown), Is.False);
            Assert.That(IsDropdownExpanded(peerBRuntimeDropdown), Is.False);
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedInputField_OnEndEdit_SyncsAcrossPeers()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerARuntimeInput = CreateRuntimeInputField(peerA.sync.transform, "RuntimeInput");
            var peerBRuntimeInput = CreateRuntimeInputField(peerB.sync.transform, "RuntimeInput");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeInput:InputField") && HasBinding(peerB.sync, "DemoCanvas/RuntimeInput:InputField"), 60);

            peerARuntimeInput.text = "Pending";
            yield return WaitFrames(5);
            Assert.That(peerBRuntimeInput.text, Is.EqualTo(string.Empty));

            peerARuntimeInput.onEndEdit.Invoke("Pending");
            yield return WaitUntil(() => peerBRuntimeInput.text == "Pending", 60);

            Assert.That(peerBRuntimeInput.text, Is.EqualTo("Pending"));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedInputField_OnValueChanged_SyncsAcrossPeers_WithoutEndEdit()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            ((CanvasUiSyncProfile)GetPrivateField(peerA.sync, "profile")).stringSendMode = CanvasUiSyncStringSendMode.OnValueChanged;
            ((CanvasUiSyncProfile)GetPrivateField(peerB.sync, "profile")).stringSendMode = CanvasUiSyncStringSendMode.OnValueChanged;
            yield return null;
            yield return null;

            var peerARuntimeInput = CreateRuntimeInputField(peerA.sync.transform, "RuntimeInputOnValueChanged");
            var peerBRuntimeInput = CreateRuntimeInputField(peerB.sync.transform, "RuntimeInputOnValueChanged");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeInputOnValueChanged:InputField") && HasBinding(peerB.sync, "DemoCanvas/RuntimeInputOnValueChanged:InputField"), 60);

            peerARuntimeInput.text = "Streaming";
            peerARuntimeInput.onValueChanged.Invoke("Streaming");
            yield return WaitUntil(() => peerBRuntimeInput.text == "Streaming", 60);

            Assert.That(peerBRuntimeInput.text, Is.EqualTo("Streaming"));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedControls_WithBindingIds_SyncAcrossDifferentHierarchies()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerAContainer = CreateRuntimeContainer(peerA.sync.transform, "OperationsPanel");
            var peerBContainer = CreateRuntimeContainer(peerB.sync.transform, "DiagnosticsPanel");
            var peerAToggle = CreateRuntimeToggle(peerAContainer, "LocalToggle");
            var peerBToggle = CreateRuntimeToggle(peerBContainer, "RemoteToggle");
            AddBindingId(peerAToggle.gameObject, "SharedToggle");
            AddBindingId(peerBToggle.gameObject, "SharedToggle");
            var peerASlider = CreateRuntimeSlider(peerAContainer, "LocalSlider");
            var peerBSlider = CreateRuntimeSlider(peerBContainer, "RemoteSlider");
            AddBindingId(peerASlider.gameObject, "SharedSlider");
            AddBindingId(peerBSlider.gameObject, "SharedSlider");
            var peerAScrollbar = CreateRuntimeScrollbar(peerAContainer, "LocalScrollbar");
            var peerBScrollbar = CreateRuntimeScrollbar(peerBContainer, "RemoteScrollbar");
            AddBindingId(peerAScrollbar.gameObject, "SharedScrollbar");
            AddBindingId(peerBScrollbar.gameObject, "SharedScrollbar");
            var peerADropdown = CreateRuntimeDropdown(peerAContainer, "LocalDropdown");
            var peerBDropdown = CreateRuntimeDropdown(peerBContainer, "RemoteDropdown");
            AddBindingId(peerADropdown.gameObject, "SharedDropdown");
            AddBindingId(peerBDropdown.gameObject, "SharedDropdown");
            var peerAInput = CreateRuntimeInputField(peerAContainer, "LocalInput");
            var peerBInput = CreateRuntimeInputField(peerBContainer, "RemoteInput");
            AddBindingId(peerAInput.gameObject, "SharedInputField");
            AddBindingId(peerBInput.gameObject, "SharedInputField");
            var peerAIndicator = CreateRuntimeText(peerAContainer, "LocalButtonState", "READY");
            var peerBIndicator = CreateRuntimeText(peerBContainer, "RemoteButtonState", "READY");
            var peerAButton = CreateRuntimeButton(peerAContainer, peerAIndicator, "LocalButton");
            var peerBButton = CreateRuntimeButton(peerBContainer, peerBIndicator, "RemoteButton");
            AddBindingId(peerAButton.gameObject, "SharedButton");
            AddBindingId(peerBButton.gameObject, "SharedButton");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/SharedToggle:Toggle") && HasBinding(peerB.sync, "DemoCanvas/SharedToggle:Toggle") && HasBinding(peerA.sync, "DemoCanvas/SharedSlider:Slider") && HasBinding(peerB.sync, "DemoCanvas/SharedSlider:Slider") && HasBinding(peerA.sync, "DemoCanvas/SharedScrollbar:Scrollbar") && HasBinding(peerB.sync, "DemoCanvas/SharedScrollbar:Scrollbar") && HasBinding(peerA.sync, "DemoCanvas/SharedDropdown:Dropdown") && HasBinding(peerB.sync, "DemoCanvas/SharedDropdown:Dropdown") && HasBinding(peerA.sync, "DemoCanvas/SharedInputField:InputField") && HasBinding(peerB.sync, "DemoCanvas/SharedInputField:InputField") && HasBinding(peerA.sync, "DemoCanvas/SharedButton:Button") && HasBinding(peerB.sync, "DemoCanvas/SharedButton:Button"), 60);

            peerAToggle.isOn = true;
            peerASlider.value = 0.61f;
            peerAScrollbar.value = 0.33f;
            peerADropdown.value = 1;
            peerAInput.text = "Bound";
            peerAInput.onEndEdit.Invoke("Bound");
            peerAButton.onClick.Invoke();
            yield return WaitUntil(() => peerBToggle.isOn && Mathf.Abs(peerBSlider.value - 0.61f) < 0.0001f && Mathf.Abs(peerBScrollbar.value - 0.33f) < 0.0001f && peerBDropdown.value == 1 && peerBInput.text == "Bound" && peerBIndicator.text == "CLICKED", 60);

            Assert.That(peerBToggle.isOn, Is.True);
            Assert.That(peerBSlider.value, Is.EqualTo(0.61f).Within(0.0001f));
            Assert.That(peerBScrollbar.value, Is.EqualTo(0.33f).Within(0.0001f));
            Assert.That(peerBDropdown.value, Is.EqualTo(1));
            Assert.That(peerBInput.text, Is.EqualTo("Bound"));
            Assert.That(peerAIndicator.text, Is.EqualTo("CLICKED"));
            Assert.That(peerBIndicator.text, Is.EqualTo("CLICKED"));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedButton_SyncsAcrossPeers()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerAIndicator = CreateRuntimeText(peerA.sync.transform, "RuntimeButtonState", "READY");
            var peerBIndicator = CreateRuntimeText(peerB.sync.transform, "RuntimeButtonState", "READY");
            var peerARuntimeButton = CreateRuntimeButton(peerA.sync.transform, peerAIndicator, "RuntimeButton");
            var peerBRuntimeButton = CreateRuntimeButton(peerB.sync.transform, peerBIndicator, "RuntimeButton");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeButton:Button") && HasBinding(peerB.sync, "DemoCanvas/RuntimeButton:Button"), 60);

            peerARuntimeButton.onClick.Invoke();
            yield return WaitUntil(() => peerAIndicator.text == "CLICKED" && peerBIndicator.text == "CLICKED", 60);

            Assert.That(peerARuntimeButton, Is.Not.Null);
            Assert.That(peerBRuntimeButton, Is.Not.Null);
            Assert.That(peerAIndicator.text, Is.EqualTo("CLICKED"));
            Assert.That(peerBIndicator.text, Is.EqualTo("CLICKED"));
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedButton_ClickBeforeRemoteBindingExists_AppliesAfterRemoteButtonAppears()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerAIndicator = CreateRuntimeText(peerA.sync.transform, "LateRuntimeButtonState", "READY");
            var peerBIndicator = CreateRuntimeText(peerB.sync.transform, "LateRuntimeButtonState", "READY");
            var peerARuntimeButton = CreateRuntimeButton(peerA.sync.transform, peerAIndicator, "LateRuntimeButton");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/LateRuntimeButton:Button"), 60);

            peerARuntimeButton.onClick.Invoke();
            yield return WaitFrames(10);
            Assert.That(peerAIndicator.text, Is.EqualTo("CLICKED"));
            Assert.That(peerBIndicator.text, Is.EqualTo("READY"));

            var peerBRuntimeButton = CreateRuntimeButton(peerB.sync.transform, peerBIndicator, "LateRuntimeButton");
            yield return WaitUntil(() => HasBinding(peerB.sync, "DemoCanvas/LateRuntimeButton:Button") && peerBIndicator.text == "CLICKED", 60);

            Assert.That(peerBRuntimeButton, Is.Not.Null);
            Assert.That(peerBIndicator.text, Is.EqualTo("CLICKED"));
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

        private static Transform CreateRuntimeContainer(Transform parent, string objectName)
        {
            var containerObject = new GameObject(objectName, typeof(RectTransform));
            containerObject.transform.SetParent(parent, false);
            return containerObject.transform;
        }

        private static Toggle CreateRuntimeToggle(Transform parent, string toggleName)
        {
            var toggleObject = new GameObject(toggleName, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);
            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            return toggle;
        }

        private static Slider CreateRuntimeSlider(Transform parent, string sliderName)
        {
            var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
            sliderObject.name = sliderName;
            sliderObject.transform.SetParent(parent, false);
            var slider = sliderObject.GetComponent<Slider>();
            slider.SetValueWithoutNotify(0f);
            return slider;
        }

        private static Scrollbar CreateRuntimeScrollbar(Transform parent, string scrollbarName)
        {
            var scrollbarObject = DefaultControls.CreateScrollbar(new DefaultControls.Resources());
            scrollbarObject.name = scrollbarName;
            scrollbarObject.transform.SetParent(parent, false);
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.SetValueWithoutNotify(0f);
            return scrollbar;
        }

        private static Dropdown CreateRuntimeDropdown(Transform parent, string dropdownName)
        {
            var dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = dropdownName;
            dropdownObject.transform.SetParent(parent, false);
            var dropdown = dropdownObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            dropdown.options.Add(new Dropdown.OptionData("Idle"));
            dropdown.options.Add(new Dropdown.OptionData("Live"));
            dropdown.options.Add(new Dropdown.OptionData("Bypass"));
            dropdown.SetValueWithoutNotify(0);
            dropdown.RefreshShownValue();
            return dropdown;
        }

        private static InputField CreateRuntimeInputField(Transform parent, string inputName)
        {
            var inputObject = DefaultControls.CreateInputField(new DefaultControls.Resources());
            inputObject.name = inputName;
            inputObject.transform.SetParent(parent, false);
            var input = inputObject.GetComponent<InputField>();
            input.SetTextWithoutNotify(string.Empty);
            return input;
        }

        private static Button CreateRuntimeButton(Transform parent, Text indicatorText, string buttonName)
        {
            var buttonObject = new GameObject(buttonName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => indicatorText.text = "CLICKED");
            return button;
        }

        private static Text CreateRuntimeText(Transform parent, string objectName, string initialText)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = initialText;
            return text;
        }

        private static CanvasUiSyncBindingId AddBindingId(GameObject target, string bindingId)
        {
            var component = target.AddComponent<CanvasUiSyncBindingId>();
            component.BindingId = bindingId;
            return component;
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

        private static void EnsureEventSystemExists()
        {
            if (Object.FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(eventSystemObject);
        }

        private static void ClickDropdown(Dropdown dropdown)
        {
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            var eventData = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute<IPointerClickHandler>(dropdown.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static void ClickDropdownBlocker(Dropdown dropdown)
        {
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            var blockerField = typeof(Dropdown).GetField("m_Blocker", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(blockerField, Is.Not.Null);
            var blocker = blockerField.GetValue(dropdown) as GameObject;
            Assert.That(blocker, Is.Not.Null);
            var eventData = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute<IPointerClickHandler>(blocker, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static void SelectDropdownItem(Dropdown dropdown, int optionIndex)
        {
            var eventSystem = Object.FindObjectOfType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            var dropdownField = typeof(Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(dropdownField, Is.Not.Null);
            var dropdownList = dropdownField.GetValue(dropdown) as GameObject;
            Assert.That(dropdownList, Is.Not.Null);
            var toggle = GetDropdownItemToggles(dropdown)[optionIndex];
            Assert.That(toggle, Is.Not.Null);
            var eventData = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute<IPointerClickHandler>(toggle.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static Toggle[] GetDropdownItemToggles(Dropdown dropdown)
        {
            var dropdownField = typeof(Dropdown).GetField("m_Dropdown", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(dropdownField, Is.Not.Null);
            var dropdownList = dropdownField.GetValue(dropdown) as GameObject;
            Assert.That(dropdownList, Is.Not.Null);
            return dropdownList.GetComponentsInChildren<Toggle>(true).Skip(1).Take(dropdown.options.Count).ToArray();
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
