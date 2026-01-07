// RecipeLibrary.cs (전체 교체본)
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

    [Header("Alloy Jsons")]
    public TextAsset alloyJson;   // ✅ 합금(크루시블) 전용

    [Header("Toolbench Jsons")]
    public TextAsset toolbenchJson; // ✅ Toolbench 전용 (candidates 스키마)

    JArray _r2;
    JArray _r4;

    // ✅ Toolbench 레시피(별도 스키마) - NEW(권장)
    // [
    //   {
    //     "inputs":{
    //       "material":{ "itemId":"Unfired Refractory Clay Slab", "count":1 },
    //       "tool":{ "ToolActions":{ "Carving":{} }, "count":1 }
    //     },
    //     "inputActions":{
    //       "material":{ "type":"consume", "amount":1 },
    //       "tool":{ "type":"durability", "amount":-1 }
    //     },
    //     "candidates":[ { "itemId":"Unfired Ingot Mold", "count":1 } ]
    //   }
    // ]
    //
    // ✅ Toolbench 레시피(LEGACY 호환)
    // [
    //   {
    //     "isOrdered": true,
    //     "inputs": [
    //       { "itemId": "Unfired Refractory Clay Slab", "count": 1 },
    //       { "ToolActions": { "Carving": {} }, "count": 1 }
    //     ],
    //     "inputActions": [
    //       { "type": "consume", "amount": 1 },
    //       { "type": "durability", "amount": -1 }
    //     ],
    //     "candidates": [
    //       { "itemId": "Unfired Ingot Mold", "count": 1 }
    //     ]
    //   }
    // ]
    JArray _toolbench;

    // ✅ 합금 레시피(별도 스키마)
    // [
    //   {
    //     "inputs": [ { "id":"Molten Tin", "amount":1 }, { "id":"Molten Copper", "amount":9 } ],
    //     "output": { "id":"Molten Bronze", "amount":10 }
    //   }
    // ]
    class AlloyEntry
    {
        public readonly List<(string id, int amount)> inputs = new List<(string, int)>();
        public string outId;
        public int outAmount;
    }

    readonly List<AlloyEntry> _alloys = new List<AlloyEntry>();

    void Awake()
    {
        if (recipe2Json != null && !string.IsNullOrEmpty(recipe2Json.text))
            _r2 = JArray.Parse(recipe2Json.text);
        if (recipe4Json != null && !string.IsNullOrEmpty(recipe4Json.text))
            _r4 = JArray.Parse(recipe4Json.text);

        LoadAlloys();     // ✅ 추가
        LoadToolbench();  // ✅ 추가
    }

    void LoadToolbench()
    {
        _toolbench = null;

        if (toolbenchJson == null || string.IsNullOrEmpty(toolbenchJson.text))
            return;

        try
        {
            // 1) 배열 루트 지원: [ {...}, ... ]
            _toolbench = JArray.Parse(toolbenchJson.text);
        }
        catch
        {
            // 2) 오브젝트 루트 지원: { "Toolbench": [ ... ] } 같은 케이스도 흡수
            try
            {
                var jo = JObject.Parse(toolbenchJson.text);
                if (jo.TryGetValue("Toolbench", out var tok) && tok is JArray arr)
                    _toolbench = arr;
                else
                    _toolbench = null;
            }
            catch
            {
                _toolbench = null;
            }
        }
    }

    void LoadAlloys()
    {
        _alloys.Clear();

        if (alloyJson == null || string.IsNullOrEmpty(alloyJson.text))
            return;

        JArray arr;
        try { arr = JArray.Parse(alloyJson.text); }
        catch { return; }

        for (int i = 0; i < arr.Count; i++)
        {
            var obj = arr[i] as JObject;
            if (obj == null) continue;

            var inputs = obj["inputs"] as JArray;
            var output = obj["output"] as JObject;
            if (inputs == null || inputs.Count == 0 || output == null) continue;

            string outId = output.Value<string>("id");
            int outAmt = output.Value<int?>("amount") ?? 0;
            if (string.IsNullOrEmpty(outId) || outAmt <= 0) continue;

            var e = new AlloyEntry();
            e.outId = outId;
            e.outAmount = outAmt;

            bool ok = true;
            for (int k = 0; k < inputs.Count; k++)
            {
                var inp = inputs[k] as JObject;
                if (inp == null) { ok = false; break; }

                string inId = inp.Value<string>("id");
                int inAmt = inp.Value<int?>("amount") ?? 0;

                if (string.IsNullOrEmpty(inId) || inAmt <= 0) { ok = false; break; }
                e.inputs.Add((inId, inAmt));
            }

            if (!ok || e.inputs.Count == 0) continue;
            _alloys.Add(e);
        }
    }

    /// <summary>
    /// ✅ Toolbench 전용: (재료 1슬롯 + 툴 1슬롯)에서 가능한 결과 후보(candidates)를 반환.
    /// - NEW 스키마(inputs/material+tool, inputActions/material+tool) 지원
    /// - LEGACY 스키마(inputs 배열, inputActions 배열, isOrdered)도 호환
    /// - matchedRecipe: 매칭된 레시피 JObject
    /// - remappedInputActions: 슬롯 인덱스 기준으로 리맵된 inputActions (null 허용)
    /// </summary>
    public bool TryGetToolbenchCandidates(
        List<ItemData> slots,
        out List<ItemData> candidates,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        candidates = null;
        remappedInputActions = null;
        matchedRecipe = null;

        if (itemLibrary == null || slots == null) return false;
        if (slots.Count != 2) return false;
        if (_toolbench == null || _toolbench.Count == 0) return false;

        // Toolbench는 2슬롯 고정: slots[0]=material, slots[1]=tool
        return TryMatchToolbenchSet(_toolbench, slots, out candidates, out remappedInputActions, out matchedRecipe);
    }

    bool TryMatchToolbenchSet(
        JArray recipeSet,
        List<ItemData> slots,
        out List<ItemData> candidates,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        candidates = null;
        remappedInputActions = null;
        matchedRecipe = null;

        var mat = slots[0];
        var tool = slots[1];

        for (int rix = 0; rix < recipeSet.Count; rix++)
        {
            var r = recipeSet[rix] as JObject;
            if (r == null) continue;

            // candidates 또는 outputs(호환)
            var candArray = r["candidates"] as JArray;
            if (candArray == null) candArray = r["outputs"] as JArray;
            if (candArray == null || candArray.Count == 0) continue;

            // ───────── NEW: inputs object ─────────
            // "inputs": { "material":{...}, "tool":{...} }
            if (r["inputs"] is JObject inObj)
            {
                var matSpec = inObj["material"] as JObject;
                var toolSpec = inObj["tool"] as JObject;

                // NEW 스키마가 아니면 아래 LEGACY로 내려감
                if (matSpec != null && toolSpec != null)
                {
                    if (mat == null || tool == null) continue;
                    if (!MatchSpecWithCount(mat, matSpec)) continue;
                    if (!MatchSpecWithCount(tool, toolSpec)) continue;

                    // inputActions: { material:{...}, tool:{...} } (null 허용)
                    remappedInputActions = new JArray { null, null };

                    if (r["inputActions"] is JObject actObj)
                    {
                        remappedInputActions[0] = actObj.TryGetValue("material", out var am) ? am : null;
                        remappedInputActions[1] = actObj.TryGetValue("tool", out var at) ? at : null;
                    }

                    // candidates 생성
                    var resultsNew = new List<ItemData>();
                    for (int ci = 0; ci < candArray.Count; ci++)
                    {
                        var c = candArray[ci] as JObject;
                        if (c == null) continue;

                        string id = c.Value<string>("itemId");
                        int cnt = c.Value<int?>("count") ?? 1;
                        if (string.IsNullOrEmpty(id) || cnt <= 0) continue;

                        var it = itemLibrary.Create(id, cnt);
                        if (it != null)
                            resultsNew.Add(it);
                    }

                    if (resultsNew.Count == 0)
                    {
                        remappedInputActions = null;
                        continue;
                    }

                    candidates = resultsNew;
                    matchedRecipe = r;
                    return true;
                }
            }

            // ───────── LEGACY: inputs array ─────────
            var inputs = r["inputs"] as JArray;
            if (inputs == null || inputs.Count == 0) continue;

            var inActs = r["inputActions"] as JArray;
            bool isOrdered = r.Value<bool?>("isOrdered") ?? false;

            // Toolbench는 2개 인풋만 의미 있음
            if (inputs.Count != 2) continue;

            if (mat == null || tool == null) continue;

            int[] assign = null;

            if (isOrdered)
            {
                if (!MatchSpecWithCount(mat, inputs[0] as JObject)) continue;
                if (!MatchSpecWithCount(tool, inputs[1] as JObject)) continue;
                assign = new[] { 0, 1 };
            }
            else
            {
                var presentIdx = new List<int>(2);
                if (slots[0] != null) presentIdx.Add(0);
                if (slots[1] != null) presentIdx.Add(1);

                assign = TryUnordered(inputs, slots, presentIdx);
                if (assign == null) continue;
            }

            // 입력액션 리맵(슬롯 인덱스 기준)
            remappedInputActions = new JArray { null, null };
            if (inActs != null)
            {
                for (int k = 0; k < inputs.Count; k++)
                {
                    int si = assign[k];
                    if (si >= 0 && si < 2 && k < inActs.Count)
                        remappedInputActions[si] = inActs[k];
                }
            }

            // candidates 생성
            var results = new List<ItemData>();
            for (int ci = 0; ci < candArray.Count; ci++)
            {
                var c = candArray[ci] as JObject;
                if (c == null) continue;

                string id = c.Value<string>("itemId");
                int cnt = c.Value<int?>("count") ?? 1;
                if (string.IsNullOrEmpty(id) || cnt <= 0) continue;

                var it = itemLibrary.Create(id, cnt);
                if (it != null)
                    results.Add(it);
            }

            if (results.Count == 0)
            {
                remappedInputActions = null;
                continue;
            }

            candidates = results;
            matchedRecipe = r;
            return true;
        }

        return false;
    }

    bool MatchSpecWithCount(ItemData it, JObject spec)
    {
        if (!MatchSpec(it, spec)) return false;
        int need = spec?.Value<int?>("count") ?? 1;
        if (it == null || it.Count < need) return false;
        return true;
    }

    /// <summary>
    /// ✅ 크루시블 layers에 합금 레시피를 적용한다.
    /// - BrickFurnace 등에서 "Molten X"를 layers에 커밋한 직후 호출.
    /// - 합금 레시피는 alloyJson 스키마를 사용(크래프팅 레시피와 별도).
    /// - 우선순위는 alloyJson 배열 순서(앞쪽이 우선).
    /// </summary>
    public bool TryApplyAlloysToCrucible(ItemData crucible)
    {
        if (_alloys.Count == 0) return false;
        if (crucible == null || crucible.Details == null) return false;
        if (!crucible.Details.TryGetValue("layers", out var layersObj) || layersObj == null) return false;

        if (layersObj is not List<object> layers) return false;

        bool changed = false;

        // 레시피 우선순위: 하나라도 적용되면 처음부터 다시 스캔(연쇄 합금)
        while (true)
        {
            bool applied = false;

            for (int r = 0; r < _alloys.Count; r++)
            {
                var recipe = _alloys[r];

                // 1) totals 집계
                var totals = new Dictionary<string, int>();
                for (int i = 0; i < layers.Count; i++)
                {
                    if (!TryReadLayer(layers[i], out var id, out var amt)) continue;
                    if (string.IsNullOrEmpty(id) || amt <= 0) continue;

                    if (totals.TryGetValue(id, out var cur)) totals[id] = cur + amt;
                    else totals[id] = amt;
                }

                // 2) batches 계산
                int batches = int.MaxValue;
                for (int i = 0; i < recipe.inputs.Count; i++)
                {
                    var (id, amt) = recipe.inputs[i];
                    totals.TryGetValue(id, out int have);

                    int b = have / amt;
                    if (b < batches) batches = b;
                    if (batches == 0) break;
                }

                if (batches <= 0 || batches == int.MaxValue)
                    continue;

                // 3) 소모(Top layer부터)
                for (int i = 0; i < recipe.inputs.Count; i++)
                {
                    var (id, amt) = recipe.inputs[i];
                    ConsumeFromTop(layers, id, batches * amt);
                }

                // 4) 결과 추가(Top 누적)
                AddOrStackAtTop(layers, recipe.outId, batches * recipe.outAmount);

                applied = true;
                changed = true;
                break;
            }

            if (!applied) break;
        }

        return changed;
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
    ///
    /// 액션 타입(신규 고정):
    /// - consume
    /// - set
    /// - copy
    /// - sum
    /// - delete
    ///
    /// 필드 루트(신규 고정, 대소문자 포함):
    /// name, spriteName, itemId, durability, maxDurability, tags,
    /// details, ToolActions, WeaponActions, BreakActions
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
            int filled = 0;
            for (int i = 0; i < 4; i++)
                if (slots[i] != null) filled++;

            // 1) 4-slot 레시피(_r4) 우선
            if (_r4 != null)
            {
                if (filled == 4)
                {
                    var set = FilterByInputCount(_r4, 4);
                    if (set.Count > 0 &&
                        TryMatchSet(set, slots, fourContext: true,
                                   out resultItems, out remappedInputActions, out matchedRecipe))
                        return true;

                    return false; // filled=4면 여기서 결론
                }

                if (filled == 3)
                {
                    var set = FilterByInputCount(_r4, 3);
                    if (set.Count > 0 &&
                        TryMatchSet(set, slots, fourContext: true,
                                   out resultItems, out remappedInputActions, out matchedRecipe))
                        return true;

                    return false; // filled=3면 여기서 결론
                }

                if (filled == 2)
                {
                    var set = FilterByInputCount(_r4, 2);
                    if (set.Count > 0 &&
                        TryMatchSet(set, slots, fourContext: true,
                                   out resultItems, out remappedInputActions, out matchedRecipe))
                        return true;
                }
                else if (filled == 1)
                {
                    var set = FilterByInputCount(_r4, 1);
                    if (set.Count > 0 &&
                        TryMatchSet(set, slots, fourContext: true,
                                   out resultItems, out remappedInputActions, out matchedRecipe))
                        return true;
                }
                else
                {
                    return false; // filled==0
                }
            }

            // 2) 실패 시 2-slot 레시피(_r2) fallback (상위→하위 허용)
            if (_r2 != null)
            {
                if (filled == 2)
                {
                    var set = FilterByInputCount(_r2, 2);
                    if (set.Count > 0 &&
                        TryMatchSet(set, slots, fourContext: true,
                                   out resultItems, out remappedInputActions, out matchedRecipe))
                        return true;
                }
                else if (filled == 1)
                {
                    var set = FilterByInputCount(_r2, 1);
                    if (set.Count > 0 &&
                        TryMatchSet(set, slots, fourContext: true,
                                   out resultItems, out remappedInputActions, out matchedRecipe))
                        return true;
                }
            }

            return false;
        }

        // ───────── 2슬롯 테이블 (Hand 등) ─────────
        if (n == 2)
        {
            // 하위(2슬롯)가 상위(4슬롯) 레시피를 매칭하는 일은 없어야 함 → _r2만 본다
            if (_r2 != null &&
                TryMatchSet(_r2, slots, fourContext: false,
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

            var outputsArray = r["outputs"] as JArray;
            if (outputsArray == null || outputsArray.Count == 0) continue;

            var inActs = r["inputActions"] as JArray;
            var oaRoot = r["outputActions"] as JArray; // null 가능

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

                string outId = outSpec.Value<string>("itemId");
                int outCnt = outSpec.Value<int?>("count") ?? 1;
                if (string.IsNullOrEmpty(outId) || outCnt <= 0) continue;

                var baseItem = itemLibrary.Create(outId, outCnt);
                if (baseItem == null) continue;

                // outputActions[oi] 는 JArray(액션 리스트)
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

            resultItems = results;
            matchedRecipe = r;
            return true;
        }

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
            var it = slots[i];
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
        var res = new int[inputs.Count];

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

    // 스펙 매칭(itemId/name/hasTag/ToolActions 조건)
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
                !string.Equals(it.Name, wantId, StringComparison.Ordinal))
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

        // ✅ 신규 고정 키: ToolActions
        var toolSpec = spec["ToolActions"];
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
    /// ToolActions 스펙 매칭.
    /// - spec 예시:
    ///   { "ToolActions": { "PercussionFlaking": {}, "X": { "foo": "bar" } } }
    ///   { "ToolActions": ["PercussionFlaking", "X"] }
    ///   { "ToolActions": "PercussionFlaking" }
    /// </summary>
    bool MatchToolActions(ItemData it, JToken toolSpec)
    {
        if (it.ToolActions == null || it.ToolActions.Count == 0)
            return false;

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

        if (toolSpec is not JObject obj)
        {
            string single = toolSpec.ToString();
            if (string.IsNullOrEmpty(single)) return false;
            return it.ToolActions.ContainsKey(single);
        }

        foreach (var prop in obj.Properties())
        {
            string toolName = prop.Name;
            if (string.IsNullOrEmpty(toolName))
                return false;

            if (!it.ToolActions.TryGetValue(toolName, out var cfgDict) || cfgDict == null)
                return false;

            if (prop.Value is not JObject wantCfg)
                continue;

            foreach (var cfgProp in wantCfg.Properties())
            {
                string key = cfgProp.Name;
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

    // 액션 딕셔너리 변환
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

        string single = v.ToString();
        if (string.IsNullOrEmpty(single))
            return null;

        return new Dictionary<string, Dictionary<string, object>>
        {
            { single, new Dictionary<string, object>() }
        };
    }

    // 출력액션 적용 (신규 스키마: consume/set/copy/sum/delete)
    ItemData ApplyOutputActions(ItemData dst, JArray outActs, List<ItemData> slots, int[] assign)
    {
        if (dst == null) return null;
        if (outActs == null || outActs.Count == 0) return dst;

        string overrideName = null;
        string overrideSprite = null;
        string overrideItemId = null;
        int? overrideDurability = null;
        int? overrideMaxDur = null;

        Dictionary<string, Dictionary<string, object>> overrideTool = null;
        Dictionary<string, Dictionary<string, object>> overrideWeapon = null;
        Dictionary<string, Dictionary<string, object>> overrideBreak = null;

        for (int i = 0; i < outActs.Count; i++)
        {
            var act = outActs[i] as JObject; if (act == null) continue;
            string type = act.Value<string>("type");
            if (string.IsNullOrEmpty(type)) continue;

            if (type == "set")
            {
                string field = act.Value<string>("field");
                if (string.IsNullOrEmpty(field)) continue;

                object val = null;
                bool hasVal = false;

                // 1) value
                if (act.TryGetValue("value", out var jv))
                {
                    hasVal = true;
                    if (jv.Type == JTokenType.Null) val = null;
                    else if (jv is JValue jvv) val = jvv.Value;
                    else val = jv.ToString();

                    if (val is string sv) val = ExpandTokens(sv);
                }
                // 2) fromInput + inputField
                else if (act.TryGetValue("fromInput", out var jf) && act.TryGetValue("inputField", out var jif))
                {
                    int? from = jf.Type == JTokenType.Null ? (int?)null : jf.Value<int?>();
                    string inputField = jif.Type == JTokenType.Null ? null : jif.ToString();

                    if (from.HasValue && !string.IsNullOrEmpty(inputField))
                    {
                        int si = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                        var src = (si >= 0 && si < slots.Count) ? slots[si] : null;

                        val = ReadField(src, inputField);
                        hasVal = true;

                        string strip = act.Value<string>("stripSuffix");
                        if (!string.IsNullOrEmpty(strip) && val is string s0 && s0.EndsWith(strip, StringComparison.Ordinal))
                            val = s0.Substring(0, s0.Length - strip.Length);
                    }
                }
                // 3) valueFromFields (join)
                else if (act["valueFromFields"] is JArray vff)
                {
                    string sep = act.Value<string>("separator") ?? "";
                    string pre = act.Value<string>("prefixEach") ?? "";
                    var vals = new List<string>(vff.Count);

                    for (int k = 0; k < vff.Count; k++)
                    {
                        string key = vff[k]?.ToString();
                        if (string.IsNullOrEmpty(key)) continue;

                        object v = ReadField(dst, key);
                        if (v == null) continue;

                        string s = v.ToString();
                        if (!string.IsNullOrEmpty(pre)) s = pre + s;
                        vals.Add(s);
                    }

                    val = string.Join(sep, vals);
                    hasVal = true;
                }

                if (!hasVal) continue;

                // top-level scalar
                if (field == "name") { overrideName = val?.ToString(); continue; }
                if (field == "spriteName") { overrideSprite = val?.ToString(); continue; }
                if (field == "itemId") { overrideItemId = val?.ToString(); continue; }

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

                // top-level dict replace
                if (field == "ToolActions" || field == "WeaponActions" || field == "BreakActions")
                {
                    var dict = ToActionDict(val);
                    if (field == "ToolActions") overrideTool = dict;
                    else if (field == "WeaponActions") overrideWeapon = dict;
                    else overrideBreak = dict;
                    continue;
                }

                // nested dict set
                if (field.StartsWith("details.", StringComparison.Ordinal))
                {
                    dst.SetDetailPath(field.Substring("details.".Length), val);
                    continue;
                }

                if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
                {
                    overrideTool = SetInActionRoot(overrideTool ?? dst.ToolActions, field.Substring("ToolActions.".Length), val);
                    continue;
                }

                if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
                {
                    overrideWeapon = SetInActionRoot(overrideWeapon ?? dst.WeaponActions, field.Substring("WeaponActions.".Length), val);
                    continue;
                }

                if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
                {
                    overrideBreak = SetInActionRoot(overrideBreak ?? dst.BreakActions, field.Substring("BreakActions.".Length), val);
                    continue;
                }

                continue;
            }
            else if (type == "copy")
            {
                int from = act.Value<int?>("fromInput") ?? -1;
                string inField = act.Value<string>("inputField");
                string toField = act.Value<string>("toField");
                if (from < 0 || string.IsNullOrEmpty(inField) || string.IsNullOrEmpty(toField)) continue;

                int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                var val = ReadField(src, inField);

                if (toField == "name") { overrideName = val?.ToString(); continue; }
                if (toField == "spriteName") { overrideSprite = val?.ToString(); continue; }
                if (toField == "itemId") { overrideItemId = val?.ToString(); continue; }

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

                if (toField == "ToolActions" || toField == "WeaponActions" || toField == "BreakActions")
                {
                    var dict = ToActionDict(val);
                    if (toField == "ToolActions") overrideTool = dict;
                    else if (toField == "WeaponActions") overrideWeapon = dict;
                    else overrideBreak = dict;
                    continue;
                }

                if (toField.StartsWith("details.", StringComparison.Ordinal))
                {
                    dst.SetDetailPath(toField.Substring("details.".Length), val);
                    continue;
                }

                if (toField.StartsWith("ToolActions.", StringComparison.Ordinal))
                {
                    overrideTool = SetInActionRoot(overrideTool ?? dst.ToolActions, toField.Substring("ToolActions.".Length), val);
                    continue;
                }

                if (toField.StartsWith("WeaponActions.", StringComparison.Ordinal))
                {
                    overrideWeapon = SetInActionRoot(overrideWeapon ?? dst.WeaponActions, toField.Substring("WeaponActions.".Length), val);
                    continue;
                }

                if (toField.StartsWith("BreakActions.", StringComparison.Ordinal))
                {
                    overrideBreak = SetInActionRoot(overrideBreak ?? dst.BreakActions, toField.Substring("BreakActions.".Length), val);
                    continue;
                }
            }
            else if (type == "sum")
            {
                string outField = act.Value<string>("field");
                string inField = act.Value<string>("inputField");
                var fromInputs = act["fromInputs"] as JArray;

                if (string.IsNullOrEmpty(outField) || string.IsNullOrEmpty(inField) || fromInputs == null)
                    continue;

                int sum = 0;
                for (int k = 0; k < fromInputs.Count; k++)
                {
                    int fi = fromInputs[k].Value<int>();
                    int si = (assign != null && fi >= 0 && fi < assign.Length) ? assign[fi] : -1;
                    var src = (si >= 0 && si < slots.Count) ? slots[si] : null;

                    var v = ReadField(src, inField);
                    if (v != null && int.TryParse(v.ToString(), out int iv))
                        sum += iv;
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

                if (outField.StartsWith("details.", StringComparison.Ordinal))
                {
                    dst.SetDetailPath(outField.Substring("details.".Length), sum);
                    continue;
                }
            }
            else if (type == "delete")
            {
                string field = act.Value<string>("field");
                if (string.IsNullOrEmpty(field)) continue;

                if (field == "ToolActions" || field == "WeaponActions" || field == "BreakActions")
                {
                    if (field == "ToolActions") overrideTool = new Dictionary<string, Dictionary<string, object>>();
                    else if (field == "WeaponActions") overrideWeapon = new Dictionary<string, Dictionary<string, object>>();
                    else overrideBreak = new Dictionary<string, Dictionary<string, object>>();
                    continue;
                }

                if (field.StartsWith("details.", StringComparison.Ordinal))
                {
                    DeleteFromDetails(dst, field.Substring("details.".Length));
                    continue;
                }

                if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
                {
                    overrideTool = DeleteFromActionRoot(overrideTool ?? dst.ToolActions, field.Substring("ToolActions.".Length));
                    continue;
                }

                if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
                {
                    overrideWeapon = DeleteFromActionRoot(overrideWeapon ?? dst.WeaponActions, field.Substring("WeaponActions.".Length));
                    continue;
                }

                if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
                {
                    overrideBreak = DeleteFromActionRoot(overrideBreak ?? dst.BreakActions, field.Substring("BreakActions.".Length));
                    continue;
                }
            }
        }

        bool changed =
            overrideName != null ||
            overrideSprite != null ||
            overrideItemId != null ||
            overrideDurability.HasValue ||
            overrideMaxDur.HasValue ||
            overrideTool != null ||
            overrideWeapon != null ||
            overrideBreak != null;

        if (!changed)
            return dst;

        string finalName = overrideName ?? dst.Name;
        string finalSprite = overrideSprite ?? dst.SpriteName;
        string finalId = overrideItemId ?? dst.ItemId;
        int finalMaxDur = overrideMaxDur ?? dst.MaxDurability;
        int finalDurability = overrideDurability ?? dst.Durability;

        var finalTool = overrideTool ?? dst.ToolActions;
        var finalWeapon = overrideWeapon ?? dst.WeaponActions;
        var finalBreak = overrideBreak ?? dst.BreakActions;

        var finalIcon = itemLibrary != null ? itemLibrary.GetSprite(finalSprite) : dst.Icon;
        var finalDetails = dst.Details;

        return new ItemData(
            itemId: finalId,
            name: finalName,
            spriteName: finalSprite,
            itemType: dst.ItemType,
            maxStack: dst.MaxStack,
            maxDurability: finalMaxDur,
            durability: finalDurability,
            toolActions: finalTool,
            weaponActions: finalWeapon,
            breakActions: finalBreak,
            tags: dst.Tags,
            details: finalDetails,
            icon: finalIcon,
            count: dst.Count
        );
    }

    // ─────────────────────────────────────────────────────────
    // Field Path (신규 고정 루트)
    // ─────────────────────────────────────────────────────────
    object ReadField(ItemData src, string field)
    {
        if (src == null || string.IsNullOrEmpty(field)) return null;

        // top-level scalar
        if (field == "name") return src.Name;
        if (field == "spriteName") return src.SpriteName;
        if (field == "itemId") return src.ItemId;
        if (field == "durability") return src.Durability;
        if (field == "maxDurability") return src.MaxDurability;
        if (field == "tags") return src.Tags;

        // top-level dict roots
        if (field == "details") return src.Details;
        if (field == "ToolActions") return src.ToolActions;
        if (field == "WeaponActions") return src.WeaponActions;
        if (field == "BreakActions") return src.BreakActions;

        // nested: details.*
        if (field.StartsWith("details.", StringComparison.Ordinal))
            return ReadFromDetails(src, field.Substring("details.".Length));

        // nested: ToolActions.* / WeaponActions.* / BreakActions.*
        if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(src.ToolActions, field.Substring("ToolActions.".Length));

        if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(src.WeaponActions, field.Substring("WeaponActions.".Length));

        if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(src.BreakActions, field.Substring("BreakActions.".Length));

        return null;
    }

    object ReadFromDetails(ItemData dst, string path)
    {
        if (dst?.Details == null || string.IsNullOrEmpty(path)) return null;

        var parts = path.Split('.');
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

    object ReadFromActionRoot(Dictionary<string, Dictionary<string, object>> root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;

        var parts = path.Split('.');
        if (parts.Length == 0) return null;

        // 1) action name
        string actionName = parts[0];
        if (!root.TryGetValue(actionName, out var paramDict) || paramDict == null)
            return null;

        if (parts.Length == 1)
            return paramDict;

        object curr = paramDict;

        // 2) inside param dict (Dictionary<string, object>) chain
        for (int i = 1; i < parts.Length; i++)
        {
            string key = parts[i];

            if (curr is Dictionary<string, object> d)
            {
                if (!d.TryGetValue(key, out curr))
                    return null;
            }
            else
            {
                return null;
            }
        }

        return curr;
    }

    // ─────────────────────────────────────────────────────────
    // Action dict mutation (copy-on-write)
    // ─────────────────────────────────────────────────────────
    Dictionary<string, Dictionary<string, object>> SetInActionRoot(
        Dictionary<string, Dictionary<string, object>> root,
        string path,
        object value)
    {
        if (root == null) root = new Dictionary<string, Dictionary<string, object>>();
        if (string.IsNullOrEmpty(path)) return root;

        var newRoot = root.ToDictionary(kv => kv.Key,
            kv => kv.Value != null ? new Dictionary<string, object>(kv.Value) : new Dictionary<string, object>());

        var parts = path.Split('.');
        if (parts.Length == 0) return newRoot;

        string actionName = parts[0];
        if (!newRoot.TryGetValue(actionName, out var param) || param == null)
            param = new Dictionary<string, object>();
        else
            param = new Dictionary<string, object>(param);

        if (parts.Length == 1)
        {
            if (value is Dictionary<string, object> d)
                param = new Dictionary<string, object>(d);
            newRoot[actionName] = param;
            return newRoot;
        }

        object curr = param;
        for (int i = 1; i < parts.Length - 1; i++)
        {
            string key = parts[i];

            if (curr is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(key, out var next) || next == null)
                {
                    var created = new Dictionary<string, object>();
                    dict[key] = created;
                    curr = created;
                }
                else if (next is Dictionary<string, object> nd)
                {
                    var copied = new Dictionary<string, object>(nd);
                    dict[key] = copied;
                    curr = copied;
                }
                else
                {
                    var created = new Dictionary<string, object>();
                    dict[key] = created;
                    curr = created;
                }
            }
            else
            {
                return newRoot;
            }
        }

        if (curr is Dictionary<string, object> last)
            last[parts[^1]] = value;

        newRoot[actionName] = param;
        return newRoot;
    }

    Dictionary<string, Dictionary<string, object>> DeleteFromActionRoot(
        Dictionary<string, Dictionary<string, object>> root,
        string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return root;

        var newRoot = root.ToDictionary(kv => kv.Key,
            kv => kv.Value != null ? new Dictionary<string, object>(kv.Value) : new Dictionary<string, object>());

        var parts = path.Split('.');
        if (parts.Length == 0) return newRoot;

        // 1) ToolActions.Combine (액션 자체 제거)
        if (parts.Length == 1)
        {
            newRoot.Remove(parts[0]);
            return newRoot;
        }

        // 2) ToolActions.Combine.xxx (param 내부 키 제거)
        string actionName = parts[0];
        if (!newRoot.TryGetValue(actionName, out var param) || param == null)
            return newRoot;

        var newParam = new Dictionary<string, object>(param);

        object curr = newParam;
        for (int i = 1; i < parts.Length - 1; i++)
        {
            string key = parts[i];

            if (curr is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(key, out var next) || next == null)
                    return newRoot;

                if (next is Dictionary<string, object> nd)
                {
                    var copied = new Dictionary<string, object>(nd);
                    dict[key] = copied;
                    curr = copied;
                }
                else
                {
                    return newRoot;
                }
            }
            else
            {
                return newRoot;
            }
        }

        if (curr is Dictionary<string, object> lastDict)
            lastDict.Remove(parts[^1]);

        newRoot[actionName] = newParam;
        return newRoot;
    }

    void DeleteFromDetails(ItemData dst, string path)
    {
        if (dst?.Details == null || string.IsNullOrEmpty(path)) return;

        var parts = path.Split('.');
        if (parts.Length == 0) return;

        if (parts.Length == 1)
        {
            dst.Details.Remove(parts[0]);
            return;
        }

        object curr = dst.Details;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (curr is Dictionary<string, object> d)
            {
                if (!d.TryGetValue(parts[i], out curr) || curr == null)
                    return;
            }
            else return;
        }

        if (curr is Dictionary<string, object> last)
            last.Remove(parts[^1]);
    }

    string ExpandTokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string rand = Guid.NewGuid().ToString("N").Substring(0, 6);
        return s.Replace("$timestamp$", ts).Replace("$rand$", rand);
    }

    // ─────────────────────────────────────────────────────────
    // ✅ Crucible layers helpers (합금 전용)
    // layers 원소 형태:
    //   - JObject: { "itemId": "Molten Copper", "amount": 9 }
    //   - Dictionary<string, object> 동일 키
    // ─────────────────────────────────────────────────────────
    bool TryReadLayer(object layerObj, out string itemId, out int amount)
    {
        itemId = null;
        amount = 0;

        if (layerObj is JObject jo)
        {
            itemId = jo.Value<string>("itemId");
            amount = jo.Value<int?>("amount") ?? 0;
            return true;
        }

        if (layerObj is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("itemId", out var idObj)) itemId = idObj as string;

            if (dict.TryGetValue("amount", out var amtObj))
            {
                if (amtObj is int i) amount = i;
                else if (amtObj is long l) amount = (int)l;
                else if (amtObj != null && int.TryParse(amtObj.ToString(), out int p)) amount = p;
            }

            return true;
        }

        return false;
    }

    void SetLayerAmount(List<object> layers, int index, int newAmount)
    {
        if (index < 0 || index >= layers.Count) return;

        if (layers[index] is JObject jo)
        {
            jo["amount"] = newAmount;
            return;
        }

        if (layers[index] is Dictionary<string, object> dict)
        {
            dict["amount"] = newAmount;
            return;
        }
    }

    void ConsumeFromTop(List<object> layers, string itemId, int need)
    {
        for (int i = layers.Count - 1; i >= 0 && need > 0; i--)
        {
            if (!TryReadLayer(layers[i], out var id, out var amt)) continue;
            if (id != itemId || amt <= 0) continue;

            int take = Mathf.Min(amt, need);
            int left = amt - take;
            need -= take;

            if (left <= 0) layers.RemoveAt(i);
            else SetLayerAmount(layers, i, left);
        }
    }

    void AddOrStackAtTop(List<object> layers, string itemId, int addAmount)
    {
        if (addAmount <= 0) return;

        if (layers.Count > 0 && TryReadLayer(layers[layers.Count - 1], out var id, out var amt) && id == itemId)
        {
            SetLayerAmount(layers, layers.Count - 1, amt + addAmount);
            return;
        }

        var jo = new JObject();
        jo["itemId"] = itemId;
        jo["amount"] = addAmount;
        layers.Add(jo);
    }
}
