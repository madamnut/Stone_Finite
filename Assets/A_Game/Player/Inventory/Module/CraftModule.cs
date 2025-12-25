// CraftModule.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class CraftModule : MonoBehaviour
{
    public enum TableType
    {
        Hand,   // 2-slot handcraft
        Primal  // 4-slot primal workbench
    }

    [Header("Table")]
    public TableType tableType = TableType.Hand;

    [Header("Inputs / Preview")]
    public List<ItemSlot> inputs  = new List<ItemSlot>(4); // 인풋 슬롯(최대 4)
    public List<ItemSlot> outputs = new List<ItemSlot>(2); // 결과 프리뷰 슬롯(2슬롯)

    [Header("UI")]
    public Button craftButton; // 크래프팅 실행 버튼

    [Header("Refs")]
    // TryCraft(List<ItemData>, out List<ItemData> resultItems, out JArray inputActions, out JObject matchedRecipe)
    public RecipeLibrary recipeLibrary;
    public Player        player;

    // 상태
    JObject _matched;
    JArray  _inActions;

    // 스냅샷
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
                case TableType.Hand:   return Mathf.Min(2, max);
                case TableType.Primal: return Mathf.Min(4, max);
                default:               return max;
            }
        }
    }

    void Awake()
    {
        // 인풋 슬롯 초기화
        if (inputs == null) inputs = new List<ItemSlot>(4);
        int active = ActiveInputCount;
        for (int i = 0; i < inputs.Count; i++)
        {
            var s = inputs[i];
            if (s == null) continue;

            s.useLocalStorage = true;
            s.denyUserPut = false;
            s.denyUserInteraction = false;
            s.Set(null);

            // 테이블 타입 기준으로 사용하지 않는 슬롯은 비활성화
            if (i >= active)
                s.gameObject.SetActive(false);
        }

        // 출력(프리뷰) 슬롯 초기화
        // ✅ 변경: 프리뷰는 "완전 상호작용 금지" (넣기/빼기 모두 금지)
        if (outputs == null) outputs = new List<ItemSlot>(2);
        for (int i = 0; i < outputs.Count; i++)
        {
            var s = outputs[i];
            if (s == null) continue;

            s.useLocalStorage = true;
            s.denyUserPut = true;
            s.denyUserInteraction = true; // 프리뷰 전용: 완전 조작 금지
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

        // 모듈 파괴 시, 인풋에 남아있는 아이템은 플레이어 인벤토리로 반환
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
    /// 인풋 슬롯 스냅샷으로 레시피 매칭 후, 결과 프리뷰만 갱신.
    /// 이 단계에서는 인풋액션·아이템 소모 없음.
    /// </summary>
    void ScanAndPreview()
    {
        _matched   = null;
        _inActions = null;

        // 프리뷰 슬롯 모두 초기화
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

            // 멀티 아웃풋 프리뷰 채우기 (최대 outputs.Count 개)
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

        // 매칭 실패 시 이미 위에서 출력 슬롯 모두 null 처리함
    }

    /// <summary>
    /// 버튼 온클릭 → 크래프팅 실행.
    /// </summary>
    public void OnClickCraft()
    {
        ExecuteCraft();
    }

    /// <summary>
    /// 크래프팅 실행:
    /// - 현재 인풋으로 다시 레시피 매칭
    /// - 성공 시 모든 결과 아이템을 플레이어 인벤토리에 지급
    /// - inputActions 적용(소모/내구도 감소 등)
    /// - 이후 다시 프리뷰 갱신
    /// </summary>
    public void ExecuteCraft()
    {
        if (recipeLibrary == null) return;
        if (player == null || player.Inventory == null) return;
        if (_matched == null) return; // 현재 유효한 매칭 없음

        int active = ActiveInputCount;

        // 현재 슬롯 상태로 다시 매칭 (인풋 변경 가능성 대비)
        var snap = new List<ItemData>(active);
        for (int i = 0; i < active; i++)
            snap.Add(inputs[i]?.Item);

        if (!recipeLibrary.TryCraft(snap, out List<ItemData> freshList, out JArray inActs, out JObject matched))
        {
            // 더 이상 유효한 레시피가 아니면 프리뷰만 갱신
            ScanAndPreview();
            return;
        }

        _inActions = inActs;
        _matched   = matched;

        if (freshList == null || freshList.Count == 0) return;

        // 결과 아이템들을 플레이어 인벤토리에 지급
        for (int i = 0; i < freshList.Count; i++)
        {
            var item = freshList[i];
            if (item == null) continue;
            player.Inventory.AddItem(item);
        }

        // 인풋 액션 적용 (소모/내구도 등)
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
                // 내구도 시스템 없는 아이템 (MaxDurability == 0) → 스킵
                if (slot.Item.MaxDurability <= 0) continue;

                // amount 는 음수면 감소, 양수면 회복
                slot.Item.ModifyDurability(amount);

                if (slot.Item.Durability <= 0)
                    slot.Set(null);
                else
                    slot.Refresh();
            }
        }
    }
}
