using UnityEngine;

namespace Game.World
{
    public partial class MultiblockManager
    {
        private sealed class MultiblockQueryService
        {
            readonly MultiblockServiceContext _ctx;

            public MultiblockQueryService(MultiblockServiceContext context)
            {
                _ctx = context;
            }

            public Multiblock GetAtCell(Vector2Int cell)
            {
                _ctx.ByCell.TryGetValue(cell, out var inst);
                return inst;
            }

            public void ApplyMetaToAllOccupiedCells(Multiblock owner, ushort targetMeta)
            {
                if (owner == null) return;
                if (_ctx.World == null) return;

                var cells = owner.OccupiedCells;
                if (cells == null) return;

                for (int i = 0; i < cells.Count; i++)
                {
                    var c = cells[i];
                    ushort id = _ctx.World.GetSolidId(c.x, c.y);
                    if (id == 0) continue;

                    _ctx.World.OverwriteSolid(c.x, c.y, id, targetMeta);
                }
            }
        }
    }
}
