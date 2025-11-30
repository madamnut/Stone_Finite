// Cursor.cs
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class Cursor : MonoBehaviour
{
    [Header("이 커서는 Canvas 자식(UI)입니다")]
    public Canvas canvas;
    public ItemSlot cursorSlot;

    [Header("Tooltip")]
    public RectTransform tooltipRoot;  // ← 툴팁 패널
    public TMP_Text tooltipText;       // 내용 텍스트
    public GameObject tooltipObject;   // 보이기/숨기기
    public Vector2 tooltipOffset = new(16, -16);
    public Vector2 tooltipPadding = new(8, 8);

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

        // ── 호버 슬롯 탐지 ──
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

        // ── 툴팁 갱신 ──
        if (hover != null && hover.Item != null && tooltipText != null)
        {
            var it = hover.Item;
            var sb = new StringBuilder(256);
            sb.AppendLine(it.Name);
            sb.Append("ID: ").AppendLine(it.ItemId);
            sb.Append("Type: ").AppendLine(it.ItemType);
            sb.Append("Sprite: ").AppendLine(it.SpriteName);
            sb.Append("Count: ").Append(it.Count).Append(" / ").AppendLine(it.MaxStack.ToString());

            // 내구도
            sb.Append("Durability: ")
              .Append(it.Durability)
              .Append(" / ")
              .AppendLine(it.MaxDurability.ToString());

            // 태그
            sb.AppendLine("Tags:");
            if (it.Tags != null && it.Tags.Count > 0)
            {
                for (int i = 0; i < it.Tags.Count; i++)
                {
                    sb.Append(" - ").AppendLine(it.Tags[i]);
                }
            }
            else
            {
                sb.AppendLine(" - (none)");
            }

            // 파라미터(최상위만 간단히 출력)
            sb.AppendLine("Params:");
            if (it.Parameters != null && it.Parameters.Count > 0)
            {
                foreach (var kv in it.Parameters)
                    sb.Append(" - ")
                      .Append(kv.Key)
                      .Append(": ")
                      .AppendLine(kv.Value == null ? "null" : kv.Value.ToString());
            }
            else
            {
                sb.AppendLine(" - (none)");
            }

            tooltipText.text = sb.ToString();
            if (tooltipObject != null && !tooltipObject.activeSelf) tooltipObject.SetActive(true);

            // ── 패널 자체를 화면 경계로 클램프 ──
            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                var canvasRT  = (RectTransform)canvas.transform;
                var tooltipRT = tooltipRoot != null ? tooltipRoot : tooltipText.rectTransform;

                LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRT);

                Vector2 ap = tooltipOffset;  // 커서 기준 오프셋
                var r  = tooltipRT.rect;
                var cr = canvasRT.rect;
                var pv = tooltipRT.pivot;

                float left   = rt.anchoredPosition.x + ap.x - r.width  * pv.x;
                float right  = rt.anchoredPosition.x + ap.x + r.width  * (1f - pv.x);
                float bottom = rt.anchoredPosition.y + ap.y - r.height * pv.y;
                float top    = rt.anchoredPosition.y + ap.y + r.height * (1f - pv.y);

                float minX = cr.xMin + tooltipPadding.x;
                float maxX = cr.xMax - tooltipPadding.x;
                float minY = cr.yMin + tooltipPadding.y;
                float maxY = cr.yMax - tooltipPadding.y;

                if (left   < minX) ap.x += (minX - left);
                if (right  > maxX) ap.x -= (right - maxX);
                if (top    > maxY) ap.y -= (top - maxY);
                if (bottom < minY) ap.y += (minY - bottom);

                tooltipRT.anchoredPosition = ap; // 커서(부모) 기준 배치
            }
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

        // ===== 결과 슬롯 픽업 우선 분기 =====
        if (slotView.denyUserPut)
        {
            var cm = slotView.GetComponentInParent<CraftModule>();
            if (cm != null) { cm.TryTakeOutput(cursorSlot); return; }
            return;
        }

        var cur = cursorSlot.Item;

        bool useLocal =
            slotView.useLocalStorage ||
            slotView.inventory == null ||
            slotView.index < 0 ||
            (slotView.inventory != null && slotView.index >= slotView.inventory.items.Count);

        if (useLocal)
        {
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
                slotView.Set(cur);
                cursorSlot.Set(slot);
                return;
            }

            if (btn == PointerEventData.InputButton.Right)
            {
                // 슬롯 → 커서 (반 갈라서)
                if (cur == null && slot != null)
                {
                    int take = (slot.Count + 1) / 2;
                    var copy = new ItemData(
                        itemId:          slot.ItemId,
                        name:            slot.Name,
                        spriteName:      slot.SpriteName,
                        itemType:        slot.ItemType,
                        maxStack:        slot.MaxStack,
                        maxDurability:   slot.MaxDurability,
                        durability:      slot.Durability,
                        craftingActions: slot.CraftingActions,
                        interActions:    slot.InterActions,
                        toolActions:     slot.ToolActions,
                        weaponActions:   slot.WeaponActions,
                        tags:            slot.Tags,
                        parameters:      slot.Parameters,
                        icon:            slot.Icon,
                        count:           take
                    );
                    cursorSlot.Set(copy);

                    slot.Count -= take;
                    if (slot.Count <= 0) slotView.Set(null);
                    else slotView.Refresh();
                    return;
                }

                // 커서 → 빈 슬롯 (1개 내려놓기)
                if (cur != null && slot == null)
                {
                    var newSlot = new ItemData(
                        itemId:          cur.ItemId,
                        name:            cur.Name,
                        spriteName:      cur.SpriteName,
                        itemType:        cur.ItemType,
                        maxStack:        cur.MaxStack,
                        maxDurability:   cur.MaxDurability,
                        durability:      cur.Durability,
                        craftingActions: cur.CraftingActions,
                        interActions:    cur.InterActions,
                        toolActions:     cur.ToolActions,
                        weaponActions:   cur.WeaponActions,
                        tags:            cur.Tags,
                        parameters:      cur.Parameters,
                        icon:            cur.Icon,
                        count:           1
                    );
                    slotView.Set(newSlot);

                    cur.Count -= 1;
                    if (cur.Count <= 0) cursorSlot.Set(null);
                    else cursorSlot.Refresh();
                    return;
                }

                // 커서 → 같은 아이디 슬롯 (1개 합치기)
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

        // ===== 인벤토리 바운드 경로 =====
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
            // 인벤토리 슬롯 → 커서 (반 갈라서)
            if (cur == null && slotInv != null)
            {
                int take = (slotInv.Count + 1) / 2;
                var copy = new ItemData(
                    itemId:          slotInv.ItemId,
                    name:            slotInv.Name,
                    spriteName:      slotInv.SpriteName,
                    itemType:        slotInv.ItemType,
                    maxStack:        slotInv.MaxStack,
                    maxDurability:   slotInv.MaxDurability,
                    durability:      slotInv.Durability,
                    craftingActions: slotInv.CraftingActions,
                    interActions:    slotInv.InterActions,
                    toolActions:     slotInv.ToolActions,
                    weaponActions:   slotInv.WeaponActions,
                    tags:            slotInv.Tags,
                    parameters:      slotInv.Parameters,
                    icon:            slotInv.Icon,
                    count:           take
                );
                cursorSlot.Set(copy);

                slotInv.Count -= take;
                if (slotInv.Count <= 0) items[idx] = null;
                inv.NotifyChanged();
                return;
            }

            // 커서 → 인벤토리 빈 슬롯 (1개 내려놓기)
            if (cur != null && slotInv == null)
            {
                items[idx] = new ItemData(
                    itemId:          cur.ItemId,
                    name:            cur.Name,
                    spriteName:      cur.SpriteName,
                    itemType:        cur.ItemType,
                    maxStack:        cur.MaxStack,
                    maxDurability:   cur.MaxDurability,
                    durability:      cur.Durability,
                    craftingActions: cur.CraftingActions,
                    interActions:    cur.InterActions,
                    toolActions:     cur.ToolActions,
                    weaponActions:   cur.WeaponActions,
                    tags:            cur.Tags,
                    parameters:      cur.Parameters,
                    icon:            cur.Icon,
                    count:           1
                );
                cur.Count -= 1;
                if (cur.Count <= 0) cursorSlot.Set(null);
                else cursorSlot.Refresh();
                inv.NotifyChanged();
                return;
            }

            // 커서 → 같은 아이디 인벤토리 슬롯 (1개 합치기)
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
