public static class WorldLoadContext
{
    public enum LoadType { NewWorld, LoadWorld }

    public static LoadType loadType { get; private set; }
    public static string worldName { get; private set; }
    public static int seed { get; private set; }

    public static void SetNewWorld(string name, int seedValue)
    {
        loadType = LoadType.NewWorld;
        worldName = name;
        seed = seedValue;
    }

    public static void SetLoadWorld(string name)
    {
        loadType = LoadType.LoadWorld;
        worldName = name;
        seed = 0;
    }

    public static void Clear()
    {
        worldName = null;
        seed = 0;
    }

    public static string GetSavePath() => $"Saves/{worldName}/";
}
