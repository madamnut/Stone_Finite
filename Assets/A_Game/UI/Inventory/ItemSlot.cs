


using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Game.Core;

namespace Game.UI
{
    
    
    
    
    
    
    public class ItemSlot : MonoBehaviour,
                            IPointerEnterHandler,
                            IPointerExitHandler,
                            IPointerClickHandler
    {
        [Header("UI References")]

        public Image           iconImage;          
        public TextMeshProUGUI countText;          
        public Image           scopeImage;         
        public Image           selectedImage;      
    
        [Header("Durability UI")]
        public GameObject      durabilityRoot;     
        public Image           durabilityBar;      
    
        [Header("Progress UI")]
        public GameObject      progressRoot;       
        public Image           progressBar;        
    
        [Header("Binding (optional)")]
        public InventoryData inventory;            
        public int           index = -1;           
    
        [Header("Local Mode / Guard")]
        public bool useLocalStorage = false;       
        public bool denyUserPut     = false;       
        public bool denyUserInteraction = false;   
    
        
        
        
        public bool useAsButton = false;
        public event Action<ItemSlot> onClick;
    
        private ItemData _item;                    
    
        
        
        void Awake()
        {
            if (scopeImage)     scopeImage.enabled    = false; 
            if (selectedImage)  selectedImage.enabled = false;
            if (durabilityRoot) durabilityRoot.SetActive(false);
            if (progressRoot)   progressRoot.SetActive(false);
        }
    
        
    
        
        
        
        
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
    
            Refresh(); 
        }
    
        
        
        
        
        public void Refresh()
        {
            if (_item == null)
            {
                if (countText)      countText.enabled      = false;
                if (durabilityRoot) durabilityRoot.SetActive(false);
                if (progressRoot)   progressRoot.SetActive(false);
                return;
            }
    
            
            if (countText)
            {
                bool showStack = _item.Count > 1;
                countText.enabled = showStack;
                if (showStack) countText.text = _item.Count.ToString();
            }
    
            
            if (durabilityRoot && durabilityBar)
            {
                int maxDur = _item.MaxDurability;
                int curDur = _item.Durability;
    
                
                if (maxDur <= 0)
                {
                    durabilityRoot.SetActive(false);
                }
                else
                {
                    durabilityRoot.SetActive(true);
    
                    
                    
                    
                    
                    float fill = 0f;
                    if (maxDur > 1)
                    {
                        fill = (float)(curDur - 1) / (float)(maxDur - 1);
                        fill = Mathf.Clamp01(fill);
                    }
                    else
                    {
                        
                        fill = 0f;
                    }
    
                    durabilityBar.fillAmount = fill;
    
                    
                    Color red   = new Color(1f, 0f, 0f);
                    Color green = new Color(0f, 1f, 0f);
    
                    Color finalColor = Color.Lerp(red, green, fill);
                    durabilityBar.color = finalColor;
                }
            }
        }
    
        
        
        
        
        
        
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
    
        
        
        public void SetScope(bool on)
        {
            if (scopeImage) scopeImage.enabled = on;
        }
    
        
        
        
        
        
        public void SetSelected(bool on)
        {
            if (selectedImage) selectedImage.enabled = on;
        }
    
        
        
        public void OnPointerEnter(PointerEventData _)
        {
            
            if (useAsButton) return;
    
            if (selectedImage) selectedImage.enabled = true;
        }
    
        
        public void OnPointerExit(PointerEventData _)
        {
            if (useAsButton) return;
    
            if (selectedImage) selectedImage.enabled = false;
        }
    
        
        
        public void OnPointerClick(PointerEventData _)
        {
            if (!useAsButton) return;
            if (denyUserInteraction) return;
    
            onClick?.Invoke(this);
        }
    
        
        public ItemData Item    => _item;
        public bool     IsEmpty => _item == null;
    }
}
