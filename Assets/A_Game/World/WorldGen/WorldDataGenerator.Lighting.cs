using System;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public static partial class WorldDataGenerator
    {
        private static void PropagateNaturalLight(WorldData world, CellLibrary cellLibrary)
        {
            int w = world.bg.GetLength(0);
            int h = world.bg.GetLength(1);
    
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                world.SetNaturalLight(x, y, 0);
                world.SetArtificialLight(x, y, 0);
            }
    
            byte[,] atten = new byte[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                byte a = 0;
    
                if (world.GetBG(x, y) != ID_AIR) a += 1;
    
                ushort sid = world.GetSolid(x, y).id;
                if (sid != 0)
                {
                    var flags = cellLibrary.GetSolidFlags(sid);
                    if ((flags & CellLibrary.SolidFlags.Collidable) != 0)
                        a += 2;
                }
    
                atten[x, y] = a;
            }
    
            const byte INF = 255;
            byte[,] dist = new byte[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                dist[x, y] = INF;
    
            var buckets = new List<(int x, int y)>[NATURAL_MAX + 1];
            for (int i = 0; i <= NATURAL_MAX; i++)
                buckets[i] = new List<(int x, int y)>();
    
            int yTop = h - 1;
            for (int x = 0; x < w; x++)
            {
                dist[x, yTop] = 0;
                buckets[0].Add((x, yTop));
            }
    
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
    
            for (byte d = 0; d <= NATURAL_MAX; d++)
            {
                var bucket = buckets[d];
                for (int idx = 0; idx < bucket.Count; idx++)
                {
                    var (cx, cy) = bucket[idx];
                    if (dist[cx, cy] != d) continue;
    
                    for (int dir = 0; dir < 4; dir++)
                    {
                        int nx = cx + dx[dir];
                        int ny = cy + dy[dir];
                        if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) continue;
    
                        byte a = atten[nx, ny];
                        int ndInt = d + a;
                        if (ndInt > NATURAL_MAX) continue;
    
                        byte nd = (byte)ndInt;
                        if (nd >= dist[nx, ny]) continue;
    
                        dist[nx, ny] = nd;
                        buckets[nd].Add((nx, ny));
                    }
                }
    
                bucket.Clear();
            }
    
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                byte d = dist[x, y];
                if (d <= NATURAL_MAX)
                {
                    ushort val = (ushort)(NATURAL_MAX - d);
                    world.SetNaturalLight(x, y, val);
                }
            }
        }
    }
}
