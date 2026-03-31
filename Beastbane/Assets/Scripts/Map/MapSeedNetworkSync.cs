using Beastbane.Netcode;
using Mirror;
using UnityEngine;

namespace Beastbane.Map
{
    /// <summary>
    /// Bridges HostIntegerBroadcaster and MapGenerator for multiplayer seed sync.
    ///
    /// Host  : generates the map, then sends Map.Seed via HostIntegerBroadcaster.
    /// Client: waits for the broadcaster to appear, receives the seed, then generates.
    ///
    /// Attach to the same GameObject as MapGenerator.
    /// The broadcaster prefab must be registered in NetworkManager.spawnPrefabs
    /// (or rely on HostIntegerBroadcasterUI to spawn it).
    /// </summary>
    public sealed class MapSeedNetworkSync : MonoBehaviour
    {
        [SerializeField] private MapGenerator mapGenerator;

        private HostIntegerBroadcaster _broadcaster;
        private bool _generated;

        private void Awake()
        {
            if (mapGenerator == null)
                mapGenerator = GetComponent<MapGenerator>();

            if (mapGenerator == null)
            {
                Debug.LogError("MapSeedNetworkSync: MapGenerator not found on this GameObject.");
                return;
            }

            // Block MapGenerator.Start() from auto-generating — we drive it here.
            mapGenerator.waitForNetworkSeed = true;
        }

        private void Start()
        {
            if (mapGenerator == null) return;

            if (!NetworkClient.active)
            {
                // Offline / solo play — generate immediately with normal seed logic.
                mapGenerator.GenerateMap();
                _generated = true;
                return;
            }

            if (NetworkServer.active)
            {
                // Host: generate the map now and store the seed.
                // The broadcaster may not be spawned yet, so send the seed in Update()
                // once we find it.
                mapGenerator.GenerateMap();
                _generated = true;
            }
            // Clients: do nothing here — wait for broadcaster to appear in Update().
        }

        private void Update()
        {
            if (_broadcaster != null) return;

            _broadcaster = FindFirstObjectByType<HostIntegerBroadcaster>();
            if (_broadcaster == null) return;

            if (NetworkServer.active)
            {
                // Host found the spawned broadcaster — push the seed.
                _broadcaster.BroadcastFromHost(mapGenerator.Map.Seed);
                Debug.Log($"MapSeedNetworkSync: host sent seed={mapGenerator.Map.Seed}");
            }
            else
            {
                // Client found the broadcaster. Subscribe for future changes.
                _broadcaster.SharedValueChanged += OnSeedReceived;

                // If the seed was already synced before we subscribed (common when
                // connecting after the host broadcasted), use SharedValue directly.
                if (!_generated && _broadcaster.SharedValue != 0)
                {
                    OnSeedReceived(_broadcaster.SharedValue);
                }
            }
        }

        private void OnSeedReceived(int seed)
        {
            if (_generated) return;
            if (NetworkServer.active) return; // Host already generated.

            mapGenerator.Seed = seed;
            mapGenerator.GenerateMap();
            _generated = true;
            Debug.Log($"MapSeedNetworkSync: client generated map with host seed={seed}");
        }

        private void OnDestroy()
        {
            if (_broadcaster != null)
                _broadcaster.SharedValueChanged -= OnSeedReceived;
        }
    }
}
