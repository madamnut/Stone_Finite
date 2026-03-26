


using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class WorldChunkSystem
    {
        
        public void ProcessDirtyChunks()
        {
            if (solidDirtyChunks.Count == 0 &&
                platformDirtyChunks.Count == 0 &&
                liquidDirtyChunks.Count == 0 &&
                bgDirtyChunks.Count == 0 &&
                utilityDirtyChunks.Count == 0 &&
                lightDirtyChunks.Count == 0)

                return;

            if (bgDirtyChunks.Count > 0)
            {
                foreach (var coord in bgDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.bgDirty) continue;

                    RefreshChunkLayer(coord, LayerType.BG);
                    c.bgDirty = false;
                }
                bgDirtyChunks.Clear();
            }

            if (utilityDirtyChunks.Count > 0)
            {
                foreach (var coord in utilityDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.utilityDirty) continue;

                    RefreshChunkLayer(coord, LayerType.Utility);
                    c.utilityDirty = false;
                }
                utilityDirtyChunks.Clear();
            }

            if (solidDirtyChunks.Count > 0)
            {
                foreach (var coord in solidDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.solidDirty && !c.platformDirty) continue;

                    RefreshChunkLayer(coord, LayerType.Solid);
                    c.solidDirty = false;
                    c.platformDirty = false;
                }
                solidDirtyChunks.Clear();
                platformDirtyChunks.Clear();
            }

            if (platformDirtyChunks.Count > 0)
            {
                foreach (var coord in platformDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.platformDirty) continue;

                    RefreshChunkLayer(coord, LayerType.Platform);
                    c.platformDirty = false;
                }
                platformDirtyChunks.Clear();
            }

            if (liquidDirtyChunks.Count > 0)
            {
                foreach (var coord in liquidDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.liquidDirty) continue;

                    RefreshChunkLayer(coord, LayerType.Liquid);
                    c.liquidDirty = false;
                }
                liquidDirtyChunks.Clear();
            }

            if (lightDirtyChunks.Count > 0)
            {
                foreach (var coord in lightDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.lightDirty) continue;

                    RefreshLightLayer(coord);
                    c.lightDirty = false;
                }
                lightDirtyChunks.Clear();
            }
        }

        
        public void MarkChunkDirty(int worldX, int worldY, bool markSolid, bool markBG = false, bool markLiquid = false, bool markUtility = false)
        {
            int cx = Mathf.FloorToInt(worldX / (float)chunkSize);
            int cy = Mathf.FloorToInt(worldY / (float)chunkSize);
            var coord = new Vector2Int(cx, cy);

            if (!activeChunks.TryGetValue(coord, out var go)) return;
            var c = go.GetComponent<Chunk>();

            if (markSolid)
            {
                c.solidDirty = true;
                c.platformDirty = true;
                solidDirtyChunks.Add(coord);
                platformDirtyChunks.Add(coord);
            }
            if (markLiquid)
            {
                c.liquidDirty = true;
                liquidDirtyChunks.Add(coord);
            }
            if (markBG)
            {
                c.bgDirty = true;
                bgDirtyChunks.Add(coord);
            }
            if (markUtility)
            {
                c.utilityDirty = true;
                utilityDirtyChunks.Add(coord);
            }
        }

        
        public void MarkLightDirtyCell(int worldX, int worldY)
        {
            int x = Mathf.Clamp(worldX, 0, worldWidth - 1);
            int y = Mathf.Clamp(worldY, 0, worldHeight - 1);

            int cx = x / chunkSize;
            int cy = y / chunkSize;

            var coord = new Vector2Int(cx, cy);
            if (!activeChunks.TryGetValue(coord, out var go)) return;

            var c = go.GetComponent<Chunk>();
            c.lightDirty = true;
            lightDirtyChunks.Add(coord);
        }

        
        public void MarkLightDirtyCells(List<Vector2Int> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                var p = cells[i];
                MarkLightDirtyCell(p.x, p.y);
            }
        }

        
        public void MarkLightDirtyRect(int x, int y, int w, int h)
        {
            int x0 = Mathf.Clamp(x, 0, worldWidth - 1);
            int y0 = Mathf.Clamp(y, 0, worldHeight - 1);
            int x1 = Mathf.Clamp(x + w - 1, 0, worldWidth - 1);
            int y1 = Mathf.Clamp(y + h - 1, 0, worldHeight - 1);

            int cx0 = x0 / chunkSize;
            int cy0 = y0 / chunkSize;
            int cx1 = x1 / chunkSize;
            int cy1 = y1 / chunkSize;

            for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                var coord = new Vector2Int(cx, cy);
                if (!activeChunks.TryGetValue(coord, out var go)) continue;

                var c = go.GetComponent<Chunk>();
                c.lightDirty = true;
                lightDirtyChunks.Add(coord);
            }
        }

        
        public void MarkAllChunksLightDirty()
        {
            foreach (var kv in activeChunks)
            {
                var coord = kv.Key;
                var c = kv.Value.GetComponent<Chunk>();
                c.lightDirty = true;
                lightDirtyChunks.Add(coord);
            }
        }
    }
}
