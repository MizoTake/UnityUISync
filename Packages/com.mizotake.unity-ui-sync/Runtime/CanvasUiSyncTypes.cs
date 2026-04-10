using System;

namespace Mizotake.UnityUiSync
{
    public enum CanvasUiSyncSyncMode
    {
        PeerToPeer = 0
    }

    public enum CanvasUiSyncStringSendMode
    {
        OnEndEdit = 0,
        OnValueChanged = 1
    }

    public enum CanvasUiSyncValueType
    {
        Bool = 0,
        Float = 1,
        Int = 2,
        String = 3,
        Button = 4
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
