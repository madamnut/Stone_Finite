


using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    
    public class CrucibleSmelts : MonoBehaviour, IPointerClickHandler, ICursorTooltipSource
    {

        CrucibleView _view;
        int _layerIndex;
        string _itemId;
        int _amount;
    
        
        public void Init(CrucibleView view, int layerIndexInCrucible, string itemId, int amount)
        {
            _view = view;
            _layerIndex = layerIndexInCrucible;
            _itemId = itemId;
            _amount = amount;
    
            
            var g = GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
        }
    
        
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_view == null) return;
    
            
            
    
            _view.BringLayerToTop(_layerIndex);
        }
    
        
        
        public void TryBuildTooltip(StringBuilder sb)
        {
            if (sb == null) return;
            sb.AppendLine(_itemId);
            sb.Append("amount: ").Append(_amount);
        }
    }
}
