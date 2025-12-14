using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// Solid / Liquid 정의 JSON + 단일 SpriteAtlas를 받아서
/// - 배치 시점: id -> SolidCell / LiquidCell (정의값 그대로)
/// - 런타임 조회: id -> name / interaction(string) / sprite 등 제공
///
/// JSON 규칙:
/// - 기재되지 않은 속성은 0/false/null 취급
/// - brightness는 0~15 클램프
///
/// Sprite 규칙(권장):
/// - Atlas 안 Sprite 이름 = JSON의 key(name)
/// - Liquid 단계 스프라이트: "{LiquidName}_1" ~ "{LiquidName}_16"
///   (예: Water_1 .. Water_16)
/// </summary>

[DefaultExecutionOrder(-10000)]
public class CellLibrary : MonoBehaviour
{
    [Header("Solid Json (ATT_Solid.json)")]
    public TextAsset solidJson;

    [Header("Liquid Json (ATT_Liquid.json)")]
    public TextAsset liquidJson;

    [Header("Sprite Atlas (Solid+Liquid)")]
    public SpriteAtlas atlas;

    struct SolidDef
    {
        public ushort id;
        public byte brightness;
        public SolidFlags flags;
        public string interaction; // 그대로(없으면 null)
        public string name;        // JSON key
    }

    struct LiquidDef
    {
        public ushort id;
        public byte brightness;
        public string name; // JSON key (예: "Water")
    }

    readonly Dictionary<ushort, SolidDef>  _solidById      = new Dictionary<ushort, SolidDef>(256);
    readonly Dictionary<ushort, LiquidDef> _liquidById     = new Dictionary<ushort, LiquidDef>(32);

    readonly Dictionary<string, ushort> _solidIdByName  = new Dictionary<string, ushort>(256);
    readonly Dictionary<string, ushort> _liquidIdByName = new Dictionary<string, ushort>(32);

    readonly Dictionary<ushort, Sprite> _solidSpriteById  = new Dictionary<ushort, Sprite>(256);

    // Liquid는 2종류 캐시:
    // 1) 베이스(디버그/대표용): id -> Sprite (LiquidName)
    // 2) 양 기반 렌더링용:     id -> Sprite[17] (index 1..16), "{LiquidName}_{level}"
    readonly Dictionary<ushort, Sprite>   _liquidBaseSpriteById  = new Dictionary<ushort, Sprite>(32);
    readonly Dictionary<ushort, Sprite[]> _liquidLevelSpritesById = new Dictionary<ushort, Sprite[]>(32);

    // amount(1..128) -> level(1..16) 맵 (8단위)
    static readonly byte[] _amountToLevel = BuildAmountToLevel();

    static byte[] BuildAmountToLevel()
    {
        // index: 0..128, value: 0..16 (0은 비어있음)
        var map = new byte[WorldData.MaxFluid + 1];
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
        BuildLiquidCache();
        BuildSpriteCache();
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
            bool gravity    = o["gravity"]?.Value<bool>() ?? false;

            SolidFlags flags = SolidFlags.None;
            if (collidable) flags |= SolidFlags.Collidable;
            if (gravity)    flags |= SolidFlags.HasGravity;

            if (o.TryGetValue("depend", out JToken depTok) && depTok is JArray deps)
            {
                for (int i = 0; i < deps.Count; i++)
                {
                    string d = deps[i]?.Value<string>();
                    if (string.IsNullOrEmpty(d)) continue;

                    switch (d)
                    {
                        case "background": flags |= SolidFlags.DepBackground; break;
                        case "up":         flags |= SolidFlags.DepUp; break;
                        case "down":       flags |= SolidFlags.DepDown; break;
                        case "left":       flags |= SolidFlags.DepLeft; break;
                        case "right":      flags |= SolidFlags.DepRight; break;
                    }
                }
            }

            string interaction = o["interaction"]?.Value<string>(); // 없으면 null

            var def = new SolidDef
            {
                id = id,
                brightness = brightness,
                flags = flags,
                interaction = interaction,
                name = name
            };

            _solidById[id] = def;
            if (!_solidIdByName.ContainsKey(name))
                _solidIdByName.Add(name, id);
        }
    }

    void BuildLiquidCache()
    {
        _liquidById.Clear();
        _liquidIdByName.Clear();

        if (liquidJson == null || string.IsNullOrEmpty(liquidJson.text))
            return;

        var root = JObject.Parse(liquidJson.text);

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

            var def = new LiquidDef
            {
                id = id,
                brightness = brightness,
                name = name
            };

            _liquidById[id] = def;
            if (!_liquidIdByName.ContainsKey(name))
                _liquidIdByName.Add(name, id);
        }
    }

    void BuildSpriteCache()
    {
        _solidSpriteById.Clear();
        _liquidBaseSpriteById.Clear();
        _liquidLevelSpritesById.Clear();

        if (atlas == null) return;

        foreach (var kv in _solidById)
        {
            var def = kv.Value;
            var sp = atlas.GetSprite(def.name);
            if (sp != null)
                _solidSpriteById[def.id] = sp;
        }

        foreach (var kv in _liquidById)
        {
            var def = kv.Value;

            // 베이스(있으면 넣고, 없어도 무방)
            var baseSp = atlas.GetSprite(def.name);
            if (baseSp != null)
                _liquidBaseSpriteById[def.id] = baseSp;

            // 1..16 단계 스프라이트
            var arr = new Sprite[17]; // 0 unused
            for (int level = 1; level <= 16; level++)
            {
                string nm = $"{def.name}_{level}";
                var sp = atlas.GetSprite(nm);
                if (sp != null)
                    arr[level] = sp;
            }
            _liquidLevelSpritesById[def.id] = arr;
        }
    }

    // ─────────────────────────────────────────────────────────
    // Public API (월드데이터 변환)
    // ─────────────────────────────────────────────────────────

    public SolidCell MakeSolidCell(ushort id)
    {
        if (!_solidById.TryGetValue(id, out var def))
        {
            return new SolidCell
            {
                id = id,
                brightness = 0,
                flags = SolidFlags.None
            };
        }

        return new SolidCell
        {
            id = def.id,
            brightness = def.brightness,
            flags = def.flags
        };
    }

    public LiquidCell MakeLiquidCell(ushort id)
    {
        return MakeLiquidCell(id, WorldData.MaxFluid);
    }

    public LiquidCell MakeLiquidCell(ushort id, byte amount)
    {
        if (!_liquidById.TryGetValue(id, out var def))
        {
            return new LiquidCell
            {
                id = id,
                amount = amount,
                brightness = 0
            };
        }

        return new LiquidCell
        {
            id = def.id,
            amount = amount,
            brightness = def.brightness
        };
    }

    // ─────────────────────────────────────────────────────────
    // Public API (런타임 조회)
    // ─────────────────────────────────────────────────────────

    public string GetSolidName(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.name : null;
    }

    public string GetLiquidName(ushort id)
    {
        return _liquidById.TryGetValue(id, out var def) ? def.name : null;
    }

    public string GetSolidInteraction(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.interaction : null;
    }

    public SolidFlags GetSolidFlags(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.flags : SolidFlags.None;
    }

    public byte GetSolidBrightness(ushort id)
    {
        return _solidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;
    }

    public byte GetLiquidBrightness(ushort id)
    {
        return _liquidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;
    }

    public bool TryGetSolidIdByName(string name, out ushort id)
    {
        return _solidIdByName.TryGetValue(name, out id);
    }

    public bool TryGetLiquidIdByName(string name, out ushort id)
    {
        return _liquidIdByName.TryGetValue(name, out id);
    }

    // ─────────────────────────────────────────────────────────
    // Public API (Sprite)
    // ─────────────────────────────────────────────────────────

    public Sprite GetSolidSprite(ushort id)
    {
        return _solidSpriteById.TryGetValue(id, out var sp) ? sp : null;
    }

    /// <summary>
    /// 액체 "베이스" 스프라이트.
    /// - atlas에 LiquidName(예: "Water")가 있으면 반환
    /// - 없으면 null 가능
    /// </summary>
    public Sprite GetLiquidSprite(ushort id)
    {
        return _liquidBaseSpriteById.TryGetValue(id, out var sp) ? sp : null;
    }

    /// <summary>
    /// amount(1..128)에 따라 "{LiquidName}_1..16"에서 선택.
    /// - 내부 레벨: (amount + 7) / 8  => 1..16
    /// - 단계 스프라이트가 없으면, 베이스가 있으면 베이스로 폴백
    /// </summary>
    public Sprite GetLiquidSpriteByAmount(ushort liquidId, byte amount)
    {
        if (liquidId == 0 || amount == 0) return null;

        byte lvl = _amountToLevel[amount];

        if (_liquidLevelSpritesById.TryGetValue(liquidId, out var arr))
        {
            var sp = arr[lvl];
            if (sp != null) return sp;
        }

        // 폴백: base sprite
        return GetLiquidSprite(liquidId);
    }

    public void RebuildSpriteCache()
    {
        BuildSpriteCache();
    }
}
