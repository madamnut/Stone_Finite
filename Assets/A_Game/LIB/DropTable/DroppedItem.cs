using UnityEngine;
using Newtonsoft.Json;

public class DroppedItem : Entity
{
    [Header("Rendering")]
    public SpriteRenderer spriteRenderer;

    public ItemData ItemData { get; private set; }

    //────────────────────────────────────────────
    // Entity 요구 구현
    //────────────────────────────────────────────

    public override EntityKind Kind => EntityKind.DroppedItem;

    public override void SetSimActive(bool active)
    {
        // 기본 구현: GameObject 활성/비활성 + 플래그 갱신
        base.SetSimActive(active);
    }

    // DroppedItem 전용 페이로드
    [System.Serializable]
    private class DroppedItemPayload
    {
        public string itemId;
        public int    count;
        public int    durability;
    }

    /// <summary>
    /// 엔티티 저장 형식: Kind + Position + DroppedItemPayload JSON
    /// (itemId / count / durability 만 저장)
    /// </summary>
    public override EntitySaveData ToSaveData()
    {
        DroppedItemPayload payload = null;

        if (ItemData != null)
        {
            payload = new DroppedItemPayload
            {
                itemId     = ItemData.ItemId,
                count      = ItemData.Count,
                durability = ItemData.Durability
            };
        }

        return new EntitySaveData
        {
            Kind        = EntityKind.DroppedItem,
            Position    = transform.position,
            PayloadJson = (payload != null)
                ? JsonConvert.SerializeObject(payload)
                : string.Empty
        };
    }

    /// <summary>
    /// 기본 구현에서는 위치만 복원.
    /// 실제 ItemData는 WorldSaveSystem.LoadEntities 쪽에서
    /// ItemLibrary.Create(...) + Initialize(...)로 다시 채워 넣음.
    /// </summary>
    public override void FromSaveData(EntitySaveData data)
    {
        transform.position = data.Position;
        // ItemData는 WorldSaveSystem.LoadEntities에서 처리하므로 여기서는 건드리지 않음.
    }

    //────────────────────────────────────────────
    // Initialize (드랍 시 스폰 또는 로드 시 호출)
    //────────────────────────────────────────────

    /// <summary>
    /// 드랍 아이템 스폰/로드 시 실제 ItemData를 주입하는 초기화 함수
    /// </summary>
    public void Initialize(ItemData data)
    {
        ItemData = data;

        if (spriteRenderer != null)
            spriteRenderer.sprite = data.Icon;
    }
}
