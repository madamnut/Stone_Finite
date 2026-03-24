using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        void UpdateCorpseHoverState()
        {
            Corpse newHoverCorpse = null;
            Vector3 mouseWorld3 = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mousePos2 = new Vector2(mouseWorld3.x, mouseWorld3.y);
    
            var hits = Physics2D.OverlapPointAll(mousePos2, corpseLayerMask);
            int bestOrder = int.MinValue;
    
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
    
                var corpse = col.GetComponentInParent<Corpse>();
                if (corpse == null) continue;
    
                int order = 0;
                if (corpse.mainRenderer != null)
                    order = corpse.mainRenderer.sortingOrder;
    
                if (newHoverCorpse == null || order > bestOrder)
                {
                    newHoverCorpse = corpse;
                    bestOrder = order;
                }
            }
    
            if (newHoverCorpse != _hoverCorpse)
            {
                if (_hoverCorpse != null)
                    _hoverCorpse.SetHovered(false);
    
                _hoverCorpse = newHoverCorpse;
    
                if (_hoverCorpse != null)
                    _hoverCorpse.SetHovered(true);
            }
        }
    
        void UpdateHighlight()
        {
            if (!GetMouseCell(out int cx, out int cy))
            {
                _hlGO.SetActive(false);
                return;
            }
    
            float half = cellSize * 0.5f;
            _hlGO.transform.position = new Vector3(cx * cellSize + half, cy * cellSize + half, 0f);
    
            if (_layerMode == LayerMode.Utility)
            {
                ItemData held = GetHeldItem();
    
                if (held != null && held.Count > 0 && held.ToolActions != null &&
                    held.ToolActions.TryGetValue("PlaceUtility", out var pObj) &&
                    pObj is Dictionary<string, object> p &&
                    TryGetPlaceUtilityParam(p, out var type, out var cell, out _))
                {
                    if (type == "Cogwheel")
                    {
                        bool can = false;
    
                        if (worldManager != null && cellLibrary != null &&
                            TryGetCogwheelPlacementSpec(cell, out var size, out _))
                        {
                            if (cellLibrary.TryGetUtilityIdByName(cell, out ushort centerId) && centerId != 0)
                                can = gearNetworkManager != null && gearNetworkManager.CanPlaceGear(new Vector2Int(cx, cy), size);
                        }
    
                        _hlSR.sprite = can
                            ? (HighLight_Utility_CAN != null ? HighLight_Utility_CAN : HighLight_Solid_CAN)
                            : (HighLight_Utility_CANNOT != null ? HighLight_Utility_CANNOT : HighLight_Solid_CANNOT);
    
                        PulseHighlight();
                        return;
                    }
                }
    
                ushort uid2 = (worldManager != null) ? worldManager.GetUtilityId(cx, cy) : (ushort)0;
                bool has = uid2 != 0;
                bool canBreak = has && !IsUtilityOccupiedCell(cx, cy);
    
                _hlSR.sprite = canBreak
                    ? (HighLight_Utility_CAN != null ? HighLight_Utility_CAN : HighLight_Solid_CAN)
                    : (HighLight_Utility != null ? HighLight_Utility : HighLight_Solid);
    
                PulseHighlight();
                return;
            }
    
            {
                ItemData held = GetHeldItem();
                if (held != null && held.Count > 0 && held.ToolActions != null)
                {
                    bool isSource = held.ToolActions.TryGetValue("PlaceSource", out var psObj) && psObj is Dictionary<string, object>;
                    bool isBelt = held.ToolActions.TryGetValue("PlaceBelt", out var pbObj) && pbObj is Dictionary<string, object>;
    
                    if (isSource || isBelt)
                    {
                        bool can = false;
    
                        if (!IsUtilityOccupiedCell(cx, cy) && IsUtilityCenterCell(cx, cy))
                        {
                            var cellPos = new Vector2Int(cx, cy);
    
                            if (gearNetworkManager != null && gearNetworkManager.TryGetGearNodeIdAtCell(cellPos, out int n1))
                            {
                                if (isSource)
                                {
                                    can = worldManager != null && worldManager.GetSolidId(cx, cy) == 0;
                                }
                                else
                                {
                                    if (!_beltPending) can = worldManager != null && worldManager.GetSolidId(cx, cy) == 0;
                                    else
                                    {
                                        if (_hotbarScope == _beltPendingScope && held == _beltPendingHeldRef)
                                        {
                                            if (gearNetworkManager.TryGetGearNodeIdAtCell(_beltStartCell, out int n0))
                                                can = (n0 != n1) &&
                                                      worldManager != null &&
                                                      worldManager.GetSolidId(_beltStartCell.x, _beltStartCell.y) == 0 &&
                                                      worldManager.GetSolidId(cx, cy) == 0;
                                        }
                                    }
                                }
                            }
                        }
    
                        if (_layerMode == LayerMode.Solid)
                            _hlSR.sprite = can ? HighLight_Solid_CAN : HighLight_Solid_CANNOT;
                        else
                            _hlSR.sprite = can ? HighLight_BG_CAN : HighLight_BG_CANNOT;
    
                        PulseHighlight();
                        return;
                    }
                }
            }
    
            ushort solidId = worldManager.GetSolidId(cx, cy);
            ushort bgId = worldManager.GetBGId(cx, cy);
    
            bool hasSolid = solidId != 0;
            bool hasBg = bgId != 0;
    
            if (_layerMode == LayerMode.Solid)
            {
                bool canBreak = hasSolid;
                _hlSR.sprite = canBreak ? HighLight_Solid_CAN : HighLight_Solid;
            }
            else
            {
                bool blocked = hasSolid;
                if (hasBg && blocked) _hlSR.sprite = HighLight_BG_CANNOT;
                else if (hasBg) _hlSR.sprite = HighLight_BG_CAN;
                else _hlSR.sprite = HighLight_BG;
            }
    
            PulseHighlight();
        }
    
        void PulseHighlight()
        {
            _hlGO.SetActive(true);
            _timer += Time.deltaTime;
            float t = (_timer / period) % 1f;
            float sin = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
            float s = Mathf.Lerp(minScale, maxScale, sin);
            _hlGO.transform.localScale = Vector3.one * s;
        }
    
        bool GetMouseCell(out int x, out int y)
        {
            Vector3 wp = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            x = Mathf.FloorToInt(wp.x / cellSize);
            y = Mathf.FloorToInt(wp.y / cellSize);
    
            if (!worldManager.InBounds(x, y))
            {
                x = y = 0;
                return false;
            }
            return true;
        }
    }
}
