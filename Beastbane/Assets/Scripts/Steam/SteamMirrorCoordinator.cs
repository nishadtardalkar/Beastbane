using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

#if MIRROR
using Mirror;
#endif

namespace Beastbane.Steam
{
    /// <summary>
    /// Bridges Steam lobby state to Mirror start/join calls.
    /// </summary>
    public sealed class SteamMirrorCoordinator : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "GameScene";
        [SerializeField] private bool autoStartMirrorFromLobbyState = true;

#if MIRROR
        [Header("Mirror")]
        [SerializeField] private NetworkManager networkManagerOverride;
#endif

        private SteamLobbyManager _lobbyManager;

        private void Awake()
        {
            _lobbyManager = SteamLobbyManager.Instance;
        }

        private void OnEnable()
        {
            var mgr = SteamLobbyManager.Instance;
            if (mgr == null) return;

            _lobbyManager = mgr;
            mgr.LobbyEntered += OnLobbyEntered;
            mgr.LobbyDataUpdated += OnLobbyDataUpdated;
        }

        private void OnDisable()
        {
            var mgr = _lobbyManager;
            if (mgr == null) return;
            mgr.LobbyEntered -= OnLobbyEntered;
            mgr.LobbyDataUpdated -= OnLobbyDataUpdated;
        }

        public void StartGameFromHost()
        {
            if (_lobbyManager == null) _lobbyManager = SteamLobbyManager.Instance;
            if (_lobbyManager == null)
            {
                Debug.LogWarning("SteamMirrorCoordinator: SteamLobbyManager not found.");
                return;
            }

            if (!_lobbyManager.StartGame(gameplaySceneName))
            {
                Debug.LogWarning("SteamMirrorCoordinator: failed to mark lobby as starting.");
                return;
            }

#if MIRROR
            EnsureMirrorHostStarted();
#endif
        }

        private void OnLobbyEntered(
#if STEAMWORKS_NET
            CSteamID _
#else
            object _
#endif
        )
        {
            if (!autoStartMirrorFromLobbyState) return;
            TryHandleLobbyState();
        }

        private void OnLobbyDataUpdated(
#if STEAMWORKS_NET
            CSteamID _
#else
            object _
#endif
        )
        {
            if (!autoStartMirrorFromLobbyState) return;
            TryHandleLobbyState();
        }

        private void TryHandleLobbyState()
        {
            if (_lobbyManager == null) return;
            if (!_lobbyManager.InLobby) return;

            var state = _lobbyManager.GetLobbyData(_lobbyManager.LobbyStateKey);
            if (!string.Equals(state, "starting", System.StringComparison.OrdinalIgnoreCase)) return;

#if MIRROR
            if (_lobbyManager.IsLobbyOwner)
            {
                EnsureMirrorHostStarted();
                return;
            }

            EnsureMirrorClientStarted();
#endif
        }

#if MIRROR && STEAMWORKS_NET
        private NetworkManager GetNetworkManager()
        {
            if (networkManagerOverride != null) return networkManagerOverride;
            return NetworkManager.singleton;
        }

        private void EnsureMirrorHostStarted()
        {
            var net = GetNetworkManager();
            if (net == null)
            {
                Debug.LogWarning("SteamMirrorCoordinator: NetworkManager not found.");
                return;
            }

            if (NetworkServer.active || NetworkClient.isConnected || NetworkClient.active) return;
            net.StartHost();
        }

        private void EnsureMirrorClientStarted()
        {
            var net = GetNetworkManager();
            if (net == null)
            {
                Debug.LogWarning("SteamMirrorCoordinator: NetworkManager not found.");
                return;
            }

            if (NetworkClient.isConnected || NetworkClient.active || NetworkServer.active) return;

            var hostSteamId = _lobbyManager.GetLobbyData(_lobbyManager.LobbyHostKey);
            if (string.IsNullOrWhiteSpace(hostSteamId))
            {
                Debug.LogWarning("SteamMirrorCoordinator: lobby host SteamID missing.");
                return;
            }

            // For Steam P2P transports, networkAddress is typically the host SteamID string.
            net.networkAddress = hostSteamId;
            net.StartClient();
        }
#endif
    }
}
