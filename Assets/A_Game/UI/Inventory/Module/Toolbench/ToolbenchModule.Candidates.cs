using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.World;
using Game.Core;

namespace Game.UI
{
    public partial class ToolbenchModule
    {
        void RebuildCandidates()
        {
            _candidates = null;
            _inputActions = null;
            _matchedRecipe = null;

            if (_selectedCandidateSlot != null)
                _selectedCandidateSlot.SetSelected(false);

            _selectedCandidateSlot = null;
            _selectedCandidateItem = null;

            if (previewSlot != null)
                previewSlot.Set(null);

            ClearViewport();

            Debug.Log($"[ToolbenchUI] RebuildCandidates: tb={_toolbench != null}, recipeLib={recipeLibrary != null}, inventoryOwner={_inventoryOwner != null}, prefab={candidateSlotPrefab != null}, content={viewportContent != null}");

            if (_toolbench == null) return;
            if (recipeLibrary == null) return;

            var mat = materialSlot != null ? materialSlot.Item : null;
            var tool = toolSlot != null ? toolSlot.Item : null;

            Debug.Log($"[ToolbenchUI] inputs: mat={(mat != null ? mat.ItemId : "null")} x{(mat != null ? mat.Count : 0)}, tool={(tool != null ? tool.ItemId : "null")} x{(tool != null ? tool.Count : 0)}");

            if (tool != null)
            {
                string keys = tool.ToolActions != null ? string.Join(",", tool.ToolActions.Keys) : "null";
                Debug.Log($"[ToolbenchUI] toolActions={keys}");
            }

            var inputs = new List<ItemData>(2) { mat, tool };

            if (!recipeLibrary.TryGetToolbenchCandidates(inputs, out List<ItemData> candidates, out JArray remappedInputActions, out JObject matchedRecipe))
            {
                Debug.Log("[ToolbenchUI] TryGetToolbenchCandidates = false");
                return;
            }

            if (candidates == null || candidates.Count == 0)
            {
                Debug.Log("[ToolbenchUI] candidates = 0");
                return;
            }

            Debug.Log($"[ToolbenchUI] candidates = {candidates.Count}");

            _candidates = candidates;
            _inputActions = remappedInputActions;
            _matchedRecipe = matchedRecipe;

            if (candidateSlotPrefab == null || viewportContent == null)
                return;

            for (int i = 0; i < _candidates.Count; i++)
            {
                var item = _candidates[i];
                if (item == null) continue;

                var slot = Instantiate(candidateSlotPrefab, viewportContent);
                slot.useLocalStorage = true;
                slot.denyUserPut = true;
                slot.denyUserInteraction = false;
                slot.useAsButton = true;
                slot.SetSelected(false);
                slot.Set(item);
                slot.onClick += OnCandidateClicked;
                _candSlots.Add(slot);
            }
        }

        void ClearViewport()
        {
            for (int i = 0; i < _candSlots.Count; i++)
            {
                var slot = _candSlots[i];
                if (slot == null) continue;

                slot.onClick -= OnCandidateClicked;
                Destroy(slot.gameObject);
            }
            _candSlots.Clear();

            if (viewportContent == null) return;
            for (int i = viewportContent.childCount - 1; i >= 0; i--)
                Destroy(viewportContent.GetChild(i).gameObject);
        }

        void OnCandidateClicked(ItemSlot slot)
        {
            if (slot == null) return;
            if (_candidates == null) return;
            if (slot.Item == null) return;

            if (_selectedCandidateSlot != null)
                _selectedCandidateSlot.SetSelected(false);

            _selectedCandidateSlot = slot;
            _selectedCandidateSlot.SetSelected(true);
            _selectedCandidateItem = slot.Item;

            if (previewSlot != null)
                previewSlot.Set(slot.Item);

            if (_toolbench != null)
                _toolbench.SetSlot(Toolbench.SlotKind.Preview, slot.Item);
        }

        void OnClickCraft()
        {
            if (_toolbench == null) return;
            var inventory = GetInventory();
            if (inventory == null) return;
            if (recipeLibrary == null) return;
            if (_selectedCandidateSlot == null) return;
            if (_selectedCandidateItem == null) return;

            var mat = materialSlot != null ? materialSlot.Item : null;
            var tool = toolSlot != null ? toolSlot.Item : null;
            var inputs = new List<ItemData>(2) { mat, tool };

            if (!recipeLibrary.TryGetToolbenchCandidates(inputs, out List<ItemData> freshCandidates, out JArray freshInputActions, out JObject freshRecipe))
            {
                RebuildCandidates();
                return;
            }

            if (freshCandidates == null || freshCandidates.Count == 0)
            {
                RebuildCandidates();
                return;
            }

            int selectedIdx = -1;
            string wantId = _selectedCandidateItem.ItemId;
            int wantCount = _selectedCandidateItem.Count;

            for (int i = 0; i < freshCandidates.Count; i++)
            {
                var candidate = freshCandidates[i];
                if (candidate == null) continue;
                if (candidate.ItemId == wantId && candidate.Count == wantCount)
                {
                    selectedIdx = i;
                    break;
                }
            }

            if (selectedIdx < 0)
            {
                RebuildCandidates();
                return;
            }

            var outItem = freshCandidates[selectedIdx];
            if (outItem == null) return;

            inventory.AddItem(outItem);

            ApplyInputActions(freshInputActions);
            PushInputsToToolbench();
            SnapshotInputs();
            RebuildCandidates();
        }

        void ApplyInputActions(JArray actions)
        {
            if (actions == null) return;

            ApplyOne(actions, 0, materialSlot);
            ApplyOne(actions, 1, toolSlot);
        }

        void ApplyOne(JArray actions, int index, ItemSlot slot)
        {
            if (slot == null || slot.Item == null) return;
            if (index < 0 || index >= actions.Count) return;
            if (actions[index] == null || actions[index].Type == JTokenType.Null) return;

            var act = actions[index] as JObject;
            if (act == null) return;

            string type = act.Value<string>("type");
            int amount = act.Value<int?>("amount") ?? 0;

            if (string.IsNullOrEmpty(type) || amount == 0)
                return;

            if (type == "consume")
            {
                slot.Item.Count -= amount;
                if (slot.Item.Count <= 0) slot.Set(null);
                else slot.Refresh();
            }
            else if (type == "durability")
            {
                slot.Item.ModifyDurability(amount);
                if (slot.Item.MaxDurability > 0 && slot.Item.Durability <= 0) slot.Set(null);
                else slot.Refresh();
            }
        }

        void ResolveInventoryOwner()
        {
            _inventoryOwner = inventoryOwnerComponent as IInventoryOwner;
            if (inventoryOwnerComponent != null && _inventoryOwner == null)
                Debug.LogWarning($"[ToolbenchModule] Assigned component on {name} does not implement IInventoryOwner.", this);
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
    }
}
