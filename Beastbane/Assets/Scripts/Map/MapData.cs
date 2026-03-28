using System.Collections.Generic;

namespace Beastbane.Map
{
    [System.Serializable]
    public class MapData
    {
        public MapNode StartNode;
        public MapNode BossNode;
        public List<List<MapNode>> Rings = new List<List<MapNode>>();
        public int Seed;

        public MapNode GetNodeById(string id)
        {
            if (StartNode != null && StartNode.Id == id)
                return StartNode;

            for (int i = 0; i < Rings.Count; i++)
            {
                for (int j = 0; j < Rings[i].Count; j++)
                {
                    if (Rings[i][j].Id == id)
                        return Rings[i][j];
                }
            }
            return null;
        }

        public List<MapNode> AllNodes()
        {
            var all = new List<MapNode>();
            if (StartNode != null)
                all.Add(StartNode);

            for (int i = 0; i < Rings.Count; i++)
                all.AddRange(Rings[i]);

            return all;
        }
    }
}
