using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

using Game.Data;
using Game.Player;
using Game.Lobby;

namespace Game.World
{
    public partial class WorldManager
    {
        void Awake()
        {
            InitializeLifecycleState();
            LogBootContext();
    
            if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
            {
                BootNewWorld();
            }
            else if (WorldLoadContext.loadType == WorldLoadContext.LoadType.LoadWorld)
            {
                if (!TryBootLoadedWorld())
                    return;
            }
    
            FinalizeBootInitialization();
        }
    
        void Start()
        {
            ApplyLoadedPlayerAndInventory();
            StartCoroutine(AutosaveLoop());
        }
    
        void OnApplicationQuit()
        {
            if (_didQuitSave) return;
            _didQuitSave = true;
            SaveWorld();
        }
    
        public void OnClickSave()
        {
            SaveWorld();
        }
    
        void Update()
        {
            chunkSystem.UpdateVisibleChunks(player.position, this);
        }
    
        void FixedUpdate()
        {
            TickGearNetworks();
    
            StepTick();
            DoRandomTicks();
    
            worldTick++;
    
            if (worldTick - _lastLoggedSecondTick >= ticksPerSecond)
                AdvanceWorldClock();
    
            ProcessArtificialLightQueues();
            chunkSystem.ProcessDirtyChunks();
        }
    
        private void InitializeLifecycleState()
        {
            W = settings.width;
            H = settings.height;
    
            tickCurr.Clear();
            tickNext.Clear();
    
            CacheUtilityOccupiedIdIfNeeded();
        }
    
        private void LogBootContext()
        {
            string dirBoot = WorldLoadContext.GetSavePath();
            string pathBoot = Path.Combine(dirBoot, "world.bin");
            Debug.Log($"[BOOT] loadType={WorldLoadContext.loadType}, seed={WorldLoadContext.seed}, saveExists={File.Exists(pathBoot)}, path={pathBoot}");
        }
    
        private void TickGearNetworks()
        {
            if (gearNetworkManager == null)
                return;
    
            gearNetworkManager.TickSources();
            gearNetworkManager.TickNetworks();
        }
    
        private void AdvanceWorldClock()
        {
            _lastLoggedSecondTick += ticksPerSecond;
    
            worldMinute++;
            if (worldMinute >= minutesPerDay)
            {
                worldMinute = 0;
                worldDay++;
            }
            worldHour = worldMinute / 60;
    
            ApplyTimeSyncedBrightness(forceDirty: false);
            var band = GetTimeBand();
        }
    
        private void BootNewWorld()
        {
            Debug.Log("[BOOT] NewWorld branch: Generate -> SaveWorld()");
            worldMap = WorldDataGenerator.Generate(settings, WorldLoadContext.seed, cellLibrary);
    
            if (TryFindSpawnPosition(out var spawnPosition))
            {
                player.position = spawnPosition;
                Debug.Log($"[SPAWN] Spawn at X={Mathf.FloorToInt(spawnPosition.x)}, Y={Mathf.FloorToInt(spawnPosition.y)}");
            }
            else
            {
                Debug.LogWarning("[SPAWN] Failed to find a valid spawn position. Keeping the existing player position.");
            }
    
            SaveWorld();
        }
    
        private bool TryBootLoadedWorld()
        {
            if (!LoadWorldFromDisk(out worldMap, out _loadedMultiblocks))
            {
                Debug.LogError("[BOOT] Failed to load the world file. Returning to lobby.");
                SceneManager.LoadScene("Loby");
                return false;
            }
    
            LoadPlayerData();
            LoadEntities();
            return true;
        }
    
        private bool TryFindSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = player.position;
    
            int centerX = Mathf.Clamp(2500, 0, W - 1);
    
            for (int radius = 0; radius < W; radius++)
            {
                int[] xs = { centerX, centerX - radius, centerX + radius };
    
                foreach (int x in xs)
                {
                    if (x < 0 || x >= W) continue;
    
                    for (int y = H - 1; y >= 0; y--)
                    {
                        ushort solidId = worldMap.GetSolid(x, y).id;
                        byte waterAmount = worldMap.GetFluid(x, y).amount;
    
                        if (waterAmount > 0) break;
    
                        if (solidId == 0) continue;
    
                        spawnPosition = new Vector3(x + 0.5f, Mathf.Min(y + 5, H - 1) + 0.5f, player.position.z);
                        return true;
                    }
                }
            }
    
            return false;
        }
    
        private void InitializeChunkSystem()
        {
            chunkSystem = new WorldChunkSystem(
                W,
                H,
                ChunkSize,
                ChunkRadius,
                maxLoadsPerFrame,
                worldMap,
                chunkPrefab,
                chunkRoot,
                cellLibrary,
                RecalculateLightAt
            );
            chunkSystem.InitializePool(initialPoolSize);
        }
    
        private void InitializeWorldClock()
        {
            if (WorldLoadContext.loadType == WorldLoadContext.LoadType.NewWorld)
            {
                worldTick = 0L;
                worldMinute = 12 * 60;
                worldHour = 12;
                worldDay = 0;
                return;
            }
    
            if (ticksPerDay <= 0 || minutesPerDay <= 0)
            {
                worldDay = 0;
                worldMinute = 0;
                worldHour = 0;
                return;
            }
    
            long day = worldTick / ticksPerDay;
            long tickOfDay = worldTick % ticksPerDay;
            int ticksPerMin = ticksPerDay / minutesPerDay;
    
            int baseMinutes = 12 * 60;
            int minuteOfDay = baseMinutes + (ticksPerMin > 0 ? (int)(tickOfDay / ticksPerMin) : 0);
            minuteOfDay %= minutesPerDay;
    
            worldDay = (int)day;
            worldMinute = minuteOfDay;
            worldHour = worldMinute / 60;
        }
    
        private void LoadMultiblocks()
        {
            if (multiblockManager == null)
                return;
    
            if (WorldLoadContext.loadType == WorldLoadContext.LoadType.LoadWorld)
                multiblockManager.LoadFromSaveDatas(_loadedMultiblocks);
            else
                multiblockManager.LoadFromSaveDatas(null);
        }
    
        private void FinalizeBootInitialization()
        {
            InitializeChunkSystem();
            InitializeWorldClock();
    
            _lastLoggedSecondTick = worldTick;
    
            ApplyTimeSyncedBrightness(forceDirty: true);
            chunkSystem.ResetLastPlayerChunk(player.position);
    
            LoadMultiblocks();
        }
    }
}
