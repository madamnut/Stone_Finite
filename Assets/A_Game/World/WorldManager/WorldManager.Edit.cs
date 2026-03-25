using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        private bool HasAnyNeighborSupport_BGorSolid(int x, int y, bool solidMustBeCollidable)
        {
            bool Check(int nx, int ny)
            {
                if (!worldMap.InBounds(nx, ny)) return false;
    
                if (worldMap.GetBG(nx, ny) != 0) return true;
    
                ushort sid = worldMap.GetSolid(nx, ny).id;
                if (sid == 0) return false;
    
                if (!solidMustBeCollidable) return true;
    
                return IsSupportSolid(nx, ny);
            }
    
            if (Check(x - 1, y)) return true;
            if (Check(x + 1, y)) return true;
            if (Check(x, y - 1)) return true;
            if (Check(x, y + 1)) return true;
    
            return false;
        }
    
        private bool IsValidSupportForSolidAttach(int sx, int sy)
        {
            if (!worldMap.InBounds(sx, sy)) return false;
    
            if (worldMap.GetBG(sx, sy) != 0) return true;
    
            return IsSupportSolid(sx, sy);
        }
    
        private bool HasVariantMeta(ushort id, ushort meta)
        {
            return cellLibrary.HasSolidVariant(id, meta);
        }
    
        public bool SetUtilityExact(int x, int y, ushort id, ushort meta = 0)
        {
            if (!worldMap.InBounds(x, y)) return false;
    
            worldMap.SetUtility(x, y, id, meta);
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
            return true;
        }
    
        public bool ClearUtilityExact(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return false;
    
            worldMap.SetUtility(x, y, 0, 0);
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
            return true;
        }
    
        public bool IsUtilityAreaEmpty(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
        {
            if (offsets == null || offsets.Count == 0) return false;
    
            for (int i = 0; i < offsets.Count; i++)
            {
                int x = center.x + offsets[i].x;
                int y = center.y + offsets[i].y;
                if (!worldMap.InBounds(x, y)) return false;
    
                if (worldMap.GetUtility(x, y).id != 0)
                    return false;
            }
    
            return true;
        }
    
        public bool PlaceUtilityFootprint(
            Vector2Int center,
            ushort centerId,
            ushort centerMeta,
            ushort occupiedId,
            IReadOnlyList<Vector2Int> offsets
        )
        {
            if (centerId == 0) return false;
            if (offsets == null || offsets.Count == 0) return false;
            if (!IsUtilityAreaEmpty(center, offsets)) return false;
    
            for (int i = 0; i < offsets.Count; i++)
            {
                int x = center.x + offsets[i].x;
                int y = center.y + offsets[i].y;
    
                if (offsets[i].x == 0 && offsets[i].y == 0)
                    worldMap.SetUtility(x, y, centerId, centerMeta);
                else
                    worldMap.SetUtility(x, y, occupiedId, 0);
    
                MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
            }
    
            return true;
        }
    
        public bool ClearUtilityFootprint(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
        {
            if (offsets == null || offsets.Count == 0) return false;
    
            bool any = false;
    
            for (int i = 0; i < offsets.Count; i++)
            {
                int x = center.x + offsets[i].x;
                int y = center.y + offsets[i].y;
                if (!worldMap.InBounds(x, y)) continue;
    
                var u = worldMap.GetUtility(x, y);
                if (u.id == 0) continue;
    
                worldMap.SetUtility(x, y, 0, 0);
                MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                any = true;
            }
    
            return any;
        }
    
        public bool PlaceGearFootprintUtility(
            Vector2Int center,
            ushort centerId,
            ushort centerMeta,
            ushort occupiedId,
            IReadOnlyList<Vector2Int> occupiedCells
        )
        {
            if (centerId == 0) return false;
            if (!worldMap.InBounds(center.x, center.y)) return false;
            if (worldMap.GetUtility(center.x, center.y).id != 0) return false;
            if (occupiedCells != null && occupiedCells.Count > 0 && occupiedId == 0) return false;
    
            if (occupiedCells != null)
            {
                for (int i = 0; i < occupiedCells.Count; i++)
                {
                    var cell = occupiedCells[i];
                    if (!worldMap.InBounds(cell.x, cell.y)) return false;
    
                    var u = worldMap.GetUtility(cell.x, cell.y);
                    if (u.id != 0 && u.id != occupiedId)
                        return false;
                }
            }
    
            worldMap.SetUtility(center.x, center.y, centerId, centerMeta);
            MarkChunkDirty(center.x, center.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
    
            if (occupiedCells != null)
            {
                for (int i = 0; i < occupiedCells.Count; i++)
                {
                    var cell = occupiedCells[i];
                    worldMap.SetUtility(cell.x, cell.y, occupiedId, 0);
                    MarkChunkDirty(cell.x, cell.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                }
            }
    
            return true;
        }
    
        public void RemoveGearFootprintUtility(Vector2Int center, IReadOnlyList<Vector2Int> occupiedCells)
        {
            ClearGearFootprintUtility(center, occupiedCells);
        }
    
        public ushort BreakUtility(int x, int y)
        {
            return BreakUtilityAt(x, y);
        }
    
        public ushort BreakUtilityAt(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;
    
            CacheUtilityOccupiedIdIfNeeded();
    
            var u = worldMap.GetUtility(x, y);
            if (u.id == 0) return 0;
    
            if (_utilityOccupiedId != 0 && u.id == _utilityOccupiedId)
                return 0;
    
            var cell = new Vector2Int(x, y);
    
            if (gearNetworkManager != null && gearNetworkManager.IsGearOccupiedCell(cell))
            {
                ushort centerUtilityId = u.id;
                ushort centerUtilityMeta = u.meta;
    
                bool hasSourceSolid = false;
                bool hasBeltSolid = false;
                Vector2Int otherBeltCenter = default;
    
                ushort centerSolidIdBeforeBreak = worldMap.GetSolid(x, y).id;
                string centerSolidTypeBeforeBreak = (cellLibrary != null)
                    ? cellLibrary.GetSolidType(centerSolidIdBeforeBreak)
                    : "Default";
    
                if (centerSolidIdBeforeBreak != 0)
                {
                    if (centerSolidTypeBeforeBreak == "Source")
                    {
                        hasSourceSolid = true;
                    }
                    else if (centerSolidTypeBeforeBreak == "Belt" &&
                             gearNetworkManager.TryGetBeltAtGearCell(cell, out _, out otherBeltCenter))
                    {
                        hasBeltSolid = true;
                    }
                }
    
                string droppedSourceId = null;
                List<GearNetworkManager.BeltDrop> droppedBelts = null;
    
                if (!gearNetworkManager.TryRemoveGearAt(cell, out droppedSourceId, out droppedBelts, out var removedOccupiedCells))
                    return 0;
    
                if (hasSourceSolid)
                    RemoveSolidNoDrop(x, y, emitVfx: true);
    
                if (hasBeltSolid)
                {
                    RemoveSolidNoDrop(x, y, emitVfx: true);
                    RemoveSolidNoDrop(otherBeltCenter.x, otherBeltCenter.y, emitVfx: true);
                }
    
                ClearGearFootprintUtility(cell, removedOccupiedCells);
    
                if (vfx != null && cellLibrary != null)
                {
                    var spr = cellLibrary.GetUtilitySprite(centerUtilityId, centerUtilityMeta);
                    vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
                }
    
                var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
    
                if (itemDropper != null && cellLibrary != null)
                {
                    string gearItemId = cellLibrary.GetUtilityName(centerUtilityId);
                    if (!string.IsNullOrEmpty(gearItemId))
                        itemDropper.SpawnDroppedItems(gearItemId, pos3);
                }
    
                if (itemDropper != null && !string.IsNullOrEmpty(droppedSourceId) && itemLibrary != null)
                {
                    var srcData = itemLibrary.Create(droppedSourceId, 1);
                    if (srcData != null)
                        itemDropper.SpawnDroppedItem(srcData, pos3);
                }
    
                return centerUtilityId;
            }
    
            worldMap.SetUtility(x, y, 0, 0);
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
    
            if (vfx != null && cellLibrary != null)
            {
                var spr = cellLibrary.GetUtilitySprite(u.id, u.meta);
                vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
            }
    
            if (itemDropper != null && cellLibrary != null)
            {
                string key = cellLibrary.GetUtilityName(u.id);
                if (!string.IsNullOrEmpty(key))
                {
                    var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    itemDropper.SpawnDroppedItems(key, pos3);
                }
            }
    
            return u.id;
        }
    
        void ClearGearFootprintUtility(Vector2Int center, IReadOnlyList<Vector2Int> occupiedCells)
        {
            CacheUtilityOccupiedIdIfNeeded();
    
            if (worldMap.InBounds(center.x, center.y))
            {
                worldMap.SetUtility(center.x, center.y, 0, 0);
                MarkChunkDirty(center.x, center.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
            }
    
            if (_utilityOccupiedId == 0 || occupiedCells == null) return;
    
            for (int i = 0; i < occupiedCells.Count; i++)
            {
                var p = occupiedCells[i];
                if (!worldMap.InBounds(p.x, p.y)) continue;
    
                var u = worldMap.GetUtility(p.x, p.y);
                if (u.id != _utilityOccupiedId) continue;
                if (gearNetworkManager != null && gearNetworkManager.HasGearOccupiedVisualAt(p))
                    continue;
    
                worldMap.SetUtility(p.x, p.y, 0, 0);
                MarkChunkDirty(p.x, p.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
            }
        }
    
        void CacheUtilityOccupiedIdIfNeeded()
        {
            if (_utilityOccupiedId != 0) return;
            if (cellLibrary == null) return;
    
            if (cellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out var occ))
                _utilityOccupiedId = occ;
        }
    
        public void OverwriteSolid(int x, int y, ushort newId, ushort newMeta = 0)
        {
            var cur = worldMap.GetSolid(x, y);
            ushort oldSolidId = cur.id;
            ushort oldSolidMeta = cur.meta;
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            worldMap.SetSolid(x, y, newId, newMeta);
    
            if ((cellLibrary.GetSolidFlags(newId) & CellLibrary.SolidFlags.Collidable) != 0)
                worldMap.SetFluid(x, y, 0, 0);
    
            MarkChunkDirty(x, y, markSolid: true);
            OnCellEdited(x, y);
            HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
        }
    
        public bool PlaceSolid(int x, int y, ushort id)
            => PlaceSolid(x, y, id, RelV.Neutral, RelH.Neutral);
    
        private bool PlaceSolidAtEmpty(int x, int y, ushort id, RelV relV, RelH relH)
        {
            if (!worldMap.InBounds(x, y)) return false;
            if (id == 0) return false;
    
            var curS = worldMap.GetSolid(x, y);
            if (curS.id != 0) return false;
    
            bool hasBgHere = worldMap.GetBG(x, y) != 0;
    
            if (!hasBgHere)
            {
                if (!HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable: true))
                    return false;
            }
    
            var candidates = new List<ushort>(5);
    
            if (hasBgHere && HasVariantMeta(id, META_BG))
                candidates.Add(META_BG);
    
            void Add(ushort first, ushort second)
            {
                if (HasVariantMeta(id, first)) candidates.Add(first);
                if (HasVariantMeta(id, second)) candidates.Add(second);
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
    
                return IsValidSupportForSolidAttach(sx, sy);
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
                if (HasVariantMeta(id, META_DEFAULT))
                {
                    chosenMeta = META_DEFAULT;
                    found = true;
                }
            }
    
            if (!found) return false;
    
            ushort oldSolidId = 0;
            ushort oldSolidMeta = 0;
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            worldMap.SetSolid(x, y, id, chosenMeta);
    
            if ((cellLibrary.GetSolidFlags(id) & CellLibrary.SolidFlags.Collidable) != 0)
                worldMap.SetFluid(x, y, 0, 0);
    
            MarkChunkDirty(x, y, markSolid: true);
            OnCellEdited(x, y);
    
            HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
            return true;
        }
    
        public bool PlaceSolid(int x, int y, ushort id, RelV relV, RelH relH)
        {
            if (!worldMap.InBounds(x, y)) return false;
            if (id == 0) return false;
    
            var curS = worldMap.GetSolid(x, y);
    
            if (curS.id != 0)
            {
                if (!IsSupportSolid(x, y))
                    return false;
    
                bool TryNeighbor(int nx, int ny, RelV nRelV, RelH nRelH)
                {
                    if (!worldMap.InBounds(nx, ny)) return false;
                    if (worldMap.GetSolid(nx, ny).id != 0) return false;
    
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
            if (!worldMap.InBounds(x, y)) return false;
            if (id == 0) return false;
    
            if (worldMap.GetSolid(x, y).id != 0) return false;
    
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            worldMap.SetSolid(x, y, id, 0);
    
            if ((cellLibrary.GetSolidFlags(id) & CellLibrary.SolidFlags.Collidable) != 0)
                worldMap.SetFluid(x, y, 0, 0);
    
            MarkChunkDirty(x, y, markSolid: true);
            OnCellEdited(x, y);
    
            HandleSourceLightChangeAt(x, y, oldSolidId: 0, oldSolidMeta: 0, oldFluidId: oldFluidId);
            return true;
        }
    
        public bool PlaceFluid(int x, int y, ushort fluidId, byte amount)
        {
            if (!worldMap.InBounds(x, y)) return false;
            if (fluidId == 0 || amount == 0) return false;
    
            var oldS = worldMap.GetSolid(x, y);
            ushort oldSolidId = oldS.id;
            ushort oldSolidMeta = oldS.meta;
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            if (IsCollidable(x, y)) return false;
    
            var cur = worldMap.GetFluid(x, y);
    
            if (cur.id != 0 && cur.amount > 0 && cur.id != fluidId)
                return false;
    
            int current = cur.amount;
            int space = WorldData.MaxFluid - current;
            if (space <= 0) return false;
    
            int insert = (amount <= space) ? amount : space;
            int newAmt = current + insert;
    
            worldMap.SetFluid(x, y, fluidId, (byte)newAmt);
    
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
            OnCellEdited(x, y);
    
            HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
            return insert > 0;
        }
    
        public bool PlaceBG(int x, int y, ushort id)
            => PlaceBG(x, y, id, RelV.Neutral, RelH.Neutral);
    
        public bool PlaceBG(int x, int y, ushort id, RelV relV, RelH relH)
        {
            if (!worldMap.InBounds(x, y)) return false;
            if (id == 0) return false;
    
            if (worldMap.GetSolid(x, y).id != 0) return false;
            if (worldMap.GetBG(x, y) != 0) return false;
    
            if (!HasAnyNeighborSupport_BGorSolid(x, y, solidMustBeCollidable: false))
                return false;
    
            worldMap.SetBG(x, y, id);
    
            MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
            OnCellEdited(x, y);
            return true;
        }
    
        public ushort RemoveSolidNoDrop(int x, int y, bool emitVfx = false)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;
    
            var s = worldMap.GetSolid(x, y);
            ushort oldSolidId = s.id;
            ushort oldMeta = s.meta;
            if (oldSolidId == 0) return 0;
    
            ushort oldFluidId = worldMap.GetFluid(x, y).id;
    
            worldMap.SetSolid(x, y, 0, 0);
    
            MarkChunkDirty(x, y, markSolid: true);
            OnCellEdited(x, y);
    
            HandleSourceLightChangeAt(x, y, oldSolidId, oldMeta, oldFluidId);
    
            if (emitVfx && vfx != null && cellLibrary != null)
            {
                var spr = cellLibrary.GetSolidSprite(oldSolidId, oldMeta);
                vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
            }
    
            return oldSolidId;
        }
    
        public ushort BreakSolid(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;
    
            var s = worldMap.GetSolid(x, y);
            ushort oldSolidId = s.id;
            ushort oldMeta = s.meta;
            if (oldSolidId == 0) return 0;
    
            string solidType = (cellLibrary != null) ? cellLibrary.GetSolidType(oldSolidId) : "Default";
            var cell = new Vector2Int(x, y);
    
            if (solidType == "Source")
            {
                if (gearNetworkManager != null)
                    gearNetworkManager.TryRemoveSourceAtGearCell(cell, out _);
    
                ushort removedSourceSolid = RemoveSolidNoDrop(x, y, emitVfx: false);
                if (removedSourceSolid == 0) return 0;
    
                string srcKey = cellLibrary != null ? cellLibrary.GetSolidName(oldSolidId) : null;
                if (!string.IsNullOrEmpty(srcKey))
                {
                    var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
    
                    if (vfx != null && cellLibrary != null)
                    {
                        var spr = cellLibrary.GetSolidSprite(oldSolidId, oldMeta);
                        vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
                    }
    
                    if (itemDropper != null)
                        itemDropper.SpawnDroppedItems(srcKey, pos3);
                }
    
                return removedSourceSolid;
            }
    
            if (solidType == "Belt")
            {
                Vector2Int otherGearCenter = default;
                bool removedBeltLink = false;
    
                if (gearNetworkManager != null)
                    removedBeltLink = gearNetworkManager.TryRemoveBeltAtGearCell(cell, out _, out otherGearCenter);
    
                if (removedBeltLink)
                {
                    RemoveSolidNoDrop(otherGearCenter.x, otherGearCenter.y, emitVfx: true);
                }
    
                ushort removedBeltSolid = RemoveSolidNoDrop(x, y, emitVfx: false);
                if (removedBeltSolid == 0) return 0;
    
                string beltKey = cellLibrary != null ? cellLibrary.GetSolidName(oldSolidId) : null;
                if (!string.IsNullOrEmpty(beltKey))
                {
                    var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
    
                    if (vfx != null && cellLibrary != null)
                    {
                        var spr = cellLibrary.GetSolidSprite(oldSolidId, oldMeta);
                        vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
                    }
    
                    if (itemDropper != null)
                        itemDropper.SpawnDroppedItems(beltKey, pos3);
                }
    
                return removedBeltSolid;
            }
    
            ushort removed = RemoveSolidNoDrop(x, y, emitVfx: false);
            if (removed == 0) return 0;
    
            string key = cellLibrary != null ? cellLibrary.GetSolidName(oldSolidId) : null;
    
            if (!string.IsNullOrEmpty(key))
            {
                var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
    
                if (vfx != null && cellLibrary != null)
                {
                    var spr = cellLibrary.GetSolidSprite(oldSolidId, oldMeta);
                    vfx.EmitBlockAtCell(spr, x, y, 1, grid: 3, count: -1);
                }
    
                if (itemDropper != null)
                    itemDropper.SpawnDroppedItems(key, pos3);
            }
    
            return removed;
        }
    
        public FluidCell BreakFluid(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return default;
    
            var oldS = worldMap.GetSolid(x, y);
            ushort oldSolidId = oldS.id;
            ushort oldSolidMeta = oldS.meta;
    
            var removed = worldMap.GetFluid(x, y);
            ushort oldFluidId = removed.id;
    
            if (removed.id == 0 || removed.amount == 0) return removed;
    
            worldMap.SetFluid(x, y, 0, 0);
    
            MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: true);
            OnCellEdited(x, y);
    
            HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);
            return removed;
        }
    
        public ushort BreakBG(int x, int y)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return 0;
    
            ushort removed = worldMap.GetBG(x, y);
            if (removed == 0) return 0;
    
            worldMap.SetBG(x, y, 0);
    
            MarkChunkDirty(x, y, markSolid: false, markBG: true, markLiquid: false);
            OnCellEdited(x, y);
    
            if (cellLibrary != null)
            {
                string key = cellLibrary.GetSolidName(removed);
                if (!string.IsNullOrEmpty(key))
                {
                    var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
    
                    if (vfx != null)
                    {
                        var spr = cellLibrary.GetSolidSprite(removed, 0);
                        vfx.EmitBlockAtCell(spr, x, y, 1, grid: 2, count: -1);
                    }
    
                    if (itemDropper != null)
                        itemDropper.SpawnDroppedItems(key, pos3);
                }
            }
    
            return removed;
        }
    
        public bool PlaceCell(int x, int y, ushort id) => PlaceSolid(x, y, id);
        public bool PlaceBgCell(int x, int y, ushort id) => PlaceBG(x, y, id);
    
        public ushort BreakCell(int x, int y, CellLayer layer)
        {
            return layer == CellLayer.Solid ? BreakSolid(x, y) : BreakBG(x, y);
        }
    }
}
