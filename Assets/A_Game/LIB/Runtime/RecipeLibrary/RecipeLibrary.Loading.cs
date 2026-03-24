using Newtonsoft.Json.Linq;
using UnityEngine;


using Game.World;
namespace Game.Data
{
    public partial class RecipeLibrary
    {
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
    }
}
