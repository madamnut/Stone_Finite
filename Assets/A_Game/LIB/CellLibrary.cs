using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Newtonsoft.Json.Linq;

[DefaultExecutionOrder(-100)]
public class CellLibrary : MonoBehaviour
{
    [Header("스프라이트 아틀라스")]
    [SerializeField] private SpriteAtlas atlas;

    [Header("ATT_Cell.json (딕셔너리 구조)")]
    [SerializeField] private TextAsset cellJson;

    public struct CellDef
    {
        public string  name;
        public Sprite  sprite;
        public ushort  id;

        public FgFlags flags;       // HasGravity / Collidable / Dep* 등
        public byte    brightness;  // 0~15
        public string  interaction; // 상호작용 태그
    }

    private static readonly Dictionary<ushort, CellDef> byId    = new();
    private static readonly Dictionary<ushort, string>  idToKey = new();

    void Awake()
    {
        byId.Clear();
        idToKey.Clear();

        var root = JObject.Parse(cellJson.text);

        foreach (var pair in root)
        {
            string key = pair.Key;
            var obj    = (JObject)pair.Value;

            ushort id = (ushort)(obj["id"]?.Value<int>() ?? 0);

            // ───────────────── Flag 조립 ─────────────────
            FgFlags flags = FgFlags.None;

            // gravity
            bool gravity = obj["gravity"]?.Value<bool>() ?? false;
            if (gravity)
                flags |= FgFlags.HasGravity;

            // collidable
            bool collidable = obj["collidable"]?.Value<bool>() ?? false;
            if (collidable)
                flags |= FgFlags.Collidable;

            // depend
            if (obj["depend"] is JArray deps)
            {
                foreach (var t in deps)
                {
                    string dep = (t.Value<string>() ?? "").ToLowerInvariant();
                    switch (dep)
                    {
                        case "background": flags |= FgFlags.DepBackground; break;
                        case "up":         flags |= FgFlags.DepUp;         break;
                        case "down":       flags |= FgFlags.DepDown;       break;
                        case "left":       flags |= FgFlags.DepLeft;       break;
                        case "right":      flags |= FgFlags.DepRight;      break;
                    }
                }
            }

            // interaction
            string interaction = obj["interaction"]?.Value<string>();

            // brightness (0~15)
            byte brightness = 0;
            if (obj["brightness"] != null)
            {
                int raw = obj["brightness"]!.Value<int>();
                brightness = (byte)Mathf.Clamp(raw, 0, 15);
            }

            // sprite
            Sprite sp = atlas.GetSprite(key);
            if (sp == null)
            {
                Debug.LogWarning($"CellLibrary: 스프라이트 '{key}' 없음 (ID {id})", this);
            }

            // 중복 ID 검사
            if (byId.ContainsKey(id))
            {
                Debug.LogWarning($"CellLibrary: 중복 ID {id} ('{key}') 무시", this);
                continue;
            }

            // 등록
            var def = new CellDef
            {
                name        = key,
                sprite      = sp,
                id          = id,
                flags       = flags,
                brightness  = brightness,
                interaction = interaction
            };

            byId.Add(id, def);
            idToKey[id] = key;
        }
    }

    // ───────────────────── 기본 쿼리 ─────────────────────

    public static FgFlags FlagsOf(ushort id)
        => byId.TryGetValue(id, out var d) ? d.flags : FgFlags.None;

    public static Sprite GetSprite(ushort id)
        => byId.TryGetValue(id, out var d) ? d.sprite : null;

    public static string GetName(ushort id)
        => byId.TryGetValue(id, out var d) ? d.name : $"Unknown_{id}";

    public static string GetKey(ushort id)
        => idToKey.TryGetValue(id, out var k) ? k : null;

    public static string InteractionOf(ushort id)
        => byId.TryGetValue(id, out var d) ? d.interaction : null;

    public static byte BrightnessOf(ushort id)
        => byId.TryGetValue(id, out var d) ? d.brightness : (byte)0;


    // ───────────────────── FgCell 생성 ─────────────────────

    public static FgCell MakeFgCell(ushort id)
    {
        if (!byId.TryGetValue(id, out var d))
        {
            // 정의 없는 경우 → 완전 빈 셀 반환
            return new FgCell
            {
                id          = 0,
                fluidId     = 0,
                fluidAmount = 0,
                brightness  = 0,
                flags       = FgFlags.None
            };
        }

        return new FgCell
        {
            id          = id,
            fluidId     = 0,
            fluidAmount = 0,
            brightness  = d.brightness,
            flags       = d.flags
        };
    }
}
