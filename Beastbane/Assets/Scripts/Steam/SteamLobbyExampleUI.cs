using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Beastbane.Steam
{
    /// <summary>
    /// Optional: simple button bindings for testing in-editor.
    /// </summary>
    public sealed class SteamLobbyExampleUI : MonoBehaviour
    {
        [SerializeField] private SteamMirrorCoordinator mirrorCoordinator;

        public void CreateLobby()
        {
            SteamLobbyManager.Instance?.CreateLobby();
        }

        public void LeaveLobby()
        {
            SteamLobbyManager.Instance?.LeaveLobby();
        }

        public void OpenInviteOverlay()
        {
            SteamLobbyManager.Instance?.OpenInviteOverlay();
        }

        public void StartGame()
        {
            if (mirrorCoordinator == null)
                mirrorCoordinator = FindFirstObjectByType<SteamMirrorCoordinator>();

            if (mirrorCoordinator == null)
            {
                Debug.LogWarning("SteamLobbyExampleUI: SteamMirrorCoordinator not found in scene.");
                return;
            }

            mirrorCoordinator.StartGameFromHost();
        }

#if STEAMWORKS_NET
        public void InviteFriendBySteamIdString(string steamId64)
        {
            if (!ulong.TryParse(steamId64, out var id)) return;
            SteamLobbyManager.Instance?.InviteFriend(new CSteamID(id));
        }
#else
        public void InviteFriendBySteamIdString(string steamId64) { }
#endif
    }
}

