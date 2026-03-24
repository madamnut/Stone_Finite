using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Player
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
    
            // ?대┃????癒밸뒗 媛???뷀븳 ?먯씤: Graphic raycastTarget 爰쇱졇?덉쓬
            var g = GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
        }
    
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_view == null) return;
    
            // ?붾쾭源낆슜: ?ш린 濡쒓렇媛 ??李랁엳硫?"?대┃ ?대깽???먯껜媛 ???ㅼ뼱?ㅻ뒗" ?곹깭
            // Debug.Log($"[CrucibleSmelts] click idx={_layerIndex} {_itemId}:{_amount}");
    
            _view.BringLayerToTop(_layerIndex);
        }
    
        // Cursor媛 ?닿구濡??댄똻??戮묐뒗 援ъ“?쇰㈃, ?쒓렇?덉쿂??void ?ъ빞 ??
        public void TryBuildTooltip(StringBuilder sb)
        {
            if (sb == null) return;
            sb.AppendLine(_itemId);
            sb.Append("amount: ").Append(_amount);
        }
    }
}
