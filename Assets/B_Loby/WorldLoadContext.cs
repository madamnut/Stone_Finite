// WorldLoadContext.cs
using System.IO;
using UnityEngine;

namespace Game.Lobby
{
    
    public static class WorldLoadContext
    {
        public enum LoadType { NewWorld, LoadWorld }
    
        public static LoadType loadType  { get; private set; }
        public static string   worldName { get; private set; }
        public static int      seed      { get; private set; }
    
        // LobyManager????????ㅻ쿋獒?癲ル슢????? ????곕럡??????깼??
        [System.Serializable]
        private class WorldMetaData
        {
            public string worldName;
            public int    seed;
            public string lastPlayed;
        }
    
        public static void SetNewWorld(string name, int seedValue)
        {
            loadType  = LoadType.NewWorld;
            worldName = name;
            seed      = seedValue;
        }
    
        public static void SetLoadWorld(string name)
        {
            loadType  = LoadType.LoadWorld;
            worldName = name;
    
            // ??れ삀???筌?0
            seed = 0;
    
            try
            {
                string dir     = GetSavePath();
                string metaPath = Path.Combine(dir, "world_meta.json");
    
                if (!File.Exists(metaPath))
                {
                    Debug.LogWarning($"[WorldLoadContext] world_meta.json not found: {metaPath}");
                    return;
                }
    
                string json = File.ReadAllText(metaPath);
                var meta    = JsonUtility.FromJson<WorldMetaData>(json);
                if (meta == null)
                {
                    Debug.LogWarning($"[WorldLoadContext] Failed to parse world_meta.json: {metaPath}");
                    return;
                }
    
                seed = meta.seed;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WorldLoadContext] SetLoadWorld failed: {ex.Message}");
            }
        }
    
        public static void Clear()
        {
            worldName = null;
            seed      = 0;
            // loadType?? ???????縕?猿녿뎨????ш끽維?????⑤챶?뺧┼???숆강筌?????
        }
    
        // ?????濡ろ뜑?灌鍮? ??繹먮굝六???袁⑸룈????ш끽維??Worlds/<name>/
        public static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, "Worlds", worldName);
        }
    }
}
