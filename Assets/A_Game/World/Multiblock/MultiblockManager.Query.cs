


using UnityEngine;

namespace Game.World
{
    public partial class MultiblockManager
    {
        
        public Multiblock GetAtCell(Vector2Int cell)
            => _queryService.GetAtCell(cell);

        
        public void ApplyMetaToAllOccupiedCells(Multiblock owner, ushort targetMeta)
            => _queryService.ApplyMetaToAllOccupiedCells(owner, targetMeta);
    }
}
