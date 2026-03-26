


using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;


namespace Game.Data
{
    public partial class RecipeLibrary : MonoBehaviour
    {
        [Header("Deps")]

        public ItemLibrary itemLibrary;
    
        [Header("Recipe Jsons")]
        public TextAsset recipe2Json;  
        public TextAsset recipe4Json;  
        public TextAsset recipe9Json;  
        public TextAsset recipe16Json; 
    
        [Header("Alloy Jsons")]
        public TextAsset alloyJson;   
    
        [Header("Toolbench Jsons")]
        public TextAsset toolbenchJson; 
    
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
    }
}
