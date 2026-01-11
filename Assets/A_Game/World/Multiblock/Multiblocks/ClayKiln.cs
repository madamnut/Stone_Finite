// ClayKiln.cs (전체 교체본) - 연료 타는중(_fuelTicksLeft > 0)일 때만 meta=1, 아니면 meta=0
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class ClayKiln : Multiblock
{
    public enum SlotKind
    {
        FuelIn, FuelOut,
        FireInA, FireOutA,
        FireInB, FireOutB
    }

    ItemData _fuelIn;
    ItemData _fuelOut;

    ItemData _fireInA;
    ItemData _fireOutA;

    ItemData _fireInB;
    ItemData _fireOutB;

    int _fuelTicksLeft = 0;
    int _fuelTicksMax  = 0;

    string _fuelResultItemId = null;
    int _fuelResultAmount = 1;

    class FireLane
    {
        public ItemData In;
        public ItemData Out;

        public int ticksDone;
        public int ticksNeed;

        public string resultItemId;
        public int resultAmount;

        public string prevInItemId;
        public int prevInDur;

        public void ResetProgress()
        {
            ticksDone = 0;
            ticksNeed = 0;
            resultItemId = null;
            resultAmount = 1;
        }

        public bool IngredientChanged()
        {
            if (In == null || In.Count <= 0)
                return !string.IsNullOrEmpty(prevInItemId);

            string curId = In.ItemId;
            int curDur = In.Durability;

            if (string.IsNullOrEmpty(prevInItemId)) return true;
            if (curId != prevInItemId) return true;
            if (curDur != prevInDur) return true;

            return false;
        }

        public void SnapshotIngredient()
        {
            if (In == null || In.Count <= 0)
            {
                prevInItemId = null;
                prevInDur = 0;
                return;
            }

            prevInItemId = In.ItemId;
            prevInDur = In.Durability;
        }
    }

    readonly FireLane _laneA = new FireLane();
    readonly FireLane _laneB = new FireLane();

    bool _droppedOnDestroy = false;

    const int FIRE_HOLD_TICKS = 5;
    int _fireHoldTicksLeft = 0;

    // 논리상 "타고 있음"(연료 소모 기준)
    public bool Isburning => _fuelTicksLeft > 0;

    // VFX용(hold 포함)
    bool IsFireActiveFx => _fuelTicksLeft > 0 || _fireHoldTicksLeft > 0;

    public float FuelProgress01
    {
        get
        {
            if (_fuelTicksMax <= 0) return 0f;
            return Mathf.Clamp01((float)_fuelTicksLeft / _fuelTicksMax);
        }
    }

    public float FireProgressA01
    {
        get
        {
            if (_laneA.ticksNeed <= 0) return 0f;
            return Mathf.Clamp01((float)_laneA.ticksDone / _laneA.ticksNeed);
        }
    }

    public float FireProgressB01
    {
        get
        {
            if (_laneB.ticksNeed <= 0) return 0f;
            return Mathf.Clamp01((float)_laneB.ticksDone / _laneB.ticksNeed);
        }
    }

    public ItemData GetSlot(SlotKind kind)
    {
        return kind switch
        {
            SlotKind.FuelIn   => _fuelIn,
            SlotKind.FuelOut  => _fuelOut,
            SlotKind.FireInA  => _fireInA,
            SlotKind.FireOutA => _fireOutA,
            SlotKind.FireInB  => _fireInB,
            SlotKind.FireOutB => _fireOutB,
            _ => null
        };
    }

    public void SetSlot(SlotKind kind, ItemData item)
    {
        switch (kind)
        {
            case SlotKind.FuelIn:   _fuelIn = item; break;
            case SlotKind.FuelOut:  _fuelOut = item; break;

            case SlotKind.FireInA:  _fireInA = item; break;
            case SlotKind.FireOutA: _fireOutA = item; break;

            case SlotKind.FireInB:  _fireInB = item; break;
            case SlotKind.FireOutB: _fireOutB = item; break;
        }

        CleanupZeroCountSlots();
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
    {
        Manager.OpenModule("Clay Kiln", this);
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

    // Fire_02 : (1, 0.3)
    // Smoke   : (1, 2)
    public override void GetVfxRequests(List<VfxRequest> outList)
    {
        if (outList == null) return;

        outList.Add(new VfxRequest
        {
            key    = "Fire_02",
            offset = new Vector2(1f, 0.3f),
            active = IsFireActiveFx
        });

        outList.Add(new VfxRequest
        {
            key    = "Smoke",
            offset = new Vector2(1f, 2f),
            active = IsFireActiveFx
        });
    }

    public override void Tick()
    {
        CleanupZeroCountSlots();

        _laneA.In  = _fireInA;
        _laneA.Out = _fireOutA;

        _laneB.In  = _fireInB;
        _laneB.Out = _fireOutB;

        bool wasBurning = Isburning; // ✅ meta 기준은 "연료가 타고 있나"
        bool wasFireFxOn = IsFireActiveFx;

        if (_laneA.IngredientChanged())
        {
            _laneA.ResetProgress();
            _laneA.SnapshotIngredient();
        }
        if (_laneB.IngredientChanged())
        {
            _laneB.ResetProgress();
            _laneB.SnapshotIngredient();
        }

        bool canFireA = CanFireNow(_laneA.In, out int needA, out string resA, out int amtA);
        bool canFireB = CanFireNow(_laneB.In, out int needB, out string resB, out int amtB);

        bool outBlockedA = IsOutputFullOrBlocked(_laneA.Out, resA, amtA);
        bool outBlockedB = IsOutputFullOrBlocked(_laneB.Out, resB, amtB);

        bool anyCanProcess = (canFireA && !outBlockedA) || (canFireB && !outBlockedB);

        // 1) 연료가 없으면: 처리 가능한 상황에서만 점화 시도
        if (_fuelTicksLeft <= 0)
        {
            if (!string.IsNullOrEmpty(_fuelResultItemId))
            {
                TryPushFuelResultToFuelOut();
            }
            else
            {
                if (anyCanProcess)
                {
                    if (!IsFuelOutBlockedForNewFuel())
                        TryStartBurnFromFuelIn();
                }
            }
        }

        // 2) 연료 감소
        if (_fuelTicksLeft > 0)
        {
            _fuelTicksLeft -= 1;
            if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;

            if (_fuelTicksLeft > 0) _fireHoldTicksLeft = 0;
        }

        // 2.5) 연료 종료 시 hold 시작
        if (wasBurning && _fuelTicksLeft <= 0)
        {
            _fireHoldTicksLeft = FIRE_HOLD_TICKS;
        }

        // 2.6) hold 감소
        if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
        {
            _fireHoldTicksLeft -= 1;
            if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
        }

        // 3) Fire 진행: "틱 시작 시 불꽃 ON" 기준 + 각 lane 별로 독립 진행
        if (wasBurning)
        {
            TickLaneFire(_laneA, canFireA, needA, resA, amtA, outBlockedA);
            TickLaneFire(_laneB, canFireB, needB, resB, amtB, outBlockedB);
        }

        // 4) 연료가 끝났으면 부산물 처리
        if (wasBurning && _fuelTicksLeft <= 0)
        {
            TryPushFuelResultToFuelOut();
        }

        _fireInA  = _laneA.In;
        _fireOutA = _laneA.Out;

        _fireInB  = _laneB.In;
        _fireOutB = _laneB.Out;

        // ✅ meta는 "연료가 타는 중" 기준으로만 동기화
        bool isBurningNow = Isburning;
        if (wasBurning != isBurningNow)
        {
            RequestApplyKilnMeta(isBurningNow);
        }

        // VFX edge는 필요 없지만, 여기선 따로 안함(루프라 active만 바뀌면 됨)

        CleanupZeroCountSlots();
    }

    void TickLaneFire(FireLane lane, bool canFire, int need, string resultItem, int amount, bool outBlocked)
    {
        if (!canFire) return;
        if (outBlocked) return;

        if (lane.ticksNeed <= 0 || lane.resultItemId != resultItem)
        {
            lane.ticksNeed = need;
            lane.resultItemId = resultItem;
            lane.resultAmount = Mathf.Max(1, amount);
            lane.ticksDone = Mathf.Clamp(lane.ticksDone, 0, lane.ticksNeed);
        }

        lane.ticksDone += 1;
        if (lane.ticksDone > lane.ticksNeed) lane.ticksDone = lane.ticksNeed;

        if (lane.ticksNeed > 0 && lane.ticksDone >= lane.ticksNeed)
        {
            if (!IsOutputFullOrBlocked(lane.Out, lane.resultItemId, lane.resultAmount))
            {
                if (ConsumeOne(lane.In))
                {
                    if (lane.In != null && lane.In.Count <= 0) lane.In = null;
                    TryProduceResultToOut(lane, lane.resultItemId, lane.resultAmount);
                }

                lane.ResetProgress();
                lane.SnapshotIngredient();
            }
        }
    }

    void RequestApplyKilnMeta(bool burning)
    {
        if (Manager == null) return;
        // ✅ 연료 타는중이면 meta=6, 아니면 meta=0
        Manager.ApplyMetaToAllOccupiedCells(this, (ushort)(burning ? 6 : 0));
    }

    void CleanupZeroCountSlots()
    {
        if (_fuelIn != null && _fuelIn.Count <= 0) _fuelIn = null;
        if (_fuelOut != null && _fuelOut.Count <= 0) _fuelOut = null;

        if (_fireInA != null && _fireInA.Count <= 0) _fireInA = null;
        if (_fireOutA != null && _fireOutA.Count <= 0) _fireOutA = null;

        if (_fireInB != null && _fireInB.Count <= 0) _fireInB = null;
        if (_fireOutB != null && _fireOutB.Count <= 0) _fireOutB = null;
    }

    bool CanFireNow(ItemData input, out int fireNeed, out string resultItem, out int amount)
    {
        fireNeed = 0;
        resultItem = null;
        amount = 1;

        if (input == null) return false;
        if (input.Count <= 0) return false;

        if (input.ToolActions == null) return false;
        if (!input.ToolActions.TryGetValue("Fire", out Dictionary<string, object> cfg) || cfg == null)
            return false;

        if (cfg.TryGetValue("fireTicks", out var ftObj) && ftObj != null)
        {
            if (ftObj is int i) fireNeed = i;
            else if (ftObj is long l) fireNeed = (int)l;
            else if (ftObj is float f) fireNeed = Mathf.RoundToInt(f);
            else if (ftObj is double d) fireNeed = (int)d;
            else int.TryParse(ftObj.ToString(), out fireNeed);
        }

        if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
            resultItem = riObj.ToString();

        if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
        {
            if (amObj is int i) amount = i;
            else if (amObj is long l) amount = (int)l;
            else if (amObj is float f) amount = Mathf.RoundToInt(f);
            else if (amObj is double d) amount = (int)d;
            else int.TryParse(amObj.ToString(), out amount);
        }
        amount = Mathf.Max(1, amount);

        if (fireNeed <= 0) return false;
        if (string.IsNullOrEmpty(resultItem)) return false;

        return true;
    }

    bool IsFuelOutBlockedForNewFuel()
    {
        if (_fuelIn == null) return true;

        if (_fuelIn.ToolActions == null) return true;
        if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
            return true;

        string resultItem = null;
        int amount = 1;

        if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
            resultItem = riObj.ToString();

        if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
        {
            if (amObj is int i) amount = i;
            else if (amObj is long l) amount = (int)l;
            else if (amObj is float f) amount = Mathf.RoundToInt(f);
            else if (amObj is double d) amount = (int)d;
            else int.TryParse(amObj.ToString(), out amount);
        }
        amount = Mathf.Max(1, amount);

        if (string.IsNullOrEmpty(resultItem))
            return false;

        if (_fuelOut == null) return false;
        if (_fuelOut.ItemId != resultItem) return true;

        return (_fuelOut.Count + amount) > _fuelOut.MaxStack;
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
        int amount = 1;

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

        if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
        {
            if (amObj is int i) amount = i;
            else if (amObj is long l) amount = (int)l;
            else if (amObj is float f) amount = Mathf.RoundToInt(f);
            else if (amObj is double d) amount = (int)d;
            else int.TryParse(amObj.ToString(), out amount);
        }
        amount = Mathf.Max(1, amount);

        if (burnTicks <= 0) return;

        _fuelIn.Count -= 1;
        if (_fuelIn.Count <= 0) _fuelIn = null;

        _fuelTicksLeft = burnTicks;
        _fuelTicksMax  = burnTicks;

        _fuelResultItemId = resultItem;
        _fuelResultAmount = amount;

        _fireHoldTicksLeft = 0;

        CleanupZeroCountSlots();

        // ✅ 점화 즉시 meta=1
        RequestApplyKilnMeta(true);
    }

    void TryPushFuelResultToFuelOut()
    {
        if (string.IsNullOrEmpty(_fuelResultItemId))
        {
            _fuelTicksMax = 0;
            _fuelResultAmount = 1;
            return;
        }

        int amount = Mathf.Max(1, _fuelResultAmount);

        if (IsOutputFullOrBlocked(_fuelOut, _fuelResultItemId, amount))
            return;

        if (_fuelOut == null)
        {
            if (Manager != null && Manager.ItemLibrary != null)
                _fuelOut = Manager.ItemLibrary.Create(_fuelResultItemId, amount);

            if (_fuelOut != null)
            {
                _fuelResultItemId = null;
                _fuelResultAmount = 1;
                _fuelTicksMax = 0;
            }

            CleanupZeroCountSlots();
            return;
        }

        _fuelOut.Count += amount;

        _fuelResultItemId = null;
        _fuelResultAmount = 1;
        _fuelTicksMax = 0;

        CleanupZeroCountSlots();
    }

    bool IsOutputFullOrBlocked(ItemData outSlot, string expectedItemId, int requiredAmount)
    {
        if (string.IsNullOrEmpty(expectedItemId))
            return false;

        requiredAmount = Mathf.Max(1, requiredAmount);

        if (outSlot == null) return false;
        if (outSlot.ItemId != expectedItemId) return true;

        return (outSlot.Count + requiredAmount) > outSlot.MaxStack;
    }

    void TryProduceResultToOut(FireLane lane, string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        amount = Mathf.Max(1, amount);

        if (IsOutputFullOrBlocked(lane.Out, itemId, amount))
            return;

        if (lane.Out == null)
        {
            if (Manager != null && Manager.ItemLibrary != null)
                lane.Out = Manager.ItemLibrary.Create(itemId, amount);

            return;
        }

        lane.Out.Count += amount;
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

        DropSlot(ref _fireInA, origin);
        DropSlot(ref _fireOutA, origin);

        DropSlot(ref _fireInB, origin);
        DropSlot(ref _fireOutB, origin);
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

        root["fuelIn"]   = PackItem(_fuelIn);
        root["fuelOut"]  = PackItem(_fuelOut);

        root["fireInA"]  = PackItem(_fireInA);
        root["fireOutA"] = PackItem(_fireOutA);

        root["fireInB"]  = PackItem(_fireInB);
        root["fireOutB"] = PackItem(_fireOutB);

        root["fuelTicksLeft"]    = _fuelTicksLeft;
        root["fuelTicksMax"]     = _fuelTicksMax;
        root["fuelResultItemId"] = _fuelResultItemId;
        root["fuelResultAmount"] = _fuelResultAmount;

        root["laneA_ticksDone"]  = _laneA.ticksDone;
        root["laneA_ticksNeed"]  = _laneA.ticksNeed;
        root["laneA_resultItem"] = _laneA.resultItemId;
        root["laneA_resultAmt"]  = _laneA.resultAmount;
        root["laneA_prevId"]     = _laneA.prevInItemId;
        root["laneA_prevDur"]    = _laneA.prevInDur;

        root["laneB_ticksDone"]  = _laneB.ticksDone;
        root["laneB_ticksNeed"]  = _laneB.ticksNeed;
        root["laneB_resultItem"] = _laneB.resultItemId;
        root["laneB_resultAmt"]  = _laneB.resultAmount;
        root["laneB_prevId"]     = _laneB.prevInItemId;
        root["laneB_prevDur"]    = _laneB.prevInDur;

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

        _fuelIn = _fuelOut = null;
        _fireInA = _fireOutA = null;
        _fireInB = _fireOutB = null;

        _fuelTicksLeft = _fuelTicksMax = 0;
        _fuelResultItemId = null;
        _fuelResultAmount = 1;

        _laneA.ResetProgress();
        _laneB.ResetProgress();
        _laneA.prevInItemId = null; _laneA.prevInDur = 0;
        _laneB.prevInItemId = null; _laneB.prevInDur = 0;

        _droppedOnDestroy = false;
        _fireHoldTicksLeft = 0;

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

                _fuelIn   = UnpackItem(root["fuelIn"]);
                _fuelOut  = UnpackItem(root["fuelOut"]);

                _fireInA  = UnpackItem(root["fireInA"]);
                _fireOutA = UnpackItem(root["fireOutA"]);

                _fireInB  = UnpackItem(root["fireInB"]);
                _fireOutB = UnpackItem(root["fireOutB"]);

                _fuelTicksLeft    = root.Value<int?>("fuelTicksLeft") ?? 0;
                _fuelTicksMax     = root.Value<int?>("fuelTicksMax") ?? 0;
                _fuelResultItemId = root.Value<string>("fuelResultItemId");
                _fuelResultAmount = root.Value<int?>("fuelResultAmount") ?? 1;
                if (_fuelResultAmount < 1) _fuelResultAmount = 1;

                _laneA.ticksDone    = root.Value<int?>("laneA_ticksDone") ?? 0;
                _laneA.ticksNeed    = root.Value<int?>("laneA_ticksNeed") ?? 0;
                _laneA.resultItemId = root.Value<string>("laneA_resultItem");
                _laneA.resultAmount = root.Value<int?>("laneA_resultAmt") ?? 1;
                if (_laneA.resultAmount < 1) _laneA.resultAmount = 1;
                _laneA.prevInItemId = root.Value<string>("laneA_prevId");
                _laneA.prevInDur    = root.Value<int?>("laneA_prevDur") ?? 0;

                _laneB.ticksDone    = root.Value<int?>("laneB_ticksDone") ?? 0;
                _laneB.ticksNeed    = root.Value<int?>("laneB_ticksNeed") ?? 0;
                _laneB.resultItemId = root.Value<string>("laneB_resultItem");
                _laneB.resultAmount = root.Value<int?>("laneB_resultAmt") ?? 1;
                if (_laneB.resultAmount < 1) _laneB.resultAmount = 1;
                _laneB.prevInItemId = root.Value<string>("laneB_prevId");
                _laneB.prevInDur    = root.Value<int?>("laneB_prevDur") ?? 0;
            }
        }

        CleanupZeroCountSlots();

        // ✅ 로드 직후 meta는 "연료 타는중" 기준으로 맞춤
        RequestApplyKilnMeta(Isburning);
    }
}
