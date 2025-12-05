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

    /// <summary>
    /// 슬롯 스냅샷 그대로 입력.
    /// - resultItems: 출력액션까지 적용된 결과 아이템 배열(멀티 아웃풋).
    /// - remappedInputActions: 슬롯 인덱스별 인풋액션(JArray, null 허용).
    ///
    /// JSON 스키마:
    /// {
    ///   "isOrdered": false,
    ///   "inputs": [...],
    ///   "inputActions": [ {..}, {..}, ... ],
    ///   "outputs": [
    ///     { "itemId": "X", "count": 1 },
    ///     { "itemId": "Y", "count": 2 }
    ///   ],
    ///   "outputActions": [
    ///     [ {..}, {..} ],   // outputs[0] 에 대한 액션
    ///     [ {..} ]          // outputs[1] 에 대한 액션
    ///   ]
    /// }
    /// </summary>
    public bool TryCraft(
        List<ItemData> slots,
        out List<ItemData> resultItems,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItems = null;
        remappedInputActions = null;
        matchedRecipe = null;
        if (itemLibrary == null || slots == null) return false;

        int n = slots.Count;

        // ───────── 4슬롯 테이블 (Primal 등) ─────────
        if (n == 4)
        {
            // 1) 4슬롯 레시피 중 inputs.Count == 4
            if (_r4 != null)
            {
                var r4_4 = FilterByInputCount(_r4, 4);
                if (r4_4.Count > 0 &&
                    TryMatchSet(r4_4, slots, fourContext: true,
                                out resultItems, out remappedInputActions, out matchedRecipe))
                    return true;

                // 2) 4슬롯 레시피 중 inputs.Count == 3
                var r4_3 = FilterByInputCount(_r4, 3);
                if (r4_3.Count > 0 &&
                    TryMatchSet(r4_3, slots, fourContext: true,
                                out resultItems, out remappedInputActions, out matchedRecipe))
                    return true;

                // 3) 4슬롯 레시피 중 inputs.Count == 2 → 2x2 윈도우 매칭
                var r4_2 = FilterByInputCount(_r4, 2);
                if (r4_2.Count > 0 &&
                    TryMatch2InFourContext(r4_2, slots,
                                            out resultItems, out remappedInputActions, out matchedRecipe))
                    return true;
            }

            // 4) 2슬롯 레시피 세트를 4슬롯 테이블에서 사용
            if (_r2 != null &&
                TryMatch2InFourContext(_r2, slots,
                                       out resultItems, out remappedInputActions, out matchedRecipe))
                return true;

            return false;
        }

        // ───────── 2슬롯 테이블 (Hand 등) ─────────
        if (n == 2)
        {
            // 1) 2슬롯 레시피(Hand 기본)
            if (_r2 != null &&
                TryMatchSet(_r2, slots, fourContext: false,
                            out resultItems, out remappedInputActions, out matchedRecipe))
                return true;

            // 2) 4슬롯 레시피 중 inputs.Count == 2 를 2슬롯 테이블에서 재사용
            if (_r4 != null &&
                TryMatchSet(FilterByInputCount(_r4, 2), slots, fourContext: false,
                            out resultItems, out remappedInputActions, out matchedRecipe))
                return true;

            return false;
        }

        return false;
    }

    // 세트 매칭(슬롯 수/인덱스 그대로, null 슬롯 유지) + 멀티 아웃풋
    bool TryMatchSet(
        JArray recipeSet,
        List<ItemData> slots,
        bool fourContext,
        out List<ItemData> resultItems,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItems = null;
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

            // 새 스키마: outputs 필수
            var outputsArray = r["outputs"] as JArray;
            if (outputsArray == null || outputsArray.Count == 0) continue;

            var inActs = r["inputActions"] as JArray;
            var oaRoot = r["outputActions"] as JArray; // null 가능

            // 빈 슬롯 처리
            int filledCount = presentIdx.Count;
            if (!fourContext)
            {
                if (filledCount != inputs.Count)
                    continue;
            }
            else
            {
                if (filledCount < inputs.Count)
                    continue;
            }

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

            // 멀티 아웃풋 생성
            var results = new List<ItemData>();

            for (int oi = 0; oi < outputsArray.Count; oi++)
            {
                var outSpec = outputsArray[oi] as JObject;
                if (outSpec == null) continue;

                string outId  = outSpec.Value<string>("itemId");
                int    outCnt = outSpec.Value<int?>("count") ?? 1;
                if (string.IsNullOrEmpty(outId) || outCnt <= 0) continue;

                var baseItem = itemLibrary.Create(outId, outCnt);
                if (baseItem == null) continue;

                // outputActions[oi] 는 JArray(액션 리스트) 라고 가정
                JArray perActs = null;
                if (oaRoot != null && oi < oaRoot.Count && oaRoot[oi] is JArray ja)
                    perActs = ja;

                var finalItem = ApplyOutputActions(baseItem, perActs, slots, assign);
                if (finalItem != null)
                    results.Add(finalItem);
            }

            if (results.Count == 0)
            {
                remappedInputActions = null;
                continue;
            }

            resultItems   = results;
            matchedRecipe = r;
            return true;
        }

        return false;
    }

    // 4슬롯 컨텍스트에서 2슬롯/1슬롯 레시피를 우선순위대로 시도
    bool TryMatch2InFourContext(
        JArray recipeSet2,
        List<ItemData> slots,
        out List<ItemData> resultItems,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItems = null;
        remappedInputActions = null;
        matchedRecipe = null;

        if (recipeSet2 == null || slots.Count != 4) return false;

        // 1) inputs.Count == 2 레시피 먼저 (예: Plant Twine + Plant Twine → Long Plant Twine)
        var cnt2 = FilterByInputCount(recipeSet2, 2);
        if (cnt2.Count > 0 &&
            TryMatchSet(cnt2, slots, fourContext: true,
                        out resultItems, out remappedInputActions, out matchedRecipe))
            return true;

        // 2) 그 다음 inputs.Count == 1 레시피 (예: Plant Twine → Short Plant Twine)
        var cnt1 = FilterByInputCount(recipeSet2, 1);
        if (cnt1.Count > 0 &&
            TryMatchSet(cnt1, slots, fourContext: true,
                        out resultItems, out remappedInputActions, out matchedRecipe))
            return true;

        return false;
    }

    // inputs.Count == cnt 필터
    JArray FilterByInputCount(JArray src, int cnt)
    {
        var arr = new JArray();
        if (src == null) return arr;

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

    // 스펙 매칭(itemId/name/params/hasTag/craftingActions, 배열 부분집합 지원)
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

        // params 매칭 (점 표기법/중첩 구조 지원)
        var paramSpec = spec["params"] as JObject;
        if (paramSpec != null && paramSpec.Properties().Any())
        {
            constraints++;
            if (it == null) return false;
            if (!MatchParams(it.Parameters, paramSpec)) return false;
        }

        // 태그 매칭
        var tagSpec = spec["hasTag"] as JArray;
        if (tagSpec != null && tagSpec.Count > 0)
        {
            constraints++;
            if (it == null) return false;

            foreach (var t in tagSpec)
            {
                string tag = t.ToString();
                if (!it.HasTag(tag)) return false;
            }
        }

        // craftingActions 매칭 (tool gating용)
        var caSpec = spec["craftingActions"] as JArray;
        if (caSpec != null && caSpec.Count > 0)
        {
            constraints++;
            if (it == null) return false;

            var required = caSpec.Select(x => x.ToString()).ToList();
            var haveList = it.CraftingActions ?? new List<string>();
            foreach (var req in required)
            {
                if (!haveList.Contains(req))
                    return false;
            }
        }

        if (constraints == 0) return false;
        return true;
    }

    // params: 점 표기법(Head.type) + 중첩 JObject 모두 지원
    bool MatchParams(Dictionary<string, object> parameters, JObject paramSpec)
    {
        if (paramSpec == null || !paramSpec.Properties().Any())
            return true;

        if (parameters == null)
            return false;

        // 실제 아이템 파라미터 전체를 JObject로 래핑
        var haveRoot = JObject.FromObject(parameters);

        // paramSpec 의 각 프로퍼티를 경로 기반으로 비교
        foreach (var p in paramSpec.Properties())
        {
            string path = p.Name;          // 예: "Head.type"
            JToken want = p.Value;

            // 경로 분해 (점 표기법)
            var parts = path.Split('.');
            JToken current = haveRoot;
            for (int i = 0; i < parts.Length; i++)
            {
                string key = parts[i];

                if (current is JObject jo)
                {
                    if (!jo.TryGetValue(key, out var next))
                        return false;      // 경로에 해당하는 키 없음 → 불일치
                    current = next;
                }
                else
                {
                    return false;          // 중간에 객체가 아닌 타입이 나옴 → 불일치
                }
            }

            // 최종 토큰 current 와 원하는 want 비교
            if (!MatchToken(current, want))
                return false;
        }

        return true;
    }

    bool MatchToken(JToken have, JToken want)
    {
        if (want == null) return have == null;

        if (want.Type == JTokenType.Object)
        {
            if (have == null || have.Type != JTokenType.Object) return false;
            var wObj = (JObject)want;
            var hObj = (JObject)have;

            foreach (var p in wObj.Properties())
            {
                if (!hObj.TryGetValue(p.Name, out var subHave))
                    return false;
                if (!MatchToken(subHave, p.Value))
                    return false;
            }
            return true;
        }

        if (want.Type == JTokenType.Array)
        {
            var wArr = (JArray)want;

            if (have != null && have.Type == JTokenType.Array)
            {
                var hArr = (JArray)have;
                var haveStrings = hArr.Select(x => x.ToString()).ToList();
                var wantStrings = wArr.Select(x => x.ToString()).ToList();
                return wantStrings.All(w => haveStrings.Contains(w));
            }
            else
            {
                if (wArr.Count != 1) return false;
                return MatchToken(have, wArr[0]);
            }
        }

        var haveStr = have?.ToString() ?? "";
        var wantStr = want.ToString();
        return string.Equals(haveStr, wantStr, StringComparison.Ordinal);
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

    // 출력액션(루트 name/spriteName/itemId/durability/maxDurability/액션 배열 반영 + 나머지는 Parameters/params)
    ItemData ApplyOutputActions(ItemData dst, JArray outActs, List<ItemData> slots, int[] assign)
    {
        if (dst == null) return null;
        if (outActs == null || outActs.Count == 0) return dst;
        if (dst.Parameters == null) return dst;

        string       overrideName       = null;
        string       overrideSprite     = null;
        string       overrideItemId     = null;
        int?         overrideDurability = null;
        int?         overrideMaxDur     = null;
        List<string> overrideCraft      = null;
        List<string> overrideInter      = null;
        List<string> overrideTool       = null;
        List<string> overrideWeapon     = null;

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

                    if (field == "craftingActions" ||
                        field == "interActions" ||
                        field == "toolActions" ||
                        field == "weaponActions")
                    {
                        var list = ToStringList(val);
                        if (field == "craftingActions")      overrideCraft  = list;
                        else if (field == "interActions")    overrideInter  = list;
                        else if (field == "toolActions")     overrideTool   = list;
                        else                                 overrideWeapon = list;
                        continue;
                    }

                    if (field == "name")       { overrideName   = val?.ToString(); continue; }
                    if (field == "spriteName") { overrideSprite = val?.ToString(); continue; }
                    if (field == "itemId")     { overrideItemId = val?.ToString(); continue; }

                    if (field == "durability")
                    {
                        if (val != null && int.TryParse(val.ToString(), out int iv))
                            overrideDurability = iv;
                        continue;
                    }
                    if (field == "maxDurability")
                    {
                        if (val != null && int.TryParse(val.ToString(), out int iv))
                            overrideMaxDur = iv;
                        continue;
                    }

                    dst.SetParamPath(field, val);
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

                    if (field == "craftingActions" ||
                        field == "interActions" ||
                        field == "toolActions" ||
                        field == "weaponActions")
                    {
                        var list = ToStringList(val);
                        if (field == "craftingActions")      overrideCraft  = list;
                        else if (field == "interActions")    overrideInter  = list;
                        else if (field == "toolActions")     overrideTool   = list;
                        else                                 overrideWeapon = list;
                        continue;
                    }

                    string sVal = val?.ToString();

                    if (field == "name")       { overrideName   = sVal; continue; }
                    if (field == "spriteName") { overrideSprite = sVal; continue; }
                    if (field == "itemId")     { overrideItemId = sVal; continue; }

                    if (field == "durability")
                    {
                        if (val != null && int.TryParse(val.ToString(), out int iv))
                            overrideDurability = iv;
                        continue;
                    }
                    if (field == "maxDurability")
                    {
                        if (val != null && int.TryParse(val.ToString(), out int iv))
                            overrideMaxDur = iv;
                        continue;
                    }

                    dst.SetParamPath(field, val);
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
                        object v = ReadFromParameters(dst, key);
                        if (v != null)
                        {
                            string s = v.ToString();
                            if (!string.IsNullOrEmpty(pre)) s = pre + s;
                            vals.Add(s);
                        }
                    }
                    string joined = string.Join(sep, vals);

                    if (field == "name")       { overrideName   = joined; continue; }
                    if (field == "spriteName") { overrideSprite = joined; continue; }
                    if (field == "itemId")     { overrideItemId = joined; continue; }

                    dst.SetParamPath(field, joined);
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

                if (toField == "name")       { overrideName   = val; continue; }
                if (toField == "spriteName") { overrideSprite = val; continue; }
                if (toField == "itemId")     { overrideItemId = val; continue; }

                dst.SetParamPath(toField, val);
            }
            else if (type == "copyField")
            {
                int from = act.Value<int>("fromInput");
                string inField = act.Value<string>("inputField");
                string toField = act.Value<string>("toField");
                int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                var val = ReadField(src, inField);

                if (toField == "craftingActions" ||
                    toField == "interActions" ||
                    toField == "toolActions" ||
                    toField == "weaponActions")
                {
                    var list = ToStringList(val);
                    if (toField == "craftingActions")      overrideCraft  = list;
                    else if (toField == "interActions")    overrideInter  = list;
                    else if (toField == "toolActions")     overrideTool   = list;
                    else                                   overrideWeapon = list;
                    continue;
                }

                string sVal = val?.ToString();

                if (toField == "name")       { overrideName   = sVal; continue; }
                if (toField == "spriteName") { overrideSprite = sVal; continue; }
                if (toField == "itemId")     { overrideItemId = sVal; continue; }

                if (toField == "durability")
                {
                    if (val != null && int.TryParse(val.ToString(), out int iv))
                        overrideDurability = iv;
                    continue;
                }
                if (toField == "maxDurability")
                {
                    if (val != null && int.TryParse(val.ToString(), out int iv))
                        overrideMaxDur = iv;
                    continue;
                }

                dst.SetParamPath(toField, val);
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

                if (outField == "name")       { overrideName   = sum.ToString(); continue; }
                if (outField == "spriteName") { overrideSprite = sum.ToString(); continue; }
                if (outField == "itemId")     { overrideItemId = sum.ToString(); continue; }

                if (outField == "durability")
                {
                    overrideDurability = (overrideDurability ?? 0) + sum;
                    continue;
                }
                if (outField == "maxDurability")
                {
                    overrideMaxDur = (overrideMaxDur ?? 0) + sum;
                    continue;
                }

                dst.SetParamPath(outField, sum);
            }
            else if (type == "paramSet")
            {
                string field = act.Value<string>("field");

                if (act.TryGetValue("value", out var jv))
                {
                    object val = jv.Type == JTokenType.Null ? null : ((JValue)jv).Value;
                    dst.SetParamPath(field, val);
                    continue;
                }

                int? from = act.Value<int?>("fromInput");
                string inputField = act.Value<string>("inputField");
                if (from.HasValue && !string.IsNullOrEmpty(inputField))
                {
                    int si = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                    var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                    var val = ReadField(src, inputField);
                    dst.SetParamPath(field, val);
                    continue;
                }
            }
            else if (type == "paramSum")
            {
                string field    = act.Value<string>("field");
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
                dst.SetParamPath(field, sum);
            }
        }

        bool changed =
            overrideName       != null ||
            overrideSprite     != null ||
            overrideItemId     != null ||
            overrideDurability.HasValue ||
            overrideMaxDur.HasValue ||
            overrideCraft  != null ||
            overrideInter  != null ||
            overrideTool   != null ||
            overrideWeapon != null;

        if (!changed)
            return dst;

        string finalName       = overrideName       ?? dst.Name;
        string finalSprite     = overrideSprite     ?? dst.SpriteName;
        string finalId         = overrideItemId     ?? dst.ItemId;
        int    finalMaxDur     = overrideMaxDur     ?? dst.MaxDurability;
        int    finalDurability = overrideDurability ?? dst.Durability;

        var finalCraft  = overrideCraft  ?? dst.CraftingActions;
        var finalInter  = overrideInter  ?? dst.InterActions;
        var finalTool   = overrideTool   ?? dst.ToolActions;
        var finalWeapon = overrideWeapon ?? dst.WeaponActions;

        var finalIcon = itemLibrary != null ? itemLibrary.GetSprite(finalSprite) : dst.Icon;

        return new ItemData(
            itemId:          finalId,
            name:            finalName,
            spriteName:      finalSprite,
            itemType:        dst.ItemType,
            maxStack:        dst.MaxStack,
            maxDurability:   finalMaxDur,
            durability:      finalDurability,
            craftingActions: finalCraft,
            interActions:    finalInter,
            toolActions:     finalTool,
            weaponActions:   finalWeapon,
            tags:            dst.Tags,
            parameters:      dst.Parameters,
            icon:            finalIcon,
            count:           dst.Count
        );
    }

    object ReadField(ItemData src, string field)
    {
        if (src == null) return null;

        var val = ReadFromParameters(src, field);
        if (val != null) return val;

        switch (field)
        {
            case "name":            return src.Name;
            case "spriteName":      return src.SpriteName;
            case "itemId":          return src.ItemId;
            case "durability":      return src.Durability;
            case "maxDurability":   return src.MaxDurability;
            case "craftingActions": return src.CraftingActions;
            case "interActions":    return src.InterActions;
            case "toolActions":     return src.ToolActions;
            case "weaponActions":   return src.WeaponActions;
            case "tags":            return src.Tags;
        }
        return null;
    }

    object ReadFromParameters(ItemData dst, string field)
    {
        if (dst?.Parameters == null || string.IsNullOrEmpty(field)) return null;

        var root = JObject.FromObject(dst.Parameters);
        var parts = field.Split('.');
        JToken current = root;

        for (int i = 0; i < parts.Length; i++)
        {
            string key = parts[i];

            if (current is JObject jo)
            {
                if (!jo.TryGetValue(key, out current))
                    return null;
            }
            else
            {
                return null;
            }
        }

        if (current is JValue jv) return jv.Value;
        return current;
    }

    string ExpandTokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string ts   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string rand = Guid.NewGuid().ToString("N").Substring(0, 6);
        return s.Replace("$timestamp$", ts).Replace("$rand$", rand);
    }
}
