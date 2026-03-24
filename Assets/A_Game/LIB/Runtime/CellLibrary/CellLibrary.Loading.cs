using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;


namespace Game.Data
{
    public partial class CellLibrary
    {
        void Awake()
        {
            BuildSolidCache();
            BuildUtilityCache();
            BuildFluidCache();
            BuildSpriteCache();
            BuildTileCache();
        }
    
        static uint MakeKey(ushort id, ushort meta) => ((uint)id << 16) | meta;
        static uint MakeFluidLevelKey(ushort fluidId, byte level) => ((uint)fluidId << 16) | level;
    
        void BuildSolidCache()
        {
            _solidById.Clear();
            _solidIdByName.Clear();
    
            if (solidJson == null || string.IsNullOrEmpty(solidJson.text))
                return;
    
            var root = JObject.Parse(solidJson.text);
    
            foreach (var prop in root.Properties())
            {
                string name = prop.Name;
                var o = (JObject)prop.Value;
    
                int idInt = o["id"]?.Value<int>() ?? 0;
                if (idInt < 0) idInt = 0;
                if (idInt > ushort.MaxValue) idInt = ushort.MaxValue;
                ushort id = (ushort)idInt;
    
                // ??type (미기?�면 Default)
                string type = o["type"]?.Value<string>();
                if (string.IsNullOrEmpty(type)) type = "Default";
    
                int bInt = o["brightness"]?.Value<int>() ?? 0;
                if (bInt < 0) bInt = 0; else if (bInt > 15) bInt = 15;
                byte brightness = (byte)bInt;
    
                bool collidable = o["collidable"]?.Value<bool>() ?? false;
                bool gravity = o["gravity"]?.Value<bool>() ?? false;
                bool isPlatform = o["isPlatform"]?.Value<bool>() ?? false;
    
                if (collidable && isPlatform)
                    Debug.LogError($"[CellLibrary] invalid solid def: both collidable and isPlatform are true (name={name}, id={id})");
    
                SolidFlags flags = SolidFlags.None;
                if (collidable) flags |= SolidFlags.Collidable;
                if (gravity) flags |= SolidFlags.HasGravity;
    
                string interaction = o["interaction"]?.Value<string>();
    
                Dictionary<ushort, SolidVariantDef> variants = null;
    
                if (o.TryGetValue("variants", out JToken vTok) && vTok is JArray vArr && vArr.Count > 0)
                {
                    variants = new Dictionary<ushort, SolidVariantDef>(vArr.Count);
    
                    for (int i = 0; i < vArr.Count; i++)
                    {
                        if (!(vArr[i] is JObject vObj)) continue;
    
                        int metaInt = vObj["meta"]?.Value<int>() ?? 0;
                        if (metaInt < 0) metaInt = 0;
                        if (metaInt > ushort.MaxValue) metaInt = ushort.MaxValue;
                        ushort meta = (ushort)metaInt;
    
                        string spriteName = vObj["sprite"]?.Value<string>();
                        if (string.IsNullOrEmpty(spriteName))
                            continue;
    
                        string attachedAt = vObj["attachedAt"]?.Value<string>();
    
                        sbyte brightnessOverride = -1;
                        if (vObj.TryGetValue("brightness_override", out JToken boTok) && boTok != null && boTok.Type != JTokenType.Null)
                        {
                            int boInt = boTok.Value<int>();
                            if (boInt < 0) boInt = 0; else if (boInt > 15) boInt = 15;
                            brightnessOverride = (sbyte)boInt;
                        }
    
                        variants[meta] = new SolidVariantDef
                        {
                            meta = meta,
                            spriteName = spriteName,
                            attachedAt = attachedAt,
                            brightnessOverride = brightnessOverride
                        };
                    }
    
                    if (variants.Count == 0)
                        variants = null;
                }
    
                if (variants == null)
                    continue;
    
                var def = new SolidDef
                {
                    id = id,
                    type = type,
                    brightness = brightness,
                    flags = flags,
                    isPlatform = isPlatform,
                    interaction = interaction,
                    name = name,
                    variants = variants
                };
    
                _solidById[id] = def;
    
                if (!_solidIdByName.ContainsKey(name))
                    _solidIdByName.Add(name, id);
            }
        }
    
        void BuildUtilityCache()
        {
            _utilityById.Clear();
            _utilityIdByName.Clear();
    
            if (utilityJson == null || string.IsNullOrEmpty(utilityJson.text))
                return;
    
            var root = JObject.Parse(utilityJson.text);
    
            foreach (var prop in root.Properties())
            {
                string name = prop.Name;
                var o = (JObject)prop.Value;
    
                int idInt = o["id"]?.Value<int>() ?? 0;
                if (idInt < 0) idInt = 0;
                if (idInt > ushort.MaxValue) idInt = ushort.MaxValue;
                ushort id = (ushort)idInt;
    
                // ??type (미기?�면 Default)
                string type = o["type"]?.Value<string>();
                if (string.IsNullOrEmpty(type)) type = "Default";
    
                Dictionary<ushort, UtilityVariantDef> variants = null;
    
                if (o.TryGetValue("variants", out JToken vTok) && vTok is JArray vArr && vArr.Count > 0)
                {
                    variants = new Dictionary<ushort, UtilityVariantDef>(vArr.Count);
    
                    for (int i = 0; i < vArr.Count; i++)
                    {
                        if (!(vArr[i] is JObject vObj)) continue;
    
                        int metaInt = vObj["meta"]?.Value<int>() ?? 0;
                        if (metaInt < 0) metaInt = 0;
                        if (metaInt > ushort.MaxValue) metaInt = ushort.MaxValue;
                        ushort meta = (ushort)metaInt;
    
                        string spriteName = vObj["sprite"]?.Value<string>();
                        if (string.IsNullOrEmpty(spriteName))
                            continue;
    
                        variants[meta] = new UtilityVariantDef { meta = meta, spriteName = spriteName };
                    }
    
                    if (variants.Count == 0)
                        variants = null;
                }
    
                var def = new UtilityDef
                {
                    id = id,
                    name = name,
                    type = type,
                    variants = variants
                };
    
                _utilityById[id] = def;
    
                if (!_utilityIdByName.ContainsKey(name))
                    _utilityIdByName.Add(name, id);
            }
        }
    
        void BuildFluidCache()
        {
            _fluidById.Clear();
            _fluidIdByName.Clear();
    
            if (fluidJson == null || string.IsNullOrEmpty(fluidJson.text))
                return;
    
            var root = JObject.Parse(fluidJson.text);
    
            foreach (var prop in root.Properties())
            {
                string name = prop.Name;
                var o = (JObject)prop.Value;
    
                int idInt = o["id"]?.Value<int>() ?? 0;
                if (idInt < 0) idInt = 0;
                if (idInt > ushort.MaxValue) idInt = ushort.MaxValue;
                ushort id = (ushort)idInt;
    
                int bInt = o["brightness"]?.Value<int>() ?? 0;
                if (bInt < 0) bInt = 0; else if (bInt > 15) bInt = 15;
                byte brightness = (byte)bInt;
    
                var def = new FluidDef { id = id, brightness = brightness, name = name };
                _fluidById[id] = def;
    
                if (!_fluidIdByName.ContainsKey(name))
                    _fluidIdByName.Add(name, id);
            }
        }
    
        void BuildSpriteCache()
        {
            _solidSpriteByKey.Clear();
            _utilitySpriteByKey.Clear();
            _fluidBaseSpriteById.Clear();
            _fluidLevelSpritesById.Clear();
    
            if (atlas == null) return;
    
            foreach (var kv in _solidById)
            {
                var def = kv.Value;
                foreach (var vkv in def.variants)
                {
                    var v = vkv.Value;
                    var sp = atlas.GetSprite(v.spriteName);
                    if (sp != null)
                        _solidSpriteByKey[MakeKey(def.id, v.meta)] = sp;
                }
            }
    
            foreach (var kv in _utilityById)
            {
                var def = kv.Value;
                if (def.variants == null) continue;
    
                foreach (var vkv in def.variants)
                {
                    var v = vkv.Value;
                    var sp = atlas.GetSprite(v.spriteName);
                    if (sp != null)
                        _utilitySpriteByKey[MakeKey(def.id, v.meta)] = sp;
                }
            }
    
            foreach (var kv in _fluidById)
            {
                var def = kv.Value;
    
                var baseSp = atlas.GetSprite(def.name);
                if (baseSp != null)
                    _fluidBaseSpriteById[def.id] = baseSp;
    
                var arr = new Sprite[17];
                for (int level = 1; level <= 16; level++)
                {
                    string nm = $"{def.name}_{level}";
                    var sp = atlas.GetSprite(nm);
                    if (sp != null)
                        arr[level] = sp;
                }
                _fluidLevelSpritesById[def.id] = arr;
            }
        }
    
        void BuildTileCache()
        {
            _bgTileById.Clear();
            _solidTileByKey.Clear();
            _platformColliderTileByKey.Clear();
            _utilityTileByKey.Clear();
            _fluidTileByKey.Clear();
        }
    }
}
