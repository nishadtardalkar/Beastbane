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
        [SerializeField] private HostIntegerBroadcaster broadcasterPrefab;
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

            if (!NetworkServer.active)
            {
                TryAutoStartHost();
            }

            if (!NetworkServer.active && !NetworkClient.active)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: no active Mirror client/server. Start game first.");
                return;
            }

            if (NetworkServer.active)
            {
                target = EnsureBroadcasterSpawnedOnServer(target);
                if (target == null)
                {
                    Debug.LogWarning("HostIntegerBroadcasterUI: no spawned broadcaster found, and prefab spawn failed.");
                    return;
                }
            }
            else if (target == null)
            {
                // Client-only side: broadcaster must already be spawned by host.
                target = FindFirstObjectByType<HostIntegerBroadcaster>();
                if (target == null)
                {
                    Debug.LogWarning("HostIntegerBroadcasterUI: no spawned HostIntegerBroadcaster visible on client yet.");
                    return;
                }
            }

            target.SendFromLocalPeer(valueToSend);

            if (incrementAfterSend)
            {
                valueToSend++;
            }
        }

        private HostIntegerBroadcaster ResolveBroadcaster()
        {
            if (broadcaster != null && broadcaster.gameObject.scene.IsValid()) return broadcaster;
            broadcaster = FindFirstObjectByType<HostIntegerBroadcaster>();
            return broadcaster;
        }

        private HostIntegerBroadcaster EnsureBroadcasterSpawnedOnServer(HostIntegerBroadcaster current)
        {
            if (!NetworkServer.active) return null;

            if (current != null &&
                current.TryGetComponent<NetworkIdentity>(out var currentIdentity) &&
                currentIdentity.isServer)
            {
                return current;
            }

            HostIntegerBroadcaster prefabToSpawn = broadcasterPrefab;
            if (prefabToSpawn == null && broadcaster != null && !broadcaster.gameObject.scene.IsValid())
            {
                // Support the common case where a prefab asset was assigned to "broadcaster".
                prefabToSpawn = broadcaster;
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning("HostIntegerBroadcasterUI: assign broadcasterPrefab (network spawnable) in inspector.");
                return null;
            }

            var manager = NetworkManager.singleton;
            if (manager != null && !manager.spawnPrefabs.Contains(prefabToSpawn.gameObject))
            {
                Debug.LogWarning(
                    "HostIntegerBroadcasterUI: broadcasterPrefab is not in NetworkManager.spawnPrefabs. " +
                    "Add it there so clients can spawn it correctly."
                );
                manager.spawnPrefabs.Add(prefabToSpawn.gameObject);
            }

            var instance = Instantiate(prefabToSpawn);
            if (!instance.TryGetComponent<NetworkIdentity>(out var identity))
            {
                Debug.LogError("HostIntegerBroadcasterUI: broadcaster prefab must have NetworkIdentity.");
                Destroy(instance.gameObject);
                return null;
            }

            NetworkServer.Spawn(instance.gameObject);
            broadcaster = instance;
            return instance;
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
