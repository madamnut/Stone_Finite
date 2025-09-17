// ItemLibrary.cs
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

    // 합성/단일 공통 캐시
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

        foreach (var kv in dict) allItemDict[kv.Key] = kv.Value;
    }

    /// <summary>
    /// 스프라이트 획득.
    /// - "A + B + C" 형태면 Resources/Atlas에서 각 토큰을 불러와 합성.
    ///   C가 맨 밑, B가 중간, A가 최상단이 되도록 역순 합성.
    ///   합성 결과 텍스처의 FilterMode는 Point(없음)로 설정.
    /// - 아니면 Atlas/Resources에서 단일 스프라이트 조회.
    /// - JSON은 기존처럼 "Module_a" 등 짧은 키를 써도 됨. 자동으로 "Textures/ItemParts/" 경로도 탐색.
    /// </summary>
    public Sprite GetSprite(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;

        if (_spriteCache.TryGetValue(spriteName, out var cached)) return cached;

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

                // 1) Resources: 원래 키
                var s = Resources.Load<Sprite>(key);
                // 2) Resources: ItemParts 경로 자동 보정
                if (s == null)
                    s = Resources.Load<Sprite>("Textures/ItemParts/" + key);
                // 3) Atlas 폴백
                if (s == null && itemAtlas != null)
                    s = itemAtlas.GetSprite(key);

                if (s == null)
                {
                    Debug.LogWarning($"[ItemLibrary] 합성 소스 스프라이트를 찾을 수 없음: {key}");
                    continue;
                }

                // 캐시는 원래 키로 저장
                _spriteCache[key] = s;
                sprites.Add(s);
            }

            if (sprites.Count == 0) return null;

            var baseS = sprites[0];
            var rect  = baseS.rect;
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point; // ← 합성 결과 필터 없음(포인트)
            tex.SetPixels32(new Color32[w * h]); // 투명 초기화

            void BlendOver(Color32[] dst, Color32[] src)
            {
                for (int i = 0; i < dst.Length; i++)
                {
                    float aS = src[i].a / 255f; if (aS <= 0f) continue;
                    float aD = dst[i].a / 255f;
                    float outA = aS + aD * (1 - aS);

                    float r = (src[i].r / 255f) * aS + (dst[i].r / 255f) * aD * (1 - aS);
                    float g = (src[i].g / 255f) * aS + (dst[i].g / 255f) * aD * (1 - aS);
                    float b = (src[i].b / 255f) * aS + (dst[i].b / 255f) * aD * (1 - aS);

                    if (outA > 0f) { r /= outA; g /= outA; b /= outA; }

                    dst[i] = new Color(r, g, b, outA);
                }
            }

            var dstBuf = tex.GetPixels32();

            // 역순 합성: 마지막 파트가 밑, 첫 파트가 최상단
            for (int idx = sprites.Count - 1; idx >= 0; idx--)
            {
                var s = sprites[idx];
                var srcTex = s.texture;
                var r = s.rect;
                int sx = Mathf.RoundToInt(r.x);
                int sy = Mathf.RoundToInt(r.y);
                int sw = Mathf.RoundToInt(r.width);
                int sh = Mathf.RoundToInt(r.height);

                int cw = Mathf.Min(sw, w);
                int ch = Mathf.Min(sh, h);
                int offX = (w - cw) / 2;
                int offY = (h - ch) / 2;

                // 구버전 호환: GetPixels(x,y,w,h)
                var srcColors = srcTex.GetPixels(sx + (sw - cw) / 2, sy + (sh - ch) / 2, cw, ch);
                var src = new Color32[srcColors.Length];
                for (int j = 0; j < srcColors.Length; j++) src[j] = srcColors[j];

                var lay = new Color32[w * h];
                for (int row = 0; row < ch; row++)
                    Array.Copy(src, row * cw, lay, (offY + row) * w + offX, cw);

                BlendOver(dstBuf, lay);
            }

            tex.SetPixels32(dstBuf);
            tex.Apply(false, false);

            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), baseS.pixelsPerUnit);
            sprite.name = $"Composite[{spriteName}]";
            _spriteCache[spriteName] = sprite;
            return sprite;
        }

        // 단일 스프라이트
        Sprite single = Resources.Load<Sprite>(spriteName);
        if (single == null)
            single = Resources.Load<Sprite>("Textures/ItemParts/" + spriteName);
        if (single == null)
        {
            if (itemAtlas == null)
            {
                Debug.LogError("SpriteAtlas가 할당되지 않았습니다.");
                return null;
            }
            single = itemAtlas.GetSprite(spriteName);
        }

        if (single != null) _spriteCache[spriteName] = single;
        return single;
    }

    public JObject GetItemJson(string itemId)
    {
        if (allItemDict.TryGetValue(itemId, out var obj)) return obj;
        Debug.LogWarning($"아이템 데이터가 존재하지 않습니다: {itemId}");
        return null;
    }

    public ItemData Create(string itemId, int count = 1)
    {
        var def = GetItemJson(itemId);
        if (def == null) return null;

        string name       = def.Value<string>("name")       ?? itemId;
        string spriteName = def.Value<string>("spriteName") ?? itemId;
        string itemType   = def.Value<string>("itemType")   ?? "Generic";
        int    maxStack   = def.Value<int?>("maxStack")     ?? 1;

        var props = new Dictionary<string, object>();
        if (def["unique"] is JObject unique)
        {
            foreach (var kv in unique)
                props[kv.Key] = kv.Value.ToObject<object>();
        }

        var icon = GetSprite(spriteName);

        return new ItemData(
            itemId:     itemId,
            name:       name,
            spriteName: spriteName,
            itemType:   itemType,
            maxStack:   maxStack,
            unique:     props,
            icon:       icon,
            count:      count
        );
    }
}
