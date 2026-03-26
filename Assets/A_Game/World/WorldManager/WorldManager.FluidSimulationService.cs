using UnityEngine;

using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class FluidSimulationService
        {
            readonly WorldServiceContext _ctx;

            public FluidSimulationService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public void StepFluidAt(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return;

                var l = _ctx.WorldMap.GetFluid(x, y);
                ushort fluidId = l.id;
                int amt = l.amount;

                if (amt <= 0)
                {
                    if (fluidId != 0)
                    {
                        SetFluidInternal(x, y, 0, 0);
                        _ctx.OnCellEdited(x, y);
                    }
                    return;
                }
                if (fluidId == 0)
                {
                    SetFluidInternal(x, y, 0, 0);
                    _ctx.OnCellEdited(x, y);
                    return;
                }

                bool Blocked(int gx, int gy)
                {
                    if (!_ctx.WorldMap.InBounds(gx, gy)) return true;
                    return _ctx.IsCollidable(gx, gy);
                }

                int dy = y - 1;
                if (dy >= 0 && !Blocked(x, dy))
                {
                    var below = _ctx.WorldMap.GetFluid(x, dy);
                    if (below.amount > 0 && below.id != 0 && below.id != fluidId)
                        return;

                    int belowAmt = below.amount;
                    int cap = WorldData.MaxFluid - belowAmt;
                    if (cap > 0)
                    {
                        int move = Mathf.Min(amt, cap);
                        MoveFluidInternal(x, y, x, dy, fluidId, move);
                        _ctx.OnCellEdited(x, y);
                        _ctx.OnCellEdited(x, dy);
                        return;
                    }
                }

                int xl = x - 1, xr = x + 1;
                bool canL = xl >= 0 && !Blocked(xl, y);
                bool canR = xr < _ctx.Width && !Blocked(xr, y);

                int Al = 0, Ar = 0;

                if (canL)
                {
                    var c = _ctx.WorldMap.GetFluid(xl, y);
                    if (c.amount > 0 && c.id != 0 && c.id != fluidId) canL = false;
                    else Al = c.amount;
                }
                if (canR)
                {
                    var c = _ctx.WorldMap.GetFluid(xr, y);
                    if (c.amount > 0 && c.id != 0 && c.id != fluidId) canR = false;
                    else Ar = c.amount;
                }

                int capL = canL ? (WorldData.MaxFluid - Al) : 0;
                int capR = canR ? (WorldData.MaxFluid - Ar) : 0;

                int flowL = 0, flowR = 0;

                if (canL)
                {
                    int diff = amt - Al;
                    if (diff > 0)
                    {
                        int prop = Mathf.Clamp(Mathf.Max(1, diff / 2), 1, 20);
                        flowL = Mathf.Min(prop, capL);
                    }
                }
                if (canR)
                {
                    int diff = amt - Ar;
                    if (diff > 0)
                    {
                        int prop = Mathf.Clamp(Mathf.Max(1, diff / 2), 1, 20);
                        flowR = Mathf.Min(prop, capR);
                    }
                }

                int want = flowL + flowR;
                if (want <= 0) return;

                int total = Mathf.Min(amt, want);

                int takeL = 0, takeR = 0;
                if (flowL > 0 && flowR > 0)
                {
                    int denom = flowL + flowR;
                    takeL = (total * flowL + denom / 2) / denom;
                    if (takeL > flowL) takeL = flowL;
                    takeR = total - takeL;
                    if (takeR > flowR) { takeR = flowR; takeL = total - takeR; }
                }
                else if (flowL > 0) takeL = Mathf.Min(total, flowL);
                else takeR = Mathf.Min(total, flowR);

                if (takeL > 0) MoveFluidInternal(x, y, xl, y, fluidId, takeL);
                if (takeR > 0) MoveFluidInternal(x, y, xr, y, fluidId, takeR);

                _ctx.OnCellEdited(x, y);
                if (takeL > 0) _ctx.OnCellEdited(xl, y);
                if (takeR > 0) _ctx.OnCellEdited(xr, y);
            }

            void SetFluidInternal(int x, int y, ushort id, int newAmount)
            {
                var oldS = _ctx.WorldMap.GetSolid(x, y);
                ushort oldSolidId = oldS.id;
                ushort oldSolidMeta = oldS.meta;
                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                newAmount = Mathf.Clamp(newAmount, 0, WorldData.MaxFluid);

                if (_ctx.IsCollidable(x, y) || id == 0 || newAmount == 0)
                    _ctx.WorldMap.SetFluid(x, y, 0, 0);
                else
                    _ctx.WorldMap.SetFluid(x, y, id, (byte)newAmount);

                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
            }

            void MoveFluidInternal(int fx, int fy, int tx, int ty, ushort id, int amount)
            {
                if (amount <= 0) return;

                var from = _ctx.WorldMap.GetFluid(fx, fy);
                var to = _ctx.WorldMap.GetFluid(tx, ty);

                if (from.amount <= 0 || from.id != id) return;
                if (to.amount > 0 && to.id != 0 && to.id != id) return;

                int fromAmt = from.amount;
                int toAmt = to.amount;

                int move = Mathf.Min(amount, fromAmt);
                move = Mathf.Min(move, WorldData.MaxFluid - toAmt);
                if (move <= 0) return;

                SetFluidInternal(fx, fy, id, fromAmt - move);
                SetFluidInternal(tx, ty, id, toAmt + move);
            }
        }
    }
}
