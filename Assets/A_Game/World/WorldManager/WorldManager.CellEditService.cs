


using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.Data;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class CellEditService
        {

            readonly WorldServiceContext _ctx;

            
            public CellEditService(WorldServiceContext context)
            {
                _ctx = context;
            }

            
            public void OverwriteSolid(int x, int y, ushort newId, ushort newMeta = 0)
            {
                var cur = _ctx.WorldMap.GetSolid(x, y);
                ushort oldSolidId = cur.id;
                ushort oldSolidMeta = cur.meta;
                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                _ctx.WorldMap.SetSolid(x, y, newId, newMeta);

                if ((_ctx.CellLibrary.GetSolidFlags(newId) & CellLibrary.SolidFlags.Collidable) != 0)
                    _ctx.WorldMap.SetFluid(x, y, 0, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: true);
                _ctx.OnCellEdited(x, y);
                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
            }

            
            public bool PlaceSolid(int x, int y, ushort id)
                => PlaceSolid(x, y, id, RelV.Neutral, RelH.Neutral);

            
            public bool PlaceSolid(int x, int y, ushort id, RelV relV, RelH relH)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;
                if (id == 0) return false;

                var curS = _ctx.WorldMap.GetSolid(x, y);

                if (curS.id != 0)
                {
                    if (!_ctx.IsSupportSolid(x, y))
                        return false;

                    
                    bool TryNeighbor(int nx, int ny, RelV nRelV, RelH nRelH)
                    {
                        if (!_ctx.WorldMap.InBounds(nx, ny)) return false;
                        if (_ctx.WorldMap.GetSolid(nx, ny).id != 0) return false;

                        return PlaceSolidAtEmpty(nx, ny, id, nRelV, nRelH);
                    }

                    if (relH == RelH.Left)
                    {
                        if (TryNeighbor(x + 1, y, RelV.Neutral, RelH.Left)) return true;
                        if (TryNeighbor(x - 1, y, RelV.Neutral, RelH.Right)) return true;
                    }
                    
                    else if (relH == RelH.Right)
                    {
                        if (TryNeighbor(x - 1, y, RelV.Neutral, RelH.Right)) return true;
                        if (TryNeighbor(x + 1, y, RelV.Neutral, RelH.Left)) return true;
                    }
                    else
                    {
                        if (TryNeighbor(x - 1, y, RelV.Neutral, RelH.Right)) return true;
                        if (TryNeighbor(x + 1, y, RelV.Neutral, RelH.Left)) return true;
                    }

                    if (relV == RelV.Up)
                    {
                        if (TryNeighbor(x, y - 1, RelV.Up, RelH.Neutral)) return true;
                        if (TryNeighbor(x, y + 1, RelV.Down, RelH.Neutral)) return true;
                    }
                    
                    else if (relV == RelV.Down)
                    {
                        if (TryNeighbor(x, y + 1, RelV.Down, RelH.Neutral)) return true;
                        if (TryNeighbor(x, y - 1, RelV.Up, RelH.Neutral)) return true;
                    }
                    else
                    {
                        if (TryNeighbor(x, y - 1, RelV.Up, RelH.Neutral)) return true;
                        if (TryNeighbor(x, y + 1, RelV.Down, RelH.Neutral)) return true;
                    }

                    return false;
                }

                return PlaceSolidAtEmpty(x, y, id, relV, relH);
            }

            
            public bool PlaceSolidExact(int x, int y, ushort id)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;
                if (id == 0) return false;

                if (_ctx.WorldMap.GetSolid(x, y).id != 0) return false;

                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                _ctx.WorldMap.SetSolid(x, y, id, 0);

                if ((_ctx.CellLibrary.GetSolidFlags(id) & CellLibrary.SolidFlags.Collidable) != 0)
                    _ctx.WorldMap.SetFluid(x, y, 0, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: true);
                _ctx.OnCellEdited(x, y);

                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId: 0, oldSolidMeta: 0, oldFluidId: oldFluidId);
                return true;
            }

            
            public bool PlaceFluid(int x, int y, ushort fluidId, byte amount)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;
                if (fluidId == 0 || amount == 0) return false;

                var oldS = _ctx.WorldMap.GetSolid(x, y);
                ushort oldSolidId = oldS.id;
                ushort oldSolidMeta = oldS.meta;
                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                if (_ctx.IsCollidable(x, y)) return false;

                var cur = _ctx.WorldMap.GetFluid(x, y);

                if (cur.id != 0 && cur.amount > 0 && cur.id != fluidId)
                    return false;

                int current = cur.amount;
                int space = WorldData.MaxFluid - current;
                if (space <= 0) return false;

                int insert = (amount <= space) ? amount : space;
                int newAmt = current + insert;

                _ctx.WorldMap.SetFluid(x, y, fluidId, (byte)newAmt);

                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
                _ctx.OnCellEdited(x, y);

                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
                return insert > 0;
            }

            
            public bool PlaceBG(int x, int y, ushort id)
                => PlaceBG(x, y, id, RelV.Neutral, RelH.Neutral);

            
            public bool PlaceBG(int x, int y, ushort id, RelV relV, RelH relH)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;
                if (id == 0) return false;

                if (_ctx.WorldMap.GetSolid(x, y).id != 0) return false;
                if (_ctx.WorldMap.GetBG(x, y) != 0) return false;

                if (!_ctx.EditSupportService.HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable: false))
                    return false;

                _ctx.WorldMap.SetBG(x, y, id);

                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
                _ctx.OnCellEdited(x, y);
                return true;
            }

            
            public ushort RemoveSolidNoDrop(int x, int y, bool emitVfx = false)
            {
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return 0;

                var s = _ctx.WorldMap.GetSolid(x, y);
                ushort oldSolidId = s.id;
                ushort oldMeta = s.meta;
                if (oldSolidId == 0) return 0;

                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                _ctx.WorldMap.SetSolid(x, y, 0, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: true);
                _ctx.OnCellEdited(x, y);

                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId, oldMeta, oldFluidId);

                if (emitVfx)
                    _ctx.DropAndVfxService.EmitSolidBreakVfx(oldSolidId, oldMeta, x, y);

                return oldSolidId;
            }

            
            public ushort BreakSolid(int x, int y)
            {
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return 0;

                var s = _ctx.WorldMap.GetSolid(x, y);
                ushort oldSolidId = s.id;
                ushort oldMeta = s.meta;
                if (oldSolidId == 0) return 0;

                string solidType = (_ctx.CellLibrary != null) ? _ctx.CellLibrary.GetSolidType(oldSolidId) : "Default";
                var cell = new Vector2Int(x, y);

                if (solidType == "Source")
                {
                    if (_ctx.GearNetworkManager != null)
                        _ctx.GearNetworkManager.TryRemoveSourceAtGearCell(cell, out _);

                    ushort removedSourceSolid = RemoveSolidNoDrop(x, y, emitVfx: false);
                    if (removedSourceSolid == 0) return 0;

                    var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    _ctx.DropAndVfxService.EmitSolidBreakVfx(oldSolidId, oldMeta, x, y);
                    _ctx.DropAndVfxService.SpawnSolidDrops(oldSolidId, pos);
                    return removedSourceSolid;
                }

                if (solidType == "Belt")
                {
                    Vector2Int otherGearCenter = default;
                    bool removedBeltLink = false;

                    if (_ctx.GearNetworkManager != null)
                        removedBeltLink = _ctx.GearNetworkManager.TryRemoveBeltAtGearCell(cell, out _, out otherGearCenter);

                    if (removedBeltLink)
                        RemoveSolidNoDrop(otherGearCenter.x, otherGearCenter.y, emitVfx: true);

                    ushort removedBeltSolid = RemoveSolidNoDrop(x, y, emitVfx: false);
                    if (removedBeltSolid == 0) return 0;

                    var pos = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    _ctx.DropAndVfxService.EmitSolidBreakVfx(oldSolidId, oldMeta, x, y);
                    _ctx.DropAndVfxService.SpawnSolidDrops(oldSolidId, pos);
                    return removedBeltSolid;
                }

                ushort removed = RemoveSolidNoDrop(x, y, emitVfx: false);
                if (removed == 0) return 0;

                var position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                _ctx.DropAndVfxService.EmitSolidBreakVfx(oldSolidId, oldMeta, x, y);
                _ctx.DropAndVfxService.SpawnSolidDrops(oldSolidId, position);
                return removed;
            }

            
            public FluidCell BreakFluid(int x, int y)
            {
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return default;

                var oldS = _ctx.WorldMap.GetSolid(x, y);
                ushort oldSolidId = oldS.id;
                ushort oldSolidMeta = oldS.meta;

                var removed = _ctx.WorldMap.GetFluid(x, y);
                ushort oldFluidId = removed.id;

                if (removed.id == 0 || removed.amount == 0) return removed;

                _ctx.WorldMap.SetFluid(x, y, 0, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
                _ctx.OnCellEdited(x, y);

                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
                return removed;
            }

            
            public ushort BreakBG(int x, int y)
            {
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return 0;

                ushort removed = _ctx.WorldMap.GetBG(x, y);
                if (removed == 0) return 0;

                _ctx.WorldMap.SetBG(x, y, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
                _ctx.OnCellEdited(x, y);

                var position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                _ctx.DropAndVfxService.EmitSolidBreakVfx(removed, 0, x, y, grid: 2);
                _ctx.DropAndVfxService.SpawnSolidDrops(removed, position);
                return removed;
            }

            
            public bool PlaceCell(int x, int y, ushort id) => PlaceSolid(x, y, id);
            
            public bool PlaceBgCell(int x, int y, ushort id) => PlaceBG(x, y, id);

            
            public ushort BreakCell(int x, int y, CellLayer layer)
            {
                return layer == CellLayer.Solid ? BreakSolid(x, y) : BreakBG(x, y);
            }

            
            bool PlaceSolidAtEmpty(int x, int y, ushort id, RelV relV, RelH relH)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;
                if (id == 0) return false;

                var curS = _ctx.WorldMap.GetSolid(x, y);
                if (curS.id != 0) return false;

                bool hasBgHere = _ctx.WorldMap.GetBG(x, y) != 0;

                if (!hasBgHere)
                {
                    if (!_ctx.EditSupportService.HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable: true))
                        return false;
                }

                var candidates = new List<ushort>(5);

                if (hasBgHere && _ctx.EditSupportService.HasVariantMeta(id, META_BG))
                    candidates.Add(META_BG);

                
                void Add(ushort first, ushort second)
                {
                    if (_ctx.EditSupportService.HasVariantMeta(id, first)) candidates.Add(first);
                    if (_ctx.EditSupportService.HasVariantMeta(id, second)) candidates.Add(second);
                }

                if (relH == RelH.Left) Add(META_LEFT, META_RIGHT);
                
                else if (relH == RelH.Right) Add(META_RIGHT, META_LEFT);
                
                else Add(META_LEFT, META_RIGHT);

                if (relV == RelV.Up) Add(META_UP, META_DOWN);
                
                else if (relV == RelV.Down) Add(META_DOWN, META_UP);
                
                else Add(META_DOWN, META_UP);

                var seen = new HashSet<ushort>();
                for (int i = candidates.Count - 1; i >= 0; i--)
                {
                    if (!seen.Add(candidates[i]))
                        candidates.RemoveAt(i);
                }

                ushort chosenMeta = 0;
                bool found = false;

                
                bool HasSupportFor(ushort m)
                {
                    int sx = x, sy = y;

                    switch (m)
                    {
                        case META_UP: sy = y + 1; break;
                        case META_DOWN: sy = y - 1; break;
                        case META_LEFT: sx = x - 1; break;
                        case META_RIGHT: sx = x + 1; break;
                        default: return false;
                    }

                    return _ctx.EditSupportService.IsValidSupportForSolidAttach(sx, sy);
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    ushort m = candidates[i];

                    if (m == META_BG)
                    {
                        chosenMeta = META_BG;
                        found = true;
                        break;
                    }

                    if (!HasSupportFor(m))
                        continue;

                    chosenMeta = m;
                    found = true;
                    break;
                }

                if (!found)
                {
                    if (_ctx.EditSupportService.HasVariantMeta(id, META_DEFAULT))
                    {
                        chosenMeta = META_DEFAULT;
                        found = true;
                    }
                }

                if (!found) return false;

                ushort oldSolidId = 0;
                ushort oldSolidMeta = 0;
                ushort oldFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                _ctx.WorldMap.SetSolid(x, y, id, chosenMeta);

                if ((_ctx.CellLibrary.GetSolidFlags(id) & CellLibrary.SolidFlags.Collidable) != 0)
                    _ctx.WorldMap.SetFluid(x, y, 0, 0);

                _ctx.MarkChunkDirty(x, y, markSolid: true);
                _ctx.OnCellEdited(x, y);

                _ctx.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
                return true;
            }
        }
    }
}
