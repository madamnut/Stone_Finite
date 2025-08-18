using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Newtonsoft.Json.Linq; // Newtonsoft JSON 패키지 필요

// ┌──────────────────────────────────────────────────────┐
// │ BlockLibrary                                         │
// │ • JSON: 스프라이트명을 키로 하는 딕셔너리 구조       │
// │   예)                                                 │
// │   {                                                  │
// │     "air"  : { "id":0, "collider":false, "liquid":false },
// │     "sand" : { "id":2, "collider":true,  "liquid":false, "gravity":true },
// │     "vine" : { "id":3, "collider":false, "liquid":false, "dependent":true }
// │   }                                                  │
// │ • 추가 속성                                          │
// │   gravity   → 중력 영향 (아래로 낙하)                 │
// │   dependent → 아래 블록이 파괴되면 같이 파괴          │
// └──────────────────────────────────────────────────────┘
public class BlockLibrary : MonoBehaviour
{
    // ▣ 인스펙터 ▣
    [Header("스프라이트 아틀라스")]
    [SerializeField] private SpriteAtlas atlas;

    [Header("ATT_Block.json (딕셔너리 형식)")]
    [SerializeField] private TextAsset blockJson;

    // ▣ 내부 데이터 구조 ▣
    public struct BlockData
    {
        public string name;     // 스프라이트 키 (표기용)
        public Sprite sprite;   // 텍스처
        public bool collider;   // 충돌 여부
        public bool liquid;     // 액체 여부
        public bool gravity;    // 중력 적용 (모래 등)
        public bool dependent;  // 아래 블록이 사라지면 함께 파괴 (덩굴·횃불)
    }

    private static readonly Dictionary<ushort, BlockData> byId = new();

    // ──────────────────────────────────────
    private void Awake()
    {
        if (atlas == null || blockJson == null)
        {
            Debug.LogError("BlockLibrary: 아틀라스나 JSON이 지정되지 않았습니다.", this);
            return;
        }

        byId.Clear();

        JObject root = JObject.Parse(blockJson.text);
        foreach (var pair in root)
        {
            string spriteKey = pair.Key;          // ex) "rock"
            JObject obj      = (JObject)pair.Value;

            ushort id      = (ushort)(obj["id"       ]?.Value<int>()  ?? 0);
            bool collider  =          obj["collider" ]?.Value<bool>() ?? false;
            bool liquid    =          obj["liquid"   ]?.Value<bool>() ?? false;
            bool gravity   =          obj["gravity"  ]?.Value<bool>() ?? false;
            bool dependent =          obj["dependent"]?.Value<bool>() ?? false;

            Sprite sp = atlas.GetSprite(spriteKey);
            if (sp == null)
                Debug.LogWarning($"BlockLibrary: 스프라이트 '{spriteKey}' (ID {id})를 아틀라스에서 찾을 수 없습니다.", this);

            if (byId.ContainsKey(id))
            {
                Debug.LogWarning($"BlockLibrary: 중복 ID {id} 감지 — '{spriteKey}' 무시됨.", this);
                continue;
            }

            byId.Add(id, new BlockData
            {
                name      = spriteKey,
                sprite    = sp,
                collider  = collider,
                liquid    = liquid,
                gravity   = gravity,
                dependent = dependent
            });
        }
    }

    // ──────────────────────────────────────
    // 정적 조회 함수
    public static Sprite GetSprite   (ushort id) => byId.TryGetValue(id, out var d) ? d.sprite   : null;
    public static bool   HasCollider (ushort id) => byId.TryGetValue(id, out var d) && d.collider;
    public static bool   IsLiquid    (ushort id) => byId.TryGetValue(id, out var d) && d.liquid;
    public static bool   HasGravity  (ushort id) => byId.TryGetValue(id, out var d) && d.gravity;
    public static bool   IsDependent (ushort id) => byId.TryGetValue(id, out var d) && d.dependent;
    public static string GetName     (ushort id) => byId.TryGetValue(id, out var d) ? d.name     : $"Unknown_{id}";
}
