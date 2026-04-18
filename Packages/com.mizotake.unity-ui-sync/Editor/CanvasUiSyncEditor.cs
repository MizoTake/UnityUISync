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
        private SerializedProperty excludedComponentsProperty;

        private void OnEnable()
        {
            profileProperty = serializedObject.FindProperty("profile");
            canvasIdOverrideProperty = serializedObject.FindProperty("canvasIdOverride");
            rescanOnEnableProperty = serializedObject.FindProperty("rescanOnEnable");
            syncEnabledProperty = serializedObject.FindProperty("syncEnabled");
            excludedComponentsProperty = serializedObject.FindProperty("excludedComponents");
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
            EditorGUILayout.PropertyField(excludedComponentsProperty, new GUIContent("同期除外 UI", "ここに登録した UI コンポーネントは同期対象として走査しません。"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
