


using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        private sealed class UtilityInteractionService
        {

            readonly InteractionController _owner;

            
            public UtilityInteractionService(InteractionController owner)
            {
                _owner = owner;
            }

            
            public bool TryGetPlaceUtilityParam(
                Dictionary<string, object> placeParam,
                out string type,
                out string cell,
                out Dictionary<string, object> typeObj)
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

            
            public bool TryGetCellParam(Dictionary<string, object> p, out string cell)
            {
                cell = null;
                if (p == null) return false;
                if (!p.TryGetValue("cell", out var c) || c == null) return false;
                cell = c.ToString();
                return !string.IsNullOrEmpty(cell);
            }

            
            public bool TryGetCogwheelPlacementSpec(string cell, out GearNode.GearSize size, out int maxRpm)
            {
                size = GearNode.GearSize.Small;
                maxRpm = 0;

                if (string.IsNullOrEmpty(cell) || _owner.utilityLibrary == null)
                    return false;

                if (!_owner.utilityLibrary.TryGetCogwheel(cell, out var def))
                    return false;

                size = (def.size == "Big") ? GearNode.GearSize.Big : GearNode.GearSize.Small;
                maxRpm = def.maxRpm;
                return true;
            }

            
            public bool IsUtilityOccupiedCell(int x, int y)
            {
                if (_owner.worldManager == null) return false;
                ushort uid = _owner.worldManager.GetUtilityId(x, y);
                if (uid == 0) return false;
                return (_owner._utilityOccupiedId != 0 && uid == _owner._utilityOccupiedId);
            }

            
            public bool IsUtilityCenterCell(int x, int y)
            {
                if (_owner.worldManager == null) return false;
                ushort uid = _owner.worldManager.GetUtilityId(x, y);
                if (uid == 0) return false;
                return (_owner._utilityOccupiedId == 0 || uid != _owner._utilityOccupiedId);
            }

            
            public void BreakUtilityAtCursor()
            {
                if (_owner.worldManager == null) return;
                if (!_owner.GetMouseCell(out int cx, out int cy)) return;

                ushort uid = _owner.worldManager.GetUtilityId(cx, cy);
                if (uid == 0) return;

                if (IsUtilityOccupiedCell(cx, cy))
                    return;

                ushort removed = _owner.worldManager.BreakUtility(cx, cy);
                if (removed == 0) return;

                _owner.sound.PlayDig();
            }

            
            public bool HandlePlaceUtility(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
            {
                if (_owner.worldManager == null || _owner.gearNetworkManager == null || _owner.cellLibrary == null)
                    return false;

                if (!TryGetPlaceUtilityParam(placeParam, out var type, out var cell, out _))
                    return false;

                if (IsUtilityOccupiedCell(cx, cy))
                    return false;

                var cellPos = new Vector2Int(cx, cy);

                if (type == "Cogwheel")
                {
                    if (!_owner.cellLibrary.TryGetUtilityIdByName(cell, out ushort centerId) || centerId == 0)
                        return false;

                    if (!TryGetCogwheelPlacementSpec(cell, out var size, out int maxRpm))
                        return false;

                    _owner.gearNetworkManager.RegisterCogwheelSpec(cell, size, maxRpm);

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

                    if (!_owner.gearNetworkManager.CanPlaceGear(cellPos, cell))
                        return false;

                    ushort occId = _owner._utilityOccupiedId;
                    if (size == GearNode.GearSize.Big && occId == 0)
                        return false;

                    if (!_owner.gearNetworkManager.TryAddGear(cellPos, cell, out _))
                        return false;

                    if (!_owner.worldManager.PlaceGearFootprintUtility(cellPos, centerId, 0, occId, occupiedCells))
                    {
                        _owner.gearNetworkManager.TryRemoveGearAt(cellPos, out _, out _, out _);
                        return false;
                    }

                    _owner.sound.PlayPlace();
                    _owner._heldItemService.Consume(held, 1);
                    return true;
                }

                return false;
            }
        }
    }
}
