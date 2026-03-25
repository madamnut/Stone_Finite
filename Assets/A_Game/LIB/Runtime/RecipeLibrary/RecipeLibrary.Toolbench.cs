using System.Collections.Generic;
using Newtonsoft.Json.Linq;


using Game.Core;
namespace Game.Data
{
    public partial class RecipeLibrary
    {
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
    
                var inputs = r["inputs"] as JArray;
                if (inputs == null || inputs.Count != 2) continue;
    
                var inActs = r["inputActions"] as JArray;
                bool isShapeless = r.Value<bool?>("isShapeless")
                                   ?? !(r.Value<bool?>("isOrdered") ?? true);
    
                if (mat == null || tool == null) continue;
    
                int[] assign = null;
    
                if (!isShapeless)
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
    }
}
