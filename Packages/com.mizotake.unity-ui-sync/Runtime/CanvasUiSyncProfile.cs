using System.Collections.Generic;
using UnityEngine;

namespace Mizotake.UnityUiSync
{
    [CreateAssetMenu(fileName = "CanvasUiSyncProfile", menuName = "Unity UI Sync/プロファイル")]
    public sealed class CanvasUiSyncProfile : ScriptableObject
    {
        public string profileName = "Default";
        public string nodeId = "Node";
        public int protocolVersion = 1;
        public float helloIntervalSeconds = 1f;
        public float nodeTimeoutSeconds = 5f;
        public float snapshotRequestIntervalSeconds = 1.5f;
        public int snapshotRequestRetryCount = 5;
        public float snapshotRetryCooldownSeconds = 5f;
        public float snapshotStateTimeoutSeconds = 10f;
        public float periodicFullResyncIntervalSeconds = 60f;
        public float sliderEpsilon = 0.001f;
        public float minimumProposeIntervalSeconds = 0.05f;
        public float minimumCommitBroadcastIntervalSeconds = 0.05f;
        public CanvasUiSyncStringSendMode stringSendMode = CanvasUiSyncStringSendMode.OnEndEdit;
        public bool enableOscTransport = true;
        public int listenPort = 9000;
        public bool allowDynamicPeerJoin = false;
        public List<string> allowedPeers = new List<string>();
        public List<CanvasUiSyncRemoteEndpoint> peerEndpoints = new List<CanvasUiSyncRemoteEndpoint>();
        public bool enableDebugLog;
        public bool verboseLog = true;
        public bool logUnknownSyncId = true;
        public bool logTypeMismatch = true;
        public bool logDuplicateSyncId = true;
        public bool logRegistryHashMismatch = true;
        public bool enableStatisticsLog;
        public float statisticsLogIntervalSeconds = 60f;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = "Default";
            }

            if (string.IsNullOrWhiteSpace(nodeId))
            {
                nodeId = "Node";
            }

            protocolVersion = Mathf.Max(1, protocolVersion);
            helloIntervalSeconds = Mathf.Max(0.1f, helloIntervalSeconds);
            nodeTimeoutSeconds = Mathf.Max(0.1f, nodeTimeoutSeconds);
            snapshotRequestIntervalSeconds = Mathf.Max(0.1f, snapshotRequestIntervalSeconds);
            snapshotRequestRetryCount = Mathf.Max(1, snapshotRequestRetryCount);
            snapshotRetryCooldownSeconds = Mathf.Max(0.1f, snapshotRetryCooldownSeconds);
            snapshotStateTimeoutSeconds = Mathf.Max(0.5f, snapshotStateTimeoutSeconds);
            periodicFullResyncIntervalSeconds = Mathf.Max(0f, periodicFullResyncIntervalSeconds);
            sliderEpsilon = Mathf.Max(0f, sliderEpsilon);
            minimumProposeIntervalSeconds = Mathf.Max(0f, minimumProposeIntervalSeconds);
            minimumCommitBroadcastIntervalSeconds = Mathf.Max(0f, minimumCommitBroadcastIntervalSeconds);
            statisticsLogIntervalSeconds = Mathf.Max(1f, statisticsLogIntervalSeconds);
            enableOscTransport = true;
            listenPort = Mathf.Clamp(listenPort, 1, 65535);
            if (peerEndpoints == null)
            {
                peerEndpoints = new List<CanvasUiSyncRemoteEndpoint>();
            }

            if (allowedPeers == null)
            {
                allowedPeers = new List<string>();
            }

            foreach (var endpoint in peerEndpoints)
            {
                if (endpoint == null)
                {
                    continue;
                }

                endpoint.port = Mathf.Clamp(endpoint.port, 1, 65535);
                if (string.IsNullOrWhiteSpace(endpoint.ipAddress))
                {
                    endpoint.ipAddress = "127.0.0.1";
                }
            }
        }
    }
}
