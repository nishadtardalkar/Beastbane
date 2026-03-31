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

        /// <summary>
        /// Host/server call: sets value and sends to all connected clients.
        /// </summary>
        [Server]
        public void BroadcastFromHost(int value)
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
            Debug.Log($"HostIntegerBroadcaster: host broadcast value={value}");
        }

        private void OnSharedValueChanged(int oldValue, int newValue)
        {
            Debug.Log($"HostIntegerBroadcaster: value updated {oldValue} -> {newValue}");
            SharedValueChanged?.Invoke(newValue);
        }
    }
}
