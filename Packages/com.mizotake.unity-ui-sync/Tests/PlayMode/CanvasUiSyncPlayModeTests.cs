using System.Collections;
using System.Collections.Generic;
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

            foreach (var eventSystem in Object.FindObjectsOfType<EventSystem>(true))
            {
                Object.Destroy(eventSystem.gameObject);
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
        public IEnumerator ExcludedRuntimeToggle_DoesNotSync_WhileIncludedRuntimeToggleStillSyncs()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var peerAIncludedToggle = CreateRuntimeToggle(peerA.sync.transform, "IncludedToggle");
            var peerBIncludedToggle = CreateRuntimeToggle(peerB.sync.transform, "IncludedToggle");
            var peerAExcludedToggle = CreateRuntimeToggle(peerA.sync.transform, "ExcludedToggle");
            var peerBExcludedToggle = CreateRuntimeToggle(peerB.sync.transform, "ExcludedToggle");
            AssignExcludedComponents(peerA.sync, peerAExcludedToggle);
            AssignExcludedComponents(peerB.sync, peerBExcludedToggle);
            InvokePrivate(peerA.sync, "RefreshBindingsIfHierarchyChanged", true);
            InvokePrivate(peerB.sync, "RefreshBindingsIfHierarchyChanged", true);
            yield return null;

            Assert.That(HasBinding(peerA.sync, "DemoCanvas/IncludedToggle:Toggle"), Is.True);
            Assert.That(HasBinding(peerB.sync, "DemoCanvas/IncludedToggle:Toggle"), Is.True);
            Assert.That(HasBinding(peerA.sync, "DemoCanvas/ExcludedToggle:Toggle"), Is.False);
            Assert.That(HasBinding(peerB.sync, "DemoCanvas/ExcludedToggle:Toggle"), Is.False);

            peerAIncludedToggle.isOn = true;
            yield return WaitUntil(() => peerBIncludedToggle.isOn, 60);

            Assert.That(peerBIncludedToggle.isOn, Is.True);

            peerAExcludedToggle.isOn = true;
            yield return WaitFrames(10);

            Assert.That(peerBExcludedToggle.isOn, Is.False);
            Assert.That(HasBinding(peerA.sync, "DemoCanvas/ExcludedToggle:Toggle"), Is.False);
            Assert.That(HasBinding(peerB.sync, "DemoCanvas/ExcludedToggle:Toggle"), Is.False);
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
                expected = !expected;
                if (index % 2 == 0)
                {
                    peerA.toggle.isOn = expected;
                }
                else
                {
                    peerB.toggle.isOn = expected;
                }

                yield return WaitUntil(() => peerA.toggle.isOn == expected && peerB.toggle.isOn == expected, 30);
            }

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
        public IEnumerator Toggle_DisableSyncOnRemotePeer_ReenableSync_CatchesUpAndRestoresBidirectionalSync()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            peerB.sync.DisableSync();
            Assert.That(peerB.sync.SyncEnabled, Is.False);

            peerA.toggle.isOn = true;
            yield return WaitFrames(10);
            Assert.That(peerB.toggle.isOn, Is.False);

            peerB.sync.EnableSync();
            Assert.That(peerB.sync.SyncEnabled, Is.True);
            yield return WaitUntil(() => peerB.toggle.isOn, 120);

            Assert.That(peerB.toggle.isOn, Is.True);

            peerB.toggle.isOn = false;
            yield return WaitUntil(() => !peerA.toggle.isOn, 60);

            Assert.That(peerA.toggle.isOn, Is.False);
        }

        [UnityTest]
        public IEnumerator Toggle_SetSyncEnabledOnBothPeers_AfterPause_RestoresBidirectionalSync()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            peerA.sync.SetSyncEnabled(false);
            peerB.sync.SetSyncEnabled(false);
            Assert.That(peerA.sync.SyncEnabled, Is.False);
            Assert.That(peerB.sync.SyncEnabled, Is.False);

            yield return WaitFrames(10);

            peerA.sync.SetSyncEnabled(true);
            peerB.sync.SetSyncEnabled(true);
            Assert.That(peerA.sync.SyncEnabled, Is.True);
            Assert.That(peerB.sync.SyncEnabled, Is.True);

            peerA.toggle.isOn = true;
            yield return WaitUntil(() => peerB.toggle.isOn, 120);

            Assert.That(peerB.toggle.isOn, Is.True);

            peerB.toggle.isOn = false;
            yield return WaitUntil(() => !peerA.toggle.isOn, 60);

            Assert.That(peerA.toggle.isOn, Is.False);
        }

        [UnityTest]
        public IEnumerator Toggle_RemotePeerReenabledAfterNodeTimeout_RejoinsAndCatchesUp()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            ConfigureFastReconnectProfile(peerA.sync);
            ConfigureFastReconnectProfile(peerB.sync);
            yield return WaitUntil(() => GetNodeCount(peerA.sync) == 1 && GetNodeCount(peerB.sync) == 1, 120);

            peerB.sync.DisableSync();
            yield return WaitUntil(() => GetNodeCount(peerA.sync) == 0, 120);

            Assert.That(GetNodeCount(peerA.sync), Is.EqualTo(0));

            peerA.toggle.isOn = true;
            yield return WaitFrames(10);
            Assert.That(peerB.toggle.isOn, Is.False);

            peerB.sync.EnableSync();
            yield return WaitUntil(() => GetNodeCount(peerA.sync) == 1 && peerB.toggle.isOn, 180);

            Assert.That(GetNodeCount(peerA.sync), Is.EqualTo(1));
            Assert.That(peerB.toggle.isOn, Is.True);
        }

        [UnityTest]
        public IEnumerator Toggle_RemotePeerRepeatedSyncOffOn_KeepsReconnectingToLatestState()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            yield return null;
            yield return null;

            var expected = false;
            for (var index = 0; index < 4; index++)
            {
                peerB.sync.SetSyncEnabled(false);
                expected = !expected;
                peerA.toggle.isOn = expected;
                yield return WaitFrames(10);

                Assert.That(peerB.toggle.isOn, Is.Not.EqualTo(expected));

                peerB.sync.SetSyncEnabled(true);
                yield return WaitUntil(() => peerB.toggle.isOn == expected, 120);

                Assert.That(peerB.toggle.isOn, Is.EqualTo(expected));
            }

            peerB.toggle.isOn = !expected;
            yield return WaitUntil(() => peerA.toggle.isOn == !expected, 60);

            Assert.That(peerA.toggle.isOn, Is.EqualTo(!expected));
        }

        [UnityTest]
        public IEnumerator SharedRuntimeControls_RemoteSyncOff_StatefulControlsCatchUpWithinBudget_ButtonDoesNotReplay()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            ConfigureLowChatterProfile(peerA.sync);
            ConfigureLowChatterProfile(peerB.sync);
            yield return null;
            yield return null;

            var controls = CreateSharedRuntimeControls(peerA.sync, peerB.sync);
            yield return WaitUntilSharedRuntimeBindingsRegistered(peerA.sync, peerB.sync);

            Assert.That(HasBinding(peerA.sync, "DemoCanvas/SharedToggle:Toggle"), Is.True);
            Assert.That(HasBinding(peerB.sync, "DemoCanvas/SharedToggle:Toggle"), Is.True);
            Assert.That(HasBinding(peerA.sync, "DemoCanvas/SharedInputField:InputField"), Is.True);
            Assert.That(HasBinding(peerB.sync, "DemoCanvas/SharedInputField:InputField"), Is.True);

            peerB.sync.DisableSync();
            Assert.That(peerB.sync.SyncEnabled, Is.False);

            controls.PeerAToggle.isOn = true;
            controls.PeerASlider.value = 0.84f;
            controls.PeerAScrollbar.value = 0.41f;
            controls.PeerADropdown.value = 1;
            controls.PeerADropdown.value = 2;
            controls.PeerAInput.text = "Offline A";
            controls.PeerAInput.onEndEdit.Invoke("Offline A");
            controls.PeerAInput.text = "Offline Final";
            controls.PeerAInput.onEndEdit.Invoke("Offline Final");
            controls.PeerAButton.onClick.Invoke();
            yield return WaitFrames(10);

            Assert.That(controls.PeerBToggle.isOn, Is.False);
            Assert.That(controls.PeerBSlider.value, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(controls.PeerBScrollbar.value, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(controls.PeerBDropdown.value, Is.EqualTo(0));
            Assert.That(controls.PeerBInput.text, Is.EqualTo(string.Empty));
            Assert.That(controls.PeerAIndicator.text, Is.EqualTo("CLICKED"));
            Assert.That(controls.PeerBIndicator.text, Is.EqualTo("READY"));

            ResetMessageCounters(peerA.sync);
            ResetMessageCounters(peerB.sync);
            peerB.sync.EnableSync();
            Assert.That(peerB.sync.SyncEnabled, Is.True);

            var catchUpFrameCount = 0;
            while (catchUpFrameCount < 60 && !(controls.PeerBToggle.isOn && Mathf.Abs(controls.PeerBSlider.value - 0.84f) < 0.0001f && Mathf.Abs(controls.PeerBScrollbar.value - 0.41f) < 0.0001f && controls.PeerBDropdown.value == 2 && controls.PeerBInput.text == "Offline Final"))
            {
                catchUpFrameCount++;
                yield return null;
            }

            Assert.That(controls.PeerBToggle.isOn, Is.True);
            Assert.That(controls.PeerBSlider.value, Is.EqualTo(0.84f).Within(0.0001f));
            Assert.That(controls.PeerBScrollbar.value, Is.EqualTo(0.41f).Within(0.0001f));
            Assert.That(controls.PeerBDropdown.value, Is.EqualTo(2));
            Assert.That(controls.PeerBInput.text, Is.EqualTo("Offline Final"));
            Assert.That(controls.PeerBIndicator.text, Is.EqualTo("READY"));
            Assert.That(catchUpFrameCount, Is.LessThan(60));
            Assert.That(GetSentMessageCount(peerA.sync), Is.LessThanOrEqualTo(20));
            Assert.That(GetReceivedMessageCount(peerB.sync), Is.LessThanOrEqualTo(20));

            controls.PeerBButton.onClick.Invoke();
            yield return WaitUntil(() => controls.PeerAIndicator.text == "CLICKED" && controls.PeerBIndicator.text == "CLICKED", 60);

            Assert.That(controls.PeerAIndicator.text, Is.EqualTo("CLICKED"));
            Assert.That(controls.PeerBIndicator.text, Is.EqualTo("CLICKED"));

            controls.PeerBToggle.isOn = false;
            yield return WaitUntil(() => !controls.PeerAToggle.isOn, 60);

            Assert.That(controls.PeerAToggle.isOn, Is.False);
        }

        [UnityTest]
        public IEnumerator SharedRuntimeControls_LocalSyncOff_RemoteSnapshotOverridesUnsyncedLocalEditsOnReenable()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            ConfigureLowChatterProfile(peerA.sync);
            ConfigureLowChatterProfile(peerB.sync);
            yield return null;
            yield return null;

            var controls = CreateSharedRuntimeControls(peerA.sync, peerB.sync);
            yield return WaitUntilSharedRuntimeBindingsRegistered(peerA.sync, peerB.sync);

            peerA.sync.DisableSync();
            Assert.That(peerA.sync.SyncEnabled, Is.False);

            controls.PeerAToggle.isOn = true;
            controls.PeerASlider.value = 0.82f;
            controls.PeerADropdown.value = 2;
            controls.PeerAInput.text = "LocalOnly";
            controls.PeerAInput.onEndEdit.Invoke("LocalOnly");
            controls.PeerAButton.onClick.Invoke();
            yield return WaitFrames(10);

            Assert.That(controls.PeerBToggle.isOn, Is.False);
            Assert.That(controls.PeerBSlider.value, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(controls.PeerBDropdown.value, Is.EqualTo(0));
            Assert.That(controls.PeerBInput.text, Is.EqualTo(string.Empty));
            Assert.That(controls.PeerBIndicator.text, Is.EqualTo("READY"));

            controls.PeerBToggle.isOn = true;
            yield return WaitUntil(() => controls.PeerBToggle.isOn, 30);
            controls.PeerBSlider.value = 0.27f;
            controls.PeerBToggle.isOn = false;
            controls.PeerBDropdown.value = 1;
            controls.PeerBInput.text = "RemoteState";
            controls.PeerBInput.onEndEdit.Invoke("RemoteState");
            yield return WaitFrames(10);

            Assert.That(controls.PeerAToggle.isOn, Is.True);
            Assert.That(controls.PeerASlider.value, Is.EqualTo(0.82f).Within(0.0001f));
            Assert.That(controls.PeerADropdown.value, Is.EqualTo(2));
            Assert.That(controls.PeerAInput.text, Is.EqualTo("LocalOnly"));
            Assert.That(controls.PeerAIndicator.text, Is.EqualTo("CLICKED"));

            peerA.sync.EnableSync();
            Assert.That(peerA.sync.SyncEnabled, Is.True);
            yield return WaitUntil(() => !controls.PeerAToggle.isOn && Mathf.Abs(controls.PeerASlider.value - 0.27f) < 0.0001f && controls.PeerADropdown.value == 1 && controls.PeerAInput.text == "RemoteState", 60);

            Assert.That(controls.PeerAToggle.isOn, Is.False);
            Assert.That(controls.PeerASlider.value, Is.EqualTo(0.27f).Within(0.0001f));
            Assert.That(controls.PeerADropdown.value, Is.EqualTo(1));
            Assert.That(controls.PeerAInput.text, Is.EqualTo("RemoteState"));
            Assert.That(controls.PeerAIndicator.text, Is.EqualTo("CLICKED"));
            Assert.That(controls.PeerBIndicator.text, Is.EqualTo("READY"));

            controls.PeerAToggle.isOn = true;
            yield return WaitUntil(() => controls.PeerBToggle.isOn, 60);

            Assert.That(controls.PeerBToggle.isOn, Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeGeneratedInputField_OnValueChanged_BurstQueuesLatestPendingValueUntilFlush()
        {
            var ports = AllocatePortPair();
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", ports.peerAPort, ports.peerBPort);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", ports.peerBPort, ports.peerAPort);
            ConfigureLowChatterProfile(peerA.sync);
            ConfigureLowChatterProfile(peerB.sync);
            GetProfile(peerA.sync).stringSendMode = CanvasUiSyncStringSendMode.OnValueChanged;
            GetProfile(peerB.sync).stringSendMode = CanvasUiSyncStringSendMode.OnValueChanged;
            GetProfile(peerA.sync).minimumCommitBroadcastIntervalSeconds = 0.2f;
            GetProfile(peerB.sync).minimumCommitBroadcastIntervalSeconds = 0.2f;
            yield return null;
            yield return null;

            var peerARuntimeInput = CreateRuntimeInputField(peerA.sync.transform, "RuntimeInputBurst");
            var peerBRuntimeInput = CreateRuntimeInputField(peerB.sync.transform, "RuntimeInputBurst");
            yield return WaitUntil(() => HasBinding(peerA.sync, "DemoCanvas/RuntimeInputBurst:InputField") && HasBinding(peerB.sync, "DemoCanvas/RuntimeInputBurst:InputField"), 60);

            var localState = GetLocalState(peerA.sync, "DemoCanvas/RuntimeInputBurst:InputField");
            SetPublicProperty(localState, "LastBroadcastAt", Time.unscaledTime);
            ResetMessageCounters(peerA.sync);
            ResetMessageCounters(peerB.sync);

            peerARuntimeInput.SetTextWithoutNotify("A");
            peerARuntimeInput.onValueChanged.Invoke("A");
            peerARuntimeInput.SetTextWithoutNotify("AB");
            peerARuntimeInput.onValueChanged.Invoke("AB");
            peerARuntimeInput.SetTextWithoutNotify("ABCD");
            peerARuntimeInput.onValueChanged.Invoke("ABCD");

            Assert.That(peerBRuntimeInput.text, Is.EqualTo(string.Empty));
            Assert.That((bool)GetPublicProperty(localState, "HasPendingBroadcast"), Is.True);
            Assert.That((string)GetPublicProperty(localState, "PendingValue"), Is.EqualTo("ABCD"));
            Assert.That((float)GetPublicProperty(localState, "NextBroadcastAt"), Is.GreaterThan(Time.unscaledTime));
            yield return WaitUntil(() => peerBRuntimeInput.text == "ABCD", 60);

            Assert.That(peerBRuntimeInput.text, Is.EqualTo("ABCD"));
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

        private sealed class SharedRuntimeControls
        {
            public SharedRuntimeControls(Toggle peerAToggle, Toggle peerBToggle, Slider peerASlider, Slider peerBSlider, Scrollbar peerAScrollbar, Scrollbar peerBScrollbar, Dropdown peerADropdown, Dropdown peerBDropdown, InputField peerAInput, InputField peerBInput, Button peerAButton, Button peerBButton, Text peerAIndicator, Text peerBIndicator)
            {
                PeerAToggle = peerAToggle;
                PeerBToggle = peerBToggle;
                PeerASlider = peerASlider;
                PeerBSlider = peerBSlider;
                PeerAScrollbar = peerAScrollbar;
                PeerBScrollbar = peerBScrollbar;
                PeerADropdown = peerADropdown;
                PeerBDropdown = peerBDropdown;
                PeerAInput = peerAInput;
                PeerBInput = peerBInput;
                PeerAButton = peerAButton;
                PeerBButton = peerBButton;
                PeerAIndicator = peerAIndicator;
                PeerBIndicator = peerBIndicator;
            }

            public Toggle PeerAToggle { get; }
            public Toggle PeerBToggle { get; }
            public Slider PeerASlider { get; }
            public Slider PeerBSlider { get; }
            public Scrollbar PeerAScrollbar { get; }
            public Scrollbar PeerBScrollbar { get; }
            public Dropdown PeerADropdown { get; }
            public Dropdown PeerBDropdown { get; }
            public InputField PeerAInput { get; }
            public InputField PeerBInput { get; }
            public Button PeerAButton { get; }
            public Button PeerBButton { get; }
            public Text PeerAIndicator { get; }
            public Text PeerBIndicator { get; }
        }

        private static void ConfigureLowChatterProfile(CanvasUiSync sync)
        {
            var profile = GetProfile(sync);
            profile.helloIntervalSeconds = 10f;
            profile.snapshotRequestIntervalSeconds = 10f;
            profile.snapshotRetryCooldownSeconds = 10f;
        }

        private static SharedRuntimeControls CreateSharedRuntimeControls(CanvasUiSync peerASync, CanvasUiSync peerBSync)
        {
            var peerAContainer = CreateRuntimeContainer(peerASync.transform, "OperationsPanel");
            var peerBContainer = CreateRuntimeContainer(peerBSync.transform, "StatusPanel");
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
            return new SharedRuntimeControls(peerAToggle, peerBToggle, peerASlider, peerBSlider, peerAScrollbar, peerBScrollbar, peerADropdown, peerBDropdown, peerAInput, peerBInput, peerAButton, peerBButton, peerAIndicator, peerBIndicator);
        }

        private static IEnumerator WaitUntilSharedRuntimeBindingsRegistered(CanvasUiSync peerASync, CanvasUiSync peerBSync)
        {
            yield return WaitUntil(() => HasBinding(peerASync, "DemoCanvas/SharedToggle:Toggle") && HasBinding(peerBSync, "DemoCanvas/SharedToggle:Toggle") && HasBinding(peerASync, "DemoCanvas/SharedSlider:Slider") && HasBinding(peerBSync, "DemoCanvas/SharedSlider:Slider") && HasBinding(peerASync, "DemoCanvas/SharedScrollbar:Scrollbar") && HasBinding(peerBSync, "DemoCanvas/SharedScrollbar:Scrollbar") && HasBinding(peerASync, "DemoCanvas/SharedDropdown:Dropdown") && HasBinding(peerBSync, "DemoCanvas/SharedDropdown:Dropdown") && HasBinding(peerASync, "DemoCanvas/SharedInputField:InputField") && HasBinding(peerBSync, "DemoCanvas/SharedInputField:InputField") && HasBinding(peerASync, "DemoCanvas/SharedButton:Button") && HasBinding(peerBSync, "DemoCanvas/SharedButton:Button"), 60);
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

        private static void ConfigureFastReconnectProfile(CanvasUiSync sync)
        {
            var profile = GetProfile(sync);
            profile.helloIntervalSeconds = 0.02f;
            profile.nodeTimeoutSeconds = 0.05f;
            profile.snapshotRequestIntervalSeconds = 0.02f;
            profile.snapshotRequestRetryCount = 10;
            profile.snapshotRetryCooldownSeconds = 0.02f;
        }

        private static void AssignExcludedComponents(CanvasUiSync sync, params Component[] components)
        {
            SetPrivateField(sync, "excludedComponents", new List<Component>(components));
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"field {fieldName} was not found");
            field.SetValue(instance, value);
        }

        private static void InvokePrivate(object instance, string methodName, params object[] args)
        {
            var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"method {methodName} was not found");
            method.Invoke(instance, args);
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

        private static int GetNodeCount(CanvasUiSync sync)
        {
            return ((IDictionary)GetPrivateField(sync, "nodes")).Count;
        }

        private static bool HasBinding(CanvasUiSync sync, string syncId)
        {
            return ((IDictionary)GetPrivateField(sync, "bindings")).Contains(syncId);
        }

        private static object GetBinding(CanvasUiSync sync, string syncId)
        {
            return ((IDictionary)GetPrivateField(sync, "bindings"))[syncId];
        }

        private static object GetLocalState(CanvasUiSync sync, string syncId)
        {
            return ((IDictionary)GetPrivateField(sync, "localStates"))[syncId];
        }

        private static int GetSentMessageCount(CanvasUiSync sync)
        {
            return (int)GetPrivateField(sync, "sentMessageCount");
        }

        private static int GetReceivedMessageCount(CanvasUiSync sync)
        {
            return (int)GetPrivateField(sync, "receivedMessageCount");
        }

        private static void ResetMessageCounters(CanvasUiSync sync)
        {
            SetPrivateField(sync, "sentMessageCount", 0);
            SetPrivateField(sync, "receivedMessageCount", 0);
        }

        private static void SetPublicProperty(object instance, string propertyName, object value)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"property {propertyName} was not found");
            property.SetValue(instance, value);
        }

        private static object GetPublicProperty(object instance, string propertyName)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"property {propertyName} was not found");
            return property.GetValue(instance);
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

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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

        private static CanvasUiSyncProfile GetProfile(CanvasUiSync sync)
        {
            return (CanvasUiSyncProfile)GetPrivateField(sync, "profile");
        }

        private static CanvasUiSyncSamplePresenter FindPresenter(string canvasName)
        {
            return Object.FindObjectsOfType<CanvasUiSyncSamplePresenter>(true).FirstOrDefault(component => component.GetComponentsInParent<Canvas>(true).Any(canvas => canvas.name == canvasName));
        }
    }
}
