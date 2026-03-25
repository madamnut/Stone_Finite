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
    
            // ?リ옇???嶺뚮∥??
            string name       = def.Value<string>("name")       ?? itemId;
            string spriteName = def.Value<string>("spriteName") ?? itemId;
            string itemType   = def.Value<string>("itemType")   ?? "Generic";
            int    maxStack   = def.Value<int?>("maxStack")     ?? 1;
    
            // ???⑸윞??
            int maxDurability = def.Value<int?>("maxDurability") ?? 0;
            int durability    = maxDurability;
    
            // ??蹂μ쟽
            var tags = new List<string>();
            if (def["tags"] is JArray tagsArray)
            {
                var list = tagsArray.ToObject<List<string>>();
                if (list != null)
                    tags.AddRange(list);
            }
    
            // ???떷?3?? dict<???떷???藥? ?筌????逾ф쾬?롮구??
            var breakActions  = ReadActionDict(def["breakActions"]);
            var toolActions   = ReadActionDict(def["toolActions"]);
            var weaponActions = ReadActionDict(def["weaponActions"]);
    
            // Details: ATT ?猷먮쳜???"details" ??곕?餓λ맮彛??곌랜踰딀쾮?
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
    
            // ?熬곣뫗逾??
            var icon = GetSprite(spriteName);
    
            // 嶺뚣끉裕뉏펺?ItemData ??諛댁뎽
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
    
        /// <summary>
        /// ???떷??熬곣뫀援????堉?????
        /// ?꾩룇瑗??? Dictionary&lt;string, Dictionary&lt;string, object&gt;&gt;
        /// - null ????dict
        /// - JArray ["A","B"] ??{ "A": {}, "B": {} }
        /// - JObject { "A": {...}, "B": {...} }
        ///   ??{ "A": (A??JObject ??dict), "B": (B??JObject ??dict) }
        /// - ????濡る룎(string ?? ??{ value: {} }
        /// </summary>
        Dictionary<string, Dictionary<string, object>> ReadActionDict(JToken token)
        {
            var dict = new Dictionary<string, Dictionary<string, object>>();
    
            if (token == null || token.Type == JTokenType.Null)
                return dict;
    
            // ["A","B"] ?筌먐븍Ф
            if (token is JArray arr)
            {
                foreach (var t in arr)
                {
                    if (t == null || t.Type == JTokenType.Null) continue;
                    var name = t.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
    
                    if (!dict.ContainsKey(name))
                        dict[name] = new Dictionary<string, object>(); // ???逾ф쾬?롮구????怨몃쾳
                }
                return dict;
            }
    
            // { "A": {...}, "B": {...} } ?筌먐븍Ф
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
                        // ?띠룆???JObject?띠럾? ?熬곣뫀鍮띸춯? ?잙갭梨뜻틦???濡る룎???띠룆????뿉??띠룆흮????影??꽑繞벿뮻??
                        paramDict = new Dictionary<string, object>
                        {
                            ["value"] = (prop.Value is JValue jv) ? jv.Value : prop.Value?.ToString()
                        };
                    }
    
                    dict[name] = paramDict;
                }
                return dict;
            }
    
            // ??關逾???(string ??
            var single = token.ToString();
            if (!string.IsNullOrEmpty(single))
            {
                dict[single] = new Dictionary<string, object>();
            }
    
            return dict;
        }
    }
}
