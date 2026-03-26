


using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Game.Core;


namespace Game.Data
{
    public partial class ItemLibrary
    {
        
        public ItemData Create(string itemId, int count = 1)
        {
            var def = GetItemJson(itemId);
            if (def == null) return null;
    
            
            string name       = def.Value<string>("name")       ?? itemId;
            string spriteName = def.Value<string>("spriteName") ?? itemId;
            string itemType   = def.Value<string>("itemType")   ?? "Generic";
            int    maxStack   = def.Value<int?>("maxStack")     ?? 1;
    
            
            int maxDurability = def.Value<int?>("maxDurability") ?? 0;

            int durability    = maxDurability;
    
            
            var tags = new List<string>();
            if (def["tags"] is JArray tagsArray)
            {
                var list = tagsArray.ToObject<List<string>>();
                if (list != null)
                    tags.AddRange(list);
            }
    
            
            var breakActions  = ReadActionDict(def["breakActions"]);
            var toolActions   = ReadActionDict(def["toolActions"]);
            var weaponActions = ReadActionDict(def["weaponActions"]);
    
            
            var details = new Dictionary<string, object>();
            if (def["details"] is JObject detObj)
            {
                var detDict = detObj.ToObject<Dictionary<string, object>>();
                if (detDict != null)
                {
                    foreach (var kv in detDict)
                        details[kv.Key] = kv.Value;
                }
            }
    
            
            var icon = GetSprite(spriteName);
    
            
            return new ItemData(
                itemId:        itemId,
                name:          name,
                spriteName:    spriteName,
                itemType:      itemType,
                maxStack:      maxStack,
                maxDurability: maxDurability,
                durability:    durability,
                toolActions:   toolActions,
                weaponActions: weaponActions,
                breakActions:  breakActions,
                tags:          tags,
                details:       details,
                icon:          icon,
                count:         count
            );
        }
    
        
        
        
        
        
        
        
        
        
        
        Dictionary<string, Dictionary<string, object>> ReadActionDict(JToken token)
        {
            var dict = new Dictionary<string, Dictionary<string, object>>();
    
            if (token == null || token.Type == JTokenType.Null)
                return dict;
    
            
            if (token is JArray arr)
            {
                foreach (var t in arr)
                {
                    if (t == null || t.Type == JTokenType.Null) continue;
                    var name = t.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
    
                    if (!dict.ContainsKey(name))
                        dict[name] = new Dictionary<string, object>(); 
                }
                return dict;
            }
    
            
            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    string name = prop.Name;
                    if (string.IsNullOrEmpty(name)) continue;
    
                    Dictionary<string, object> paramDict = null;
    
                    if (prop.Value is JObject paramObj)
                    {
                        paramDict = paramObj.ToObject<Dictionary<string, object>>() 
                                    ?? new Dictionary<string, object>();
                    }
                    else
                    {
                        
                        paramDict = new Dictionary<string, object>
                        {
                            ["value"] = (prop.Value is JValue jv) ? jv.Value : prop.Value?.ToString()
                        };
                    }
    
                    dict[name] = paramDict;
                }
                return dict;
            }
    
            
            var single = token.ToString();
            if (!string.IsNullOrEmpty(single))
            {
                dict[single] = new Dictionary<string, object>();
            }
    
            return dict;
        }
    }
}
