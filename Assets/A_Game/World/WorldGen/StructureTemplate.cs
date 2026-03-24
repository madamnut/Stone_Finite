using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;


namespace Game.World
{
    [System.Serializable]
    public class StructureTemplate
    {
        [JsonProperty("anchor")]
        public Anchor anchor;
    
        [JsonProperty("layers")]
        public Layers layers;
    
        // key = ë°°ì¹˜??id (?? 2000, 2001)
        // ê°?= ?´ë‹¹ idê°€ ??–´?????ˆëŠ” ê¸°ì¡´ ?€ê²?id ëª©ë¡
        // ?? { "2001": { "targets":[0] }, "2000": { "targets":[0,2001] } }
        [JsonProperty("writeRules")]
        public Dictionary<int, WriteRule> writeRules;
    }
    
    [System.Serializable]
    public class Anchor
    {
        [JsonProperty("x")] public int x;
        [JsonProperty("y")] public int y;
    }
    
    [System.Serializable]
    public class Layers
    {
        // ? íƒ?? ?„ì¬ ?„ë¡œ?íŠ¸??FG??decoë§??¬ìš©.
        [JsonProperty("solid")] public int[][] solid;
        [JsonProperty("deco")]  public int[][] deco;
    }
    
    [System.Serializable]
    public class WriteRule
    {
        [JsonProperty("targets")] public int[] targets; // ??–´?°ê¸° ?ˆìš© ?€??id ì§‘í•©
    }
    
    public static class StructureLoader
    {
        // Resources/Structures/<name>.json ë¡œë“œ
        public static StructureTemplate Load(string name)
        {
            TextAsset ta = Resources.Load<TextAsset>($"Structures/{name}");
            if (ta == null)
            {
                Debug.LogError($"StructureLoader: not found Resources/Structures/{name}.json");
                return null;
            }
            return JsonConvert.DeserializeObject<StructureTemplate>(ta.text);
        }
    }
}
