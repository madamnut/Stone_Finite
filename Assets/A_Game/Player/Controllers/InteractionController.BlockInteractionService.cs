using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        private sealed class BlockInteractionService
        {
            readonly InteractionController _owner;

            public BlockInteractionService(InteractionController owner)
            {
                _owner = owner;
            }

            public void BreakAtCursor()
            {
                if (!_owner.GetMouseCell(out int cx, out int cy)) return;

                ushort solidId = _owner.worldManager.GetSolidId(cx, cy);
                ushort bgId = _owner.worldManager.GetBGId(cx, cy);

                bool hasSolid = solidId != 0;
                bool hasBg = bgId != 0;

                if (_owner._layerMode == LayerMode.Solid)
                {
                    if (!hasSolid) return;

                    Multiblock mb = _owner.multiblockManager.GetAtCell(new Vector2Int(cx, cy));

                    _owner.worldManager.BreakSolid(cx, cy);
                    _owner.sound.PlayDig();

                    if (mb != null)
                        mb.OnCellBroken(new Vector2Int(cx, cy));
                }
                else
                {
                    if (!hasBg) return;
                    if (hasSolid) return;
                    _owner.worldManager.BreakBG(cx, cy);
                    _owner.sound.PlayDig();
                }
            }

            public bool HandlePlace(ItemData held, int cx, int cy, Dictionary<string, object> placeParam)
            {
                string layerStr = placeParam.TryGetValue("layer", out var layerObj) ? layerObj?.ToString() : null;
                string cellName = placeParam.TryGetValue("cell", out var cellObj) ? cellObj?.ToString() : null;

                ushort solidId = _owner.worldManager.GetSolidId(cx, cy);
                ushort bgId = _owner.worldManager.GetBGId(cx, cy);

                bool hasSolid = solidId != 0;
                bool hasBg = bgId != 0;

                WorldManager.CellLayer targetLayer;

                if (string.Equals(layerStr, "Dynamic", StringComparison.OrdinalIgnoreCase))
                {
                    targetLayer = (_owner._layerMode == LayerMode.BG)
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

                _owner.worldManager.cellLibrary.TryGetSolidIdByName(cellName, out ushort placeId);
                if (placeId == 0) return false;

                ComputeRelativeDirs(cx, cy, out var relV, out var relH);

                bool placed =
                    (targetLayer == WorldManager.CellLayer.Solid)
                        ? _owner.worldManager.PlaceSolid(cx, cy, placeId, relV, relH)
                        : _owner.worldManager.PlaceBG(cx, cy, placeId, relV, relH);

                if (!placed) return false;

                _owner.sound.PlayPlace();
                _owner._heldItemService.Consume(held, 1);
                return true;
            }

            void ComputeRelativeDirs(int cx, int cy, out WorldManager.RelV relV, out WorldManager.RelH relH)
            {
                float half = _owner.cellSize * 0.5f;
                float cellCenterX = cx * _owner.cellSize + half;
                float cellCenterY = cy * _owner.cellSize + half;

                Vector3 p = _owner.player.transform.position;

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
        }
    }
}
