using System.Collections.Generic;
using UnityEngine;

namespace Beastbane.Map
{
    public class MapGenerator : MonoBehaviour
    {
        [Header("Map Structure")]
        [Tooltip("Number of rings radiating out from the center (not counting start).")]
        public int RingCount = 4;

        [Tooltip("Desired spacing between nodes along a ring's circumference. Lower = denser map.")]
        public float NodeDensity = 6f;

        [Tooltip("Minimum nodes any ring can have (inner ring floor).")]
        public int MinNodesPerRing = 3;

        [Tooltip("Seed for reproducible generation. -1 = random.")]
        public int Seed = -1;

        [Tooltip("If true, Start() will not auto-generate. MapSeedNetworkSync will drive generation once the host seed arrives.")]
        public bool waitForNetworkSeed = false;

        [Header("Organic Layout")]
        [Tooltip("Base distance between rings.")]
        public float RingSpacing = 4f;

        [Tooltip("How much the node can jitter radially within its sector.")]
        public float RadialJitter = 1.2f;

        [Tooltip("How much the node can jitter angularly within its sector (0-1, fraction of sector width).")]
        [Range(0f, 0.45f)]
        public float AngularJitterFraction = 0.35f;

        [Header("Connections")]
        [Tooltip("Min connections per node to nearest neighbours.")]
        public int MinConnections = 2;

        [Tooltip("Max connections per node to nearest neighbours.")]
        public int MaxConnections = 3;

        [Tooltip("Max distance to consider for connections (in world units).")]
        public float MaxConnectionDistance = 10f;

        public MapData Map { get; private set; }

        void Start()
        {
            if (!waitForNetworkSeed)
                GenerateMap();
        }

        public MapData GenerateMap()
        {
            int activeSeed = Seed == -1 ? System.Environment.TickCount : Seed;
            var rng = new System.Random(activeSeed);

            Map = new MapData { Seed = activeSeed };
            int nodeCounter = 0;

            // --- Center start node ---
            var startNode = new MapNode($"node_{nodeCounter++}", 0)
            {
                IsStart = true,
                Position = Vector2.zero
            };
            Map.StartNode = startNode;

            // --- Place nodes using sector division per ring ---
            for (int r = 1; r <= RingCount; r++)
            {
                // Auto-calculate node count from ring circumference and desired density
                float circumference = 2f * Mathf.PI * r * RingSpacing;
                int sectorCount = Mathf.Max(MinNodesPerRing, Mathf.RoundToInt(circumference / NodeDensity));

                float sectorAngle = 360f / sectorCount;   // degrees per sector
                float baseRadius = r * RingSpacing;

                // Random rotation offset so sectors don't align ring-to-ring
                float ringRotation = (float)(rng.NextDouble() * 360.0);

                var ringNodes = new List<MapNode>();

                for (int s = 0; s < sectorCount; s++)
                {
                    var node = new MapNode($"node_{nodeCounter++}", r);
                    node.NodeType = rng.Next(0, MapNode.TypeCount);

                    // Sector center angle
                    float sectorCenter = ringRotation + sectorAngle * s;

                    // Jitter within sector bounds
                    float maxAngularOffset = sectorAngle * AngularJitterFraction;
                    float angleOffset = (float)(rng.NextDouble() * 2 - 1) * maxAngularOffset;
                    float finalAngle = (sectorCenter + angleOffset) * Mathf.Deg2Rad;

                    // Jitter radius
                    float radiusOffset = (float)(rng.NextDouble() * 2 - 1) * RadialJitter;
                    float finalRadius = Mathf.Max(baseRadius + radiusOffset, RingSpacing * 0.5f);

                    node.Position = new Vector2(
                        Mathf.Cos(finalAngle) * finalRadius,
                        Mathf.Sin(finalAngle) * finalRadius
                    );

                    ringNodes.Add(node);
                }

                Map.Rings.Add(ringNodes);
            }

            // --- Pick a random node in the last 3 rings as the boss ---
            int bossRingMin = Mathf.Max(0, Map.Rings.Count - 3);
            int bossRingIndex = rng.Next(bossRingMin, Map.Rings.Count);
            var bossRing = Map.Rings[bossRingIndex];
            int bossIndex = rng.Next(0, bossRing.Count);
            bossRing[bossIndex].IsBoss = true;
            Map.BossNode = bossRing[bossIndex];

            // --- Connect ring by ring, outward from start ---

            // Start → ring 0: connect to ALL ring-0 nodes (creates initial branches)
            var ring0 = Map.Rings[0];
            foreach (var r0Node in ring0)
            {
                startNode.AddConnection(r0Node.Id);
                r0Node.AddConnection(startNode.Id);
            }

            // Ring i → ring i+1: each node connects to its 2-3 nearest in the next ring
            for (int r = 0; r < Map.Rings.Count - 1; r++)
            {
                var currentRing = Map.Rings[r];
                var nextRing = Map.Rings[r + 1];
                var connectedInNext = new HashSet<string>();

                foreach (var node in currentRing)
                {
                    // Sort next ring by distance from this node
                    var sorted = new List<MapNode>(nextRing);
                    sorted.Sort((a, b) =>
                        Vector2.Distance(node.Position, a.Position)
                            .CompareTo(Vector2.Distance(node.Position, b.Position)));

                    int count = rng.Next(MinConnections, MaxConnections + 1);
                    count = Mathf.Min(count, sorted.Count);

                    for (int i = 0; i < count; i++)
                    {
                        if (Vector2.Distance(node.Position, sorted[i].Position) <= MaxConnectionDistance)
                        {
                            node.AddConnection(sorted[i].Id);
                            sorted[i].AddConnection(node.Id);
                            connectedInNext.Add(sorted[i].Id);
                        }
                    }
                }

                // Ensure every next-ring node has at least one incoming connection
                foreach (var nextNode in nextRing)
                {
                    if (connectedInNext.Contains(nextNode.Id)) continue;

                    // Find closest node in current ring
                    MapNode closest = currentRing[0];
                    float bestDist = float.MaxValue;
                    foreach (var cNode in currentRing)
                    {
                        float d = Vector2.Distance(cNode.Position, nextNode.Position);
                        if (d < bestDist) { bestDist = d; closest = cNode; }
                    }
                    closest.AddConnection(nextNode.Id);
                    nextNode.AddConnection(closest.Id);
                }
            }

            // Same-ring connections: occasionally link adjacent sector neighbours
            for (int r = 0; r < Map.Rings.Count; r++)
            {
                var ring = Map.Rings[r];
                for (int i = 0; i < ring.Count; i++)
                {
                    int next = (i + 1) % ring.Count;
                    float dist = Vector2.Distance(ring[i].Position, ring[next].Position);
                    // Only link if they're close enough (nearby sectors)
                    if (dist <= MaxConnectionDistance * 0.6f && rng.NextDouble() < 0.3)
                    {
                        ring[i].AddConnection(ring[next].Id);
                        ring[next].AddConnection(ring[i].Id);
                    }
                }
            }

            Debug.Log($"[MapGenerator] Organic map — seed: {activeSeed}, " +
                      $"{Map.AllNodes().Count} nodes, {RingCount} rings, boss: {Map.BossNode.Id}");

            return Map;
        }
    }
}
