using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Game.Player;


namespace Game.Data
{
    public partial class ItemLibrary
    {
        public ItemData Create(string itemId, int count = 1)
        {
            var def = GetItemJson(itemId);
            if (def == null) return null;
    
            // 湲곕낯 硫뷀?
            string name       = def.Value<string>("name")       ?? itemId;
            string spriteName = def.Value<string>("spriteName") ?? itemId;
            string itemType   = def.Value<string>("itemType")   ?? "Generic";
            int    maxStack   = def.Value<int?>("maxStack")     ?? 1;
    
            // ?닿뎄??
            int maxDurability = def.Value<int?>("maxDurability") ?? 0;
            int durability    = maxDurability;
    
            // ?쒓렇
            var tags = new List<string>();
            if (def["tags"] is JArray tagsArray)
            {
                var list = tagsArray.ToObject<List<string>>();
                if (list != null)
                    tags.AddRange(list);
            }
    
            // ?≪뀡 3醫? dict<?≪뀡?대쫫, ?몃??뚮씪誘명꽣>
            var breakActions  = ReadActionDict(def["breakActions"]);
            var toolActions   = ReadActionDict(def["toolActions"]);
            var weaponActions = ReadActionDict(def["weaponActions"]);
    
            // Details: ATT 猷⑦듃??"details" 釉붾줉留?蹂듭궗
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
    
            // ?꾩씠肄?
            var icon = GetSprite(spriteName);
    
            // 理쒖쥌 ItemData ?앹꽦
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
        /// ?≪뀡 ?꾨뱶 ?뚯떛 ?ы띁
        /// 諛섑솚?? Dictionary&lt;string, Dictionary&lt;string, object&gt;&gt;
        /// - null ??鍮?dict
        /// - JArray ["A","B"] ??{ "A": {}, "B": {} }
        /// - JObject { "A": {...}, "B": {...} }
        ///   ??{ "A": (A??JObject ??dict), "B": (B??JObject ??dict) }
        /// - 媛??섎굹(string ?? ??{ value: {} }
        /// </summary>
        Dictionary<string, Dictionary<string, object>> ReadActionDict(JToken token)
        {
            var dict = new Dictionary<string, Dictionary<string, object>>();
    
            if (token == null || token.Type == JTokenType.Null)
                return dict;
    
            // ["A","B"] ?뺥깭
            if (token is JArray arr)
            {
                foreach (var t in arr)
                {
                    if (t == null || t.Type == JTokenType.Null) continue;
                    var name = t.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
    
                    if (!dict.ContainsKey(name))
                        dict[name] = new Dictionary<string, object>(); // ?뚮씪誘명꽣 ?놁쓬
                }
                return dict;
            }
    
            // { "A": {...}, "B": {...} } ?뺥깭
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
                        // 媛믪씠 JObject媛 ?꾨땲硫? 洹몃깷 ?섎굹??媛믪쑝濡?媛먯떥???ｌ뼱以??
                        paramDict = new Dictionary<string, object>
                        {
                            ["value"] = (prop.Value is JValue jv) ? jv.Value : prop.Value?.ToString()
                        };
                    }
    
                    dict[name] = paramDict;
                }
                return dict;
            }
    
            // ?⑥씪 媛?(string ??
            var single = token.ToString();
            if (!string.IsNullOrEmpty(single))
            {
                dict[single] = new Dictionary<string, object>();
            }
    
            return dict;
        }
    }
}
