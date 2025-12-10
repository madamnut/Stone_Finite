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

            // 빈 슬롯 처리: "레시피 인풋들만" 존재해야 함
            int filledCount = presentIdx.Count;
            if (filledCount != inputs.Count)
                continue;

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

        // 1) inputs.Count == 2 레시피 먼저
        var cnt2 = FilterByInputCount(recipeSet2, 2);
        if (cnt2.Count > 0 &&
            TryMatchSet(cnt2, slots, fourContext: true,
                        out resultItems, out remappedInputActions, out matchedRecipe))
            return true;

        // 2) 그 다음 inputs.Count == 1 레시피
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
            int si   = win[k];
            var it   = slots[si];
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
        var res     = TryUnordered(inputs, slots, present);
        if (res == null) return false;
        assign = res;
        return true;
    }

    // 스펙 매칭(itemId/name/hasTag/toolActions 조건)
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
            if (!string.Equals(it.Name, wantNm, StringComparison.Ordinal))
                return false;
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

        // toolActions 매칭 (JObject / JArray / 단일 값 모두 지원)
        var toolSpec = spec["toolActions"];
        if (toolSpec != null && toolSpec.Type != JTokenType.Null)
        {
            constraints++;
            if (it == null) return false;
            if (!MatchToolActions(it, toolSpec)) return false;
        }

        if (constraints == 0) return false;
        return true;
    }

    /// <summary>
    /// toolActions 스펙 매칭.
    /// - spec 예시:
    ///   { "toolActions": { "PercussionFlaking": {}, "X": { "foo": "bar" } } }
    ///   { "toolActions": ["PercussionFlaking", "X"] }
    ///   { "toolActions": "PercussionFlaking" }
    ///
    /// ItemData.ToolActions:
    ///   Dictionary&lt;string, Dictionary&lt;string, object&gt;&gt;
    ///   (키 = 액션명, 값 = 파라미터 딕셔너리)
    /// </summary>
    bool MatchToolActions(ItemData it, JToken toolSpec)
    {
        if (it.ToolActions == null || it.ToolActions.Count == 0)
            return false;

        // 배열: ["A","B"] → 이름만 검사
        if (toolSpec is JArray arr)
        {
            var required = arr.Select(x => x.ToString()).ToList();
            foreach (var name in required)
            {
                if (string.IsNullOrEmpty(name)) continue;
                if (!it.ToolActions.ContainsKey(name))
                    return false;
            }
            return true;
        }

        // 단일 값: "A"
        if (toolSpec is not JObject obj)
        {
            string single = toolSpec.ToString();
            if (string.IsNullOrEmpty(single)) return false;
            return it.ToolActions.ContainsKey(single);
        }

        // 정식 JObject: { "A": { ... }, "B": { ... } }
        foreach (var prop in obj.Properties())
        {
            string toolName = prop.Name;
            if (string.IsNullOrEmpty(toolName))
                return false;

            if (!it.ToolActions.TryGetValue(toolName, out var cfgDict) || cfgDict == null)
                return false;

            if (prop.Value is not JObject wantCfg)
                continue; // 빈 오브젝트면 이름만 체크하는 셈

            // wantCfg 내부 필드 전부 일치 검사 (부분집합 조건)
            foreach (var cfgProp in wantCfg.Properties())
            {
                string key     = cfgProp.Name;
                string wantVal = cfgProp.Value.ToString();

                if (!cfgDict.TryGetValue(key, out var haveObj))
                    return false;

                string haveVal = haveObj?.ToString() ?? "";
                if (!string.Equals(haveVal, wantVal, StringComparison.Ordinal))
                    return false;
            }
        }

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

    // 액션 딕셔너리 변환:
    // - null → null
    // - Dictionary<string, Dictionary<string, object>> 그대로 복사
    // - Dictionary<string, object> → 내부를 Dictionary<string, object>로 캐스팅/래핑
    // - 리스트/배열/단일 문자열 → 이름만 키로 두고 빈 파라미터 딕셔너리
    Dictionary<string, Dictionary<string, object>> ToActionDict(object v)
    {
        if (v == null)
            return null;

        if (v is Dictionary<string, Dictionary<string, object>> dd)
        {
            return dd.ToDictionary(kv => kv.Key,
                                   kv => kv.Value != null
                                       ? new Dictionary<string, object>(kv.Value)
                                       : new Dictionary<string, object>());
        }

        if (v is Dictionary<string, object> d0)
        {
            var res = new Dictionary<string, Dictionary<string, object>>();
            foreach (var kv in d0)
            {
                if (kv.Value is Dictionary<string, object> inner)
                    res[kv.Key] = new Dictionary<string, object>(inner);
                else
                    res[kv.Key] = new Dictionary<string, object>();
            }
            return res;
        }

        if (v is JArray ja)
        {
            var res = new Dictionary<string, Dictionary<string, object>>();
            foreach (var x in ja)
            {
                string name = x.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                if (!res.ContainsKey(name))
                    res[name] = new Dictionary<string, object>();
            }
            return res;
        }

        if (v is System.Collections.IEnumerable ien && v is not string)
        {
            var res = new Dictionary<string, Dictionary<string, object>>();
            foreach (var x in ien)
            {
                string name = x?.ToString();
                if (string.IsNullOrEmpty(name)) continue;
                if (!res.ContainsKey(name))
                    res[name] = new Dictionary<string, object>();
            }
            return res;
        }

        // 단일 값
        string single = v.ToString();
        if (string.IsNullOrEmpty(single))
            return null;

        return new Dictionary<string, Dictionary<string, object>>
        {
            { single, new Dictionary<string, object>() }
        };
    }

    // 출력액션 적용 (name/spriteName/itemId/durability/maxDurability/액션 딕셔너리 + Details 조작)
    ItemData ApplyOutputActions(ItemData dst, JArray outActs, List<ItemData> slots, int[] assign)
    {
        if (dst == null) return null;
        if (outActs == null || outActs.Count == 0) return dst;

        string       overrideName       = null;
        string       overrideSprite     = null;
        string       overrideItemId     = null;
        int?         overrideDurability = null;
        int?         overrideMaxDur     = null;

        Dictionary<string, Dictionary<string, object>> overrideTool   = null;
        Dictionary<string, Dictionary<string, object>> overrideWeapon = null;
        Dictionary<string, Dictionary<string, object>> overrideBreak  = null;

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

                    // 액션 딕셔너리 직접 세팅
                    if (field == "toolActions" ||
                        field == "weaponActions" ||
                        field == "breakActions")
                    {
                        var dict = ToActionDict(val);
                        if (field == "toolActions")        overrideTool   = dict;
                        else if (field == "weaponActions") overrideWeapon = dict;
                        else                               overrideBreak  = dict;
                        continue;
                    }

                    // 특수 필드
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

                    // 나머지는 Details
                    if (field.StartsWith("details.", StringComparison.Ordinal))
                        dst.SetDetailPath(field.Substring("details.".Length), val);
                    else
                        dst.SetDetailPath(field, val);

                    continue;
                }

                // fromInput (+ stripSuffix)
                int?   from       = act.Value<int?>("fromInput");
                string inputField = act.Value<string>("inputField");
                if (from.HasValue && !string.IsNullOrEmpty(inputField))
                {
                    int si  = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                    var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                    var val = ReadField(src, inputField);

                    string strip = act.Value<string>("stripSuffix");
                    if (!string.IsNullOrEmpty(strip) && val is string svFrom && svFrom.EndsWith(strip))
                        val = svFrom.Substring(0, svFrom.Length - strip.Length);

                    if (field == "toolActions" ||
                        field == "weaponActions" ||
                        field == "breakActions")
                    {
                        var dict = ToActionDict(val);
                        if (field == "toolActions")        overrideTool   = dict;
                        else if (field == "weaponActions") overrideWeapon = dict;
                        else                               overrideBreak  = dict;
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

                    if (field.StartsWith("details.", StringComparison.Ordinal))
                        dst.SetDetailPath(field.Substring("details.".Length), val);
                    else
                        dst.SetDetailPath(field, val);

                    continue;
                }

                // valueFromFields (join)
                var vff = act["valueFromFields"] as JArray;
                if (vff != null)
                {
                    string sep = act.Value<string>("separator") ?? "";
                    string pre = act.Value<string>("prefixEach") ?? "";
                    var vals   = new List<string>(vff.Count);

                    foreach (var jf in vff)
                    {
                        string key = jf.ToString();
                        object v   = ReadField(dst, key);
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

                    if (field.StartsWith("details.", StringComparison.Ordinal))
                        dst.SetDetailPath(field.Substring("details.".Length), joined);
                    else
                        dst.SetDetailPath(field, joined);

                    continue;
                }
            }
            else if (type == "copyId")
            {
                int    from    = act.Value<int>("fromInput");
                string toField = act.Value<string>("toField");
                int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                var val = src?.ItemId;

                if (toField == "name")       { overrideName   = val; continue; }
                if (toField == "spriteName") { overrideSprite = val; continue; }
                if (toField == "itemId")     { overrideItemId = val; continue; }

                if (toField.StartsWith("details.", StringComparison.Ordinal))
                    dst.SetDetailPath(toField.Substring("details.".Length), val);
                else
                    dst.SetDetailPath(toField, val);
            }
            else if (type == "copyField")
            {
                int    from    = act.Value<int>("fromInput");
                string inField = act.Value<string>("inputField");
                string toField = act.Value<string>("toField");
                int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                var val = ReadField(src, inField);

                if (toField == "toolActions" ||
                    toField == "weaponActions" ||
                    toField == "breakActions")
                {
                    var dict = ToActionDict(val);
                    if (dict != null)
                    {
                        if (toField == "toolActions")        overrideTool   = dict;
                        else if (toField == "weaponActions") overrideWeapon = dict;
                        else                                 overrideBreak  = dict;
                    }
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

                if (toField.StartsWith("details.", StringComparison.Ordinal))
                    dst.SetDetailPath(toField.Substring("details.".Length), val);
                else
                    dst.SetDetailPath(toField, val);
            }
            else if (type == "sumFields")
            {
                string outField  = act.Value<string>("field");
                string inField   = act.Value<string>("inputField");
                var    fromInputs = act["fromInputs"] as JArray;
                int sum = 0;

                if (fromInputs != null)
                {
                    foreach (var jf in fromInputs)
                    {
                        int fi = jf.Value<int>();
                        int si = (assign != null && fi >= 0 && fi < assign.Length) ? assign[fi] : -1;
                        var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                        var v   = ReadField(src, inField);
                        if (v != null && int.TryParse(v.ToString(), out int iv)) sum += iv;
                    }
                }

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

                if (outField == "name")       { overrideName   = sum.ToString(); continue; }
                if (outField == "spriteName") { overrideSprite = sum.ToString(); continue; }
                if (outField == "itemId")     { overrideItemId = sum.ToString(); continue; }

                if (outField.StartsWith("details.", StringComparison.Ordinal))
                    dst.SetDetailPath(outField.Substring("details.".Length), sum);
                else
                    dst.SetDetailPath(outField, sum);
            }
            else if (type == "paramSet")
            {
                string field = act.Value<string>("field");

                if (act.TryGetValue("value", out var jv))
                {
                    object val = jv.Type == JTokenType.Null ? null : ((JValue)jv).Value;

                    if (field.StartsWith("details.", StringComparison.Ordinal))
                        dst.SetDetailPath(field.Substring("details.".Length), val);
                    else
                        dst.SetDetailPath(field, val);

                    continue;
                }

                int?   from       = act.Value<int?>("fromInput");
                string inputField = act.Value<string>("inputField");
                if (from.HasValue && !string.IsNullOrEmpty(inputField))
                {
                    int si  = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                    var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                    var val = ReadField(src, inputField);

                    if (field.StartsWith("details.", StringComparison.Ordinal))
                        dst.SetDetailPath(field.Substring("details.".Length), val);
                    else
                        dst.SetDetailPath(field, val);

                    continue;
                }
            }
            else if (type == "paramSum")
            {
                string field     = act.Value<string>("field");
                string inField   = act.Value<string>("inputField");
                var    fromInputs = act["fromInputs"] as JArray;
                int sum = 0;

                if (fromInputs != null)
                {
                    foreach (var jf in fromInputs)
                    {
                        int fi = jf.Value<int>();
                        int si = (assign != null && fi >= 0 && fi < assign.Length) ? assign[fi] : -1;
                        var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                        var v   = ReadField(src, inField);
                        if (v != null && int.TryParse(v.ToString(), out int iv)) sum += iv;
                    }
                }

                if (field.StartsWith("details.", StringComparison.Ordinal))
                    dst.SetDetailPath(field.Substring("details.".Length), sum);
                else
                    dst.SetDetailPath(field, sum);
            }
        }

        bool changed =
            overrideName       != null ||
            overrideSprite     != null ||
            overrideItemId     != null ||
            overrideDurability.HasValue ||
            overrideMaxDur.HasValue ||
            overrideTool   != null ||
            overrideWeapon != null ||
            overrideBreak  != null;

        if (!changed)
            return dst;

        string finalName       = overrideName       ?? dst.Name;
        string finalSprite     = overrideSprite     ?? dst.SpriteName;
        string finalId         = overrideItemId     ?? dst.ItemId;
        int    finalMaxDur     = overrideMaxDur     ?? dst.MaxDurability;
        int    finalDurability = overrideDurability ?? dst.Durability;

        var finalTool   = overrideTool   ?? dst.ToolActions;
        var finalWeapon = overrideWeapon ?? dst.WeaponActions;
        var finalBreak  = overrideBreak  ?? dst.BreakActions;

        var finalIcon    = itemLibrary != null ? itemLibrary.GetSprite(finalSprite) : dst.Icon;
        var finalDetails = dst.Details;

        return new ItemData(
            itemId:        finalId,
            name:          finalName,
            spriteName:    finalSprite,
            itemType:      dst.ItemType,
            maxStack:      dst.MaxStack,
            maxDurability: finalMaxDur,
            durability:    finalDurability,
            toolActions:   finalTool,
            weaponActions: finalWeapon,
            breakActions:  finalBreak,
            tags:          dst.Tags,
            details:       finalDetails,
            icon:          finalIcon,
            count:         dst.Count
        );
    }

    object ReadField(ItemData src, string field)
    {
        if (src == null || string.IsNullOrEmpty(field)) return null;

        const string prefix = "details.";

        // Details.* 경로
        if (field.StartsWith(prefix, StringComparison.Ordinal))
        {
            string inner = field.Substring(prefix.Length);
            return ReadFromDetails(src, inner);
        }

        // 특수 필드
        switch (field)
        {
            case "name":          return src.Name;
            case "spriteName":    return src.SpriteName;
            case "itemId":        return src.ItemId;
            case "durability":    return src.Durability;
            case "maxDurability": return src.MaxDurability;
            case "tags":          return src.Tags;

            case "toolActions":
            {
                var v = ReadFromDetails(src, "toolActions");
                return v ?? (object)src.ToolActions;
            }
            case "weaponActions":
            {
                var v = ReadFromDetails(src, "weaponActions");
                return v ?? (object)src.WeaponActions;
            }
            case "breakActions":
            {
                var v = ReadFromDetails(src, "breakActions");
                return v ?? (object)src.BreakActions;
            }
        }

        // fallback: Details 루트 기준 경로로 시도
        return ReadFromDetails(src, field);
    }

    object ReadFromDetails(ItemData dst, string path)
    {
        if (dst?.Details == null || string.IsNullOrEmpty(path)) return null;

        var parts   = path.Split('.');
        object curr = dst.Details;

        for (int i = 0; i < parts.Length; i++)
        {
            string key = parts[i];

            if (curr is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(key, out curr))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return curr;
    }

    string ExpandTokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string ts   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string rand = Guid.NewGuid().ToString("N").Substring(0, 6);
        return s.Replace("$timestamp$", ts).Replace("$rand$", rand);
    }
}
