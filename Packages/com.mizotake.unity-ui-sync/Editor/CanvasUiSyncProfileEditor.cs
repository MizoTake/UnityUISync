using UnityEditor;
using UnityEngine;

namespace Mizotake.UnityUiSync.Editor
{
    [CustomEditor(typeof(CanvasUiSyncProfile))]
    public sealed class CanvasUiSyncProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty profileNameProperty;
        private SerializedProperty nodeIdProperty;
        private SerializedProperty protocolVersionProperty;
        private SerializedProperty enableOscTransportProperty;
        private SerializedProperty listenPortProperty;
        private SerializedProperty allowDynamicPeerJoinProperty;
        private SerializedProperty allowedPeersProperty;
        private SerializedProperty peerEndpointsProperty;
        private SerializedProperty helloIntervalSecondsProperty;
        private SerializedProperty nodeTimeoutSecondsProperty;
        private SerializedProperty snapshotRequestIntervalSecondsProperty;
        private SerializedProperty snapshotRequestRetryCountProperty;
        private SerializedProperty snapshotRetryCooldownSecondsProperty;
        private SerializedProperty snapshotStateTimeoutSecondsProperty;
        private SerializedProperty periodicFullResyncIntervalSecondsProperty;
        private SerializedProperty sliderEpsilonProperty;
        private SerializedProperty minimumProposeIntervalSecondsProperty;
        private SerializedProperty minimumCommitBroadcastIntervalSecondsProperty;
        private SerializedProperty stringSendModeProperty;
        private SerializedProperty enableDebugLogProperty;
        private SerializedProperty verboseLogProperty;
        private SerializedProperty logUnknownSyncIdProperty;
        private SerializedProperty logTypeMismatchProperty;
        private SerializedProperty logDuplicateSyncIdProperty;
        private SerializedProperty logRegistryHashMismatchProperty;
        private SerializedProperty enableStatisticsLogProperty;
        private SerializedProperty statisticsLogIntervalSecondsProperty;

        private void OnEnable()
        {
            profileNameProperty = serializedObject.FindProperty("profileName");
            nodeIdProperty = serializedObject.FindProperty("nodeId");
            protocolVersionProperty = serializedObject.FindProperty("protocolVersion");
            enableOscTransportProperty = serializedObject.FindProperty("enableOscTransport");
            listenPortProperty = serializedObject.FindProperty("listenPort");
            allowDynamicPeerJoinProperty = serializedObject.FindProperty("allowDynamicPeerJoin");
            allowedPeersProperty = serializedObject.FindProperty("allowedPeers");
            peerEndpointsProperty = serializedObject.FindProperty("peerEndpoints");
            helloIntervalSecondsProperty = serializedObject.FindProperty("helloIntervalSeconds");
            nodeTimeoutSecondsProperty = serializedObject.FindProperty("nodeTimeoutSeconds");
            snapshotRequestIntervalSecondsProperty = serializedObject.FindProperty("snapshotRequestIntervalSeconds");
            snapshotRequestRetryCountProperty = serializedObject.FindProperty("snapshotRequestRetryCount");
            snapshotRetryCooldownSecondsProperty = serializedObject.FindProperty("snapshotRetryCooldownSeconds");
            snapshotStateTimeoutSecondsProperty = serializedObject.FindProperty("snapshotStateTimeoutSeconds");
            periodicFullResyncIntervalSecondsProperty = serializedObject.FindProperty("periodicFullResyncIntervalSeconds");
            sliderEpsilonProperty = serializedObject.FindProperty("sliderEpsilon");
            minimumProposeIntervalSecondsProperty = serializedObject.FindProperty("minimumProposeIntervalSeconds");
            minimumCommitBroadcastIntervalSecondsProperty = serializedObject.FindProperty("minimumCommitBroadcastIntervalSeconds");
            stringSendModeProperty = serializedObject.FindProperty("stringSendMode");
            enableDebugLogProperty = serializedObject.FindProperty("enableDebugLog");
            verboseLogProperty = serializedObject.FindProperty("verboseLog");
            logUnknownSyncIdProperty = serializedObject.FindProperty("logUnknownSyncId");
            logTypeMismatchProperty = serializedObject.FindProperty("logTypeMismatch");
            logDuplicateSyncIdProperty = serializedObject.FindProperty("logDuplicateSyncId");
            logRegistryHashMismatchProperty = serializedObject.FindProperty("logRegistryHashMismatch");
            enableStatisticsLogProperty = serializedObject.FindProperty("enableStatisticsLog");
            statisticsLogIntervalSecondsProperty = serializedObject.FindProperty("statisticsLogIntervalSeconds");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Profile は共有する P2P 同期設定です。各ノードは同格で、最後に触った操作を正として同期します。", MessageType.Info);

            DrawSectionHeader("基本設定", "全 peer で共有する識別情報です。");
            EditorGUILayout.PropertyField(profileNameProperty, new GUIContent("プロファイル名"));
            EditorGUILayout.PropertyField(nodeIdProperty, new GUIContent("ノード ID", "この端末を識別する一意名です。"));
            EditorGUILayout.PropertyField(protocolVersionProperty, new GUIContent("プロトコル バージョン"));

            DrawSectionHeader("ネットワーク", "ローカル通信を含め、現在は常に uOSC で送受信します。");
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(enableOscTransportProperty, new GUIContent("OSC 通信を使う", "互換性のために残していますが、現在は常に有効です。"));
            }
            EditorGUILayout.PropertyField(listenPortProperty, new GUIContent("受信ポート"));
            EditorGUILayout.PropertyField(allowDynamicPeerJoinProperty, new GUIContent("動的参加を許可", "許可すると allowedPeers にない nodeId も受信します。"));
            if (!allowDynamicPeerJoinProperty.boolValue)
            {
                EditorGUILayout.PropertyField(allowedPeersProperty, new GUIContent("許可 peer"), true);
            }

            EditorGUILayout.PropertyField(peerEndpointsProperty, new GUIContent("接続先 peer", "name は相手の nodeId に合わせます。"), true);

            DrawSectionHeader("同期ルール", "Last-Write-Wins、再送、長時間運用向け再同期の調整項目です。");
            EditorGUILayout.PropertyField(helloIntervalSecondsProperty, new GUIContent("Hello 間隔 (秒)"));
            EditorGUILayout.PropertyField(nodeTimeoutSecondsProperty, new GUIContent("peer 切断判定 (秒)"));
            EditorGUILayout.PropertyField(snapshotRequestIntervalSecondsProperty, new GUIContent("Snapshot 再要求間隔 (秒)"));
            EditorGUILayout.PropertyField(snapshotRequestRetryCountProperty, new GUIContent("Snapshot 再試行回数"));
            EditorGUILayout.PropertyField(snapshotRetryCooldownSecondsProperty, new GUIContent("Snapshot 再試行クールダウン (秒)"));
            EditorGUILayout.PropertyField(snapshotStateTimeoutSecondsProperty, new GUIContent("Snapshot 保持タイムアウト (秒)", "BeginSnapshot を受けたまま EndSnapshot が来ない場合の掃除時間です。"));
            EditorGUILayout.PropertyField(periodicFullResyncIntervalSecondsProperty, new GUIContent("定期フル再同期間隔 (秒)", "0 で無効です。長時間運用で状態ずれをならします。"));
            EditorGUILayout.PropertyField(sliderEpsilonProperty, new GUIContent("連続値の最小差分"));
            EditorGUILayout.PropertyField(minimumProposeIntervalSecondsProperty, new GUIContent("ローカル送信の最小間隔 (秒)"));
            EditorGUILayout.PropertyField(minimumCommitBroadcastIntervalSecondsProperty, new GUIContent("再送信の最小間隔 (秒)"));
            EditorGUILayout.PropertyField(stringSendModeProperty, new GUIContent("文字列の送信タイミング"));

            DrawSectionHeader("ログと監視", "調査用ログと長時間運用の簡易統計です。");
            EditorGUILayout.PropertyField(enableDebugLogProperty, new GUIContent("Debug.Log を表示", "有効な場合のみ CanvasUiSync の Debug.Log/Warning/Error を出力します。"));
            using (new EditorGUI.DisabledScope(!enableDebugLogProperty.boolValue))
            {
                EditorGUILayout.PropertyField(verboseLogProperty, new GUIContent("詳細ログ"));
                EditorGUILayout.PropertyField(logUnknownSyncIdProperty, new GUIContent("未知の SyncId を記録"));
                EditorGUILayout.PropertyField(logTypeMismatchProperty, new GUIContent("型不一致を記録"));
                EditorGUILayout.PropertyField(logDuplicateSyncIdProperty, new GUIContent("重複 SyncId を記録"));
                EditorGUILayout.PropertyField(logRegistryHashMismatchProperty, new GUIContent("RegistryHash 不一致を記録"));
                EditorGUILayout.PropertyField(enableStatisticsLogProperty, new GUIContent("統計ログを有効化", "送受信数、概算バイト数、GC 回数を一定間隔で記録します。"));
                if (enableDebugLogProperty.boolValue && enableStatisticsLogProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(statisticsLogIntervalSecondsProperty, new GUIContent("統計ログ間隔 (秒)"));
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private static void DrawSectionHeader(string title, string description)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(description, MessageType.None);
        }
    }
}
