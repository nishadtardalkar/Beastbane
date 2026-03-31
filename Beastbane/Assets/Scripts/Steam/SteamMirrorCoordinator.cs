using System;
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
        [SerializeField] private bool spawnNetworkManagerIfMissing = true;
        [SerializeField] private NetworkManager networkManagerPrefab;
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

            if (NetworkManager.singleton != null) return NetworkManager.singleton;

#if UNITY_2023_1_OR_NEWER
            var existing = FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
#else
            var existing = FindObjectOfType<NetworkManager>();
#endif
            if (existing != null) return existing;

            if (spawnNetworkManagerIfMissing && networkManagerPrefab != null)
            {
                var instance = Instantiate(networkManagerPrefab);
                if (instance != null)
                {
                    if (!instance.gameObject.activeSelf) instance.gameObject.SetActive(true);
                    return instance;
                }
            }

            Debug.LogWarning(
                "SteamMirrorCoordinator: NetworkManager not found. " +
                "Add one to the scene, set NetworkManager.singleton, or assign networkManagerPrefab on SteamMirrorCoordinator."
            );
            return null;
        }

        private void EnsureMirrorHostStarted()
        {
            var net = GetNetworkManager();
            if (net == null)
            {
                Debug.LogWarning("SteamMirrorCoordinator: NetworkManager not found.");
                return;
            }

            if (!EnsureTransportConfigured(net)) return;

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

            if (!EnsureTransportConfigured(net)) return;

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

        private bool EnsureTransportConfigured(NetworkManager net)
        {
            if (net.transport != null) return true;

            if (net.TryGetComponent<Transport>(out var sameObjectTransport) && sameObjectTransport != null)
            {
                net.transport = sameObjectTransport;
                return true;
            }

            // Prefer FizzySteamworks when available for Steam P2P.
            var fizzyType = Type.GetType("Mirror.FizzySteam.FizzySteamworks, Assembly-CSharp");
            if (fizzyType != null && typeof(Transport).IsAssignableFrom(fizzyType))
            {
                var fizzy = net.GetComponent(fizzyType) as Transport;
                if (fizzy == null)
                {
                    fizzy = net.gameObject.AddComponent(fizzyType) as Transport;
                }

                if (fizzy != null)
                {
                    net.transport = fizzy;
                    Debug.LogWarning("SteamMirrorCoordinator: Auto-assigned FizzySteamworks transport.");
                    return true;
                }
            }

#if UNITY_2023_1_OR_NEWER
            var anyTransport = FindFirstObjectByType<Transport>(FindObjectsInactive.Include);
#else
            var anyTransport = FindObjectOfType<Transport>();
#endif
            if (anyTransport != null)
            {
                net.transport = anyTransport;
                Debug.LogWarning($"SteamMirrorCoordinator: Auto-assigned scene transport '{anyTransport.GetType().Name}'.");
                return true;
            }

            Debug.LogError("SteamMirrorCoordinator: No Mirror Transport found. Add FizzySteamworks (or another Transport) to the NetworkManager and assign it.");
            return false;
        }
#endif
    }
}
