using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class DroppedItem : Entity
{
    [Header("Rendering")]
    public SpriteRenderer spriteRenderer;

    [Header("References")]
    [SerializeField] private ItemLibrary itemLibrary; // spriteName -> Sprite 복원

    public ItemData ItemData { get; private set; }

    public override EntityKind Kind => EntityKind.DroppedItem;

    public override void SetSimActive(bool active)
    {
        base.SetSimActive(active);
    }

    // ItemData "전체" 스냅샷(Icon은 제외: spriteName으로 복원)
    [Serializable]
    private class DroppedItemPayload
    {
        public string itemId;
        public string name;
        public string spriteName;
        public string itemType;
        public int maxStack;

        public int maxDurability;
        public int durability;
        public int count;

        public List<string> tags;
        public Dictionary<string, object> details;

        public Dictionary<string, Dictionary<string, object>> breakActions;
        public Dictionary<string, Dictionary<string, object>> toolActions;
        public Dictionary<string, Dictionary<string, object>> weaponActions;
    }

    public override EntitySaveData ToSaveData()
    {
        DroppedItemPayload payload = null;

        if (ItemData != null)
        {
            payload = new DroppedItemPayload
            {
                itemId        = ItemData.ItemId,
                name          = ItemData.Name,
                spriteName    = ItemData.SpriteName,
                itemType      = ItemData.ItemType,
                maxStack      = ItemData.MaxStack,

                maxDurability = ItemData.MaxDurability,
                durability    = ItemData.Durability,
                count         = ItemData.Count,

                tags          = ItemData.Tags,
                details       = ItemData.Details,

                breakActions  = ItemData.BreakActions,
                toolActions   = ItemData.ToolActions,
                weaponActions = ItemData.WeaponActions
            };
        }

        return new EntitySaveData
        {
            Kind        = EntityKind.DroppedItem,
            Position    = transform.position,
            PayloadJson = (payload != null) ? JsonConvert.SerializeObject(payload) : string.Empty
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        transform.position = data.Position;

        if (string.IsNullOrEmpty(data.PayloadJson))
        {
            Initialize(null);
            return;
        }

        DroppedItemPayload payload;
        try
        {
            payload = JsonConvert.DeserializeObject<DroppedItemPayload>(data.PayloadJson);
        }
        catch
        {
            Initialize(null);
            return;
        }

        Sprite icon = null;
        if (itemLibrary != null && !string.IsNullOrEmpty(payload.spriteName))
            icon = itemLibrary.GetSprite(payload.spriteName);

        var rebuilt = new ItemData(
            itemId:        payload.itemId,
            name:          payload.name,
            spriteName:    payload.spriteName,
            itemType:      payload.itemType,
            maxStack:      payload.maxStack,
            maxDurability: payload.maxDurability,
            durability:    payload.durability,
            toolActions:   payload.toolActions,
            weaponActions: payload.weaponActions,
            breakActions:  payload.breakActions,
            tags:          payload.tags,
            details:       payload.details,
            icon:          icon,
            count:         payload.count
        );

        Initialize(rebuilt);
    }

    // 드랍 아이템 스폰/로드 시 실제 ItemData 주입
    public void Initialize(ItemData data)
    {
        ItemData = data;

        if (spriteRenderer == null)
            return;

        if (data == null)
        {
            spriteRenderer.sprite = null;
            return;
        }

        if (data.Icon != null)
        {
            spriteRenderer.sprite = data.Icon;
            return;
        }

        if (itemLibrary != null && !string.IsNullOrEmpty(data.SpriteName))
            spriteRenderer.sprite = itemLibrary.GetSprite(data.SpriteName);
        else
            spriteRenderer.sprite = null;
    }
}
