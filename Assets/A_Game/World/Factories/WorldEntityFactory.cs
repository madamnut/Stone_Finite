


using UnityEngine;

using Game.Core;

namespace Game.World
{
    public static class WorldEntityFactory
    {
        
        public static DroppedItem SpawnDroppedItem(
            EntityManager entityManager,
            ItemDropper itemDropper,
            GameObject droppedItemPrefab,
            ItemData data,
            Vector2 position)
        {
            if (data == null || entityManager == null)

                return null;

            if (itemDropper != null)
            {
                Transform parent = itemDropper.dropRoot != null ? itemDropper.dropRoot : itemDropper.transform;
                var spawned = itemDropper.SpawnDroppedItemAt(data, new Vector3(position.x, position.y, 0f), parent);
                if (spawned != null)
                    return spawned;
            }

            if (droppedItemPrefab == null)
                return null;

            var go = Object.Instantiate(droppedItemPrefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
            var droppedItem = go.GetComponent<DroppedItem>();
            if (droppedItem == null)
            {
                Object.Destroy(go);
                return null;
            }

            droppedItem.Initialize(data);
            entityManager.Register(droppedItem);
            return droppedItem;
        }

        
        public static Mob SpawnMob(
            EntityManager entityManager,
            MobLibrary mobLibrary,
            string mobId,
            Vector2 position,
            Transform parentOverride = null)
        {
            if (mobLibrary == null || string.IsNullOrEmpty(mobId))
                return null;

            return mobLibrary.SpawnMob(mobId, new Vector3(position.x, position.y, 0f), entityManager, parentOverride);
        }

        
        public static Corpse SpawnCorpse(CorpseLibrary corpseLibrary, string corpseId, Vector2 position)
        {
            if (corpseLibrary == null || string.IsNullOrEmpty(corpseId))
                return null;

            return corpseLibrary.SpawnCorpse(corpseId, position);
        }

        
        public static FallingBlock SpawnFallingBlock(
            EntityManager entityManager,
            FallingBlock fallingPrefab,
            EntitySaveData data)
        {
            if (entityManager == null || fallingPrefab == null || data == null)
                return null;

            var go = Object.Instantiate(fallingPrefab, data.Position, Quaternion.identity);
            var fallingBlock = go.GetComponent<FallingBlock>();
            if (fallingBlock == null)
            {
                Object.Destroy(go);
                return null;
            }

            fallingBlock.FromSaveData(data);
            entityManager.Register(fallingBlock);
            return fallingBlock;
        }
    }
}
