


using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.World
{
    internal static class EntityPersistence
    {

        const string EntitySaveFile = "entities.bin";

        
        public static void SaveEntities(EntityManager em)
        {
            try
            {
                string dir = WorldSavePathResolver.EnsureDirectory();
                string path = Path.Combine(dir, EntitySaveFile);
                string tmp = path + ".tmp";

                var src = em.Entities;
                var list = new List<Entity>(src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    if (src[i] != null)
                        list.Add(src[i]);
                }

                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(list.Count);

                    foreach (var e in list)
                    {
                        EntitySaveData data = e.ToSaveData();
                        if (data == null) continue;

                        bw.Write((byte)data.Kind);
                        bw.Write(data.Position.x);
                        bw.Write(data.Position.y);

                        string payload = data.PayloadJson ?? "";
                        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(payload);
                        bw.Write(bytes.Length);
                        bw.Write(bytes);
                    }
                }

                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);

                Debug.Log($"[SAVE-ENTITY] saved count={list.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveEntities failed: {e}");
            }
        }
    }
}
