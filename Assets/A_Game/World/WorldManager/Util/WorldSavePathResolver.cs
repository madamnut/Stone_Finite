


using System.IO;

using Game.Lobby;

namespace Game.World
{
    internal static class WorldSavePathResolver
    {
        
        public static string EnsureDirectory()
        {
            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return dir;
        }

        
        public static string GetPath(string fileName)
        {
            return Path.Combine(WorldLoadContext.GetSavePath(), fileName);
        }
    }
}
