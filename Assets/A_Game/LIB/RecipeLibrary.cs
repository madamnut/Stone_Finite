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

    // 슬롯 수 → 레시피 배열(JObject)
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
    /// 2슬롯 입력(a,b)으로 레시피 매칭. 성공 시 출력 정보와 actions, 매칭 레시피 반환.
    /// </summary>
    public bool TryMatch2(ItemData a, ItemData b,
                          out string outputId, out int outputCount,
                          out JArray actions, out JObject matched)
    {
        outputId = null; outputCount = 0; actions = null; matched = null;
        if (a == null || b == null) return false;

        var arr = GetRecipes(2);
        if (arr == null || arr.Count == 0) return false;

        for (int i = 0; i < arr.Count; i++)
        {
            var r = (JObject)arr[i];
            var inputs = r["inputs"] as JArray;
            if (inputs == null || inputs.Count != 2) continue;

            bool ordered = r.Value<bool?>("isOrdered") ?? false;

            bool ok = ordered
                ? (MatchInput(a, (JObject)inputs[0]) && MatchInput(b, (JObject)inputs[1]))
                : ((MatchInput(a, (JObject)inputs[0]) && MatchInput(b, (JObject)inputs[1])) ||
                   (MatchInput(a, (JObject)inputs[1]) && MatchInput(b, (JObject)inputs[0])));

            if (!ok) continue;

            matched     = r;
            outputId    = r.Value<string>("output");
            outputCount = r.Value<int?>("outputCount") ?? 1;
            actions     = r["actions"] as JArray; // 입력별 액션(consume, durability 등)
            return true;
        }
        return false;
    }

    // 최소 매칭 로직: itemId 우선, 없으면 attr 키/값 일치(배열이면 포함)
    bool MatchInput(ItemData item, JObject req)
    {
        if (item == null || req == null) return false;

        int need = req.Value<int?>("count") ?? 1;

        var itemId = req.Value<string>("itemId");
        if (!string.IsNullOrEmpty(itemId))
            return item.ItemId == itemId && item.Count >= need;

        var attr = req["attr"] as JObject;
        if (attr != null)
        {
            foreach (var p in attr)
            {
                if (!item.UniqueProps.TryGetValue(p.Key, out var have)) return false;

                var want = p.Value;
                if (want is JArray wantArr)
                {
                    bool ok = false;
                    foreach (var w in wantArr)
                        if (have != null && have.ToString() == w.ToString()) { ok = true; break; }
                    if (!ok) return false;
                }
                else
                {
                    if ((have == null && want.Type != JTokenType.Null) ||
                        (have != null && have.ToString() != want.ToString()))
                        return false;
                }
            }
            return item.Count >= need;
        }

        return false;
    }
}
