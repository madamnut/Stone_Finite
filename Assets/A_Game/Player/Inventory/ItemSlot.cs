using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리/핫바 공통 슬롯  
/// ▸ 아이콘(Image)   ▸ 스택(TextMeshProUGUI)  
/// ▸ Scope(Image) (핫바 선택) ▸ Selected(Image) (마우스 오버)
/// </summary>
public class ItemSlot : MonoBehaviour,
                        IPointerEnterHandler,
                        IPointerExitHandler
{
    [Header("UI References")]
    public Image           iconImage;      // 아이콘
    public TextMeshProUGUI countText;      // 스택 수
    public Image           scopeImage;     // 핫바 선택 테두리
    public Image           selectedImage;  // 마우스 오버 하이라이트

    private ItemData _item;                // 현재 아이템(null → 빈 슬롯)

    /*────────── 초기화 ──────────*/
    void Awake()
    {
        scopeImage.enabled    = false;     // 시작 시 OFF
        selectedImage.enabled = false;
    }

    /*────────── 공개 API ──────────*/

    /// <summary>
    /// 새 아이템으로 설정 (null → 슬롯 비우기)
    /// </summary>
    public void Set(ItemData item)
    {
        _item = item;

        if (item == null)
        {
            iconImage.enabled  = false;
            countText.enabled  = false;
            return;
        }

        iconImage.enabled = true;
        iconImage.sprite  = item.Icon;
        Refresh();                         // 스택 숫자 표시
    }

    /// <summary>
    /// 아이템 Count가 변했을 때 UI 갱신
    /// </summary>
    public void Refresh()
    {
        if (_item == null) return;

        bool showStack = _item.Count > 1;
        countText.enabled = showStack;
        if (showStack) countText.text = _item.Count.ToString();
    }

    /// <summary>핫바 키 선택 시 테두리 토글</summary>
    public void SetScope(bool on)    => scopeImage.enabled    = on;

    /// <summary>외부에서 직접 선택 하이라이트 토글</summary>
    public void SetSelected(bool on) => selectedImage.enabled = on;

    /*────────── Mouse Hover ──────────*/
    public void OnPointerEnter(PointerEventData _) => selectedImage.enabled = true;
    public void OnPointerExit (PointerEventData _) => selectedImage.enabled = false;

    /*────────── 프로퍼티 ──────────*/
    public ItemData Item  => _item;
    public bool     IsEmpty => _item == null;
}
