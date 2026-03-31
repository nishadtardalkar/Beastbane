using UnityEngine;

namespace Beastbane.Steam
{
    /// <summary>
    /// Drop this into your bootstrap scene to ensure Steam objects exist.
    /// </summary>
    public sealed class SteamSceneInstaller : MonoBehaviour
    {
        [SerializeField] private bool createIfMissing = true;

        private void Awake()
        {
            if (!createIfMissing) return;

            if (SteamBootstrap.Instance == null)
            {
                var go = new GameObject(nameof(SteamBootstrap));
                go.AddComponent<SteamBootstrap>();
            }

            if (SteamLobbyManager.Instance == null)
            {
                var go = new GameObject(nameof(SteamLobbyManager));
                go.AddComponent<SteamLobbyManager>();
            }

            if (FindFirstObjectByType<SteamMirrorCoordinator>() == null)
            {
                var go = new GameObject(nameof(SteamMirrorCoordinator));
                go.AddComponent<SteamMirrorCoordinator>();
            }
        }
    }
}

