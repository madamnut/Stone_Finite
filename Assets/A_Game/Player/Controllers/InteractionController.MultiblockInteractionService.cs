using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        private sealed class MultiblockInteractionService
        {
            readonly InteractionController _owner;

            public MultiblockInteractionService(InteractionController owner)
            {
                _owner = owner;
            }

            public bool TryCellInteraction()
            {
                if (_owner._state != GameState.Ingame) return false;
                if (!_owner.GetMouseCell(out int cx, out int cy)) return false;

                var mb = _owner.multiblockManager.GetAtCell(new Vector2Int(cx, cy));
                if (mb != null)
                {
                    mb.OnInteract(new Vector2Int(cx, cy));
                    return true;
                }

                return false;
            }

            public bool HandleBuildMultiblock(ItemData held, int cx, int cy, Dictionary<string, object> param)
            {
                ushort solidId = _owner.worldManager.GetSolidId(cx, cy);
                if (solidId == 0) return false;

                if (_owner.multiblockManager.GetAtCell(new Vector2Int(cx, cy)) != null)
                    return false;

                string clickedKey = _owner.worldManager.cellLibrary.GetSolidName(solidId);

                if (!MultiblockLibrary.TryGetByIngredient(clickedKey, out var defs) || defs.Count == 0)
                    return false;

                int worldW = _owner.worldManager.settings.width;
                int worldH = _owner.worldManager.settings.height;

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

                                    if (_owner.multiblockManager.GetAtCell(new Vector2Int(wx, wy)) != null)
                                    {
                                        mismatch = true;
                                        break;
                                    }

                                    ushort wid = _owner.worldManager.GetSolidId(wx, wy);
                                    string worldKey = _owner.worldManager.cellLibrary.GetSolidName(wid);

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
                    _owner.multiblockManager.Create(bestDef, bestOx, bestOy);
                    _owner.sound.PlayMultiblockComplete();
                    return true;
                }

                return false;
            }
        }
    }
}
