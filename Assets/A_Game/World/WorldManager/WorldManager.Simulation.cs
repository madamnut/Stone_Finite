using UnityEngine;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        public void EnqTick(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
    
            if (tickNext.Add(new Vector2Int(x, y)))
            {
                RecalculateLightAt(x, y);
            }
        }
    
        public void OnCellEdited(int gx, int gy)
        {
            if ((uint)gx >= (uint)W || (uint)gy >= (uint)H) return;
    
            EnqTick(gx, gy);
            EnqTick(gx + 1, gy);
            EnqTick(gx - 1, gy);
            EnqTick(gx, gy + 1);
            EnqTick(gx, gy - 1);
        }
    
        private void SwapTickBuffers()
        {
            var t = tickCurr;
            tickCurr = tickNext;
            tickNext = t;
            tickNext.Clear();
        }
    
        void StepTick()
        {
            if (tickCurr.Count == 0) SwapTickBuffers();
            if (tickCurr.Count == 0) return;
    
            foreach (var p in tickCurr)
            {
                StepAttachmentAt(p.x, p.y);
                StepGravityAt(p.x, p.y);
                StepFluidAt(p.x, p.y);
            }
            tickCurr.Clear();
        }
    
        void StepAttachmentAt(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return;
    
            var s = worldMap.GetSolid(x, y);
            if (s.id == 0) return;
    
            if (!cellLibrary.GetAttachedAt(s.id, s.meta, out string attachedAt))
                return;
    
            if (attachedAt == "BG")
            {
                if (worldMap.GetBG(x, y) == 0)
                    BreakSolid(x, y);
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
    
            if (!worldMap.InBounds(sx, sy))
            {
                BreakSolid(x, y);
                return;
            }
    
            if (worldMap.GetSolid(sx, sy).id == 0)
                BreakSolid(x, y);
        }
    
        void DoRandomTicks()
        {
            if (!Application.isPlaying) return;
            if (randomTicksPerWorldTick <= 0) return;
    
            Vector3 p = player.position;
            int pcx = Mathf.FloorToInt(p.x / ChunkSize);
            int pcy = Mathf.FloorToInt(p.y / ChunkSize);
    
            int r = ChunkRadius;
    
            int cxMin = pcx - r;
            int cxMax = pcx + r;
            int cyMin = pcy - r;
            int cyMax = pcy + r;
    
            int xMin = cxMin * ChunkSize;
            int xMax = (cxMax + 1) * ChunkSize;
            int yMin = cyMin * ChunkSize;
            int yMax = (cyMax + 1) * ChunkSize;
    
            if (xMin < 0) xMin = 0;
            if (yMin < 0) yMin = 0;
            if (xMax > W) xMax = W;
            if (yMax > H) yMax = H;
    
            if (xMin >= xMax || yMin >= yMax) return;
        }
    
        void StepFluidAt(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return;
    
            var l = worldMap.GetFluid(x, y);
            ushort fluidId = l.id;
            int amt = l.amount;
    
            if (amt <= 0)
            {
                if (fluidId != 0)
                {
                    SetFluidInternal(x, y, 0, 0);
                    OnCellEdited(x, y);
                }
                return;
            }
            if (fluidId == 0)
            {
                SetFluidInternal(x, y, 0, 0);
                OnCellEdited(x, y);
                return;
            }
    
            bool Blocked(int gx, int gy)
            {
                if (!worldMap.InBounds(gx, gy)) return true;
                return IsCollidable(gx, gy);
            }
    
            int dy = y - 1;
            if (dy >= 0 && !Blocked(x, dy))
            {
                var below = worldMap.GetFluid(x, dy);
                if (below.amount > 0 && below.id != 0 && below.id != fluidId)
                    return;
    
                int belowAmt = below.amount;
                int cap = WorldData.MaxFluid - belowAmt;
                if (cap > 0)
                {
                    int move = Mathf.Min(amt, cap);
                    MoveFluidInternal(x, y, x, dy, fluidId, move);
                    OnCellEdited(x, y);
                    OnCellEdited(x, dy);
                    return;
                }
            }
    
            int xl = x - 1, xr = x + 1;
            bool canL = xl >= 0 && !Blocked(xl, y);
            bool canR = xr < W && !Blocked(xr, y);
    
            int Al = 0, Ar = 0;
    
            if (canL)
            {
                var c = worldMap.GetFluid(xl, y);
                if (c.amount > 0 && c.id != 0 && c.id != fluidId) canL = false;
                else Al = c.amount;
            }
            if (canR)
            {
                var c = worldMap.GetFluid(xr, y);
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
    
            OnCellEdited(x, y);
            if (takeL > 0) OnCellEdited(xl, y);
            if (takeR > 0) OnCellEdited(xr, y);
        }
    
        void SetFluidInternal(int x, int y, ushort id, int newAmount)
        {
            var oldS = worldMap.GetSolid(x, y);
            ushort oldSolidId = oldS.id;
            ushort oldSolidMeta = oldS.meta;
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            newAmount = Mathf.Clamp(newAmount, 0, WorldData.MaxFluid);
    
            if (IsCollidable(x, y) || id == 0 || newAmount == 0)
                worldMap.SetFluid(x, y, 0, 0);
            else
                worldMap.SetFluid(x, y, id, (byte)newAmount);
    
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
            HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
        }
    
        void MoveFluidInternal(int fx, int fy, int tx, int ty, ushort id, int amount)
        {
            if (amount <= 0) return;
    
            var from = worldMap.GetFluid(fx, fy);
            var to = worldMap.GetFluid(tx, ty);
    
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
    
        void StepGravityAt(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return;
    
            var s = worldMap.GetSolid(x, y);
            ushort id = s.id;
            if (id == 0) return;
    
            if (!HasGravity(id)) return;
    
            int by = y - 1;
            if (by < 0) return;
    
            if (worldMap.GetSolid(x, by).id != 0) return;
    
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            worldMap.SetSolid(x, y, 0, 0);
    
            MarkChunkDirty(x, y, markSolid: true);
            OnCellEdited(x, y);
    
            var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
            var spr = cellLibrary.GetSolidSprite(id, s.meta);
    
            var fb = Instantiate(fallingBlockPrefab, pos, Quaternion.identity);
            fb.Init(id, this, spr);
    
            entityManager.Register(fb);
    
            HandleSourceLightChangeAt(x, y, oldSolidId: id, oldSolidMeta: s.meta, oldFluidId: oldFluidId);
        }
    }
}
