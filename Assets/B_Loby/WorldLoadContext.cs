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
    
        // LobyManager?먯꽌 ?곕뒗 硫뷀?? ?숈씪??援ъ“
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
    
            // 湲곕낯媛?0
            seed = 0;
    
            try
            {
                string dir     = GetSavePath();
                string metaPath = Path.Combine(dir, "world_meta.json");
    
                if (!File.Exists(metaPath))
                {
                    Debug.LogWarning($"[WorldLoadContext] world_meta.json ?놁쓬: {metaPath}");
                    return;
                }
    
                string json = File.ReadAllText(metaPath);
                var meta    = JsonUtility.FromJson<WorldMetaData>(json);
                if (meta == null)
                {
                    Debug.LogWarning($"[WorldLoadContext] world_meta.json ?뚯떛 ?ㅽ뙣: {metaPath}");
                    return;
                }
    
                seed = meta.seed;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WorldLoadContext] SetLoadWorld ?ㅽ뙣: {ex.Message}");
            }
        }
    
        public static void Clear()
        {
            worldName = null;
            seed      = 0;
            // loadType? 援녹씠 珥덇린???꾩슂 ?놁쑝硫?洹몃?濡???
        }
    
        // ???寃쎈줈: ?쇱떆?ㅽ꽩???꾨옒 Worlds/<name>/
        public static string GetSavePath()
        {
            return Path.Combine(Application.persistentDataPath, "Worlds", worldName);
        }
    }
}
