using System;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Host/server-owned broadcaster that syncs one integer to all players.
    /// Attach this to a GameObject with NetworkIdentity.
    /// </summary>
    public sealed class HostIntegerBroadcaster : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnSharedValueChanged))]
        [SerializeField] private int sharedValue;

        public int SharedValue => sharedValue;

        public event Action<int> SharedValueChanged;

        /// <summary>
        /// Host/server call: sets value and sends to all connected clients.
        /// </summary>
        [Server]
        public void BroadcastFromHost(int value)
        {
            sharedValue = value;
            RpcValueBroadcast(value);
            Debug.Log($"HostIntegerBroadcaster: host broadcast value={value}");
        }

        [ClientRpc]
        private void RpcValueBroadcast(int value)
        {
            Debug.Log($"HostIntegerBroadcaster: client received value={value}");
        }

        private void OnSharedValueChanged(int oldValue, int newValue)
        {
            SharedValueChanged?.Invoke(newValue);
        }
    }
}
