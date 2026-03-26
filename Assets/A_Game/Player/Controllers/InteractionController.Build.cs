using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        bool TryGetPlaceUtilityParam(
            Dictionary<string, object> placeParam,
            out string type,
            out string cell,
            out Dictionary<string, object> typeObj)
            => _utilityInteractionService.TryGetPlaceUtilityParam(placeParam, out type, out cell, out typeObj);

        bool TryGetCogwheelPlacementSpec(string cell, out GearNode.GearSize size, out int maxRpm)
            => _utilityInteractionService.TryGetCogwheelPlacementSpec(cell, out size, out maxRpm);

        bool IsUtilityOccupiedCell(int x, int y)
            => _utilityInteractionService.IsUtilityOccupiedCell(x, y);

        bool IsUtilityCenterCell(int x, int y)
            => _utilityInteractionService.IsUtilityCenterCell(x, y);

        void HandleLeftClick()
        {
            if (_combatMode && _layerMode != LayerMode.Utility)
            {
                TryWeaponAttack();
                return;
            }

            if (_layerMode == LayerMode.Utility)
            {
                _utilityInteractionService.BreakUtilityAtCursor();
                return;
            }

            _blockInteractionService.BreakAtCursor();
        }

        void HandleRightClick()
        {
            if (_corpseInteractionService.TryCorpseInteraction())
                return;

            if (_layerMode == LayerMode.Utility)
            {
                TryItemInteraction_UtilityOnly();
                return;
            }

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            if (!shift)
            {
                if (_multiblockInteractionService.TryCellInteraction()) return;
                if (TryItemInteraction()) return;
            }
            else
            {
                if (TryItemInteraction()) return;
                if (_multiblockInteractionService.TryCellInteraction()) return;
            }
        }

        bool TryItemInteraction()
        {
            if (_state != GameState.Ingame) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return false;
            if (!GetMouseCell(out int cx, out int cy))
                return false;

            if (!_heldItemService.TryGetHeldItem(out var held))
                return false;

            if (held.ToolActions == null || held.ToolActions.Count == 0)
                return false;

            foreach (var kv in held.ToolActions)
            {
                string actionName = kv.Key;
                var param = kv.Value ?? new Dictionary<string, object>();

                bool ok = actionName switch
                {
                    "Place" => _blockInteractionService.HandlePlace(held, cx, cy, param),
                    "PlaceGear" => _gearInteractionService.HandlePlaceGear(held, cx, cy, param),
                    "AttachSource" => _gearInteractionService.HandleAttachSource(held, cx, cy, param),
                    "AttachBelt" => _gearInteractionService.HandleAttachBelt(held, cx, cy, param),
                    "PlaceSource" => _gearInteractionService.HandlePlaceSource(held, cx, cy, param),
                    "PlaceBelt" => _gearInteractionService.HandlePlaceBelt(held, cx, cy, param),
                    "BuildMultiblock" => _multiblockInteractionService.HandleBuildMultiblock(held, cx, cy, param),
                    _ => false
                };

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

            if (!_heldItemService.TryGetHeldItem(out var held))
                return false;

            if (held.ToolActions == null)
                return false;

            if (!held.ToolActions.TryGetValue("PlaceUtility", out var pObj))
                return false;

            if (pObj is not Dictionary<string, object> p)
                return false;

            return _utilityInteractionService.HandlePlaceUtility(held, cx, cy, p);
        }
    }
}
