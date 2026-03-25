using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    
    public class ItemData
    {
        /* 疫꿸퀡??筌롫?? */
        public string ItemId     { get; }
        public string Name       { get; }
        public string SpriteName { get; }
        public string ItemType   { get; }
        public int    MaxStack   { get; }
    
        /* ??용럡??*/
        public int MaxDurability { get; }
        public int Durability    { get; set; }
    
        /* 揶쏆뮇??獄??袁⑹뵠??*/
        public int    Count { get; set; }
        public Sprite Icon  { get; }
    
        /* ??볥젃 (ATT??tags) */
        public List<string> Tags { get; }
    
        /* ??る?3??(揶???る???已?????????る???紐? ???뵬沃섎챸苑??類ㅻ??댿봺) */
        public Dictionary<string, Dictionary<string, object>> ToolActions   { get; }
        public Dictionary<string, Dictionary<string, object>> WeaponActions { get; }
        public Dictionary<string, Dictionary<string, object>> BreakActions  { get; }
    
        /* ?酉???깅뮞 (ATT details + 鈺곌퀬鍮 野껉퀗????疫꿸퀬? ?類ㅼ삢 ?닌듼? */
        public Dictionary<string, object> Details { get; private set; }
    
        /* ??밴쉐??*/
        public ItemData(
            string itemId,
            string name,
            string spriteName,
            string itemType,
            int    maxStack,
            int    maxDurability,
            int    durability,
            Dictionary<string, Dictionary<string, object>> toolActions,
            Dictionary<string, Dictionary<string, object>> weaponActions,
            Dictionary<string, Dictionary<string, object>> breakActions,
            List<string> tags,
            Dictionary<string, object> details,
            Sprite icon,
            int    count = 1)
        {
            ItemId       = itemId;
            Name         = name;
            SpriteName   = spriteName;
            ItemType     = itemType;
            MaxStack     = maxStack;
    
            MaxDurability = maxDurability;
            Durability    = (durability > 0) ? durability : maxDurability;
    
            Icon  = icon;
            Count = count;
    
            Tags = tags != null
                ? new List<string>(tags)
                : new List<string>();
    
            // ??る?3?? ??已??????뵬沃섎챸苑??類ㅻ??댿봺
            ToolActions = toolActions != null
                ? new Dictionary<string, Dictionary<string, object>>(toolActions)
                : new Dictionary<string, Dictionary<string, object>>();
    
            WeaponActions = weaponActions != null
                ? new Dictionary<string, Dictionary<string, object>>(weaponActions)
                : new Dictionary<string, Dictionary<string, object>>();
    
            BreakActions = breakActions != null
                ? new Dictionary<string, Dictionary<string, object>>(breakActions)
                : new Dictionary<string, Dictionary<string, object>>();
    
            Details = details != null
                ? new Dictionary<string, object>(details)
                : new Dictionary<string, object>();
        }
    
        /* ?????????????????????????????????? ?醫뤿뼢 筌롫뗄苑???????????????????????????????????? */
    
        // ??μ뵬 ??鈺곌퀬??
        public T GetDetail<T>(string key)
        {
            if (Details.TryGetValue(key, out var v) && v is T t)
                return t;
            return default;
        }
    
        // ??μ뵬 ????쇱젟
        public void SetDetail(string key, object value)
        {
            Details[key] = value;
        }
    
        // 餓λ쵐爰?野껋럥以?疫꿸퀡而?detail ??쇱젟 (?? "head.itemId", "weapon.head.damage")
        public void SetDetailPath(string path, object value)
        {
            if (string.IsNullOrEmpty(path))
                return;
    
            var parts = path.Split('.');
            var dict  = Details;
    
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string key = parts[i];
    
                if (!dict.TryGetValue(key, out var next) || next is not Dictionary<string, object> nextDict)
                {
                    nextDict = new Dictionary<string, object>();
                    dict[key] = nextDict;
                }
    
                dict = nextDict;
            }
    
            string last = parts[parts.Length - 1];
            dict[last] = value;
        }
    
        // ??용럡??鈺곌퀣??
        public void ModifyDurability(int amount)
        {
            Durability += amount;
            if (Durability > MaxDurability) Durability = MaxDurability;
            if (Durability < 0)            Durability = 0;
        }
    
        // ??볥젃 野꺜??
        public bool HasTag(string tag)
        {
            if (Tags != null && Tags.Contains(tag))
                return true;
    
            // fallback
            if (Details.TryGetValue("tags", out var v) && v is List<string> tags)
                return tags.Contains(tag);
    
            return false;
        }
    
        // ??る?癰귣똻? ???
        public bool HasToolAction(string action)
            => ToolActions != null && ToolActions.ContainsKey(action);
    
        public bool HasWeaponAction(string action)
            => WeaponActions != null && WeaponActions.ContainsKey(action);
    
        public bool HasBreakAction(string action)
            => BreakActions != null && BreakActions.ContainsKey(action);
    
        // ??る????뵬沃섎챸苑??類ㅻ??댿봺 揶쎛?紐꾩궎疫?(??곸몵筌?null)
        public Dictionary<string, object> GetToolActionParams(string action)
        {
            if (ToolActions != null && ToolActions.TryGetValue(action, out var cfg))
                return cfg;
            return null;
        }
    
        public Dictionary<string, object> GetWeaponActionParams(string action)
        {
            if (WeaponActions != null && WeaponActions.TryGetValue(action, out var cfg))
                return cfg;
            return null;
        }
    
        public Dictionary<string, object> GetBreakActionParams(string action)
        {
            if (BreakActions != null && BreakActions.TryGetValue(action, out var cfg))
                return cfg;
            return null;
        }
    }
}
