using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Game.Core;


namespace Game.Data
{
    public partial class RecipeLibrary
    {
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        // Craft
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
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
    
            // ??⑥ろ맖??戮곕쭊: ?熬곣뫗?????逾?????????곕뻣????????? ?롪봇維?????곕뻣??????????
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
                var oaRoot = r["outputActions"] as JArray; // output??action list (null ?띠럾???
    
                int[] assign = null;
    
                if (isShapeless)
                {
                    // shapeless: inputs??"?브퀡????濡ル츎 嶺뚮씭??칰?彛? ??filledCount == inputs.Count
                    if (presentIdx.Count != inputs.Count) continue;
    
                    assign = TryUnorderedShapeless(inputs, slots, presentIdx);
                    if (assign == null) continue;
                }
                else
                {
                    // shaped: inputs???롪봇維??????2/4/9/16) ?잙갭梨??? null?????녻맱????ш껑 ?띠럾???
                    if (!TryMatchShapedWithSlidingAndTransforms(inputs, slots, out assign))
                        continue;
                }
    
                // inputConditions ???
                var conds = r["inputConditions"] as JArray;
                if (conds != null && conds.Count > 0)
                {
                    if (!EvalAllConditions(conds, slots, assign))
                        continue;
                }
    
                // inputActions ?洹먮맓猷?(?????筌뤾퍓????リ옇??)
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
    
                // outputs ??諛댁뎽 + outputActions ??⑤챷??
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
    
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        // Matching: Shapeless
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
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
    
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        // Matching: Shaped (Sliding + Rotate/Reflect)
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
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
                // shaped: null?? "???녻맱? ???
                if (i >= recipeInputs.Count) { baseGrid.cells[i] = null; continue; }
                baseGrid.cells[i] = recipeInputs[i] as JObject; // null ?띠럾???
            }
    
            // ??2-slot shaped??"??ル쪇援???戮곗굚?? ?곌랜??? transforms(????????? ?ル??
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
    
            // ?筌먦끉??A: ???덉┣???꾩룆?? ?熬? null??怨룹꽑????
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
    
            // 2-slot shaped??2x1(?띠럾????????た??
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
                    int dst = ToIndex(nx, y, ng.w);
                    ng.cells[dst] = g.cells[src];
                }
            }
    
            return ng;
        }
    
        string SerializeSpec(GridSpec g)
        {
            // null/??null ?????堉????댁떳??怨쀬Ŧ ?띠룄???fingerprint
            // (?寃몃쳳???띠럾???쒑땻?? ???? ?寃몃쳳????繞벿살탮??variant嶺???琉우꽑??
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
    
                // ToolActions ?브퀡?????ル뱼????藥?
                if (s["ToolActions"] is JObject ta)
                {
                    foreach (var p in ta.Properties())
                        sb.Append("TA:").Append(p.Name).Append(",");
                }
                sb.Append(";");
            }
            return sb.ToString();
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        // Spec matching
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
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
    }
}
