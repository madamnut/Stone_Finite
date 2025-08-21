using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 런타임 인게임 아이템 데이터를 담는 단순 컨테이너 클래스입니다.
/// 외부에서 생성 시 전달된 값을 보관하며 자체 파싱이나 변경 로직은 포함하지 않습니다.
/// </summary>
public class ItemData
{
    // 기본 속성
    public string ItemId { get; private set; }
    public string Name { get; private set; }
    public string SpriteName { get; private set; }
    public string ItemType { get; private set; }
    public int    MaxStack { get; private set; }

    // 고유 속성들 (변경되지 않는다고 가정)
    private readonly Dictionary<string, object> _uniqueProps;

    // 아이콘 스프라이트
    public Sprite Icon { get; private set; }

    /// <summary>
    /// 생성자: 외부에서 모든 값을 전달받아 초기화만 수행합니다.
    /// </summary>
    /// <param name="itemId">아이템 식별자</param>
    /// <param name="name">아이템 이름</param>
    /// <param name="spriteName">스프라이트 키</param>
    /// <param name="itemType">아이템 타입</param>
    /// <param name="maxStack">최대 스택 개수</param>
    /// <param name="uniqueProps">고유 속성 딕셔너리</param>
    /// <param name="icon">아이콘 스프라이트</param>
    public ItemData(
        string itemId,
        string name,
        string spriteName,
        string itemType,
        int maxStack,
        Dictionary<string, object> uniqueProps,
        Sprite icon)
    {
        ItemId     = itemId;
        Name       = name;
        SpriteName = spriteName;
        ItemType   = itemType;
        MaxStack   = maxStack;
        Icon       = icon;

        // 외부에서 전달된 고유 속성만 보관
        _uniqueProps = uniqueProps != null
            ? new Dictionary<string, object>(uniqueProps)
            : new Dictionary<string, object>();
    }

    /// <summary>
    /// 고유 속성 값 조회
    /// </summary>
    public T GetUnique<T>(string key)
    {
        if (_uniqueProps.TryGetValue(key, out var val) && val is T t)
            return t;
        return default;
    }

    /// <summary>
    /// 모든 고유 속성 키 목록을 반환합니다.
    /// </summary>
    public IEnumerable<string> UniqueKeys => _uniqueProps.Keys;

    /// <summary>
    /// 읽기 전용 고유 속성 사전
    /// </summary>
    public IReadOnlyDictionary<string, object> UniqueProps => _uniqueProps;
}
