


using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    
    public class ItemData
    {
        

        public string ItemId     { get; }
        public string Name       { get; }
        public string SpriteName { get; }
        public string ItemType   { get; }
        public int    MaxStack   { get; }
    
        
        public int MaxDurability { get; }
        public int Durability    { get; set; }
    
        
        public int    Count { get; set; }
        public Sprite Icon  { get; }
    
        
        public List<string> Tags { get; }
    
        
        public Dictionary<string, Dictionary<string, object>> ToolActions   { get; }
        public Dictionary<string, Dictionary<string, object>> WeaponActions { get; }
        public Dictionary<string, Dictionary<string, object>> BreakActions  { get; }
    
        
        public Dictionary<string, object> Details { get; private set; }
    
        
        
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
    
        
    
        
        public T GetDetail<T>(string key)
        {
            if (Details.TryGetValue(key, out var v) && v is T t)
                return t;
            return default;
        }
    
        
        
        public void SetDetail(string key, object value)
        {
            Details[key] = value;
        }
    
        
        
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
    
        
        
        public void ModifyDurability(int amount)
        {
            Durability += amount;
            if (Durability > MaxDurability) Durability = MaxDurability;
            if (Durability < 0)            Durability = 0;
        }
    
        
        
        public bool HasTag(string tag)
        {
            if (Tags != null && Tags.Contains(tag))
                return true;
    
            
            if (Details.TryGetValue("tags", out var v) && v is List<string> tags)
                return tags.Contains(tag);
    
            return false;
        }
    
        
        
        public bool HasToolAction(string action)
            => ToolActions != null && ToolActions.ContainsKey(action);
    
        
        public bool HasWeaponAction(string action)
            => WeaponActions != null && WeaponActions.ContainsKey(action);
    
        
        public bool HasBreakAction(string action)
            => BreakActions != null && BreakActions.ContainsKey(action);
    
        
        
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
