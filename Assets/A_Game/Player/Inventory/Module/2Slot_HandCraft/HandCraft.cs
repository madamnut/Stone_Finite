using UnityEngine;
using Newtonsoft.Json.Linq;

public class HandCraft : MonoBehaviour
{
    [Header("2-Slot Crafting")]
    public ItemSlot input0;   // 왼쪽(0)
    public ItemSlot input1;   // 오른쪽(1)
    public ItemSlot output;   // 결과(투입 금지)

    [Header("Refs")]
    public RecipeLibrary recipeLibrary; // TryMatch2 사용
    public ItemLibrary   itemLibrary;   // 결과 아이템 생성(프로젝트 API에 맞게 교체)
    public Player        player;        // ← 인풋 반환 대상 인벤 소유자

    // 상태
    JObject _matched;     // 매칭된 레시피
    JArray  _actions;     // 입력별 액션(슬롯 순서에 정렬된 상태)
    JArray  _outActions;  // 출력 액션(보관만)
    string  _outId;       // 결과 ID
    int     _outCount;    // 결과 수량

    ItemData _prev0, _prev1; int _c0, _c1, _d0, _d1;

    void Awake()
    {
        if (input0) { input0.useLocalStorage = true; input0.denyUserPut = false; input0.Set(null); }
        if (input1) { input1.useLocalStorage = true; input1.denyUserPut = false; input1.Set(null); }
        if (output) { output.useLocalStorage = true; output.denyUserPut = true;  output.Set(null); }

        Snapshot();
        ScanAndPreview();
    }

    void Update()
    {
        if (Changed())
        {
            Snapshot();
            ScanAndPreview();
        }
    }

    void OnDestroy()
    {
        // 인벤 패널 닫힐 때 모듈이 Destroy됨. 입력 슬롯을 플레이어 인벤으로 반환.
        if (player == null || player.Inventory == null) return;

        if (input0 != null && input0.Item != null)
        {
            int left = player.Inventory.AddItem(input0.Item);
            if (left == 0) input0.Set(null);
            else { input0.Item.Count = left; input0.Refresh(); }
        }

        if (input1 != null && input1.Item != null)
        {
            int left = player.Inventory.AddItem(input1.Item);
            if (left == 0) input1.Set(null);
            else { input1.Item.Count = left; input1.Refresh(); }
        }
        // output 슬롯은 반환하지 않음
    }

    bool Changed()
    {
        var a = input0 ? input0.Item : null;
        var b = input1 ? input1.Item : null;

        int ca = a?.Count ?? 0;
        int cb = b?.Count ?? 0;

        int da = 0;
        if (a != null && a.Unique.TryGetValue("durability", out var dv0))
            int.TryParse(dv0.ToString(), out da);

        int db = 0;
        if (b != null && b.Unique.TryGetValue("durability", out var dv1))
            int.TryParse(dv1.ToString(), out db);

        return a != _prev0 || b != _prev1 || ca != _c0 || cb != _c1 || da != _d0 || db != _d1;
    }

    void Snapshot()
    {
        _prev0 = input0 ? input0.Item : null; _c0 = _prev0?.Count ?? 0;
        _prev1 = input1 ? input1.Item : null; _c1 = _prev1?.Count ?? 0;

        _d0 = 0;
        if (_prev0 != null && _prev0.Unique.TryGetValue("durability", out var dv0))
            int.TryParse(dv0.ToString(), out _d0);

        _d1 = 0;
        if (_prev1 != null && _prev1.Unique.TryGetValue("durability", out var dv1))
            int.TryParse(dv1.ToString(), out _d1);
    }

    void ScanAndPreview()
    {
        _matched = null; _actions = null; _outActions = null; _outId = null; _outCount = 0;
        if (!output) return;

        var a = input0 ? input0.Item : null;
        var b = input1 ? input1.Item : null;
        if (a == null || b == null) { output.Set(null); return; }

        if (recipeLibrary != null &&
            recipeLibrary.TryMatch2(a, b, out _outId, out _outCount, out _actions, out _outActions, out _matched))
        {
            var preview = (itemLibrary != null) ? itemLibrary.Create(_outId, _outCount) : null;
            output.Set(preview);
            return;
        }

        output.Set(null);
    }

    /// <summary>출력 슬롯 클릭 시 1회 제작을 시도.</summary>
    public void TryTakeOutput(ItemSlot cursorSlot)
    {
        if (output == null || cursorSlot == null) return;
        if (_matched == null || output.IsEmpty) return;

        var cur = cursorSlot.Item;
        var outItem = output.Item;
        if (cur != null && (cur.ItemId != outItem.ItemId || cur.Count >= cur.MaxStack))
            return;

        // 입력 재검증
        if (!(recipeLibrary != null &&
              recipeLibrary.TryMatch2(input0.Item, input1.Item, out _outId, out _outCount, out _actions, out _outActions, out _matched)))
        {
            ScanAndPreview();
            return;
        }

        // 입력별 액션 1회 적용
        ApplyActionOnce(input0, _actions, 0);
        ApplyActionOnce(input1, _actions, 1);

        // 결과 1회량 지급
        if (cur == null)
        {
            var made = (itemLibrary != null) ? itemLibrary.Create(_outId, _outCount) : null;
            cursorSlot.Set(made);
        }
        else
        {
            cur.Count += _outCount;
            cursorSlot.Refresh();
        }

        Snapshot();
        ScanAndPreview();
    }

    void ApplyActionOnce(ItemSlot slot, JArray actions, int i)
    {
        if (slot == null || slot.Item == null) return;
        if (actions == null || actions.Count <= i) return;

        var act = actions[i] as JObject;
        if (act == null) return;

        string type = act.Value<string>("type");
        int amount  = act.Value<int?>("amount") ?? 1;

        if (type == "consume")
        {
            slot.Item.Count -= amount;
            if (slot.Item.Count <= 0) slot.Set(null); else slot.Refresh();
            return;
        }

        if (type == "durability")
        {
            if (slot.Item.Unique.TryGetValue("durability", out var d))
            {
                int cur = 0; int.TryParse(d.ToString(), out cur);
                cur += amount; // 음수면 감소
                slot.Item.Unique["durability"] = cur;
                if (cur <= 0) slot.Set(null); else slot.Refresh();
            }
        }
    }
}
