using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using UnityEngine;

public class RecipeLibrary : MonoBehaviour
{
    [Header("Recipes JSON by slot-count")]
    public TextAsset recipes2Slots;
    public TextAsset recipes4Slots;
    public TextAsset recipes9Slots;
    public TextAsset recipes16Slots;
    public TextAsset recipes25Slots;
    public TextAsset recipes36Slots;

    private readonly Dictionary<int, JArray> _bySlots = new();

    public JArray GetRecipes(int slotCount)
    {
        return _bySlots.TryGetValue(slotCount, out var arr) ? arr : new JArray();
    }

    void Awake()
    {
        _bySlots.Clear();
        Load(recipes2Slots,  2);
        Load(recipes4Slots,  4);
        Load(recipes9Slots,  9);
        Load(recipes16Slots, 16);
        Load(recipes25Slots, 25);
        Load(recipes36Slots, 36);
    }

    void Load(TextAsset json, int slots)
    {
        if (json == null) return;
        try
        {
            var arr = JArray.Parse(json.text);
            _bySlots[slots] = arr;
        }
        catch (System.SystemException ex)
        {
            Debug.LogError($"[RecipeLibrary] {slots}슬롯 JSON 파싱 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 2슬롯 매칭. actions는 입력 슬롯 순서(input0,input1)에 정렬돼 반환.
    /// </summary>
    public bool TryMatch2(ItemData a, ItemData b,
                          out string outputId, out int outputCount,
                          out JArray actions, out JArray outputActions, out JObject matched)
    {
        outputId = null; outputCount = 0; actions = null; outputActions = null; matched = null;
        if (a == null || b == null) return false;

        var arr = GetRecipes(2);
        if (arr == null || arr.Count == 0) return false;

        for (int i = 0; i < arr.Count; i++)
        {
            var r = (JObject)arr[i];
            var inputs = r["inputs"] as JArray;
            if (inputs == null || inputs.Count != 2) continue;

            bool ordered = r.Value<bool?>("isOrdered") ?? false;

            bool ab = MatchInput(a, (JObject)inputs[0]) && MatchInput(b, (JObject)inputs[1]);
            bool ba = !ordered && MatchInput(a, (JObject)inputs[1]) && MatchInput(b, (JObject)inputs[0]);
            if (!ab && !ba) continue;

            matched     = r;
            outputId    = r.Value<string>("output");
            outputCount = r.Value<int?>("outputCount") ?? 1;

            var acts = r["actions"] as JArray;
            if (acts != null && acts.Count == 2 && ba)
            {
                var re = new JArray();
                re.Add(acts[1]); // input0 ← inputs[1]
                re.Add(acts[0]); // input1 ← inputs[0]
                actions = re;
            }
            else
            {
                actions = acts;
            }

            outputActions = r["outputActions"] as JArray;
            return true;
        }
        return false;
    }

    // itemId 또는 unique 조건 단일 판정
    bool MatchInput(ItemData item, JObject req)
    {
        if (item == null || req == null) return false;

        int need = req.Value<int?>("count") ?? 1;

        var itemId = req.Value<string>("itemId");
        if (!string.IsNullOrEmpty(itemId))
            return item.ItemId == itemId && item.Count >= need;

        var unique = req["unique"] as JObject;
        if (unique != null)
        {
            foreach (var p in unique)
            {
                if (!item.Unique.TryGetValue(p.Key, out var have)) return false;

                var want = p.Value;
                if (want is JArray wantArr)
                {
                    bool ok = false;
                    if (have is IEnumerable en && !(have is string))
                    {
                        foreach (var hv in en)
                        {
                            foreach (var w in wantArr)
                            {
                                if (hv != null && hv.ToString() == w.ToString()) { ok = true; break; }
                            }
                            if (ok) break;
                        }
                    }
                    else
                    {
                        foreach (var w in wantArr)
                        {
                            if (have != null && have.ToString() == w.ToString()) { ok = true; break; }
                        }
                    }
                    if (!ok) return false;
                }
                else
                {
                    if (have is IEnumerable en && !(have is string))
                    {
                        bool ok = false;
                        foreach (var hv in en)
                        {
                            if (hv != null && hv.ToString() == want.ToString()) { ok = true; break; }
                        }
                        if (!ok) return false;
                    }
                    else
                    {
                        if ((have == null && want.Type != JTokenType.Null) ||
                            (have != null && have.ToString() != want.ToString()))
                            return false;
                    }
                }
            }
            return item.Count >= need;
        }

        return false;
    }
}
