using System.Collections.Generic;
using UnityEngine;

namespace Beastbane.Map
{
    public class MapVisualizer : MonoBehaviour
    {
        [Header("References")]
        public MapGenerator Generator;

        [Header("Node Sprites — assign one per type (index 0–5)")]
        public Sprite[] NodeTypeSprites = new Sprite[MapNode.TypeCount];
        public Sprite StartSprite;
        public Sprite BossSprite;

        [Header("Node Settings")]
        public float NodeScale = 1f;
        public int NodeSortingOrder = 10;

        [Header("Path Settings (dashed line)")]
        public float DashLength = 0.3f;
        public float GapLength = 0.2f;
        public float PathWidth = 0.08f;
        public Color PathColor = new Color(0.85f, 0.8f, 0.55f, 0.8f);
        public int PathSortingOrder = 5;

        Transform _nodesParent;
        Transform _pathsParent;

        void Start()
        {
            if (Generator == null)
                Generator = GetComponent<MapGenerator>();
        }

        void LateUpdate()
        {
            // Wait for MapGenerator to finish generating, then build visuals once
            if (_built) return;
            if (Generator == null || Generator.Map == null) return;

            BuildVisuals(Generator.Map);
            _built = true;
        }

        bool _built;

        public void BuildVisuals(MapData map)
        {
            ClearVisuals();

            _nodesParent = new GameObject("MapNodes").transform;
            _nodesParent.SetParent(transform);
            _nodesParent.localPosition = Vector3.zero;

            _pathsParent = new GameObject("MapPaths").transform;
            _pathsParent.SetParent(transform);
            _pathsParent.localPosition = Vector3.zero;

            var allNodes = map.AllNodes();
            var drawnEdges = new HashSet<string>();

            // Spawn node sprites
            foreach (var node in allNodes)
            {
                CreateNodeVisual(node);
            }

            // Spawn dashed-line paths
            foreach (var node in allNodes)
            {
                foreach (string targetId in node.ConnectionIds)
                {
                    string edgeKey = node.Id.CompareTo(targetId) < 0
                        ? $"{node.Id}-{targetId}" : $"{targetId}-{node.Id}";
                    if (drawnEdges.Contains(edgeKey)) continue;
                    drawnEdges.Add(edgeKey);

                    var target = map.GetNodeById(targetId);
                    if (target == null) continue;

                    CreateDashedPath(node.Position, target.Position);
                }
            }
        }

        void CreateNodeVisual(MapNode node)
        {
            var go = new GameObject($"Node_{node.Id}");
            go.transform.SetParent(_nodesParent);
            go.transform.localPosition = new Vector3(node.Position.x, node.Position.y, 0f);

            float scale = NodeScale;
            if (node.IsBoss) scale *= 2.5f;
            else if (node.IsStart) scale *= 1.5f;
            go.transform.localScale = Vector3.one * scale;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = NodeSortingOrder;

            if (node.IsStart && StartSprite != null)
                sr.sprite = StartSprite;
            else if (node.IsBoss && BossSprite != null)
                sr.sprite = BossSprite;
            else if (NodeTypeSprites != null && node.NodeType < NodeTypeSprites.Length && NodeTypeSprites[node.NodeType] != null)
                sr.sprite = NodeTypeSprites[node.NodeType];
            else
            {
                // Fallback: use a white circle sprite so node is visible
                sr.sprite = GetWhitePixelSprite();
                sr.color = GetFallbackColor(node);
            }
        }

        Color GetFallbackColor(MapNode node)
        {
            if (node.IsStart) return Color.green;
            if (node.IsBoss) return Color.red;
            // Distinct colors per type as placeholder
            switch (node.NodeType)
            {
                case 0: return new Color(1f, 0.8f, 0.2f);   // gold
                case 1: return new Color(0.4f, 0.7f, 1f);   // blue
                case 2: return new Color(0.3f, 0.9f, 0.3f);  // green
                case 3: return new Color(0.9f, 0.3f, 0.3f);  // red
                case 4: return new Color(0.8f, 0.5f, 1f);   // purple
                case 5: return new Color(1f, 1f, 1f);       // white
                default: return Color.gray;
            }
        }

        void CreateDashedPath(Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            float totalLength = direction.magnitude;
            Vector2 dir = direction.normalized;
            float segmentLength = DashLength + GapLength;

            float traveled = 0f;
            int dashIndex = 0;

            while (traveled < totalLength)
            {
                float dashStart = traveled;
                float dashEnd = Mathf.Min(traveled + DashLength, totalLength);

                if (dashEnd > dashStart + 0.01f)
                {
                    Vector2 segStart = from + dir * dashStart;
                    Vector2 segEnd = from + dir * dashEnd;
                    Vector2 mid = (segStart + segEnd) / 2f;
                    float len = (dashEnd - dashStart);

                    var dashGo = new GameObject($"Dash_{dashIndex++}");
                    dashGo.transform.SetParent(_pathsParent);
                    dashGo.transform.localPosition = new Vector3(mid.x, mid.y, 0f);

                    // Rotate to face direction
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    dashGo.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

                    // Scale: x = dash length, y = width
                    dashGo.transform.localScale = new Vector3(len, PathWidth, 1f);

                    var sr = dashGo.AddComponent<SpriteRenderer>();
                    sr.sprite = GetWhitePixelSprite();
                    sr.color = PathColor;
                    sr.sortingOrder = PathSortingOrder;
                }

                traveled += segmentLength;
            }
        }

        static Sprite _whitePixel;
        static Sprite GetWhitePixelSprite()
        {
            if (_whitePixel == null)
            {
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _whitePixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }
            return _whitePixel;
        }

        public void ClearVisuals()
        {
            if (_nodesParent != null) DestroyImmediate(_nodesParent.gameObject);
            if (_pathsParent != null) DestroyImmediate(_pathsParent.gameObject);
        }
    }
}
