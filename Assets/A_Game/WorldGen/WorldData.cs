using System;

/// <summary>
/// 월드 전체 맵 데이터를 담는 구조체
/// • fg: 확장된 전경 셀 데이터 배열
/// • bg: 기존 배경 레이어 ID 배열
/// </summary>
[Serializable]
public struct WorldData
{
    public CellData[,] fg;
    public ushort[,]  bg;
}

/// <summary>
/// 전경 레이어용 확장 셀 데이터와 배경 레이어용 ID 맵을 담는 구조체
/// </summary>
[Serializable]
public struct CellData
{
    public ushort id;
    public bool hasCollider;
    public bool isLiquid;
    public bool hasGravity;
    public bool isDependent;
}