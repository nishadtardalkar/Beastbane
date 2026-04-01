using System;
using System.Collections.Generic;
using Beastbane.Map;
using Mirror;
using UnityEngine;

namespace Beastbane.Netcode
{
    /// <summary>
    /// Central tracker for every player's current map node.
    /// Attach to a network-spawned object (e.g. the MapSpawner prefab).
    /// Also manages per-player sprites positioned over their current node.
    /// </summary>
    public class PlayerMapState : NetworkBehaviour
    {
        [Header("Player Visuals")]
        [SerializeField] private Sprite _playerSprite;
        [SerializeField] private float _playerSpriteScale = 0.6f;
        [SerializeField] private int _playerSortingOrder = 20;
        [SerializeField] private float _stackOffset = 0.4f;

        private static readonly Color[] PlayerColors =
        {
            Color.cyan, Color.yellow, Color.magenta, Color.green,
            new(1f, 0.5f, 0f), new(0.5f, 0.5f, 1f)
        };

        /// <summary>Maps connectionId -> current MapNode.Id</summary>
        private readonly SyncDictionary<int, string> _playerNodes = new();

        private readonly Dictionary<int, GameObject> _playerSpriteObjects = new();

        private MapVisualizer _visualizer;
        private MapGenerator _mapGenerator;

        /// <summary>Fires on all clients: (connectionId, oldNodeId, newNodeId)</summary>
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

            foreach (var kvp in _playerNodes)
            {
                PlayerNodeChanged?.Invoke(kvp.Key, string.Empty, kvp.Value);
                UpdatePlayerSprite(kvp.Key, kvp.Value);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            _playerNodes.OnChange -= OnDictChanged;
            ClearAllSprites();
        }

        public string GetNodeId(int connectionId) =>
            _playerNodes.TryGetValue(connectionId, out var nodeId) ? nodeId : null;

        public int PlayerCount => _playerNodes.Count;

        /// <summary>Iterate all (connectionId, nodeId) pairs.</summary>
        public IEnumerable<KeyValuePair<int, string>> AllPlayers() => _playerNodes;

        /// <summary>Server sets a player's node (e.g. initial placement).</summary>
        [Server]
        public void SetNode(int connectionId, string nodeId)
        {
            string oldId = _playerNodes.TryGetValue(connectionId, out var prev) ? prev : string.Empty;
            _playerNodes[connectionId] = nodeId;
            PlayerNodeChanged?.Invoke(connectionId, oldId, nodeId);
            UpdatePlayerSprite(connectionId, nodeId);
        }

        /// <summary>Owning client requests a move via the server.</summary>
        [Command(requiresAuthority = false)]
        public void CmdRequestMove(string nodeId, NetworkConnectionToClient sender = null)
        {
            if (sender == null) return;
            SetNode(sender.connectionId, nodeId);
        }

        /// <summary>Server removes a player (e.g. on disconnect).</summary>
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
                    // value = old node id for OP_SET
                    PlayerNodeChanged?.Invoke(connectionId, value, _playerNodes[connectionId]);
                    UpdatePlayerSprite(connectionId, _playerNodes[connectionId]);
                    break;
                case SyncIDictionary<int, string>.Operation.OP_REMOVE:
                    // value = old node id for OP_REMOVE
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
