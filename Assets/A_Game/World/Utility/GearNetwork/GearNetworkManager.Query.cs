


using UnityEngine;

namespace Game.World
{
    public sealed partial class GearNetworkManager
    {
        
        public bool IsGearOccupiedCell(Vector2Int cell)
        {
            if (IsGearCenterCell(cell))

                return true;

            return _gearNodeIdsByOccupiedCell.TryGetValue(cell, out var set) && set != null && set.Count > 0;
        }

        
        public bool TryGetGearNodeIdAtCell(Vector2Int anyGearCell, out int gearNodeId)
        {
            gearNodeId = -1;

            if (IsGearCenterCell(anyGearCell))
                return _gearCenterToNodeId.TryGetValue(anyGearCell, out gearNodeId);

            if (!_gearNodeIdsByOccupiedCell.TryGetValue(anyGearCell, out var set) || set == null || set.Count != 1)
                return false;

            foreach (var nodeId in set)
            {
                gearNodeId = nodeId;
                return true;
            }

            return false;
        }

        
        public bool HasGearOccupiedVisualAt(Vector2Int cell)
        {
            return _gearNodeIdsByOccupiedCell.TryGetValue(cell, out var set) && set != null && set.Count > 0;
        }
    }
}
