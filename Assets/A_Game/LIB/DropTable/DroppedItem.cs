using Newtonsoft.Json;
using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    [Header("Rendering")] public SpriteRenderer spriteRenderer;
    private ItemData _itemData;

    public void Initialize(ItemData data)
    {
        _itemData           = data;
        spriteRenderer.sprite = data.Icon;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Icon(순환참조 위험)만 제외하고 나머지 모든 정보 직렬화
        var logObj = new
        {
            _itemData.ItemId,
            _itemData.Name,
            _itemData.SpriteName,
            _itemData.ItemType,
            _itemData.MaxStack,
            UniqueProps = _itemData.UniqueProps    // Dictionary<string,object>
        };

        Debug.Log($"Picked up item → {JsonConvert.SerializeObject(logObj)}");

        Destroy(gameObject);
    }

    public ItemData GetItemData() => _itemData;
}
