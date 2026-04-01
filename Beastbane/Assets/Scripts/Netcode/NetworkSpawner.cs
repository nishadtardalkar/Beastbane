using System;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    public class NetworkSpawner : MonoBehaviour
    {
        /// <summary>
        /// Use from Button.onClick — pass the int matching the SpawnObject enum
        /// (e.g. 0 = Map). Shows up under Static Parameters in the inspector.
        /// </summary>
        public void Spawn(int spawnIndex)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("NetworkSpawner: only host/server can spawn network objects.");
                return;
            }

            if (NetworkManager.singleton == null)
            {
                Debug.LogError("NetworkSpawner: NetworkManager.singleton is null.");
                return;
            }

            int index = spawnIndex;
            if (index < 0 || index >= NetworkManager.singleton.spawnPrefabs.Count)
            {
                Debug.LogError($"NetworkSpawner: spawn index {index} is out of range.");
                return;
            }

            GameObject obj = NetworkManager.singleton.spawnPrefabs[index];
            if (obj == null)
            {
                Debug.LogError($"NetworkSpawner: spawn prefab at index {index} is null.");
                return;
            }

            var instance = Instantiate(obj);
            NetworkServer.Spawn(instance);
        }
    }
}
