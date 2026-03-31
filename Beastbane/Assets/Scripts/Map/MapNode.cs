using System.Collections.Generic;
using UnityEngine;

namespace Beastbane.Map
{
    [System.Serializable]
    public class MapNode
    {
        public const int TypeCount = 6;

        public string Id;
        public int Ring;
        public int NodeType; // 0-5, to be mapped to specific encounter types later
        public bool IsStart;
        public bool IsBoss;
        public Vector2 Position;
        public List<string> ConnectionIds = new List<string>();

        public MapNode(string id, int ring)
        {
            Id = id;
            Ring = ring;
        }

        public void AddConnection(string targetId)
        {
            if (!ConnectionIds.Contains(targetId))
                ConnectionIds.Add(targetId);
        }
    }
}
