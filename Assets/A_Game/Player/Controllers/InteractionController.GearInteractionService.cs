


using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        private sealed class GearInteractionService
        {

            readonly InteractionController _owner;

            
            public GearInteractionService(InteractionController owner)
            {
                _owner = owner;
            }

            
            public bool HandlePlaceSource(ItemData held, int cx, int cy, Dictionary<string, object> param)
            {
                if (_owner.gearNetworkManager == null || _owner.worldManager == null || _owner.worldManager.cellLibrary == null)
                    return false;

                if (!_owner._utilityInteractionService.TryGetCellParam(param, out var cell))
                    return false;

                if (_owner._utilityInteractionService.IsUtilityOccupiedCell(cx, cy))
                    return false;

                if (!_owner._utilityInteractionService.IsUtilityCenterCell(cx, cy))
                    return false;

                var cellPos = new Vector2Int(cx, cy);

                if (!_owner.gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out _))
                    return false;

                if (!_owner.worldManager.cellLibrary.TryGetSolidIdByName(cell, out ushort solidId) || solidId == 0)
                    return false;

                if (_owner.worldManager.GetSolidId(cx, cy) != 0)
                    return false;

                if (_owner.utilityLibrary == null || !_owner.utilityLibrary.TryGetSource(cell, out var sourceDef))
                    return false;

                if (cell == "Windmill")
                    _owner.gearNetworkManager.RegisterSourceSpec(cell, SourceNode.SourceKind.Windmill, sourceDef.rpm, sourceDef.stressCapacity);
                
                else if (cell == "Waterwheel")
                    _owner.gearNetworkManager.RegisterSourceSpec(cell, SourceNode.SourceKind.Waterwheel, sourceDef.rpm, sourceDef.stressCapacity);
                else
                    return false;

                if (!_owner.gearNetworkManager.TryAttachSourceAtCell(cellPos, cell, out _))
                    return false;

                if (!_owner.worldManager.PlaceSolidExact(cx, cy, solidId))
                {
                    _owner.gearNetworkManager.TryRemoveSourceAtGearCell(cellPos, out _);
                    return false;
                }

                _owner.sound.PlayPlace();
                _owner._heldItemService.Consume(held, 1);
                return true;
            }

            
            public bool HandlePlaceBelt(ItemData held, int cx, int cy, Dictionary<string, object> param)
            {
                if (_owner.gearNetworkManager == null || _owner.worldManager == null || _owner.worldManager.cellLibrary == null)
                    return false;

                if (!_owner._utilityInteractionService.TryGetCellParam(param, out var beltKind))
                    return false;

                if (_owner._utilityInteractionService.IsUtilityOccupiedCell(cx, cy))
                    return false;

                if (!_owner._utilityInteractionService.IsUtilityCenterCell(cx, cy))
                    return false;

                var cellPos = new Vector2Int(cx, cy);

                if (!_owner.gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out _))
                    return false;

                if (!_owner.worldManager.cellLibrary.TryGetSolidIdByName(beltKind, out ushort beltSolidId) || beltSolidId == 0)
                    return false;

                if (!_owner._beltPending)
                {
                    if (_owner.worldManager.GetSolidId(cx, cy) != 0)
                        return false;

                    _owner._beltPending = true;
                    _owner._beltStartCell = cellPos;
                    _owner._beltPendingKind = beltKind;
                    _owner._beltPendingScope = _owner._hotbarScope;
                    _owner._beltPendingHeldRef = held;
                    return true;
                }

                if (_owner._hotbarScope != _owner._beltPendingScope || held != _owner._beltPendingHeldRef || _owner._beltPendingKind != beltKind)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.gearNetworkManager.TryGetGearNodeIdAtCell(_owner._beltStartCell, out int g0) ||
                    !_owner.gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out int g1))
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (g0 == g1)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                const int BELT_COST = 2;
                if (held.Count < BELT_COST)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (_owner.worldManager.GetSolidId(_owner._beltStartCell.x, _owner._beltStartCell.y) != 0 ||
                    _owner.worldManager.GetSolidId(cx, cy) != 0)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.gearNetworkManager.TryAttachBeltAtCells(_owner._beltStartCell, cellPos, beltKind, out _))
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.worldManager.PlaceSolidExact(_owner._beltStartCell.x, _owner._beltStartCell.y, beltSolidId))
                {
                    _owner.gearNetworkManager.TryRemoveBeltAtGearCell(_owner._beltStartCell, out _, out _);
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.worldManager.PlaceSolidExact(cx, cy, beltSolidId))
                {
                    _owner.worldManager.OverwriteSolid(_owner._beltStartCell.x, _owner._beltStartCell.y, 0, 0);
                    _owner.gearNetworkManager.TryRemoveBeltAtGearCell(_owner._beltStartCell, out _, out _);
                    _owner.CancelBeltPlacement();
                    return false;
                }

                _owner.sound.PlayPlace();
                _owner._heldItemService.Consume(held, BELT_COST);
                _owner.CancelBeltPlacement();
                return true;
            }

            
            public bool HandlePlaceGear(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
            {
                if (_owner.worldManager == null || _owner.gearNetworkManager == null)
                    return false;

                if (!TryGetGearPlaceInfo(placeParam, out var gearId, out var cellName))
                    return false;

                var center = new Vector2Int(cx, cy);

                if (!_owner.gearNetworkManager.CanPlaceGear(center, gearId))
                    return false;

                if (!_owner.worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId))
                    return false;

                if (placeId == 0)
                    return false;

                if (!_owner.worldManager.PlaceSolidExact(cx, cy, placeId))
                    return false;

                if (!_owner.gearNetworkManager.TryAddGear(center, gearId, out _))
                {
                    _owner.worldManager.OverwriteSolid(cx, cy, 0, 0);
                    return false;
                }

                _owner.sound.PlayPlace();
                _owner._heldItemService.Consume(held, 1);
                return true;
            }

            
            public bool HandleAttachSource(ItemData held, int cx, int cy, Dictionary<string, object> param)
            {
                if (_owner.gearNetworkManager == null)
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

                if (!_owner.gearNetworkManager.IsGearOccupiedCell(cell))
                    return false;

                if (!_owner.gearNetworkManager.TryAttachSourceAtCell(cell, sourceKind, out _))
                    return false;

                _owner.sound.PlayPlace();
                _owner._heldItemService.Consume(held, 1);
                return true;
            }

            
            public bool HandleAttachBelt(ItemData held, int cx, int cy, Dictionary<string, object> param)
            {
                if (_owner.gearNetworkManager == null)
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

                if (!_owner._beltPending)
                {
                    if (!_owner.gearNetworkManager.IsGearOccupiedCell(cell))
                        return false;

                    _owner._beltPending = true;
                    _owner._beltStartCell = cell;
                    _owner._beltPendingKind = beltKind;
                    _owner._beltPendingScope = _owner._hotbarScope;
                    _owner._beltPendingHeldRef = held;

                    return true;
                }

                if (_owner._hotbarScope != _owner._beltPendingScope || held != _owner._beltPendingHeldRef || _owner._beltPendingKind != beltKind)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.gearNetworkManager.IsGearOccupiedCell(cell))
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.gearNetworkManager.TryGetGearNodeIdAtCell(_owner._beltStartCell, out int g0) ||
                    !_owner.gearNetworkManager.TryGetGearNodeIdAtCell(cell, out int g1))
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (g0 == g1)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                if (!_owner.gearNetworkManager.TryAttachBeltAtCells(_owner._beltStartCell, cell, beltKind, out _))
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                const int BELT_COST = 2;
                if (held.Count < BELT_COST)
                {
                    _owner.CancelBeltPlacement();
                    return false;
                }

                _owner.sound.PlayPlace();
                _owner._heldItemService.Consume(held, BELT_COST);
                _owner.CancelBeltPlacement();
                return true;
            }

            
            bool TryGetGearPlaceInfo(Dictionary<string, object> placeParam, out string gearId, out string cellName)
            {
                gearId = null;
                cellName = null;

                if (_owner.gearNetworkManager == null || _owner.worldManager == null || _owner.worldManager.cellLibrary == null)
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
        }
    }
}
