using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class UtilityEditService
        {
            readonly WorldServiceContext _ctx;

            public UtilityEditService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public bool SetUtilityExact(int x, int y, ushort id, ushort meta = 0)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;

                _ctx.WorldMap.SetUtility(x, y, id, meta);
                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                return true;
            }

            public bool ClearUtilityExact(int x, int y)
            {
                if (!_ctx.WorldMap.InBounds(x, y)) return false;

                _ctx.WorldMap.SetUtility(x, y, 0, 0);
                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                return true;
            }

            public bool IsUtilityAreaEmpty(Vector2Int center, IReadOnlyList<Vector2Int> offsets)
            {
                if (offsets == null || offsets.Count == 0) return false;

                for (int i = 0; i < offsets.Count; i++)
                {
                    int x = center.x + offsets[i].x;
                    int y = center.y + offsets[i].y;
                    if (!_ctx.WorldMap.InBounds(x, y)) return false;

                    if (_ctx.WorldMap.GetUtility(x, y).id != 0)
                        return false;
                }

                return true;
            }

            public bool PlaceUtilityFootprint(
                Vector2Int center,
                ushort centerId,
                ushort centerMeta,
                ushort occupiedId,
                IReadOnlyList<Vector2Int> offsets)
            {
                if (centerId == 0) return false;
                if (offsets == null || offsets.Count == 0) return false;
                if (!IsUtilityAreaEmpty(center, offsets)) return false;

                for (int i = 0; i < offsets.Count; i++)
                {
                    int x = center.x + offsets[i].x;
                    int y = center.y + offsets[i].y;

                    if (offsets[i].x == 0 && offsets[i].y == 0)
                        _ctx.WorldMap.SetUtility(x, y, centerId, centerMeta);
                    else
                        _ctx.WorldMap.SetUtility(x, y, occupiedId, 0);

                    _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
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
                    if (!_ctx.WorldMap.InBounds(x, y)) continue;

                    var u = _ctx.WorldMap.GetUtility(x, y);
                    if (u.id == 0) continue;

                    _ctx.WorldMap.SetUtility(x, y, 0, 0);
                    _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                    any = true;
                }

                return any;
            }

            public bool PlaceGearFootprintUtility(
                Vector2Int center,
                ushort centerId,
                ushort centerMeta,
                ushort occupiedId,
                IReadOnlyList<Vector2Int> occupiedCells)
            {
                if (centerId == 0) return false;
                if (!_ctx.WorldMap.InBounds(center.x, center.y)) return false;
                if (_ctx.WorldMap.GetUtility(center.x, center.y).id != 0) return false;
                if (occupiedCells != null && occupiedCells.Count > 0 && occupiedId == 0) return false;

                if (occupiedCells != null)
                {
                    for (int i = 0; i < occupiedCells.Count; i++)
                    {
                        var cell = occupiedCells[i];
                        if (!_ctx.WorldMap.InBounds(cell.x, cell.y)) return false;

                        var u = _ctx.WorldMap.GetUtility(cell.x, cell.y);
                        if (u.id != 0 && u.id != occupiedId)
                            return false;
                    }
                }

                _ctx.WorldMap.SetUtility(center.x, center.y, centerId, centerMeta);
                _ctx.MarkChunkDirty(center.x, center.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);

                if (occupiedCells != null)
                {
                    for (int i = 0; i < occupiedCells.Count; i++)
                    {
                        var cell = occupiedCells[i];
                        _ctx.WorldMap.SetUtility(cell.x, cell.y, occupiedId, 0);
                        _ctx.MarkChunkDirty(cell.x, cell.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
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
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return 0;

                CacheUtilityOccupiedIdIfNeeded();

                var u = _ctx.WorldMap.GetUtility(x, y);
                if (u.id == 0) return 0;

                if (_ctx.UtilityOccupiedId != 0 && u.id == _ctx.UtilityOccupiedId)
                    return 0;

                var cell = new Vector2Int(x, y);

                if (_ctx.GearNetworkManager != null && _ctx.GearNetworkManager.IsGearOccupiedCell(cell))
                {
                    ushort centerUtilityId = u.id;
                    ushort centerUtilityMeta = u.meta;

                    bool hasSourceSolid = false;
                    bool hasBeltSolid = false;
                    Vector2Int otherBeltCenter = default;

                    ushort centerSolidIdBeforeBreak = _ctx.WorldMap.GetSolid(x, y).id;
                    string centerSolidTypeBeforeBreak = (_ctx.CellLibrary != null)
                        ? _ctx.CellLibrary.GetSolidType(centerSolidIdBeforeBreak)
                        : "Default";

                    if (centerSolidIdBeforeBreak != 0)
                    {
                        if (centerSolidTypeBeforeBreak == "Source")
                        {
                            hasSourceSolid = true;
                        }
                        else if (centerSolidTypeBeforeBreak == "Belt" &&
                                 _ctx.GearNetworkManager.TryGetBeltAtGearCell(cell, out _, out otherBeltCenter))
                        {
                            hasBeltSolid = true;
                        }
                    }

                    string droppedSourceId = null;
                    List<GearNetworkManager.BeltDrop> droppedBelts = null;

                    if (!_ctx.GearNetworkManager.TryRemoveGearAt(cell, out droppedSourceId, out droppedBelts, out var removedOccupiedCells))
                        return 0;

                    if (hasSourceSolid)
                        _ctx.CellEditService.RemoveSolidNoDrop(x, y, emitVfx: true);

                    if (hasBeltSolid)
                    {
                        _ctx.CellEditService.RemoveSolidNoDrop(x, y, emitVfx: true);
                        _ctx.CellEditService.RemoveSolidNoDrop(otherBeltCenter.x, otherBeltCenter.y, emitVfx: true);
                    }

                    ClearGearFootprintUtility(cell, removedOccupiedCells);

                    _ctx.DropAndVfxService.EmitUtilityBreakVfx(centerUtilityId, centerUtilityMeta, x, y);

                    var pos3 = new Vector3(x + 0.5f, y + 0.5f, 0f);
                    _ctx.DropAndVfxService.SpawnUtilityDrops(centerUtilityId, pos3);
                    _ctx.DropAndVfxService.SpawnItemDropById(droppedSourceId, pos3);

                    return centerUtilityId;
                }

                _ctx.WorldMap.SetUtility(x, y, 0, 0);
                _ctx.MarkChunkDirty(x, y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);

                _ctx.DropAndVfxService.EmitUtilityBreakVfx(u.id, u.meta, x, y);

                var position = new Vector3(x + 0.5f, y + 0.5f, 0f);
                _ctx.DropAndVfxService.SpawnUtilityDrops(u.id, position);

                return u.id;
            }

            public void ClearGearFootprintUtility(Vector2Int center, IReadOnlyList<Vector2Int> occupiedCells)
            {
                CacheUtilityOccupiedIdIfNeeded();

                if (_ctx.WorldMap.InBounds(center.x, center.y))
                {
                    _ctx.WorldMap.SetUtility(center.x, center.y, 0, 0);
                    _ctx.MarkChunkDirty(center.x, center.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                }

                if (_ctx.UtilityOccupiedId == 0 || occupiedCells == null) return;

                for (int i = 0; i < occupiedCells.Count; i++)
                {
                    var p = occupiedCells[i];
                    if (!_ctx.WorldMap.InBounds(p.x, p.y)) continue;

                    var u = _ctx.WorldMap.GetUtility(p.x, p.y);
                    if (u.id != _ctx.UtilityOccupiedId) continue;
                    if (_ctx.GearNetworkManager != null && _ctx.GearNetworkManager.HasGearOccupiedVisualAt(p))
                        continue;

                    _ctx.WorldMap.SetUtility(p.x, p.y, 0, 0);
                    _ctx.MarkChunkDirty(p.x, p.y, markSolid: false, markBG: false, markLiquid: false, markUtility: true);
                }
            }

            public void CacheUtilityOccupiedIdIfNeeded()
            {
                if (_ctx.UtilityOccupiedId != 0) return;
                if (_ctx.CellLibrary == null) return;

                if (_ctx.CellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out var occ))
                    _ctx.UtilityOccupiedId = occ;
            }
        }
    }
}
