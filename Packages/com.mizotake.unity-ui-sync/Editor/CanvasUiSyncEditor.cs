using UnityEditor;
using UnityEngine;

namespace Mizotake.UnityUiSync.Editor
{
    [CustomEditor(typeof(CanvasUiSync))]
    public sealed class CanvasUiSyncEditor : UnityEditor.Editor
    {
        private SerializedProperty profileProperty;
        private SerializedProperty canvasIdOverrideProperty;
        private SerializedProperty rescanOnEnableProperty;
        private SerializedProperty syncEnabledProperty;
        private SerializedProperty excludedUiProperty;
        private SerializedProperty legacyExcludedComponentsProperty;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
            canvasIdOverrideProperty = serializedObject.FindProperty("canvasIdOverride");
            rescanOnEnableProperty = serializedObject.FindProperty("rescanOnEnable");
            syncEnabledProperty = serializedObject.FindProperty("syncEnabled");
            excludedUiProperty = serializedObject.FindProperty("excludedUi");
            legacyExcludedComponentsProperty = serializedObject.FindProperty("excludedComponents");
            if (legacyExcludedComponentsProperty.arraySize > 0)
            {
                RefreshExclusions("同期除外 UI を移行", true);
                serializedObject.Update();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox("CanvasUiSync コンポーネントでは、シーン固有の補助設定だけを扱います。P2P 同期の接続先や競合ルールは Profile アセットで編集してください。", MessageType.Info);

            EditorGUILayout.LabelField("必須設定", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(profileProperty, new GUIContent("プロファイル", "共有する通信設定アセットです。"));

            var profile = profileProperty.objectReferenceValue as CanvasUiSyncProfile;
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Profile が未設定です。Create メニューから CanvasUiSyncProfile を作成して割り当ててください。", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("現在の Profile: " + profile.profileName + " / nodeId=" + profile.nodeId, MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Profile を選択"))
                    {
                        Selection.activeObject = profile;
                        EditorGUIUtility.PingObject(profile);
                    }

                    if (GUILayout.Button("サンプルを再生成"))
                    {
                        CanvasUiSyncSampleBuilder.RebuildSampleAssets();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("シーン固有の補助設定", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("同じ Profile を使っていても、Scene ごとに上書きしたい補助設定だけをここで持ちます。", MessageType.None);
            EditorGUILayout.PropertyField(canvasIdOverrideProperty, new GUIContent("Canvas ID 上書き", "未指定なら GameObject 名を使います。複数 Canvas を区別したいときだけ設定します。"));
            EditorGUILayout.PropertyField(rescanOnEnableProperty, new GUIContent("Enable 時に再スキャン", "実行中に生成された Canvas 配下 UI は自動再スキャンされます。これは Enable/Disable をまたぐ再構築に備える補助設定です。"));
            EditorGUILayout.PropertyField(syncEnabledProperty, new GUIContent("同期を有効化", "off の間は送受信と定期同期を止めます。API からも切り替えられます。"));
            EditorGUILayout.HelpBox("同期除外 UI は CanvasUiSyncBindingId を優先し、未設定なら Canvas からの階層パスで識別します。同期 UI Component を選ぶとその種別を、RectTransform などを選ぶと同じ GameObject 上の全同期 UI を除外します。設定を解除するときはリスト要素を削除してください。", MessageType.None);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(excludedUiProperty, new GUIContent("同期除外 UI", "対象 UI と同じ GameObject 上の Component を登録します。Binding ID または階層パスへ変換して照合します。"), true);
            var exclusionsChanged = EditorGUI.EndChangeCheck();
            DrawExclusionDiagnostics();

            var undoGroup = -1;
            if (exclusionsChanged)
            {
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("同期除外 UI を変更");
                Undo.RecordObjects(targets, "同期除外 UI を変更");
            }

            var applied = serializedObject.ApplyModifiedProperties();
            if (exclusionsChanged && applied)
            {
                RefreshExclusions("同期除外 UI を変更", false);
            }

            if (undoGroup >= 0)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private void RefreshExclusions(string undoName, bool recordUndo)
        {
            foreach (var targetObject in targets)
            {
                if (targetObject is not CanvasUiSync sync)
                {
                    continue;
                }

                if (recordUndo)
                {
                    Undo.RecordObject(sync, undoName);
                }

                sync.RefreshExclusions();
                PrefabUtility.RecordPrefabInstancePropertyModifications(sync);
                EditorUtility.SetDirty(sync);
            }
        }

        private void DrawExclusionDiagnostics()
        {
            var owner = serializedObject.isEditingMultipleObjects ? null : serializedObject.targetObject as CanvasUiSync;
            for (var index = 0; index < excludedUiProperty.arraySize; index++)
            {
                var exclusionProperty = excludedUiProperty.GetArrayElementAtIndex(index);
                var targetProperty = exclusionProperty.FindPropertyRelative("target");
                var bindingId = exclusionProperty.FindPropertyRelative("bindingId").stringValue;
                var hierarchyPath = exclusionProperty.FindPropertyRelative("hierarchyPath").stringValue;
                var componentType = exclusionProperty.FindPropertyRelative("componentType").stringValue;
                var targetComponent = targetProperty.objectReferenceValue as Component;
                if (owner != null && targetComponent != null && targetComponent.transform != owner.transform && !targetComponent.transform.IsChildOf(owner.transform))
                {
                    EditorGUILayout.HelpBox("要素 " + index + " はこの Canvas の配下ではないため、除外キーを更新できません。", MessageType.Warning);
                }

                var locatorKind = !string.IsNullOrWhiteSpace(bindingId) ? "Binding ID" : "階層パス";
                var locator = !string.IsNullOrWhiteSpace(bindingId) ? bindingId : hierarchyPath;
                if (!string.IsNullOrEmpty(locator))
                {
                    EditorGUILayout.LabelField("要素 " + index + ": " + locatorKind + "=" + locator + " / UI種別=" + (string.IsNullOrEmpty(componentType) ? "すべて" : componentType), EditorStyles.miniLabel);
                }
            }
        }
    }
}
