


using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class TickSimulationService
        {

            readonly WorldServiceContext _ctx;

            
            public TickSimulationService(WorldServiceContext context)
            {
                _ctx = context;
            }

            
            public void EnqTick(int x, int y)
            {
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return;

                if (_ctx.TickNext.Add(new Vector2Int(x, y)))
                {
                    _ctx.RecalculateLightAt(x, y);
                }
            }

            
            public void OnCellEdited(int gx, int gy)
            {
                if ((uint)gx >= (uint)_ctx.Width || (uint)gy >= (uint)_ctx.Height) return;

                EnqTick(gx, gy);
                EnqTick(gx + 1, gy);
                EnqTick(gx - 1, gy);
                EnqTick(gx, gy + 1);
                EnqTick(gx, gy - 1);
            }

            
            public void StepTick()
            {
                if (_ctx.TickCurrent.Count == 0) SwapTickBuffers();
                if (_ctx.TickCurrent.Count == 0) return;

                foreach (var p in _ctx.TickCurrent)
                {
                    StepAttachmentAt(p.x, p.y);
                    _ctx.GravitySimulationService.StepGravityAt(p.x, p.y);
                    _ctx.FluidSimulationService.StepFluidAt(p.x, p.y);
                }
                _ctx.TickCurrent.Clear();
            }

            
            void SwapTickBuffers()
            {
                var t = _ctx.TickCurrent;
                _ctx.TickCurrent = _ctx.TickNext;
                _ctx.TickNext = t;
                _ctx.TickNext.Clear();
            }

            
            void StepAttachmentAt(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return;

                var s = _ctx.WorldMap.GetSolid(x, y);
                if (s.id == 0) return;

                if (!_ctx.CellLibrary.GetAttachedAt(s.id, s.meta, out string attachedAt))
                    return;

                if (attachedAt == "BG")
                {
                    if (_ctx.WorldMap.GetBG(x, y) == 0)
                        _ctx.BreakSolid(x, y);
                    return;
                }

                int sx = x;
                int sy = y;

                switch (attachedAt)
                {
                    case "Down": sy = y - 1; break;
                    case "Up": sy = y + 1; break;
                    case "Left": sx = x - 1; break;
                    case "Right": sx = x + 1; break;
                    default:
                        throw new System.Exception($"[Attachment] Unknown attachedAt='{attachedAt}' (solidId={s.id}, meta={s.meta})");
                }

                if (!_ctx.WorldMap.InBounds(sx, sy))
                {
                    _ctx.BreakSolid(x, y);
                    return;
                }

                if (_ctx.WorldMap.GetSolid(sx, sy).id == 0)
                    _ctx.BreakSolid(x, y);
            }
        }
    }
}
