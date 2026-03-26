


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
            
        }
    
        
        
        public static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, "Worlds", worldName);
        }
    }
}
