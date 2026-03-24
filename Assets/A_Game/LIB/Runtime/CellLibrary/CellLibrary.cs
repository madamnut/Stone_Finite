// CellLibrary.cs (?�체 교체�?
// ??Solid/Utility??type 지??추�? 버전
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;


using Game.World;
namespace Game.Data
{
    [DefaultExecutionOrder(-10000)]
    public partial class CellLibrary : MonoBehaviour
    {
        [Header("Solid Json (ATT_Solid.json)")]
        public TextAsset solidJson;
    
        [Header("Utility Json (ATT_Utility.json)")]
        public TextAsset utilityJson;
    
        [Header("Fluid Json (ATT_Fluid.json)")]
        public TextAsset fluidJson;
    
        [Header("Sprite Atlas (Solid+Utility+Fluid)")]
        public SpriteAtlas atlas;
    
        [Serializable, Flags]
        public enum SolidFlags : byte
        {
            None       = 0,
            Collidable = 1 << 0,
            HasGravity = 1 << 1,
        }
    
        struct SolidVariantDef
        {
            public ushort meta;
            public string spriteName;
            public string attachedAt;
            public sbyte brightnessOverride; // -1 none, else 0..15
        }
    
        struct SolidDef
        {
            public ushort id;
            public byte brightness;
            public SolidFlags flags;
            public bool isPlatform;
            public string type;        // ??
            public string interaction; // optional
            public string name;        // json key
            public Dictionary<ushort, SolidVariantDef> variants; // meta -> variant
        }
    
        struct UtilityVariantDef
        {
            public ushort meta;
            public string spriteName;
        }
    
        struct UtilityDef
        {
            public ushort id;
            public string name; // json key
            public string type; // ??
            public Dictionary<ushort, UtilityVariantDef> variants;
        }
    
        struct FluidDef
        {
            public ushort id;
            public byte brightness;
            public string name;
        }
    
        readonly Dictionary<ushort, SolidDef> _solidById = new Dictionary<ushort, SolidDef>(256);
        readonly Dictionary<ushort, UtilityDef> _utilityById = new Dictionary<ushort, UtilityDef>(256);
        readonly Dictionary<ushort, FluidDef> _fluidById = new Dictionary<ushort, FluidDef>(32);
    
        readonly Dictionary<string, ushort> _solidIdByName = new Dictionary<string, ushort>(256);
        readonly Dictionary<string, ushort> _utilityIdByName = new Dictionary<string, ushort>(256);
        readonly Dictionary<string, ushort> _fluidIdByName = new Dictionary<string, ushort>(32);
    
        readonly Dictionary<uint, Sprite> _solidSpriteByKey = new Dictionary<uint, Sprite>(512);
        readonly Dictionary<uint, Sprite> _utilitySpriteByKey = new Dictionary<uint, Sprite>(512);
    
        readonly Dictionary<ushort, Sprite> _fluidBaseSpriteById = new Dictionary<ushort, Sprite>(32);
        readonly Dictionary<ushort, Sprite[]> _fluidLevelSpritesById = new Dictionary<ushort, Sprite[]>(32);
    
        readonly Dictionary<ushort, Tile> _bgTileById = new Dictionary<ushort, Tile>(256);
        readonly Dictionary<uint, Tile> _solidTileByKey = new Dictionary<uint, Tile>(512);
        readonly Dictionary<uint, Tile> _platformColliderTileByKey = new Dictionary<uint, Tile>(256);
        readonly Dictionary<uint, Tile> _utilityTileByKey = new Dictionary<uint, Tile>(512);
        readonly Dictionary<uint, Tile> _fluidTileByKey = new Dictionary<uint, Tile>(256);
    
        static readonly byte[] _amountToLevel = BuildAmountToLevel();
    
        static byte[] BuildAmountToLevel()
        {
            var map = new byte[WorldData.MaxFluid + 1];
            for (int a = 0; a <= WorldData.MaxFluid; a++)
            {
                if (a <= 0) { map[a] = 0; continue; }
                int lv = (a + 7) / 8;
                if (lv < 1) lv = 1;
                if (lv > 16) lv = 16;
                map[a] = (byte)lv;
            }
            return map;
        }
    }
}
