// CraftModule.cs
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class CraftModule : MonoBehaviour
{
    [Header("Inputs/Output")]
    public List<ItemSlot> inputs = new List<ItemSlot>(4); // 2~4 사용
    public ItemSlot output;

    [Header("Refs")]
    public RecipeLibrary recipeLibrary; // TryCraft(List<ItemData>, out ItemData, out JArray, out JObject)
    public Player        player;

    // 상태
    JObject _matched;
    JArray  _inActions;

    // 스냅샷
    ItemData[] _prevItems;
    int[]      _prevCounts;
    int[]      _prevDurs;

    void Awake()
    {
        if (inputs == null) inputs = new List<ItemSlot>(4);
        for (int i = 0; i < inputs.Count; i++)
        {
            var s = inputs[i];
            if (s == null) continue;
            s.useLocalStorage = true;
            s.denyUserPut     = false;
            s.Set(null);
        }
        if (output != null)
        {
            output.useLocalStorage = true;
            output.denyUserPut     = true;
            output.Set(null);
        }

        AllocSnapshot();
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
        if (player == null || player.Inventory == null) return;
        for (int i = 0; i < inputs.Count; i++)
        {
            var s = inputs[i];
            if (s == null || s.Item == null) continue;
            int left = player.Inventory.AddItem(s.Item);
            if (left == 0) s.Set(null);
            else { s.Item.Count = left; s.Refresh(); }
        }
    }

    void AllocSnapshot()
    {
        int n = Mathf.Max(0, inputs?.Count ?? 0);
        _prevItems  = new ItemData[n];
        _prevCounts = new int[n];
        _prevDurs   = new int[n];
    }

    bool Changed()
    {
        if (inputs == null) return false;
        if (_prevItems == null || _prevItems.Length != inputs.Count) { AllocSnapshot(); return true; }

        for (int i = 0; i < inputs.Count; i++)
        {
            var it = inputs[i]?.Item;
            int c  = it?.Count ?? 0;
            int d  = it?.Durability ?? 0;

            if (it != _prevItems[i] || c != _prevCounts[i] || d != _prevDurs[i]) return true;
        }
        return false;
    }

    void Snapshot()
    {
        if (inputs == null) return;
        if (_prevItems == null || _prevItems.Length != inputs.Count) AllocSnapshot();

        for (int i = 0; i < inputs.Count; i++)
        {
            var it = inputs[i]?.Item;
            _prevItems[i]  = it;
            _prevCounts[i] = it?.Count ?? 0;
            _prevDurs[i]   = it?.Durability ?? 0;
        }
    }

    void ScanAndPreview()
    {
        _matched = null; _inActions = null;
        if (output) output.Set(null);
        if (recipeLibrary == null) return;

        var snap = new List<ItemData>(inputs.Count);
        for (int i = 0; i < inputs.Count; i++) snap.Add(inputs[i]?.Item);

        if (recipeLibrary.TryCraft(snap, out ItemData resultItem, out JArray inputActions, out JObject matched))
        {
            _matched   = matched;
            _inActions = inputActions;
            output.Set(resultItem);
            return;
        }
        output.Set(null);
    }

    public void TryTakeOutput(ItemSlot cursorSlot)
    {
        if (recipeLibrary == null || output == null || cursorSlot == null) return;
        if (_matched == null || output.IsEmpty) return;

        var cur  = cursorSlot.Item;
        var prod = output.Item;
        if (prod == null) return;

        // 커서에 이미 다른 아이템이 있을 때 스택 가능 여부 검사
        if (cur != null)
        {
            if (cur.ItemId != prod.ItemId) return;
            if (cur.Count >= cur.MaxStack) return;
            int room = cur.MaxStack - cur.Count;
            if (prod.Count > room) return;
        }

        // 현재 슬롯 스냅샷으로 다시 시도 (중간에 인풋이 변했을 수 있으므로)
        var snap = new List<ItemData>(inputs.Count);
        for (int i = 0; i < inputs.Count; i++) snap.Add(inputs[i]?.Item);
        if (!recipeLibrary.TryCraft(snap, out ItemData fresh, out JArray inActs, out JObject matched))
        {
            ScanAndPreview();
            return;
        }
        _inActions = inActs; _matched = matched;

        // 결과 아이템 커서로 이동/합치기
        if (cur == null) cursorSlot.Set(fresh);
        else { cur.Count += fresh.Count; cursorSlot.Refresh(); }

        // 인풋 액션 적용
        ApplyInputActions(_inActions);

        Snapshot();
        ScanAndPreview();
    }

    void ApplyInputActions(JArray actions)
    {
        if (actions == null) return;
        int n = Mathf.Min(actions.Count, inputs.Count);

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
                if (slot.Item.Count <= 0) slot.Set(null);
                else slot.Refresh();
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
