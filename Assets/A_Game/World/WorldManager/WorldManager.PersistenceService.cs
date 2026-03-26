


using System.Collections.Generic;
using UnityEngine;

using Game.Core;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class PersistenceService
        {

            readonly WorldServiceContext _ctx;

            
            public PersistenceService(WorldServiceContext context)
            {
                _ctx = context;
            }

            
            public void SaveWorld()
            {
                WorldSaveSystem.SaveWorld(
                    _ctx.Width,
                    _ctx.Height,
                    _ctx.WorldMap,
                    _ctx.WorldTick,
                    _ctx.TickCurrent,
                    _ctx.TickNext,
                    _ctx.PlayerInventory,
                    _ctx.PlayerTransform,
                    _ctx.EntityManager,
                    _ctx.MultiblockManager
                );
            }

            
            public bool LoadWorldFromDisk(out WorldData loaded, out List<Multiblock.SaveData> multiblocks)
            {
                int w, h;
                long loadedTick;

                bool ok = WorldSaveSystem.LoadWorldFromDisk(
                    out loaded,
                    out w,
                    out h,
                    out loadedTick,
                    _ctx.TickCurrent,
                    _ctx.TickNext,
                    out multiblocks
                );
                if (ok)
                {
                    _ctx.Width = w;
                    _ctx.Height = h;
                    _ctx.WorldTick = loadedTick;
                }
                else
                {
                    multiblocks = null;
                }

                return ok;
            }

            
            public void LoadPlayerData()
            {
                _ctx.HasLoadedPlayerData = WorldSaveSystem.LoadPlayerData(
                    _ctx.ItemLibrary,
                    out var loadedPlayerPos,
                    out var loadedInventory
                );
                _ctx.LoadedPlayerPosition = loadedPlayerPos;
                _ctx.LoadedInventory = loadedInventory;
            }

            
            public void LoadEntities()
            {
                GameObject dropPrefab = _ctx.ItemDropper != null ? _ctx.ItemDropper.droppedItemPrefab : null;

                WorldSaveSystem.LoadEntities(
                    _ctx.EntityManager,
                    _ctx.ItemLibrary,
                    _ctx.FallingBlockPrefab,
                    _ctx.ItemDropper,
                    dropPrefab,
                    _ctx.MobLibrary,
                    _ctx.CorpseLibrary
                );
            }

            
            public void ApplyLoadedPlayerAndInventory()
            {
                if (!_ctx.HasLoadedPlayerData) return;

                var pos = _ctx.PlayerTransform.position;
                _ctx.PlayerTransform.position = new Vector3(_ctx.LoadedPlayerPosition.x, _ctx.LoadedPlayerPosition.y, pos.z);

                if (_ctx.PlayerInventory == null) return;

                var slots = _ctx.PlayerInventory.items;
                int n = Mathf.Min(slots.Count, _ctx.LoadedInventory.Count);

                for (int i = 0; i < n; i++)
                {
                    var data = _ctx.LoadedInventory[i];
                    slots[i] = (data != null && data.Count > 0) ? data : null;
                }

                for (int i = n; i < slots.Count; i++)
                    slots[i] = null;

                _ctx.PlayerInventory.NotifyChanged();
            }
        }
    }
}
