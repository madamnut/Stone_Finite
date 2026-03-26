using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class LightingService
        {
            readonly WorldServiceContext _ctx;

            public LightingService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public void RecalculateLightAt(int x0, int y0)
            {
                if ((uint)x0 >= (uint)_ctx.Width || (uint)y0 >= (uint)_ctx.Height) return;

                var q = new Queue<(int x, int y)>();
                q.Enqueue((x0, y0));

                (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

                while (q.Count > 0)
                {
                    var (x, y) = q.Dequeue();

                    ushort oldN16 = _ctx.WorldMap.GetNaturalLight(x, y);
                    byte oldN = (byte)Mathf.Clamp((int)oldN16, 0, NAT_MAX);

                    int attenHere = 0;
                    if (_ctx.WorldMap.GetBG(x, y) != 0) attenHere += 1;
                    if (_ctx.IsCollidable(x, y)) attenHere += 2;

                    byte best = 0;
                    foreach (var (dx, dy) in dirs)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((uint)nx >= (uint)_ctx.Width || (uint)ny >= (uint)_ctx.Height) continue;

                        int nNat = (int)_ctx.WorldMap.GetNaturalLight(nx, ny);
                        int cand = nNat - attenHere;
                        if (cand > best) best = (byte)Mathf.Clamp(cand, 0, NAT_MAX);
                    }

                    if (best != oldN)
                    {
                        _ctx.WorldMap.SetNaturalLight(x, y, best);

                        foreach (var (dx, dy) in dirs)
                        {
                            int mx = x + dx, my = y + dy;
                            if ((uint)mx >= (uint)_ctx.Width || (uint)my >= (uint)_ctx.Height) continue;
                            q.Enqueue((mx, my));
                        }

                        _ctx.MarkLightDirtyRect(x - 1, y - 1, 3, 3);
                    }
                }
            }

            public void ProcessArtificialLightQueues()
            {
                if (_ctx.DecreaseQueue.Count == 0 && _ctx.IncreaseQueue.Count == 0) return;
                if (_ctx.ArtificialLightOpsPerTick <= 0) return;

                _ctx.LightChangedSet.Clear();
                _ctx.LightChangedList.Clear();

                int ops = _ctx.ArtificialLightOpsPerTick;

                while (ops > 0 && _ctx.DecreaseQueue.Count > 0)
                {
                    ops--;

                    var n = _ctx.DecreaseQueue.Dequeue();
                    int x = n.x, y = n.y;
                    byte v = n.v;

                    ProcessDecreaseNeighbor(x + 1, y, v);
                    ProcessDecreaseNeighbor(x - 1, y, v);
                    ProcessDecreaseNeighbor(x, y + 1, v);
                    ProcessDecreaseNeighbor(x, y - 1, v);
                }

                if (_ctx.DecreaseQueue.Count == 0 && _ctx.SeedList.Count > 0)
                {
                    for (int i = 0; i < _ctx.SeedList.Count; i++)
                    {
                        var p = _ctx.SeedList[i];
                        if ((uint)p.x >= (uint)_ctx.Width || (uint)p.y >= (uint)_ctx.Height) continue;

                        byte cur = (byte)Mathf.Clamp((int)_ctx.WorldMap.GetArtificialLight(p.x, p.y), 0, ART_MAX);
                        if (cur > 0) EnqueueIncrease(p.x, p.y, cur);
                    }
                    _ctx.SeedSet.Clear();
                    _ctx.SeedList.Clear();
                }

                while (ops > 0 && _ctx.DecreaseQueue.Count == 0 && _ctx.IncreaseQueue.Count > 0)
                {
                    ops--;

                    var n = _ctx.IncreaseQueue.Dequeue();
                    int x = n.x, y = n.y;
                    byte v = n.v;

                    if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) continue;

                    byte curA = (byte)Mathf.Clamp((int)_ctx.WorldMap.GetArtificialLight(x, y), 0, ART_MAX);
                    if (v <= curA) continue;

                    _ctx.WorldMap.SetArtificialLight(x, y, v);
                    RecordLightChanged(x, y);

                    if (v <= 1) continue;

                    ProcessIncreaseNeighbor(x + 1, y, v);
                    ProcessIncreaseNeighbor(x - 1, y, v);
                    ProcessIncreaseNeighbor(x, y + 1, v);
                    ProcessIncreaseNeighbor(x, y - 1, v);
                }

                if (_ctx.LightChangedList.Count > 0)
                    _ctx.MarkLightDirtyCells(_ctx.LightChangedList);
            }

            public void HandleSourceLightChangeAt(int x, int y, ushort oldSolidId, ushort oldSolidMeta, ushort oldFluidId)
            {
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return;

                var newS = _ctx.WorldMap.GetSolid(x, y);
                ushort newSolidId = newS.id;
                ushort newSolidMeta = newS.meta;
                ushort newFluidId = _ctx.WorldMap.GetFluid(x, y).id;

                byte oldB = GetSourceBrightness(oldSolidId, oldSolidMeta, oldFluidId);
                byte newB = GetSourceBrightness(newSolidId, newSolidMeta, newFluidId);

                if (oldB == 0 && newB == 0) return;

                byte oldV = (byte)Mathf.Clamp((int)_ctx.WorldMap.GetArtificialLight(x, y), 0, ART_MAX);

                if (oldB > 0 && oldB >= newB)
                {
                    if (oldV > 0) EnqueueDecrease(x, y, oldV);
                }

                if (newB > 0)
                {
                    EnqueueIncrease(x, y, newB);
                }
            }

            int GetArtCost(int nx, int ny)
            {
                int cost = ATT_AIR;
                if (_ctx.IsCollidable(nx, ny)) cost = ATT_SOLID;
                else if (_ctx.WorldMap.GetBG(nx, ny) != 0) cost = ATT_BG;
                return cost;
            }

            void RecordLightChanged(int x, int y)
            {
                var p = new Vector2Int(x, y);
                if (_ctx.LightChangedSet.Add(p))
                    _ctx.LightChangedList.Add(p);
            }

            void RecordSeed(int x, int y)
            {
                var p = new Vector2Int(x, y);
                if (_ctx.SeedSet.Add(p))
                    _ctx.SeedList.Add(p);
            }

            void EnqueueIncrease(int x, int y, byte v)
            {
                if (v == 0) return;
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return;
                if (v > ART_MAX) v = ART_MAX;
                _ctx.IncreaseQueue.Enqueue(new IncNode(x, y, v));
            }

            void EnqueueDecrease(int x, int y, byte oldV)
            {
                if (oldV == 0) return;
                if ((uint)x >= (uint)_ctx.Width || (uint)y >= (uint)_ctx.Height) return;

                ushort cur = _ctx.WorldMap.GetArtificialLight(x, y);
                if (cur != 0)
                {
                    _ctx.WorldMap.SetArtificialLight(x, y, 0);
                    RecordLightChanged(x, y);
                }

                _ctx.DecreaseQueue.Enqueue(new DecNode(x, y, oldV));
            }

            byte GetSourceBrightness(ushort solidId, ushort solidMeta, ushort fluidId)
            {
                byte sb = _ctx.CellLibrary.GetSolidBrightness(solidId, solidMeta);
                byte lb = _ctx.CellLibrary.GetFluidBrightness(fluidId);
                return (sb >= lb) ? sb : lb;
            }

            void ProcessDecreaseNeighbor(int nx, int ny, byte v)
            {
                if ((uint)nx >= (uint)_ctx.Width || (uint)ny >= (uint)_ctx.Height)
                    return;

                byte cur = (byte)Mathf.Clamp((int)_ctx.WorldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (cur == 0) return;

                if (cur < v)
                {
                    _ctx.WorldMap.SetArtificialLight(nx, ny, 0);
                    RecordLightChanged(nx, ny);
                    _ctx.DecreaseQueue.Enqueue(new DecNode(nx, ny, cur));
                }
                else
                {
                    RecordSeed(nx, ny);
                }
            }

            void ProcessIncreaseNeighbor(int nx, int ny, byte v)
            {
                if ((uint)nx >= (uint)_ctx.Width || (uint)ny >= (uint)_ctx.Height)
                    return;

                int cost = GetArtCost(nx, ny);
                int nv = v - cost;
                byte nCur = (byte)Mathf.Clamp((int)_ctx.WorldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                if (nv > 0 && nv > nCur)
                    _ctx.IncreaseQueue.Enqueue(new IncNode(nx, ny, (byte)nv));
            }
        }
    }
}
