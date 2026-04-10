using UnityEditor;
using UnityEngine;

namespace Mizotake.UnityUiSync.Editor
{
    [CustomPropertyDrawer(typeof(CanvasUiSyncRemoteEndpoint))]
    public sealed class CanvasUiSyncRemoteEndpointDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 4f + 8f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var nameProperty = property.FindPropertyRelative("name");
            var ipAddressProperty = property.FindPropertyRelative("ipAddress");
            var portProperty = property.FindPropertyRelative("port");
            var enabledProperty = property.FindPropertyRelative("enabled");
            var lineHeight = EditorGUIUtility.singleLineHeight;
            var y = position.y;

            EditorGUI.LabelField(new Rect(position.x, y, position.width, lineHeight), label.text, EditorStyles.boldLabel);
            y += lineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), nameProperty, new GUIContent("ノード ID", "接続先 peer の nodeId と同じ名前にします。"));
            y += lineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, lineHeight), ipAddressProperty, new GUIContent("IP アドレス", "送信先の IP アドレスです。"));
            y += lineHeight + 2f;
            var leftWidth = position.width * 0.7f;
            EditorGUI.PropertyField(new Rect(position.x, y, leftWidth - 4f, lineHeight), portProperty, new GUIContent("ポート", "送信先の OSC ポートです。"));
            EditorGUI.PropertyField(new Rect(position.x + leftWidth, y, position.width - leftWidth, lineHeight), enabledProperty, new GUIContent("有効", "無効にすると送信先一覧から外します。"));
            EditorGUI.EndProperty();
        }
    }
}
