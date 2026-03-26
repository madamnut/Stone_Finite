


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
        
        [JsonProperty("solid")] public int[][] solid;
        [JsonProperty("deco")]  public int[][] deco;
    }
    
    [System.Serializable]
    public class WriteRule
    {
        [JsonProperty("targets")] public int[] targets; 
    }
    
    public static class StructureLoader
    {
        
        
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
