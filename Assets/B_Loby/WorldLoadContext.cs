// WorldLoadContext.cs
using System.IO;
using UnityEngine;

public static class WorldLoadContext
{
    public enum LoadType { NewWorld, LoadWorld }

    public static LoadType loadType  { get; private set; }
    public static string   worldName { get; private set; }
    public static int      seed      { get; private set; }

    // LobyManager에서 쓰는 메타와 동일한 구조
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

        // 기본값 0
        seed = 0;

        try
        {
            string dir     = GetSavePath();
            string metaPath = Path.Combine(dir, "world_meta.json");

            if (!File.Exists(metaPath))
            {
                Debug.LogWarning($"[WorldLoadContext] world_meta.json 없음: {metaPath}");
                return;
            }

            string json = File.ReadAllText(metaPath);
            var meta    = JsonUtility.FromJson<WorldMetaData>(json);
            if (meta == null)
            {
                Debug.LogWarning($"[WorldLoadContext] world_meta.json 파싱 실패: {metaPath}");
                return;
            }

            seed = meta.seed;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[WorldLoadContext] SetLoadWorld 실패: {ex.Message}");
        }
    }

    public static void Clear()
    {
        worldName = null;
        seed      = 0;
        // loadType은 굳이 초기화 필요 없으면 그대로 둠
    }

    // 저장 경로: 퍼시스턴트 아래 Worlds/<name>/
    public static string GetSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "Worlds", worldName);
    }
}
