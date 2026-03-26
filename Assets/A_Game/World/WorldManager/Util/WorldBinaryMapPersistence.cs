using System.Collections.Generic;
using System.IO;
using UnityEngine;

using Game.Core;

namespace Game.World
{
    internal static class WorldBinaryMapPersistence
    {
        const string SaveFile = "world.bin";

        public static void SaveWorld(
            int width,
            int height,
            WorldData worldMap,
            long worldTick,
            HashSet<Vector2Int> tickCurr,
            HashSet<Vector2Int> tickNext,
            MultiblockManager multiblockManager)
        {
            try
            {
                string dir = WorldSavePathResolver.EnsureDirectory();
                string path = Path.Combine(dir, SaveFile);
                string tmp = path + ".tmp";

                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(width);
                    bw.Write(height);
                    bw.Write(worldTick);

                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        bw.Write(worldMap.bg[x, y]);

                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var u = worldMap.utility[x, y];
                        bw.Write(u.id);
                        bw.Write(u.meta);
                    }

                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var s = worldMap.solid[x, y];
                        bw.Write(s.id);
                        bw.Write(s.meta);
                    }

                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var f = worldMap.fluid[x, y];
                        bw.Write(f.id);
                        bw.Write(f.amount);
                    }

                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        bw.Write(worldMap.naturalLight[x, y]);
                        bw.Write(worldMap.artificialLight[x, y]);
                    }

                    bw.Write(tickCurr.Count);
                    foreach (var p in tickCurr)
                    {
                        bw.Write(p.x);
                        bw.Write(p.y);
                    }

                    bw.Write(tickNext.Count);
                    foreach (var p in tickNext)
                    {
                        bw.Write(p.x);
                        bw.Write(p.y);
                    }

                    int mbCount = multiblockManager != null && multiblockManager.Instances != null
                        ? multiblockManager.Instances.Count
                        : 0;

                    bw.Write(mbCount);

                    if (mbCount > 0)
                    {
                        foreach (var kv in multiblockManager.Instances)
                        {
                            var mb = kv.Value;
                            if (mb == null)
                                throw new System.Exception("[SAVE] Multiblock instance is null in Instances.");

                            Multiblock.SaveData sd = mb.ToSaveData();

                            bw.Write(sd.DefId ?? "");
                            bw.Write(sd.InstId);
                            bw.Write(sd.Origin.x);
                            bw.Write(sd.Origin.y);
                            bw.Write(sd.Width);
                            bw.Write(sd.Height);
                            bw.Write(sd.PayloadJson ?? "");

                            ushort[] orig = sd.OriginalSolidIds;
                            int origLen = orig != null ? orig.Length : 0;
                            bw.Write(origLen);
                            if (origLen > 0)
                            {
                                for (int i = 0; i < origLen; i++)
                                    bw.Write(orig[i]);
                            }
                        }
                    }
                }

                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveWorld failed: {e}");
            }
        }

        public static bool LoadWorldFromDisk(
            out WorldData loaded,
            out int width,
            out int height,
            out long loadedTick,
            HashSet<Vector2Int> tickCurr,
            HashSet<Vector2Int> tickNext,
            out List<Multiblock.SaveData> multiblocks)
        {
            loaded = default;
            width = height = 0;
            loadedTick = 0;
            multiblocks = null;

            string path = WorldSavePathResolver.GetPath(SaveFile);
            if (!File.Exists(path))
            {
                Debug.Log("[LOAD] world.bin not found");
                return false;
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                width = br.ReadInt32();
                height = br.ReadInt32();
                loadedTick = br.ReadInt64();

                var data = new WorldData(width, height);

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    data.bg[x, y] = br.ReadUInt16();

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    ref var u = ref data.utility[x, y];
                    u.id = br.ReadUInt16();
                    u.meta = br.ReadUInt16();
                }

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    ref var s = ref data.solid[x, y];
                    s.id = br.ReadUInt16();
                    s.meta = br.ReadUInt16();
                }

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    ref var f = ref data.fluid[x, y];
                    f.id = br.ReadUInt16();
                    f.amount = br.ReadByte();
                }

                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    data.naturalLight[x, y] = br.ReadUInt16();
                    data.artificialLight[x, y] = br.ReadUInt16();
                }

                tickCurr.Clear();
                tickNext.Clear();

                int cCount = br.ReadInt32();
                for (int i = 0; i < cCount; i++)
                {
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    if ((uint)x < (uint)width && (uint)y < (uint)height)
                        tickCurr.Add(new Vector2Int(x, y));
                }

                int nCount = br.ReadInt32();
                for (int i = 0; i < nCount; i++)
                {
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    if ((uint)x < (uint)width && (uint)y < (uint)height)
                        tickNext.Add(new Vector2Int(x, y));
                }

                int mbCount = br.ReadInt32();
                if (mbCount < 0) mbCount = 0;
                multiblocks = new List<Multiblock.SaveData>(mbCount);

                for (int i = 0; i < mbCount; i++)
                {
                    var sd = new Multiblock.SaveData
                    {
                        DefId = br.ReadString(),
                        InstId = br.ReadInt32(),
                        Origin = new Vector2Int(br.ReadInt32(), br.ReadInt32()),
                        Width = br.ReadInt32(),
                        Height = br.ReadInt32(),
                        PayloadJson = br.ReadString()
                    };

                    int origLen = br.ReadInt32();
                    if (origLen > 0)
                    {
                        sd.OriginalSolidIds = new ushort[origLen];
                        for (int j = 0; j < origLen; j++)
                            sd.OriginalSolidIds[j] = br.ReadUInt16();
                    }
                    else
                    {
                        sd.OriginalSolidIds = null;
                    }

                    multiblocks.Add(sd);
                }

                loaded = data;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LoadWorldFromDisk failed: {e}");
                loaded = null;
                width = height = 0;
                loadedTick = 0;
                multiblocks = null;
                return false;
            }
        }
    }
}
