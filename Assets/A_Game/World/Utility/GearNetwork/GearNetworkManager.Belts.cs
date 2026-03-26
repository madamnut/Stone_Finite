using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public sealed partial class GearNetworkManager
    {
        bool IsWaterAt(int x, int y)
        {
            if (world == null) return false;
            if (!world.InBounds(x, y)) return false;

            byte amt;
            ushort fid = world.GetFluidId(x, y, out amt);
            return fid == 1 && amt > 0;
        }

        public bool TryAttachBeltAtCells(Vector2Int startAnyGearCell, Vector2Int endAnyGearCell, string beltKind, out int materialCost)
        {
            materialCost = 0;

            if (world == null) return false;
            if (string.IsNullOrEmpty(beltKind)) return false;
            if (!TryGetGearNodeIdAtCell(startAnyGearCell, out int startGearNodeId)) return false;
            if (!TryGetGearNodeIdAtCell(endAnyGearCell, out int endGearNodeId)) return false;
            if (startGearNodeId == endGearNodeId) return false;
            if (!_gearNodes.TryGetValue(startGearNodeId, out var g0)) return false;
            if (!_gearNodes.TryGetValue(endGearNodeId, out var g1)) return false;
            if (HasAnyBeltOnGear(startGearNodeId)) return false;
            if (HasAnyBeltOnGear(endGearNodeId)) return false;

            materialCost = CalcBeltCost(g0.Center, g1.Center);

            var pair = new GearIdPair(startGearNodeId, endGearNodeId);
            var link = new BeltLink(pair, beltKind);

            _beltByStartGearNodeId[startGearNodeId] = link;
            _beltKindByStartGearNodeId[startGearNodeId] = beltKind;

            if (!_beltStartsByEndGearNodeId.TryGetValue(endGearNodeId, out var set))
            {
                set = new HashSet<int>();
                _beltStartsByEndGearNodeId[endGearNodeId] = set;
            }
            set.Add(startGearNodeId);

            if (!_suppressRebuild)
                RebuildNetworksFrom(startGearNodeId);

            return true;
        }

        public bool TryRemoveBeltAtGearCell(Vector2Int anyGearCell, out BeltDrop droppedBelt)
        {
            droppedBelt = default;

            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;

            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var link))
            {
                int endId = link.gearIds.gearId1;

                if (!_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out var beltKind))
                    return false;

                int count = 0;
                if (_gearNodes.TryGetValue(gearNodeId, out var g0) && _gearNodes.TryGetValue(endId, out var g1))
                    count = CalcBeltCost(g0.Center, g1.Center);

                droppedBelt = new BeltDrop { beltKind = beltKind, count = count };
                RemoveBeltInternal(gearNodeId, endId, beltKind);
                return true;
            }

            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                int startId = -1;
                foreach (var s in starts) { startId = s; break; }

                if (startId < 0) return false;
                if (!_beltKindByStartGearNodeId.TryGetValue(startId, out var beltKind)) return false;

                int count = 0;
                if (_gearNodes.TryGetValue(startId, out var g0) && _gearNodes.TryGetValue(gearNodeId, out var g1))
                    count = CalcBeltCost(g0.Center, g1.Center);

                droppedBelt = new BeltDrop { beltKind = beltKind, count = count };
                RemoveBeltInternal(startId, gearNodeId, beltKind);
                return true;
            }

            return false;
        }

        public bool TryRemoveBeltAtGearCell(Vector2Int anyGearCell, out BeltDrop droppedBelt, out Vector2Int otherGearCenter)
        {
            droppedBelt = default;
            otherGearCenter = default;

            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;

            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var link))
            {
                int endId = link.gearIds.gearId1;

                if (!_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out var beltKind))
                    return false;
                if (!_gearNodes.TryGetValue(gearNodeId, out var g0) || !_gearNodes.TryGetValue(endId, out var g1))
                    return false;

                droppedBelt = new BeltDrop { beltKind = beltKind, count = CalcBeltCost(g0.Center, g1.Center) };
                otherGearCenter = g1.Center;
                RemoveBeltInternal(gearNodeId, endId, beltKind);
                return true;
            }

            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                int startId = -1;
                foreach (var s in starts) { startId = s; break; }

                if (startId < 0) return false;
                if (!_beltKindByStartGearNodeId.TryGetValue(startId, out var beltKind)) return false;
                if (!_gearNodes.TryGetValue(startId, out var g0) || !_gearNodes.TryGetValue(gearNodeId, out var g1))
                    return false;

                droppedBelt = new BeltDrop { beltKind = beltKind, count = CalcBeltCost(g0.Center, g1.Center) };
                otherGearCenter = g0.Center;
                RemoveBeltInternal(startId, gearNodeId, beltKind);
                return true;
            }

            return false;
        }

        public bool TryGetBeltAtGearCell(Vector2Int anyGearCell, out string beltKind, out Vector2Int otherGearCenter)
        {
            beltKind = null;
            otherGearCenter = default;

            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;

            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var outLink))
            {
                int endId = outLink.gearIds.gearId1;
                if (!_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out beltKind))
                    return false;
                if (!_gearNodes.TryGetValue(endId, out var g1))
                    return false;

                otherGearCenter = g1.Center;
                return true;
            }

            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                int startId = -1;
                foreach (var s in starts) { startId = s; break; }

                if (startId < 0) return false;
                if (!_beltKindByStartGearNodeId.TryGetValue(startId, out beltKind))
                    return false;
                if (!_gearNodes.TryGetValue(startId, out var g0))
                    return false;

                otherGearCenter = g0.Center;
                return true;
            }

            return false;
        }

        bool HasAnyBeltOnGear(int gearNodeId)
        {
            if (_beltByStartGearNodeId.ContainsKey(gearNodeId))
                return true;

            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var set) && set != null && set.Count > 0)
                return true;

            return false;
        }

        void RemoveBeltsConnectedToGear(int gearNodeId, List<BeltDrop> drops)
        {
            if (_beltByStartGearNodeId.TryGetValue(gearNodeId, out var outLink))
            {
                int endGearId = outLink.gearIds.gearId1;

                if (_beltKindByStartGearNodeId.TryGetValue(gearNodeId, out var beltKind))
                {
                    int count = 0;
                    if (_gearNodes.TryGetValue(gearNodeId, out var g0) && _gearNodes.TryGetValue(endGearId, out var g1))
                        count = CalcBeltCost(g0.Center, g1.Center);

                    drops.Add(new BeltDrop { beltKind = beltKind, count = count });
                    RemoveBeltInternal(gearNodeId, endGearId, beltKind);
                }
            }

            if (_beltStartsByEndGearNodeId.TryGetValue(gearNodeId, out var starts) && starts != null && starts.Count > 0)
            {
                var tmp = new List<int>(starts);

                for (int i = 0; i < tmp.Count; i++)
                {
                    int startId = tmp[i];

                    if (!_beltKindByStartGearNodeId.TryGetValue(startId, out var beltKind))
                        continue;

                    int count = 0;
                    if (_gearNodes.TryGetValue(startId, out var g0) && _gearNodes.TryGetValue(gearNodeId, out var g1))
                        count = CalcBeltCost(g0.Center, g1.Center);

                    drops.Add(new BeltDrop { beltKind = beltKind, count = count });
                    RemoveBeltInternal(startId, gearNodeId, beltKind);
                }
            }
        }

        void RemoveBeltInternal(int startGearNodeId, int endGearNodeId, string beltKind)
        {
            EnsureVfxRef();
            if (vfx != null && !string.IsNullOrEmpty(beltKind))
                vfx.SetBeltLoopVfx(startGearNodeId, beltKind, false, Vector3.zero, Vector3.zero, 0f, 1, Color.white);

            _beltByStartGearNodeId.Remove(startGearNodeId);
            _beltKindByStartGearNodeId.Remove(startGearNodeId);

            if (_beltStartsByEndGearNodeId.TryGetValue(endGearNodeId, out var set))
            {
                set.Remove(startGearNodeId);
                if (set.Count == 0)
                    _beltStartsByEndGearNodeId.Remove(endGearNodeId);
            }

            if (!_suppressRebuild)
                RebuildNetworksFrom(startGearNodeId);
        }

        static int CalcBeltCost(Vector2Int a, Vector2Int b)
        {
            return 2;
        }

        public bool TryGetBeltMaterialItemId(string beltKind, out string materialItemId)
        {
            materialItemId = null;
            if (string.IsNullOrEmpty(beltKind)) return false;
            if (!_beltSpecById.TryGetValue(beltKind, out var spec)) return false;
            materialItemId = spec.materialItemId;
            return !string.IsNullOrEmpty(materialItemId);
        }
    }
}
