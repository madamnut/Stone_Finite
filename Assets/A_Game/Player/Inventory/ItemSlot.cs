using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace Game.Player
{
    
    /// <summary>
    /// ?몃깽?좊━/?ル컮/?щ옒?꾪똿 怨듯넻 ?щ’
    /// ???꾩씠肄?Image)        ???ㅽ깮(TextMeshProUGUI)
    /// ??Scope(Image) (?ル컮 ?좏깮) ??Selected(Image) (留덉슦???ㅻ쾭 / (useAsButton ?? ?좏깮 ?쒖떆)
    /// </summary>
    public class ItemSlot : MonoBehaviour,
                            IPointerEnterHandler,
                            IPointerExitHandler,
                            IPointerClickHandler
    {
        [Header("UI References")]
        public Image           iconImage;          // ?꾩씠肄?
        public TextMeshProUGUI countText;          // ?ㅽ깮 ??
        public Image           scopeImage;         // ?ル컮 ?좏깮 ?뚮몢由?
        public Image           selectedImage;      // 留덉슦???ㅻ쾭 ?섏씠?쇱씠??/ (useAsButton ?? ?좏깮 ?쒖떆
    
        [Header("Durability UI")]
        public GameObject      durabilityRoot;     // ?닿뎄??諛?遺紐??ㅻ툕?앺듃
        public Image           durabilityBar;      // ?닿뎄??諛?Filled Image)
    
        [Header("Progress UI")]
        public GameObject      progressRoot;       // 吏꾪뻾 諛?遺紐??ㅻ툕?앺듃 (?쒖옉 ??OFF)
        public Image           progressBar;        // 吏꾪뻾 諛?Filled Image)
    
        [Header("Binding (硫뷀??곗씠??")]
        public InventoryData inventory;            // ???щ’???랁븳 ?몃깽?좊━
        public int           index = -1;           // ?몃깽?좊━ ?몃뜳??0~49 ??
    
        [Header("Local Mode / Guard")]
        public bool useLocalStorage = false;       // ?щ옒?꾪똿 ?щ’ ???몃뜳???놁씠 ?댁슜
        public bool denyUserPut     = false;       // ?좎? ?ъ엯 湲덉?(異쒕젰 ?щ’??
        public bool denyUserInteraction = false;   // ?좎? ?곹샇?묒슜 湲덉?(?쎄린 ?꾩슜)
    
        // ??肄붾뱶 湲곕컲 踰꾪듉 紐⑤뱶(?몄뒪?숉꽣??Button 而댄룷?뚰듃 異붽? ????
        // - true硫?selectedImage??"?좏깮 ?쒖떆"濡쒕쭔 ?ъ슜(hover ?좉? 湲덉?)
        // - ?대┃ ??onClick ?대깽??諛쒗뻾
        public bool useAsButton = false;
        public event Action<ItemSlot> onClick;
    
        private ItemData _item;                    // ?꾩옱 ?꾩씠??null ??鍮??щ’)
    
        /*?????????? 珥덇린????????????*/
        void Awake()
        {
            if (scopeImage)     scopeImage.enabled    = false; // ?쒖옉 ??OFF
            if (selectedImage)  selectedImage.enabled = false;
            if (durabilityRoot) durabilityRoot.SetActive(false);
            if (progressRoot)   progressRoot.SetActive(false);
        }
    
        /*?????????? 怨듦컻 API ??????????*/
    
        /// <summary>
        /// ???꾩씠?쒖쑝濡??ㅼ젙 (null ???щ’ 鍮꾩슦湲?
        /// </summary>
        public void Set(ItemData item)
        {
            _item = item;
    
            if (item == null)
            {
                if (iconImage)      iconImage.enabled      = false;
                if (countText)      countText.enabled      = false;
                if (durabilityRoot) durabilityRoot.SetActive(false);
                if (progressRoot)   progressRoot.SetActive(false);
                return;
            }
    
            if (iconImage)
            {
                iconImage.enabled = true;
                iconImage.sprite  = item.Icon;
            }
    
            Refresh(); // ?ㅽ깮/?닿뎄??UI 媛깆떊
        }
    
        /// <summary>
        /// ?꾩씠??Count???닿뎄?꾧? 蹂?덉쓣 ??UI 媛깆떊
        /// </summary>
        public void Refresh()
        {
            if (_item == null)
            {
                if (countText)      countText.enabled      = false;
                if (durabilityRoot) durabilityRoot.SetActive(false);
                if (progressRoot)   progressRoot.SetActive(false);
                return;
            }
    
            // ?ㅽ깮 ?쒖떆
            if (countText)
            {
                bool showStack = _item.Count > 1;
                countText.enabled = showStack;
                if (showStack) countText.text = _item.Count.ToString();
            }
    
            // ?닿뎄??諛??쒖떆 / 媛깆떊
            if (durabilityRoot && durabilityBar)
            {
                int maxDur = _item.MaxDurability;
                int curDur = _item.Durability;
    
                // ?닿뎄???뺣낫媛 ?녿뒗 ?꾩씠??(?먮뒗 0/?뚯닔) ??諛??④?
                if (maxDur <= 0)
                {
                    durabilityRoot.SetActive(false);
                }
                else
                {
                    durabilityRoot.SetActive(true);
    
                    // fill 怨꾩궛
                    // ?붽뎄?ы빆:
                    // - ?닿뎄??1 ?⑥븯????諛붾뒗 "?꾩쟾??鍮??곹깭" (fillAmount == 0)
                    // - ?닿뎄??maxDur ????諛붾뒗 媛??李??곹깭 (fillAmount == 1)
                    float fill = 0f;
                    if (maxDur > 1)
                    {
                        fill = (float)(curDur - 1) / (float)(maxDur - 1);
                        fill = Mathf.Clamp01(fill);
                    }
                    else
                    {
                        // maxDur == 1 ??1 ?⑥븯????怨㏓컮濡?鍮??곹깭濡?
                        fill = 0f;
                    }
    
                    durabilityBar.fillAmount = fill;
    
                    // ?됱긽: ?닿뎄???믪쓣?섎줉 珥덈줉, ??쓣?섎줉 鍮④컯?쇰줈 ?곗냽 蹂??
                    Color red   = new Color(1f, 0f, 0f);
                    Color green = new Color(0f, 1f, 0f);
    
                    Color finalColor = Color.Lerp(red, green, fill);
                    durabilityBar.color = finalColor;
                }
            }
        }
    
        /// <summary>
        /// ?뱀닔 耳?댁뒪?먯꽌留?吏꾪뻾 諛??쒖떆/媛깆떊.
        /// fill01: 0~1
        /// show: true硫??쒖떆, false硫??④?
        /// </summary>
        public void SetProgress(float fill01, bool show)
        {
            if (!progressRoot || !progressBar) return;
    
            if (!show)
            {
                progressRoot.SetActive(false);
                return;
            }
    
            progressRoot.SetActive(true);
            progressBar.fillAmount = Mathf.Clamp01(fill01);
        }
    
        /// <summary>?ル컮 ???좏깮 ???뚮몢由??좉?</summary>
        public void SetScope(bool on)
        {
            if (scopeImage) scopeImage.enabled = on;
        }
    
        /// <summary>
        /// ?몃??먯꽌 吏곸젒 ?좏깮 ?섏씠?쇱씠???좉?
        /// - useAsButton=true???꾨낫 ?щ’? ?닿구濡쒕쭔 selectedImage瑜??쒖뼱(hover ?좉? 湲덉?)
        /// </summary>
        public void SetSelected(bool on)
        {
            if (selectedImage) selectedImage.enabled = on;
        }
    
        /*?????????? Mouse Hover ??????????*/
        public void OnPointerEnter(PointerEventData _)
        {
            // 踰꾪듉 紐⑤뱶?먯꽌??selectedImage瑜?"?좏깮 ?쒖떆"濡??곕?濡?hover濡?嫄대뱶由ъ? ?딆쓬
            if (useAsButton) return;
    
            if (selectedImage) selectedImage.enabled = true;
        }
    
        public void OnPointerExit(PointerEventData _)
        {
            if (useAsButton) return;
    
            if (selectedImage) selectedImage.enabled = false;
        }
    
        /*?????????? Click ??????????*/
        public void OnPointerClick(PointerEventData _)
        {
            if (!useAsButton) return;
            if (denyUserInteraction) return;
    
            onClick?.Invoke(this);
        }
    
        /*?????????? ?꾨줈?쇳떚 ??????????*/
        public ItemData Item    => _item;
        public bool     IsEmpty => _item == null;
    }
}
