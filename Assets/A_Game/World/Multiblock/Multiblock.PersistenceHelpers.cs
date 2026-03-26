using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Core;

namespace Game.World
{
    public abstract partial class Multiblock
    {
        protected ushort[] SnapshotOriginalSolidIds()
        {
            ushort[] orig = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                orig[x + y * Width] = originalSolidIds.TryGetValue(cell, out var id) ? id : (ushort)0;
            }

            return orig;
        }

        protected void RestoreBaseSaveData(SaveData data)
        {
            DefId = data.DefId;
            InstId = data.InstId;
            Origin = data.Origin;
            Width = data.Width;
            Height = data.Height;

            occupiedCells.Clear();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));

            originalSolidIds.Clear();
            if (data.OriginalSolidIds == null || data.OriginalSolidIds.Length != Width * Height)
                return;

            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                originalSolidIds[cell] = data.OriginalSolidIds[x + y * Width];
            }
        }

        protected ItemData UnpackSavedItem(JToken tok)
        {
            if (tok == null || tok.Type == JTokenType.Null) return null;

            string id = tok.Value<string>("id");
            int count = tok.Value<int?>("count") ?? 0;
            int dur = tok.Value<int?>("dur") ?? 0;

            if (string.IsNullOrEmpty(id) || count <= 0) return null;

            ItemData it = null;
            if (Manager != null && Manager.ItemLibrary != null)
                it = Manager.ItemLibrary.Create(id, count);

            if (it != null)
                it.Durability = dur;

            return it;
        }
    }
}
