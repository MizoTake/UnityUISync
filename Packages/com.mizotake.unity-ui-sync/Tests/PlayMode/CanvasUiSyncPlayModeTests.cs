using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync.Tests.PlayMode
{
    public sealed class CanvasUiSyncPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            foreach (var canvas in Object.FindObjectsOfType<CanvasUiSync>(true))
            {
                Object.Destroy(canvas.gameObject);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Toggle_SyncsBothDirections_WithoutOscTransport()
        {
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", 9000, 9001);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", 9001, 9000);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return null;
            Assert.That(peerA.toggle.isOn, Is.True);
            Assert.That(peerB.toggle.isOn, Is.True);

            peerB.toggle.isOn = false;
            yield return null;
            Assert.That(peerA.toggle.isOn, Is.False);
            Assert.That(peerB.toggle.isOn, Is.False);
        }

        [UnityTest]
        public IEnumerator Toggle_LocalOperation_DoesNotOscillateBackAfterSeveralFrames()
        {
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", 9000, 9001);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", 9001, 9000);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return null;
            yield return null;
            yield return null;

            Assert.That(peerA.toggle.isOn, Is.True);
            Assert.That(peerB.toggle.isOn, Is.True);
        }

        [UnityTest]
        public IEnumerator Toggle_RemoteSync_UpdatesSamplePresenterText()
        {
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", 9000, 9001);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", 9001, 9000, true);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return null;
            yield return null;

            Assert.That(peerB.toggle.isOn, Is.True);
            Assert.That(peerB.presenter, Is.Not.Null);
            Assert.That(peerB.presenter.powerValueText.text, Is.EqualTo("ON"));
        }

        [UnityTest]
        public IEnumerator Toggle_RemoteSync_UpdatesSamplePresenterCheckmark()
        {
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", 9000, 9001);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", 9001, 9000, true);
            yield return null;
            yield return null;

            peerA.toggle.isOn = true;
            yield return null;
            yield return null;

            Assert.That(peerB.toggle.isOn, Is.True);
            Assert.That(peerB.presenter, Is.Not.Null);
            Assert.That(peerB.presenter.powerToggleCheckmark, Is.Not.Null);
            Assert.That(peerB.presenter.powerToggleCheckmark.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Toggle_RepeatedBidirectionalSync_RemainsConsistentOverManyFrames()
        {
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", 9000, 9001);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", 9001, 9000);
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

            yield return null;
            Assert.That(peerA.toggle.isOn, Is.EqualTo(expected));
            Assert.That(peerB.toggle.isOn, Is.EqualTo(expected));
        }

        [UnityTest]
        public IEnumerator Toggle_RepeatedEnableDisableWithRescanOnEnable_KeepsSyncStable()
        {
            var peerA = CreatePeer("PeerACanvas", "PeerA", "PeerB", 9000, 9001);
            var peerB = CreatePeer("PeerBCanvas", "PeerB", "PeerA", 9001, 9000);
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
            profile.minimumCommitBroadcastIntervalSeconds = 0f;
            profile.allowedPeers.Add(remoteNodeId);
            profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = remoteNodeId, ipAddress = "127.0.0.1", port = remotePort, enabled = true });
            SetPrivateField(sync, "profile", profile);
            SetPrivateField(sync, "canvasIdOverride", "DemoCanvas");
            canvasObject.SetActive(true);
            return (sync, toggle, presenter);
        }

        private static void SetPrivateField(object instance, string fieldName, object value)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"field {fieldName} was not found");
            field.SetValue(instance, value);
        }
    }
}
