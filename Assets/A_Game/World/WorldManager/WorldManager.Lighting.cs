using System.Collections.Generic;
using UnityEngine;

using Game.Data;

namespace Game.World
{
    public partial class WorldManager
    {
        public void RecalculateLightAt(int x0, int y0)
        {
            if ((uint)x0 >= (uint)W || (uint)y0 >= (uint)H) return;
    
            var q = new Queue<(int x, int y)>();
            q.Enqueue((x0, y0));
    
            (int dx, int dy)[] dirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };
    
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
    
                ushort oldN16 = worldMap.GetNaturalLight(x, y);
                byte oldN = (byte)Mathf.Clamp((int)oldN16, 0, NAT_MAX);
    
                int attenHere = 0;
                if (worldMap.GetBG(x, y) != 0) attenHere += 1;
                if (IsCollidable(x, y)) attenHere += 2;
    
                byte best = 0;
                foreach (var (dx, dy) in dirs)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((uint)nx >= (uint)W || (uint)ny >= (uint)H) continue;
    
                    int nNat = (int)worldMap.GetNaturalLight(nx, ny);
                    int cand = nNat - attenHere;
                    if (cand > best) best = (byte)Mathf.Clamp(cand, 0, NAT_MAX);
                }
    
                if (best != oldN)
                {
                    worldMap.SetNaturalLight(x, y, best);
    
                    foreach (var (dx, dy) in dirs)
                    {
                        int mx = x + dx, my = y + dy;
                        if ((uint)mx >= (uint)W || (uint)my >= (uint)H) continue;
                        q.Enqueue((mx, my));
                    }
    
                    MarkLightDirtyRect(x - 1, y - 1, 3, 3);
                }
            }
        }
    
        public void MarkChunkDirty(int worldX, int worldY, bool markSolid, bool markBG = false, bool markLiquid = false, bool markUtility = false)
        {
            chunkSystem.MarkChunkDirty(worldX, worldY, markSolid, markBG, markLiquid, markUtility);
        }
    
        public void MarkLightDirtyCell(int x, int y)
        {
            chunkSystem.MarkLightDirtyCell(x, y);
        }
    
        public void MarkLightDirtyCells(List<Vector2Int> cells)
        {
            chunkSystem.MarkLightDirtyCells(cells);
        }
    
        private void MarkLightDirtyRect(int x, int y, int w, int h)
        {
            chunkSystem.MarkLightDirtyRect(x, y, w, h);
        }
    
        private int GetArtCost(int nx, int ny)
        {
            int cost = ATT_AIR;
            if (IsCollidable(nx, ny)) cost = ATT_SOLID;
            else if (worldMap.GetBG(nx, ny) != 0) cost = ATT_BG;
            return cost;
        }
    
        private void RecordLightChanged(int x, int y)
        {
            var p = new Vector2Int(x, y);
            if (_lightChangedSet.Add(p))
                _lightChangedList.Add(p);
        }
    
        private void RecordSeed(int x, int y)
        {
            var p = new Vector2Int(x, y);
            if (_seedSet.Add(p))
                _seedList.Add(p);
        }
    
        private void EnqueueIncrease(int x, int y, byte v)
        {
            if (v == 0) return;
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
            if (v > ART_MAX) v = ART_MAX;
            _incQ.Enqueue(new IncNode(x, y, v));
        }
    
        private void EnqueueDecrease(int x, int y, byte oldV)
        {
            if (oldV == 0) return;
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
    
            ushort cur = worldMap.GetArtificialLight(x, y);
            if (cur != 0)
            {
                worldMap.SetArtificialLight(x, y, 0);
                RecordLightChanged(x, y);
            }
    
            _decQ.Enqueue(new DecNode(x, y, oldV));
        }
    
        private void ProcessArtificialLightQueues()
        {
            if (_decQ.Count == 0 && _incQ.Count == 0) return;
            if (artificialLightOpsPerTick <= 0) return;
    
            _lightChangedSet.Clear();
            _lightChangedList.Clear();
    
            int ops = artificialLightOpsPerTick;
    
            while (ops > 0 && _decQ.Count > 0)
            {
                ops--;
    
                var n = _decQ.Dequeue();
                int x = n.x, y = n.y;
                byte v = n.v;
    
                int nx, ny;
                byte cur;
    
                nx = x + 1; ny = y;
                if ((uint)nx < (uint)W)
                {
                    cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (cur != 0)
                    {
                        if (cur < v)
                        {
                            worldMap.SetArtificialLight(nx, ny, 0);
                            RecordLightChanged(nx, ny);
                            _decQ.Enqueue(new DecNode(nx, ny, cur));
                        }
                        else RecordSeed(nx, ny);
                    }
                }
    
                nx = x - 1; ny = y;
                if (nx >= 0)
                {
                    cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (cur != 0)
                    {
                        if (cur < v)
                        {
                            worldMap.SetArtificialLight(nx, ny, 0);
                            RecordLightChanged(nx, ny);
                            _decQ.Enqueue(new DecNode(nx, ny, cur));
                        }
                        else RecordSeed(nx, ny);
                    }
                }
    
                nx = x; ny = y + 1;
                if ((uint)ny < (uint)H)
                {
                    cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (cur != 0)
                    {
                        if (cur < v)
                        {
                            worldMap.SetArtificialLight(nx, ny, 0);
                            RecordLightChanged(nx, ny);
                            _decQ.Enqueue(new DecNode(nx, ny, cur));
                        }
                        else RecordSeed(nx, ny);
                    }
                }
    
                nx = x; ny = y - 1;
                if (ny >= 0)
                {
                    cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (cur != 0)
                    {
                        if (cur < v)
                        {
                            worldMap.SetArtificialLight(nx, ny, 0);
                            RecordLightChanged(nx, ny);
                            _decQ.Enqueue(new DecNode(nx, ny, cur));
                        }
                        else RecordSeed(nx, ny);
                    }
                }
            }
    
            if (_decQ.Count == 0 && _seedList.Count > 0)
            {
                for (int i = 0; i < _seedList.Count; i++)
                {
                    var p = _seedList[i];
                    if ((uint)p.x >= (uint)W || (uint)p.y >= (uint)H) continue;
    
                    byte cur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(p.x, p.y), 0, ART_MAX);
                    if (cur > 0) EnqueueIncrease(p.x, p.y, cur);
                }
                _seedSet.Clear();
                _seedList.Clear();
            }
    
            while (ops > 0 && _decQ.Count == 0 && _incQ.Count > 0)
            {
                ops--;
    
                var n = _incQ.Dequeue();
                int x = n.x, y = n.y;
                byte v = n.v;
    
                if ((uint)x >= (uint)W || (uint)y >= (uint)H) continue;
    
                byte curA = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(x, y), 0, ART_MAX);
                if (v <= curA) continue;
    
                worldMap.SetArtificialLight(x, y, v);
                RecordLightChanged(x, y);
    
                if (v <= 1) continue;
    
                int nx, ny;
                int cost;
                int nv;
    
                nx = x + 1; ny = y;
                if ((uint)nx < (uint)W)
                {
                    cost = GetArtCost(nx, ny);
                    nv = v - cost;
                    byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (nv > 0 && nv > nCur)
                        _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
                }
    
                nx = x - 1; ny = y;
                if (nx >= 0)
                {
                    cost = GetArtCost(nx, ny);
                    nv = v - cost;
                    byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (nv > 0 && nv > nCur)
                        _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
                }
    
                nx = x; ny = y + 1;
                if ((uint)ny < (uint)H)
                {
                    cost = GetArtCost(nx, ny);
                    nv = v - cost;
                    byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (nv > 0 && nv > nCur)
                        _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
                }
    
                nx = x; ny = y - 1;
                if (ny >= 0)
                {
                    cost = GetArtCost(nx, ny);
                    nv = v - cost;
                    byte nCur = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(nx, ny), 0, ART_MAX);
                    if (nv > 0 && nv > nCur)
                        _incQ.Enqueue(new IncNode(nx, ny, (byte)nv));
                }
            }
    
            if (_lightChangedList.Count > 0)
                MarkLightDirtyCells(_lightChangedList);
        }
    
        private byte GetSourceBrightness(ushort solidId, ushort solidMeta, ushort fluidId)
        {
            byte sb = cellLibrary.GetSolidBrightness(solidId, solidMeta);
            byte lb = cellLibrary.GetFluidBrightness(fluidId);
            return (sb >= lb) ? sb : lb;
        }
    
        private void HandleSourceLightChangeAt(int x, int y, ushort oldSolidId, ushort oldSolidMeta, ushort oldFluidId)
        {
            if ((uint)x >= (uint)W || (uint)y >= (uint)H) return;
    
            var newS = worldMap.GetSolid(x, y);
            ushort newSolidId = newS.id;
            ushort newSolidMeta = newS.meta;
            ushort newFluidId = worldMap.GetFluid(x, y).id;
    
            byte oldB = GetSourceBrightness(oldSolidId, oldSolidMeta, oldFluidId);
            byte newB = GetSourceBrightness(newSolidId, newSolidMeta, newFluidId);
    
            if (oldB == 0 && newB == 0) return;
    
            byte oldV = (byte)Mathf.Clamp((int)worldMap.GetArtificialLight(x, y), 0, ART_MAX);
    
            if (oldB > 0 && oldB >= newB)
            {
                if (oldV > 0) EnqueueDecrease(x, y, oldV);
            }
    
            if (newB > 0)
            {
                EnqueueIncrease(x, y, newB);
            }
        }
    }
}
