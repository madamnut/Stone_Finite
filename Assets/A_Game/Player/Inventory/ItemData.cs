using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    
    public class ItemData
    {
        /* 湲곕낯 硫뷀? */
        public string ItemId     { get; }
        public string Name       { get; }
        public string SpriteName { get; }
        public string ItemType   { get; }
        public int    MaxStack   { get; }
    
        /* ?닿뎄??*/
        public int MaxDurability { get; }
        public int Durability    { get; set; }
    
        /* 媛쒖닔 諛??꾩씠肄?*/
        public int    Count { get; set; }
        public Sprite Icon  { get; }
    
        /* ?쒓렇 (ATT??tags) */
        public List<string> Tags { get; }
    
        /* ?≪뀡 3醫?(媛??≪뀡 ?대쫫 ???대떦 ?≪뀡???몃? ?뚮씪誘명꽣 ?뺤뀛?덈━) */
        public Dictionary<string, Dictionary<string, object>> ToolActions   { get; }
        public Dictionary<string, Dictionary<string, object>> WeaponActions { get; }
        public Dictionary<string, Dictionary<string, object>> BreakActions  { get; }
    
        /* ?뷀뀒?쇱뒪 (ATT details + 議고빀 寃곌낵 ??湲고? ?뺤옣 援ъ“) */
        public Dictionary<string, object> Details { get; private set; }
    
        /* ?앹꽦??*/
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
    
            // ?≪뀡 3醫? ?대쫫 ???뚮씪誘명꽣 ?뺤뀛?덈━
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
    
        /* ????????????????? ?좏떥 硫붿꽌??????????????????? */
    
        // ?⑥씪 ??議고쉶
        public T GetDetail<T>(string key)
        {
            if (Details.TryGetValue(key, out var v) && v is T t)
                return t;
            return default;
        }
    
        // ?⑥씪 ???ㅼ젙
        public void SetDetail(string key, object value)
        {
            Details[key] = value;
        }
    
        // 以묒꺽 寃쎈줈 湲곕컲 detail ?ㅼ젙 (?? "head.itemId", "weapon.head.damage")
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
    
        // ?닿뎄??議곗젅
        public void ModifyDurability(int amount)
        {
            Durability += amount;
            if (Durability > MaxDurability) Durability = MaxDurability;
            if (Durability < 0)            Durability = 0;
        }
    
        // ?쒓렇 寃??
        public bool HasTag(string tag)
        {
            if (Tags != null && Tags.Contains(tag))
                return true;
    
            // fallback
            if (Details.TryGetValue("tags", out var v) && v is List<string> tags)
                return tags.Contains(tag);
    
            return false;
        }
    
        // ?≪뀡 蹂댁쑀 ?щ?
        public bool HasToolAction(string action)
            => ToolActions != null && ToolActions.ContainsKey(action);
    
        public bool HasWeaponAction(string action)
            => WeaponActions != null && WeaponActions.ContainsKey(action);
    
        public bool HasBreakAction(string action)
            => BreakActions != null && BreakActions.ContainsKey(action);
    
        // ?≪뀡 ?뚮씪誘명꽣 ?뺤뀛?덈━ 媛?몄삤湲?(?놁쑝硫?null)
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
