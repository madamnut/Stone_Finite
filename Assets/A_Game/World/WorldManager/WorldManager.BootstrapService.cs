using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

using Game.Core;
using Game.Lobby;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class BootstrapService
        {
            readonly WorldServiceContext _ctx;

            public BootstrapService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public void InitializeLifecycleState()
            {
                _ctx.Width = _ctx.Settings.width;
                _ctx.Height = _ctx.Settings.height;

                ResolvePlayerInventoryReference();

                _ctx.TickCurrent.Clear();
                _ctx.TickNext.Clear();

                CacheUtilityOccupiedIdIfNeeded();
            }

            public void LogBootContext()
            {
                string dirBoot = WorldLoadContext.GetSavePath();
                string pathBoot = Path.Combine(dirBoot, "world.bin");
                Debug.Log($"[BOOT] loadType={WorldLoadContext.loadType}, seed={WorldLoadContext.seed}, saveExists={File.Exists(pathBoot)}, path={pathBoot}");
            }

            public void TickGearNetworks()
            {
                if (_ctx.GearNetworkManager == null)
                    return;

                _ctx.GearNetworkManager.TickSources();
                _ctx.GearNetworkManager.TickNetworks();
            }

            public void AdvanceWorldClock()
            {
                _ctx.LastLoggedSecondTick += _ctx.TicksPerSecond;

                _ctx.WorldMinute++;
                if (_ctx.WorldMinute >= _ctx.MinutesPerDay)
                {
                    _ctx.WorldMinute = 0;
                    _ctx.WorldDay++;
                }
                _ctx.WorldHour = _ctx.WorldMinute / 60;

                _ctx.ApplyTimeSyncedBrightness(forceDirty: false);
            }

            public void BootNewWorld()
            {
                Debug.Log("[BOOT] NewWorld branch: Generate -> SaveWorld()");
                _ctx.WorldMap = WorldDataGenerator.Generate(_ctx.Settings, WorldLoadContext.seed, _ctx.CellLibrary);

                if (TryFindSpawnPosition(out var spawnPosition))
                {
                    _ctx.PlayerTransform.position = spawnPosition;
                    Debug.Log($"[SPAWN] Spawn at X={Mathf.FloorToInt(spawnPosition.x)}, Y={Mathf.FloorToInt(spawnPosition.y)}");
                }
                else
                {
                    Debug.LogWarning("[SPAWN] Failed to find a valid spawn position. Keeping the existing player position.");
                }

                _ctx.SaveWorld();
            }

            public bool TryBootLoadedWorld()
            {
                if (!_ctx.LoadWorldFromDisk(out var loadedWorldMap, out var loadedMultiblocks))
                {
                    Debug.LogError("[BOOT] Failed to load the world file. Returning to lobby.");
                    SceneManager.LoadScene("Loby");
                    return false;
                }

                _ctx.WorldMap = loadedWorldMap;
                _ctx.LoadedMultiblocks = loadedMultiblocks;

                _ctx.LoadPlayerData();
                _ctx.LoadEntities();
                return true;
            }

            public void FinalizeBootInitialization()
            {
                InitializeChunkSystem();
                InitializeWorldClock();

                _ctx.LastLoggedSecondTick = _ctx.WorldTick;

                _ctx.ApplyTimeSyncedBrightness(forceDirty: true);
                _ctx.ChunkSystem.ResetLastPlayerChunk(_ctx.PlayerTransform.position);

                LoadMultiblocks();
            }

            bool TryFindSpawnPosition(out Vector3 spawnPosition)
            {
                spawnPosition = _ctx.PlayerTransform.position;

                int centerX = Mathf.Clamp(2500, 0, _ctx.Width - 1);

                for (int radius = 0; radius < _ctx.Width; radius++)
                {
                    int[] xs = { centerX, centerX - radius, centerX + radius };

                    foreach (int x in xs)
                    {
                        if (x < 0 || x >= _ctx.Width) continue;

                        for (int y = _ctx.Height - 1; y >= 0; y--)
                        {
                            ushort solidId = _ctx.WorldMap.GetSolid(x, y).id;
                            byte waterAmount = _ctx.WorldMap.GetFluid(x, y).amount;

                            if (waterAmount > 0) break;

                            if (solidId == 0) continue;

                            spawnPosition = new Vector3(x + 0.5f, Mathf.Min(y + 5, _ctx.Height - 1) + 0.5f, _ctx.PlayerTransform.position.z);
                            return true;
                        }
                    }
                }

                return false;
            }

            void ResolvePlayerInventoryReference()
            {
                _ctx.PlayerInventory = null;

                if (_ctx.PlayerTransform == null)
                    return;

                var behaviours = _ctx.PlayerTransform.GetComponents<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] is IInventoryOwner owner)
                    {
                        _ctx.PlayerInventory = owner.Inventory;
                        return;
                    }
                }
            }

            void CacheUtilityOccupiedIdIfNeeded()
            {
                if (_ctx.UtilityOccupiedId != 0) return;
                if (_ctx.CellLibrary == null) return;

                if (_ctx.CellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out var occ))
                    _ctx.UtilityOccupiedId = occ;
            }

            void InitializeChunkSystem()
            {
                _ctx.ChunkSystem = new WorldChunkSystem(
                    _ctx.Width,
                    _ctx.Height,
                    ChunkSize,
                    _ctx.ChunkRadius,
                    _ctx.MaxLoadsPerFrame,
                    _ctx.WorldMap,
                    _ctx.ChunkPrefab,
                    _ctx.ChunkRoot,
                    _ctx.CellLibrary,
                    _ctx.RecalculateLightAt
                );
                _ctx.ChunkSystem.InitializePool(_ctx.InitialPoolSize);
            }

            void InitializeWorldClock()
            {
                if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
                {
                    _ctx.WorldTick = 0L;
                    _ctx.WorldMinute = 12 * 60;
                    _ctx.WorldHour = 12;
                    _ctx.WorldDay = 0;
                    return;
                }

                if (_ctx.TicksPerDay <= 0 || _ctx.MinutesPerDay <= 0)
                {
                    _ctx.WorldDay = 0;
                    _ctx.WorldMinute = 0;
                    _ctx.WorldHour = 0;
                    return;
                }

                long day = _ctx.WorldTick / _ctx.TicksPerDay;
                long tickOfDay = _ctx.WorldTick % _ctx.TicksPerDay;
                int ticksPerMin = _ctx.TicksPerDay / _ctx.MinutesPerDay;

                int baseMinutes = 12 * 60;
                int minuteOfDay = baseMinutes + (ticksPerMin > 0 ? (int)(tickOfDay / ticksPerMin) : 0);
                minuteOfDay %= _ctx.MinutesPerDay;

                _ctx.WorldDay = (int)day;
                _ctx.WorldMinute = minuteOfDay;
                _ctx.WorldHour = _ctx.WorldMinute / 60;
            }

            void LoadMultiblocks()
            {
                if (_ctx.MultiblockManager == null)
                    return;

                if (WorldLoadContext.loadType == WorldLoadContext.LoadType.LoadWorld)
                    _ctx.MultiblockManager.LoadFromSaveDatas(_ctx.LoadedMultiblocks);
                else
                    _ctx.MultiblockManager.LoadFromSaveDatas(null);
            }
        }
    }
}
