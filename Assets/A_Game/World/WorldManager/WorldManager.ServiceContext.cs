using System.Collections.Generic;
using UnityEngine;

using Game.Core;
using Game.Data;
using Game.Support;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class WorldServiceContext
        {
            readonly WorldManager _owner;

            public WorldServiceContext(WorldManager owner)
            {
                _owner = owner;
            }

            public WorldManager Owner => _owner;
            public int Width { get => _owner.W; set => _owner.W = value; }
            public int Height { get => _owner.H; set => _owner.H = value; }
            public WorldData WorldMap { get => _owner.worldMap; set => _owner.worldMap = value; }
            public WorldChunkSystem ChunkSystem { get => _owner.chunkSystem; set => _owner.chunkSystem = value; }
            public WorldGenSettings Settings => _owner.settings;
            public CellLibrary CellLibrary => _owner.cellLibrary;
            public ItemLibrary ItemLibrary => _owner.itemLibrary;
            public RecipeLibrary RecipeLibrary => _owner.recipeLibrary;
            public ItemDropper ItemDropper => _owner.itemDropper;
            public VfxManager Vfx => _owner.vfx;
            public MultiblockManager MultiblockManager => _owner.multiblockManager;
            public CorpseLibrary CorpseLibrary => _owner.corpseLibrary;
            public EntityManager EntityManager => _owner.entityManager;
            public MobLibrary MobLibrary => _owner.mobLibrary;
            public GearNetworkManager GearNetworkManager => _owner.gearNetworkManager;
            public Transform PlayerTransform => _owner.player;
            public FallingBlock FallingBlockPrefab => _owner.fallingBlockPrefab;
            public GameObject ChunkPrefab => _owner.chunkPrefab;
            public Transform ChunkRoot => _owner.chunkRoot;
            public int InitialPoolSize => _owner.initialPoolSize;
            public int ChunkRadius => _owner.ChunkRadius;
            public int MaxLoadsPerFrame => _owner.maxLoadsPerFrame;
            public int TicksPerSecond => _owner.ticksPerSecond;
            public int MinutesPerDay => _owner.minutesPerDay;
            public int TicksPerDay => _owner.ticksPerDay;
            public int RandomTicksPerWorldTick => _owner.randomTicksPerWorldTick;
            public int ArtificialLightOpsPerTick => _owner.artificialLightOpsPerTick;
            public byte GlobalBrightnessOffset { get => _owner.globalBrightnessOffset; set => _owner.globalBrightnessOffset = value; }
            public byte MaxDarknessOffset => _owner.maxDarknessOffset;
            public byte LastBrightnessOffset { get => _owner._lastBrightnessOffset; set => _owner._lastBrightnessOffset = value; }
            public long WorldTick { get => _owner.worldTick; set => _owner.worldTick = value; }
            public int WorldMinute { get => _owner.worldMinute; set => _owner.worldMinute = value; }
            public int WorldHour { get => _owner.worldHour; set => _owner.worldHour = value; }
            public int WorldDay { get => _owner.worldDay; set => _owner.worldDay = value; }
            public long LastLoggedSecondTick { get => _owner._lastLoggedSecondTick; set => _owner._lastLoggedSecondTick = value; }
            public HashSet<Vector2Int> TickCurrent { get => _owner.tickCurr; set => _owner.tickCurr = value; }
            public HashSet<Vector2Int> TickNext { get => _owner.tickNext; set => _owner.tickNext = value; }
            public List<Multiblock.SaveData> LoadedMultiblocks { get => _owner._loadedMultiblocks; set => _owner._loadedMultiblocks = value; }
            public bool HasLoadedPlayerData { get => _owner._hasLoadedPlayerData; set => _owner._hasLoadedPlayerData = value; }
            public Vector2 LoadedPlayerPosition { get => _owner._loadedPlayerPos; set => _owner._loadedPlayerPos = value; }
            public List<ItemData> LoadedInventory { get => _owner._loadedInventory; set => _owner._loadedInventory = value; }
            public InventoryData PlayerInventory { get => _owner._playerInventory; set => _owner._playerInventory = value; }
            public ushort UtilityOccupiedId { get => _owner._utilityOccupiedId; set => _owner._utilityOccupiedId = value; }
            public Queue<IncNode> IncreaseQueue => _owner._incQ;
            public Queue<DecNode> DecreaseQueue => _owner._decQ;
            public HashSet<Vector2Int> SeedSet => _owner._seedSet;
            public List<Vector2Int> SeedList => _owner._seedList;
            public HashSet<Vector2Int> LightChangedSet => _owner._lightChangedSet;
            public List<Vector2Int> LightChangedList => _owner._lightChangedList;
            public DropAndVfxService DropAndVfxService => _owner._dropAndVfxService;
            public CellEditService CellEditService => _owner._cellEditService;
            public EditSupportService EditSupportService => _owner._editSupportService;
            public UtilityEditService UtilityEditService => _owner._utilityEditService;
            public BootstrapService BootstrapService => _owner._bootstrapService;
            public TickSimulationService TickSimulationService => _owner._tickSimulationService;
            public RandomTickSimulationService RandomTickSimulationService => _owner._randomTickSimulationService;
            public LightingService LightingService => _owner._lightingService;
            public RuntimeStateService RuntimeStateService => _owner._runtimeStateService;
            public FluidSimulationService FluidSimulationService => _owner._fluidSimulationService;
            public GravitySimulationService GravitySimulationService => _owner._gravitySimulationService;

            public void MarkChunkDirty(int x, int y, bool markSolid = true, bool markBG = false, bool markLiquid = false, bool markUtility = false)
                => _owner.MarkChunkDirty(x, y, markSolid, markBG, markLiquid, markUtility);

            public void OnCellEdited(int x, int y)
                => _owner.OnCellEdited(x, y);

            public void HandleSourceLightChangeAt(int x, int y, ushort oldSolidId, ushort oldSolidMeta, ushort oldFluidId)
                => _owner.HandleSourceLightChangeAt(x, y, oldSolidId, oldSolidMeta, oldFluidId);

            public void RecalculateLightAt(int x, int y)
                => _owner.RecalculateLightAt(x, y);

            public void MarkLightDirtyCell(int x, int y)
                => _owner.MarkLightDirtyCell(x, y);

            public void MarkLightDirtyCells(List<Vector2Int> cells)
                => _owner.MarkLightDirtyCells(cells);

            public void MarkLightDirtyRect(int x, int y, int w, int h)
                => _owner.MarkLightDirtyRect(x, y, w, h);

            public void ApplyTimeSyncedBrightness(bool forceDirty)
                => _owner._runtimeStateService.ApplyTimeSyncedBrightness(forceDirty);

            public bool IsCollidable(int x, int y)
                => _owner.IsCollidable(x, y);

            public bool HasGravity(ushort id)
                => _owner.HasGravity(id);

            public ushort BreakSolid(int x, int y)
                => _owner.BreakSolid(x, y);

            public bool IsSupportSolid(int x, int y)
                => _owner.IsSupportSolid(x, y);

            public bool InBounds(int x, int y)
                => _owner.InBounds(x, y);

            public ushort GetSolidId(int x, int y)
                => _owner.GetSolidId(x, y);

            public ushort GetBGId(int x, int y)
                => _owner.GetBGId(x, y);

            public ushort GetUtilityId(int x, int y)
                => _owner.GetUtilityId(x, y);

            public UtilityCell GetUtility(int x, int y)
                => _owner.GetUtility(x, y);

            public void SaveWorld()
                => _owner.SaveWorld();

            public void UpdateVisibleChunks()
            {
                if (_owner.chunkSystem == null || _owner.player == null)
                    return;

                _owner.chunkSystem.UpdateVisibleChunks(_owner.player.position, _owner);
            }

            public void ProcessDirtyChunks()
            {
                _owner.chunkSystem?.ProcessDirtyChunks();
            }

            public bool LoadWorldFromDisk(out WorldData loaded, out List<Multiblock.SaveData> multiblocks)
                => _owner.LoadWorldFromDisk(out loaded, out multiblocks);

            public void LoadPlayerData()
                => _owner.LoadPlayerData();

            public void LoadEntities()
                => _owner.LoadEntities();
        }
    }
}
