using Mirror;
using UnityEngine;
using Beastbane.Map;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Network-spawned map bootstrapper.
    /// Server picks/syncs the seed, then every peer generates the same map.
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class MapSpawner : NetworkBehaviour
    {
        [SerializeField] private MapGenerator mapGenerator;

        [SyncVar(hook = nameof(OnSeedChanged))]
        [SerializeField] private int mapSeed = int.MinValue;

        private bool _generated;

        private void Awake()
        {
            if (mapGenerator == null)
                mapGenerator = GetComponent<MapGenerator>();

            if (mapGenerator == null)
            {
                Debug.LogError("MapSpawner: MapGenerator not found on this GameObject.");
                return;
            }

            // Generation is controlled by this network flow.
            mapGenerator.waitForNetworkSeed = true;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            EnsureServerSeedAndGenerate();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            TryGenerateFromSyncedSeed();
        }

        private void OnSeedChanged(int oldSeed, int newSeed)
        {
            Debug.Log($"MapSpawner: seed updated {oldSeed} -> {newSeed}");
            TryGenerateFromSyncedSeed();
        }

        [Server]
        private void EnsureServerSeedAndGenerate()
        {
            if (mapGenerator == null) return;
            if (_generated) return;

            if (mapSeed == int.MinValue)
            {
                mapSeed = mapGenerator.Seed == -1 ? System.Environment.TickCount : mapGenerator.Seed;
                Debug.Log($"MapSpawner: server assigned seed={mapSeed}");
            }

            GenerateLocalMap(mapSeed);
            Debug.Log($"MapSpawner: server generated map with seed={mapSeed}");
        }

        private void TryGenerateFromSyncedSeed()
        {
            if (_generated) return;
            if (mapGenerator == null) return;
            if (mapSeed == int.MinValue) return;

            GenerateLocalMap(mapSeed);
            Debug.Log($"MapSpawner: client generated map with seed={mapSeed}");
        }

        private void GenerateLocalMap(int seed)
        {
            mapGenerator.Seed = seed;
            mapGenerator.GenerateMap();
            _generated = true;
        }
    }
}
