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

    // 상태
    JObject _matched;                 // 매칭된 레시피
    JArray  _actions;                 // 입력별 액션
    string  _outId;                   // 결과 ID
    int     _outCount;                // 결과 수량

    ItemData _prev0, _prev1; int _c0, _c1;

    void Awake()
    {
        if (input0) { input0.useLocalStorage = true; input0.denyUserPut = false; input0.Set(null);}
        if (input1) { input1.useLocalStorage = true; input1.denyUserPut = false; input1.Set(null);}
        if (output) { output.useLocalStorage = true; output.denyUserPut = true; output.Set(null); }

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

    bool Changed()
    {
        var a = input0 ? input0.Item : null;
        var b = input1 ? input1.Item : null;
        int ca = (a != null) ? a.Count : 0;
        int cb = (b != null) ? b.Count : 0;
        return a != _prev0 || b != _prev1 || ca != _c0 || cb != _c1;
    }

    void Snapshot()
    {
        _prev0 = input0 ? input0.Item : null; _c0 = (_prev0 != null) ? _prev0.Count : 0;
        _prev1 = input1 ? input1.Item : null; _c1 = (_prev1 != null) ? _prev1.Count : 0;
    }

    void ScanAndPreview()
    {
        _matched = null; _actions = null; _outId = null; _outCount = 0;
        if (!output) return;

        var a = input0 ? input0.Item : null;
        var b = input1 ? input1.Item : null;
        if (a == null || b == null) { output.Set(null); return; }

        if (recipeLibrary != null &&
            recipeLibrary.TryMatch2(a, b, out _outId, out _outCount, out _actions, out _matched))
        {
            // 미리보기 생성: 프로젝트의 ItemLibrary 팩토리 메서드로 교체 필요
            // 예: itemLibrary.Create(id, count) 또는 itemLibrary.Spawn/Instantiate 등
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

        // 입력 재검증: 수량 변동 등
        if (!(recipeLibrary != null &&
              recipeLibrary.TryMatch2(input0.Item, input1.Item, out _outId, out _outCount, out _actions, out _matched)))
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
            if (slot.Item.UniqueProps.TryGetValue("durability", out var d))
            {
                int cur = 0; int.TryParse(d.ToString(), out cur);
                cur += amount; // 음수면 감소
                // UniqueProps가 읽기 전용이면 수정 불가. 수정 가능 딕셔너리여야 함.
                slot.Item.UniqueProps["durability"] = cur;
                if (cur <= 0) slot.Set(null); else slot.Refresh();
            }
        }
    }
}
