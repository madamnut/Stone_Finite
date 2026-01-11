using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

/// <summary>
/// Solid / Fluid 정의 JSON + 단일 SpriteAtlas를 받아서
/// - 런타임 조회: id/meta -> sprite, attachedAt, interaction 등 제공
/// - 타일(Tile) 캐싱 제공: BG/Solid/Fluid (+ PlatformCollider 전용)
///
/// JSON 규칙:
/// - 기재되지 않은 속성은 0/false/null 취급
/// - brightness는 0~15 클램프
/// - variants[].brightness_override는 0~15 클램프 (미기재면 미오버라이드)
/// - isPlatform(선택): true/false
///
/// 추가 규칙(사용자 전제):
/// - isPlatform 과 collidable 은 서로 배타적 (둘 다 true 금지)
/// - 둘 다 false 가능
///
/// Sprite 규칙(권장):
/// - Solid variants Sprite 이름 = variants[].sprite
/// - Fluid 단계 스프라이트: "{FluidName}_1" ~ "{FluidName}_16"
///   (예: Water_1 .. Water_16)
///
/// Tile 정책(확정):
/// - BG: 항상 collider 없음 (Tile.colliderType = None)
/// - Solid(FG): collidable=true면 collider Sprite, 아니면 None
/// - PlatformCollider: isPlatform=true면 collider Sprite (렌더는 끄는 타일맵에서 사용)
/// - Fluid: 항상 collider 있음(Trigger는 TilemapCollider2D 인스펙터에서 처리),
///          따라서 Tile.colliderType = Sprite
/// </summary>
[DefaultExecutionOrder(-10000)]
public class CellLibrary : MonoBehaviour
{
    [Header("Solid Json (ATT_Solid.json)")]
    public TextAsset solidJson;

    [Header("Fluid Json (ATT_Fluid.json)")]
    public TextAsset fluidJson;

    [Header("Sprite Atlas (Solid+Fluid)")]
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
        public string spriteName; // variants[].sprite (필수)
        public string attachedAt; // variants[].attachedAt (선택)

        // ✅ optional override
        // -1이면 오버라이드 없음, 0..15면 오버라이드 값
        public sbyte brightnessOverride;

        // key는 가독성용이라 저장/사용 안 함
    }

    struct SolidDef
    {
        public ushort id;
        public byte brightness;
        public SolidFlags flags;

        // ✅ JSON: isPlatform (선택)
        public bool isPlatform;

        public string interaction; // 없으면 null
        public string name;        // JSON key
        public Dictionary<ushort, SolidVariantDef> variants; // meta -> variant (필수라고 가정)
    }

    struct FluidDef
    {
        public ushort id;
        public byte brightness;
        public string name; // JSON key (예: "Water")
    }

    // ───────── 정의 캐시 ─────────
    readonly Dictionary<ushort, SolidDef> _solidById = new Dictionary<ushort, SolidDef>(256);
    readonly Dictionary<ushort, FluidDef> _fluidById = new Dictionary<ushort, FluidDef>(32);

    readonly Dictionary<string, ushort> _solidIdByName = new Dictionary<string, ushort>(256);
    readonly Dictionary<string, ushort> _fluidIdByName = new Dictionary<string, ushort>(32);

    // ───────── 스프라이트 캐시 ─────────
    readonly Dictionary<uint, Sprite> _solidSpriteByKey = new Dictionary<uint, Sprite>(512);

    readonly Dictionary<ushort, Sprite> _fluidBaseSpriteById = new Dictionary<ushort, Sprite>(32);
    readonly Dictionary<ushort, Sprite[]> _fluidLevelSpritesById = new Dictionary<ushort, Sprite[]>(32);

    // ───────── 타일 캐시 ─────────
    // BG는 meta 개념 없음 (bg는 ushort id만 가진다고 가정)
    readonly Dictionary<ushort, Tile> _bgTileById = new Dictionary<ushort, Tile>(256);

    // Solid(FG): (id, meta) -> Tile
    readonly Dictionary<uint, Tile> _solidTileByKey = new Dictionary<uint, Tile>(512);

    // ✅ PlatformCollider: (id, meta) -> Tile (항상 collider Sprite)
    readonly Dictionary<uint, Tile> _platformColliderTileByKey = new Dictionary<uint, Tile>(256);

    // Fluid: (id, level) -> Tile (level 1..16)
    readonly Dictionary<uint, Tile> _fluidTileByKey = new Dictionary<uint, Tile>(256);

    // amount(1..128) -> level(1..16) 맵 (8단위)
    static readonly byte[] _amountToLevel = BuildAmountToLevel();

    static byte[] BuildAmountToLevel()
    {
        var map = new byte[WorldData.MaxFluid + 1]; // index: 0..128
        for (int a = 0; a <= WorldData.MaxFluid; a++)
        {
            if (a <= 0) { map[a] = 0; continue; }
            int lv = (a + 7) / 8; // 1..16
            if (lv < 1) lv = 1;
            if (lv > 16) lv = 16;
            map[a] = (byte)lv;
        }
        return map;
    }

    void Awake()
    {
        BuildSolidCache();
        BuildFluidCache();
        BuildSpriteCache();
        BuildTileCache(); // 스프라이트 캐시 후
    }

    static uint MakeKey(ushort id, ushort meta)
    {
        return ((uint)id << 16) | meta;
    }

    static uint MakeFluidLevelKey(ushort fluidId, byte level)
    {
        return ((uint)fluidId << 16) | level;
    }

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

            int bInt = o["brightness"]?.Value<int>() ?? 0;
            if (bInt < 0) bInt = 0; else if (bInt > 15) bInt = 15;
            byte brightness = (byte)bInt;

            bool collidable = o["collidable"]?.Value<bool>() ?? false;
            bool gravity = o["gravity"]?.Value<bool>() ?? false;

            bool isPlatform = o["isPlatform"]?.Value<bool>() ?? false;

            // ✅ 데이터 검증: 배타 규칙 위반 감지
            if (collidable && isPlatform)
            {
                Debug.LogError($"[CellLibrary] invalid solid def: both collidable and isPlatform are true (name={name}, id={id})");
                // 계속 진행은 하되, 충돌이 나기 쉬우니 여기서 collidable을 무시하는 식의 보정은 하지 않음.
            }

            SolidFlags flags = SolidFlags.None;
            if (collidable) flags |= SolidFlags.Collidable;
            if (gravity) flags |= SolidFlags.HasGravity;

            string interaction = o["interaction"]?.Value<string>(); // 없으면 null

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
                        continue; // sprite는 필수

                    string attachedAt = vObj["attachedAt"]?.Value<string>(); // optional

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
                continue; // Solid 스펙 전제 위반 -> 등록 안 함

            var def = new SolidDef
            {
                id = id,
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

            int bInt = o["brightness"]?.Value<int>() ?? 0; // 미기재면 0
            if (bInt < 0) bInt = 0; else if (bInt > 15) bInt = 15;
            byte brightness = (byte)bInt;

            var def = new FluidDef
            {
                id = id,
                brightness = brightness,
                name = name
            };

            _fluidById[id] = def;

            if (!_fluidIdByName.ContainsKey(name))
                _fluidIdByName.Add(name, id);
        }
    }

    void BuildSpriteCache()
    {
        _solidSpriteByKey.Clear();
        _fluidBaseSpriteById.Clear();
        _fluidLevelSpritesById.Clear();

        if (atlas == null) return;

        // Solid: 모든 variants sprite 캐시
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

        // Fluid base + level sprites
        foreach (var kv in _fluidById)
        {
            var def = kv.Value;

            var baseSp = atlas.GetSprite(def.name);
            if (baseSp != null)
                _fluidBaseSpriteById[def.id] = baseSp;

            var arr = new Sprite[17]; // 0 unused
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
        _fluidTileByKey.Clear();
    }

    // ─────────────────────────────────────────────────────────
    // Public API (WorldData용 생성)
    // ─────────────────────────────────────────────────────────

    public SolidCell MakeSolidCell(ushort id, ushort meta = 0)
    {
        return new SolidCell { id = id, meta = meta };
    }

    public FluidCell MakeFluidCell(ushort id, byte amount)
    {
        return new FluidCell { id = id, amount = amount };
    }

    // ─────────────────────────────────────────────────────────
    // Public API (기본 조회)
    // ─────────────────────────────────────────────────────────

    public string GetSolidName(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.name : null;
    }

    public string GetFluidName(ushort id)
    {
        return _fluidById.TryGetValue(id, out var def) ? def.name : null;
    }

    public SolidFlags GetSolidFlags(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.flags : SolidFlags.None;
    }

    public bool IsPlatform(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) && def.isPlatform;
    }

    public bool HasSolidVariant(ushort id, ushort meta)
    {
        return _solidById.TryGetValue(id, out var def) &&
               def.variants != null &&
               def.variants.ContainsKey(meta);
    }

    public byte GetSolidBrightness(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;
    }

    public byte GetSolidBrightness(ushort id, ushort meta)
    {
        if (!_solidById.TryGetValue(id, out var def))
            return 0;

        if (def.variants != null && def.variants.TryGetValue(meta, out var v))
        {
            if (v.brightnessOverride >= 0)
                return (byte)v.brightnessOverride;
        }

        return def.brightness;
    }

    public byte GetFluidBrightness(ushort id)
    {
        return _fluidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;
    }

    public bool TryGetSolidIdByName(string name, out ushort id)
    {
        return _solidIdByName.TryGetValue(name, out id);
    }

    public bool TryGetFluidIdByName(string name, out ushort id)
    {
        return _fluidIdByName.TryGetValue(name, out id);
    }

    // ─────────────────────────────────────────────────────────
    // Public API (Interaction)
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    // Public API (AttachedAt)
    // ─────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────
    // Public API (Sprite)
    // ─────────────────────────────────────────────────────────

    public Sprite GetSolidSprite(ushort id, ushort meta)
    {
        return _solidSpriteByKey.TryGetValue(MakeKey(id, meta), out var sp) ? sp : null;
    }

    public Sprite GetSolidSprite(ushort id)
    {
        return GetSolidSprite(id, 0);
    }

    public Sprite GetFluidSprite(ushort id)
    {
        return _fluidBaseSpriteById.TryGetValue(id, out var sp) ? sp : null;
    }

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

    // ─────────────────────────────────────────────────────────
    // Public API (Tile)
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// BG 타일: 항상 collider 없음.
    /// BG 스프라이트는 Solid meta=0 기준으로 조회.
    /// </summary>
    public TileBase GetBgTile(ushort id)
    {
        if (id == 0) return null;

        if (_bgTileById.TryGetValue(id, out var t))
            return t;

        var sp = GetSolidSprite(id, 0);
        if (sp == null) return null;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = Tile.ColliderType.None;

        _bgTileById[id] = tile;
        return tile;
    }

    /// <summary>
    /// Solid(FG) 타일: collidable 속성에 따라 collider 결정.
    /// - isPlatform은 여기서 collider에 관여하지 않음 (플랫폼 콜라이더는 별도 타일맵에서 생성)
    /// </summary>
    public TileBase GetSolidTile(ushort id, ushort meta)
    {
        if (id == 0) return null;

        uint key = MakeKey(id, meta);
        if (_solidTileByKey.TryGetValue(key, out var t))
            return t;

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

    /// <summary>
    /// ✅ PlatformCollider 타일:
    /// - isPlatform=true 인 셀의 "콜라이더 전용" 타일
    /// - 반드시 Platform 전용 타일맵(렌더러 OFF)에서만 사용
    /// - colliderType은 항상 Sprite
    /// </summary>
    public TileBase GetPlatformColliderTile(ushort id, ushort meta)
    {
        if (id == 0) return null;

        uint key = MakeKey(id, meta);
        if (_platformColliderTileByKey.TryGetValue(key, out var t))
            return t;

        var sp = GetSolidSprite(id, meta);
        if (sp == null) return null;

        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sp;
        tile.name = sp.name;
        tile.colliderType = Tile.ColliderType.Sprite;

        _platformColliderTileByKey[key] = tile;
        return tile;
    }

    /// <summary>
    /// Fluid 타일: 전역 정책으로 항상 collider 있음(Trigger는 TilemapCollider2D에서).
    /// amount(1..128) → level(1..16)로 캐시(16단계 고정).
    /// </summary>
    public TileBase GetFluidTile(ushort fluidId, byte amount)
    {
        if (fluidId == 0 || amount == 0) return null;

        byte lvl = _amountToLevel[amount];
        if (lvl == 0) return null;

        uint key = MakeFluidLevelKey(fluidId, lvl);
        if (_fluidTileByKey.TryGetValue(key, out var t))
            return t;

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
