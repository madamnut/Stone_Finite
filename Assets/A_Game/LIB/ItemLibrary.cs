using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.U2D;

public class ItemLibrary : MonoBehaviour
{
    [Header("Sprite Atlas (단일 스프라이트용)")]
    public SpriteAtlas itemAtlas;

    public List<TextAsset> jsonFiles = new List<TextAsset>();

    // key: itemId, value: 정의 원본(JObject)
    private Dictionary<string, JObject> allItemDict;

    // 스프라이트 캐시
    private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(128);

    void Awake()
    {
        allItemDict = new Dictionary<string, JObject>();
        foreach (var textAsset in jsonFiles) MergeJson(textAsset);
    }

    void MergeJson(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogWarning("할당되지 않은 JSON 파일이 있습니다.");
            return;
        }

        Dictionary<string, JObject> dict;
        try
        {
            dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, JObject>>(textAsset.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON 파싱 오류 ({textAsset.name}): {ex.Message}");
            return;
        }

        foreach (var kv in dict)
            allItemDict[kv.Key] = kv.Value;
    }

    public Sprite GetSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;

        if (_spriteCache.TryGetValue(spriteName, out var cached))
            return cached;

        // 합성 스프라이트 ("A + B + C")
        if (spriteName.Contains(" + "))
        {
            var parts = spriteName.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
            var sprites = new List<Sprite>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                string key = parts[i].Trim();

                if (_spriteCache.TryGetValue(key, out var partCached))
                {
                    sprites.Add(partCached);
                    continue;
                }

                Sprite s = null;

                // Resources (기본)
                s = Resources.Load<Sprite>(key);
                // ItemParts 폴더
                if (s == null)
                    s = Resources.Load<Sprite>("Textures/ItemParts/" + key);
                // Atlas
                if (s == null && itemAtlas != null)
                    s = itemAtlas.GetSprite(key);

                if (s == null)
                {
                    Debug.LogWarning($"[ItemLibrary] 합성 스프라이트 소스 없음: {key}");
                    continue;
                }

                _spriteCache[key] = s;
                sprites.Add(s);
            }

            if (sprites.Count == 0) return null;

            // 합성 작업
            var baseS = sprites[0];
            int w = (int)baseS.rect.width;
            int h = (int)baseS.rect.height;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels32(new Color32[w * h]);

            var dst = tex.GetPixels32();

            void BlendOver(Color32[] dstBuf, Color32[] srcBuf)
            {
                for (int i = 0; i < dstBuf.Length; i++)
                {
                    float aS = srcBuf[i].a / 255f;
                    if (aS <= 0f) continue;

                    float aD = dstBuf[i].a / 255f;
                    float outA = aS + aD * (1 - aS);

                    float r = (srcBuf[i].r / 255f) * aS + (dstBuf[i].r / 255f) * aD * (1 - aS);
                    float g = (srcBuf[i].g / 255f) * aS + (dstBuf[i].g / 255f) * aD * (1 - aS);
                    float b = (srcBuf[i].b / 255f) * aS + (dstBuf[i].b / 255f) * aD * (1 - aS);

                    if (outA > 0f)
                    {
                        r /= outA;
                        g /= outA;
                        b /= outA;
                    }

                    dstBuf[i] = new Color(r, g, b, outA);
                }
            }

            for (int i = sprites.Count - 1; i >= 0; i--)
            {
                var s = sprites[i];
                var srcTex = s.texture;
                var r = s.rect;

                int sx = (int)r.x;
                int sy = (int)r.y;
                int sw = (int)r.width;
                int sh = (int)r.height;

                var srcColors = srcTex.GetPixels(sx, sy, sw, sh);
                Color32[] srcBuf = Array.ConvertAll(srcColors, c => (Color32)c);

                var lay = new Color32[w * h];
                int offX = (w - sw) / 2;
                int offY = (h - sh) / 2;

                for (int row = 0; row < sh; row++)
                {
                    Array.Copy(srcBuf, row * sw, lay, (offY + row) * w + offX, sw);
                }

                BlendOver(dst, lay);
            }

            tex.SetPixels32(dst);
            tex.Apply();

            var finalSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
            _spriteCache[spriteName] = finalSprite;

            return finalSprite;
        }

        // 단일 스프라이트
        Sprite single = Resources.Load<Sprite>(spriteName);
        if (single == null)
            single = Resources.Load<Sprite>("Textures/ItemParts/" + spriteName);
        if (single == null && itemAtlas != null)
            single = itemAtlas.GetSprite(spriteName);

        if (single != null)
            _spriteCache[spriteName] = single;

        return single;
    }

    public JObject GetItemJson(string itemId)
    {
        if (allItemDict.TryGetValue(itemId, out var obj))
            return obj;

        Debug.LogWarning($"아이템 데이터 없음: {itemId}");
        return null;
    }

    /// <summary>
    /// ATT JSON → ItemData 생성
    /// - breakActions/toolActions/weaponActions:
    ///   • 배열: ["A","B"]                       → { "A": {}, "B": {} }
    ///   • 오브젝트: { "A": {...}, "B": {...} }  → 그대로 Dictionary<string, Dictionary<string,object>>
    ///   • 값 하나(string 등)                    → { value: {} }
    /// - Details:
    ///   • ATT 루트의 "details" 블록만 복사 (toolActions 등은 여기 안 넣음)
    /// </summary>
    public ItemData Create(string itemId, int count = 1)
    {
        var def = GetItemJson(itemId);
        if (def == null) return null;

        // 기본 메타
        string name       = def.Value<string>("name")       ?? itemId;
        string spriteName = def.Value<string>("spriteName") ?? itemId;
        string itemType   = def.Value<string>("itemType")   ?? "Generic";
        int    maxStack   = def.Value<int?>("maxStack")     ?? 1;

        // 내구도
        int maxDurability = def.Value<int?>("maxDurability") ?? 0;
        int durability    = maxDurability;

        // 태그
        var tags = new List<string>();
        if (def["tags"] is JArray tagsArray)
        {
            var list = tagsArray.ToObject<List<string>>();
            if (list != null)
                tags.AddRange(list);
        }

        // 액션 3종: dict<액션이름, 세부파라미터>
        var breakActions  = ReadActionDict(def["breakActions"]);
        var toolActions   = ReadActionDict(def["toolActions"]);
        var weaponActions = ReadActionDict(def["weaponActions"]);

        // Details: ATT 루트의 "details" 블록만 복사
        var details = new Dictionary<string, object>();
        if (def["details"] is JObject detObj)
        {
            var detDict = detObj.ToObject<Dictionary<string, object>>();
            if (detDict != null)
            {
                foreach (var kv in detDict)
                    details[kv.Key] = kv.Value;
            }
        }

        // 아이콘
        var icon = GetSprite(spriteName);

        // 최종 ItemData 생성
        return new ItemData(
            itemId:        itemId,
            name:          name,
            spriteName:    spriteName,
            itemType:      itemType,
            maxStack:      maxStack,
            maxDurability: maxDurability,
            durability:    durability,
            toolActions:   toolActions,
            weaponActions: weaponActions,
            breakActions:  breakActions,
            tags:          tags,
            details:       details,
            icon:          icon,
            count:         count
        );
    }

    /// <summary>
    /// 액션 필드 파싱 헬퍼
    /// 반환형: Dictionary&lt;string, Dictionary&lt;string, object&gt;&gt;
    /// - null → 빈 dict
    /// - JArray ["A","B"] → { "A": {}, "B": {} }
    /// - JObject { "A": {...}, "B": {...} }
    ///   → { "A": (A의 JObject → dict), "B": (B의 JObject → dict) }
    /// - 값 하나(string 등) → { value: {} }
    /// </summary>
    Dictionary<string, Dictionary<string, object>> ReadActionDict(JToken token)
    {
        var dict = new Dictionary<string, Dictionary<string, object>>();

        if (token == null || token.Type == JTokenType.Null)
            return dict;

        // ["A","B"] 형태
        if (token is JArray arr)
        {
            foreach (var t in arr)
            {
                if (t == null || t.Type == JTokenType.Null) continue;
                var name = t.ToString();
                if (string.IsNullOrEmpty(name)) continue;

                if (!dict.ContainsKey(name))
                    dict[name] = new Dictionary<string, object>(); // 파라미터 없음
            }
            return dict;
        }

        // { "A": {...}, "B": {...} } 형태
        if (token is JObject obj)
        {
            foreach (var prop in obj.Properties())
            {
                string name = prop.Name;
                if (string.IsNullOrEmpty(name)) continue;

                Dictionary<string, object> paramDict = null;

                if (prop.Value is JObject paramObj)
                {
                    paramDict = paramObj.ToObject<Dictionary<string, object>>() 
                                ?? new Dictionary<string, object>();
                }
                else
                {
                    // 값이 JObject가 아니면, 그냥 하나의 값으로 감싸서 넣어준다.
                    paramDict = new Dictionary<string, object>
                    {
                        ["value"] = (prop.Value is JValue jv) ? jv.Value : prop.Value?.ToString()
                    };
                }

                dict[name] = paramDict;
            }
            return dict;
        }

        // 단일 값 (string 등)
        var single = token.ToString();
        if (!string.IsNullOrEmpty(single))
        {
            dict[single] = new Dictionary<string, object>();
        }

        return dict;
    }
}
