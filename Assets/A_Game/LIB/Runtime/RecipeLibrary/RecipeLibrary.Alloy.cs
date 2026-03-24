using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Game.Player;


namespace Game.Data
{
    public partial class RecipeLibrary
    {
        public bool TryApplyAlloysToCrucible(ItemData crucible)
        {
            if (_alloys.Count == 0) return false;
            if (crucible == null || crucible.Details == null) return false;
            if (!crucible.Details.TryGetValue("layers", out var layersObj) || layersObj == null) return false;
            if (layersObj is not IList layers) return false;
    
            bool changed = false;
    
            while (true)
            {
                bool applied = false;
    
                for (int r = 0; r < _alloys.Count; r++)
                {
                    var recipe = _alloys[r];
    
                    var totals = new Dictionary<string, int>();
                    for (int i = 0; i < layers.Count; i++)
                    {
                        if (!TryReadLayer(layers[i], out var id, out var amt)) continue;
                        if (string.IsNullOrEmpty(id) || amt <= 0) continue;
    
                        if (totals.TryGetValue(id, out var cur)) totals[id] = cur + amt;
                        else totals[id] = amt;
                    }
    
                    int batches = int.MaxValue;
                    for (int i = 0; i < recipe.inputs.Count; i++)
                    {
                        var (id, amt) = recipe.inputs[i];
                        totals.TryGetValue(id, out int have);
    
                        int b = have / amt;
                        if (b < batches) batches = b;
                        if (batches == 0) break;
                    }
    
                    if (batches <= 0 || batches == int.MaxValue)
                        continue;
    
                    for (int i = 0; i < recipe.inputs.Count; i++)
                    {
                        var (id, amt) = recipe.inputs[i];
                        ConsumeFromTop(layers, id, batches * amt);
                    }
    
                    AddOrStackAtTop(layers, recipe.outId, batches * recipe.outAmount);
    
                    applied = true;
                    changed = true;
                    break;
                }
    
                if (!applied) break;
            }
    
            return changed;
        }
    
        bool TryReadLayer(object layerObj, out string itemId, out int amount)
        {
            itemId = null;
            amount = 0;
    
            if (layerObj is JObject jo)
            {
                itemId = jo.Value<string>("itemId");
                amount = jo.Value<int?>("amount") ?? 0;
                return true;
            }
    
            if (layerObj is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("itemId", out var idObj)) itemId = idObj as string;
    
                if (dict.TryGetValue("amount", out var amtObj))
                {
                    if (amtObj is int i) amount = i;
                    else if (amtObj is long l) amount = (int)l;
                    else if (amtObj != null && int.TryParse(amtObj.ToString(), out int p)) amount = p;
                }
    
                return true;
            }
    
            return false;
        }
    
        void SetLayerAmount(IList layers, int index, int newAmount)
        {
            if (layers == null) return;
            if (index < 0 || index >= layers.Count) return;
    
            var elem = layers[index];
    
            if (elem is JObject jo)
            {
                jo["amount"] = newAmount;
                return;
            }
    
            if (elem is Dictionary<string, object> dict)
            {
                dict["amount"] = newAmount;
                return;
            }
        }
    
        void ConsumeFromTop(IList layers, string itemId, int need)
        {
            if (layers == null) return;
    
            for (int i = layers.Count - 1; i >= 0 && need > 0; i--)
            {
                if (!TryReadLayer(layers[i], out var id, out var amt)) continue;
                if (id != itemId || amt <= 0) continue;
    
                int take = Mathf.Min(amt, need);
                int left = amt - take;
                need -= take;
    
                if (left <= 0) layers.RemoveAt(i);
                else SetLayerAmount(layers, i, left);
            }
        }
    
        void AddOrStackAtTop(IList layers, string itemId, int addAmount)
        {
            if (layers == null) return;
            if (addAmount <= 0) return;
    
            if (layers.Count > 0 && TryReadLayer(layers[layers.Count - 1], out var id, out var amt) && id == itemId)
            {
                SetLayerAmount(layers, layers.Count - 1, amt + addAmount);
                return;
            }
    
            var jo = new JObject();
            jo["itemId"] = itemId;
            jo["amount"] = addAmount;
            layers.Add(jo);
        }
    }
}
