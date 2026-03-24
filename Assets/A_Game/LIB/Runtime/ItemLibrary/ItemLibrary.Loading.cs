using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;


namespace Game.Data
{
    public partial class ItemLibrary
    {
        void Awake()
        {
            allItemDict = new Dictionary<string, JObject>();
            foreach (var textAsset in jsonFiles) MergeJson(textAsset);
        }
    
        void MergeJson(TextAsset textAsset)
        {
            if (textAsset == null)
            {
                Debug.LogWarning("?†Îãπ?òÏ? ?äÏ? JSON ?åÏùº???àÏäµ?àÎã§.");
                return;
            }
    
            Dictionary<string, JObject> dict;
            try
            {
                dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, JObject>>(textAsset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON ?åÏã± ?§Î•ò ({textAsset.name}): {ex.Message}");
                return;
            }
    
            foreach (var kv in dict)
                allItemDict[kv.Key] = kv.Value;
        }
    }
}
