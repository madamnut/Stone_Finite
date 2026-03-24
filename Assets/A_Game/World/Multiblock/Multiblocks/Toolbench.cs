// Toolbench.cs
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Player;

namespace Game.World
{
    public class Toolbench : Multiblock
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
    
        // UI????곗뒧???熬곣뫀沅???????β돦裕녻キ??????熬곣뫀六? ???놁졑 ?곌떠??????????
        readonly List<ItemData> _candidates = new List<ItemData>(16);
        public IReadOnlyList<ItemData> Candidates => _candidates;
    
        // ?熬곣뫗???熬곣뫀沅???깅턄 ????????곕뻣??源낇뱺????븐뼔裕됬춯?뼿(?뺢퀗?????????????놁졑 ???嫄????⑸윞 ??⑤챷???
        // ?잙?裕?? inputs = [??筌? ?? ?????녌??띠럾??筌먐삳┃???????筌뤾퍓????고뱺 嶺뚮씮?????怨룻닡??븐뼔堉?
        JArray _remappedInputActions; // length=2, ?????爰??{type, ...} ???裕?null
        JObject _matchedRecipe;       // ??븐뼚?붺뭐??怨뺣뾼?????ルㅎ臾?
    
        // ???놁졑???꾩룆???????熬곣뫀沅??熬곣뱿遊????쒕샑???
        string _prevMatId;
        int _prevMatDur;
        int _prevMatCount;
    
        string _prevToolId;
        int _prevToolDur;
        int _prevToolCount;
    
        bool _droppedOnDestroy;
    
        public override void OnInteract(Game.Player.Player player, Vector2Int hitCell)
        {
            // MultiblockManager.OpenModule??Toolbench ??댟??怨룸츩 ?怨뺣뼺? ?熬곣뫗??
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
                // ?熬곣뱿遊?????UI????ｇ춯??筌뤿굝????⑤?源??얜????釉띾쐝?)
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
    
        /// <summary>
        /// RecipeLibrary 嶺뚮씞?됭눧??롪퍒???좊ご?Toolbench???낅슣???(ToolbenchModule??????筌뤾쑵??
        /// - candidates: ???х뙴?筌뤾쑬????戮?뻣???熬곣뫀沅??熬곣뫗逾??類ｊ독(?잙갭梨???????
        /// - remappedInputActions: [??筌???? ??怨룸???????⑤챷??????놁졑 ???떷?consume/durability ??
        /// - matchedRecipe: ??븐뼚?붺뭐??怨뺣뾼?????ルㅎ臾?
        /// </summary>
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
    
            // ?熬곣뫀沅뽪뤆?쎛 ?꾩룆???????熬곣뱿遊???????쒕샑?????ルㅎ臾???熬곣뫀沅?嶺뚮ㅄ維뽨빳?ぢ??釉띾쐠??뉎럦?臾먮쭑 ?????깅쾳)
            _preview = null;
        }
    
        /// <summary>
        /// ?熬곣뫀沅?????????熬곣뱿遊???▽빳???ルㅎ臾?(ToolbenchModule??????筌뤾쑵??
        /// </summary>
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
    
        /// <summary>
        /// ?뺢퀗??????????
        /// 1) ?熬곣뱿遊???熬곣뫗逾??戮곕굵 ??????怨룹꽑 ?筌뤾퍒萸???影?끸뵛(?熬곣뫁??
        /// 2) ?繹먭퍓沅??濡?듆 ???놁졑???떷???⑤챷????筌?consume / ??durability ??
        /// </summary>
        public bool TryCraftSelected(Game.Player.Player player)
        {
            if (player == null) return false;
            if (!CanCraftSelected()) return false;
            if (player.Inventory == null) return false;
    
            // ?筌뤾퍒萸?????곗꽑???熬곣뫗逾?????고돩?????沅?嶺뚣볦굣????ㅻ쾴?? ?꾩렮維?)
            var give = CloneItem(_preview);
            if (give == null) return false;
    
            int left = player.Inventory.AddItem(give);
            if (left > 0)
            {
                // ?熬곣뫁??????엷 ???덉넮嶺??β뼯?뉐첎????ル벣瑗????? ???놁졑 ???嫄?X, ???덉넮 嶺뚳퐣瑗??
                // (?筌뤾퍒萸?AddItem???遊붋??????엷 ??left???꾩룇瑗???濡ル츎 ?筌먐븍Ф嶺?
                //  ?熬곣뫗????뚮뿭寃?????????깆쓧???우벟 ???덉넮???곌랜??????놁졑?????嫄??? ???낅츎??
                return false;
            }
    
            // ???놁졑 ???떷???⑤챷??
            ApplyInputActions();
    
            // ?熬곣뱿遊???????????/??????袁⑤쭑嶺뚯솘? ?筌먦끉??
            // - ???놁졑????貫????깅さ嶺??띠룇?? ?熬곣뫀沅????⑥リ틭 ??戮곗굚???????깅さ????????????
            // - ???댁떳 ?熬곣뫀沅????놁졑 ?곌떠????띠럾???쒑땻?????깅さ???????類ｋ츎 ???놁졑?곌떠????롪틵???餓?嶺뚳퐣瑗??
            InvalidateIfInputsChanged();
    
            return true;
        }
    
        void ApplyInputActions()
        {
            // ?リ옇??? ???곕뻣??? ??怨몃さ嶺??熬곣뱭?≪물?용봾利?????
            if (_remappedInputActions == null) return;
    
            // ?????筌뤾퍓??? 0=material, 1=tool
            ApplyOneInputAction(ref _material, _remappedInputActions, 0);
            ApplyOneInputAction(ref _tool, _remappedInputActions, 1);
        }
    
        void ApplyOneInputAction(ref ItemData slotItem, JArray acts, int index)
        {
            if (acts == null) return;
            if (index < 0 || index >= acts.Count) return;
            if (acts[index] == null || acts[index].Type == JTokenType.Null) return;
    
            // ?リ옇????熬곣뫁夷??釉띾콦?????inputActions????關逾????듬땹??釉띾콦????⑤베裕???댟??怨룸츩?띠럾? 嶺뚮씭?섌뇡???? {type:"consume"...})
            // ?獄????꾩룄?ｈ굢??곌랜踰?????떷??????좊듆 ????嶺뚳퐣瑗??
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
                int amt = act.Value<int?>("amount") ?? 0; // ?곌랜???-1
                if (amt == 0) return;
    
                if (slotItem == null) return;
    
                // ItemData.ModifyDurability?띠럾? ?브퀡??????⑸윞 0??????????嶺뚳퐣瑗??????
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
                // ???놁졑???꾩룆???????熬곣뫀沅??熬곣뱿遊????????곕뻣 ??ｌ뫒亦??琉우꽑????
                _preview = null;
                _candidates.Clear();
                _remappedInputActions = null;
                _matchedRecipe = null;
            }
        }
    
        ItemData CloneItem(ItemData src)
        {
            if (src == null) return null;
    
            // ItemData???釉띾쐞?/?띠럾??곌떠? ??源놁궨??源껎맋 ???깆쓧???우벟 ???筌뤾쑬裕??怨룸츩???곌랜踰??
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
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Save / Load
        // ??????????????????????????????????????????????????????????????????????????????????????????
        public override SaveData ToSaveData()
        {
            var root = new JObject();
    
            JToken PackItem(ItemData it)
            {
                if (it == null || it.Count <= 0) return JValue.CreateNull();
                var o = new JObject();
                o["id"] = it.ItemId;
                o["count"] = it.Count;
                o["dur"] = it.Durability;
                return o;
            }
    
            root["material"] = PackItem(_material);
            root["tool"] = PackItem(_tool);
            root["preview"] = PackItem(_preview);
    
            // OriginalSolidIds (row-major)
            ushort[] orig = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                orig[x + y * Width] = originalSolidIds.TryGetValue(cell, out var id) ? id : (ushort)0;
            }
    
            return new SaveData
            {
                DefId = DefId,
                InstId = InstId,
                Origin = Origin,
                Width = Width,
                Height = Height,
                PayloadJson = root.ToString(),
                OriginalSolidIds = orig
            };
        }
    
        public override void FromSaveData(SaveData data)
        {
            DefId = data.DefId;
            InstId = data.InstId;
            Origin = data.Origin;
            Width = data.Width;
            Height = data.Height;
    
            occupiedCells.Clear();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));
    
            originalSolidIds.Clear();
            if (data.OriginalSolidIds != null && data.OriginalSolidIds.Length == Width * Height)
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                    originalSolidIds[cell] = data.OriginalSolidIds[x + y * Width];
                }
            }
    
            _material = _tool = _preview = null;
            _candidates.Clear();
            _remappedInputActions = null;
            _matchedRecipe = null;
    
            _droppedOnDestroy = false;
    
            _prevMatId = _prevToolId = null;
            _prevMatDur = _prevToolDur = 0;
            _prevMatCount = _prevToolCount = 0;
    
            if (string.IsNullOrEmpty(data.PayloadJson))
                return;
    
            JObject root = null;
            try { root = JObject.Parse(data.PayloadJson); }
            catch { root = null; }
            if (root == null) return;
    
            ItemData UnpackItem(JToken tok)
            {
                if (tok == null || tok.Type == JTokenType.Null) return null;
    
                string id = tok.Value<string>("id");
                int count = tok.Value<int?>("count") ?? 0;
                int dur = tok.Value<int?>("dur") ?? 0;
    
                if (string.IsNullOrEmpty(id) || count <= 0) return null;
    
                ItemData it = null;
                if (Manager != null && Manager.ItemLibrary != null)
                    it = Manager.ItemLibrary.Create(id, count);
    
                if (it != null)
                    it.Durability = dur;
    
                return it;
            }
    
            _material = UnpackItem(root["material"]);
            _tool = UnpackItem(root["tool"]);
            _preview = UnpackItem(root["preview"]);
    
            InvalidateIfInputsChanged();
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Break / Drop
        // ??????????????????????????????????????????????????????????????????????????????????????????
        public override void OnCellBroken(Vector2Int brokenCell)
        {
            if (!_droppedOnDestroy)
            {
                _droppedOnDestroy = true;
                DropIfAny(_material);
                DropIfAny(_tool);
                DropIfAny(_preview);
            }
    
            base.OnCellBroken(brokenCell);
        }
    
        void DropIfAny(ItemData it)
        {
            if (it == null || it.Count <= 0) return;
            if (World == null || World.itemDropper == null) return;
    
            Vector3 origin = new Vector3(
                Origin.x + (Width * 0.5f),
                Origin.y + (Height * 0.5f),
                0f
            );
    
            var copy = CloneItem(it);
            World.itemDropper.SpawnDroppedItem(copy, origin);
        }
    }
}
