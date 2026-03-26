using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class GravitySimulationService
        {
            readonly WorldServiceContext _ctx;

            public GravitySimulationService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public void StepGravityAt(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return;

                var s = _ctx.WorldMap.GetSolid(x, y);
                ushort id = s.id;
                if (id == 0) return;

                if (!_ctx.HasGravity(id)) return;

                int by = y - 1;
                if (by < 0) return;

                if (_ctx.WorldMap.GetSolid(x, by).id != 0) return;

                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                _ctx.WorldMap.SetSolid(x, y, 0, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: true);
                _ctx.OnCellEdited(x, y);

                var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
                var spr = _ctx.CellLibrary.GetSolidSprite(id, s.meta);

                var fb = Object.Instantiate(_ctx.FallingBlockPrefab, pos, Quaternion.identity);
                fb.Init(id, _ctx.Owner, spr);

                _ctx.EntityManager.Register(fb);

                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId: id, oldSolidMeta: s.meta, oldFluidId: oldFluidId);
            }
        }
    }
}
