using System;
using System.Collections.Generic;
using Beastbane.Combat;
using Beastbane.Data;
using Beastbane.Map;
using Beastbane.UI;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Central tracker for every player's current map node.
    /// Attach to a network-spawned object (e.g. the MapSpawner prefab).
    /// Also manages per-player sprites positioned over their current node,
    /// and orchestrates combat start/end with CombatManager.
    /// </summary>
    public class PlayerMapState : NetworkBehaviour
    {
        [Header("Player Visuals")]
        [SerializeField] private Sprite _playerSprite;
        [SerializeField] private float _playerSpriteScale = 0.6f;
        [SerializeField] private int _playerSortingOrder = 20;
        [SerializeField] private float _stackOffset = 0.4f;

        [Header("Combat")]
        [SerializeField] private GameDatabase _db;
        [SerializeField] private string _combatSceneName = "CombatScene";

        private static readonly Color[] PlayerColors =
        {
            Color.cyan, Color.yellow, Color.magenta, Color.green,
            new(1f, 0.5f, 0f), new(0.5f, 0.5f, 1f)
        };

        private readonly SyncDictionary<int, string> _playerNodes = new();

        [SyncVar(hook = nameof(OnActiveSceneChanged))]
        private int _activeSceneIndex = -1;

        private readonly Dictionary<int, GameObject> _playerSpriteObjects = new();
        private bool _spritesNeedRefresh;

        [Tooltip("Name of the SceneSwitcher child to parent under.")]
        [SerializeField] private string _mapSceneName = "MapScene";

        private MapVisualizer _visualizer;
        private MapGenerator _mapGenerator;
        private SceneSwitcher _sceneSwitcher;
        private CombatManager _combatManager;

        public event Action<int, string, string> PlayerNodeChanged;

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.OnConnectedEvent += OnServerPlayerConnected;
            PlaceAllPlayersAtStart();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            NetworkServer.OnConnectedEvent -= OnServerPlayerConnected;
            UnsubscribeCombat();
        }

        [Server]
        private void OnServerPlayerConnected(NetworkConnectionToClient conn)
        {
            if (_mapGenerator == null)
                _mapGenerator = FindAnyObjectByType<MapGenerator>();
            if (_mapGenerator == null || _mapGenerator.Map == null) return;

            string startNodeId = _mapGenerator.Map.StartNode?.Id;
            if (string.IsNullOrEmpty(startNodeId)) return;

            SetNode(conn.connectionId, startNodeId);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _playerNodes.OnChange += OnDictChanged;
            _spritesNeedRefresh = true;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _playerNodes.OnChange -= OnDictChanged;
            ClearAllSprites();
        }

        private void LateUpdate()
        {
            if (!_spritesNeedRefresh) return;
            if (_playerNodes.Count == 0) return;

            var vis = FindVisualizer();
            if (vis == null || vis.NodeObjects.Count == 0) return;

            foreach (var kvp in _playerNodes)
                UpdatePlayerSprite(kvp.Key, kvp.Value);

            _spritesNeedRefresh = false;
        }

        public string GetNodeId(int connectionId) =>
            _playerNodes.TryGetValue(connectionId, out var nodeId) ? nodeId : null;

        public int PlayerCount => _playerNodes.Count;

        public IEnumerable<KeyValuePair<int, string>> AllPlayers() => _playerNodes;

        [Server]
        public void SetNode(int connectionId, string nodeId)
        {
            string oldId = _playerNodes.TryGetValue(connectionId, out var prev) ? prev : string.Empty;
            _playerNodes[connectionId] = nodeId;
            PlayerNodeChanged?.Invoke(connectionId, oldId, nodeId);
            UpdatePlayerSprite(connectionId, nodeId);
        }

        [Command(requiresAuthority = false)]
        public void CmdRequestMove(string nodeId, NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            SetNode(sender.connectionId, nodeId);
        }

        // ── Combat Integration ──────────────────────────────────────

        [Command(requiresAuthority = false)]
        public void CmdStartCombat(string nodeId, NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            ServerStartCombat(sender.connectionId, nodeId);
        }

        [Server]
        private void ServerStartCombat(int connectionId, string nodeId)
        {
            if (_mapGenerator == null)
                _mapGenerator = FindAnyObjectByType<MapGenerator>();
            if (_mapGenerator == null || _mapGenerator.Map == null) return;

            var node = _mapGenerator.Map.GetNodeById(nodeId);
            if (node == null || !node.IsCombatNode) return;

            SwitchAllToScene(_combatSceneName);

            var runData = FindPlayerRunData(connectionId);
            if (runData == null || !runData.HeroSelected)
            {
                Debug.LogWarning($"PlayerMapState: No PlayerRunData or hero not selected for conn {connectionId}. " +
                                 "Scene switched but combat not initialised.");
                return;
            }

            if (_combatManager == null)
                _combatManager = FindAnyObjectByType<CombatManager>(FindObjectsInactive.Include);
            if (_combatManager == null)
            {
                Debug.LogWarning("PlayerMapState: CombatManager not found. Scene switched but combat not initialised.");
                return;
            }

            int enemyIndex = PickEnemyForNode(node);
            SubscribeCombat();

            _combatManager.InitCombat(
                connectionId,
                runData.HeroIndex,
                runData.GetDeckCopy(),
                runData.CurrentHP,
                runData.MaxHP,
                runData.Energy,
                enemyIndex
            );

            Debug.Log($"PlayerMapState: Started combat for conn {connectionId} on node {nodeId}, enemy {enemyIndex}");
        }

        [Server]
        private void OnCombatEnded()
        {
            UnsubscribeCombat();
            ReturnToMap();
        }

        [Command(requiresAuthority = false)]
        public void CmdReturnToMap()
        {
            ReturnToMap();
        }

        [Server]
        private void ReturnToMap()
        {
            SwitchAllToScene(_mapSceneName);

            var turnState = FindAnyObjectByType<TurnState>();
            if (turnState != null)
                turnState.EndTurn();
        }

        [Server]
        private void SubscribeCombat()
        {
            if (_combatManager == null)
                _combatManager = FindAnyObjectByType<CombatManager>();
            if (_combatManager != null)
                _combatManager.CombatEnded += OnCombatEnded;
        }

        [Server]
        private void UnsubscribeCombat()
        {
            if (_combatManager != null)
                _combatManager.CombatEnded -= OnCombatEnded;
        }

        [Server]
        private int PickEnemyForNode(MapNode node)
        {
            if (_db == null || _db.enemies.Length == 0) return 0;

            // Boss nodes get the last enemy, elites get middle, combat gets random from first half
            if (node.IsBoss && _db.enemies.Length > 0)
                return _db.enemies.Length - 1;
            if (node.NodeType == MapNode.Elite && _db.enemies.Length > 1)
                return _db.enemies.Length / 2;

            return UnityEngine.Random.Range(0, Mathf.Max(1, _db.enemies.Length / 2));
        }

        [Server]
        private void SwitchAllToScene(string sceneName)
        {
            if (_sceneSwitcher == null)
                _sceneSwitcher = FindAnyObjectByType<SceneSwitcher>();
            if (_sceneSwitcher == null) return;

            int idx = _sceneSwitcher.GetSceneIndex(sceneName);
            if (idx < 0)
            {
                Debug.LogWarning($"PlayerMapState: SceneSwitcher has no child named '{sceneName}'.");
                return;
            }

            _activeSceneIndex = idx;
            SwitchLocalScene(idx);
        }

        // ── Existing methods ────────────────────────────────────────

        [Server]
        public void RemovePlayer(int connectionId)
        {
            _playerNodes.Remove(connectionId);
            DestroyPlayerSprite(connectionId);
        }

        private void OnDictChanged(SyncIDictionary<int, string>.Operation op, int connectionId, string value)
        {
            switch (op)
            {
                case SyncIDictionary<int, string>.Operation.OP_ADD:
                    PlayerNodeChanged?.Invoke(connectionId, string.Empty, _playerNodes[connectionId]);
                    UpdatePlayerSprite(connectionId, _playerNodes[connectionId]);
                    break;
                case SyncIDictionary<int, string>.Operation.OP_SET:
                    PlayerNodeChanged?.Invoke(connectionId, value, _playerNodes[connectionId]);
                    UpdatePlayerSprite(connectionId, _playerNodes[connectionId]);
                    break;
                case SyncIDictionary<int, string>.Operation.OP_REMOVE:
                    PlayerNodeChanged?.Invoke(connectionId, value, string.Empty);
                    DestroyPlayerSprite(connectionId);
                    break;
            }
        }

        private MapVisualizer FindVisualizer()
        {
            if (_visualizer == null)
                _visualizer = FindAnyObjectByType<MapVisualizer>();
            return _visualizer;
        }

        private void UpdatePlayerSprite(int connectionId, string nodeId)
        {
            var vis = FindVisualizer();
            if (vis == null) return;

            var nodeGo = vis.GetNodeObject(nodeId);
            if (nodeGo == null) return;

            if (!_playerSpriteObjects.TryGetValue(connectionId, out var spriteGo) || spriteGo == null)
            {
                spriteGo = new GameObject($"PlayerIcon_{connectionId}");
                spriteGo.transform.SetParent(transform, true);
                var sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = _playerSprite != null ? _playerSprite : GetFallbackSprite();
                sr.sortingOrder = _playerSortingOrder;
                sr.color = PlayerColors[connectionId % PlayerColors.Length];
                spriteGo.transform.localScale = Vector3.one * _playerSpriteScale;
                _playerSpriteObjects[connectionId] = spriteGo;
            }

            var nodePos = nodeGo.transform.position;
            int slot = GetStackSlot(connectionId, nodeId);
            var offset = new Vector3(_stackOffset * slot, _stackOffset * slot, 0f);
            spriteGo.transform.position = nodePos + offset;
        }

        private int GetStackSlot(int connectionId, string nodeId)
        {
            int slot = 0;
            foreach (var kvp in _playerNodes)
            {
                if (kvp.Key == connectionId) return slot;
                if (kvp.Value == nodeId) slot++;
            }
            return slot;
        }

        private void DestroyPlayerSprite(int connectionId)
        {
            if (!_playerSpriteObjects.TryGetValue(connectionId, out var go)) return;
            if (go != null) Destroy(go);
            _playerSpriteObjects.Remove(connectionId);
        }

        private void ClearAllSprites()
        {
            foreach (var kvp in _playerSpriteObjects)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            _playerSpriteObjects.Clear();
        }

        private void OnActiveSceneChanged(int oldIndex, int newIndex)
        {
            SwitchLocalScene(newIndex);
        }

        private void SwitchLocalScene(int sceneIndex)
        {
            if (_sceneSwitcher == null)
                _sceneSwitcher = FindAnyObjectByType<SceneSwitcher>();
            if (_sceneSwitcher == null) return;

            Debug.Log($"PlayerMapState: switching local scene to index {sceneIndex}");
            _sceneSwitcher.SwitchTo(sceneIndex);

            int mapIdx = _sceneSwitcher.GetSceneIndex(_mapSceneName);
            SetPlayerSpritesVisible(sceneIndex == mapIdx);
        }

        private void SetPlayerSpritesVisible(bool visible)
        {
            foreach (var kvp in _playerSpriteObjects)
            {
                if (kvp.Value != null)
                    kvp.Value.SetActive(visible);
            }
        }

        [Server]
        private void PlaceAllPlayersAtStart()
        {
            if (_mapGenerator == null)
                _mapGenerator = FindAnyObjectByType<MapGenerator>();

            if (_mapGenerator == null || _mapGenerator.Map == null) return;

            string startNodeId = _mapGenerator.Map.StartNode?.Id;
            if (string.IsNullOrEmpty(startNodeId)) return;

            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn != null)
                    SetNode(conn.connectionId, startNodeId);
            }

            Debug.Log($"PlayerMapState: placed {NetworkServer.connections.Count} player(s) at {startNodeId}");
        }

        private static PlayerRunData FindPlayerRunData(int connectionId)
        {
            foreach (var prd in FindObjectsByType<PlayerRunData>(FindObjectsSortMode.None))
            {
                if (prd.connectionToClient != null && prd.connectionToClient.connectionId == connectionId)
                    return prd;
            }
            return null;
        }

        private static Sprite _fallbackSprite;
        private static Sprite GetFallbackSprite()
        {
            if (_fallbackSprite == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return _fallbackSprite;
        }
    }
}
