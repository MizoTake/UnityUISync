using System;

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
}
