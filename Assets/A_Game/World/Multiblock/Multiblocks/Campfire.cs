// Campfire.cs
using System.Collections.Generic;
using UnityEngine;

public class Campfire : Multiblock
{
    public enum SlotKind { FuelIn, FuelOut, IngredientIn, IngredientOut }

    // 4 slots (요구사항)
    ItemData _fuelIn;
    ItemData _fuelOut;
    ItemData _ingIn;
    ItemData _ingOut;

    // 연료 버퍼(불꽃 게이지)
    int _fuelTicksLeft = 0;
    int _fuelTicksMax  = 0;

    // 현재 연료가 다 타면 나오는 부산물(예: Ash)
    string _fuelResultItemId = null;

    // 요리 진행(게이지)
    int _cookTicksDone = 0;
    int _cookTicksNeed = 0;
    string _cookResultItemId = null;

    // 재료 중간에 빼면 초기화 규칙용
    ItemData _prevIngRef = null;
    int _prevIngCount = 0;
    int _prevIngDur = 0;

    public bool Isburning => _fuelTicksLeft > 0;

    public float FuelProgress01
    {
        get
        {
            if (_fuelTicksMax <= 0) return 0f;
            return Mathf.Clamp01((float)_fuelTicksLeft / _fuelTicksMax);
        }
    }

    public float CookProgress01
    {
        get
        {
            if (_cookTicksNeed <= 0) return 0f;
            return Mathf.Clamp01((float)_cookTicksDone / _cookTicksNeed);
        }
    }

    public ItemData GetSlot(SlotKind kind)
    {
        return kind switch
        {
            SlotKind.FuelIn        => _fuelIn,
            SlotKind.FuelOut       => _fuelOut,
            SlotKind.IngredientIn  => _ingIn,
            SlotKind.IngredientOut => _ingOut,
            _ => null
        };
    }

    public void SetSlot(SlotKind kind, ItemData item)
    {
        switch (kind)
        {
            case SlotKind.FuelIn:        _fuelIn = item; break;
            case SlotKind.FuelOut:       _fuelOut = item; break;
            case SlotKind.IngredientIn:  _ingIn = item; break;
            case SlotKind.IngredientOut: _ingOut = item; break;
        }

        // ✅ 고스트 방지
        CleanupZeroCountSlots();
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        Manager.OpenModule("Campfire", this);
    }

    // ───────── VFX 요청 ─────────
    // Campfire는 Fire_01 하나만 사용, Origin 기준 (1, 0.5)
    public override void GetVfxRequests(List<VfxRequest> outList)
    {
        if (outList == null) return;

        outList.Add(new VfxRequest
        {
            key    = "Fire_01",
            offset = new Vector2(1f, .8f),
            active = Isburning
        });
    }

    public override void Tick()
    {
        CleanupZeroCountSlots();

        if (IngredientChanged())
        {
            ResetCookProgress();
            SnapshotIngredient();
        }

        bool canCookNow = CanCookNow(out int cookNeed, out string cookResult);
        bool ingOutBlocked = IsOutputFullOrBlocked(_ingOut, cookResult);

        if (_fuelTicksLeft <= 0)
        {
            if (!string.IsNullOrEmpty(_fuelResultItemId))
            {
                TryPushFuelResultToFuelOut();
            }
            else
            {
                if (canCookNow && !ingOutBlocked)
                {
                    if (!IsFuelOutBlockedForNewFuel())
                        TryStartBurnFromFuelIn();
                }
            }
        }

        if (_fuelTicksLeft > 0 && canCookNow && !ingOutBlocked)
        {
            if (_cookTicksNeed <= 0 || _cookResultItemId != cookResult)
            {
                _cookTicksNeed = cookNeed;
                _cookResultItemId = cookResult;
                _cookTicksDone = Mathf.Clamp(_cookTicksDone, 0, _cookTicksNeed);
            }

            _fuelTicksLeft -= 1;
            if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;

            _cookTicksDone += 1;
            if (_cookTicksDone > _cookTicksNeed) _cookTicksDone = _cookTicksNeed;

            if (_cookTicksNeed > 0 && _cookTicksDone >= _cookTicksNeed)
            {
                if (ConsumeOne(_ingIn))
                {
                    if (_ingIn != null && _ingIn.Count <= 0) _ingIn = null;
                    TryProduceCookResult();
                }

                ResetCookProgress();
                SnapshotIngredient();
            }

            if (_fuelTicksLeft <= 0)
            {
                TryPushFuelResultToFuelOut();
            }
        }

        CleanupZeroCountSlots();
    }

    void CleanupZeroCountSlots()
    {
        if (_fuelIn != null && _fuelIn.Count <= 0) _fuelIn = null;
        if (_fuelOut != null && _fuelOut.Count <= 0) _fuelOut = null;
        if (_ingIn != null && _ingIn.Count <= 0) _ingIn = null;
        if (_ingOut != null && _ingOut.Count <= 0) _ingOut = null;
    }

    bool IngredientChanged()
    {
        var it = _ingIn;
        int c = it != null ? it.Count : 0;
        int d = it != null ? it.Durability : 0;
        return (it != _prevIngRef || c != _prevIngCount || d != _prevIngDur);
    }

    void SnapshotIngredient()
    {
        _prevIngRef = _ingIn;
        _prevIngCount = _ingIn != null ? _ingIn.Count : 0;
        _prevIngDur = _ingIn != null ? _ingIn.Durability : 0;
    }

    void ResetCookProgress()
    {
        _cookTicksDone = 0;
        _cookTicksNeed = 0;
        _cookResultItemId = null;
    }

    bool CanCookNow(out int cookNeed, out string cookResult)
    {
        cookNeed = 0;
        cookResult = null;

        if (_ingIn == null) return false;
        if (_ingIn.Count <= 0) return false;

        if (_ingIn.ToolActions == null) return false;
        if (!_ingIn.ToolActions.TryGetValue("Cook", out Dictionary<string, object> cfg) || cfg == null)
            return false;

        if (cfg.TryGetValue("cookTicks", out var ctObj) && ctObj != null)
        {
            if (ctObj is int i) cookNeed = i;
            else if (ctObj is long l) cookNeed = (int)l;
            else if (ctObj is float f) cookNeed = Mathf.RoundToInt(f);
            else if (ctObj is double d) cookNeed = (int)d;
            else int.TryParse(ctObj.ToString(), out cookNeed);
        }

        if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
            cookResult = riObj.ToString();

        if (cookNeed <= 0) return false;
        if (string.IsNullOrEmpty(cookResult)) return false;

        return true;
    }

    bool IsFuelOutBlockedForNewFuel()
    {
        if (_fuelIn == null) return true;

        if (_fuelIn.ToolActions == null) return true;
        if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
            return true;

        string resultItem = null;
        if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
            resultItem = riObj.ToString();

        if (string.IsNullOrEmpty(resultItem))
            return false;

        if (_fuelOut == null) return false;
        if (_fuelOut.ItemId != resultItem) return true;

        return _fuelOut.Count >= _fuelOut.MaxStack;
    }

    void TryStartBurnFromFuelIn()
    {
        if (_fuelIn == null) return;
        if (_fuelIn.Count <= 0) { _fuelIn = null; return; }

        if (_fuelIn.ToolActions == null) return;
        if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
            return;

        int burnTicks = 0;
        string resultItem = null;

        if (cfg.TryGetValue("burnTicks", out var btObj) && btObj != null)
        {
            if (btObj is int i) burnTicks = i;
            else if (btObj is long l) burnTicks = (int)l;
            else if (btObj is float f) burnTicks = Mathf.RoundToInt(f);
            else if (btObj is double d) burnTicks = (int)d;
            else int.TryParse(btObj.ToString(), out burnTicks);
        }

        if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
            resultItem = riObj.ToString();

        if (burnTicks <= 0) return;

        _fuelIn.Count -= 1;
        if (_fuelIn.Count <= 0) _fuelIn = null;

        _fuelTicksLeft = burnTicks;
        _fuelTicksMax  = burnTicks;
        _fuelResultItemId = resultItem;

        CleanupZeroCountSlots();
    }

    void TryPushFuelResultToFuelOut()
    {
        if (string.IsNullOrEmpty(_fuelResultItemId))
        {
            _fuelTicksMax = 0;
            return;
        }

        if (_fuelOut == null)
        {
            if (Manager != null && Manager.ItemLibrary != null)
                _fuelOut = Manager.ItemLibrary.Create(_fuelResultItemId, 1);

            if (_fuelOut != null)
            {
                _fuelResultItemId = null;
                _fuelTicksMax = 0;
            }

            CleanupZeroCountSlots();
            return;
        }

        if (_fuelOut.ItemId != _fuelResultItemId)
            return;

        if (_fuelOut.Count >= _fuelOut.MaxStack)
            return;

        _fuelOut.Count += 1;

        _fuelResultItemId = null;
        _fuelTicksMax = 0;

        CleanupZeroCountSlots();
    }

    bool IsOutputFullOrBlocked(ItemData outSlot, string expectedItemId)
    {
        if (string.IsNullOrEmpty(expectedItemId))
            return false;

        if (outSlot == null) return false;
        if (outSlot.ItemId != expectedItemId) return true;

        return outSlot.Count >= outSlot.MaxStack;
    }

    void TryProduceCookResult()
    {
        if (string.IsNullOrEmpty(_cookResultItemId)) return;

        if (IsOutputFullOrBlocked(_ingOut, _cookResultItemId))
            return;

        if (_ingOut == null)
        {
            if (Manager != null && Manager.ItemLibrary != null)
                _ingOut = Manager.ItemLibrary.Create(_cookResultItemId, 1);

            CleanupZeroCountSlots();
            return;
        }

        _ingOut.Count += 1;
        CleanupZeroCountSlots();
    }

    bool ConsumeOne(ItemData it)
    {
        if (it == null) return false;
        if (it.Count <= 0) return false;
        it.Count -= 1;
        return true;
    }

    public override SaveData ToSaveData()
    {
        return new SaveData
        {
            DefId = DefId,
            InstId = InstId,
            Origin = Origin,
            Width = Width,
            Height = Height,
            PayloadJson = null
        };
    }

    public override void FromSaveData(SaveData data)
    {
        DefId  = data.DefId;
        InstId = data.InstId;
        Origin = data.Origin;
        Width  = data.Width;
        Height = data.Height;

        occupiedCells.Clear();
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));

        _fuelIn = _fuelOut = _ingIn = _ingOut = null;
        _fuelTicksLeft = _fuelTicksMax = 0;
        _fuelResultItemId = null;
        ResetCookProgress();
        SnapshotIngredient();
    }
}
