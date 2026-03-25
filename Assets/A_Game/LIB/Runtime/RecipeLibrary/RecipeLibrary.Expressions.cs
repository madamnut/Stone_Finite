using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Game.Core;


namespace Game.Data
{
    public partial class RecipeLibrary
    {
        // Input conditions and expression helpers
        bool EvalAllConditions(JArray conds, List<ItemData> slots, int[] assign)
        {
            for (int i = 0; i < conds.Count; i++)
            {
                var c = conds[i] as JObject;
                if (c == null) return false;
    
                string path = c.Value<string>("path");
                string op = c.Value<string>("op");
                string rhs = c.Value<string>("rhs");
    
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(op) || string.IsNullOrEmpty(rhs))
                    return false;
    
                object lObj = ResolveExpr(path, slots, assign);
                object rObj = ResolveExpr(rhs, slots, assign);
    
                if (!Compare(lObj, op, rObj))
                    return false;
            }
    
            return true;
        }
    
        bool Compare(object left, string op, object right)
        {
            bool lNum = TryToNumber(left, out double ln);
            bool rNum = TryToNumber(right, out double rn);
    
            if (lNum && rNum)
            {
                switch (op)
                {
                    case ">=": return ln >= rn;
                    case ">": return ln > rn;
                    case "<=": return ln <= rn;
                    case "<": return ln < rn;
                    case "==": return Math.Abs(ln - rn) < 0.000001;
                    case "!=": return Math.Abs(ln - rn) >= 0.000001;
                    default: return false;
                }
            }
    
            string ls = left?.ToString();
            string rs = right?.ToString();
    
            if (op == "==") return string.Equals(ls, rs, StringComparison.Ordinal);
            if (op == "!=") return !string.Equals(ls, rs, StringComparison.Ordinal);
    
            return false;
        }
    
        bool TryToNumber(object v, out double num)
        {
            num = 0;
    
            if (v == null) return false;
            if (v is int i) { num = i; return true; }
            if (v is long l) { num = l; return true; }
            if (v is float f) { num = f; return true; }
            if (v is double d) { num = d; return true; }
    
            if (v is JValue jv)
            {
                if (jv.Value == null) return false;
                return TryToNumber(jv.Value, out num);
            }
    
            return double.TryParse(v.ToString(), out num);
        }
    
        void NormalizeInputActions(JArray remapped, List<ItemData> slots, int[] assign)
        {
            if (remapped == null) return;
    
            for (int i = 0; i < remapped.Count; i++)
            {
                if (remapped[i] is not JObject act) continue;
    
                string type = act.Value<string>("type");
                if (string.IsNullOrEmpty(type)) continue;
    
                if (type == "consumeMetal")
                {
                    var amtTok = act["amount"];
                    if (amtTok == null) continue;
    
                    if (amtTok.Type == JTokenType.String)
                    {
                        string expr = amtTok.ToString();
                        object v = ResolveExpr(expr, slots, assign);
                        if (TryToNumber(v, out double dn))
                            act["amount"] = (int)Math.Round(dn);
                    }
                }
            }
        }
    
        object ResolveExpr(string expr, List<ItemData> slots, int[] assign)
        {
            if (string.IsNullOrEmpty(expr)) return null;
    
            if (int.TryParse(expr, out int iv)) return iv;
    
            if (expr.StartsWith("inputs[", StringComparison.Ordinal))
            {
                int close = expr.IndexOf(']');
                if (close <= 6) return null;
    
                string idxStr = expr.Substring(7, close - 7);
                if (!int.TryParse(idxStr, out int recipeInputIndex)) return null;
    
                int si = (assign != null && recipeInputIndex >= 0 && recipeInputIndex < assign.Length) ? assign[recipeInputIndex] : -1;
                ItemData it = (si >= 0 && si < slots.Count) ? slots[si] : null;
                if (it == null) return null;
    
                string rest = expr.Substring(close + 1);
                if (string.IsNullOrEmpty(rest)) return it;
    
                if (rest.StartsWith(".", StringComparison.Ordinal))
                    rest = rest.Substring(1);
    
                return ResolveOnItem(it, rest);
            }
    
            return null;
        }
    
        string ResolveExprToString(string expr, List<ItemData> slots, int[] assign)
        {
            if (string.IsNullOrEmpty(expr)) return null;
            object v = ResolveExpr(expr, slots, assign);
            return v?.ToString();
        }
    
        object ResolveOnItem(ItemData it, string path)
        {
            if (it == null || string.IsNullOrEmpty(path)) return null;
    
            if (path == "name") return it.Name;
            if (path == "spriteName") return it.SpriteName;
            if (path == "itemId") return it.ItemId;
            if (path == "durability") return it.Durability;
            if (path == "maxDurability") return it.MaxDurability;
            if (path == "tags") return it.Tags;
    
            if (path.StartsWith("ToolActions.", StringComparison.Ordinal))
                return ReadFromActionRoot(it.ToolActions, path.Substring("ToolActions.".Length));
    
            if (path.StartsWith("WeaponActions.", StringComparison.Ordinal))
                return ReadFromActionRoot(it.WeaponActions, path.Substring("WeaponActions.".Length));
    
            if (path.StartsWith("BreakActions.", StringComparison.Ordinal))
                return ReadFromActionRoot(it.BreakActions, path.Substring("BreakActions.".Length));
    
            if (path.StartsWith("details.", StringComparison.Ordinal))
                return ResolveFromDetails(it, path.Substring("details.".Length));
    
            if (path == "details")
                return it.Details;
    
            return null;
        }
    
        object ResolveFromDetails(ItemData it, string path)
        {
            if (it?.Details == null || string.IsNullOrEmpty(path)) return null;
    
            object curr = it.Details;
            var parts = path.Split('.');
    
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
    
                string key = part;
                int? index = null;
    
                int lb = part.IndexOf('[');
                if (lb >= 0)
                {
                    int rb = part.IndexOf(']', lb + 1);
                    if (rb > lb)
                    {
                        key = part.Substring(0, lb);
                        string idxStr = part.Substring(lb + 1, rb - lb - 1);
                        if (int.TryParse(idxStr, out int idx))
                            index = idx;
                    }
                }
    
                if (!string.IsNullOrEmpty(key))
                {
                    if (!TryGetFromMap(curr, key, out var next))
                        return null;
                    curr = next;
                }
    
                if (index.HasValue)
                {
                    int idx = index.Value;
    
                    if (curr is JArray ja)
                    {
                        int real = idx < 0 ? ja.Count + idx : idx;
                        if (real < 0 || real >= ja.Count) return null;
                        curr = ja[real];
                    }
                    else if (curr is List<object> list)
                    {
                        int real = idx < 0 ? list.Count + idx : idx;
                        if (real < 0 || real >= list.Count) return null;
                        curr = list[real];
                    }
                    else if (curr is System.Collections.IList ilist)
                    {
                        int real = idx < 0 ? ilist.Count + idx : idx;
                        if (real < 0 || real >= ilist.Count) return null;
                        curr = ilist[real];
                    }
                    else
                    {
                        return null;
                    }
                }
    
                if (curr is JValue jv)
                    curr = jv.Value;
            }
    
            if (curr is JValue jvv) return jvv.Value;
            return curr;
        }
    
        bool TryGetFromMap(object curr, string key, out object value)
        {
            value = null;
    
            if (curr is Dictionary<string, object> dict)
            {
                if (!dict.TryGetValue(key, out value))
                    return false;
                return true;
            }
    
            if (curr is JObject jo)
            {
                if (!jo.TryGetValue(key, out var tok))
                    return false;
    
                value = tok is JValue jv ? jv.Value : tok;
                return true;
            }
    
            return false;
        }
    
        string BuildId(string prefix, string metal, string suffix)
        {
            string p = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
            string m = string.IsNullOrWhiteSpace(metal) ? null : metal.Trim();
            string s = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
    
            if (string.IsNullOrEmpty(m)) return null;
    
            if (!string.IsNullOrEmpty(p) && !string.IsNullOrEmpty(s)) return $"{p} {m} {s}";
            if (!string.IsNullOrEmpty(p)) return $"{p} {m}";
            if (!string.IsNullOrEmpty(s)) return $"{m} {s}";
            return m;
        }
    }
}
