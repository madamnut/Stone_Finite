using UnityEngine;
using UnityEngine.Tilemaps;

namespace Game.World
{
    public partial class WorldChunkSystem
    {
        private enum LayerType { BG, Utility, Solid, Platform, Liquid }

        private void CreateChunk(Vector2Int coord)
        {
            int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
            int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);
            if (coord.x < 0 || coord.y < 0 || coord.x > cxMax || coord.y > cyMax) return;

            var go = GetFromPool();
            go.SetActive(true);
            go.name = $"Chunk_{coord.x}_{coord.y}";
            go.transform.localPosition = new Vector3(coord.x * chunkSize, coord.y * chunkSize, 0f);

            var c = go.GetComponent<Chunk>();

            var bgBuf = c.bgBuffer;
            var utilBuf = c.utilityBuffer;
            var solidBuf = c.solidBuffer;
            var platBuf = c.platformBuffer;
            var liqBuf = c.liquidBuffer;

            int size = chunkSize * chunkSize;
            for (int i = 0; i < size; i++)
            {
                bgBuf[i] = null;
                utilBuf[i] = null;
                solidBuf[i] = null;
                platBuf[i] = null;
                liqBuf[i] = null;
            }

            var bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);

            for (int y = 0; y < chunkSize; y++)
            for (int x = 0; x < chunkSize; x++)
            {
                int wx = coord.x * chunkSize + x;
                int wy = coord.y * chunkSize + y;
                int idx = y * chunkSize + x;

                if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                    continue;

                bgBuf[idx] = cellLibrary.GetBgTile(worldMap.bg[wx, wy]);

                var u = worldMap.utility[wx, wy];
                utilBuf[idx] = (u.id != 0) ? cellLibrary.GetUtilityTile(u.id, u.meta) : null;

                var s = worldMap.solid[wx, wy];
                if (s.id != 0)
                {
                    solidBuf[idx] = cellLibrary.GetSolidTile(s.id, s.meta);
                    platBuf[idx] = cellLibrary.IsPlatform(s.id)
                        ? cellLibrary.GetPlatformColliderTile(s.id, s.meta)
                        : null;
                }
                else
                {
                    solidBuf[idx] = null;
                    platBuf[idx] = null;
                }

                var f = worldMap.fluid[wx, wy];
                liqBuf[idx] = (f.id != 0 && f.amount > 0)
                    ? cellLibrary.GetFluidTile(f.id, f.amount)
                    : null;
            }

            c.bgTilemap.SetTilesBlock(bounds, bgBuf);
            if (c.utilityTilemap != null) c.utilityTilemap.SetTilesBlock(bounds, utilBuf);
            c.solidTilemap.SetTilesBlock(bounds, solidBuf);
            if (c.platformTilemap != null) c.platformTilemap.SetTilesBlock(bounds, platBuf);
            c.liquidTilemap.SetTilesBlock(bounds, liqBuf);

            for (int y = 0; y < chunkSize; y++)
            for (int x = 0; x < chunkSize; x++)
            {
                int wx = coord.x * chunkSize + x;
                int wy = coord.y * chunkSize + y;
                int idx = y * chunkSize + x;

                if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                {
                    c.liquidTypePixels[idx] = new Color32(0, 0, 0, 255);
                    c.liquidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                    continue;
                }

                var f2 = worldMap.fluid[wx, wy];
                byte type = (byte)((f2.id != 0 && f2.amount > 0) ? Mathf.Min((int)f2.id, 255) : 0);
                byte amt = (byte)((f2.id != 0 && f2.amount > 0) ? f2.amount : 0);

                c.liquidTypePixels[idx] = new Color32(type, 0, 0, 255);
                c.liquidAmtPixels[idx] = new Color32(amt, 0, 0, 255);
            }

            c.liquidTypeTex.SetPixels32(c.liquidTypePixels);
            c.liquidTypeTex.Apply(false, false);

            c.liquidAmountTex.SetPixels32(c.liquidAmtPixels);
            c.liquidAmountTex.Apply(false, false);

            Vector3 origin = go.transform.position;

            c.liquidRenderer.GetPropertyBlock(c.liquidMpb);
            c.liquidMpb.SetTexture("_TypeTex", c.liquidTypeTex);
            c.liquidMpb.SetTexture("_AmountTex", c.liquidAmountTex);
            c.liquidMpb.SetVector("_ChunkOriginWS", new Vector4(origin.x, origin.y, 0f, 0f));
            c.liquidRenderer.SetPropertyBlock(c.liquidMpb);

            var coll = c.solidTilemap.GetComponent<TilemapCollider2D>();
            if (coll != null)
            {
                c.solidTilemap.RefreshAllTiles();
                coll.ProcessTilemapChanges();
            }

            if (c.platformTilemap != null)
            {
                var pColl = c.platformTilemap.GetComponent<TilemapCollider2D>();
                if (pColl != null)
                {
                    c.platformTilemap.RefreshAllTiles();
                    pColl.ProcessTilemapChanges();
                }
            }

            activeChunks[coord] = go;

            RefreshLightLayer(coord);

            c.bgDirty = false;
            c.utilityDirty = false;
            c.solidDirty = false;
            c.platformDirty = false;
            c.liquidDirty = false;
            c.lightDirty = false;
        }

        private void RefreshChunkLayer(Vector2Int coord, LayerType type)
        {
            if (!activeChunks.TryGetValue(coord, out var go)) return;

            var c = go.GetComponent<Chunk>();
            var bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);

            switch (type)
            {
                case LayerType.BG:
                {
                    var buf = c.bgBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }
                        buf[idx] = cellLibrary.GetBgTile(worldMap.bg[wx, wy]);
                    }
                    c.bgTilemap.SetTilesBlock(bounds, buf);
                    break;
                }

                case LayerType.Utility:
                {
                    if (c.utilityTilemap == null) break;

                    var buf = c.utilityBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }

                        var u = worldMap.utility[wx, wy];
                        buf[idx] = (u.id != 0) ? cellLibrary.GetUtilityTile(u.id, u.meta) : null;
                    }

                    c.utilityTilemap.SetTilesBlock(bounds, buf);
                    break;
                }

                case LayerType.Solid:
                {
                    var buf = c.solidBuffer;
                    var pbuf = c.platformBuffer;

                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; pbuf[idx] = null; continue; }

                        var s = worldMap.solid[wx, wy];
                        if (s.id == 0)
                        {
                            buf[idx] = null;
                            pbuf[idx] = null;
                            continue;
                        }

                        buf[idx] = cellLibrary.GetSolidTile(s.id, s.meta);
                        pbuf[idx] = cellLibrary.IsPlatform(s.id)
                            ? cellLibrary.GetPlatformColliderTile(s.id, s.meta)
                            : null;
                    }

                    c.solidTilemap.SetTilesBlock(bounds, buf);
                    if (c.platformTilemap != null) c.platformTilemap.SetTilesBlock(bounds, pbuf);

                    var coll2 = c.solidTilemap.GetComponent<TilemapCollider2D>();
                    if (coll2 != null)
                    {
                        c.solidTilemap.RefreshAllTiles();
                        coll2.ProcessTilemapChanges();
                    }

                    if (c.platformTilemap != null)
                    {
                        var pColl2 = c.platformTilemap.GetComponent<TilemapCollider2D>();
                        if (pColl2 != null)
                        {
                            c.platformTilemap.RefreshAllTiles();
                            pColl2.ProcessTilemapChanges();
                        }
                    }

                    break;
                }

                case LayerType.Platform:
                {
                    if (c.platformTilemap == null) break;

                    var pbuf = c.platformBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { pbuf[idx] = null; continue; }

                        var s = worldMap.solid[wx, wy];
                        pbuf[idx] = (s.id != 0 && cellLibrary.IsPlatform(s.id))
                            ? cellLibrary.GetPlatformColliderTile(s.id, s.meta)
                            : null;
                    }

                    c.platformTilemap.SetTilesBlock(bounds, pbuf);

                    var pColl = c.platformTilemap.GetComponent<TilemapCollider2D>();
                    if (pColl != null)
                    {
                        c.platformTilemap.RefreshAllTiles();
                        pColl.ProcessTilemapChanges();
                    }

                    break;
                }

                case LayerType.Liquid:
                {
                    var buf = c.liquidBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }

                        var f = worldMap.fluid[wx, wy];
                        buf[idx] = (f.id != 0 && f.amount > 0)
                            ? cellLibrary.GetFluidTile(f.id, f.amount)
                            : null;
                    }

                    c.liquidTilemap.SetTilesBlock(bounds, buf);

                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;

                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                        {
                            c.liquidTypePixels[idx] = new Color32(0, 0, 0, 255);
                            c.liquidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                            continue;
                        }

                        var f2 = worldMap.fluid[wx, wy];
                        byte type2 = (byte)((f2.id != 0 && f2.amount > 0) ? Mathf.Min((int)f2.id, 255) : 0);
                        byte amt2 = (byte)((f2.id != 0 && f2.amount > 0) ? f2.amount : 0);

                        c.liquidTypePixels[idx] = new Color32(type2, 0, 0, 255);
                        c.liquidAmtPixels[idx] = new Color32(amt2, 0, 0, 255);
                    }

                    c.liquidTypeTex.SetPixels32(c.liquidTypePixels);
                    c.liquidTypeTex.Apply(false, false);

                    c.liquidAmountTex.SetPixels32(c.liquidAmtPixels);
                    c.liquidAmountTex.Apply(false, false);

                    Vector3 origin2 = go.transform.position;

                    c.liquidRenderer.GetPropertyBlock(c.liquidMpb);
                    c.liquidMpb.SetTexture("_TypeTex", c.liquidTypeTex);
                    c.liquidMpb.SetTexture("_AmountTex", c.liquidAmountTex);
                    c.liquidMpb.SetVector("_ChunkOriginWS", new Vector4(origin2.x, origin2.y, 0f, 0f));
                    c.liquidRenderer.SetPropertyBlock(c.liquidMpb);

                    break;
                }
            }
        }

        private void RefreshLightLayer(Vector2Int coord)
        {
            if (!activeChunks.TryGetValue(coord, out var go)) return;
            var c = go.GetComponent<Chunk>();

            if (c.lightOverlayRenderer == null) return;
            if (c.lightTex == null || c.lightPixels == null) return;

            int l = chunkSize + 2;
            if (c.lightPixels.Length != l * l) return;

            int sx = coord.x * chunkSize;
            int sy = coord.y * chunkSize;

            for (int ty = 0; ty < l; ty++)
            {
                for (int tx = 0; tx < l; tx++)
                {
                    int gx = Mathf.Clamp(sx + (tx - 1), 0, worldWidth - 1);
                    int gy = Mathf.Clamp(sy + (ty - 1), 0, worldHeight - 1);

                    ushort nat = worldMap.naturalLight[gx, gy];
                    ushort art = worldMap.artificialLight[gx, gy];

                    int ns = (int)nat - (int)globalBrightnessOffset;
                    if (ns < 0) ns = 0;
                    if (ns > LIGHT_MAX) ns = LIGHT_MAX;

                    int a = (int)art;
                    if (a < 0) a = 0;
                    if (a > LIGHT_MAX) a = LIGHT_MAX;

                    float n01 = ns / 15f;
                    float a01 = a / 15f;
                    float b01 = Mathf.Clamp01(Mathf.Max(n01, a01));

                    float a01Final = 1f - b01;
                    byte ab = (byte)Mathf.RoundToInt(Mathf.Clamp01(a01Final) * 255f);

                    c.lightPixels[ty * l + tx] = new Color32(0, 0, 0, ab);
                }
            }

            c.lightTex.SetPixels32(c.lightPixels);
            c.lightTex.Apply(false, false);

            c.lightOverlayRenderer.GetPropertyBlock(c.lightMpb);
            c.lightMpb.SetTexture("_LightTex", c.lightTex);
            c.lightOverlayRenderer.SetPropertyBlock(c.lightMpb);
        }
    }
}
