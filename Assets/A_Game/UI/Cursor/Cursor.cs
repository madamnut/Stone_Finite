// Cursor.cs (?????밸븶?????????⑤벡???
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Game.Core;

namespace Game.UI
{
    
    public class Cursor : MonoBehaviour
    {
        [Header("Cursor Canvas")]
        public Canvas canvas;
        public ItemSlot cursorSlot;
    
        [Header("Tooltip")]
        public RectTransform tooltipRoot;  // ???????썹땟怨ロ떐??????걘??
        public TMP_Text tooltipText;       // ?????쇨덧??????筌뤾쑬已??
        public GameObject tooltipObject;   // ???ㅼ뒧????釉뚰??먮궪???鶯ㅺ동??????
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
    
            // ???? ??꿔꺂???????? ????
            ItemSlot hoverSlot = null;
            ICursorTooltipSource hoverTip = null;
    
            if (EventSystem.current != null)
            {
                _hits.Clear();
                var data = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
                EventSystem.current.RaycastAll(data, _hits);
    
                for (int i = 0; i < _hits.Count; i++)
                {
                    var go = _hits[i].gameObject;
    
                    // 1) ItemSlot ?????????
                    var s = go.GetComponentInParent<ItemSlot>();
                    if (s != null && s != cursorSlot)
                    {
                        hoverSlot = s;
                        break;
                    }
    
                    // 2) ?????쇨덧?筌먦렜逾?ICursorTooltipSource ?????밸븶??????嶺????癲ル슢??㎖猷⑷덩??????????
                    if (hoverTip == null)
                    {
                        var t = go.GetComponentInParent<ICursorTooltipSource>();
                        if (t != null) hoverTip = t;
                    }
                }
            }
    
            // ???? ?????썹땟怨ロ떐????ル봿????????
            bool showTooltip = false;
    
            if (tooltipText != null)
            {
                // A) ItemSlot ?????썹땟怨ロ떐??????????黎??筌??믨퀡??
                if (hoverSlot != null && hoverSlot.Item != null)
                {
                    var it = hoverSlot.Item;
                    var sb = new StringBuilder(512);
    
                    sb.AppendLine(it.Name);
                    sb.Append("ID: ").AppendLine(it.ItemId);
                    sb.Append("Type: ").AppendLine(it.ItemType);
                    sb.Append("Sprite: ").AppendLine(it.SpriteName);
                    sb.Append("Count: ").Append(it.Count).Append(" / ").AppendLine(it.MaxStack.ToString());
                    sb.Append("Durability: ")
                      .Append(it.Durability)
                      .Append(" / ")
                      .AppendLine(it.MaxDurability.ToString());
    
                    sb.AppendLine();
                    sb.AppendLine("Tags:");
                    if (it.Tags != null && it.Tags.Count > 0)
                    {
                        for (int i = 0; i < it.Tags.Count; i++)
                            sb.Append(" - ").AppendLine(it.Tags[i]);
                    }
                    else
                    {
                        sb.AppendLine(" - (none)");
                    }
    
                    sb.AppendLine();
                    sb.AppendLine("ToolActions:");
                    AppendActionKeysInline(sb, it.ToolActions);
    
                    sb.AppendLine("WeaponActions:");
                    AppendActionKeysInline(sb, it.WeaponActions);
    
                    sb.AppendLine("BreakActions:");
                    AppendActionKeysInline(sb, it.BreakActions);
    
                    sb.AppendLine();
                    sb.AppendLine("Details:");
                    if (it.Details != null && it.Details.Count > 0)
                    {
                        foreach (var kv in it.Details)
                            AppendDetailRecursive(sb, " - ", kv.Key, kv.Value);
                    }
                    else
                    {
                        sb.AppendLine(" - (none)");
                    }
    
                    tooltipText.text = sb.ToString();
                    showTooltip = true;
                }
                // B) ICursorTooltipSource ?????썹땟怨ロ떐?????????濚밸Ŧ援잏몭????
                else if (hoverTip != null)
                {
                    var sb = new StringBuilder(128);
                    hoverTip.TryBuildTooltip(sb);
                    if (sb.Length > 0)
                    {
                        tooltipText.text = sb.ToString();
                        showTooltip = true;
                    }
                }
            }
    
            if (tooltipObject != null)
            {
                if (showTooltip)
                {
                    if (!tooltipObject.activeSelf) tooltipObject.SetActive(true);
    
                    // ???? ?????걘????????????거?쭛????β뼯援?????????????????
                    if (canvas.renderMode != RenderMode.WorldSpace)
                    {
                        var canvasRT = (RectTransform)canvas.transform;
                        var tooltipRT = tooltipRoot != null ? tooltipRoot : tooltipText.rectTransform;
    
                        LayoutRebuilder.ForceRebuildLayoutImmediate(tooltipRT);
    
                        Vector2 ap = tooltipOffset;
                        var r = tooltipRT.rect;
                        var cr = canvasRT.rect;
                        var pv = tooltipRT.pivot;
    
                        float left = rt.anchoredPosition.x + ap.x - r.width * pv.x;
                        float right = rt.anchoredPosition.x + ap.x + r.width * (1f - pv.x);
                        float bottom = rt.anchoredPosition.y + ap.y - r.height * pv.y;
                        float top = rt.anchoredPosition.y + ap.y + r.height * (1f - pv.y);
    
                        float minX = cr.xMin + tooltipPadding.x;
                        float maxX = cr.xMax - tooltipPadding.x;
                        float minY = cr.yMin + tooltipPadding.y;
                        float maxY = cr.yMax - tooltipPadding.y;
    
                        if (left < minX) ap.x += (minX - left);
                        if (right > maxX) ap.x -= (right - maxX);
                        if (top > maxY) ap.y -= (top - maxY);
                        if (bottom < minY) ap.y += (minY - bottom);
    
                        tooltipRT.anchoredPosition = ap;
                    }
                }
                else
                {
                    if (tooltipObject.activeSelf) tooltipObject.SetActive(false);
                }
            }
    
            if (Input.GetMouseButtonDown(0)) HandleClick(PointerEventData.InputButton.Left);
            if (Input.GetMouseButtonDown(1)) HandleClick(PointerEventData.InputButton.Right);
        }
    
        static void AppendActionKeysInline(
            StringBuilder sb,
            IDictionary<string, Dictionary<string, object>> actions)
        {
            if (actions == null || actions.Count == 0)
            {
                sb.AppendLine(" - (none)");
                return;
            }
    
            foreach (var kv in actions)
            {
                string actionName = kv.Key;
                var paramDict = kv.Value;
    
                sb.Append(" - ").Append(actionName).AppendLine(":");
    
                if (paramDict != null && paramDict.Count > 0)
                {
                    foreach (var p in paramDict)
                        AppendDetailRecursive(sb, "    ", p.Key, p.Value);
                }
                else
                {
                    sb.AppendLine("    (no params)");
                }
            }
        }
    
        static void AppendDetailRecursive(StringBuilder sb, string indent, string key, object value)
        {
            if (value is Dictionary<string, object> dict)
            {
                sb.Append(indent).Append(key).AppendLine(":");
                foreach (var kv in dict)
                    AppendDetailRecursive(sb, indent + "  ", kv.Key, kv.Value);
            }
            else if (value is System.Collections.IList list && value is not string)
            {
                sb.Append(indent).Append(key).AppendLine(": [");
                int idx = 0;
                foreach (var v in list)
                {
                    string k = $"[{idx}]";
                    AppendDetailRecursive(sb, indent + "  ", k, v);
                    idx++;
                }
                sb.Append(indent).AppendLine("]");
            }
            else
            {
                sb.Append(indent)
                  .Append(key)
                  .Append(": ")
                  .AppendLine(value == null ? "null" : value.ToString());
            }
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
    
            // ???嶺??????轅붽틓??熬곥끇釉???????? ??壤굿??뚯돩???쑩??⑹땡? "?????밸븶??뫢??????????轅붽틓??影?뽧걤???? ?????ㅿ폎??
            // (?????밸븶?????????? ItemSlot.onClick???????????轅붽틓??影?뽧걤??
            if (slotView.useAsButton)
                return;
    
            if (slotView.denyUserInteraction)
                return;
    
            var cur = cursorSlot.Item;
    
            bool useLocal =
                slotView.useLocalStorage ||
                slotView.inventory == null ||
                slotView.index < 0 ||
                (slotView.inventory != null && slotView.index >= slotView.inventory.items.Count);
    
            if (useLocal)
            {
                var slot = slotView.Item;
    
                if (slotView.denyUserPut && cur != null)
                    return;
    
                bool same = (cur != null && slot != null && cur.ItemId == slot.ItemId);
                int room = slot != null ? (slot.MaxStack - slot.Count) : 0;
    
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
                        cur.Count -= move;
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
                    if (cur == null && slot != null)
                    {
                        int take = (slot.Count + 1) / 2;
                        var copy = new ItemData(
                            itemId: slot.ItemId,
                            name: slot.Name,
                            spriteName: slot.SpriteName,
                            itemType: slot.ItemType,
                            maxStack: slot.MaxStack,
                            maxDurability: slot.MaxDurability,
                            durability: slot.Durability,
                            toolActions: slot.ToolActions,
                            weaponActions: slot.WeaponActions,
                            breakActions: slot.BreakActions,
                            tags: slot.Tags,
                            details: slot.Details,
                            icon: slot.Icon,
                            count: take
                        );
                        cursorSlot.Set(copy);
    
                        slot.Count -= take;
                        if (slot.Count <= 0) slotView.Set(null);
                        else slotView.Refresh();
                        return;
                    }
    
                    if (cur != null && slot == null)
                    {
                        var newSlot = new ItemData(
                            itemId: cur.ItemId,
                            name: cur.Name,
                            spriteName: cur.SpriteName,
                            itemType: cur.ItemType,
                            maxStack: cur.MaxStack,
                            maxDurability: cur.MaxDurability,
                            durability: cur.Durability,
                            toolActions: cur.ToolActions,
                            weaponActions: cur.WeaponActions,
                            breakActions: cur.BreakActions,
                            tags: cur.Tags,
                            details: cur.Details,
                            icon: cur.Icon,
                            count: 1
                        );
                        slotView.Set(newSlot);
    
                        cur.Count -= 1;
                        if (cur.Count <= 0) cursorSlot.Set(null);
                        else cursorSlot.Refresh();
                        return;
                    }
    
                    if (cur != null && same && slot.Count < slot.MaxStack)
                    {
                        slot.Count += 1;
                        cur.Count -= 1;
                        slotView.Refresh();
                        if (cur.Count <= 0) cursorSlot.Set(null);
                        else cursorSlot.Refresh();
                        return;
                    }
                }
                return;
            }
    
            // ===== ??꿔꺂???沃???????る?????ш끽維뽳쭩??????β뼯援????る쑏?=====
            var inv = slotView.inventory;
            var items = inv.items;
            int idx = slotView.index;
    
            var slotInv = items[idx];
    
            if (slotView.denyUserPut && cur != null)
                return;
    
            bool sameInv = (cur != null && slotInv != null && cur.ItemId == slotInv.ItemId);
            int roomInv = slotInv != null ? (slotInv.MaxStack - slotInv.Count) : 0;
    
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
                    cur.Count -= move;
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
                        itemId: slotInv.ItemId,
                        name: slotInv.Name,
                        spriteName: slotInv.SpriteName,
                        itemType: slotInv.ItemType,
                        maxStack: slotInv.MaxStack,
                        maxDurability: slotInv.MaxDurability,
                        durability: slotInv.Durability,
                        toolActions: slotInv.ToolActions,
                        weaponActions: slotInv.WeaponActions,
                        breakActions: slotInv.BreakActions,
                        tags: slotInv.Tags,
                        details: slotInv.Details,
                        icon: slotInv.Icon,
                        count: take
                    );
                    cursorSlot.Set(copy);
    
                    slotInv.Count -= take;
                    if (slotInv.Count <= 0) items[idx] = null;
                    inv.NotifyChanged();
                    return;
                }
    
                if (cur != null && slotInv == null)
                {
                    items[idx] = new ItemData(
                        itemId: cur.ItemId,
                        name: cur.Name,
                        spriteName: cur.SpriteName,
                        itemType: cur.ItemType,
                        maxStack: cur.MaxStack,
                        maxDurability: cur.MaxDurability,
                        durability: cur.Durability,
                        toolActions: cur.ToolActions,
                        weaponActions: cur.WeaponActions,
                        breakActions: cur.BreakActions,
                        tags: cur.Tags,
                        details: cur.Details,
                        icon: cur.Icon,
                        count: 1
                    );
                    cur.Count -= 1;
                    if (cur.Count <= 0) cursorSlot.Set(null);
                    else cursorSlot.Refresh();
                    inv.NotifyChanged();
                    return;
                }
    
                if (cur != null && sameInv && slotInv.Count < slotInv.MaxStack)
                {
                    slotInv.Count += 1;
                    cur.Count -= 1;
                    if (cur.Count <= 0) cursorSlot.Set(null);
                    else cursorSlot.Refresh();
                    inv.NotifyChanged();
                    return;
                }
            }
        }
    }
}
