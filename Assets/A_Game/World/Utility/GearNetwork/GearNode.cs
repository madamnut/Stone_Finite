// GearNode.cs (?ÑÏ≤¥ ÍµêÏ≤¥Î≥?
// Î≥ÄÍ≤?
// - ?êÏú†Í≥µÍ∞Ñ(OccupiedCells) ?úÍ±∞ (Utility ?àÏù¥?¥Í? ?¥Îãπ)

using UnityEngine;
using System.Collections.Generic;


namespace Game.World
{
    public sealed class GearNode
    {
        public enum GearSize { Small, Big }
        public enum RotationDir { CW, CCW }
    
        public int NodeId { get; private set; }
    
        public Vector2Int Center { get; private set; }
        public GearSize Size { get; private set; }
        public int MaxRpm { get; private set; }
        public IReadOnlyList<Vector2Int> OccupiedCells => _occupiedCells;
    
        public RotationDir Dir { get; set; }
        public int Rpm { get; set; }
    
        readonly List<Vector2Int> _occupiedCells;
    
        public GearNode(
            int nodeId,
            Vector2Int center,
            GearSize size,
            int maxRpm
        )
        {
            NodeId = nodeId;
            Center = center;
            Size = size;
            MaxRpm = maxRpm;
            _occupiedCells = BuildOccupiedCells(center, size);
    
            Dir = RotationDir.CW;
            Rpm = 0;
        }
    
        static List<Vector2Int> BuildOccupiedCells(Vector2Int center, GearSize size)
        {
            if (size != GearSize.Big)
                return new List<Vector2Int>(0);
    
            return new List<Vector2Int>
            {
                center + Vector2Int.up,
                center + Vector2Int.down,
                center + Vector2Int.left,
                center + Vector2Int.right
            };
        }
    }
}
