// CraftModule.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class CraftModule : MonoBehaviour
{
    public enum TableType
    {
        Hand,       // 2-slot handcraft
        Primal,     // 4-slot primal workbench
        Forge,      // 9-slot forge workbench
        Industrial  // 16-slot industrial workbench
    }

    [Header("Table")]
    public TableType tableType = TableType.Hand;

    [Header("Inputs / Preview")]
    public List<ItemSlot> inputs  = new List<ItemSlot>(16); // 인풋 슬롯(최대 16)
    public List<ItemSlot> outputs = new List<ItemSlot>(2);  // 결과 프리뷰 슬롯(2슬롯)

    [Header("UI")]
    public Button craftButton; // 크래프팅 실행 버튼

    [Header("Refs")]
    // TryCraft(List<ItemData>, out List<ItemData> resultItems, out JArray inputActions, out JObject matchedRecipe)
    public RecipeLibrary recipeLibrary;
    public Player        player;

    // 상태
    JObject _matched;
    JArray  _inActions;

    // 스냅샷
    ItemData[] _prevItems;
    int[]      _prevCounts;
    int[]      _prevDurs;

    int ActiveInputCount
    {
        get
        {
            int max = inputs?.Count ?? 0;
            switch (tableType)
            {
                case TableType.Hand:       return Mathf.Min(2, max);
                case TableType.Primal:     return Mathf.Min(4, max);
                case TableType.Forge:      return Mathf.Min(9, max);
                case TableType.Industrial: return Mathf.Min(16, max);
                default:                   return max;
            }
        }
    }

    void Awake()
    {
        // 인풋 슬롯 초기화
        if (inputs == null) inputs = new List<ItemSlot>(16);
        int active = ActiveInputCount;

        for (int i = 0; i < inputs.Count; i++)
        {
            var s = inputs[i];
            if (s == null) continue;

            s.useLocalStorage = true;
            s.denyUserPut = false;
            s.denyUserInteraction = false;
            s.Set(null);

            // 테이블 타입 기준으로 사용하지 않는 슬롯은 비활성화
            if (i >= active)
                s.gameObject.SetActive(false);
            else
                s.gameObject.SetActive(true);
        }

        // 출력(프리뷰) 슬롯 초기화
        // ✅ 변경: 프리뷰는 "완전 상호작용 금지" (넣기/빼기 모두 금지)
        if (outputs == null) outputs = new List<ItemSlot>(2);
        for (int i = 0; i < outputs.Count; i++)
        {
            var s = outputs[i];
            if (s == null) continue;

            s.useLocalStorage = true;
            s.denyUserPut = true;
            s.denyUserInteraction = true; // 프리뷰 전용: 완전 조작 금지
            s.Set(null);
        }

        if (craftButton != null)
            craftButton.onClick.AddListener(OnClickCraft);

        AllocSnapshot();
        Snapshot();
        ScanAndPreview();
    }

    void OnDestroy()
    {
        if (craftButton != null)
            craftButton.onClick.RemoveListener(OnClickCraft);

        // 모듈 파괴 시, 인풋에 남아있는 아이템은 플레이어 인벤토리로 반환
        if (player == null || player.Inventory == null) return;

        int active = ActiveInputCount;
        for (int i = 0; i < active; i++)
        {
            var s = inputs[i];
            if (s == null || s.Item == null) continue;
            int left = player.Inventory.AddItem(s.Item);
            if (left == 0) s.Set(null);
            else { s.Item.Count = left; s.Refresh(); }
        }
    }

    void Update()
    {
        if (Changed())
        {
            Snapshot();
            ScanAndPreview();
        }
    }

    void AllocSnapshot()
    {
        int n = Mathf.Max(0, ActiveInputCount);
        _prevItems  = new ItemData[n];
        _prevCounts = new int[n];
        _prevDurs   = new int[n];
    }

    bool Changed()
    {
        if (inputs == null) return false;

        int active = ActiveInputCount;

        if (_prevItems == null || _prevItems.Length != active)
        {
            AllocSnapshot();
            return true;
        }

        for (int i = 0; i < active; i++)
        {
            var it = inputs[i]?.Item;
            int c  = it?.Count ?? 0;
            int d  = it?.Durability ?? 0;

            if (it != _prevItems[i] || c != _prevCounts[i] || d != _prevDurs[i])
                return true;
        }
        return false;
    }

    void Snapshot()
    {
        if (inputs == null) return;

        int active = ActiveInputCount;
        if (_prevItems == null || _prevItems.Length != active)
            AllocSnapshot();

        for (int i = 0; i < active; i++)
        {
            var it = inputs[i]?.Item;
            _prevItems[i]  = it;
            _prevCounts[i] = it?.Count ?? 0;
            _prevDurs[i]   = it?.Durability ?? 0;
        }
    }

    /// <summary>
    /// 인풋 슬롯 스냅샷으로 레시피 매칭 후, 결과 프리뷰만 갱신.
    /// 이 단계에서는 인풋액션·아이템 소모 없음.
    /// </summary>
    void ScanAndPreview()
    {
        _matched   = null;
        _inActions = null;

        // 프리뷰 슬롯 모두 초기화
        if (outputs != null)
        {
            for (int i = 0; i < outputs.Count; i++)
                if (outputs[i] != null) outputs[i].Set(null);
        }

        if (recipeLibrary == null) return;

        int active = ActiveInputCount;
        var snap = new List<ItemData>(active);
        for (int i = 0; i < active; i++)
            snap.Add(inputs[i]?.Item);

        if (recipeLibrary.TryCraft(snap, out List<ItemData> resultItems, out JArray inputActions, out JObject matched))
        {
            _matched   = matched;
            _inActions = inputActions;

            // 멀티 아웃풋 프리뷰 채우기 (최대 outputs.Count 개)
            if (outputs != null)
            {
                for (int i = 0; i < outputs.Count; i++)
                {
                    var slot = outputs[i];
                    if (slot == null) continue;

                    if (resultItems != null && i < resultItems.Count)
                        slot.Set(resultItems[i]);
                    else
                        slot.Set(null);
                }
            }
            return;
        }

        // 매칭 실패 시 이미 위에서 출력 슬롯 모두 null 처리함
    }

    /// <summary>
    /// 버튼 온클릭 → 크래프팅 실행.
    /// </summary>
    public void OnClickCraft()
    {
        ExecuteCraft();
    }

    /// <summary>
    /// 크래프팅 실행:
    /// - 현재 인풋으로 다시 레시피 매칭
    /// - 성공 시 모든 결과 아이템을 플레이어 인벤토리에 지급
    /// - inputActions 적용(소모/내구도 감소 등)
    /// - 이후 다시 프리뷰 갱신
    /// </summary>
    public void ExecuteCraft()
    {
        if (recipeLibrary == null) return;
        if (player == null || player.Inventory == null) return;
        if (_matched == null) return; // 현재 유효한 매칭 없음

        int active = ActiveInputCount;

        // 현재 슬롯 상태로 다시 매칭 (인풋 변경 가능성 대비)
        var snap = new List<ItemData>(active);
        for (int i = 0; i < active; i++)
            snap.Add(inputs[i]?.Item);

        if (!recipeLibrary.TryCraft(snap, out List<ItemData> freshList, out JArray inActs, out JObject matched))
        {
            // 더 이상 유효한 레시피가 아니면 프리뷰만 갱신
            ScanAndPreview();
            return;
        }

        _inActions = inActs;
        _matched   = matched;

        if (freshList == null || freshList.Count == 0) return;

        // 결과 아이템들을 플레이어 인벤토리에 지급
        for (int i = 0; i < freshList.Count; i++)
        {
            var item = freshList[i];
            if (item == null) continue;
            player.Inventory.AddItem(item);
        }

        // 인풋 액션 적용 (소모/내구도 등)
        ApplyInputActions(_inActions);

        Snapshot();
        ScanAndPreview();
    }

    void ApplyInputActions(JArray actions)
    {
        if (actions == null) return;

        int active = ActiveInputCount;
        int n = Mathf.Min(actions.Count, active);

        for (int i = 0; i < n; i++)
        {
            var slot = inputs[i];
            if (slot == null || slot.Item == null) continue;

            var act = actions[i] as JObject;
            if (act == null) continue;

            string type = act.Value<string>("type");
            int amount  = act.Value<int?>("amount") ?? 1;

            if (type == "consume")
            {
                slot.Item.Count -= amount;
                if (slot.Item.Count <= 0)
                    slot.Set(null);
                else
                    slot.Refresh();
            }
            else if (type == "durability")
            {
                // 내구도 시스템 없는 아이템 (MaxDurability == 0) → 스킵
                if (slot.Item.MaxDurability <= 0) continue;

                // amount 는 음수면 감소, 양수면 회복
                slot.Item.ModifyDurability(amount);

                if (slot.Item.Durability <= 0)
                    slot.Set(null);
                else
                    slot.Refresh();
            }
            else if (type == "consumeMetal")
            {
                // Crucible.details.layers 의 "맨 위(layers[-1])"에서 amount 만큼 소모
                // layers 원소는 JObject 또는 Dictionary<string, object> 형태를 허용
                if (slot.Item.Details == null) continue;
                if (!slot.Item.Details.TryGetValue("layers", out var layersObj) || layersObj == null) continue;

                List<object> layers = null;

                if (layersObj is List<object> list)
                {
                    layers = list;
                }
                else if (layersObj is JArray jarr)
                {
                    // normalize to List<object>
                    layers = new List<object>(jarr.Count);
                    for (int k = 0; k < jarr.Count; k++)
                        layers.Add(jarr[k]);
                    slot.Item.SetDetail("layers", layers);
                }
                else
                {
                    continue;
                }

                int need = Mathf.Max(1, amount);

                while (need > 0 && layers.Count > 0)
                {
                    int topIndex = layers.Count - 1;
                    object top = layers[topIndex];

                    int topAmt = 0;

                    if (top is JObject jo)
                    {
                        topAmt = jo.Value<int?>("amount") ?? 0;
                    }
                    else if (top is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue("amount", out var aObj) && aObj != null)
                        {
                            if (aObj is int ai) topAmt = ai;
                            else if (aObj is long al) topAmt = (int)al;
                            else if (aObj is float af) topAmt = Mathf.RoundToInt(af);
                            else if (aObj is double ad) topAmt = (int)ad;
                            else int.TryParse(aObj.ToString(), out topAmt);
                        }
                    }
                    else if (top is JToken tok)
                    {
                        // 드물게 JToken으로 들어온 경우
                        if (tok.Type == JTokenType.Object)
                        {
                            var o = (JObject)tok;
                            topAmt = o.Value<int?>("amount") ?? 0;
                        }
                    }

                    if (topAmt <= 0)
                    {
                        layers.RemoveAt(topIndex);
                        continue;
                    }

                    int take = Mathf.Min(topAmt, need);
                    int left = topAmt - take;
                    need -= take;

                    if (left <= 0)
                    {
                        layers.RemoveAt(topIndex);
                    }
                    else
                    {
                        if (top is JObject jo2)
                            jo2["amount"] = left;
                        else if (top is Dictionary<string, object> dict2)
                            dict2["amount"] = left;
                        else if (top is JToken tok2 && tok2.Type == JTokenType.Object)
                            ((JObject)tok2)["amount"] = left;
                    }
                }

                slot.Refresh();
            }
        }
    }
}
