using Mirror;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Beastbane.Netcode
{
    /// <summary>
    /// Optional helper for UI button/input wiring in editor.
    /// </summary>
    public sealed class HostIntegerBroadcasterUI : MonoBehaviour
    {
        [SerializeField] private HostIntegerBroadcaster broadcaster;
        [SerializeField] private int valueToSend = 1;
        [SerializeField] private bool incrementAfterSend = true;
        [SerializeField] private bool autoStartHostIfLobbyOwner = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private Key debugSendKey = Key.F8;
#else
        [SerializeField] private KeyCode debugSendKey = KeyCode.F8;
#endif


        public void SetValueFromString(string text)
        {
            if (!int.TryParse(text, out var parsed))
            {
                Debug.LogWarning($"HostIntegerBroadcasterUI: invalid integer '{text}'.");
                return;
            }

            valueToSend = parsed;
        }

        public void SendConfiguredValue()
        {
            var target = ResolveBroadcaster();
            if (target == null)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: HostIntegerBroadcaster not found.");
                return;
            }

            if (!NetworkServer.active)
            {
                TryAutoStartHost();
            }

            if (!NetworkServer.active)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: only host/server can broadcast. Make sure lobby owner pressed Start Game.");
                return;
            }

            target.BroadcastFromHost(valueToSend);

            if (incrementAfterSend)
            {
                valueToSend++;
            }
        }

        private HostIntegerBroadcaster ResolveBroadcaster()
        {
            if (broadcaster != null) return broadcaster;
            broadcaster = FindFirstObjectByType<HostIntegerBroadcaster>();
            return broadcaster;
        }

        private void TryAutoStartHost()
        {
            if (!autoStartHostIfLobbyOwner) return;

#if STEAMWORKS_NET
            var lobby = Steam.SteamLobbyManager.Instance;
            if (lobby == null || !lobby.InLobby || !lobby.IsLobbyOwner) return;

            var coordinator = FindFirstObjectByType<Steam.SteamMirrorCoordinator>();
            if (coordinator == null)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: SteamMirrorCoordinator not found.");
                return;
            }

            coordinator.StartGameFromHost();
#endif
        }
    }
}
