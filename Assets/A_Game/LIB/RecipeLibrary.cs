// RecipeLibrary.cs
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class RecipeLibrary : MonoBehaviour
{
    [Header("Deps")]
    public ItemLibrary itemLibrary;

    [Header("Recipe Jsons")]
    public TextAsset recipe2Json; // 2-slot
    public TextAsset recipe4Json; // 4-slot

    JArray _r2;
    JArray _r4;

    void Awake()
    {
        if (recipe2Json != null && !string.IsNullOrEmpty(recipe2Json.text))
            _r2 = JArray.Parse(recipe2Json.text);
        if (recipe4Json != null && !string.IsNullOrEmpty(recipe4Json.text))
            _r4 = JArray.Parse(recipe4Json.text);
    }

    /// 슬롯 스냅샷 그대로 입력. 결과아이템(출력액션 적용완료) + 슬롯별 인풋액션 반환.
    public bool TryCraft(
        List<ItemData> slots,
        out ItemData resultItem,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItem = null;
        remappedInputActions = null;
        matchedRecipe = null;
        if (itemLibrary == null || slots == null) return false;

        int n = slots.Count;

        if (n == 4)
        {
            if (_r4 != null && TryMatchSet(_r4, slots, fourContext: true, out resultItem, out remappedInputActions, out matchedRecipe))
                return true;

            if (_r2 != null && TryMatch2InFourContext(_r2, slots, out resultItem, out remappedInputActions, out matchedRecipe))
                return true;

            if (_r4 != null && TryMatch2InFourContext(FilterByInputCount(_r4, 2), slots, out resultItem, out remappedInputActions, out matchedRecipe))
                return true;

            return false;
        }

        if (n == 2)
        {
            if (_r2 != null && TryMatchSet(_r2, slots, fourContext: false, out resultItem, out remappedInputActions, out matchedRecipe))
                return true;

            if (_r4 != null && TryMatchSet(FilterByInputCount(_r4, 2), slots, fourContext: false, out resultItem, out remappedInputActions, out matchedRecipe))
                return true;

            return false;
        }

        return false;
    }

    // 세트 매칭(슬롯 수/인덱스 그대로, null 슬롯 유지)
    bool TryMatchSet(
        JArray recipeSet,
        List<ItemData> slots,
        bool fourContext,
        out ItemData resultItem,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItem = null;
        remappedInputActions = null;
        matchedRecipe = null;

        var presentIdx = new List<int>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null) presentIdx.Add(i);

        for (int rix = 0; rix < recipeSet.Count; rix++)
        {
            var r = recipeSet[rix] as JObject; if (r == null) continue;

            var inputs = r["inputs"] as JArray;
            if (inputs == null || inputs.Count == 0) continue;

            bool isOrdered = r.Value<bool?>("isOrdered") ?? false;
            string outId   = r.Value<string>("output");
            int outCount   = r.Value<int?>("outputCount") ?? 1;
            var inActs     = r["inputActions"] as JArray;
            var outActs    = r["outputActions"] as JArray;

            if (string.IsNullOrEmpty(outId) || outCount <= 0) continue;
            if (presentIdx.Count < inputs.Count) continue;

            int[] assign = null;

            if (isOrdered)
            {
                if (slots.Count == inputs.Count)
                {
                    if (!MatchOrderedAll(inputs, slots)) continue;
                    assign = Enumerable.Range(0, inputs.Count).ToArray();
                }
                else if (fourContext && inputs.Count == 2 && slots.Count == 4)
                {
                    if (!TryOrderedWindow(inputs, slots, new[] { 0, 1 }, out assign) &&
                        !TryOrderedWindow(inputs, slots, new[] { 2, 3 }, out assign))
                        continue;
                }
                else
                {
                    continue;
                }
            }
            else
            {
                if (fourContext && inputs.Count == 2 && slots.Count == 4)
                {
                    if (presentIdx.Count == 2)
                    {
                        assign = TryUnordered(inputs, slots, presentIdx);
                        if (assign == null) continue;
                    }
                    else
                    {
                        if (!TryUnorderedInAllowed(inputs, slots, new[] { 0, 1 }, out assign) &&
                            !TryUnorderedInAllowed(inputs, slots, new[] { 2, 3 }, out assign))
                            continue;
                    }
                }
                else
                {
                    assign = TryUnordered(inputs, slots, presentIdx);
                    if (assign == null) continue;
                }
            }

            // 입력액션 리맵
            remappedInputActions = new JArray();
            for (int i = 0; i < slots.Count; i++) remappedInputActions.Add(null);
            if (inActs != null)
            {
                for (int k = 0; k < inputs.Count; k++)
                {
                    int si = assign[k];
                    if (si >= 0 && si < slots.Count) remappedInputActions[si] = inActs[k];
                }
            }

            // 결과 생성 + 출력액션 적용
            var baseItem = itemLibrary.Create(outId, outCount);
            resultItem = ApplyOutputActions(baseItem, outActs, slots, assign);

            if (resultItem != null) { matchedRecipe = r; return true; }
        }

        return false;
    }

    // 4슬롯 컨텍스트에서 2슬롯 레시피를 규칙대로 시도
    bool TryMatch2InFourContext(
        JArray recipeSet2,
        List<ItemData> slots,
        out ItemData resultItem,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItem = null; remappedInputActions = null; matchedRecipe = null;
        if (recipeSet2 == null || slots.Count != 4) return false;

        var only2 = FilterByInputCount(recipeSet2, 2);
        return TryMatchSet(only2, slots, fourContext: true, out resultItem, out remappedInputActions, out matchedRecipe);
    }

    // inputs.Count==cnt 필터
    JArray FilterByInputCount(JArray src, int cnt)
    {
        var arr = new JArray();
        for (int i = 0; i < src.Count; i++)
        {
            var r = src[i] as JObject;
            if (r == null) continue;
            var ins = r["inputs"] as JArray;
            if (ins != null && ins.Count == cnt) arr.Add(r);
        }
        return arr;
    }

    // 정형: 인덱스 일치 전체 검사
    bool MatchOrderedAll(JArray inputs, List<ItemData> slots)
    {
        if (inputs.Count > slots.Count) return false;
        for (int i = 0; i < inputs.Count; i++)
        {
            var spec = inputs[i] as JObject;
            var it   = slots[i];
            if (!MatchSpec(it, spec)) return false;

            int need = spec?.Value<int?>("count") ?? 1;
            if (it == null || it.Count < need) return false;
        }
        return true;
    }

    // 정형: 주어진 윈도우 검사
    bool TryOrderedWindow(JArray inputs, List<ItemData> slots, int[] win, out int[] assign)
    {
        assign = null;
        if (inputs.Count != win.Length) return false;

        for (int k = 0; k < win.Length; k++)
        {
            int si = win[k];
            var it = slots[si];
            var spec = inputs[k] as JObject;
            if (!MatchSpec(it, spec)) return false;

            int need = spec?.Value<int?>("count") ?? 1;
            if (it == null || it.Count < need) return false;
        }

        assign = (int[])win.Clone();
        return true;
    }

    // 무형: presentIdx 내에서 백트래킹
    int[] TryUnordered(JArray inputs, List<ItemData> slots, List<int> presentIdx)
    {
        var used = new HashSet<int>();
        var res  = new int[inputs.Count];

        bool Dfs(int idx)
        {
            if (idx >= inputs.Count) return true;
            var spec = inputs[idx] as JObject;

            foreach (var si in presentIdx)
            {
                if (used.Contains(si)) continue;
                var it = slots[si];
                if (!MatchSpec(it, spec)) continue;

                int need = spec?.Value<int?>("count") ?? 1;
                if (it == null || it.Count < need) continue;

                used.Add(si);
                res[idx] = si;
                if (Dfs(idx + 1)) return true;
                used.Remove(si);
            }
            return false;
        }

        return Dfs(0) ? res : null;
    }

    // 무형: 허용된 윈도우 내에서만 무순서 매칭
    bool TryUnorderedInAllowed(JArray inputs, List<ItemData> slots, int[] allowed, out int[] assign)
    {
        assign = null;
        var present = allowed.Where(i => i >= 0 && i < slots.Count && slots[i] != null).ToList();
        var res = TryUnordered(inputs, slots, present);
        if (res == null) return false;
        assign = res;
        return true;
    }

    // 스펙 매칭(itemId/name/unique, 배열 부분집합 지원)
    bool MatchSpec(ItemData it, JObject spec)
    {
        if (spec == null) return false;

        int constraints = 0;

        string wantId = spec.Value<string>("itemId");
        string wantNm = spec.Value<string>("name");
        if (!string.IsNullOrEmpty(wantId)) constraints++;
        if (!string.IsNullOrEmpty(wantNm)) constraints++;

        if (!string.IsNullOrEmpty(wantId))
        {
            if (it == null) return false;
            if (!string.Equals(it.ItemId, wantId, StringComparison.Ordinal) &&
                !string.Equals(it.Name,   wantId, StringComparison.Ordinal))
                return false;
        }
        if (!string.IsNullOrEmpty(wantNm))
        {
            if (it == null) return false;
            if (!string.Equals(it.Name, wantNm, StringComparison.Ordinal)) return false;
        }

        var uniq = spec["unique"] as JObject;
        if (uniq != null && uniq.Properties().Any())
        {
            constraints++;
            if (it == null || it.Unique == null) return false;

            foreach (var p in uniq.Properties())
            {
                string key = p.Name;
                if (!it.Unique.TryGetValue(key, out var have)) return false;

                var wantTok = p.Value;
                if (wantTok is JArray wantArr)
                {
                    var wantList = wantArr.Select(x => x.ToString()).ToList();
                    var haveList = ToStringList(have);
                    if (!wantList.All(w => haveList.Contains(w))) return false;
                }
                else
                {
                    string wantStr = wantTok.ToString();
                    string haveStr = have?.ToString() ?? "";
                    if (!string.Equals(haveStr, wantStr, StringComparison.Ordinal)) return false;
                }
            }
        }

        if (constraints == 0) return false;
        return true;
    }

    List<string> ToStringList(object v)
    {
        if (v == null) return new List<string>();
        if (v is JArray ja) return ja.Select(x => x.ToString()).ToList();
        if (v is System.Collections.IEnumerable ien && v is not string)
        {
            var list = new List<string>();
            foreach (var x in ien) list.Add(x?.ToString() ?? "");
            return list;
        }
        return new List<string> { v.ToString() };
    }

    // 출력액션(루트 name/spriteName/itemId 반영 + 나머지는 Unique)
    ItemData ApplyOutputActions(ItemData dst, JArray outActs, List<ItemData> slots, int[] assign)
    {
        if (dst == null) return null;
        if (outActs == null || outActs.Count == 0) return dst;
        if (dst.Unique == null) return dst;

        string overrideName = null;
        string overrideSprite = null;
        string overrideItemId = null;

        for (int i = 0; i < outActs.Count; i++)
        {
            var act = outActs[i] as JObject; if (act == null) continue;
            string type = act.Value<string>("type");

            if (type == "setValue")
            {
                string field = act.Value<string>("field");

                // direct value
                if (act.TryGetValue("value", out var jv))
                {
                    object val = jv.Type == JTokenType.Null ? null : ((JValue)jv).Value;
                    if (val is string sv) val = ExpandTokens(sv);

                    if (field == "name")        { overrideName   = val?.ToString(); continue; }
                    if (field == "spriteName")  { overrideSprite = val?.ToString(); continue; }
                    if (field == "itemId")      { overrideItemId = val?.ToString(); continue; }

                    dst.Unique[field] = val;
                    continue;
                }

                // fromInput (+ stripSuffix)
                int? from = act.Value<int?>("fromInput");
                string inputField = act.Value<string>("inputField");
                if (from.HasValue && !string.IsNullOrEmpty(inputField))
                {
                    int si = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                    var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                    var val = ReadField(src, inputField);

                    string strip = act.Value<string>("stripSuffix");
                    if (!string.IsNullOrEmpty(strip) && val is string svFrom && svFrom.EndsWith(strip))
                        val = svFrom.Substring(0, svFrom.Length - strip.Length);

                    string sVal = val?.ToString();

                    if (field == "name")        { overrideName   = sVal; continue; }
                    if (field == "spriteName")  { overrideSprite = sVal; continue; }
                    if (field == "itemId")      { overrideItemId = sVal; continue; }

                    dst.Unique[field] = val;
                    continue;
                }

                // valueFromFields (join)
                var vff = act["valueFromFields"] as JArray;
                if (vff != null)
                {
                    string sep = act.Value<string>("separator") ?? "";
                    string pre = act.Value<string>("prefixEach") ?? "";
                    var vals = new List<string>(vff.Count);
                    foreach (var jf in vff)
                    {
                        string key = jf.ToString();
                        object v = ReadFromUnique(dst, key);
                        if (v != null)
                        {
                            string s = v.ToString();
                            if (!string.IsNullOrEmpty(pre)) s = pre + s;
                            vals.Add(s);
                        }
                    }
                    string joined = string.Join(sep, vals);

                    if (field == "name")        { overrideName   = joined; continue; }
                    if (field == "spriteName")  { overrideSprite = joined; continue; }
                    if (field == "itemId")      { overrideItemId = joined; continue; }

                    dst.Unique[field] = joined;
                    continue;
                }
            }
            else if (type == "copyId")
            {
                int from = act.Value<int>("fromInput");
                string toField = act.Value<string>("toField");
                int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                var val = src?.ItemId;

                if (toField == "name")        { overrideName   = val; continue; }
                if (toField == "spriteName")  { overrideSprite = val; continue; }
                if (toField == "itemId")      { overrideItemId = val; continue; }

                dst.Unique[toField] = val;
            }
            else if (type == "copyField")
            {
                int from = act.Value<int>("fromInput");
                string inField = act.Value<string>("inputField");
                string toField = act.Value<string>("toField");
                int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                var val = ReadField(src, inField);
                string sVal = val?.ToString();

                if (toField == "name")        { overrideName   = sVal; continue; }
                if (toField == "spriteName")  { overrideSprite = sVal; continue; }
                if (toField == "itemId")      { overrideItemId = sVal; continue; }

                dst.Unique[toField] = val;
            }
            else if (type == "sumFields")
            {
                string outField = act.Value<string>("field");
                string inField  = act.Value<string>("inputField");
                var fromInputs  = act["fromInputs"] as JArray;
                int sum = 0;
                if (fromInputs != null)
                {
                    foreach (var jf in fromInputs)
                    {
                        int fi = jf.Value<int>();
                        int si = (assign != null && fi >= 0 && fi < assign.Length) ? assign[fi] : -1;
                        var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                        var v = ReadField(src, inField);
                        if (v != null && int.TryParse(v.ToString(), out int iv)) sum += iv;
                    }
                }

                if (outField == "name")        { overrideName   = sum.ToString(); continue; }
                if (outField == "spriteName")  { overrideSprite = sum.ToString(); continue; }
                if (outField == "itemId")      { overrideItemId = sum.ToString(); continue; }

                dst.Unique[outField] = sum;
            }
        }

        // 루트 오버라이드가 있으면 새 인스턴스로 재구성
        if (overrideName != null || overrideSprite != null || overrideItemId != null)
        {
            string finalName   = overrideName   ?? dst.Name;
            string finalSprite = overrideSprite ?? dst.SpriteName;
            string finalId     = overrideItemId ?? dst.ItemId;
            var finalIcon = itemLibrary != null ? itemLibrary.GetSprite(finalSprite) : dst.Icon;

            return new ItemData(
                itemId:     finalId,
                name:       finalName,
                spriteName: finalSprite,
                itemType:   dst.ItemType,
                maxStack:   dst.MaxStack,
                unique:     dst.Unique,
                icon:       finalIcon,
                count:      dst.Count
            );
        }

        return dst;
    }

    object ReadField(ItemData src, string field)
    {
        if (src == null) return null;

        if (src.Unique != null && src.Unique.TryGetValue(field, out var v)) return v;

        switch (field)
        {
            case "name":       return src.Name;
            case "spriteName": return src.SpriteName;
            case "itemId":     return src.ItemId;
        }
        return null;
    }

    object ReadFromUnique(ItemData dst, string field)
    {
        if (dst?.Unique == null) return null;
        dst.Unique.TryGetValue(field, out var v);
        return v;
    }

    string ExpandTokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string ts   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string rand = System.Guid.NewGuid().ToString("N").Substring(0, 6);
        return s.Replace("$timestamp$", ts).Replace("$rand$", rand);
    }
}
