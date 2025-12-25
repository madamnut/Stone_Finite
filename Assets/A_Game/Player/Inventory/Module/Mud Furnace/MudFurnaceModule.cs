// MudFurnaceModule.cs
using UnityEngine;
using UnityEngine.UI;

public class MudFurnaceModule : MonoBehaviour
{
    [Header("Slots")]
    public ItemSlot fuelInputSlot;         // 연료 입력
    public ItemSlot fuelByproductSlot;     // 연료 잔여물 출력
    public ItemSlot materialInputSlot;     // 재료 입력
    public ItemSlot materialOutputSlot;    // 재료 결과물 출력

    [Header("Progress Bars")]
    public Image cookProgressBar;          // 재료 가열 진행도 (0 → 1 증가)
    public Image burnProgressBar;          // 연료 소모 진행도 (1 → 0 감소)

    Player _player;

    public void Init(Player player)
    {
        _player = player;

        SetCookProgress(0f);   // 가열은 0부터 시작
        SetBurnProgress(0f);   // 불 없으면 0으로 표시
    }

    /// <summary>
    /// 재료 가열 진행도 (0 → 1)
    /// </summary>
    public void SetCookProgress(float t)
    {
        if (cookProgressBar != null)
            cookProgressBar.fillAmount = Mathf.Clamp01(t);
    }

    /// <summary>
    /// 연료 소모 진행도 (1 → 0 감소)
    /// t: 남은 연료 비율 (0~1)
    /// 내부적으로 fillAmount = t (1이면 꽉 찬 불, 0이면 불 없음)
    /// </summary>
    public void SetBurnProgress(float t)
    {
        if (burnProgressBar != null)
        {
            // “남은 연료 시간”이 들어오는 값 t(0~1)
            // t==1 → 게이지 풀 / t==0 → 게이지 0
            burnProgressBar.fillAmount = Mathf.Clamp01(t);
        }
    }
}
