using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        // 클릭이 안 먹는 가장 흔한 원인: Graphic raycastTarget 꺼져있음
        var g = GetComponent<Graphic>();
        if (g != null) g.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (_view == null) return;

        // 디버깅용: 여기 로그가 안 찍히면 "클릭 이벤트 자체가 안 들어오는" 상태
        // Debug.Log($"[CrucibleSmelts] click idx={_layerIndex} {_itemId}:{_amount}");

        _view.BringLayerToTop(_layerIndex);
    }

    // Cursor가 이걸로 툴팁을 뽑는 구조라면, 시그니처는 void 여야 함
    public void TryBuildTooltip(StringBuilder sb)
    {
        if (sb == null) return;
        sb.AppendLine(_itemId);
        sb.Append("amount: ").Append(_amount);
    }
}
