// CellLibrary.cs (전체 교체본)
// ✅ Solid/Utility에 type 지원 추가 버전
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

[DefaultExecutionOrder(-10000)]
public class CellLibrary : MonoBehaviour
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
        public string type;        // ✅
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
        public string type; // ✅
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

    void Awake()
    {
        BuildSolidCache();
        BuildUtilityCache();
        BuildFluidCache();
        BuildSpriteCache();
        BuildTileCache();
    }

    static uint MakeKey(ushort id, ushort meta) => ((uint)id << 16) | meta;
    static uint MakeFluidLevelKey(ushort fluidId, byte level) => ((uint)fluidId << 16) | level;

    void BuildSolidCache()
    {
        _solidById.Clear();
        _solidIdByName.Clear();

        if (solidJson == null || string.IsNullOrEmpty(solidJson.text))
            return;

        var root = JObject.Parse(solidJson.text);

        foreach (var prop in root.Properties())
        {
            string name = prop.Name;
            var o = (JObject)prop.Value;

            int idInt = o["id"]?.Value<int>() ?? 0;
            if (idInt < 0) idInt = 0;
            if (idInt > ushort.MaxValue) idInt = ushort.MaxValue;
            ushort id = (ushort)idInt;

            // ✅ type (미기재면 Default)
            string type = o["type"]?.Value<string>();
            if (string.IsNullOrEmpty(type)) type = "Default";

            int bInt = o["brightness"]?.Value<int>() ?? 0;
            if (bInt < 0) bInt = 0; else if (bInt > 15) bInt = 15;
            byte brightness = (byte)bInt;

            bool collidable = o["collidable"]?.Value<bool>() ?? false;
            bool gravity = o["gravity"]?.Value<bool>() ?? false;
            bool isPlatform = o["isPlatform"]?.Value<bool>() ?? false;

            if (collidable && isPlatform)
                Debug.LogError($"[CellLibrary] invalid solid def: both collidable and isPlatform are true (name={name}, id={id})");

            SolidFlags flags = SolidFlags.None;
            if (collidable) flags |= SolidFlags.Collidable;
            if (gravity) flags |= SolidFlags.HasGravity;

            string interaction = o["interaction"]?.Value<string>();

            Dictionary<ushort, SolidVariantDef> variants = null;

            if (o.TryGetValue("variants", out JToken vTok) && vTok is JArray vArr && vArr.Count > 0)
            {
                variants = new Dictionary<ushort, SolidVariantDef>(vArr.Count);

                for (int i = 0; i < vArr.Count; i++)
                {
                    if (!(vArr[i] is JObject vObj)) continue;

                    int metaInt = vObj["meta"]?.Value<int>() ?? 0;
                    if (metaInt < 0) metaInt = 0;
                    if (metaInt > ushort.MaxValue) metaInt = ushort.MaxValue;
                    ushort meta = (ushort)metaInt;

                    string spriteName = vObj["sprite"]?.Value<string>();
                    if (string.IsNullOrEmpty(spriteName))
                        continue;

                    string attachedAt = vObj["attachedAt"]?.Value<string>();

                    sbyte brightnessOverride = -1;
                    if (vObj.TryGetValue("brightness_override", out JToken boTok) && boTok != null && boTok.Type != JTokenType.Null)
                    {
                        int boInt = boTok.Value<int>();
                        if (boInt < 0) boInt = 0; else if (boInt > 15) boInt = 15;
                        brightnessOverride = (sbyte)boInt;
                    }

                    variants[meta] = new SolidVariantDef
                    {
                        meta = meta,
                        spriteName = spriteName,
                        attachedAt = attachedAt,
                        brightnessOverride = brightnessOverride
                    };
                }

                if (variants.Count == 0)
                    variants = null;
            }

            if (variants == null)
                continue;

            var def = new SolidDef
            {
                id = id,
                type = type,
                brightness = brightness,
                flags = flags,
                isPlatform = isPlatform,
                interaction = interaction,
                name = name,
                variants = variants
            };

            _solidById[id] = def;

            if (!_solidIdByName.ContainsKey(name))
                _solidIdByName.Add(name, id);
        }
    }

    void BuildUtilityCache()
    {
        _utilityById.Clear();
        _utilityIdByName.Clear();

        if (utilityJson == null || string.IsNullOrEmpty(utilityJson.text))
            return;

        var root = JObject.Parse(utilityJson.text);

        foreach (var prop in root.Properties())
        {
            string name = prop.Name;
            var o = (JObject)prop.Value;

            int idInt = o["id"]?.Value<int>() ?? 0;
            if (idInt < 0) idInt = 0;
            if (idInt > ushort.MaxValue) idInt = ushort.MaxValue;
            ushort id = (ushort)idInt;

            // ✅ type (미기재면 Default)
            string type = o["type"]?.Value<string>();
            if (string.IsNullOrEmpty(type)) type = "Default";

            Dictionary<ushort, UtilityVariantDef> variants = null;

            if (o.TryGetValue("variants", out JToken vTok) && vTok is JArray vArr && vArr.Count > 0)
            {
                variants = new Dictionary<ushort, UtilityVariantDef>(vArr.Count);

                for (int i = 0; i < vArr.Count; i++)
                {
                    if (!(vArr[i] is JObject vObj)) continue;

                    int metaInt = vObj["meta"]?.Value<int>() ?? 0;
                    if (metaInt < 0) metaInt = 0;
                    if (metaInt > ushort.MaxValue) metaInt = ushort.MaxValue;
                    ushort meta = (ushort)metaInt;

                    string spriteName = vObj["sprite"]?.Value<string>();
                    if (string.IsNullOrEmpty(spriteName))
                        continue;

                    variants[meta] = new UtilityVariantDef { meta = meta, spriteName = spriteName };
                }

                if (variants.Count == 0)
                    variants = null;
            }

            var def = new UtilityDef
            {
                id = id,
                name = name,
                type = type,
                variants = variants
            };

            _utilityById[id] = def;

            if (!_utilityIdByName.ContainsKey(name))
                _utilityIdByName.Add(name, id);
        }
    }

    void BuildFluidCache()
    {
        _fluidById.Clear();
        _fluidIdByName.Clear();

        if (fluidJson == null || string.IsNullOrEmpty(fluidJson.text))
            return;

        var root = JObject.Parse(fluidJson.text);

        foreach (var prop in root.Properties())
        {
            string name = prop.Name;
            var o = (JObject)prop.Value;

            int idInt = o["id"]?.Value<int>() ?? 0;
            if (idInt < 0) idInt = 0;
            if (idInt > ushort.MaxValue) idInt = ushort.MaxValue;
            ushort id = (ushort)idInt;

            int bInt = o["brightness"]?.Value<int>() ?? 0;
            if (bInt < 0) bInt = 0; else if (bInt > 15) bInt = 15;
            byte brightness = (byte)bInt;

            var def = new FluidDef { id = id, brightness = brightness, name = name };
            _fluidById[id] = def;

            if (!_fluidIdByName.ContainsKey(name))
                _fluidIdByName.Add(name, id);
        }
    }

    void BuildSpriteCache()
    {
        _solidSpriteByKey.Clear();
        _utilitySpriteByKey.Clear();
        _fluidBaseSpriteById.Clear();
        _fluidLevelSpritesById.Clear();

        if (atlas == null) return;

        foreach (var kv in _solidById)
        {
            var def = kv.Value;
            foreach (var vkv in def.variants)
            {
                var v = vkv.Value;
                var sp = atlas.GetSprite(v.spriteName);
                if (sp != null)
                    _solidSpriteByKey[MakeKey(def.id, v.meta)] = sp;
            }
        }

        foreach (var kv in _utilityById)
        {
            var def = kv.Value;
            if (def.variants == null) continue;

            foreach (var vkv in def.variants)
            {
                var v = vkv.Value;
                var sp = atlas.GetSprite(v.spriteName);
                if (sp != null)
                    _utilitySpriteByKey[MakeKey(def.id, v.meta)] = sp;
            }
        }

        foreach (var kv in _fluidById)
        {
            var def = kv.Value;

            var baseSp = atlas.GetSprite(def.name);
            if (baseSp != null)
                _fluidBaseSpriteById[def.id] = baseSp;

            var arr = new Sprite[17];
            for (int level = 1; level <= 16; level++)
            {
                string nm = $"{def.name}_{level}";
                var sp = atlas.GetSprite(nm);
                if (sp != null)
                    arr[level] = sp;
            }
            _fluidLevelSpritesById[def.id] = arr;
        }
    }

    void BuildTileCache()
    {
        _bgTileById.Clear();
        _solidTileByKey.Clear();
        _platformColliderTileByKey.Clear();
        _utilityTileByKey.Clear();
        _fluidTileByKey.Clear();
    }

    // ───────── WorldData helpers ─────────
    public SolidCell MakeSolidCell(ushort id, ushort meta = 0) => new SolidCell { id = id, meta = meta };
    public UtilityCell MakeUtilityCell(ushort id, ushort meta = 0) => new UtilityCell { id = id, meta = meta };
    public FluidCell MakeFluidCell(ushort id, byte amount) => new FluidCell { id = id, amount = amount };

    // ───────── Lookups ─────────
    public string GetSolidName(ushort id) => _solidById.TryGetValue(id, out var def) ? def.name : null;
    public string GetSolidType(ushort id) => _solidById.TryGetValue(id, out var def) ? def.type : "Default";

    public string GetUtilityName(ushort id) => _utilityById.TryGetValue(id, out var def) ? def.name : null;
    public string GetUtilityType(ushort id) => _utilityById.TryGetValue(id, out var def) ? def.type : "Default";

    public string GetFluidName(ushort id) => _fluidById.TryGetValue(id, out var def) ? def.name : null;

    public SolidFlags GetSolidFlags(ushort id) => _solidById.TryGetValue(id, out var def) ? def.flags : SolidFlags.None;
    public bool IsPlatform(ushort id) => _solidById.TryGetValue(id, out var def) && def.isPlatform;

    public bool HasSolidVariant(ushort id, ushort meta)
        => _solidById.TryGetValue(id, out var def) && def.variants != null && def.variants.ContainsKey(meta);

    public bool HasUtilityVariant(ushort id, ushort meta)
        => _utilityById.TryGetValue(id, out var def) && def.variants != null && def.variants.ContainsKey(meta);

    public byte GetSolidBrightness(ushort id) => _solidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;

    public byte GetSolidBrightness(ushort id, ushort meta)
    {
        if (!_solidById.TryGetValue(id, out var def)) return 0;

        if (def.variants != null && def.variants.TryGetValue(meta, out var v))
        {
            if (v.brightnessOverride >= 0) return (byte)v.brightnessOverride;
        }
        return def.brightness;
    }

    public byte GetFluidBrightness(ushort id) => _fluidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;

    public bool TryGetSolidIdByName(string name, out ushort id) => _solidIdByName.TryGetValue(name, out id);
    public bool TryGetUtilityIdByName(string name, out ushort id) => _utilityIdByName.TryGetValue(name, out id);
    public bool TryGetFluidIdByName(string name, out ushort id) => _fluidIdByName.TryGetValue(name, out id);

    public bool GetInteraction(ushort id, out string interaction)
    {
        if (_solidById.TryGetValue(id, out var def) && !string.IsNullOrEmpty(def.interaction))
        {
            interaction = def.interaction;
            return true;
        }
        interaction = null;
        return false;
    }

    public bool GetAttachedAt(ushort id, ushort meta, out string attachedAt)
    {
        if (_solidById.TryGetValue(id, out var def) &&
            def.variants != null &&
            def.variants.TryGetValue(meta, out var v) &&
            !string.IsNullOrEmpty(v.attachedAt))
        {
            attachedAt = v.attachedAt;
            return true;
        }
        attachedAt = null;
        return false;
    }

    // ───────── Sprites ─────────
    public Sprite GetSolidSprite(ushort id, ushort meta) => _solidSpriteByKey.TryGetValue(MakeKey(id, meta), out var sp) ? sp : null;
    public Sprite GetSolidSprite(ushort id) => GetSolidSprite(id, 0);

    public Sprite GetUtilitySprite(ushort id, ushort meta) => _utilitySpriteByKey.TryGetValue(MakeKey(id, meta), out var sp) ? sp : null;
    public Sprite GetUtilitySprite(ushort id) => GetUtilitySprite(id, 0);

    public Sprite GetFluidSprite(ushort id) => _fluidBaseSpriteById.TryGetValue(id, out var sp) ? sp : null;

    public Sprite GetFluidSpriteByAmount(ushort fluidId, byte amount)
    {
        if (fluidId == 0 || amount == 0) return null;

        byte lvl = _amountToLevel[amount];
        if (_fluidLevelSpritesById.TryGetValue(fluidId, out var arr))
        {
            var sp = arr[lvl];
            if (sp != null) return sp;
        }
        return GetFluidSprite(fluidId);
    }

    // ───────── Tiles ─────────
    public TileBase GetBgTile(ushort id)
    {
        if (id == 0) return null;
        if (_bgTileById.TryGetValue(id, out var t)) return t;

        var sp = GetSolidSprite(id, 0);
        if (sp == null) return null;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = Tile.ColliderType.None;

        _bgTileById[id] = tile;
        return tile;
    }

    public TileBase GetSolidTile(ushort id, ushort meta)
    {
        if (id == 0) return null;

        uint key = MakeKey(id, meta);
        if (_solidTileByKey.TryGetValue(key, out var t)) return t;

        var sp = GetSolidSprite(id, meta);
        if (sp == null) return null;

        bool collidable = (_solidById.TryGetValue(id, out var def) && (def.flags & SolidFlags.Collidable) != 0);

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = collidable ? Tile.ColliderType.Sprite : Tile.ColliderType.None;

        _solidTileByKey[key] = tile;
        return tile;
    }

    public TileBase GetPlatformColliderTile(ushort id, ushort meta)
    {
        if (id == 0) return null;

        uint key = MakeKey(id, meta);
        if (_platformColliderTileByKey.TryGetValue(key, out var t)) return t;

        var sp = GetSolidSprite(id, meta);
        if (sp == null) return null;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = Tile.ColliderType.Sprite;

        _platformColliderTileByKey[key] = tile;
        return tile;
    }

    public TileBase GetUtilityTile(ushort id, ushort meta)
    {
        if (id == 0) return null;

        uint key = MakeKey(id, meta);
        if (_utilityTileByKey.TryGetValue(key, out var t)) return t;

        var sp = GetUtilitySprite(id, meta);
        if (sp == null) return null;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = Tile.ColliderType.None;

        _utilityTileByKey[key] = tile;
        return tile;
    }

    public TileBase GetFluidTile(ushort fluidId, byte amount)
    {
        if (fluidId == 0 || amount == 0) return null;

        byte lvl = _amountToLevel[amount];
        if (lvl == 0) return null;

        uint key = MakeFluidLevelKey(fluidId, lvl);
        if (_fluidTileByKey.TryGetValue(key, out var t)) return t;

        Sprite sp = null;
        if (_fluidLevelSpritesById.TryGetValue(fluidId, out var arr))
            sp = arr[lvl];

        if (sp == null)
            sp = GetFluidSprite(fluidId);

        if (sp == null) return null;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = Tile.ColliderType.Sprite;

        _fluidTileByKey[key] = tile;
        return tile;
    }

    public void RebuildSpriteCache()
    {
        BuildSpriteCache();
        BuildTileCache();
    }
}