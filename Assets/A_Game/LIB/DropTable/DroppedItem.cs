using UnityEngine;

/// <summary>
/// 월드에 떨어진 아이템을 표현하는 컴포넌트입니다.
/// 외부에서 전달된 ItemData 인스턴스와 그 안의 Icon을 사용해 렌더링만 담당합니다.
/// </summary>
public class DroppedItem : MonoBehaviour
{
    [Header("Rendering")]
    public SpriteRenderer spriteRenderer;

    private ItemData _itemData;

    /// <summary>
    /// 외부에서 생성한 ItemData를 받아 보관하고,
    /// 그 안에 담긴 Icon 스프라이트로 렌더러를 세팅합니다.
    /// </summary>
    public void Initialize(ItemData itemData)
    {
        _itemData = itemData;

        if (spriteRenderer == null)
        {
            Debug.LogWarning("DroppedItem: SpriteRenderer가 할당되지 않았습니다.");
            return;
        }

        if (_itemData.Icon != null)
        {
            spriteRenderer.sprite = _itemData.Icon;
        }
        else
        {
            Debug.LogWarning($"DroppedItem: Icon이 null입니다. ItemId={_itemData.ItemId}");
        }
    }

    /// <summary>보관 중인 ItemData를 반환합니다.</summary>
    public ItemData GetItemData() => _itemData;
}
