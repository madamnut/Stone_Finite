// Cursor.cs
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class Cursor : MonoBehaviour
{
    [Header("이 커서는 Canvas 자식(UI)입니다")]
    public Canvas canvas;
    public ItemSlot cursorSlot;

    [Header("Tooltip")]
    public TMP_Text tooltipText;      // 커서 자식 TMP_Text
    public GameObject tooltipObject;  // 보이기/숨기기용(옵션)

    private RectTransform rt;
    private readonly List<RaycastResult> _hits = new List<RaycastResult>(8);

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        if (cursorSlot != null) cursorSlot.Set(null);
        if (tooltipObject != null) tooltipObject.SetActive(false);
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

        // ── 현재 호버 중인 ItemSlot 탐지 ──
        ItemSlot hover = null;
        if (EventSystem.current != null)
        {
            _hits.Clear();
            var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            EventSystem.current.RaycastAll(data, _hits);

            for (int i = 0; i < _hits.Count; i++)
            {
                var s = _hits[i].gameObject.GetComponentInParent<ItemSlot>();
                if (s == null || s == cursorSlot) continue;
                hover = s;
                break;
            }
        }

        // ── 툴팁: 호버 슬롯의 아이템 "전부" 표시 ──
        if (hover != null && hover.Item != null && tooltipText != null)
        {
            var it = hover.Item;
            var sb = new StringBuilder(256);

            // 기본 메타
            sb.AppendLine(it.Name);
            sb.Append("ID: ").AppendLine(it.ItemId);
            sb.Append("Type: ").AppendLine(it.ItemType);
            sb.Append("Sprite: ").AppendLine(it.SpriteName);
            sb.Append("Count: ").Append(it.Count).Append(" / ").AppendLine(it.MaxStack.ToString());

            // 고유 속성
            sb.AppendLine("Props:");
            if (it.UniqueProps != null && it.UniqueProps.Count > 0)
            {
                foreach (var kv in it.UniqueProps)
                    sb.Append(" - ").Append(kv.Key).Append(": ").AppendLine(kv.Value == null ? "null" : kv.Value.ToString());
            }
            else
            {
                sb.AppendLine(" - (none)");
            }

            tooltipText.text = sb.ToString();
            if (tooltipObject != null && !tooltipObject.activeSelf) tooltipObject.SetActive(true);
        }
        else
        {
            if (tooltipObject != null && tooltipObject.activeSelf) tooltipObject.SetActive(false);
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
            if (s == null || s == cursorSlot) continue;
            slotView = s;
            break;
        }
        if (slotView == null) return;

        var cur = cursorSlot.Item;

        bool useLocal =
            slotView.useLocalStorage ||
            slotView.inventory == null ||
            slotView.index < 0 ||
            (slotView.inventory != null && slotView.index >= slotView.inventory.items.Count);

        if (useLocal)
        {
            // 출력 슬롯(투입 금지)은 클릭 시 제작 시도
            if (slotView.denyUserPut)
            {
                var hc = slotView.GetComponentInParent<HandCraft>();
                if (hc != null) hc.TryTakeOutput(cursorSlot);
                return;
            }

            var slot = slotView.Item;
            bool same = (cur != null && slot != null && cur.ItemId == slot.ItemId);
            int room  = slot != null ? (slot.MaxStack - slot.Count) : 0;

            if (btn == PointerEventData.InputButton.Left)
            {
                if (cur == null)
                {
                    if (slot == null) return;
                    cursorSlot.Set(slot);
                    slotView.Set(null);
                    return;
                }
                if (slot == null)
                {
                    slotView.Set(cur);
                    cursorSlot.Set(null);
                    return;
                }
                if (same && room > 0)
                {
                    int move = cur.Count < room ? cur.Count : room;
                    slot.Count += move;
                    cur.Count  -= move;
                    slotView.Refresh();
                    if (cur.Count <= 0) cursorSlot.Set(null);
                    else cursorSlot.Refresh();
                    return;
                }
                // swap
                slotView.Set(cur);
                cursorSlot.Set(slot);
                return;
            }

            if (btn == PointerEventData.InputButton.Right)
            {
                if (cur == null && slot != null)
                {
                    int take = (slot.Count + 1) / 2;
                    var copy = new ItemData(
                        slot.ItemId, slot.Name, slot.SpriteName, slot.ItemType,
                        slot.MaxStack, new System.Collections.Generic.Dictionary<string, object>(slot.UniqueProps),
                        slot.Icon, take);
                    cursorSlot.Set(copy);

                    slot.Count -= take;
                    if (slot.Count <= 0) slotView.Set(null);
                    else slotView.Refresh();
                    return;
                }

                if (cur != null && slot == null)
                {
                    slotView.Set(new ItemData(
                        cur.ItemId, cur.Name, cur.SpriteName, cur.ItemType,
                        cur.MaxStack, new System.Collections.Generic.Dictionary<string, object>(cur.UniqueProps),
                        cur.Icon, 1));
                    cur.Count -= 1;
                    if (cur.Count <= 0) cursorSlot.Set(null);
                    else cursorSlot.Refresh();
                    return;
                }

                if (cur != null && same && slot.Count < slot.MaxStack)
                {
                    slot.Count += 1;
                    cur.Count  -= 1;
                    slotView.Refresh();
                    if (cur.Count <= 0) cursorSlot.Set(null);
                    else cursorSlot.Refresh();
                    return;
                }
            }
            return;
        }

        // ===== 인벤토리 바운드 경로(기존 로직 유지) =====
        var inv   = slotView.inventory;
        var items = inv.items;
        int idx   = slotView.index;

        var slotInv  = items[idx];
        bool sameInv = (cur != null && slotInv != null && cur.ItemId == slotInv.ItemId);
        int roomInv  = slotInv != null ? (slotInv.MaxStack - slotInv.Count) : 0;

        if (btn == PointerEventData.InputButton.Left)
        {
            if (cur == null)
            {
                if (slotInv == null) return;
                cursorSlot.Set(slotInv);
                items[idx] = null;
            }
            else if (slotInv == null)
            {
                items[idx] = cur;
                cursorSlot.Set(null);
            }
            else if (sameInv && roomInv > 0)
            {
                int move = cur.Count < roomInv ? cur.Count : roomInv;
                slotInv.Count += move;
                cur.Count     -= move;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
            }
            else
            {
                items[idx] = cur;
                cursorSlot.Set(slotInv);
            }
            inv.NotifyChanged();
            return;
        }

        if (btn == PointerEventData.InputButton.Right)
        {
            if (cur == null && slotInv != null)
            {
                int take = (slotInv.Count + 1) / 2;
                var copy = new ItemData(
                    slotInv.ItemId, slotInv.Name, slotInv.SpriteName, slotInv.ItemType,
                    slotInv.MaxStack, new System.Collections.Generic.Dictionary<string, object>(slotInv.UniqueProps),
                    slotInv.Icon, take);
                cursorSlot.Set(copy);

                slotInv.Count -= take;
                if (slotInv.Count <= 0) items[idx] = null;
                inv.NotifyChanged();
                return;
            }

            if (cur != null && slotInv == null)
            {
                items[idx] = new ItemData(
                    cur.ItemId, cur.Name, cur.SpriteName, cur.ItemType,
                    cur.MaxStack, new System.Collections.Generic.Dictionary<string, object>(cur.UniqueProps),
                    cur.Icon, 1);
                cur.Count -= 1;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
                inv.NotifyChanged();
                return;
            }

            if (cur != null && sameInv && slotInv.Count < slotInv.MaxStack)
            {
                slotInv.Count += 1;
                cur.Count     -= 1;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
                inv.NotifyChanged();
                return;
            }
        }
    }
}
