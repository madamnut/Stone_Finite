// ToolbenchModule.cs (전체 교체본)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class ToolbenchModule : MonoBehaviour
{
    [Header("Deps")]
    public RecipeLibrary recipeLibrary; // 프리팹에서 연결
    public Player player;              // 프리팹에서 연결

    [Header("Slots")]
    public ItemSlot materialSlot;
    public ItemSlot toolSlot;
    public ItemSlot previewSlot; // 상호작용 불가

    [Header("Viewport")]
    public Transform viewportContent;     // GridLayoutGroup 붙은 Content
    public ItemSlot candidateSlotPrefab;  // 후보 슬롯 프리팹

    [Header("UI")]
    public Button craftButton;

    Toolbench _toolbench;

    List<ItemData> _candidates;
    JArray _inputActions;
    JObject _matchedRecipe;

    ItemSlot _selectedCandidateSlot; // 인덱스 대신 슬롯 참조
    ItemData _selectedCandidateItem; // 최신 후보 재계산 시 재선택 검증용

    // 후보 슬롯 캐시(선택/정리/언바인드 안정화)
    readonly List<ItemSlot> _candSlots = new List<ItemSlot>(32);

    // 입력 슬롯 변경 감지(2슬롯만)
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
        SetupSlot(toolSlot,     denyPut: false, denyInteraction: false);

        // 프리뷰: 완전 잠금
        if (previewSlot != null)
        {
            previewSlot.useLocalStorage     = true;
            previewSlot.denyUserPut         = true;
            previewSlot.denyUserInteraction = true;
            previewSlot.useAsButton         = false;
            previewSlot.Set(null);
        }

        // Toolbench 저장값 → UI
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

        ClearViewport(); // 후보 클릭 언바인드 + 오브젝트 정리
    }

    void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
    {
        if (slot == null) return;

        slot.useLocalStorage     = true;
        slot.denyUserPut         = denyPut;
        slot.denyUserInteraction = denyInteraction;
        slot.useAsButton         = false;

        if (slot.Item == null) slot.Set(null);
        else slot.Refresh();
    }

    void Update()
    {
        if (_toolbench == null) return;

        // 입력 변경 시: Toolbench 저장 + 후보 재계산
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

        if (mat != _prevMat || matCount != _prevMatCount || matDur != _prevMatDur) return true;
        if (tool != _prevTool || toolCount != _prevToolCount || toolDur != _prevToolDur) return true;

        return false;
    }

    void SnapshotInputs()
    {
        _prevMat = materialSlot != null ? materialSlot.Item : null;
        _prevMatCount = _prevMat != null ? _prevMat.Count : 0;
        _prevMatDur   = _prevMat != null ? _prevMat.Durability : 0;

        _prevTool = toolSlot != null ? toolSlot.Item : null;
        _prevToolCount = _prevTool != null ? _prevTool.Count : 0;
        _prevToolDur   = _prevTool != null ? _prevTool.Durability : 0;
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

        // 선택 초기화
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

            // ✅ 후보는 "버튼 모드"로 고정
            slot.useLocalStorage     = true;
            slot.denyUserPut         = true;
            slot.denyUserInteraction = false;
            slot.useAsButton         = true;

            // hover로 selectedImage를 건드리지 않으므로, 초기 선택표시는 꺼둠
            slot.SetSelected(false);

            // 아이템 표시
            slot.Set(it);

            // 클릭 바인딩
            slot.onClick += OnCandidateClicked;

            _candSlots.Add(slot);
        }
    }

    void ClearViewport()
    {
        // 캐시 기반으로 언바인드 + 파괴 (Transform 순회 중 Destroy 섞이는 문제 회피)
        for (int i = 0; i < _candSlots.Count; i++)
        {
            var s = _candSlots[i];
            if (s == null) continue;

            s.onClick -= OnCandidateClicked;
            Destroy(s.gameObject);
        }
        _candSlots.Clear();

        // 혹시 외부에서 자식이 추가된 케이스까지 정리
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

        // 최신 후보 재계산(입력 도중 바뀐 경우 방지)
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

        // 선택했던 candidate가 fresh 목록에 존재하는지 확인(itemId+count)
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

        // 결과 지급
        player.Inventory.AddItem(outItem);

        // 입력 액션 적용
        ApplyInputActions(freshInputActions);

        // Toolbench 저장 동기화
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
