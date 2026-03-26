using System.Collections.Generic;

using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        public void SaveWorld()
        {
            _persistenceService.SaveWorld();
        }

        bool LoadWorldFromDisk(out WorldData loaded, out List<Multiblock.SaveData> multiblocks)
        {
            return _persistenceService.LoadWorldFromDisk(out loaded, out multiblocks);
        }

        private void LoadPlayerData()
        {
            _persistenceService.LoadPlayerData();
        }

        private void LoadEntities()
        {
            _persistenceService.LoadEntities();
        }

        private void ApplyLoadedPlayerAndInventory()
        {
            _persistenceService.ApplyLoadedPlayerAndInventory();
        }
    }
}
