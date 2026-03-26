


using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class MultiblockManager
    {
        private sealed class MultiblockPersistenceService
        {

            readonly MultiblockServiceContext _ctx;

            
            public MultiblockPersistenceService(MultiblockServiceContext context)
            {
                _ctx = context;
            }

            
            public Multiblock Create(MultiblockLibrary.Def def, int originX, int originY)
            {
                int width = def.width;
                int height = def.height;
                var origin = new Vector2Int(originX, originY);

                var occupied = new List<Vector2Int>(width * height);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    occupied.Add(new Vector2Int(originX + x, originY + y));

                if (!_ctx.FactoryByDefId.TryGetValue(def.key, out var creator) || creator == null)
                {
                    Debug.LogWarning($"{LOG_MB} No factory for defId='{def.key}'. Create skipped.");
                    return null;
                }

                var inst = creator.Invoke();
                inst.Initialize(_ctx.World, def.key, origin, width, height, occupied);
                inst.Manager = _ctx.Manager;
                inst.InstId = _ctx.NextInstanceId++;

                inst.originalSolidIds.Clear();
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int wx = originX + x;
                    int wy = originY + y;
                    
                    inst.originalSolidIds[new Vector2Int(wx, wy)] = _ctx.World.GetSolidId(wx, wy);
                }

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    string resultCellName = def.result[x, y];
                    if (string.IsNullOrEmpty(resultCellName))
                        continue;

                    _ctx.World.cellLibrary.TryGetSolidIdByName(resultCellName, out ushort placeId);
                    _ctx.World.OverwriteSolid(originX + x, originY + y, placeId);
                }

                RegisterInstance(inst);
                return inst;
            }

            
            public void RegisterInstance(Multiblock inst)
            {
                _ctx.Instances.Add(inst.InstId, inst);
                foreach (var cell in inst.OccupiedCells)
                    _ctx.ByCell[cell] = inst;
            }

            
            public void Despawn(Multiblock inst, Vector2Int brokenCell)
            {
                _ctx.Instances.Remove(inst.InstId);

                foreach (var cell in inst.OccupiedCells)
                {
                    if (_ctx.ByCell.TryGetValue(cell, out var cur) && cur == inst)
                        _ctx.ByCell.Remove(cell);
                }

                if (_ctx.Vfx != null)
                    _ctx.Vfx.DespawnAllForOwner(inst.InstId);

                foreach (var kv in inst.originalSolidIds)
                {
                    var cell = kv.Key;
                    if (cell == brokenCell) continue;
                    _ctx.World.OverwriteSolid(cell.x, cell.y, kv.Value);
                }

                Debug.Log($"{LOG_MB} Despawn multiblock instId={inst.InstId}, def={inst.DefId}, restoreExcept={brokenCell}");
            }

            
            public void LoadFromSaveDatas(List<Multiblock.SaveData> list)
            {
                if (_ctx.Vfx != null && _ctx.Instances.Count > 0)
                {
                    foreach (var kv in _ctx.Instances)
                        _ctx.Vfx.DespawnAllForOwner(kv.Key);
                }

                _ctx.Instances.Clear();
                _ctx.ByCell.Clear();

                int maxId = 0;

                if (list == null || list.Count == 0)
                {
                    _ctx.NextInstanceId = 1;
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var sd = list[i];
                    if (string.IsNullOrEmpty(sd.DefId))
                        continue;

                    if (!_ctx.FactoryByDefId.TryGetValue(sd.DefId, out var creator) || creator == null)
                    {
                        Debug.LogWarning($"{LOG_MB} No factory for defId='{sd.DefId}'. Load skipped.");
                        continue;
                    }

                    var occupied = new List<Vector2Int>(sd.Width * sd.Height);
                    for (int y = 0; y < sd.Height; y++)
                    for (int x = 0; x < sd.Width; x++)
                        occupied.Add(new Vector2Int(sd.Origin.x + x, sd.Origin.y + y));

                    var inst = creator.Invoke();
                    inst.Initialize(_ctx.World, sd.DefId, sd.Origin, sd.Width, sd.Height, occupied);
                    inst.Manager = _ctx.Manager;
                    inst.InstId = sd.InstId;
                    inst.FromSaveData(sd);

                    RegisterInstance(inst);
                    if (inst.InstId > maxId)
                        maxId = inst.InstId;
                }

                _ctx.NextInstanceId = (maxId <= 0) ? 1 : (maxId + 1);
            }
        }
    }
}
