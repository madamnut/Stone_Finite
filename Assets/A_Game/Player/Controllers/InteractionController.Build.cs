using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

using Game.Data;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        bool TryGetPlaceUtilityParam(
            Dictionary<string, object> placeParam,
            out string type,
            out string cell,
            out Dictionary<string, object> typeObj
        )
        {
            type = null;
            cell = null;
            typeObj = null;
    
            if (placeParam == null) return false;
    
            if (placeParam.TryGetValue("type", out var t) && t != null) type = t.ToString();
            if (placeParam.TryGetValue("cell", out var c) && c != null) cell = c.ToString();
    
            if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(cell))
                return false;
    
            if (placeParam.TryGetValue(type, out var obj) && obj is Dictionary<string, object> dict)
                typeObj = dict;
    
            return true;
        }
    
        bool TryGetCellParam(Dictionary<string, object> p, out string cell)
        {
            cell = null;
            if (p == null) return false;
            if (!p.TryGetValue("cell", out var c) || c == null) return false;
            cell = c.ToString();
            return !string.IsNullOrEmpty(cell);
        }
    
        static bool TryReadString(Dictionary<string, object> d, string key, out string v)
        {
            v = null;
            if (d == null) return false;
            if (!d.TryGetValue(key, out var o) || o == null) return false;
            v = o.ToString();
            return !string.IsNullOrEmpty(v);
        }
    
        static bool TryReadInt(Dictionary<string, object> d, string key, out int v)
        {
            v = 0;
            if (d == null) return false;
            if (!d.TryGetValue(key, out var o) || o == null) return false;
    
            if (o is int i) { v = i; return true; }
            if (o is long l) { v = (int)l; return true; }
            if (o is float f) { v = Mathf.RoundToInt(f); return true; }
            if (o is double db) { v = (int)Math.Round(db); return true; }
    
            return int.TryParse(o.ToString(), out v);
        }
    
        bool TryGetCogwheelPlacementSpec(string cell, out GearNode.GearSize size, out int maxRpm)
        {
            size = GearNode.GearSize.Small;
            maxRpm = 0;
    
            if (string.IsNullOrEmpty(cell) || utilityLibrary == null)
                return false;
    
            if (!utilityLibrary.TryGetCogwheel(cell, out var def))
                return false;
    
            size = (def.size == "Big") ? GearNode.GearSize.Big : GearNode.GearSize.Small;
            maxRpm = def.maxRpm;
            return true;
        }
    
        bool IsUtilityOccupiedCell(int x, int y)
        {
            if (worldManager == null) return false;
            ushort uid = worldManager.GetUtilityId(x, y);
            if (uid == 0) return false;
            return (_utilityOccupiedId != 0 && uid == _utilityOccupiedId);
        }
    
        bool IsUtilityCenterCell(int x, int y)
        {
            if (worldManager == null) return false;
            ushort uid = worldManager.GetUtilityId(x, y);
            if (uid == 0) return false;
            return (_utilityOccupiedId == 0 || uid != _utilityOccupiedId);
        }
    
        void HandleLeftClick()
        {
            if (_combatMode && _layerMode != LayerMode.Utility)
            {
                TryWeaponAttack();
                return;
            }
    
            if (_layerMode == LayerMode.Utility)
            {
                BreakUtilityAtCursor();
                return;
            }
    
            BreakAtCursor();
        }
    
        void HandleRightClick()
        {
            if (TryCorpseInteraction())
                return;
    
            if (_layerMode == LayerMode.Utility)
            {
                TryItemInteraction_UtilityOnly();
                return;
            }
    
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    
            if (!shift)
            {
                if (TryCellInteraction()) return;
                if (TryItemInteraction()) return;
            }
            else
            {
                if (TryItemInteraction()) return;
                if (TryCellInteraction()) return;
            }
        }
    
        void BreakAtCursor()
        {
            if (!GetMouseCell(out int cx, out int cy)) return;
    
            ushort solidId = worldManager.GetSolidId(cx, cy);
            ushort bgId = worldManager.GetBGId(cx, cy);
    
            bool hasSolid = solidId != 0;
            bool hasBg = bgId != 0;
    
            if (_layerMode == LayerMode.Solid)
            {
                if (!hasSolid) return;
    
                Multiblock mb = multiblockManager.GetAtCell(new Vector2Int(cx, cy));
    
                worldManager.BreakSolid(cx, cy);
                sound.PlayDig();
    
                if (mb != null)
                    mb.OnCellBroken(new Vector2Int(cx, cy));
            }
            else
            {
                if (!hasBg) return;
                if (hasSolid) return;
                worldManager.BreakBG(cx, cy);
                sound.PlayDig();
            }
        }
    
        void BreakUtilityAtCursor()
        {
            if (worldManager == null) return;
            if (!GetMouseCell(out int cx, out int cy)) return;
    
            ushort uid = worldManager.GetUtilityId(cx, cy);
            if (uid == 0) return;
    
            if (IsUtilityOccupiedCell(cx, cy))
                return;
    
            ushort removed = worldManager.BreakUtility(cx, cy);
            if (removed == 0) return;
    
            sound.PlayDig();
        }
    
        bool TryItemInteraction()
        {
            if (_state != GameState.Ingame) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (!GetMouseCell(out int cx, out int cy))
                return false;
    
            var items = player.Inventory.items;
            if (_hotbarScope < 0 || _hotbarScope >= items.Count)
                return false;
    
            var held = items[_hotbarScope];
            if (held == null || held.Count <= 0)
                return false;
    
            if (held.ToolActions == null || held.ToolActions.Count == 0)
                return false;
    
            foreach (var kv in held.ToolActions)
            {
                string actionName = kv.Key;
                var param = kv.Value ?? new Dictionary<string, object>();
    
                bool ok = false;
    
                if (actionName == "Place")
                    ok = HandlePlace(held, cx, cy, param);
                else if (actionName == "PlaceGear")
                    ok = HandlePlaceGear(held, cx, cy, param);
                else if (actionName == "AttachSource")
                    ok = HandleAttachSource(held, cx, cy, param);
                else if (actionName == "AttachBelt")
                    ok = HandleAttachBelt(held, cx, cy, param);
                else if (actionName == "PlaceSource")
                    ok = HandlePlaceSource(held, cx, cy, param);
                else if (actionName == "PlaceBelt")
                    ok = HandlePlaceBelt(held, cx, cy, param);
                else if (actionName == "BuildMultiblock")
                    ok = HandleBuildMultiblock(held, cx, cy, param);
    
                if (ok) return true;
            }
    
            return false;
        }
    
        bool TryItemInteraction_UtilityOnly()
        {
            if (_state != GameState.Ingame) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (!GetMouseCell(out int cx, out int cy))
                return false;
    
            var items = player.Inventory.items;
            if (_hotbarScope < 0 || _hotbarScope >= items.Count)
                return false;
    
            var held = items[_hotbarScope];
            if (held == null || held.Count <= 0)
                return false;
    
            if (held.ToolActions == null)
                return false;
    
            if (!held.ToolActions.TryGetValue("PlaceUtility", out var pObj))
                return false;
    
            if (pObj is not Dictionary<string, object> p)
                return false;
    
            return HandlePlaceUtility(held, cx, cy, p);
        }
    
        bool HandlePlaceUtility(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
        {
            if (worldManager == null || gearNetworkManager == null || cellLibrary == null)
                return false;
    
            if (!TryGetPlaceUtilityParam(placeParam, out var type, out var cell, out _))
                return false;
    
            if (IsUtilityOccupiedCell(cx, cy))
                return false;
    
            var cellPos = new Vector2Int(cx, cy);
    
            if (type == "Cogwheel")
            {
                if (!cellLibrary.TryGetUtilityIdByName(cell, out ushort centerId) || centerId == 0)
                    return false;
    
                if (!TryGetCogwheelPlacementSpec(cell, out var size, out int maxRpm))
                    return false;
    
                gearNetworkManager.RegisterCogwheelSpec(cell, size, maxRpm);
    
                var occupiedCells =
                    (size == GearNode.GearSize.Big)
                    ? new List<Vector2Int>
                    {
                        cellPos + Vector2Int.up,
                        cellPos + Vector2Int.down,
                        cellPos + Vector2Int.left,
                        cellPos + Vector2Int.right
                    }
                    : null;
    
                if (!gearNetworkManager.CanPlaceGear(cellPos, cell))
                    return false;
    
                ushort occId = _utilityOccupiedId;
                if (size == GearNode.GearSize.Big && occId == 0)
                    return false;
    
                if (!gearNetworkManager.TryAddGear(cellPos, cell, out _))
                    return false;
    
                if (!worldManager.PlaceGearFootprintUtility(cellPos, centerId, 0, occId, occupiedCells))
                {
                    gearNetworkManager.TryRemoveGearAt(cellPos, out _, out _, out _);
                    return false;
                }
    
                sound.PlayPlace();
    
                held.Count -= 1;
                if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
                player.Inventory.NotifyChanged();
    
                RefreshHeldHandSprite();
                return true;
            }
    
            return false;
        }
    
        bool HandlePlaceSource(ItemData held, int cx, int cy, Dictionary<string, object> param)
        {
            if (gearNetworkManager == null || worldManager == null || worldManager.cellLibrary == null)
                return false;
    
            if (!TryGetCellParam(param, out var cell))
                return false;
    
            if (IsUtilityOccupiedCell(cx, cy))
                return false;
    
            if (!IsUtilityCenterCell(cx, cy))
                return false;
    
            var cellPos = new Vector2Int(cx, cy);
    
            if (!gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out _))
                return false;
    
            if (!worldManager.cellLibrary.TryGetSolidIdByName(cell, out ushort solidId) || solidId == 0)
                return false;
    
            if (worldManager.GetSolidId(cx, cy) != 0)
                return false;
    
            if (utilityLibrary == null || !utilityLibrary.TryGetSource(cell, out var sourceDef))
                return false;
    
            if (cell == "Windmill")
                gearNetworkManager.RegisterSourceSpec(cell, SourceNode.SourceKind.Windmill, sourceDef.rpm, sourceDef.stressCapacity);
            else if (cell == "Waterwheel")
                gearNetworkManager.RegisterSourceSpec(cell, SourceNode.SourceKind.Waterwheel, sourceDef.rpm, sourceDef.stressCapacity);
            else
                return false;
    
            if (!gearNetworkManager.TryAttachSourceAtCell(cellPos, cell, out _))
                return false;
    
            if (!worldManager.PlaceSolidExact(cx, cy, solidId))
            {
                gearNetworkManager.TryRemoveSourceAtGearCell(cellPos, out _);
                return false;
            }
    
            sound.PlayPlace();
    
            held.Count -= 1;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();
    
            RefreshHeldHandSprite();
            return true;
        }
    
        bool HandlePlaceBelt(ItemData held, int cx, int cy, Dictionary<string, object> param)
        {
            if (gearNetworkManager == null || worldManager == null || worldManager.cellLibrary == null)
                return false;
    
            if (!TryGetCellParam(param, out var beltKind))
                return false;
    
            if (IsUtilityOccupiedCell(cx, cy))
                return false;
    
            if (!IsUtilityCenterCell(cx, cy))
                return false;
    
            var cellPos = new Vector2Int(cx, cy);
    
            if (!gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out _))
                return false;
    
            if (!worldManager.cellLibrary.TryGetSolidIdByName(beltKind, out ushort beltSolidId) || beltSolidId == 0)
                return false;
    
            if (!_beltPending)
            {
                if (worldManager.GetSolidId(cx, cy) != 0)
                    return false;
    
                _beltPending = true;
                _beltStartCell = cellPos;
                _beltPendingKind = beltKind;
                _beltPendingScope = _hotbarScope;
                _beltPendingHeldRef = held;
                return true;
            }
    
            if (_hotbarScope != _beltPendingScope || held != _beltPendingHeldRef || _beltPendingKind != beltKind)
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (!gearNetworkManager.TryGetGearNodeIdAtCell(_beltStartCell, out int g0) ||
                !gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out int g1))
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (g0 == g1)
            {
                CancelBeltPlacement();
                return false;
            }
    
            const int BELT_COST = 2;
            if (held.Count < BELT_COST)
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (worldManager.GetSolidId(_beltStartCell.x, _beltStartCell.y) != 0 ||
                worldManager.GetSolidId(cx, cy) != 0)
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (!gearNetworkManager.TryAttachBeltAtCells(_beltStartCell, cellPos, beltKind, out _))
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (!worldManager.PlaceSolidExact(_beltStartCell.x, _beltStartCell.y, beltSolidId))
            {
                gearNetworkManager.TryRemoveBeltAtGearCell(_beltStartCell, out _, out _);
                CancelBeltPlacement();
                return false;
            }
    
            if (!worldManager.PlaceSolidExact(cx, cy, beltSolidId))
            {
                worldManager.OverwriteSolid(_beltStartCell.x, _beltStartCell.y, 0, 0);
                gearNetworkManager.TryRemoveBeltAtGearCell(_beltStartCell, out _, out _);
                CancelBeltPlacement();
                return false;
            }
    
            sound.PlayPlace();
    
            held.Count -= BELT_COST;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();
    
            RefreshHeldHandSprite();
            CancelBeltPlacement();
            return true;
        }
    
        bool TryGetGearPlaceInfo(Dictionary<string, object> placeParam, out string gearId, out string cellName)
        {
            gearId = null;
            cellName = null;
    
            if (gearNetworkManager == null || worldManager == null || worldManager.cellLibrary == null)
                return false;
    
            if (placeParam == null) return false;
    
            if (placeParam.TryGetValue("gearId", out var g0) && g0 != null) gearId = g0.ToString();
            else if (placeParam.TryGetValue("gear", out var g1) && g1 != null) gearId = g1.ToString();
            else if (placeParam.TryGetValue("cell", out var c0) && c0 != null) gearId = c0.ToString();
    
            if (placeParam.TryGetValue("cell", out var c1) && c1 != null) cellName = c1.ToString();
            else cellName = gearId;
    
            if (string.IsNullOrEmpty(gearId))
                return false;
    
            cellName = gearId;
            return true;
        }
    
        bool HandlePlaceGear(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
        {
            if (worldManager == null || gearNetworkManager == null)
                return false;
    
            if (!TryGetGearPlaceInfo(placeParam, out var gearId, out var cellName))
                return false;
    
            var center = new Vector2Int(cx, cy);
    
            if (!gearNetworkManager.CanPlaceGear(center, gearId))
                return false;
    
            if (!worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId))
                return false;
    
            if (placeId == 0)
                return false;
    
            if (!worldManager.PlaceSolidExact(cx, cy, placeId))
                return false;
    
            if (!gearNetworkManager.TryAddGear(center, gearId, out _))
            {
                worldManager.OverwriteSolid(cx, cy, 0, 0);
                return false;
            }
    
            sound.PlayPlace();
    
            held.Count -= 1;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();
    
            RefreshHeldHandSprite();
            return true;
        }
    
        bool HandleAttachSource(ItemData held, int cx, int cy, Dictionary<string, object> param)
        {
            if (gearNetworkManager == null)
                return false;
    
            string sourceKind = null;
            if (param != null)
            {
                if (param.TryGetValue("sourceKind", out var sk) && sk != null) sourceKind = sk.ToString();
                else if (param.TryGetValue("kind", out var k) && k != null) sourceKind = k.ToString();
            }
    
            if (string.IsNullOrEmpty(sourceKind))
                return false;
    
            var cell = new Vector2Int(cx, cy);
    
            if (!gearNetworkManager.IsGearOccupiedCell(cell))
                return false;
    
            if (!gearNetworkManager.TryAttachSourceAtCell(cell, sourceKind, out _))
                return false;
    
            sound.PlayPlace();
    
            held.Count -= 1;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();
    
            RefreshHeldHandSprite();
            return true;
        }
    
        bool HandleAttachBelt(ItemData held, int cx, int cy, Dictionary<string, object> param)
        {
            if (gearNetworkManager == null)
                return false;
    
            string beltKind = null;
            if (param != null)
            {
                if (param.TryGetValue("beltKind", out var bk) && bk != null) beltKind = bk.ToString();
                else if (param.TryGetValue("kind", out var k) && k != null) beltKind = k.ToString();
            }
    
            if (string.IsNullOrEmpty(beltKind))
                return false;
    
            var cell = new Vector2Int(cx, cy);
    
            if (!_beltPending)
            {
                if (!gearNetworkManager.IsGearOccupiedCell(cell))
                    return false;
    
                _beltPending = true;
                _beltStartCell = cell;
                _beltPendingKind = beltKind;
                _beltPendingScope = _hotbarScope;
                _beltPendingHeldRef = held;
    
                return true;
            }
    
            if (_hotbarScope != _beltPendingScope || held != _beltPendingHeldRef || _beltPendingKind != beltKind)
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (!gearNetworkManager.IsGearOccupiedCell(cell))
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (!gearNetworkManager.TryGetGearNodeIdAtCell(_beltStartCell, out int g0) ||
                !gearNetworkManager.TryGetGearNodeIdAtCell(cell, out int g1))
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (g0 == g1)
            {
                CancelBeltPlacement();
                return false;
            }
    
            if (!gearNetworkManager.TryAttachBeltAtCells(_beltStartCell, cell, beltKind, out int cost))
            {
                CancelBeltPlacement();
                return false;
            }
    
            const int BELT_COST = 2;
            if (held.Count < BELT_COST)
            {
                CancelBeltPlacement();
                return false;
            }
    
            sound.PlayPlace();
    
            held.Count -= BELT_COST;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();
    
            RefreshHeldHandSprite();
            CancelBeltPlacement();
            return true;
        }
    
        bool TryCorpseInteraction()
        {
            if (_state != GameState.Ingame) return false;
            if (_hoverCorpse == null) return false;
    
            var items = player.Inventory.items;
            if (_hotbarScope < 0 || _hotbarScope >= items.Count)
                return false;
    
            var held = items[_hotbarScope];
            if (held == null || held.Count <= 0)
                return false;
    
            if (held.ToolActions == null || held.ToolActions.Count == 0)
                return false;
    
            foreach (var kv in held.ToolActions)
            {
                string actionName = kv.Key;
                if (string.IsNullOrEmpty(actionName))
                    continue;
    
                if (corpseLibrary.TryProcessCorpse(_hoverCorpse, actionName))
                {
                    _hoverCorpse.SetHovered(false);
                    _hoverCorpse = null;
                    return true;
                }
            }
    
            return false;
        }
    
        bool TryCellInteraction()
        {
            if (_state != GameState.Ingame) return false;
            if (!GetMouseCell(out int cx, out int cy)) return false;
    
            var mb = multiblockManager.GetAtCell(new Vector2Int(cx, cy));
            if (mb != null)
            {
                mb.OnInteract(player, new Vector2Int(cx, cy));
                return true;
            }
    
            return false;
        }
    
        void ComputeRelativeDirs(int cx, int cy, out WorldManager.RelV relV, out WorldManager.RelH relH)
        {
            float half = cellSize * 0.5f;
            float cellCenterX = cx * cellSize + half;
            float cellCenterY = cy * cellSize + half;
    
            Vector3 p = player.transform.position;
    
            float dx = p.x - cellCenterX;
            float dy = p.y - cellCenterY;
    
            const float EPS = 0.001f;
    
            if (dy > EPS) relV = WorldManager.RelV.Up;
            else if (dy < -EPS) relV = WorldManager.RelV.Down;
            else relV = WorldManager.RelV.Neutral;
    
            if (dx > EPS) relH = WorldManager.RelH.Right;
            else if (dx < -EPS) relH = WorldManager.RelH.Left;
            else relH = WorldManager.RelH.Neutral;
        }
    
        bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
        {
            string layerStr = placeParam.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
            string cellName = placeParam.TryGetValue("cell", out var cellObj) ? cellObj?.ToString() : null;
    
            ushort solidId = worldManager.GetSolidId(cx, cy);
            ushort bgId = worldManager.GetBGId(cx, cy);
    
            bool hasSolid = solidId != 0;
            bool hasBg = bgId != 0;
    
            WorldManager.CellLayer targetLayer;
    
            if (string.Equals(layerStr, "Dynamic", StringComparison.OrdinalIgnoreCase))
            {
                targetLayer = (_layerMode == LayerMode.BG)
                    ? WorldManager.CellLayer.BG
                    : WorldManager.CellLayer.Solid;
            }
            else if (string.Equals(layerStr, "BG", StringComparison.OrdinalIgnoreCase))
            {
                targetLayer = WorldManager.CellLayer.BG;
            }
            else
            {
                targetLayer = WorldManager.CellLayer.Solid;
            }
    
            if (targetLayer == WorldManager.CellLayer.BG)
            {
                if (hasSolid) return false;
                if (hasBg) return false;
            }
    
            worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId);
            if (placeId == 0) return false;
    
            ComputeRelativeDirs(cx, cy, out var relV, out var relH);
    
            bool placed =
                (targetLayer == WorldManager.CellLayer.Solid)
                    ? worldManager.PlaceSolid(cx, cy, placeId, relV, relH)
                    : worldManager.PlaceBG(cx, cy, placeId, relV, relH);
    
            if (!placed) return false;
    
            sound.PlayPlace();
    
            held.Count -= 1;
            if (held.Count <= 0) player.Inventory.items[_hotbarScope] = null;
            player.Inventory.NotifyChanged();
    
            RefreshHeldHandSprite();
            return true;
        }
    
        bool HandleBuildMultiblock(ItemData held, int cx, int cy, Dictionary<string, object> param)
        {
            ushort solidId = worldManager.GetSolidId(cx, cy);
            if (solidId == 0) return false;
    
            if (multiblockManager.GetAtCell(new Vector2Int(cx, cy)) != null)
                return false;
    
            string clickedKey = worldManager.cellLibrary.GetSolidName(solidId);
    
            if (!MultiblockLibrary.TryGetByIngredient(clickedKey, out var defs) || defs.Count == 0)
                return false;
    
            int worldW = worldManager.settings.width;
            int worldH = worldManager.settings.height;
    
            MultiblockLibrary.Def bestDef = null;
            int bestOx = 0;
            int bestOy = 0;
            int bestArea = -1;
    
            for (int di = 0; di < defs.Count; di++)
            {
                var def = defs[di];
    
                int patternWidth = def.width;
                int patternHeight = def.height;
    
                if (patternWidth <= 0 || patternHeight <= 0) continue;
    
                for (int py = 0; py < patternHeight; py++)
                {
                    for (int px = 0; px < patternWidth; px++)
                    {
                        string patternKey = def.pattern[px, py];
                        if (patternKey != clickedKey) continue;
    
                        int originX = cx - px;
                        int originY = cy - py;
    
                        if (originX < 0 || originY < 0 ||
                            originX + patternWidth > worldW ||
                            originY + patternHeight > worldH)
                            continue;
    
                        bool mismatch = false;
    
                        for (int ly = 0; ly < patternHeight && !mismatch; ly++)
                        {
                            for (int lx = 0; lx < patternWidth; lx++)
                            {
                                int wx = originX + lx;
                                int wy = originY + ly;
    
                                if (multiblockManager.GetAtCell(new Vector2Int(wx, wy)) != null)
                                {
                                    mismatch = true;
                                    break;
                                }
    
                                ushort wid = worldManager.GetSolidId(wx, wy);
                                string worldKey = worldManager.cellLibrary.GetSolidName(wid);
    
                                if (worldKey != def.pattern[lx, ly])
                                {
                                    mismatch = true;
                                    break;
                                }
                            }
                        }
    
                        if (!mismatch)
                        {
                            int area = patternWidth * patternHeight;
    
                            if (area > bestArea)
                            {
                                bestArea = area;
                                bestDef = def;
                                bestOx = originX;
                                bestOy = originY;
                            }
                        }
                    }
                }
            }
    
            if (bestDef != null)
            {
                multiblockManager.Create(bestDef, bestOx, bestOy);
                sound.PlayMultiblockComplete();
                return true;
            }
    
            return false;
        }
    }
}
