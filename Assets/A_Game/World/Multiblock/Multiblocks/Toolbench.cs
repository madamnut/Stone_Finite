


using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public partial class Toolbench : Multiblock
    {
        public enum SlotKind
        {
            Material,
            Tool,
            Preview
        }


        ItemData _material;
        ItemData _tool;
        ItemData _preview;

        
        readonly List<ItemData> _candidates = new List<ItemData>(16);
        public IReadOnlyList<ItemData> Candidates => _candidates;

        
        
        JArray _remappedInputActions; 
        JObject _matchedRecipe;       

        
        string _prevMatId;
        int _prevMatDur;
        int _prevMatCount;

        string _prevToolId;
        int _prevToolDur;
        int _prevToolCount;

        bool _droppedOnDestroy;

        
        public override void OnInteract(Vector2Int hitCell)
        {
            
            Manager?.OpenModule("Toolbench", this);
        }

        
        public ItemData GetSlot(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.Material => _material,
                SlotKind.Tool => _tool,
                SlotKind.Preview => _preview,
                _ => null
            };
        }

        
        public void SetSlot(SlotKind kind, ItemData item)
        {
            if (kind == SlotKind.Preview)
            {
                
                _preview = item;
                return;
            }

            if (kind == SlotKind.Material) _material = item;
            
            else if (kind == SlotKind.Tool) _tool = item;

            InvalidateIfInputsChanged();
        }

        
        public void ClearPreview()
        {
            _preview = null;
        }

        
        public void ClearCandidates()
        {
            _candidates.Clear();
            _remappedInputActions = null;
            _matchedRecipe = null;
        }

        
        
        
        
        
        
        
        public void SetCandidatesFromRecipe(
            List<ItemData> candidates,
            JArray remappedInputActions,
            JObject matchedRecipe = null)
        {
            _candidates.Clear();
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Count; i++)
                    if (candidates[i] != null)
                        _candidates.Add(candidates[i]);
            }

            _remappedInputActions = remappedInputActions;
            _matchedRecipe = matchedRecipe;

            
            _preview = null;
        }

        
        
        
        
        public void SelectCandidateToPreview(ItemData candidate)
        {
            _preview = candidate;
        }

        
        public bool CanCraftSelected()
        {
            if (_preview == null || _preview.Count <= 0) return false;
            if (_material == null) return false;
            if (_tool == null) return false;
            return true;
        }

        
        
        
        
        
        
        public bool TryCraftSelected(InventoryData inventory)
        {
            if (inventory == null) return false;
            if (!CanCraftSelected()) return false;

            
            var give = CloneItem(_preview);
            if (give == null) return false;

            int left = inventory.AddItem(give);
            if (left > 0)
            {
                
                
                
                return false;
            }

            
            ApplyInputActions();

            
            
            
            InvalidateIfInputsChanged();

            return true;
        }

        
        void ApplyInputActions()
        {
            
            if (_remappedInputActions == null) return;

            
            ApplyOneInputAction(ref _material, _remappedInputActions, 0);
            ApplyOneInputAction(ref _tool, _remappedInputActions, 1);
        }

        
        void ApplyOneInputAction(ref ItemData slotItem, JArray acts, int index)
        {
            if (acts == null) return;
            if (index < 0 || index >= acts.Count) return;
            if (acts[index] == null || acts[index].Type == JTokenType.Null) return;

            
            
            if (acts[index] is JArray arr)
            {
                for (int i = 0; i < arr.Count; i++)
                    ApplySingleAction(ref slotItem, arr[i] as JObject);
            }
            else
            {
                ApplySingleAction(ref slotItem, acts[index] as JObject);
            }
        }

        
        void ApplySingleAction(ref ItemData slotItem, JObject act)
        {
            if (act == null) return;

            string type = act.Value<string>("type");
            if (string.IsNullOrEmpty(type)) return;

            if (type == "consume")
            {
                int amt = act.Value<int?>("amount") ?? 0;
                if (amt <= 0) return;

                if (slotItem == null) return;

                slotItem.Count -= amt;
                if (slotItem.Count <= 0) slotItem = null;
                return;
            }

            if (type == "durability")
            {
                int amt = act.Value<int?>("amount") ?? 0; 
                if (amt == 0) return;

                if (slotItem == null) return;

                
                slotItem.ModifyDurability(amt);
                if (slotItem.Durability <= 0 && slotItem.MaxDurability > 0)
                    slotItem = null;

                return;
            }
        }

        
        void InvalidateIfInputsChanged()
        {
            string matId = _material != null ? _material.ItemId : null;
            int matDur = _material != null ? _material.Durability : 0;
            int matCnt = _material != null ? _material.Count : 0;

            string toolId = _tool != null ? _tool.ItemId : null;
            int toolDur = _tool != null ? _tool.Durability : 0;
            int toolCnt = _tool != null ? _tool.Count : 0;

            bool changed =
                matId != _prevMatId || matDur != _prevMatDur || matCnt != _prevMatCount ||
                toolId != _prevToolId || toolDur != _prevToolDur || toolCnt != _prevToolCount;

            _prevMatId = matId; _prevMatDur = matDur; _prevMatCount = matCnt;
            _prevToolId = toolId; _prevToolDur = toolDur; _prevToolCount = toolCnt;

            if (changed)
            {
                
                _preview = null;
                _candidates.Clear();
                _remappedInputActions = null;
                _matchedRecipe = null;
            }
        }

        
        ItemData CloneItem(ItemData src)
        {
            if (src == null) return null;

            
            return new ItemData(
                itemId: src.ItemId,
                name: src.Name,
                spriteName: src.SpriteName,
                itemType: src.ItemType,
                maxStack: src.MaxStack,
                maxDurability: src.MaxDurability,
                durability: src.Durability,
                toolActions: src.ToolActions,
                weaponActions: src.WeaponActions,
                breakActions: src.BreakActions,
                tags: src.Tags,
                details: src.Details,
                icon: src.Icon,
                count: src.Count
            );
        }

    }
}
