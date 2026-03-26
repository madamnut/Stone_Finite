


using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.Data;

namespace Game.World
{
    public static class WorldSaveSystem
    {

        const string EntitySaveFile = "entities.bin";

        
        public static void SaveWorld(
            int width,
            int height,
            WorldData worldMap,
            long worldTick,
            HashSet<Vector2Int> tickCurr,
            HashSet<Vector2Int> tickNext,
            InventoryData playerInventory,
            Transform player,
            EntityManager entityManager,
            MultiblockManager multiblockManager)
        {
            WorldBinaryMapPersistence.SaveWorld(
                width,
                height,
                worldMap,
                worldTick,
                tickCurr,
                tickNext,
                multiblockManager
            );

            PlayerPersistence.SavePlayerData(playerInventory, player);
            EntityPersistence.SaveEntities(entityManager);

            Debug.Log("[SAVE] world saved.");
        }

        
        public static bool LoadWorldFromDisk(
            out WorldData loaded,
            out int width,
            out int height,
            out long loadedTick,
            HashSet<Vector2Int> tickCurr,
            HashSet<Vector2Int> tickNext,
            out List<Multiblock.SaveData> multiblocks)
        {
            return WorldBinaryMapPersistence.LoadWorldFromDisk(
                out loaded,
                out width,
                out height,
                out loadedTick,
                tickCurr,
                tickNext,
                out multiblocks
            );
        }

        
        public static void SavePlayerData(InventoryData playerInventory, Transform playerTransform)
        {
            PlayerPersistence.SavePlayerData(playerInventory, playerTransform);
        }

        
        public static bool LoadPlayerData(
            ItemLibrary itemLibrary,
            out Vector2 playerPosition,
            out List<ItemData> inventory)
        {
            return PlayerPersistence.LoadPlayerData(itemLibrary, out playerPosition, out inventory);
        }

        
        public static void SaveEntities(EntityManager em)
        {
            EntityPersistence.SaveEntities(em);
        }

        
        public static void LoadEntities(
            EntityManager em,
            ItemLibrary itemLibrary,
            FallingBlock fallingPrefab,
            ItemDropper itemDropper,
            GameObject droppedItemPrefab,
            MobLibrary mobLibrary,
            CorpseLibrary corpseLibrary)
        {
            WorldEntityRestoreService.LoadEntities(
                WorldSavePathResolver.GetPath(EntitySaveFile),
                em,
                itemLibrary,
                itemDropper,
                droppedItemPrefab,
                fallingPrefab,
                mobLibrary,
                corpseLibrary
            );
        }
    }
}
