using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mizotake.UnityUiSync.Editor
{
    public static class CanvasUiSyncSampleBuilder
    {
        private const string AssetScenePath = "Assets/Scenes/UnityUiSyncSample.unity";
        private const string AssetSampleRootPath = "Assets/UnityUISyncSamples";
        private const string AssetProfileDirectoryPath = AssetSampleRootPath + "/Profiles";
        private const string AssetReadmePath = AssetSampleRootPath + "/README.txt";
        private const string PackageRootPath = "Packages/com.mizotake.unity-ui-sync";
        private const string PackageSampleRootPath = PackageRootPath + "/Samples~/Basic Setup";
        private const string PackageProfileDirectoryPath = PackageSampleRootPath + "/Profiles";
        private const string PackageSceneDirectoryPath = PackageSampleRootPath + "/Scenes";
        private const string PackageScenePath = PackageSceneDirectoryPath + "/UnityUiSyncSample.unity";
        private const string PackageReadmePath = PackageSampleRootPath + "/README.txt";
        private const string DemoCanvasId = "DemoCanvas";

        [InitializeOnLoadMethod]
        private static void AutoRebuildSampleAssets()
        {
            if (!File.Exists(AssetScenePath))
            {
                EditorApplication.delayCall += RebuildSampleAssets;
            }
        }

        [MenuItem("Tools/Unity UI Sync/Rebuild Sample Assets")]
        public static void RebuildSampleAssets()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory(AssetSampleRootPath);
            Directory.CreateDirectory(AssetProfileDirectoryPath);
            Directory.CreateDirectory(PackageSampleRootPath);
            Directory.CreateDirectory(PackageProfileDirectoryPath);
            Directory.CreateDirectory(PackageSceneDirectoryPath);
            BuildProfiles(AssetProfileDirectoryPath);
            BuildReadme(AssetReadmePath);
            BuildScene(AssetScenePath, AssetProfileDirectoryPath + "/PeerA.asset", AssetProfileDirectoryPath + "/PeerB.asset");
            MirrorAssetToPackageSample(AssetProfileDirectoryPath + "/PeerA.asset", PackageProfileDirectoryPath + "/PeerA.asset");
            MirrorAssetToPackageSample(AssetProfileDirectoryPath + "/PeerB.asset", PackageProfileDirectoryPath + "/PeerB.asset");
            MirrorAssetToPackageSample(AssetReadmePath, PackageReadmePath);
            MirrorAssetToPackageSample(AssetScenePath, PackageScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildProfiles(string profileDirectoryPath)
        {
            CreateOrUpdateProfile(profileDirectoryPath + "/PeerA.asset", profile =>
            {
                profile.profileName = "PeerA";
                profile.nodeId = "PeerA";
                profile.protocolVersion = 4;
                profile.enableOscTransport = false;
                profile.listenPort = 9000;
                profile.allowDynamicPeerJoin = false;
                profile.allowedPeers.Clear();
                profile.allowedPeers.Add("PeerB");
                profile.peerEndpoints.Clear();
                profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerB", ipAddress = "127.0.0.1", port = 9001, enabled = true });
                profile.verboseLog = true;
            });

            CreateOrUpdateProfile(profileDirectoryPath + "/PeerB.asset", profile =>
            {
                profile.profileName = "PeerB";
                profile.nodeId = "PeerB";
                profile.protocolVersion = 4;
                profile.enableOscTransport = false;
                profile.listenPort = 9001;
                profile.allowDynamicPeerJoin = false;
                profile.allowedPeers.Clear();
                profile.allowedPeers.Add("PeerA");
                profile.peerEndpoints.Clear();
                profile.peerEndpoints.Add(new CanvasUiSyncRemoteEndpoint { name = "PeerA", ipAddress = "127.0.0.1", port = 9000, enabled = true });
                profile.verboseLog = true;
            });
        }

        private static void CreateOrUpdateProfile(string assetPath, System.Action<CanvasUiSyncProfile> configure)
        {
            var profile = AssetDatabase.LoadAssetAtPath<CanvasUiSyncProfile>(assetPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<CanvasUiSyncProfile>();
                AssetDatabase.CreateAsset(profile, assetPath);
            }

            configure(profile);
            EditorUtility.SetDirty(profile);
        }

        private static void BuildReadme(string readmePath)
        {
            File.WriteAllText(readmePath, "1. 左右に 2 つのデモ画面が並びます。\n2. 各列は ラベル / コントロール / 状態表示 の 3 列で整理されています。\n3. すべてのデモ要素は 1 画面に収まるように配置してあります。\n4. Toggle はランプと ON/OFF、Slider と Scrollbar は数値、Dropdown と InputField は内容、Button は READY/TRIGGERED が反映されます。\n");
            AssetDatabase.ImportAsset(readmePath);
        }

        private static void BuildScene(string scenePath, string peerAProfileAssetPath, string peerBProfileAssetPath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateEventSystem();
            CreateDemoCanvas("PeerACanvas", peerAProfileAssetPath, "PeerA", "左の操作が右へ反映されます。", 0.04f, 0.48f);
            CreateDemoCanvas("PeerBCanvas", peerBProfileAssetPath, "PeerB", "右の操作が左へ反映されます。", 0.52f, 0.96f);
            EditorSceneManager.SaveScene(scene, scenePath);
            if (string.Equals(scenePath, AssetScenePath, System.StringComparison.Ordinal))
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            }
        }

        private static void CreateDemoCanvas(string canvasName, string profileAssetPath, string peerName, string hintText, float anchorMinX, float anchorMaxX)
        {
            var canvasObject = CreateCanvas(canvasName);
            var sync = canvasObject.AddComponent<CanvasUiSync>();
            AssignDefaultProfile(sync, profileAssetPath);
            AssignCanvasIdOverride(sync, DemoCanvasId);
            BuildUi(canvasObject.transform, peerName, hintText, anchorMinX, anchorMaxX);
        }

        private static void MirrorAssetToPackageSample(string sourcePath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                FileUtil.DeleteFileOrDirectory(destinationPath);
            }

            if (File.Exists(destinationPath + ".meta"))
            {
                FileUtil.DeleteFileOrDirectory(destinationPath + ".meta");
            }

            FileUtil.CopyFileOrDirectory(sourcePath, destinationPath);
            if (File.Exists(sourcePath + ".meta"))
            {
                FileUtil.CopyFileOrDirectory(sourcePath + ".meta", destinationPath + ".meta");
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.14f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static void CreateEventSystem()
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        private static GameObject CreateCanvas(string canvasName)
        {
            var canvasObject = new GameObject(canvasName);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvasObject;
        }

        private static void AssignDefaultProfile(CanvasUiSync sync, string assetPath)
        {
            var serializedObject = new SerializedObject(sync);
            serializedObject.FindProperty("profile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CanvasUiSyncProfile>(assetPath);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignCanvasIdOverride(CanvasUiSync sync, string canvasIdOverride)
        {
            var serializedObject = new SerializedObject(sync);
            serializedObject.FindProperty("canvasIdOverride").stringValue = canvasIdOverride;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildUi(Transform canvasTransform, string peerName, string hintText, float anchorMinX, float anchorMaxX)
        {
            var resources = new DefaultControls.Resources();
            var root = CreatePanel(canvasTransform, anchorMinX, anchorMaxX);
            var content = CreateContentRoot(root.transform);
            var presenter = root.AddComponent<Mizotake.UnityUiSync.CanvasUiSyncSamplePresenter>();
            var top = 0f;
            PositionContentBlock((RectTransform)CreateText(content, "HeaderText", "Unity UI Sync " + peerName, 28, FontStyle.Bold, Color.white, 40f).transform, ref top, 40f, 8f);
            PositionContentBlock((RectTransform)CreateText(content, "HintText", hintText, 16, FontStyle.Normal, new Color(0.82f, 0.86f, 0.92f), 28f).transform, ref top, 28f, 12f);

            presenter.powerToggle = CreateToggleRow(content, resources, "PowerToggle", "Power", "OFF", out presenter.powerLamp, out presenter.powerValueText, out presenter.powerToggleBackground, out presenter.powerToggleCheckmark);
            PositionContentBlock((RectTransform)presenter.powerToggle.transform.parent, ref top, 68f, 12f);
            presenter.masterSlider = CreateSliderRow(content, resources, "MasterSlider", "Master", "0.25", out presenter.sliderFill, out presenter.sliderValueText);
            PositionContentBlock((RectTransform)presenter.masterSlider.transform.parent, ref top, 68f, 12f);
            presenter.masterSlider.SetValueWithoutNotify(0.25f);
            presenter.intensityScrollbar = CreateScrollbarRow(content, resources, "IntensityScrollbar", "Intensity", "0.75", out presenter.scrollbarFill, out presenter.scrollbarValueText);
            PositionContentBlock((RectTransform)presenter.intensityScrollbar.transform.parent, ref top, 68f, 12f);
            presenter.intensityScrollbar.SetValueWithoutNotify(0.75f);
            presenter.modeDropdown = CreateDropdownRow(content, resources, "ModeDropdown", "Mode", "Live", out presenter.modeValueText);
            PositionContentBlock((RectTransform)presenter.modeDropdown.transform.parent, ref top, 68f, 12f);
            presenter.modeDropdown.options.Clear();
            presenter.modeDropdown.options.Add(new Dropdown.OptionData("Idle"));
            presenter.modeDropdown.options.Add(new Dropdown.OptionData("Live"));
            presenter.modeDropdown.options.Add(new Dropdown.OptionData("Bypass"));
            presenter.modeDropdown.SetValueWithoutNotify(1);
            presenter.modeDropdown.RefreshShownValue();
            presenter.operatorInput = CreateInputFieldRow(content, resources, "OperatorInput", "Operator", "Sample operator", out presenter.operatorValueText);
            PositionContentBlock((RectTransform)presenter.operatorInput.transform.parent, ref top, 68f, 12f);
            presenter.targetToggle = CreateToggleRow(content, resources, "TargetToggle", "Target", "IDLE", out presenter.targetLamp, out presenter.targetValueText, out presenter.targetToggleBackground, out presenter.targetToggleCheckmark);
            PositionContentBlock((RectTransform)presenter.targetToggle.transform.parent, ref top, 68f, 12f);
            presenter.targetToggle.SetIsOnWithoutNotify(false);
            presenter.syncButton = CreateButtonRow(content, resources, "SyncButton", "Sync Button", "READY", out presenter.buttonPulse, out presenter.buttonValueText);
            PositionContentBlock((RectTransform)presenter.syncButton.transform.parent, ref top, 68f, 0f);
            UnityEventTools.AddBoolPersistentListener(presenter.syncButton.onClick, presenter.targetToggle.SetIsOnWithoutNotify, true);
            content.sizeDelta = new Vector2(0f, top + 16f);
            presenter.Refresh();
        }

        private static GameObject CreatePanel(Transform parent, float anchorMinX, float anchorMaxX)
        {
            var panel = new GameObject("SyncPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(anchorMinX, 0.06f);
            rect.anchorMax = new Vector2(anchorMaxX, 0.94f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.17f, 0.96f);
            return panel;
        }

        private static RectTransform CreateContentRoot(Transform parent)
        {
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(parent, false);
            var contentRect = (RectTransform)contentObject.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(18f, -620f);
            contentRect.offsetMax = new Vector2(-18f, -18f);
            return contentRect;
        }

        private static void PositionContentBlock(RectTransform rect, ref float top, float height, float spacing)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, -(top + height));
            rect.offsetMax = new Vector2(0f, -top);
            top += height + spacing;
        }

        private static Text CreateText(Transform parent, string objectName, string text, int fontSize, FontStyle fontStyle, Color color, float height)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var layout = textObject.GetComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            var textComponent = textObject.GetComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.color = color;
            textComponent.text = text;
            textComponent.alignment = TextAnchor.MiddleLeft;
            return textComponent;
        }

        private static GameObject CreateRowRoot(Transform parent, string objectName)
        {
            var row = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = 68f;
            row.GetComponent<Image>().color = new Color(0.15f, 0.18f, 0.24f, 1f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        private static Toggle CreateToggleRow(Transform parent, DefaultControls.Resources resources, string controlName, string labelText, string initialValue, out Image lamp, out Text valueText, out Image toggleBackground, out Image toggleCheckmark)
        {
            var row = CreateRowRoot(parent, controlName + "Row");
            lamp = CreateBadge(row.transform, controlName + "Lamp", new Color(0.21f, 0.24f, 0.28f));
            CreateLabel(row.transform, controlName + "Label", labelText);
            var toggleObject = DefaultControls.CreateToggle(resources);
            toggleObject.name = controlName;
            toggleObject.transform.SetParent(row.transform, false);
            ConfigureControlWidth(toggleObject, 96f, 34f);
            toggleObject.GetComponentInChildren<Text>().text = string.Empty;
            var toggle = toggleObject.GetComponent<Toggle>();
            toggleBackground = toggle.targetGraphic as Image;
            toggleCheckmark = toggle.graphic as Image;
            ConfigureToggleVisual(toggleObject, toggleBackground, toggleCheckmark);
            toggle.SetIsOnWithoutNotify(false);
            valueText = CreateValueText(row.transform, controlName + "ValueText", initialValue);
            return toggle;
        }

        private static Slider CreateSliderRow(Transform parent, DefaultControls.Resources resources, string controlName, string labelText, string initialValue, out Image fillImage, out Text valueText)
        {
            var row = CreateRowRoot(parent, controlName + "Row");
            CreateLabel(row.transform, controlName + "Label", labelText);
            var sliderObject = DefaultControls.CreateSlider(resources);
            sliderObject.name = controlName;
            sliderObject.transform.SetParent(row.transform, false);
            ConfigureControlWidth(sliderObject, 164f, 24f);
            var slider = sliderObject.GetComponent<Slider>();
            fillImage = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
            valueText = CreateValueText(row.transform, controlName + "ValueText", initialValue);
            return slider;
        }

        private static Scrollbar CreateScrollbarRow(Transform parent, DefaultControls.Resources resources, string controlName, string labelText, string initialValue, out Image fillImage, out Text valueText)
        {
            var row = CreateRowRoot(parent, controlName + "Row");
            CreateLabel(row.transform, controlName + "Label", labelText);
            var scrollbarObject = DefaultControls.CreateScrollbar(resources);
            scrollbarObject.name = controlName;
            scrollbarObject.transform.SetParent(row.transform, false);
            ConfigureControlWidth(scrollbarObject, 164f, 24f);
            var scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            fillImage = scrollbar.handleRect != null ? scrollbar.handleRect.GetComponent<Image>() : null;
            valueText = CreateValueText(row.transform, controlName + "ValueText", initialValue);
            return scrollbar;
        }

        private static Dropdown CreateDropdownRow(Transform parent, DefaultControls.Resources resources, string controlName, string labelText, string initialValue, out Text valueText)
        {
            var row = CreateRowRoot(parent, controlName + "Row");
            CreateLabel(row.transform, controlName + "Label", labelText);
            var dropdownObject = DefaultControls.CreateDropdown(resources);
            dropdownObject.name = controlName;
            dropdownObject.transform.SetParent(row.transform, false);
            ConfigureControlWidth(dropdownObject, 164f, 34f);
            valueText = CreateValueText(row.transform, controlName + "ValueText", initialValue);
            return dropdownObject.GetComponent<Dropdown>();
        }

        private static InputField CreateInputFieldRow(Transform parent, DefaultControls.Resources resources, string controlName, string labelText, string initialValue, out Text valueText)
        {
            var row = CreateRowRoot(parent, controlName + "Row");
            CreateLabel(row.transform, controlName + "Label", labelText);
            var inputObject = DefaultControls.CreateInputField(resources);
            inputObject.name = controlName;
            inputObject.transform.SetParent(row.transform, false);
            ConfigureControlWidth(inputObject, 164f, 34f);
            var input = inputObject.GetComponent<InputField>();
            input.text = initialValue;
            input.textComponent.text = initialValue;
            input.placeholder.GetComponent<Text>().text = "Operator name";
            valueText = CreateValueText(row.transform, controlName + "ValueText", initialValue);
            return input;
        }

        private static Button CreateButtonRow(Transform parent, DefaultControls.Resources resources, string controlName, string labelText, string initialValue, out Image pulseImage, out Text valueText)
        {
            var row = CreateRowRoot(parent, controlName + "Row");
            pulseImage = CreateBadge(row.transform, controlName + "Pulse", new Color(0.33f, 0.38f, 0.46f));
            CreateLabel(row.transform, controlName + "Label", labelText);
            var buttonObject = DefaultControls.CreateButton(resources);
            buttonObject.name = controlName;
            buttonObject.transform.SetParent(row.transform, false);
            ConfigureControlWidth(buttonObject, 96f, 36f);
            buttonObject.GetComponentInChildren<Text>().text = "Trigger";
            valueText = CreateValueText(row.transform, controlName + "ValueText", initialValue);
            return buttonObject.GetComponent<Button>();
        }

        private static Text CreateLabel(Transform parent, string objectName, string text)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var layout = labelObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 72f;
            layout.minWidth = 72f;
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.text = text;
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        private static Text CreateValueText(Transform parent, string objectName, string text)
        {
            var valueObject = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            valueObject.transform.SetParent(parent, false);
            var layout = valueObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 72f;
            layout.minWidth = 72f;
            var value = valueObject.GetComponent<Text>();
            value.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            value.fontSize = 15;
            value.fontStyle = FontStyle.Bold;
            value.color = new Color(0.95f, 0.95f, 0.95f);
            value.text = text;
            value.alignment = TextAnchor.MiddleRight;
            return value;
        }

        private static Image CreateBadge(Transform parent, string objectName, Color color)
        {
            var badgeObject = new GameObject(objectName, typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            badgeObject.transform.SetParent(parent, false);
            var layout = badgeObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 18f;
            layout.preferredHeight = 18f;
            layout.minWidth = 18f;
            layout.minHeight = 18f;
            var image = badgeObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void ConfigureControlWidth(GameObject controlObject, float width, float height)
        {
            var layout = controlObject.GetComponent<LayoutElement>() ?? controlObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;
        }

        private static void ConfigureToggleVisual(GameObject toggleObject, Image background, Image checkmark)
        {
            var backgroundRect = background != null ? background.rectTransform : null;
            if (backgroundRect != null)
            {
                backgroundRect.anchorMin = new Vector2(0f, 0.5f);
                backgroundRect.anchorMax = new Vector2(0f, 0.5f);
                backgroundRect.pivot = new Vector2(0f, 0.5f);
                backgroundRect.sizeDelta = new Vector2(28f, 28f);
                backgroundRect.anchoredPosition = new Vector2(0f, 0f);
                background.color = new Color(0.17f, 0.2f, 0.24f, 1f);
            }

            var checkRect = checkmark != null ? checkmark.rectTransform : null;
            if (checkRect != null)
            {
                checkRect.anchorMin = new Vector2(0.5f, 0.5f);
                checkRect.anchorMax = new Vector2(0.5f, 0.5f);
                checkRect.pivot = new Vector2(0.5f, 0.5f);
                checkRect.sizeDelta = new Vector2(18f, 18f);
                checkRect.anchoredPosition = Vector2.zero;
                checkmark.color = new Color(1f, 1f, 1f, 0f);
                checkmark.enabled = false;
            }

            var text = toggleObject.GetComponentInChildren<Text>();
            if (text != null)
            {
                Object.DestroyImmediate(text.gameObject);
            }
        }
    }
}
