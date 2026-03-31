using System;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Host/server-owned broadcaster that syncs one integer to all players.
    /// Attach this to a GameObject with NetworkIdentity.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class HostIntegerBroadcaster : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnSharedValueChanged))]
        [SerializeField] private int sharedValue;

        public int SharedValue => sharedValue;

        public event Action<int> SharedValueChanged;
        public event Action<int, int> ServerReceivedClientValue; // (value, senderConnectionId)

        /// <summary>
        /// Host/server call: sets value and sends to all connected clients.
        /// </summary>
        [Server]
        public void BroadcastFromHost(int value)
        {
            ApplyAuthoritativeValue(value, -1);
            Debug.Log($"HostIntegerBroadcaster: host set value={value}");
        }

        /// <summary>
        /// Bidirectional entrypoint:
        /// - If called on host/server, applies immediately.
        /// - If called on a client, sends to server via Command.
        /// </summary>
        public void SendFromLocalPeer(int value)
        {
            if (isServer)
            {
                ApplyAuthoritativeValue(value, -1);
                return;
            }

            if (!isClient)
            {
                Debug.LogWarning("HostIntegerBroadcaster: local peer is neither active client nor server.");
                return;
            }

            CmdSubmitValue(value);
        }

        [Command(requiresAuthority = false)]
        private void CmdSubmitValue(int value, NetworkConnectionToClient sender = null)
        {
            var senderId = sender != null ? sender.connectionId : -1;
            Debug.Log($"HostIntegerBroadcaster: server received client value={value} from conn={senderId}");
            ServerReceivedClientValue?.Invoke(value, senderId);
            ApplyAuthoritativeValue(value, senderId);
        }

        [Server]
        private void ApplyAuthoritativeValue(int value, int senderConnectionId)
        {
            if (netIdentity == null)
            {
                Debug.LogError("HostIntegerBroadcaster: missing NetworkIdentity component.");
                return;
            }

            if (!isServer)
            {
                Debug.LogWarning("HostIntegerBroadcaster: object is not spawned on server yet.");
                return;
            }

            sharedValue = value;
            RpcServerAcceptedValue(value, senderConnectionId);
        }

        [ClientRpc]
        private void RpcServerAcceptedValue(int value, int senderConnectionId)
        {
            Debug.Log($"HostIntegerBroadcaster: server accepted value={value}, senderConn={senderConnectionId}");
        }

        private void OnSharedValueChanged(int oldValue, int newValue)
        {
            Debug.Log($"HostIntegerBroadcaster: value updated {oldValue} -> {newValue}");
            SharedValueChanged?.Invoke(newValue);
        }
    }
}
