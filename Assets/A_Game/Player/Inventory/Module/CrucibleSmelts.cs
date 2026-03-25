using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Player;

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
    
            // ?????????誘⑷강???띠럾?????됀?????逾? Graphic raycastTarget ?怨쀫닔鈺???깅쾳
            var g = GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
        }
    
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (_view == null) return;
    
            // ??븐뼚?붺뭐癒?턂?? ?????β돦裕??먯쾸? ??嶺뚣볦뿨??귥춺?"????????繹??????ζ뤆?쎛 ?????곗꽑???노츎" ??⑤객臾?
            // Debug.Log($"[CrucibleSmelts] click idx={_layerIndex} {_itemId}:{_amount}");
    
            _view.BringLayerToTop(_layerIndex);
        }
    
        // Cursor?띠럾? ???㏓럡????袁⑥깓??嶺뚮?理????뚮벣???寃밸듆, ??蹂μ쟽???깊뱱??void ??????
        public void TryBuildTooltip(StringBuilder sb)
        {
            if (sb == null) return;
            sb.AppendLine(_itemId);
            sb.Append("amount: ").Append(_amount);
        }
    }
}
