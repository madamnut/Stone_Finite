// Toolbench.cs
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

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

    // UI에 뿌릴 후보들(저장/로드 대상 아님: 입력 변경 시 재계산)
    readonly List<ItemData> _candidates = new List<ItemData>(16);
    public IReadOnlyList<ItemData> Candidates => _candidates;

    // 현재 후보들이 어떤 레시피에서 왔는지(버튼 눌렀을 때 입력 소모/내구 적용용)
    // 규칙: inputs = [재료, 툴] 이라고 가정하고 슬롯 인덱스에 맞춰 담아둔다.
    JArray _remappedInputActions; // length=2, 각 원소는 {type, ...} 또는 null
    JObject _matchedRecipe;       // 디버깅/추적용(선택)

    // 입력이 바뀌면 후보/프리뷰 무효화
    string _prevMatId;
    int _prevMatDur;
    int _prevMatCount;

    string _prevToolId;
    int _prevToolDur;
    int _prevToolCount;

    bool _droppedOnDestroy;

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        // MultiblockManager.OpenModule에 Toolbench 케이스 추가 필요
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
            // 프리뷰는 UI에서만 세팅(상호작용 불가)
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
    /// RecipeLibrary 매칭 결과를 Toolbench에 주입 (ToolbenchModule에서 호출)
    /// - candidates: 뷰포트에 표시할 후보 아이템들(그대로 사용)
    /// - remappedInputActions: [재료슬롯, 툴슬롯]에 적용할 입력 액션(consume/durability 등)
    /// - matchedRecipe: 디버깅/추적용(선택)
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

        // 후보가 바뀌면 프리뷰는 무효화(선택이 후보 목록과 불일치할 수 있음)
        _preview = null;
    }

    /// <summary>
    /// 후보 클릭 시 프리뷰로 선택 (ToolbenchModule에서 호출)
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
    /// 버튼 눌렀을 때:
    /// 1) 프리뷰 아이템을 플레이어 인벤에 넣기(전량)
    /// 2) 성공하면 입력액션 적용(재료 consume / 툴 durability 등)
    /// </summary>
    public bool TryCraftSelected(Player player)
    {
        if (player == null) return false;
        if (!CanCraftSelected()) return false;
        if (player.Inventory == null) return false;

        // 인벤에 들어갈 아이템 스냅샷(원본 참조 공유 방지)
        var give = CloneItem(_preview);
        if (give == null) return false;

        int left = player.Inventory.AddItem(give);
        if (left > 0)
        {
            // 전량 삽입 실패면 롤백이 애매하므로: 입력 소모 X, 실패 처리
            // (인벤 AddItem이 부분 삽입 후 left를 반환하는 형태면,
            //  현재 구현에서는 안전하게 실패로 보고 입력을 소모하지 않는다)
            return false;
        }

        // 입력 액션 적용
        ApplyInputActions();

        // 프리뷰는 유지할지/클리어할지 정책:
        // - 입력이 남아있으면 같은 후보를 연속 제작할 수 있으니 유지해도 됨.
        // - 다만 후보/입력 변동 가능성이 있으니 여기서는 입력변경 검사로 처리.
        InvalidateIfInputsChanged();

        return true;
    }

    void ApplyInputActions()
    {
        // 기본: 레시피가 없으면 아무것도 안 함
        if (_remappedInputActions == null) return;

        // 슬롯 인덱스: 0=material, 1=tool
        ApplyOneInputAction(ref _material, _remappedInputActions, 0);
        ApplyOneInputAction(ref _tool, _remappedInputActions, 1);
    }

    void ApplyOneInputAction(ref ItemData slotItem, JArray acts, int index)
    {
        if (acts == null) return;
        if (index < 0 || index >= acts.Count) return;
        if (acts[index] == null || acts[index].Type == JTokenType.Null) return;

        // 기존 프로젝트에서 inputActions는 단일 오브젝트를 쓰는 케이스가 많았음(예: {type:"consume"...})
        // 혹시 배열(복수 액션)로 오면 둘 다 처리.
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
            int amt = act.Value<int?>("amount") ?? 0; // 보통 -1
            if (amt == 0) return;

            if (slotItem == null) return;

            // ItemData.ModifyDurability가 존재(내구 0이면 파괴 처리 포함)
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
            // 입력이 바뀌면 후보/프리뷰는 다시 계산되어야 함
            _preview = null;
            _candidates.Clear();
            _remappedInputActions = null;
            _matchedRecipe = null;
        }
    }

    ItemData CloneItem(ItemData src)
    {
        if (src == null) return null;

        // ItemData는 불변/가변 혼재라서 안전하게 새 인스턴스로 복제
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

    // ─────────────────────────────────────────────
    // Save / Load
    // ─────────────────────────────────────────────
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

    // ─────────────────────────────────────────────
    // Break / Drop
    // ─────────────────────────────────────────────
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
