// ToolbenchModule.cs (????썹땟????????怨뺤툍??
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.World;
using Game.Player;
using Game.Core;

namespace Game.UI
{
    public class ToolbenchModule : MonoBehaviour
    {
        [Header("Deps")]
        public RecipeLibrary recipeLibrary; // ????썼キ?κ괌???????????쇰뮚??
        public Game.Player.Player player;              // ????썼キ?κ괌???????????쇰뮚??
    
        [Header("Slots")]
        public ItemSlot materialSlot;
        public ItemSlot toolSlot;
        public ItemSlot previewSlot; // ????濚?????????곗뵯??
    
        [Header("Viewport")]
        public Transform viewportContent;     // GridLayoutGroup ???거?? Content
        public ItemSlot candidateSlotPrefab;  // ????썹땟?雅?????????썼キ?κ괌??
    
        [Header("UI")]
        public Button craftButton;
    
        Toolbench _toolbench;
    
        List<ItemData> _candidates;
        JArray _inputActions;
        JObject _matchedRecipe;
    
        ItemSlot _selectedCandidateSlot; // ?癲ル슢??????????????꿔꺂??癰????
        ItemData _selectedCandidateItem; // ?꿔꺂????쭍??????썹땟?雅??????????????嚥▲굧???꿔꺂?ｉ뜮?뚮쑏??
    
        // ????썹땟?雅??????????????ｋ???癲ル슢???뚭괌??癲ル슢??酉귥춾?癲ル슢??蹂좊쨨????繹먮냱???
        readonly List<ItemSlot> _candSlots = new List<ItemSlot>(32);
    
        // ????怨몄７ ??????⑤슢堉?????醫딆┫??(2????????
        ItemData _prevMat;
        int _prevMatCount;
        int _prevMatDur;
    
        ItemData _prevTool;
        int _prevToolCount;
        int _prevToolDur;
    
        public void Bind(Toolbench toolbench)
        {
            _toolbench = toolbench;
    
            SetupSlot(materialSlot, denyPut: false, denyInteraction: false);
            SetupSlot(toolSlot, denyPut: false, denyInteraction: false);
    
            // ????썼キ?κ괌?? ????썹땟??????ャ렑??
            if (previewSlot != null)
            {
                ModuleSlotSyncUtility.ConfigureLocalSlot(previewSlot, denyPut: true, denyInteraction: true);
                previewSlot.useAsButton = false;
                previewSlot.Set(null);
            }
    
            // Toolbench ???轝꿸섣?????UI
            if (_toolbench != null)
            {
                if (materialSlot != null) materialSlot.Set(_toolbench.GetSlot(Toolbench.SlotKind.Material));
                if (toolSlot != null)     toolSlot.Set(_toolbench.GetSlot(Toolbench.SlotKind.Tool));
                if (previewSlot != null)  previewSlot.Set(_toolbench.GetSlot(Toolbench.SlotKind.Preview));
            }
    
            SnapshotInputs();
            RebuildCandidates();
        }
    
        void Awake()
        {
            if (craftButton != null)
                craftButton.onClick.AddListener(OnClickCraft);
    
            ClearViewport();
        }
    
        void OnDestroy()
        {
            if (craftButton != null)
                craftButton.onClick.RemoveListener(OnClickCraft);
    
            ClearViewport(); // ????썹땟?雅???????癲ル슢??酉귥춾?癲ル슢??蹂좊쨨?+ ?????鍮????곗뵯???癲ル슢???뚭괌?
        }
    
        void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
        {
            ModuleSlotSyncUtility.ConfigureLocalSlot(slot, denyPut, denyInteraction);
            if (slot != null) slot.useAsButton = false;
        }
    
        void Update()
        {
            if (_toolbench == null) return;
    
            // ????怨몄７ ??⑤슢堉????? Toolbench ????+ ????썹땟?雅??????
            if (InputsChanged())
            {
                PushInputsToToolbench();
                SnapshotInputs();
                RebuildCandidates();
            }
        }
    
        bool InputsChanged()
        {
            var mat = materialSlot != null ? materialSlot.Item : null;
            var tool = toolSlot != null ? toolSlot.Item : null;
    
            int matCount = mat != null ? mat.Count : 0;
            int matDur   = mat != null ? mat.Durability : 0;
    
            int toolCount = tool != null ? tool.Count : 0;
            int toolDur   = tool != null ? tool.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevMat, _prevMatCount, _prevMatDur, mat)) return true;
            if (ModuleSlotSyncUtility.HasChanged(_prevTool, _prevToolCount, _prevToolDur, tool)) return true;
    
            return false;
        }
    
        void SnapshotInputs()
        {
            ModuleSlotSyncUtility.Capture(materialSlot, ref _prevMat, ref _prevMatCount, ref _prevMatDur);
            ModuleSlotSyncUtility.Capture(toolSlot, ref _prevTool, ref _prevToolCount, ref _prevToolDur);
        }
    
        void PushInputsToToolbench()
        {
            if (_toolbench == null) return;
    
            if (materialSlot != null) _toolbench.SetSlot(Toolbench.SlotKind.Material, materialSlot.Item);
            if (toolSlot != null)     _toolbench.SetSlot(Toolbench.SlotKind.Tool, toolSlot.Item);
        }
    
        void RebuildCandidates()
        {
            _candidates = null;
            _inputActions = null;
            _matchedRecipe = null;
    
            // ????ｋ???潁??용끏???
            if (_selectedCandidateSlot != null)
                _selectedCandidateSlot.SetSelected(false);
    
            _selectedCandidateSlot = null;
            _selectedCandidateItem = null;
    
            if (previewSlot != null)
                previewSlot.Set(null);
    
            ClearViewport();
    
            Debug.Log($"[ToolbenchUI] RebuildCandidates: tb={_toolbench!=null}, recipeLib={recipeLibrary!=null}, player={player!=null}, prefab={candidateSlotPrefab!=null}, content={viewportContent!=null}");
    
            if (_toolbench == null) return;
            if (recipeLibrary == null) return;
    
            var mat = materialSlot != null ? materialSlot.Item : null;
            var tool = toolSlot != null ? toolSlot.Item : null;
    
            Debug.Log($"[ToolbenchUI] inputs: mat={(mat!=null?mat.ItemId:"null")} x{(mat!=null?mat.Count:0)}, tool={(tool!=null?tool.ItemId:"null")} x{(tool!=null?tool.Count:0)}");
    
            if (tool != null)
            {
                string keys = tool.ToolActions != null ? string.Join(",", tool.ToolActions.Keys) : "null";
                Debug.Log($"[ToolbenchUI] toolActions={keys}");
            }
    
            var inputs = new List<ItemData>(2) { mat, tool };
    
            if (!recipeLibrary.TryGetToolbenchCandidates(
                    inputs,
                    out List<ItemData> candidates,
                    out JArray remappedInputActions,
                    out JObject matchedRecipe))
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
                var it = _candidates[i];
                if (it == null) continue;
    
                var slot = Instantiate(candidateSlotPrefab, viewportContent);
    
                // ??????썹땟?雅??"?筌??????꿔꺂??袁ㅻ븶????????쒙쭫??
                slot.useLocalStorage     = true;
                slot.denyUserPut         = true;
                slot.denyUserInteraction = false;
                slot.useAsButton         = true;
    
                // hover??selectedImage???꿸쑨??????節됀?? ????⑤짅嫄ч썒??붹틦??? ?潁??용끏??????ｋ???嶺?筌?????Β?ル빝癲?
                slot.SetSelected(false);
    
                // ????썹땟?㈑????嶺?筌?
                slot.Set(it);
    
                // ??????熬곣뫖利????
                slot.onClick += OnCandidateClicked;
    
                _candSlots.Add(slot);
            }
        }
    
        void ClearViewport()
        {
            // ????????뚯???維◈????Β???癲ル슢??酉귥춾?癲ル슢??蹂좊쨨?+ ?????(Transform ????筌?嚥?Destroy ??????????戮?뜪??????ㅻ쿋??
            for (int i = 0; i < _candSlots.Count; i++)
            {
                var s = _candSlots[i];
                if (s == null) continue;
    
                s.onClick -= OnCandidateClicked;
                Destroy(s.gameObject);
            }
            _candSlots.Clear();
    
            // ???????癲?????????嶺?????ㅻ쿋????????먃????ㅿ폍筌β뮫?닺쳸留?? ?癲ル슢???뚭괌?
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
            if (player == null || player.Inventory == null) return;
            if (recipeLibrary == null) return;
    
            if (_selectedCandidateSlot == null) return;
            if (_selectedCandidateItem == null) return;
    
            // ?꿔꺂????쭍??????썹땟?雅??????????怨몄７ ????썹땟???熬곣뫖利?????嚥▲굧?????熬곣뫖?삥납?)
            var mat = materialSlot != null ? materialSlot.Item : null;
            var tool = toolSlot != null ? toolSlot.Item : null;
            var inputs = new List<ItemData>(2) { mat, tool };
    
            if (!recipeLibrary.TryGetToolbenchCandidates(
                    inputs,
                    out List<ItemData> freshCandidates,
                    out JArray freshInputActions,
                    out JObject freshRecipe))
            {
                RebuildCandidates();
                return;
            }
    
            if (freshCandidates == null || freshCandidates.Count == 0)
            {
                RebuildCandidates();
                return;
            }
    
            // ????ｋ??????겹렑 candidate??醫딆쓧? fresh ?꿔꺂??袁ㅻ븶筌믠뫀萸????됰슦??????β뼯爰귨㎘濡년뵾???? ?癲ル슢캉????itemId+count)
            int selectedIdx = -1;
            string wantId = _selectedCandidateItem.ItemId;
            int wantCount = _selectedCandidateItem.Count;
    
            for (int i = 0; i < freshCandidates.Count; i++)
            {
                var c = freshCandidates[i];
                if (c == null) continue;
                if (c.ItemId == wantId && c.Count == wantCount)
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
    
            // ?嚥▲굧?????꿔꺂?????
            player.Inventory.AddItem(outItem);
    
            // ????怨몄７ ?????Β?????쇨덫??
            ApplyInputActions(freshInputActions);
    
            // Toolbench ?????????ロ꺙??
            PushInputsToToolbench();
    
            SnapshotInputs();
            RebuildCandidates();
        }
    
        void ApplyInputActions(JArray actions)
        {
            if (actions == null) return;
    
            ApplyOne(actions, 0, materialSlot); // material
            ApplyOne(actions, 1, toolSlot);     // tool
        }
    
        void ApplyOne(JArray actions, int index, ItemSlot slot)
        {
            if (slot == null || slot.Item == null) return;
            if (index < 0 || index >= actions.Count) return;
    
            if (actions[index] == null || actions[index].Type == JTokenType.Null)
                return;
    
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
    }
}
