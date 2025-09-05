using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    [Header("Rendering")] public SpriteRenderer spriteRenderer;
    public ItemData ItemData { get; private set; }

    public void Initialize(ItemData data)
    {
        ItemData = data;
        spriteRenderer.sprite = data.Icon;
    }
}