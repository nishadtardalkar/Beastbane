using System;
using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Beastbane.Steam
{
    public sealed class SteamLobbyManager : MonoBehaviour
    {
        public static SteamLobbyManager Instance { get; private set; }

        [Header("Startup")]
        [Tooltip("If enabled, automatically creates a lobby after Steam initializes.")]
        [SerializeField] private bool autoCreateLobbyOnStart = true;

        [Header("Lobby defaults")]
        [SerializeField] private ELobbyType lobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
        [SerializeField, Min(1)] private int maxMembers = 4;

        [Header("Lobby metadata")]
        [SerializeField] private string gameTagKey = "beastbane_build";
        [SerializeField] private string gameTagValue = "dev";
        [SerializeField] private string lobbyStateKey = "bb_state";
        [SerializeField] private string lobbySceneKey = "bb_scene";
        [SerializeField] private string lobbyHostKey = "bb_host";

        public bool InLobby => CurrentLobbyId != CSteamID.Nil;
        public bool IsLobbyOwner
        {
            get
            {
#if STEAMWORKS_NET
                if (!InLobby) return false;
                if (!RequireSteamReady()) return false;
                return SteamMatchmaking.GetLobbyOwner(CurrentLobbyId) == SteamUser.GetSteamID();
#else
                return false;
#endif
            }
        }

        public CSteamID CurrentLobbyId { get; private set; } = CSteamID.Nil;
        public CSteamID LobbyOwnerId { get; private set; } = CSteamID.Nil;

        public event Action<CSteamID> LobbyCreated;
        public event Action<CSteamID> LobbyEntered;
        public event Action LobbyLeft;
        public event Action<CSteamID, CSteamID> LobbyInviteReceived; // (inviter, lobby)
        public event Action<CSteamID> JoinRequested; // lobby
        public event Action<CSteamID> LobbyDataUpdated; // lobby

#if STEAMWORKS_NET
        private Callback<LobbyCreated_t> _lobbyCreated;
        private Callback<LobbyEnter_t> _lobbyEnter;
        private Callback<GameLobbyJoinRequested_t> _joinRequested;
        private Callback<LobbyInvite_t> _lobbyInvite;
        private Callback<LobbyDataUpdate_t> _lobbyDataUpdate;

        private CallResult<LobbyCreated_t> _lobbyCreateCallResult;

        private CSteamID _pendingCreateLobbyId = CSteamID.Nil;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

#if STEAMWORKS_NET
            _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
            _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnterCallback);
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyInvite = Callback<LobbyInvite_t>.Create(OnLobbyInvite);
            _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

            _lobbyCreateCallResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreatedCallResult);
#endif
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
#if STEAMWORKS_NET
            if (!autoCreateLobbyOnStart) return;
            StartCoroutine(AutoCreateLobbyWhenReady());
#endif
        }

#if STEAMWORKS_NET
        private System.Collections.IEnumerator AutoCreateLobbyWhenReady()
        {
            // Wait until SteamBootstrap exists and has initialized.
            var timeout = Time.unscaledTime + 10f;
            while (Time.unscaledTime < timeout)
            {
                var bootstrap = SteamBootstrap.Instance;
                if (bootstrap != null && bootstrap.IsInitialized) break;
                yield return null;
            }

            if (!RequireSteamReady())
            {
                Debug.LogWarning("SteamLobbyManager: auto-create lobby skipped (Steam not ready).");
                yield break;
            }

            if (!InLobby)
            {
                CreateLobby();
            }
        }
#endif

        public void CreateLobby()
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return;
            if (InLobby) LeaveLobby();

            var handle = SteamMatchmaking.CreateLobby(lobbyType, maxMembers);
            _lobbyCreateCallResult.Set(handle);
#else
            Debug.LogWarning("SteamLobbyManager.CreateLobby: Steamworks disabled.");
#endif
        }

        public void JoinLobby(CSteamID lobbyId)
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return;
            if (InLobby && lobbyId == CurrentLobbyId) return;
            if (InLobby) LeaveLobby();

            SteamMatchmaking.JoinLobby(lobbyId);
#else
            Debug.LogWarning("SteamLobbyManager.JoinLobby: Steamworks disabled.");
#endif
        }

        public void LeaveLobby()
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return;
            if (!InLobby) return;

            ClearRichPresence();
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);
            CurrentLobbyId = CSteamID.Nil;
            LobbyOwnerId = CSteamID.Nil;
            LobbyLeft?.Invoke();
#else
            Debug.LogWarning("SteamLobbyManager.LeaveLobby: Steamworks disabled.");
#endif
        }

        /// <summary>
        /// Opens Steam's overlay invite dialog for the current lobby.
        /// </summary>
        public void OpenInviteOverlay()
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return;
            if (!InLobby)
            {
                Debug.LogWarning("SteamLobbyManager.OpenInviteOverlay: Not in a lobby.");
                return;
            }

            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
#else
            Debug.LogWarning("SteamLobbyManager.OpenInviteOverlay: Steamworks disabled.");
#endif
        }

        /// <summary>
        /// Sends a direct lobby invite to a friend (works even without overlay).
        /// Friend must be in your Steam friends list.
        /// </summary>
        public bool InviteFriend(CSteamID friendId)
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return false;
            if (!InLobby) return false;

            return SteamMatchmaking.InviteUserToLobby(CurrentLobbyId, friendId);
#else
            return false;
#endif
        }

        public int GetLobbyMemberCount()
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return 0;
            if (!InLobby) return 0;
            return SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId);
#else
            return 0;
#endif
        }

        public CSteamID GetLobbyMemberByIndex(int index)
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return CSteamID.Nil;
            if (!InLobby) return CSteamID.Nil;
            return SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, index);
#else
            return CSteamID.Nil;
#endif
        }

        public string GetLobbyData(string key)
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return string.Empty;
            if (!InLobby) return string.Empty;
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            return SteamMatchmaking.GetLobbyData(CurrentLobbyId, key) ?? string.Empty;
#else
            return string.Empty;
#endif
        }

        public bool SetLobbyData(string key, string value)
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return false;
            if (!InLobby) return false;
            if (!IsLobbyOwner) return false;
            if (string.IsNullOrWhiteSpace(key)) return false;
            return SteamMatchmaking.SetLobbyData(CurrentLobbyId, key, value ?? string.Empty);
#else
            return false;
#endif
        }

        public bool StartGame(string gameplaySceneName)
        {
#if STEAMWORKS_NET
            if (!RequireSteamReady()) return false;
            if (!InLobby) return false;
            if (!IsLobbyOwner) return false;

            var hostSteamId = SteamUser.GetSteamID().m_SteamID.ToString();
            var okState = SteamMatchmaking.SetLobbyData(CurrentLobbyId, lobbyStateKey, "starting");
            var okScene = SteamMatchmaking.SetLobbyData(CurrentLobbyId, lobbySceneKey, gameplaySceneName ?? string.Empty);
            var okHost = SteamMatchmaking.SetLobbyData(CurrentLobbyId, lobbyHostKey, hostSteamId);
            return okState && okScene && okHost;
#else
            return false;
#endif
        }

        public string LobbyStateKey => lobbyStateKey;
        public string LobbySceneKey => lobbySceneKey;
        public string LobbyHostKey => lobbyHostKey;

#if STEAMWORKS_NET
        private bool RequireSteamReady()
        {
            var bootstrap = SteamBootstrap.Instance;
            if (bootstrap == null || !bootstrap.IsInitialized)
            {
                Debug.LogWarning("SteamLobbyManager: SteamBootstrap missing or Steam not initialized.");
                return false;
            }
            return true;
        }

        private void OnLobbyCreatedCallResult(LobbyCreated_t result, bool ioFailure)
        {
            // This is the async response to CreateLobby.
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"SteamLobbyManager: CreateLobby failed. ioFailure={ioFailure}, result={result.m_eResult}");
                return;
            }

            _pendingCreateLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            // We will also receive LobbyEnter_t after this; keep both events in case you want to start hosting early.
            CurrentLobbyId = _pendingCreateLobbyId;
            LobbyOwnerId = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);

            ApplyLobbyMetadata(CurrentLobbyId);
            LobbyCreated?.Invoke(CurrentLobbyId);
        }

        private void OnLobbyCreatedCallback(LobbyCreated_t result)
        {
            // Some builds of Steamworks.NET fire both callback and callresult; keep this as a fallback.
            if (result.m_eResult != EResult.k_EResultOK) return;
            var lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            if (lobbyId != CSteamID.Nil && CurrentLobbyId == CSteamID.Nil)
            {
                CurrentLobbyId = lobbyId;
                LobbyOwnerId = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
                ApplyLobbyMetadata(CurrentLobbyId);
                LobbyCreated?.Invoke(CurrentLobbyId);
            }
        }

        private void OnLobbyEnterCallback(LobbyEnter_t result)
        {
            var lobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentLobbyId = lobbyId;
            LobbyOwnerId = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);

            ApplyRichPresence();
            LobbyEntered?.Invoke(CurrentLobbyId);
        }

        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            // Fired when the user accepts an invite (or clicks "Join Game") from Steam UI.
            var lobbyId = request.m_steamIDLobby;
            JoinRequested?.Invoke(lobbyId);

            // Default behavior: auto-join.
            JoinLobby(lobbyId);
        }

        private void OnLobbyInvite(LobbyInvite_t invite)
        {
            // Fired when a friend invites you to their lobby.
            // Note: accepting is typically done through Steam UI; once accepted you'll get GameLobbyJoinRequested_t.
            LobbyInviteReceived?.Invoke(new CSteamID(invite.m_ulSteamIDUser), new CSteamID(invite.m_ulSteamIDLobby));
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t update)
        {
            var lobbyId = new CSteamID(update.m_ulSteamIDLobby);
            if (lobbyId == CSteamID.Nil) return;
            if (!InLobby || lobbyId != CurrentLobbyId) return;
            LobbyDataUpdated?.Invoke(lobbyId);
        }

        private void ApplyLobbyMetadata(CSteamID lobbyId)
        {
            if (lobbyId == CSteamID.Nil) return;
            if (!string.IsNullOrWhiteSpace(gameTagKey))
            {
                SteamMatchmaking.SetLobbyData(lobbyId, gameTagKey, gameTagValue ?? string.Empty);
            }
        }

        private void ApplyRichPresence()
        {
            if (!InLobby) return;

            // Helps Steam surface "Invite to game" / join options in friends UI.
            SteamFriends.SetRichPresence("status", "In Lobby");
            SteamFriends.SetRichPresence("connect", CurrentLobbyId.ToString());
            SteamFriends.SetRichPresence("steam_player_group", CurrentLobbyId.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId).ToString());
        }

        private void ClearRichPresence()
        {
            SteamFriends.ClearRichPresence();
        }
#endif
    }
}

