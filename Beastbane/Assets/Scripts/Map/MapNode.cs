using System.Collections.Generic;
using UnityEngine;

namespace Beastbane.Map
{
    [System.Serializable]
    public class MapNode
    {
        public const int TypeCount = 6;

        public const int Combat = 0;
        public const int Elite = 1;
        public const int Rest = 2;
        public const int Shop = 3;
        public const int Event = 4;
        public const int Boss = 5;

        public string Id;
        public int Ring;
        public int NodeType;
        public bool IsStart;
        public bool IsBoss;
        public Vector2 Position;
        public List<string> ConnectionIds = new List<string>();

        public MapNode(string id, int ring)
        {
            Id = id;
            Ring = ring;
        }

        public bool IsCombatNode =>
            NodeType == Combat || NodeType == Elite || NodeType == Boss || IsBoss;

        public void AddConnection(string targetId)
        {
            if (!ConnectionIds.Contains(targetId))
                ConnectionIds.Add(targetId);
        }
    }
}
