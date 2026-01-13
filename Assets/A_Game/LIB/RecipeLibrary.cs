// RecipeLibrary.cs (전체 교체본)
// - isOrdered 제거, isShapeless(true/false)로 통일
// - shapeless: inputs는 "존재하는 만큼만" 나열(빈칸 null 불필요), filledCount == inputs.Count 일 때만 매칭
// - shaped(isShapeless=false):
//   * inputs는 "레시피 격자 크기(2/4/9/16) 그대로" 나열 (빈칸은 null로 표시)
//   * 회전/대칭(미러) 항상 허용
//   * 큰 테이블(9/16)에서 작은 격자(2/4/9) 레시피는 "슬라이딩" 가능
//   * 정책 A: 레시피 격자 밖(윈도우 밖) 슬롯은 전부 null 이어야 매칭
//
// - ✅ 2-slot shaped 레시피는 2x1(가로)로 취급하며, "유형 제작법(재료/툴 역할)" 보호를 위해 회전/대칭(미러) 변환을 허용하지 않음.

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
    public TextAsset recipe2Json;  // 2-slot
    public TextAsset recipe4Json;  // 4-slot
    public TextAsset recipe9Json;  // 9-slot (Forge)
    public TextAsset recipe16Json; // 16-slot (Industrial)

    [Header("Alloy Jsons")]
    public TextAsset alloyJson;   // 합금(크루시블) 전용

    [Header("Toolbench Jsons")]
    public TextAsset toolbenchJson; // Toolbench 전용 (candidates 스키마)

    JArray _r2;
    JArray _r4;
    JArray _r9;
    JArray _r16;

    JArray _toolbench;

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
        if (recipe9Json != null && !string.IsNullOrEmpty(recipe9Json.text))
            _r9 = JArray.Parse(recipe9Json.text);
        if (recipe16Json != null && !string.IsNullOrEmpty(recipe16Json.text))
            _r16 = JArray.Parse(recipe16Json.text);

        LoadAlloys();
        LoadToolbench();
    }

    void LoadToolbench()
    {
        _toolbench = null;
        if (toolbenchJson == null || string.IsNullOrEmpty(toolbenchJson.text))
            return;

        try
        {
            _toolbench = JArray.Parse(toolbenchJson.text);
        }
        catch
        {
            try
            {
                var jo = JObject.Parse(toolbenchJson.text);
                if (jo.TryGetValue("Toolbench", out var tok) && tok is JArray arr)
                    _toolbench = arr;
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

    // ─────────────────────────────────────────────────────────
    // Toolbench candidates
    // ─────────────────────────────────────────────────────────
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

            var candArray = r["candidates"] as JArray;
            if (candArray == null) candArray = r["outputs"] as JArray;
            if (candArray == null || candArray.Count == 0) continue;

            // NEW: inputs object
            if (r["inputs"] is JObject inObj)
            {
                var matSpec = inObj["material"] as JObject;
                var toolSpec = inObj["tool"] as JObject;

                if (matSpec != null && toolSpec != null)
                {
                    if (mat == null || tool == null) continue;
                    if (!MatchSpecWithCount(mat, matSpec)) continue;
                    if (!MatchSpecWithCount(tool, toolSpec)) continue;

                    remappedInputActions = new JArray { null, null };
                    if (r["inputActions"] is JObject actObj)
                    {
                        remappedInputActions[0] = actObj.TryGetValue("material", out var am) ? am : null;
                        remappedInputActions[1] = actObj.TryGetValue("tool", out var at) ? at : null;
                    }

                    var results = CreateItemsFromArray(candArray);
                    if (results.Count == 0) { remappedInputActions = null; continue; }

                    candidates = results;
                    matchedRecipe = r;
                    return true;
                }
            }

            // LEGACY: inputs array
            var inputs = r["inputs"] as JArray;
            if (inputs == null || inputs.Count != 2) continue;

            var inActs = r["inputActions"] as JArray;

            // isShapeless 우선, 없으면 isOrdered(legacy)로 환산
            bool isShapeless = r.Value<bool?>("isShapeless")
                               ?? !(r.Value<bool?>("isOrdered") ?? true);

            if (mat == null || tool == null) continue;

            int[] assign = null;

            if (!isShapeless)
            {
                // shaped: inputs[0] -> slot0, inputs[1] -> slot1
                if (!MatchSpecWithCount(mat, inputs[0] as JObject)) continue;
                if (!MatchSpecWithCount(tool, inputs[1] as JObject)) continue;
                assign = new[] { 0, 1 };
            }
            else
            {
                // shapeless: 두 슬롯 무순서
                var presentIdx = new List<int>(2);
                if (slots[0] != null) presentIdx.Add(0);
                if (slots[1] != null) presentIdx.Add(1);

                assign = TryUnorderedShapeless(inputs, slots, presentIdx);
                if (assign == null) continue;
            }

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

            var resultsLegacy = CreateItemsFromArray(candArray);
            if (resultsLegacy.Count == 0) { remappedInputActions = null; continue; }

            candidates = resultsLegacy;
            matchedRecipe = r;
            return true;
        }

        return false;
    }

    List<ItemData> CreateItemsFromArray(JArray arr)
    {
        var results = new List<ItemData>();
        for (int i = 0; i < arr.Count; i++)
        {
            var o = arr[i] as JObject;
            if (o == null) continue;

            string id = o.Value<string>("itemId");
            int cnt = o.Value<int?>("count") ?? 1;
            if (string.IsNullOrEmpty(id) || cnt <= 0) continue;

            var it = itemLibrary.Create(id, cnt);
            if (it != null) results.Add(it);
        }
        return results;
    }

    // ─────────────────────────────────────────────────────────
    // Craft
    // ─────────────────────────────────────────────────────────
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

        // 우선순위: 현재 테이블 크기 레시피 → 더 작은 격자 레시피(슬라이딩)
        if (n == 16)
        {
            if (TryMatchSet(_r16, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            if (TryMatchSet(_r9,  slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            if (TryMatchSet(_r4,  slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            if (TryMatchSet(_r2,  slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            return false;
        }

        if (n == 9)
        {
            if (TryMatchSet(_r9, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            if (TryMatchSet(_r4, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            if (TryMatchSet(_r2, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            return false;
        }

        if (n == 4)
        {
            if (TryMatchSet(_r4, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            if (TryMatchSet(_r2, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            return false;
        }

        if (n == 2)
        {
            if (TryMatchSet(_r2, slots, out resultItems, out remappedInputActions, out matchedRecipe)) return true;
            return false;
        }

        return false;
    }

    bool TryMatchSet(
        JArray recipeSet,
        List<ItemData> slots,
        out List<ItemData> resultItems,
        out JArray remappedInputActions,
        out JObject matchedRecipe)
    {
        resultItems = null;
        remappedInputActions = null;
        matchedRecipe = null;

        if (recipeSet == null || recipeSet.Count == 0) return false;

        var presentIdx = new List<int>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
            if (slots[i] != null) presentIdx.Add(i);

        for (int rix = 0; rix < recipeSet.Count; rix++)
        {
            var r = recipeSet[rix] as JObject;
            if (r == null) continue;

            var inputs = r["inputs"] as JArray;
            if (inputs == null || inputs.Count == 0) continue;

            bool isShapeless = r.Value<bool?>("isShapeless") ?? false;

            var outputsArray = r["outputs"] as JArray;
            if (outputsArray == null || outputsArray.Count == 0) continue;

            var inActs = r["inputActions"] as JArray;
            var oaRoot = r["outputActions"] as JArray; // output별 action list (null 가능)

            int[] assign = null;

            if (isShapeless)
            {
                // shapeless: inputs는 "존재하는 만큼만" → filledCount == inputs.Count
                if (presentIdx.Count != inputs.Count) continue;

                assign = TryUnorderedShapeless(inputs, slots, presentIdx);
                if (assign == null) continue;
            }
            else
            {
                // shaped: inputs는 격자 크기(2/4/9/16) 그대로. null로 빈칸 표현 가능.
                if (!TryMatchShapedWithSlidingAndTransforms(inputs, slots, out assign))
                    continue;
            }

            // inputConditions 평가
            var conds = r["inputConditions"] as JArray;
            if (conds != null && conds.Count > 0)
            {
                if (!EvalAllConditions(conds, slots, assign))
                    continue;
            }

            // inputActions 리맵 (슬롯 인덱스 기준)
            remappedInputActions = new JArray();
            for (int i = 0; i < slots.Count; i++) remappedInputActions.Add(null);

            if (inActs != null)
            {
                for (int k = 0; k < Math.Min(inputs.Count, inActs.Count); k++)
                {
                    int si = (assign != null && k < assign.Length) ? assign[k] : -1;
                    if (si >= 0 && si < slots.Count)
                        remappedInputActions[si] = inActs[k];
                }
            }

            NormalizeInputActions(remappedInputActions, slots, assign);

            // outputs 생성 + outputActions 적용
            var results = new List<ItemData>();

            for (int oi = 0; oi < outputsArray.Count; oi++)
            {
                var outSpec = outputsArray[oi] as JObject;
                if (outSpec == null) continue;

                string outId = outSpec.Value<string>("itemId");
                int outCnt = outSpec.Value<int?>("count") ?? 1;
                if (string.IsNullOrEmpty(outId) || outCnt <= 0) continue;

                JArray perActs = null;
                if (oaRoot != null && oi < oaRoot.Count && oaRoot[oi] is JArray ja)
                    perActs = ja;

                ItemData baseItem = null;
                if (!string.Equals(outId, "@dynamic", StringComparison.Ordinal))
                {
                    baseItem = itemLibrary.Create(outId, outCnt);
                    if (baseItem == null) continue;
                }

                var finalItem = ApplyOutputActions(baseItem, perActs, slots, assign, outCnt);
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

    // ─────────────────────────────────────────────────────────
    // Matching: Shapeless
    // ─────────────────────────────────────────────────────────
    int[] TryUnorderedShapeless(JArray inputs, List<ItemData> slots, List<int> presentIdx)
    {
        var used = new HashSet<int>();
        var res = new int[inputs.Count];

        bool Dfs(int idx)
        {
            if (idx >= inputs.Count) return true;

            var spec = inputs[idx] as JObject;
            if (spec == null) return false;

            foreach (var si in presentIdx)
            {
                if (used.Contains(si)) continue;

                var it = slots[si];
                if (!MatchSpecWithCount(it, spec)) continue;

                used.Add(si);
                res[idx] = si;
                if (Dfs(idx + 1)) return true;
                used.Remove(si);
            }
            return false;
        }

        return Dfs(0) ? res : null;
    }

    // ─────────────────────────────────────────────────────────
    // Matching: Shaped (Sliding + Rotate/Reflect)
    // ─────────────────────────────────────────────────────────
    struct GridSpec
    {
        public int w, h;
        public JObject[] cells; // length = w*h (null allowed)
    }

    bool TryMatchShapedWithSlidingAndTransforms(JArray recipeInputs, List<ItemData> slots, out int[] assign)
    {
        assign = null;

        if (!TryGetGridSizeFromCount(recipeInputs.Count, out int rw, out int rh))
            return false;

        if (!TryGetGridSizeFromCount(slots.Count, out int tw, out int th))
            return false;

        if (rw > tw || rh > th) return false;

        // recipe grid parse
        var baseGrid = new GridSpec
        {
            w = rw,
            h = rh,
            cells = new JObject[rw * rh]
        };

        for (int i = 0; i < rw * rh; i++)
        {
            // shaped: null은 "빈칸" 의미
            if (i >= recipeInputs.Count) { baseGrid.cells[i] = null; continue; }
            baseGrid.cells[i] = recipeInputs[i] as JObject; // null 가능
        }

        // ✅ 2-slot shaped는 "유형 제작법" 보호: transforms(회전/대칭) 금지
        List<GridSpec> variants;
        if (recipeInputs.Count == 2)
        {
            variants = new List<GridSpec>(1) { baseGrid };
        }
        else
        {
            variants = GenerateUniqueTransforms(baseGrid);
        }

        // slide over target grid
        for (int oy = 0; oy <= th - rh; oy++)
        {
            for (int ox = 0; ox <= tw - rw; ox++)
            {
                for (int vi = 0; vi < variants.Count; vi++)
                {
                    var g = variants[vi];

                    if (TryMatchShapedAt(slots, tw, th, g, ox, oy, out assign))
                        return true;
                }
            }
        }

        return false;
    }

    bool TryMatchShapedAt(List<ItemData> slots, int tw, int th, GridSpec g, int ox, int oy, out int[] assign)
    {
        assign = new int[g.w * g.h];
        for (int i = 0; i < assign.Length; i++) assign[i] = -1;

        // 정책 A: 윈도우 밖은 전부 null이어야 함
        for (int ty = 0; ty < th; ty++)
        {
            for (int tx = 0; tx < tw; tx++)
            {
                int tIndex = ToIndex(tx, ty, tw);
                var tItem = slots[tIndex];

                bool inside = (tx >= ox && tx < ox + g.w && ty >= oy && ty < oy + g.h);
                if (!inside)
                {
                    if (tItem != null) return false;
                    continue;
                }

                int rx = tx - ox;
                int ry = ty - oy;
                int rIndex = ToIndex(rx, ry, g.w);
                var spec = g.cells[rIndex];

                if (spec == null)
                {
                    if (tItem != null) return false;
                    continue;
                }

                if (!MatchSpecWithCount(tItem, spec))
                    return false;

                // assign: recipe-input-index(=rIndex) -> target-slot-index
                assign[rIndex] = tIndex;
            }
        }

        return true;
    }

    int ToIndex(int x, int y, int w) => y * w + x;

    bool TryGetGridSizeFromCount(int count, out int w, out int h)
    {
        w = h = 0;

        // 2-slot shaped는 2x1(가로)로 취급
        if (count == 2) { w = 2; h = 1; return true; }

        int s = Mathf.RoundToInt(Mathf.Sqrt(count));
        if (s * s == count)
        {
            w = s; h = s; return true;
        }

        return false;
    }

    List<GridSpec> GenerateUniqueTransforms(GridSpec baseGrid)
    {
        var list = new List<GridSpec>();
        var seen = new HashSet<string>();

        // 4 rotations x (mirror or not) = up to 8 variants
        for (int rot = 0; rot < 4; rot++)
        {
            var r = Rotate(baseGrid, rot);

            for (int mir = 0; mir < 2; mir++)
            {
                var v = (mir == 0) ? r : MirrorX(r);
                var key = SerializeSpec(v);
                if (seen.Add(key))
                    list.Add(v);
            }
        }

        return list;
    }

    GridSpec Rotate(GridSpec g, int rot90Count)
    {
        rot90Count = ((rot90Count % 4) + 4) % 4;
        if (rot90Count == 0) return g;

        GridSpec cur = g;
        for (int t = 0; t < rot90Count; t++)
            cur = Rotate90(cur);

        return cur;
    }

    GridSpec Rotate90(GridSpec g)
    {
        // (x,y) -> (h-1-y, x)
        var ng = new GridSpec
        {
            w = g.h,
            h = g.w,
            cells = new JObject[g.w * g.h]
        };

        for (int y = 0; y < g.h; y++)
        {
            for (int x = 0; x < g.w; x++)
            {
                int src = ToIndex(x, y, g.w);
                int nx = g.h - 1 - y;
                int ny = x;
                int dst = ToIndex(nx, ny, ng.w);
                ng.cells[dst] = g.cells[src];
            }
        }

        return ng;
    }

    GridSpec MirrorX(GridSpec g)
    {
        // horizontal mirror: (x,y) -> (w-1-x, y)
        var ng = new GridSpec
        {
            w = g.w,
            h = g.h,
            cells = new JObject[g.w * g.h]
        };

        for (int y = 0; y < g.h; y++)
        {
            for (int x = 0; x < g.w; x++)
            {
                int src = ToIndex(x, y, g.w);
                int nx = g.w - 1 - x;
                int dst = ToIndex(nx, y, g.w);
                ng.cells[dst] = g.cells[src];
            }
        }

        return ng;
    }

    string SerializeSpec(GridSpec g)
    {
        // null/비-null 및 핵심 키만으로 간단 fingerprint
        // (충돌 가능성은 낮고, 충돌 시 중복 variant만 늘어남)
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append(g.w).Append("x").Append(g.h).Append("|");
        for (int i = 0; i < g.cells.Length; i++)
        {
            var s = g.cells[i];
            if (s == null) { sb.Append("_;"); continue; }

            sb.Append(s.Value<string>("itemId") ?? "");
            sb.Append("#");
            sb.Append(s.Value<int?>("count") ?? 1);
            sb.Append("#");

            // ToolActions 존재 유무/이름
            if (s["ToolActions"] is JObject ta)
            {
                foreach (var p in ta.Properties())
                    sb.Append("TA:").Append(p.Name).Append(",");
            }
            sb.Append(";");
        }
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────
    // Spec matching
    // ─────────────────────────────────────────────────────────
    bool MatchSpecWithCount(ItemData it, JObject spec)
    {
        if (!MatchSpec(it, spec)) return false;
        int need = spec?.Value<int?>("count") ?? 1;
        if (it == null || it.Count < need) return false;
        return true;
    }

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

        var toolSpec = spec["ToolActions"];
        if (toolSpec != null && toolSpec.Type != JTokenType.Null)
        {
            constraints++;
            if (it == null) return false;
            if (!MatchToolActions(it, toolSpec)) return false;
        }

        return constraints > 0;
    }

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

    // ─────────────────────────────────────────────────────────
    // output actions / conditions / expressions (기존 유지)
    // ─────────────────────────────────────────────────────────
    ItemData ApplyOutputActions(ItemData dst, JArray outActs, List<ItemData> slots, int[] assign, int outCount)
    {
        if (outActs == null || outActs.Count == 0) return dst;

        // @dynamic: create 먼저
        if (dst == null)
        {
            ItemData created = null;

            for (int i = 0; i < outActs.Count; i++)
            {
                var act = outActs[i] as JObject;
                if (act == null) continue;
                if (act.Value<string>("type") != "create") continue;

                string from = act.Value<string>("from");
                if (string.IsNullOrEmpty(from)) continue;

                object moltenIdObj = ResolveExpr(from, slots, assign);
                string moltenId = moltenIdObj?.ToString();
                if (string.IsNullOrEmpty(moltenId)) continue;

                string stripPrefix = act.Value<string>("stripPrefix");
                string metal = moltenId;

                if (!string.IsNullOrEmpty(stripPrefix) && metal.StartsWith(stripPrefix, StringComparison.Ordinal))
                    metal = metal.Substring(stripPrefix.Length);

                string prefix = ResolveExprToString(act.Value<string>("prefixFrom"), slots, assign);
                string suffix = ResolveExprToString(act.Value<string>("suffixFrom"), slots, assign);

                string createdItemId = BuildId(prefix, metal, suffix);
                if (string.IsNullOrEmpty(createdItemId)) continue;

                created = itemLibrary.Create(createdItemId, outCount);
                break;
            }

            dst = created;
            if (dst == null) return null;
        }

        // 이 아래 로직은 사용하던 스키마 그대로 유지 (set/copy/sum/delete)
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
            var act = outActs[i] as JObject;
            if (act == null) continue;

            string type = act.Value<string>("type");
            if (string.IsNullOrEmpty(type)) continue;

            if (type == "create") continue;

            if (type == "set")
            {
                string field = act.Value<string>("field");
                if (string.IsNullOrEmpty(field)) continue;

                object val = null;
                bool hasVal = false;

                if (act.TryGetValue("value", out var jv))
                {
                    hasVal = true;
                    if (jv.Type == JTokenType.Null) val = null;
                    else if (jv is JValue jvv) val = jvv.Value;
                    else val = jv.ToString();

                    if (val is string sv) val = ExpandTokens(sv);
                }
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

                if (field == "ToolActions" || field == "WeaponActions" || field == "BreakActions")
                {
                    var dict = ToActionDict(val);
                    if (field == "ToolActions") overrideTool = dict;
                    else if (field == "WeaponActions") overrideWeapon = dict;
                    else overrideBreak = dict;
                    continue;
                }

                if (field.StartsWith("details.", StringComparison.Ordinal))
                {
                    SetDetailPath(dst, field.Substring("details.".Length), val);
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
                    SetDetailPath(dst, toField.Substring("details.".Length), val);
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
                    SetDetailPath(dst, outField.Substring("details.".Length), sum);
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
    // inputConditions / expression
    // ─────────────────────────────────────────────────────────
    bool EvalAllConditions(JArray conds, List<ItemData> slots, int[] assign)
    {
        for (int i = 0; i < conds.Count; i++)
        {
            var c = conds[i] as JObject;
            if (c == null) return false;

            string path = c.Value<string>("path");
            string op = c.Value<string>("op");
            string rhs = c.Value<string>("rhs");

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(op) || string.IsNullOrEmpty(rhs))
                return false;

            object lObj = ResolveExpr(path, slots, assign);
            object rObj = ResolveExpr(rhs, slots, assign);

            if (!Compare(lObj, op, rObj))
                return false;
        }

        return true;
    }

    bool Compare(object left, string op, object right)
    {
        bool lNum = TryToNumber(left, out double ln);
        bool rNum = TryToNumber(right, out double rn);

        if (lNum && rNum)
        {
            switch (op)
            {
                case ">=": return ln >= rn;
                case ">": return ln > rn;
                case "<=": return ln <= rn;
                case "<": return ln < rn;
                case "==": return Math.Abs(ln - rn) < 0.000001;
                case "!=": return Math.Abs(ln - rn) >= 0.000001;
                default: return false;
            }
        }

        string ls = left?.ToString();
        string rs = right?.ToString();

        if (op == "==") return string.Equals(ls, rs, StringComparison.Ordinal);
        if (op == "!=") return !string.Equals(ls, rs, StringComparison.Ordinal);

        return false;
    }

    bool TryToNumber(object v, out double num)
    {
        num = 0;

        if (v == null) return false;
        if (v is int i) { num = i; return true; }
        if (v is long l) { num = l; return true; }
        if (v is float f) { num = f; return true; }
        if (v is double d) { num = d; return true; }

        if (v is JValue jv)
        {
            if (jv.Value == null) return false;
            return TryToNumber(jv.Value, out num);
        }

        return double.TryParse(v.ToString(), out num);
    }

    void NormalizeInputActions(JArray remapped, List<ItemData> slots, int[] assign)
    {
        if (remapped == null) return;

        for (int i = 0; i < remapped.Count; i++)
        {
            if (remapped[i] is not JObject act) continue;

            string type = act.Value<string>("type");
            if (string.IsNullOrEmpty(type)) continue;

            if (type == "consumeMetal")
            {
                var amtTok = act["amount"];
                if (amtTok == null) continue;

                if (amtTok.Type == JTokenType.String)
                {
                    string expr = amtTok.ToString();
                    object v = ResolveExpr(expr, slots, assign);
                    if (TryToNumber(v, out double dn))
                        act["amount"] = (int)Math.Round(dn);
                }
            }
        }
    }

    object ResolveExpr(string expr, List<ItemData> slots, int[] assign)
    {
        if (string.IsNullOrEmpty(expr)) return null;

        if (int.TryParse(expr, out int iv)) return iv;

        if (expr.StartsWith("inputs[", StringComparison.Ordinal))
        {
            int close = expr.IndexOf(']');
            if (close <= 6) return null;

            string idxStr = expr.Substring(7, close - 7);
            if (!int.TryParse(idxStr, out int recipeInputIndex)) return null;

            int si = (assign != null && recipeInputIndex >= 0 && recipeInputIndex < assign.Length) ? assign[recipeInputIndex] : -1;
            ItemData it = (si >= 0 && si < slots.Count) ? slots[si] : null;
            if (it == null) return null;

            string rest = expr.Substring(close + 1);
            if (string.IsNullOrEmpty(rest)) return it;

            if (rest.StartsWith(".", StringComparison.Ordinal))
                rest = rest.Substring(1);

            return ResolveOnItem(it, rest);
        }

        return null;
    }

    string ResolveExprToString(string expr, List<ItemData> slots, int[] assign)
    {
        if (string.IsNullOrEmpty(expr)) return null;
        object v = ResolveExpr(expr, slots, assign);
        return v?.ToString();
    }

    object ResolveOnItem(ItemData it, string path)
    {
        if (it == null || string.IsNullOrEmpty(path)) return null;

        if (path == "name") return it.Name;
        if (path == "spriteName") return it.SpriteName;
        if (path == "itemId") return it.ItemId;
        if (path == "durability") return it.Durability;
        if (path == "maxDurability") return it.MaxDurability;
        if (path == "tags") return it.Tags;

        if (path.StartsWith("ToolActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(it.ToolActions, path.Substring("ToolActions.".Length));

        if (path.StartsWith("WeaponActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(it.WeaponActions, path.Substring("WeaponActions.".Length));

        if (path.StartsWith("BreakActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(it.BreakActions, path.Substring("BreakActions.".Length));

        if (path.StartsWith("details.", StringComparison.Ordinal))
            return ResolveFromDetails(it, path.Substring("details.".Length));

        if (path == "details")
            return it.Details;

        return null;
    }

    object ResolveFromDetails(ItemData it, string path)
    {
        if (it?.Details == null || string.IsNullOrEmpty(path)) return null;

        object curr = it.Details;
        var parts = path.Split('.');

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];

            string key = part;
            int? index = null;

            int lb = part.IndexOf('[');
            if (lb >= 0)
            {
                int rb = part.IndexOf(']', lb + 1);
                if (rb > lb)
                {
                    key = part.Substring(0, lb);
                    string idxStr = part.Substring(lb + 1, rb - lb - 1);
                    if (int.TryParse(idxStr, out int idx))
                        index = idx;
                }
            }

            if (!string.IsNullOrEmpty(key))
            {
                if (!TryGetFromMap(curr, key, out var next))
                    return null;
                curr = next;
            }

            if (index.HasValue)
            {
                int idx = index.Value;

                if (curr is JArray ja)
                {
                    int real = idx < 0 ? ja.Count + idx : idx;
                    if (real < 0 || real >= ja.Count) return null;
                    curr = ja[real];
                }
                else if (curr is List<object> list)
                {
                    int real = idx < 0 ? list.Count + idx : idx;
                    if (real < 0 || real >= list.Count) return null;
                    curr = list[real];
                }
                else if (curr is System.Collections.IList ilist)
                {
                    int real = idx < 0 ? ilist.Count + idx : idx;
                    if (real < 0 || real >= ilist.Count) return null;
                    curr = ilist[real];
                }
                else
                {
                    return null;
                }
            }

            if (curr is JValue jv)
                curr = jv.Value;
        }

        if (curr is JValue jvv) return jvv.Value;
        return curr;
    }

    bool TryGetFromMap(object curr, string key, out object value)
    {
        value = null;

        if (curr is Dictionary<string, object> dict)
        {
            if (!dict.TryGetValue(key, out value))
                return false;
            return true;
        }

        if (curr is JObject jo)
        {
            if (!jo.TryGetValue(key, out var tok))
                return false;

            value = tok is JValue jv ? jv.Value : tok;
            return true;
        }

        return false;
    }

    string BuildId(string prefix, string metal, string suffix)
    {
        string p = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
        string m = string.IsNullOrWhiteSpace(metal) ? null : metal.Trim();
        string s = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();

        if (string.IsNullOrEmpty(m)) return null;

        if (!string.IsNullOrEmpty(p) && !string.IsNullOrEmpty(s)) return $"{p} {m} {s}";
        if (!string.IsNullOrEmpty(p)) return $"{p} {m}";
        if (!string.IsNullOrEmpty(s)) return $"{m} {s}";
        return m;
    }

    // ─────────────────────────────────────────────────────────
    // Field reads + mutations
    // ─────────────────────────────────────────────────────────
    object ReadField(ItemData src, string field)
    {
        if (src == null || string.IsNullOrEmpty(field)) return null;

        if (field == "name") return src.Name;
        if (field == "spriteName") return src.SpriteName;
        if (field == "itemId") return src.ItemId;
        if (field == "durability") return src.Durability;
        if (field == "maxDurability") return src.MaxDurability;
        if (field == "tags") return src.Tags;

        if (field == "details") return src.Details;
        if (field == "ToolActions") return src.ToolActions;
        if (field == "WeaponActions") return src.WeaponActions;
        if (field == "BreakActions") return src.BreakActions;

        if (field.StartsWith("details.", StringComparison.Ordinal))
            return ResolveFromDetails(src, field.Substring("details.".Length));

        if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(src.ToolActions, field.Substring("ToolActions.".Length));

        if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(src.WeaponActions, field.Substring("WeaponActions.".Length));

        if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
            return ReadFromActionRoot(src.BreakActions, field.Substring("BreakActions.".Length));

        return null;
    }

    object ReadFromActionRoot(Dictionary<string, Dictionary<string, object>> root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path)) return null;

        var parts = path.Split('.');
        if (parts.Length == 0) return null;

        string actionName = parts[0];
        if (!root.TryGetValue(actionName, out var paramDict) || paramDict == null)
            return null;

        if (parts.Length == 1)
            return paramDict;

        object curr = paramDict;

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

    void SetDetailPath(ItemData dst, string path, object value)
    {
        if (dst?.Details == null || string.IsNullOrEmpty(path)) return;

        var parts = path.Split('.');
        object curr = dst.Details;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string key = parts[i];

            if (curr is Dictionary<string, object> d)
            {
                if (!d.TryGetValue(key, out var next) || next == null)
                {
                    var created = new Dictionary<string, object>();
                    d[key] = created;
                    curr = created;
                }
                else if (next is Dictionary<string, object> nd)
                {
                    curr = nd;
                }
                else
                {
                    var created = new Dictionary<string, object>();
                    d[key] = created;
                    curr = created;
                }
            }
            else return;
        }

        if (curr is Dictionary<string, object> last)
            last[parts[^1]] = value;
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

    // ─────────────────────────────────────────────────────────
    // Action dict helpers (copy-on-write)
    // ─────────────────────────────────────────────────────────
    Dictionary<string, Dictionary<string, object>> ToActionDict(object v)
    {
        if (v == null) return null;

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
        if (string.IsNullOrEmpty(single)) return null;

        return new Dictionary<string, Dictionary<string, object>>
        {
            { single, new Dictionary<string, object>() }
        };
    }

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
            else return newRoot;
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

        if (parts.Length == 1)
        {
            newRoot.Remove(parts[0]);
            return newRoot;
        }

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
                else return newRoot;
            }
            else return newRoot;
        }

        if (curr is Dictionary<string, object> lastDict)
            lastDict.Remove(parts[^1]);

        newRoot[actionName] = newParam;
        return newRoot;
    }

    string ExpandTokens(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string rand = Guid.NewGuid().ToString("N").Substring(0, 6);
        return s.Replace("$timestamp$", ts).Replace("$rand$", rand);
    }

    // ─────────────────────────────────────────────────────────
    // Alloy / Crucible layers (원본 유지)
    // ─────────────────────────────────────────────────────────
    public bool TryApplyAlloysToCrucible(ItemData crucible)
    {
        if (_alloys.Count == 0) return false;
        if (crucible == null || crucible.Details == null) return false;
        if (!crucible.Details.TryGetValue("layers", out var layersObj) || layersObj == null) return false;
        if (layersObj is not System.Collections.IList layers) return false;

        bool changed = false;

        while (true)
        {
            bool applied = false;

            for (int r = 0; r < _alloys.Count; r++)
            {
                var recipe = _alloys[r];

                var totals = new Dictionary<string, int>();
                for (int i = 0; i < layers.Count; i++)
                {
                    if (!TryReadLayer(layers[i], out var id, out var amt)) continue;
                    if (string.IsNullOrEmpty(id) || amt <= 0) continue;

                    if (totals.TryGetValue(id, out var cur)) totals[id] = cur + amt;
                    else totals[id] = amt;
                }

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

                for (int i = 0; i < recipe.inputs.Count; i++)
                {
                    var (id, amt) = recipe.inputs[i];
                    ConsumeFromTop(layers, id, batches * amt);
                }

                AddOrStackAtTop(layers, recipe.outId, batches * recipe.outAmount);

                applied = true;
                changed = true;
                break;
            }

            if (!applied) break;
        }

        return changed;
    }

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

    void SetLayerAmount(System.Collections.IList layers, int index, int newAmount)
    {
        if (layers == null) return;
        if (index < 0 || index >= layers.Count) return;

        var elem = layers[index];

        if (elem is JObject jo)
        {
            jo["amount"] = newAmount;
            return;
        }

        if (elem is Dictionary<string, object> dict)
        {
            dict["amount"] = newAmount;
            return;
        }
    }

    void ConsumeFromTop(System.Collections.IList layers, string itemId, int need)
    {
        if (layers == null) return;

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

    void AddOrStackAtTop(System.Collections.IList layers, string itemId, int addAmount)
    {
        if (layers == null) return;
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
