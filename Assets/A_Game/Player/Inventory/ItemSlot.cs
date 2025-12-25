using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리/핫바/크래프팅 공통 슬롯
/// ▸ 아이콘(Image)   ▸ 스택(TextMeshProUGUI)
/// ▸ Scope(Image) (핫바 선택) ▸ Selected(Image) (마우스 오버)
/// </summary>
public class ItemSlot : MonoBehaviour,
                        IPointerEnterHandler,
                        IPointerExitHandler
{
    [Header("UI References")]
    public Image           iconImage;          // 아이콘
    public TextMeshProUGUI countText;          // 스택 수
    public Image           scopeImage;         // 핫바 선택 테두리
    public Image           selectedImage;      // 마우스 오버 하이라이트

    [Header("Durability UI")]
    public GameObject      durabilityRoot;     // 내구도 바 부모 오브젝트
    public Image           durabilityBar;      // 내구도 바(Filled Image)

    [Header("Binding (메타데이터)")]
    public InventoryData inventory;            // 이 슬롯이 속한 인벤토리
    public int           index = -1;           // 인벤토리 인덱스(0~49 등)

    [Header("Local Mode / Guard")]
    public bool useLocalStorage = false;       // 크래프팅 슬롯 등 인덱스 없이 운용
    public bool denyUserPut     = false;       // 유저 투입 금지(출력 슬롯용)
    public bool denyUserInteraction = false;   // 유저 상호작용 금지(읽기 전용)

    private ItemData _item;                    // 현재 아이템(null → 빈 슬롯)

    /*────────── 초기화 ──────────*/
    void Awake()
    {
        if (scopeImage)     scopeImage.enabled   = false; // 시작 시 OFF
        if (selectedImage)  selectedImage.enabled = false;
        if (durabilityRoot) durabilityRoot.SetActive(false);
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
            if (iconImage)      iconImage.enabled      = false;
            if (countText)      countText.enabled      = false;
            if (durabilityRoot) durabilityRoot.SetActive(false);
            return;
        }

        if (iconImage)
        {
            iconImage.enabled = true;
            iconImage.sprite  = item.Icon;
        }

        Refresh(); // 스택/내구도 UI 갱신
    }

    /// <summary>
    /// 아이템 Count나 내구도가 변했을 때 UI 갱신
    /// </summary>
    public void Refresh()
    {
        if (_item == null)
        {
            if (countText)      countText.enabled      = false;
            if (durabilityRoot) durabilityRoot.SetActive(false);
            return;
        }

        // 스택 표시
        if (countText)
        {
            bool showStack = _item.Count > 1;
            countText.enabled = showStack;
            if (showStack) countText.text = _item.Count.ToString();
        }

        // 내구도 바 표시 / 갱신
        if (durabilityRoot && durabilityBar)
        {
            int maxDur = _item.MaxDurability;
            int curDur = _item.Durability;

            // 내구도 정보가 없는 아이템 (또는 0/음수) → 바 숨김
            if (maxDur <= 0)
            {
                durabilityRoot.SetActive(false);
            }
            else
            {
                durabilityRoot.SetActive(true);

                // fill 계산
                // 요구사항:
                // - 내구도 1 남았을 때 바는 "완전히 빈 상태" (fillAmount == 0)
                // - 내구도 maxDur 일 때 바는 가득 찬 상태 (fillAmount == 1)
                float fill = 0f;
                if (maxDur > 1)
                {
                    fill = (float)(curDur - 1) / (float)(maxDur - 1);
                    fill = Mathf.Clamp01(fill);
                }
                else
                {
                    // maxDur == 1 → 1 남았을 때 곧바로 빈 상태로
                    fill = 0f;
                }

                durabilityBar.fillAmount = fill;

                // 색상: 내구도 높을수록 초록, 낮을수록 빨강으로 연속 변화
                // 0 → 빨강, 1 → 초록, 중간값은 자연스럽게 오렌지/노랑 계열로
                Color red   = new Color(1f, 0f, 0f);
                Color green = new Color(0f, 1f, 0f);

                Color finalColor = Color.Lerp(red, green, fill);
                durabilityBar.color = finalColor;
            }
        }
    }

    /// <summary>핫바 키 선택 시 테두리 토글</summary>
    public void SetScope(bool on)
    {
        if (scopeImage) scopeImage.enabled = on;
    }

    /// <summary>외부에서 직접 선택 하이라이트 토글</summary>
    public void SetSelected(bool on)
    {
        if (selectedImage) selectedImage.enabled = on;
    }

    /*────────── Mouse Hover ──────────*/
    public void OnPointerEnter(PointerEventData _)
    {
        if (selectedImage) selectedImage.enabled = true;
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (selectedImage) selectedImage.enabled = false;
    }

    /*────────── 프로퍼티 ──────────*/
    public ItemData Item    => _item;
    public bool     IsEmpty => _item == null;
}
