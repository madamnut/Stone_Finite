// CraftModule.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

using Game.Data;

namespace Game.Player
{
    public class CraftModule : MonoBehaviour
    {
        public enum TableType
        {
            Hand,       // 2-slot handcraft
            Primal,     // 4-slot primal workbench
            Forge,      // 9-slot forge workbench
            Industrial  // 16-slot industrial workbench
        }
    
        [Header("Table")]
        public TableType tableType = TableType.Hand;
    
        [Header("Inputs / Preview")]
        public List<ItemSlot> inputs  = new List<ItemSlot>(16); // ?명뭼 ?щ’(理쒕? 16)
        public List<ItemSlot> outputs = new List<ItemSlot>(2);  // 寃곌낵 ?꾨━酉??щ’(2?щ’)
    
        [Header("UI")]
        public Button craftButton; // ?щ옒?꾪똿 ?ㅽ뻾 踰꾪듉
    
        [Header("Refs")]
        // TryCraft(List<ItemData>, out List<ItemData> resultItems, out JArray inputActions, out JObject matchedRecipe)
        public RecipeLibrary recipeLibrary;
        public Player        player;
    
        // ?곹깭
        JObject _matched;
        JArray  _inActions;
    
        // ?ㅻ깄??
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
            // ?명뭼 ?щ’ 珥덇린??
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
    
                // ?뚯씠釉????湲곗??쇰줈 ?ъ슜?섏? ?딅뒗 ?щ’? 鍮꾪솢?깊솕
                if (i >= active)
                    s.gameObject.SetActive(false);
                else
                    s.gameObject.SetActive(true);
            }
    
            // 異쒕젰(?꾨━酉? ?щ’ 珥덇린??
            // ??蹂寃? ?꾨━酉곕뒗 "?꾩쟾 ?곹샇?묒슜 湲덉?" (?ｊ린/鍮쇨린 紐⑤몢 湲덉?)
            if (outputs == null) outputs = new List<ItemSlot>(2);
            for (int i = 0; i < outputs.Count; i++)
            {
                var s = outputs[i];
                if (s == null) continue;
    
                s.useLocalStorage = true;
                s.denyUserPut = true;
                s.denyUserInteraction = true; // ?꾨━酉??꾩슜: ?꾩쟾 議곗옉 湲덉?
                s.Set(null);
            }
    
            if (craftButton != null)
                craftButton.onClick.AddListener(OnClickCraft);
    
            AllocSnapshot();
            Snapshot();
            ScanAndPreview();
        }
    
        void OnDestroy()
        {
            if (craftButton != null)
                craftButton.onClick.RemoveListener(OnClickCraft);
    
            // 紐⑤뱢 ?뚭눼 ?? ?명뭼???⑥븘?덈뒗 ?꾩씠?쒖? ?뚮젅?댁뼱 ?몃깽?좊━濡?諛섑솚
            if (player == null || player.Inventory == null) return;
    
            int active = ActiveInputCount;
            for (int i = 0; i < active; i++)
            {
                var s = inputs[i];
                if (s == null || s.Item == null) continue;
                int left = player.Inventory.AddItem(s.Item);
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
    
        /// <summary>
        /// ?명뭼 ?щ’ ?ㅻ깄?룹쑝濡??덉떆??留ㅼ묶 ?? 寃곌낵 ?꾨━酉곕쭔 媛깆떊.
        /// ???④퀎?먯꽌???명뭼?≪뀡쨌?꾩씠???뚮え ?놁쓬.
        /// </summary>
        void ScanAndPreview()
        {
            _matched   = null;
            _inActions = null;
    
            // ?꾨━酉??щ’ 紐⑤몢 珥덇린??
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
    
                // 硫???꾩썐???꾨━酉?梨꾩슦湲?(理쒕? outputs.Count 媛?
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
    
            // 留ㅼ묶 ?ㅽ뙣 ???대? ?꾩뿉??異쒕젰 ?щ’ 紐⑤몢 null 泥섎━??
        }
    
        /// <summary>
        /// 踰꾪듉 ?⑦겢由????щ옒?꾪똿 ?ㅽ뻾.
        /// </summary>
        public void OnClickCraft()
        {
            ExecuteCraft();
        }
    
        /// <summary>
        /// ?щ옒?꾪똿 ?ㅽ뻾:
        /// - ?꾩옱 ?명뭼?쇰줈 ?ㅼ떆 ?덉떆??留ㅼ묶
        /// - ?깃났 ??紐⑤뱺 寃곌낵 ?꾩씠?쒖쓣 ?뚮젅?댁뼱 ?몃깽?좊━??吏湲?
        /// - inputActions ?곸슜(?뚮え/?닿뎄??媛먯냼 ??
        /// - ?댄썑 ?ㅼ떆 ?꾨━酉?媛깆떊
        /// </summary>
        public void ExecuteCraft()
        {
            if (recipeLibrary == null) return;
            if (player == null || player.Inventory == null) return;
            if (_matched == null) return; // ?꾩옱 ?좏슚??留ㅼ묶 ?놁쓬
    
            int active = ActiveInputCount;
    
            // ?꾩옱 ?щ’ ?곹깭濡??ㅼ떆 留ㅼ묶 (?명뭼 蹂寃?媛?μ꽦 ?鍮?
            var snap = new List<ItemData>(active);
            for (int i = 0; i < active; i++)
                snap.Add(inputs[i]?.Item);
    
            if (!recipeLibrary.TryCraft(snap, out List<ItemData> freshList, out JArray inActs, out JObject matched))
            {
                // ???댁긽 ?좏슚???덉떆?쇨? ?꾨땲硫??꾨━酉곕쭔 媛깆떊
                ScanAndPreview();
                return;
            }
    
            _inActions = inActs;
            _matched   = matched;
    
            if (freshList == null || freshList.Count == 0) return;
    
            // 寃곌낵 ?꾩씠?쒕뱾???뚮젅?댁뼱 ?몃깽?좊━??吏湲?
            for (int i = 0; i < freshList.Count; i++)
            {
                var item = freshList[i];
                if (item == null) continue;
                player.Inventory.AddItem(item);
            }
    
            // ?명뭼 ?≪뀡 ?곸슜 (?뚮え/?닿뎄????
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
                    // ?닿뎄???쒖뒪???녿뒗 ?꾩씠??(MaxDurability == 0) ???ㅽ궢
                    if (slot.Item.MaxDurability <= 0) continue;
    
                    // amount ???뚯닔硫?媛먯냼, ?묒닔硫??뚮났
                    slot.Item.ModifyDurability(amount);
    
                    if (slot.Item.Durability <= 0)
                        slot.Set(null);
                    else
                        slot.Refresh();
                }
                else if (type == "consumeMetal")
                {
                    // Crucible.details.layers ??"留???layers[-1])"?먯꽌 amount 留뚰겮 ?뚮え
                    // layers ?먯냼??JObject ?먮뒗 Dictionary<string, object> ?뺥깭瑜??덉슜
                    if (slot.Item.Details == null) continue;
                    if (!slot.Item.Details.TryGetValue("layers", out var layersObj) || layersObj == null) continue;
    
                    List<object> layers = null;
    
                    if (layersObj is List<object> list)
                    {
                        layers = list;
                    }
                    else if (layersObj is JArray jarr)
                    {
                        // normalize to List<object>
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
                            // ?쒕Ъ寃?JToken?쇰줈 ?ㅼ뼱??寃쎌슦
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
    }
}
