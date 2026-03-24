// WorldManager.cs (??ш끽維????????곕츅??
// ? ??????袁⑸즵???
// - Utility Occupied 癲????????? "CogwheelOccupied"???????
// - BreakUtilityAt(): CogwheelOccupied癲????????됰씭?? return 0
// - BreakUtilityAt(): ??れ삀?節낆젂繹먮씮異???れ삀????棺??짆?먰맪????(????덉쉐???⑤슣????癰귙끋源?+ footprint ??癰귙끋源?+ ??筌먦끇??
// - BreakUtilityAt(): ???⑥ロ떘 ???ャ뀖???DT_Cell ??れ삀??뫢???筌먦끇????⑤베堉? (utility name ????
// - BreakBG(): VFX + DT_Cell ??筌먦끇????⑤베堉? (BG id??Solid name 癲ル슪???嚥?肉?GetSolidName)
// - BreakUtility() ?怨뚮옓??븍닱???⑤베堉?
// - RemoveSolidNoDrop() ??⑤베堉?
// - BreakSolid(): Solid type ??れ삀??뫢???됰슣維????⑤베堉?
//   * Source: ??れ삀?節낆젂????덉쉐???⑤슣??Source ?嶺뚮ㅎ?볠뤃???癰귙끋源???Solid ???????筌먦끇??
//   * Belt: ??れ삀?節낆젂????덉쉐???⑤슣??Belt 癲ル슢??湲룹물???癰귙끋源????袁⑸즵????Belt Solid??no-drop ??癰귙끋源?
// - BreakUtility(): ??れ삀?節낆젂????????belt material ??筌먦끇????癰귙끋源?
//   (?類?뺨泳???????獄???Solid ?? ??筌먦끇????⑥??癲ル슪?ｇ몭??

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Game.Data;
using Game.Player;

namespace Game.World
{
    public partial class WorldManager : MonoBehaviour
    {
        public enum CellLayer { Solid, BG }
    
        public enum RelV { Neutral = 0, Up = 1, Down = 2 }
        public enum RelH { Neutral = 0, Left = 1, Right = 2 }
    
        [Header("??釉먮폇????獄쏅똻?????源놁젳")]
        public WorldGenSettings settings;
    
        [Header("Libraries")]
        public CellLibrary cellLibrary;
        public RecipeLibrary recipeLibrary;
    
        [Header("Chunk Prefab & Root")]
        public GameObject chunkPrefab;
        public Transform chunkRoot;
        public int initialPoolSize = 200;
    
        [Header("???????⑤９苑????????異????源놁젳")]
        public Transform player;
        public Game.Player.Player playerComp;
        public int ChunkRadius = 7;
        public int maxLoadsPerFrame = 4;
    
        [Header("Falling Blocks")]
        public FallingBlock fallingBlockPrefab;
    
        [Header("Drops / VFX")]
        public ItemDropper itemDropper;
        public VfxManager vfx;
    
        [Header("Multiblock")]
        public MultiblockManager multiblockManager;
        private List<Multiblock.SaveData> _loadedMultiblocks;
    
        [Header("Corpse")]
        public CorpseLibrary corpseLibrary;
    
        [Header("Entity")]
        public EntityManager entityManager;
    
        [Header("Mob")]
        public MobLibrary mobLibrary;
    
        [Header("??ш끽維쀩????繹먮끏???怨쀫뮛?????嶺뚮ㅎ?믦맱??怨뚮옖甕???")]
        public ItemLibrary itemLibrary;
    
        [Header("Gear Network")]
        public GearNetworkManager gearNetworkManager;
    
        [Header("Time Settings")]
        public int ticksPerSecond = 20;
        public int minutesPerDay = 24 * 60;
        public int ticksPerDay = 28800;
    
        public enum TimeBand
        {
            Midnight, LateNight, Dawn, EarlyMorning, Morning, Noon, Afternoon, Evening, Dusk, Night
        }
    
        public const int ChunkSize = 16;
        private const byte NAT_MAX = 15;
        private const byte ART_MAX = 15;
    
        [Header("Global Brightness Offset (auto by time) 0=?袁⑸즵??? 15=???嶺??")]
        [Range(0, 15)] public byte globalBrightnessOffset = 0;
    
        [Header("Night Darkness Limit (0=?袁⑸즵??? 15=??ш끽維????????")]
        [Range(0, 15)] public byte maxDarknessOffset = 3;
    
        private byte _lastBrightnessOffset = 255;
    
        private const int ATT_AIR = 1;
        private const int ATT_BG = 2;
        private const int ATT_SOLID = 3;
    
        private int W, H;
    
        private WorldData worldMap;
        private WorldChunkSystem chunkSystem;
    
        public long worldTick;
        public int worldMinute;
        public int worldHour;
        public int worldDay;
        private long _lastLoggedSecondTick = -1;
    
        private HashSet<Vector2Int> tickCurr = new();
        private HashSet<Vector2Int> tickNext = new();
    
        private bool _didQuitSave = false;
    
        private bool _hasLoadedPlayerData = false;
        private Vector2 _loadedPlayerPos;
        private List<ItemData> _loadedInventory;
    
        [Header("Random Tick")]
        public int randomTicksPerWorldTick = 64;
    
        [Header("Artificial Light Flood (Budget)")]
        public int artificialLightOpsPerTick = 8000;
    
        private struct IncNode
        {
            public int x, y;
            public byte v;
            public IncNode(int x, int y, byte v) { this.x = x; this.y = y; this.v = v; }
        }
    
        private struct DecNode
        {
            public int x, y;
            public byte v;
            public DecNode(int x, int y, byte v) { this.x = x; this.y = y; this.v = v; }
        }
    
        private readonly Queue<IncNode> _incQ = new();
        private readonly Queue<DecNode> _decQ = new();
    
        private readonly HashSet<Vector2Int> _seedSet = new();
        private readonly List<Vector2Int> _seedList = new();
    
        private readonly HashSet<Vector2Int> _lightChangedSet = new();
        private readonly List<Vector2Int> _lightChangedList = new();
    
        private const ushort META_DEFAULT = 0;
        private const ushort META_BG = 1;
        private const ushort META_UP = 2;
        private const ushort META_DOWN = 3;
        private const ushort META_LEFT = 4;
        private const ushort META_RIGHT = 5;
    
        private ushort _utilityOccupiedId = 0;
        private static readonly Vector2Int[] _dirs4 = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
    
        /*????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
         * Read-only Query
         *????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????*/
        public bool InBounds(int x, int y) => worldMap.InBounds(x, y);
    
        public ushort GetSolidId(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return 0;
            return worldMap.GetSolid(x, y).id;
        }
    
        public ushort GetBGId(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return 0;
            return worldMap.GetBG(x, y);
        }
    
        public ushort GetFluidId(int x, int y, out byte amount)
        {
            if (!worldMap.InBounds(x, y)) { amount = 0; return 0; }
            var f = worldMap.GetFluid(x, y);
            amount = f.amount;
            return f.id;
        }
    
        public UtilityCell GetUtility(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return default;
            return worldMap.GetUtility(x, y);
        }
    
        public ushort GetUtilityId(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return 0;
            return worldMap.GetUtility(x, y).id;
        }
    
        public bool IsUtilityEmpty(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return false;
            return worldMap.GetUtility(x, y).id == 0;
        }
    
        public bool IsCollidable(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return true;
            var s = worldMap.GetSolid(x, y);
            if (s.id == 0) return false;
            return (cellLibrary.GetSolidFlags(s.id) & CellLibrary.SolidFlags.Collidable) != 0;
        }
    
        private bool IsSupportSolid(int x, int y)
        {
            if (!worldMap.InBounds(x, y)) return false;
    
            var s = worldMap.GetSolid(x, y);
            if (s.id == 0) return false;
    
            var flags = cellLibrary.GetSolidFlags(s.id);
            if ((flags & CellLibrary.SolidFlags.Collidable) != 0) return true;
    
            return cellLibrary.IsPlatform(s.id);
        }
    
        private bool HasGravity(ushort solidId)
        {
            return (cellLibrary.GetSolidFlags(solidId) & CellLibrary.SolidFlags.HasGravity) != 0;
        }
    }
}
