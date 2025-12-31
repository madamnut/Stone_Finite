// Campfire.cs (전체 교체본)
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

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

    // 재료 변경 감지용 (Count 변화는 무시: 스택 분할/합치기 시 초기화 방지)
    string _prevIngItemId = null;
    int _prevIngDur = 0;

    // 파괴 드랍 가드(멀티블럭 구성 셀들이 여러 번 깨져도 1회만 드랍)
    bool _droppedOnDestroy = false;

    // ───────── 불 VFX 끊김 방지(히스테리시스) ─────────
    // 연료가 0이 된 직후에도 N틱 동안 불을 유지 (연료 교체 1틱 꺼짐 방지)
    const int FIRE_HOLD_TICKS = 5;
    int _fireHoldTicksLeft = 0;

    // 논리상 "타고 있음"(요리/연료 소모 기준)
    public bool Isburning => _fuelTicksLeft > 0;

    // 표시/메타/VFX용 "불이 보이는 상태"
    bool IsFireActiveFx => _fuelTicksLeft > 0 || _fireHoldTicksLeft > 0;

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

        CleanupZeroCountSlots();
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        Manager.OpenModule("Campfire", this);
    }

    public override void OnCellBroken(Vector2Int brokenCell)
    {
        if (!_droppedOnDestroy)
        {
            _droppedOnDestroy = true;
            DropAllInternalItems();
        }

        base.OnCellBroken(brokenCell);
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
            active = IsFireActiveFx
        });
    }

    public override void Tick()
    {
        CleanupZeroCountSlots();

        bool wasBurning   = Isburning;      // 요리/연료 소모 기준
        bool wasFireFxOn  = IsFireActiveFx; // 표시/메타 기준

        if (IngredientChanged())
        {
            ResetCookProgress();
            SnapshotIngredient();
        }

        bool canCookNow = CanCookNow(out int cookNeed, out string cookResult);
        bool ingOutBlocked = IsOutputFullOrBlocked(_ingOut, cookResult);

        // 1) 연료가 없으면: (기존 유지) "요리 가능한 상황"에서만 점화 시도
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

        // 2) 불꽃이 켜져있으면: 재료 유무 상관없이 연료는 무조건 감소
        if (_fuelTicksLeft > 0)
        {
            _fuelTicksLeft -= 1;
            if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;

            // 실제 연료가 타는 중이면 hold는 필요 없음
            if (_fuelTicksLeft > 0) _fireHoldTicksLeft = 0;
        }

        // 2.5) 연료가 방금 끝난 경우: hold 시작(이번 틱 이후부터)
        if (wasBurning && _fuelTicksLeft <= 0)
        {
            _fireHoldTicksLeft = FIRE_HOLD_TICKS;
        }

        // 2.6) hold 틱 감소 (연료가 없는 동안만)
        if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
        {
            _fireHoldTicksLeft -= 1;
            if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
        }

        // 3) 요리는: "틱 시작 시 불꽃 ON" + 재료/출력 조건 만족 시에만 진행
        if (wasBurning && canCookNow && !ingOutBlocked)
        {
            if (_cookTicksNeed <= 0 || _cookResultItemId != cookResult)
            {
                _cookTicksNeed = cookNeed;
                _cookResultItemId = cookResult;
                _cookTicksDone = Mathf.Clamp(_cookTicksDone, 0, _cookTicksNeed);
            }

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
        }

        // 4) 이번 틱에 불이 끝났으면 부산물 처리
        if (wasBurning && _fuelTicksLeft <= 0)
        {
            TryPushFuelResultToFuelOut();
        }

        // 5) 상태 전이(edge)에서만 "캠프파이어 전체 파트" meta 변경 요청
        bool isFireFxOnNow = IsFireActiveFx;
        if (wasFireFxOn != isFireFxOnNow)
        {
            RequestApplyCampfireMeta(isFireFxOnNow);
        }

        CleanupZeroCountSlots();
    }

    void RequestApplyCampfireMeta(bool burning)
    {
        if (Manager == null) return;
        // Campfire는 모든 파트가 같이 변함: Default(meta=0), Burning(meta=1)
        Manager.ApplyMetaToAllOccupiedCells(this, (ushort)(burning ? 1 : 0));
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
        // 현재 재료 없음(또는 0개): 이전에 재료가 있었으면 변경으로 간주
        if (_ingIn == null || _ingIn.Count <= 0)
            return !string.IsNullOrEmpty(_prevIngItemId);

        string curId = _ingIn.ItemId;
        int curDur = _ingIn.Durability;

        if (string.IsNullOrEmpty(_prevIngItemId)) return true;
        if (curId != _prevIngItemId) return true;
        if (curDur != _prevIngDur) return true;

        // Count 변화는 무시
        return false;
    }

    void SnapshotIngredient()
    {
        if (_ingIn == null || _ingIn.Count <= 0)
        {
            _prevIngItemId = null;
            _prevIngDur = 0;
            return;
        }

        _prevIngItemId = _ingIn.ItemId;
        _prevIngDur = _ingIn.Durability;
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

        // 새 연료가 들어오면 hold는 끊음
        _fireHoldTicksLeft = 0;

        CleanupZeroCountSlots();

        // 점화 즉시 meta 변경 요청(불꽃 켜진 순간부터)
        RequestApplyCampfireMeta(true);
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

    void DropAllInternalItems()
    {
        if (World == null || World.itemDropper == null)
            return;

        Vector3 origin = new Vector3(
            Origin.x + (Width * 0.5f),
            Origin.y + (Height * 0.5f),
            0f
        );

        DropSlot(ref _fuelIn, origin);
        DropSlot(ref _fuelOut, origin);
        DropSlot(ref _ingIn, origin);
        DropSlot(ref _ingOut, origin);
    }

    void DropSlot(ref ItemData slot, Vector3 origin)
    {
        if (slot == null) return;
        if (slot.Count <= 0) { slot = null; return; }

        var copy = new ItemData(
            itemId:        slot.ItemId,
            name:          slot.Name,
            spriteName:    slot.SpriteName,
            itemType:      slot.ItemType,
            maxStack:      slot.MaxStack,
            maxDurability: slot.MaxDurability,
            durability:    slot.Durability,
            toolActions:   slot.ToolActions,
            weaponActions: slot.WeaponActions,
            breakActions:  slot.BreakActions,
            tags:          slot.Tags,
            details:       slot.Details,
            icon:          slot.Icon,
            count:         slot.Count
        );

        World.itemDropper.SpawnDroppedItem(copy, origin);
        slot = null;
    }

    public override SaveData ToSaveData()
    {
        // PayloadJson
        JObject root = new JObject();

        JObject PackItem(ItemData it)
        {
            if (it == null || it.Count <= 0) return null;
            return new JObject
            {
                ["id"]    = it.ItemId,
                ["count"] = it.Count,
                ["dur"]   = it.Durability
            };
        }

        root["fuelIn"]  = PackItem(_fuelIn);
        root["fuelOut"] = PackItem(_fuelOut);
        root["ingIn"]   = PackItem(_ingIn);
        root["ingOut"]  = PackItem(_ingOut);

        root["fuelTicksLeft"]    = _fuelTicksLeft;
        root["fuelTicksMax"]     = _fuelTicksMax;
        root["fuelResultItemId"] = _fuelResultItemId;

        root["cookTicksDone"]    = _cookTicksDone;
        root["cookTicksNeed"]    = _cookTicksNeed;
        root["cookResultItemId"] = _cookResultItemId;

        root["prevIngItemId"]    = _prevIngItemId;
        root["prevIngDur"]       = _prevIngDur;

        // OriginalSolidIds (row-major)
        ushort[] orig = new ushort[Width * Height];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            var cell = new Vector2Int(Origin.x + x, Origin.y + y);
            orig[x + y * Width] = originalSolidIds.TryGetValue(cell, out var id) ? id : (ushort)0;
        }

        return new SaveData
        {
            DefId = DefId,
            InstId = InstId,
            Origin = Origin,
            Width = Width,
            Height = Height,
            PayloadJson = root.ToString(),
            OriginalSolidIds = orig
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

        // originalSolidIds restore (row-major)
        originalSolidIds.Clear();
        if (data.OriginalSolidIds != null && data.OriginalSolidIds.Length == Width * Height)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                originalSolidIds[cell] = data.OriginalSolidIds[x + y * Width];
            }
        }

        // defaults
        _fuelIn = _fuelOut = _ingIn = _ingOut = null;
        _fuelTicksLeft = _fuelTicksMax = 0;
        _fuelResultItemId = null;

        _cookTicksDone = 0;
        _cookTicksNeed = 0;
        _cookResultItemId = null;

        _prevIngItemId = null;
        _prevIngDur = 0;

        _droppedOnDestroy = false;

        _fireHoldTicksLeft = 0;

        // payload load
        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            JObject root = null;
            try { root = JObject.Parse(data.PayloadJson); }
            catch { root = null; }

            if (root != null)
            {
                ItemData UnpackItem(JToken tok)
                {
                    if (tok == null || tok.Type == JTokenType.Null) return null;

                    string id = tok.Value<string>("id");
                    int count = tok.Value<int?>("count") ?? 0;
                    int dur   = tok.Value<int?>("dur") ?? 0;

                    if (string.IsNullOrEmpty(id) || count <= 0) return null;

                    ItemData it = null;
                    if (Manager != null && Manager.ItemLibrary != null)
                        it = Manager.ItemLibrary.Create(id, count);

                    if (it != null)
                        it.Durability = dur;

                    return it;
                }

                _fuelIn  = UnpackItem(root["fuelIn"]);
                _fuelOut = UnpackItem(root["fuelOut"]);
                _ingIn   = UnpackItem(root["ingIn"]);
                _ingOut  = UnpackItem(root["ingOut"]);

                _fuelTicksLeft    = root.Value<int?>("fuelTicksLeft") ?? 0;
                _fuelTicksMax     = root.Value<int?>("fuelTicksMax") ?? 0;
                _fuelResultItemId = root.Value<string>("fuelResultItemId");

                _cookTicksDone    = root.Value<int?>("cookTicksDone") ?? 0;
                _cookTicksNeed    = root.Value<int?>("cookTicksNeed") ?? 0;
                _cookResultItemId = root.Value<string>("cookResultItemId");

                _prevIngItemId    = root.Value<string>("prevIngItemId");
                _prevIngDur       = root.Value<int?>("prevIngDur") ?? 0;
            }
        }

        CleanupZeroCountSlots();

        // 로드 후 meta는 현재 상태 기준으로 맞춤(표시/메타 기준)
        RequestApplyCampfireMeta(IsFireActiveFx);
    }
}
