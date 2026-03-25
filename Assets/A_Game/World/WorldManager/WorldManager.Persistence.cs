using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        public void SaveWorld()
        {
            WorldSaveSystem.SaveWorld(
                W,
                H,
                worldMap,
                worldTick,
                tickCurr,
                tickNext,
                _playerInventory,
                player,
                entityManager,
                multiblockManager
            );
        }
    
        bool LoadWorldFromDisk(out WorldData loaded, out List<Multiblock.SaveData> multiblocks)
        {
            int w, h;
            long loadedTick;
    
            bool ok = WorldSaveSystem.LoadWorldFromDisk(
                out loaded,
                out w,
                out h,
                out loadedTick,
                tickCurr,
                tickNext,
                out multiblocks
            );
            if (ok)
            {
                W = w;
                H = h;
                worldTick = loadedTick;
            }
            else
            {
                multiblocks = null;
            }
    
            return ok;
        }
    
        private void LoadPlayerData()
        {
            _hasLoadedPlayerData = WorldSaveSystem.LoadPlayerData(
                itemLibrary,
                out _loadedPlayerPos,
                out _loadedInventory
            );
        }
    
        private void LoadEntities()
        {
            GameObject dropPrefab = itemDropper.droppedItemPrefab;
    
            WorldSaveSystem.LoadEntities(
                entityManager,
                itemLibrary,
                fallingBlockPrefab,
                dropPrefab,
                mobLibrary,
                corpseLibrary
            );
        }
    
        private void ApplyLoadedPlayerAndInventory()
        {
            if (!_hasLoadedPlayerData) return;
    
            var pos = player.position;
            player.position = new Vector3(_loadedPlayerPos.x, _loadedPlayerPos.y, pos.z);
    
            if (_playerInventory == null) return;

            var slots = _playerInventory.items;
            int n = Mathf.Min(slots.Count, _loadedInventory.Count);
    
            for (int i = 0; i < n; i++)
            {
                var data = _loadedInventory[i];
                slots[i] = (data != null && data.Count > 0) ? data : null;
            }
    
            for (int i = n; i < slots.Count; i++)
                slots[i] = null;
    
            _playerInventory.NotifyChanged();
        }
    }
}
