using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Game.Core;


namespace Game.Data
{
    public partial class RecipeLibrary
    {
        // Output actions
        ItemData ApplyOutputActions(ItemData dst, JArray outActs, List<ItemData> slots, int[] assign, int outCount)
        {
            if (outActs == null || outActs.Count == 0) return dst;
    
            // @dynamic: create ?誘る닔?
            if (dst == null)
            {
                ItemData created = null;
    
                for (int i = 0; i < outActs.Count; i++)
                {
                    var act = outActs[i] as JObject;
                    if (act == null) continue;
                    if (act.Value<string>("type") != "create") continue;
    
                    string from = act.Value<string>("from");
                    if (string.IsNullOrEmpty(from)) continue;
    
                    object moltenIdObj = ResolveExpr(from, slots, assign);
                    string moltenId = moltenIdObj?.ToString();
                    if (string.IsNullOrEmpty(moltenId)) continue;
    
                    string stripPrefix = act.Value<string>("stripPrefix");
                    string metal = moltenId;
    
                    if (!string.IsNullOrEmpty(stripPrefix) && metal.StartsWith(stripPrefix, StringComparison.Ordinal))
                        metal = metal.Substring(stripPrefix.Length);
    
                    string prefix = ResolveExprToString(act.Value<string>("prefixFrom"), slots, assign);
                    string suffix = ResolveExprToString(act.Value<string>("suffixFrom"), slots, assign);
    
                    string createdItemId = BuildId(prefix, metal, suffix);
                    if (string.IsNullOrEmpty(createdItemId)) continue;
    
                    created = itemLibrary.Create(createdItemId, outCount);
                    break;
                }
    
                dst = created;
                if (dst == null) return null;
            }
    
            // ???熬곣뫁???β돦裕뉐퐲?? ?????濡レ맪 ???꾪뀞嶺??잙갭梨?????? + mul/floorInt/roundInt/fromField ?怨뺣뼺?
            string overrideName = null;
            string overrideSprite = null;
            string overrideItemId = null;
    
            double? overrideDurability = null;
            double? overrideMaxDur = null;
    
            Dictionary<string, Dictionary<string, object>> overrideTool = null;
            Dictionary<string, Dictionary<string, object>> overrideWeapon = null;
            Dictionary<string, Dictionary<string, object>> overrideBreak = null;
    
            double GetCurrentNumber(string field)
            {
                if (field == "durability")
                {
                    if (overrideDurability.HasValue) return overrideDurability.Value;
                    return dst.Durability;
                }
                if (field == "maxDurability")
                {
                    if (overrideMaxDur.HasValue) return overrideMaxDur.Value;
                    return dst.MaxDurability;
                }
                return 0;
            }
    
            void SetCurrentNumber(string field, double v)
            {
                if (field == "durability") overrideDurability = v;
                else if (field == "maxDurability") overrideMaxDur = v;
            }
    
            bool TryToDouble(object v, out double d)
            {
                d = 0;
                if (v == null) return false;
    
                if (v is double dd) { d = dd; return true; }
                if (v is float ff) { d = ff; return true; }
                if (v is int ii) { d = ii; return true; }
                if (v is long ll) { d = ll; return true; }
    
                if (v is JValue jv)
                {
                    if (jv.Value == null) return false;
                    return TryToDouble(jv.Value, out d);
                }
    
                return double.TryParse(v.ToString(), out d);
            }
    
            object ReadFieldWithOverrides(string fieldPath)
            {
                if (string.IsNullOrEmpty(fieldPath)) return null;
    
                if (fieldPath == "durability")
                    return GetCurrentNumber("durability");
    
                if (fieldPath == "maxDurability")
                    return GetCurrentNumber("maxDurability");
    
                // name/sprite/itemId??繞벿살탪??override????⑥ろ맖 ?꾩룇瑗??
                if (fieldPath == "name")
                    return overrideName ?? dst.Name;
    
                if (fieldPath == "spriteName")
                    return overrideSprite ?? dst.SpriteName;
    
                if (fieldPath == "itemId")
                    return overrideItemId ?? dst.ItemId;
    
                // ??濡?룫嶺뚯솘???dst ?リ옇??
                return ReadField(dst, fieldPath);
            }
    
            for (int i = 0; i < outActs.Count; i++)
            {
                var act = outActs[i] as JObject;
                if (act == null) continue;
    
                string type = act.Value<string>("type");
                if (string.IsNullOrEmpty(type)) continue;
    
                if (type == "create") continue;
    
                if (type == "set")
                {
                    string field = act.Value<string>("field");
                    if (string.IsNullOrEmpty(field)) continue;
    
                    object val = null;
                    bool hasVal = false;
    
                    if (act.TryGetValue("value", out var jv))
                    {
                        hasVal = true;
                        if (jv.Type == JTokenType.Null) val = null;
                        else if (jv is JValue jvv) val = jvv.Value;
                        else val = jv.ToString();
    
                        if (val is string sv) val = ExpandTokens(sv);
                    }
                    else if (act.TryGetValue("fromInput", out var jf) && act.TryGetValue("inputField", out var jif))
                    {
                        int? from = jf.Type == JTokenType.Null ? (int?)null : jf.Value<int?>();
                        string inputField = jif.Type == JTokenType.Null ? null : jif.ToString();
    
                        if (from.HasValue && !string.IsNullOrEmpty(inputField))
                        {
                            int si = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                            var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
    
                            val = ReadField(src, inputField);
                            hasVal = true;
    
                            string strip = act.Value<string>("stripSuffix");
                            if (!string.IsNullOrEmpty(strip) && val is string s0 && s0.EndsWith(strip, StringComparison.Ordinal))
                                val = s0.Substring(0, s0.Length - strip.Length);
                        }
                    }
                    else if (act.TryGetValue("fromField", out var jff))
                    {
                        string fromField = jff.Type == JTokenType.Null ? null : jff.ToString();
                        if (!string.IsNullOrEmpty(fromField))
                        {
                            val = ReadFieldWithOverrides(fromField);
                            hasVal = true;
                        }
                    }
                    else if (act["valueFromFields"] is JArray vff)
                    {
                        string sep = act.Value<string>("separator") ?? "";
                        string pre = act.Value<string>("prefixEach") ?? "";
                        var vals = new List<string>(vff.Count);
    
                        for (int k = 0; k < vff.Count; k++)
                        {
                            string key = vff[k]?.ToString();
                            if (string.IsNullOrEmpty(key)) continue;
    
                            object v = ReadFieldWithOverrides(key);
                            if (v == null) continue;
    
                            string s = v.ToString();
                            if (!string.IsNullOrEmpty(pre)) s = pre + s;
                            vals.Add(s);
                        }
    
                        val = string.Join(sep, vals);
                        hasVal = true;
                    }
    
                    if (!hasVal) continue;
    
                    if (field == "name") { overrideName = val?.ToString(); continue; }
                    if (field == "spriteName") { overrideSprite = val?.ToString(); continue; }
                    if (field == "itemId") { overrideItemId = val?.ToString(); continue; }
    
                    if (field == "durability" || field == "maxDurability")
                    {
                        if (TryToDouble(val, out double dv))
                            SetCurrentNumber(field, dv);
                        continue;
                    }
    
                    if (field == "ToolActions" || field == "WeaponActions" || field == "BreakActions")
                    {
                        var dict = ToActionDict(val);
                        if (field == "ToolActions") overrideTool = dict;
                        else if (field == "WeaponActions") overrideWeapon = dict;
                        else overrideBreak = dict;
                        continue;
                    }
    
                    if (field.StartsWith("details.", StringComparison.Ordinal))
                    {
                        SetDetailPath(dst, field.Substring("details.".Length), val);
                        continue;
                    }
    
                    if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
                    {
                        overrideTool = SetInActionRoot(overrideTool ?? dst.ToolActions, field.Substring("ToolActions.".Length), val);
                        continue;
                    }
    
                    if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
                    {
                        overrideWeapon = SetInActionRoot(overrideWeapon ?? dst.WeaponActions, field.Substring("WeaponActions.".Length), val);
                        continue;
                    }
    
                    if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
                    {
                        overrideBreak = SetInActionRoot(overrideBreak ?? dst.BreakActions, field.Substring("BreakActions.".Length), val);
                        continue;
                    }
    
                    continue;
                }
                else if (type == "copy")
                {
                    int from = act.Value<int?>("fromInput") ?? -1;
                    string inField = act.Value<string>("inputField");
                    string toField = act.Value<string>("toField");
                    if (from < 0 || string.IsNullOrEmpty(inField) || string.IsNullOrEmpty(toField)) continue;
    
                    int si = (assign != null && from >= 0 && from < assign.Length) ? assign[from] : -1;
                    var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                    var val = ReadField(src, inField);
    
                    if (toField == "name") { overrideName = val?.ToString(); continue; }
                    if (toField == "spriteName") { overrideSprite = val?.ToString(); continue; }
                    if (toField == "itemId") { overrideItemId = val?.ToString(); continue; }
    
                    if (toField == "durability" || toField == "maxDurability")
                    {
                        if (TryToDouble(val, out double dv))
                            SetCurrentNumber(toField, dv);
                        continue;
                    }
    
                    if (toField == "ToolActions" || toField == "WeaponActions" || toField == "BreakActions")
                    {
                        var dict = ToActionDict(val);
                        if (toField == "ToolActions") overrideTool = dict;
                        else if (toField == "WeaponActions") overrideWeapon = dict;
                        else overrideBreak = dict;
                        continue;
                    }
    
                    if (toField.StartsWith("details.", StringComparison.Ordinal))
                    {
                        SetDetailPath(dst, toField.Substring("details.".Length), val);
                        continue;
                    }
    
                    if (toField.StartsWith("ToolActions.", StringComparison.Ordinal))
                    {
                        overrideTool = SetInActionRoot(overrideTool ?? dst.ToolActions, toField.Substring("ToolActions.".Length), val);
                        continue;
                    }
    
                    if (toField.StartsWith("WeaponActions.", StringComparison.Ordinal))
                    {
                        overrideWeapon = SetInActionRoot(overrideWeapon ?? dst.WeaponActions, toField.Substring("WeaponActions.".Length), val);
                        continue;
                    }
    
                    if (toField.StartsWith("BreakActions.", StringComparison.Ordinal))
                    {
                        overrideBreak = SetInActionRoot(overrideBreak ?? dst.BreakActions, toField.Substring("BreakActions.".Length), val);
                        continue;
                    }
                }
                else if (type == "sum")
                {
                    string outField = act.Value<string>("field");
                    string inField = act.Value<string>("inputField");
                    var fromInputs = act["fromInputs"] as JArray;
    
                    if (string.IsNullOrEmpty(outField) || string.IsNullOrEmpty(inField) || fromInputs == null)
                        continue;
    
                    int sum = 0;
                    for (int k = 0; k < fromInputs.Count; k++)
                    {
                        int fi = fromInputs[k].Value<int>();
                        int si = (assign != null && fi >= 0 && fi < assign.Length) ? assign[fi] : -1;
                        var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
    
                        var v = ReadField(src, inField);
                        if (v != null && int.TryParse(v.ToString(), out int iv))
                            sum += iv;
                    }
    
                    if (outField == "durability")
                    {
                        overrideDurability = (overrideDurability ?? dst.Durability) + sum;
                        continue;
                    }
    
                    if (outField == "maxDurability")
                    {
                        overrideMaxDur = (overrideMaxDur ?? dst.MaxDurability) + sum;
                        continue;
                    }
    
                    if (outField.StartsWith("details.", StringComparison.Ordinal))
                    {
                        SetDetailPath(dst, outField.Substring("details.".Length), sum);
                        continue;
                    }
                }
                else if (type == "mul")
                {
                    string field = act.Value<string>("field");
                    if (string.IsNullOrEmpty(field)) continue;
    
                    // ?熬곣뫗???durability/maxDurability嶺?mul 嶺뚯솘???(?熬곣뫗???details.* ?筌먦끉??
                    if (field != "durability" && field != "maxDurability")
                        continue;
    
                    object rhs = null;
                    bool hasRhs = false;
    
                    if (act.TryGetValue("value", out var jv))
                    {
                        if (jv.Type == JTokenType.Null) rhs = null;
                        else if (jv is JValue jvv) rhs = jvv.Value;
                        else rhs = jv.ToString();
                        hasRhs = true;
                    }
                    else if (act.TryGetValue("fromInput", out var jf) && act.TryGetValue("inputField", out var jif))
                    {
                        int? from = jf.Type == JTokenType.Null ? (int?)null : jf.Value<int?>();
                        string inputField = jif.Type == JTokenType.Null ? null : jif.ToString();
    
                        if (from.HasValue && !string.IsNullOrEmpty(inputField))
                        {
                            int si = (assign != null && from.Value >= 0 && from.Value < assign.Length) ? assign[from.Value] : -1;
                            var src = (si >= 0 && si < slots.Count) ? slots[si] : null;
                            rhs = ReadField(src, inputField);
                            hasRhs = true;
                        }
                    }
                    else if (act.TryGetValue("fromField", out var jff))
                    {
                        string fromField = jff.Type == JTokenType.Null ? null : jff.ToString();
                        if (!string.IsNullOrEmpty(fromField))
                        {
                            rhs = ReadFieldWithOverrides(fromField);
                            hasRhs = true;
                        }
                    }
    
                    if (!hasRhs) continue;
    
                    if (!TryToDouble(rhs, out double r)) continue;
    
                    double cur = GetCurrentNumber(field);
                    SetCurrentNumber(field, cur * r);
                }
                else if (type == "floorInt" || type == "roundInt")
                {
                    string field = act.Value<string>("field");
                    if (string.IsNullOrEmpty(field)) continue;
    
                    if (field != "durability" && field != "maxDurability")
                        continue;
    
                    double cur = GetCurrentNumber(field);
    
                    if (type == "floorInt")
                        cur = Math.Floor(cur);
                    else
                        cur = Math.Round(cur);
    
                    SetCurrentNumber(field, cur);
                }
                else if (type == "delete")
                {
                    string field = act.Value<string>("field");
                    if (string.IsNullOrEmpty(field)) continue;
    
                    if (field == "ToolActions" || field == "WeaponActions" || field == "BreakActions")
                    {
                        if (field == "ToolActions") overrideTool = new Dictionary<string, Dictionary<string, object>>();
                        else if (field == "WeaponActions") overrideWeapon = new Dictionary<string, Dictionary<string, object>>();
                        else overrideBreak = new Dictionary<string, Dictionary<string, object>>();
                        continue;
                    }
    
                    if (field.StartsWith("details.", StringComparison.Ordinal))
                    {
                        DeleteFromDetails(dst, field.Substring("details.".Length));
                        continue;
                    }
    
                    if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
                    {
                        overrideTool = DeleteFromActionRoot(overrideTool ?? dst.ToolActions, field.Substring("ToolActions.".Length));
                        continue;
                    }
    
                    if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
                    {
                        overrideWeapon = DeleteFromActionRoot(overrideWeapon ?? dst.WeaponActions, field.Substring("WeaponActions.".Length));
                        continue;
                    }
    
                    if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
                    {
                        overrideBreak = DeleteFromActionRoot(overrideBreak ?? dst.BreakActions, field.Substring("BreakActions.".Length));
                        continue;
                    }
                }
            }
    
            bool changed =
                overrideName != null ||
                overrideSprite != null ||
                overrideItemId != null ||
                overrideDurability.HasValue ||
                overrideMaxDur.HasValue ||
                overrideTool != null ||
                overrideWeapon != null ||
                overrideBreak != null;
    
            if (!changed)
                return dst;
    
            string finalName = overrideName ?? dst.Name;
            string finalSprite = overrideSprite ?? dst.SpriteName;
            string finalId = overrideItemId ?? dst.ItemId;
    
            int finalMaxDur = (int)Mathf.FloorToInt((float)(overrideMaxDur ?? dst.MaxDurability));
            int finalDurability = (int)Mathf.FloorToInt((float)(overrideDurability ?? dst.Durability));
    
            var finalTool = overrideTool ?? dst.ToolActions;
            var finalWeapon = overrideWeapon ?? dst.WeaponActions;
            var finalBreak = overrideBreak ?? dst.BreakActions;
    
            var finalIcon = itemLibrary != null ? itemLibrary.GetSprite(finalSprite) : dst.Icon;
            var finalDetails = dst.Details;
    
            return new ItemData(
                itemId: finalId,
                name: finalName,
                spriteName: finalSprite,
                itemType: dst.ItemType,
                maxStack: dst.MaxStack,
                maxDurability: finalMaxDur,
                durability: finalDurability,
                toolActions: finalTool,
                weaponActions: finalWeapon,
                breakActions: finalBreak,
                tags: dst.Tags,
                details: finalDetails,
                icon: finalIcon,
                count: dst.Count
            );
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        // Field reads + mutations
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        object ReadField(ItemData src, string field)
        {
            if (src == null || string.IsNullOrEmpty(field)) return null;
    
            if (field == "name") return src.Name;
            if (field == "spriteName") return src.SpriteName;
            if (field == "itemId") return src.ItemId;
            if (field == "durability") return src.Durability;
            if (field == "maxDurability") return src.MaxDurability;
            if (field == "tags") return src.Tags;
    
            if (field == "details") return src.Details;
            if (field == "ToolActions") return src.ToolActions;
            if (field == "WeaponActions") return src.WeaponActions;
            if (field == "BreakActions") return src.BreakActions;
    
            if (field.StartsWith("details.", StringComparison.Ordinal))
                return ResolveFromDetails(src, field.Substring("details.".Length));
    
            if (field.StartsWith("ToolActions.", StringComparison.Ordinal))
                return ReadFromActionRoot(src.ToolActions, field.Substring("ToolActions.".Length));
    
            if (field.StartsWith("WeaponActions.", StringComparison.Ordinal))
                return ReadFromActionRoot(src.WeaponActions, field.Substring("WeaponActions.".Length));
    
            if (field.StartsWith("BreakActions.", StringComparison.Ordinal))
                return ReadFromActionRoot(src.BreakActions, field.Substring("BreakActions.".Length));
    
            return null;
        }
    
        object ReadFromActionRoot(Dictionary<string, Dictionary<string, object>> root, string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return null;
    
            var parts = path.Split('.');
            if (parts.Length == 0) return null;
    
            string actionName = parts[0];
            if (!root.TryGetValue(actionName, out var paramDict) || paramDict == null)
                return null;
    
            if (parts.Length == 1)
                return paramDict;
    
            object curr = paramDict;
    
            for (int i = 1; i < parts.Length; i++)
            {
                string key = parts[i];
    
                if (curr is Dictionary<string, object> d)
                {
                    if (!d.TryGetValue(key, out curr))
                        return null;
                }
                else
                {
                    return null;
                }
            }
    
            return curr;
        }
    
        void SetDetailPath(ItemData dst, string path, object value)
        {
            if (dst?.Details == null || string.IsNullOrEmpty(path)) return;
    
            var parts = path.Split('.');
            object curr = dst.Details;
    
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string key = parts[i];
    
                if (curr is Dictionary<string, object> d)
                {
                    if (!d.TryGetValue(key, out var next) || next == null)
                    {
                        var created = new Dictionary<string, object>();
                        d[key] = created;
                        curr = created;
                    }
                    else if (next is Dictionary<string, object> nd)
                    {
                        curr = nd;
                    }
                    else
                    {
                        var created = new Dictionary<string, object>();
                        d[key] = created;
                        curr = created;
                    }
                }
                else return;
            }
    
            if (curr is Dictionary<string, object> last)
                last[parts[^1]] = value;
        }
    
        void DeleteFromDetails(ItemData dst, string path)
        {
            if (dst?.Details == null || string.IsNullOrEmpty(path)) return;
    
            var parts = path.Split('.');
            if (parts.Length == 0) return;
    
            if (parts.Length == 1)
            {
                dst.Details.Remove(parts[0]);
                return;
            }
    
            object curr = dst.Details;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (curr is Dictionary<string, object> d)
                {
                    if (!d.TryGetValue(parts[i], out curr) || curr == null)
                        return;
                }
                else return;
            }
    
            if (curr is Dictionary<string, object> last)
                last.Remove(parts[^1]);
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        // Action dict helpers (copy-on-write)
        // ??????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        Dictionary<string, Dictionary<string, object>> ToActionDict(object v)
        {
            if (v == null) return null;
    
            if (v is Dictionary<string, Dictionary<string, object>> dd)
            {
                return dd.ToDictionary(kv => kv.Key,
                                       kv => kv.Value != null
                                           ? new Dictionary<string, object>(kv.Value)
                                           : new Dictionary<string, object>());
            }
    
            if (v is Dictionary<string, object> d0)
            {
                var res = new Dictionary<string, Dictionary<string, object>>();
                foreach (var kv in d0)
                {
                    if (kv.Value is Dictionary<string, object> inner)
                        res[kv.Key] = new Dictionary<string, object>(inner);
                    else
                        res[kv.Key] = new Dictionary<string, object>();
                }
                return res;
            }
    
            if (v is JObject jo)
            {
                // JObject -> Dictionary<string, Dictionary<string, object>>
                var res = new Dictionary<string, Dictionary<string, object>>();
                foreach (var p in jo.Properties())
                {
                    if (p.Value is JObject innerJo)
                    {
                        var inner = new Dictionary<string, object>();
                        foreach (var ip in innerJo.Properties())
                        {
                            if (ip.Value is JValue jv) inner[ip.Name] = jv.Value;
                            else inner[ip.Name] = ip.Value.ToString();
                        }
                        res[p.Name] = inner;
                    }
                    else
                    {
                        res[p.Name] = new Dictionary<string, object>();
                    }
                }
                return res;
            }
    
            if (v is JArray ja)
            {
                var res = new Dictionary<string, Dictionary<string, object>>();
                foreach (var x in ja)
                {
                    string name = x.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!res.ContainsKey(name))
                        res[name] = new Dictionary<string, object>();
                }
                return res;
            }
    
            if (v is System.Collections.IEnumerable ien && v is not string)
            {
                var res = new Dictionary<string, Dictionary<string, object>>();
                foreach (var x in ien)
                {
                    string name = x?.ToString();
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!res.ContainsKey(name))
                        res[name] = new Dictionary<string, object>();
                }
                return res;
            }
    
            string single = v.ToString();
            if (string.IsNullOrEmpty(single)) return null;
    
            return new Dictionary<string, Dictionary<string, object>>
            {
                { single, new Dictionary<string, object>() }
            };
        }
    
        Dictionary<string, Dictionary<string, object>> SetInActionRoot(
            Dictionary<string, Dictionary<string, object>> root,
            string path,
            object value)
        {
            if (root == null) root = new Dictionary<string, Dictionary<string, object>>();
            if (string.IsNullOrEmpty(path)) return root;
    
            var newRoot = root.ToDictionary(kv => kv.Key,
                kv => kv.Value != null ? new Dictionary<string, object>(kv.Value) : new Dictionary<string, object>());
    
            var parts = path.Split('.');
            if (parts.Length == 0) return newRoot;
    
            string actionName = parts[0];
            if (!newRoot.TryGetValue(actionName, out var param) || param == null)
                param = new Dictionary<string, object>();
            else
                param = new Dictionary<string, object>(param);
    
            if (parts.Length == 1)
            {
                if (value is Dictionary<string, object> d)
                    param = new Dictionary<string, object>(d);
                newRoot[actionName] = param;
                return newRoot;
            }
    
            object curr = param;
            for (int i = 1; i < parts.Length - 1; i++)
            {
                string key = parts[i];
    
                if (curr is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(key, out var next) || next == null)
                    {
                        var created = new Dictionary<string, object>();
                        dict[key] = created;
                        curr = created;
                    }
                    else if (next is Dictionary<string, object> nd)
                    {
                        var copied = new Dictionary<string, object>(nd);
                        dict[key] = copied;
                        curr = copied;
                    }
                    else
                    {
                        var created = new Dictionary<string, object>();
                        dict[key] = created;
                        curr = created;
                    }
                }
                else return newRoot;
            }
    
            if (curr is Dictionary<string, object> last)
                last[parts[^1]] = value;
    
            newRoot[actionName] = param;
            return newRoot;
        }
    
        Dictionary<string, Dictionary<string, object>> DeleteFromActionRoot(
            Dictionary<string, Dictionary<string, object>> root,
            string path)
        {
            if (root == null || string.IsNullOrEmpty(path)) return root;
    
            var newRoot = root.ToDictionary(kv => kv.Key,
                kv => kv.Value != null ? new Dictionary<string, object>(kv.Value) : new Dictionary<string, object>());
    
            var parts = path.Split('.');
            if (parts.Length == 0) return newRoot;
    
            if (parts.Length == 1)
            {
                newRoot.Remove(parts[0]);
                return newRoot;
            }
    
            string actionName = parts[0];
            if (!newRoot.TryGetValue(actionName, out var param) || param == null)
                return newRoot;
    
            var newParam = new Dictionary<string, object>(param);
    
            object curr = newParam;
            for (int i = 1; i < parts.Length - 1; i++)
            {
                string key = parts[i];
    
                if (curr is Dictionary<string, object> dict)
                {
                    if (!dict.TryGetValue(key, out var next) || next == null)
                        return newRoot;
    
                    if (next is Dictionary<string, object> nd)
                    {
                        var copied = new Dictionary<string, object>(nd);
                        dict[key] = copied;
                        curr = copied;
                    }
                    else return newRoot;
                }
                else return newRoot;
            }
    
            if (curr is Dictionary<string, object> lastDict)
                lastDict.Remove(parts[^1]);
    
            newRoot[actionName] = newParam;
            return newRoot;
        }
    
        string ExpandTokens(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string rand = Guid.NewGuid().ToString("N").Substring(0, 6);
            return s.Replace("$timestamp$", ts).Replace("$rand$", rand);
        }
    }
}
