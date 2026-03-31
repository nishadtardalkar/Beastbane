using UnityEngine;

#if STEAMWORKS_NET
using System.IO;
using Steamworks;
#endif

namespace Beastbane.Steam
{
    /// <summary>
    /// Minimal Steamworks.NET bootstrapper: initializes Steam and pumps callbacks.
    /// Keep exactly one instance alive across scenes.
    /// </summary>
    public sealed class SteamBootstrap : MonoBehaviour
    {
        public static SteamBootstrap Instance { get; private set; }

        public bool IsInitialized { get; private set; }

        [Header("Optional")]
        [Tooltip("If true, quits the app when Steam isn't running or init fails.")]
        [SerializeField] private bool quitIfSteamNotAvailable = true;

#if STEAMWORKS_NET
        [Header("Steam App ID")]
        [Tooltip("Your Steam App ID. 480 is Spacewar (test).")]
        [SerializeField] private uint steamAppId = 480;

        [Tooltip("If true, calls SteamAPI.RestartAppIfNecessary(steamAppId) before init.")]
        [SerializeField] private bool restartAppIfNecessary = true;
#endif

#if STEAMWORKS_NET
        private bool _didShutdown;
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
            TryInitializeSteam();
#else
            Debug.LogWarning("SteamBootstrap: STEAMWORKS_NET not defined. Steam features disabled.");
            IsInitialized = false;
#endif
        }

#if STEAMWORKS_NET
        private void TryInitializeSteam()
        {
            EnsureSteamAppIdFile();

            if (restartAppIfNecessary)
            {
                // If we're not started via Steam, this will relaunch via Steam and we should quit.
                // In the Editor this may be noisy; toggle restartAppIfNecessary off if needed.
                if (SteamAPI.RestartAppIfNecessary(new AppId_t(steamAppId)))
                {
                    Debug.LogWarning("SteamBootstrap: RestartAppIfNecessary returned true. Quitting so Steam can relaunch the app.");
                    Application.Quit();
                    return;
                }
            }

            // Steamworks.NET recommends checking if Steam is running first.
            if (!SteamAPI.IsSteamRunning())
            {
                Debug.LogWarning("SteamBootstrap: Steam is not running.");
                IsInitialized = false;
                if (quitIfSteamNotAvailable) Application.Quit();
                return;
            }

            try
            {
                IsInitialized = SteamAPI.Init();
            }
            catch (System.DllNotFoundException e)
            {
                Debug.LogError($"SteamBootstrap: Steamworks native DLL not found.\n{e}");
                IsInitialized = false;
            }

            if (!IsInitialized)
            {
                Debug.LogWarning(
                    "SteamBootstrap: SteamAPI.Init failed.\n" +
                    "- Make sure Steam is running and you are logged in.\n" +
                    "- Make sure steam_appid.txt exists next to the running executable (and for the Editor, often the current working directory).\n" +
                    "- Make sure steam_api64.dll is being loaded (correct platform import settings)."
                );
                if (quitIfSteamNotAvailable) Application.Quit();
            }
        }

        private void EnsureSteamAppIdFile()
        {
            // Steam reads steam_appid.txt from the process working directory / executable directory.
            // In Unity Editor this can vary, so we attempt to ensure a copy exists in a couple common locations.
            var appIdText = steamAppId.ToString();

            TryWriteAppIdFile(Directory.GetCurrentDirectory(), appIdText);

            // Project root (one level above Assets)
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(projectRoot))
            {
                TryWriteAppIdFile(projectRoot, appIdText);
            }
        }

        private static void TryWriteAppIdFile(string dir, string appIdText)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) return;
                var path = Path.Combine(dir, "steam_appid.txt");

                if (File.Exists(path))
                {
                    // Keep existing file; only fix if empty.
                    var existing = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrEmpty(existing)) return;
                }

                File.WriteAllText(path, appIdText);
            }
            catch
            {
                // Non-fatal. Init will still attempt and log failure reasons.
            }
        }
#endif

        private void Update()
        {
#if STEAMWORKS_NET
            if (!IsInitialized) return;
            SteamAPI.RunCallbacks();
#endif
        }

        private void OnApplicationQuit()
        {
#if STEAMWORKS_NET
            ShutdownSteam();
#endif
        }

        private void OnDestroy()
        {
#if STEAMWORKS_NET
            ShutdownSteam();
#endif
            if (Instance == this) Instance = null;
        }

#if STEAMWORKS_NET
        private void ShutdownSteam()
        {
            if (_didShutdown) return;
            if (!IsInitialized) return;

            _didShutdown = true;
            SteamAPI.Shutdown();
            IsInitialized = false;
        }
#endif
    }
}

