// Cursor.cs  (시작 시 커서 슬롯 비우기 추가)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Cursor : MonoBehaviour
{
    [Header("이 커서는 Canvas 자식(UI)입니다")]
    public Canvas canvas;
    public ItemSlot cursorSlot;

    private RectTransform rt;
    private readonly List<RaycastResult> _hits = new List<RaycastResult>(8);

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (cursorSlot != null) cursorSlot.Set(null); // 시작시 하얀 사각형 방지
    }

    void Update()
    {
        if (rt == null || canvas == null) return;
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        if (canvas.renderMode == RenderMode.WorldSpace)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, Input.mousePosition, cam, out var world))
                rt.position = world;
        }
        else
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, Input.mousePosition, cam, out var local))
                rt.anchoredPosition = local;
        }

        if (Input.GetMouseButtonDown(0)) HandleClick(PointerEventData.InputButton.Left);
        if (Input.GetMouseButtonDown(1)) HandleClick(PointerEventData.InputButton.Right);
    }

    void HandleClick(PointerEventData.InputButton btn)
    {
        if (EventSystem.current == null || cursorSlot == null) return;

        _hits.Clear();
        var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        EventSystem.current.RaycastAll(data, _hits);

        ItemSlot slotView = null;
        for (int i = 0; i < _hits.Count; i++)
        {
            var s = _hits[i].gameObject.GetComponentInParent<ItemSlot>();
            if (s == null || s == cursorSlot || s.inventory == null) continue;
            if (s.index < 0 || s.index >= s.inventory.items.Count) continue;
            slotView = s; break;
        }
        if (slotView == null) return;

        var inv   = slotView.inventory;
        var items = inv.items;
        int idx   = slotView.index;

        var slot  = items[idx];
        var cur   = cursorSlot.Item;

        bool same = (cur != null && slot != null && cur.ItemId == slot.ItemId);
        int room  = slot != null ? (slot.MaxStack - slot.Count) : 0;

        if (btn == PointerEventData.InputButton.Left)
        {
            if (cur == null)
            {
                if (slot == null) return;
                cursorSlot.Set(slot);
                items[idx] = null;
            }
            else if (slot == null)
            {
                items[idx] = cur;
                cursorSlot.Set(null);
            }
            else if (same && room > 0)
            {
                int move = cur.Count < room ? cur.Count : room;
                slot.Count += move;
                cur.Count  -= move;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
            }
            else
            {
                items[idx] = cur;
                cursorSlot.Set(slot);
            }
            inv.NotifyChanged();
            return;
        }

        if (btn == PointerEventData.InputButton.Right)
        {
            if (cur == null && slot != null)
            {
                int take = (slot.Count + 1) / 2;
                var copy = new ItemData(
                    slot.ItemId, slot.Name, slot.SpriteName, slot.ItemType,
                    slot.MaxStack, new Dictionary<string, object>(slot.UniqueProps),
                    slot.Icon, take);
                cursorSlot.Set(copy);

                slot.Count -= take;
                if (slot.Count <= 0) items[idx] = null;
                inv.NotifyChanged();
                return;
            }

            if (cur != null && slot == null)
            {
                items[idx] = new ItemData(
                    cur.ItemId, cur.Name, cur.SpriteName, cur.ItemType,
                    cur.MaxStack, new Dictionary<string, object>(cur.UniqueProps),
                    cur.Icon, 1);
                cur.Count -= 1;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
                inv.NotifyChanged();
                return;
            }

            if (cur != null && same && slot.Count < slot.MaxStack)
            {
                slot.Count += 1;
                cur.Count  -= 1;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
                inv.NotifyChanged();
                return;
            }
        }
    }
}
