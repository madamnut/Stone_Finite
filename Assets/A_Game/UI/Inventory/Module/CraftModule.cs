


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Core;

namespace Game.UI
{
    public partial class CraftModule : MonoBehaviour, IInventoryOwnerConsumer
    {
        public enum TableType
        {
            Hand,       
            Primal,     
            Forge,      
            Industrial  
        }
    
        [Header("Table")]

        public TableType tableType = TableType.Hand;
    
        [Header("Inputs / Preview")]
        public List<ItemSlot> inputs  = new List<ItemSlot>(16); 
        public List<ItemSlot> outputs = new List<ItemSlot>(2);  
    
        [Header("UI")]
        public Button craftButton; 
    
        [Header("Refs")]
        
        public RecipeLibrary recipeLibrary;
        [FormerlySerializedAs("player")]
        [SerializeField] private MonoBehaviour inventoryOwnerComponent;
    
        
        JObject _matched;
        JArray  _inActions;
        IInventoryOwner _inventoryOwner;
    
        
        ItemData[] _prevItems;
        int[]      _prevCounts;
        int[]      _prevDurs;
    
        int ActiveInputCount
        {
            get
            {
                int max = inputs?.Count ?? 0;
                switch (tableType)
                {
                    case TableType.Hand:       return Mathf.Min(2, max);
                    case TableType.Primal:     return Mathf.Min(4, max);
                    case TableType.Forge:      return Mathf.Min(9, max);
                    case TableType.Industrial: return Mathf.Min(16, max);
                    default:                   return max;
                }
            }
        }
    
        
        void Awake()
        {
            
            if (inputs == null) inputs = new List<ItemSlot>(16);
            int active = ActiveInputCount;
    
            for (int i = 0; i < inputs.Count; i++)
            {
                var s = inputs[i];
                if (s == null) continue;
    
                s.useLocalStorage = true;
                s.denyUserPut = false;
                s.denyUserInteraction = false;
                s.Set(null);
    
                
                if (i >= active)
                    s.gameObject.SetActive(false);
                else
                    s.gameObject.SetActive(true);
            }
    
            
            
            if (outputs == null) outputs = new List<ItemSlot>(2);
            for (int i = 0; i < outputs.Count; i++)
            {
                var s = outputs[i];
                if (s == null) continue;
    
                s.useLocalStorage = true;
                s.denyUserPut = true;
                s.denyUserInteraction = true; 
                s.Set(null);
            }
    
            if (craftButton != null)
                craftButton.onClick.AddListener(OnClickCraft);
    
            ResolveInventoryOwner();
            AllocSnapshot();
            Snapshot();
            ScanAndPreview();
        }
    
        
        void OnDestroy()
        {
            if (craftButton != null)
                craftButton.onClick.RemoveListener(OnClickCraft);
    
            
            var inventory = GetInventory();
            if (inventory == null) return;
    
            int active = ActiveInputCount;
            for (int i = 0; i < active; i++)
            {
                var s = inputs[i];
                if (s == null || s.Item == null) continue;
                int left = inventory.AddItem(s.Item);
                if (left == 0) s.Set(null);
                else { s.Item.Count = left; s.Refresh(); }
            }
        }
    
        
        void Update()
        {
            if (Changed())
            {
                Snapshot();
                ScanAndPreview();
            }
        }
    
        
        void AllocSnapshot()
        {
            int n = Mathf.Max(0, ActiveInputCount);
            _prevItems  = new ItemData[n];
            _prevCounts = new int[n];
            _prevDurs   = new int[n];
        }
    
        
        bool Changed()
        {
            if (inputs == null) return false;
    
            int active = ActiveInputCount;
    
            if (_prevItems == null || _prevItems.Length != active)
            {
                AllocSnapshot();
                return true;
            }
    
            for (int i = 0; i < active; i++)
            {
                var it = inputs[i]?.Item;
                int c  = it?.Count ?? 0;
                int d  = it?.Durability ?? 0;
    
                if (it != _prevItems[i] || c != _prevCounts[i] || d != _prevDurs[i])
                    return true;
            }
            return false;
        }
    
        
        void Snapshot()
        {
            if (inputs == null) return;
    
            int active = ActiveInputCount;
            if (_prevItems == null || _prevItems.Length != active)
                AllocSnapshot();
    
            for (int i = 0; i < active; i++)
            {
                var it = inputs[i]?.Item;
                _prevItems[i]  = it;
                _prevCounts[i] = it?.Count ?? 0;
                _prevDurs[i]   = it?.Durability ?? 0;
            }
        }
    
        
        
        
        
        #if false
        void ScanAndPreview()
        {
            _matched   = null;
            _inActions = null;
    
            
            if (outputs != null)
            {
                for (int i = 0; i < outputs.Count; i++)
                    if (outputs[i] != null) outputs[i].Set(null);
            }
    
            if (recipeLibrary == null) return;
    
            int active = ActiveInputCount;
            var snap = new List<ItemData>(active);
            for (int i = 0; i < active; i++)
                snap.Add(inputs[i]?.Item);
    
            if (recipeLibrary.TryCraft(snap, out List<ItemData> resultItems, out JArray inputActions, out JObject matched))
            {
                _matched   = matched;
                _inActions = inputActions;
    
                
                if (outputs != null)
                {
                    for (int i = 0; i < outputs.Count; i++)
                    {
                        var slot = outputs[i];
                        if (slot == null) continue;
    
                        if (resultItems != null && i < resultItems.Count)
                            slot.Set(resultItems[i]);
                        else
                            slot.Set(null);
                    }
                }
                return;
            }
    
            
        }
    
        
        
        
        public void OnClickCraft()
        {
            ExecuteCraft();
        }
    
        
        
        
        
        
        
        
        public void ExecuteCraft()
        {
            if (recipeLibrary == null) return;
            var inventory = GetInventory();
            if (inventory == null) return;
            if (_matched == null) return; 
    
            int active = ActiveInputCount;
    
            
            var snap = new List<ItemData>(active);
            for (int i = 0; i < active; i++)
                snap.Add(inputs[i]?.Item);
    
            if (!recipeLibrary.TryCraft(snap, out List<ItemData> freshList, out JArray inActs, out JObject matched))
            {
                
                ScanAndPreview();
                return;
            }
    
            _inActions = inActs;
            _matched   = matched;
    
            if (freshList == null || freshList.Count == 0) return;
    
            
            for (int i = 0; i < freshList.Count; i++)
            {
                var item = freshList[i];
                if (item == null) continue;
                inventory.AddItem(item);
            }
    
            
            ApplyInputActions(_inActions);
    
            Snapshot();
            ScanAndPreview();
        }
    
        void ApplyInputActions(JArray actions)
        {
            if (actions == null) return;
    
            int active = ActiveInputCount;
            int n = Mathf.Min(actions.Count, active);
    
            for (int i = 0; i < n; i++)
            {
                var slot = inputs[i];
                if (slot == null || slot.Item == null) continue;
    
                var act = actions[i] as JObject;
                if (act == null) continue;
    
                string type = act.Value<string>("type");
                int amount  = act.Value<int?>("amount") ?? 1;
    
                if (type == "consume")
                {
                    slot.Item.Count -= amount;
                    if (slot.Item.Count <= 0)
                        slot.Set(null);
                    else
                        slot.Refresh();
                }
                else if (type == "durability")
                {
                    
                    if (slot.Item.MaxDurability <= 0) continue;
    
                    
                    slot.Item.ModifyDurability(amount);
    
                    if (slot.Item.Durability <= 0)
                        slot.Set(null);
                    else
                        slot.Refresh();
                }
                else if (type == "consumeMetal")
                {
                    
                    
                    if (slot.Item.Details == null) continue;
                    if (!slot.Item.Details.TryGetValue("layers", out var layersObj) || layersObj == null) continue;
    
                    List<object> layers = null;
    
                    if (layersObj is List<object> list)
                    {
                        layers = list;
                    }
                    else if (layersObj is JArray jarr)
                    {
                        
                        layers = new List<object>(jarr.Count);
                        for (int k = 0; k < jarr.Count; k++)
                            layers.Add(jarr[k]);
                        slot.Item.SetDetail("layers", layers);
                    }
                    else
                    {
                        continue;
                    }
    
                    int need = Mathf.Max(1, amount);
    
                    while (need > 0 && layers.Count > 0)
                    {
                        int topIndex = layers.Count - 1;
                        object top = layers[topIndex];
    
                        int topAmt = 0;
    
                        if (top is JObject jo)
                        {
                            topAmt = jo.Value<int?>("amount") ?? 0;
                        }
                        else if (top is Dictionary<string, object> dict)
                        {
                            if (dict.TryGetValue("amount", out var aObj) && aObj != null)
                            {
                                if (aObj is int ai) topAmt = ai;
                                else if (aObj is long al) topAmt = (int)al;
                                else if (aObj is float af) topAmt = Mathf.RoundToInt(af);
                                else if (aObj is double ad) topAmt = (int)ad;
                                else int.TryParse(aObj.ToString(), out topAmt);
                            }
                        }
                        else if (top is JToken tok)
                        {
                            
                            if (tok.Type == JTokenType.Object)
                            {
                                var o = (JObject)tok;
                                topAmt = o.Value<int?>("amount") ?? 0;
                            }
                        }
    
                        if (topAmt <= 0)
                        {
                            layers.RemoveAt(topIndex);
                            continue;
                        }
    
                        int take = Mathf.Min(topAmt, need);
                        int left = topAmt - take;
                        need -= take;
    
                        if (left <= 0)
                        {
                            layers.RemoveAt(topIndex);
                        }
                        else
                        {
                            if (top is JObject jo2)
                                jo2["amount"] = left;
                            else if (top is Dictionary<string, object> dict2)
                                dict2["amount"] = left;
                            else if (top is JToken tok2 && tok2.Type == JTokenType.Object)
                                ((JObject)tok2)["amount"] = left;
                        }
                    }
    
                    slot.Refresh();
                }
            }
        }

        void ResolveInventoryOwner()
        {
            _inventoryOwner = inventoryOwnerComponent as IInventoryOwner;
            if (inventoryOwnerComponent != null && _inventoryOwner == null)
                Debug.LogWarning($"[CraftModule] Assigned component on {name} does not implement IInventoryOwner.", this);
        }

        InventoryData GetInventory()
        {
            if (_inventoryOwner == null)
                ResolveInventoryOwner();
            return _inventoryOwner != null ? _inventoryOwner.Inventory : null;
        }

        public void SetInventoryOwner(IInventoryOwner inventoryOwner)
        {
            _inventoryOwner = inventoryOwner;
            inventoryOwnerComponent = inventoryOwner as MonoBehaviour;
        }
        #endif
    }
}
