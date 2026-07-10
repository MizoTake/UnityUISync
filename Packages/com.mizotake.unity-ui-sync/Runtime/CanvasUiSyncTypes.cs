using System;
using UnityEngine;

namespace Mizotake.UnityUiSync
{
    public enum CanvasUiSyncStringSendMode
    {
        OnEndEdit = 0,
        OnValueChanged = 1
    }

    [Serializable]
    public sealed class CanvasUiSyncRemoteEndpoint
    {
        public string name = string.Empty;
        public string ipAddress = "127.0.0.1";
        public int port = 9001;
        public bool enabled = true;
    }

    [Serializable]
    internal sealed class CanvasUiSyncExclusion
    {
        [SerializeField] internal Component target;
        [SerializeField, HideInInspector] internal Component capturedTarget;
        [SerializeField, HideInInspector] internal string bindingId = string.Empty;
        [SerializeField, HideInInspector] internal string hierarchyPath = string.Empty;
        [SerializeField, HideInInspector] internal string componentType = string.Empty;
    }

    [DisallowMultipleComponent]
    public sealed class CanvasUiSyncBindingId : MonoBehaviour
    {
        [SerializeField] private string bindingId = string.Empty;

        public string BindingId
        {
            get => bindingId;
            set => bindingId = value;
        }
    }
}
