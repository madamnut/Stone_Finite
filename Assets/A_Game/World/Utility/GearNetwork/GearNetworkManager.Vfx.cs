


using UnityEngine;

namespace Game.World
{
    public sealed partial class GearNetworkManager
    {
        
        void LateUpdate()
        {
            EnsureVfxRef();
            if (vfx == null) return;

            if (_gearNodes.Count > 0)
            {
                foreach (var kv in _gearNodes)
                {

                    int nodeId = kv.Key;
                    var gear = kv.Value;

                    _gearIdByNodeId.TryGetValue(nodeId, out var gearId);

                    Vector3 pos = CellCenterToWorld(gear.Center);
                    float rpm = Mathf.Max(0f, gear.Rpm);
                    int dir = (gear.Dir == GearNode.RotationDir.CW) ? 1 : -1;

                    if (!string.IsNullOrEmpty(gearId))
                        vfx.SetRotatingLoopVfx(nodeId, gearId, true, pos, rpm, dir);
                }
            }

            if (_sourceNodes.Count > 0)
            {
                foreach (var kv in _sourceNodes)
                {
                    int sourceNodeId = kv.Key;
                    var src = kv.Value;

                    _sourceIdByNodeId.TryGetValue(sourceNodeId, out var sourceId);

                    Vector3 pos = CellCenterToWorld(src.AttachedGearCenter);
                    float rpm = Mathf.Max(0f, src.CurrentRpm);
                    int dir = (src.Dir == SourceNode.RotationDir.CW) ? 1 : -1;

                    if (!string.IsNullOrEmpty(sourceId))
                        vfx.SetRotatingLoopVfx(sourceNodeId, sourceId, true, pos, rpm, dir);
                }
            }

            if (_beltByStartGearNodeId.Count > 0)
            {
                foreach (var kv in _beltByStartGearNodeId)
                {
                    int ownerStartGearNodeId = kv.Key;
                    var link = kv.Value;

                    if (!_beltKindByStartGearNodeId.TryGetValue(ownerStartGearNodeId, out var beltKind) || string.IsNullOrEmpty(beltKind))
                        continue;

                    Color color = Color.white;
                    if (_beltSpecById.TryGetValue(beltKind, out var bspec))
                        color = bspec.color;

                    int gear0 = link.gearIds.gearId0;
                    int gear1 = link.gearIds.gearId1;

                    if (!_gearNodes.TryGetValue(gear0, out var g0)) continue;
                    if (!_gearNodes.TryGetValue(gear1, out var g1)) continue;

                    Vector3 startPos = CellCenterToWorld(g0.Center);
                    Vector3 endPos = CellCenterToWorld(g1.Center);

                    float rpm = Mathf.Max(0f, g0.Rpm);
                    int dir = (g0.Dir == GearNode.RotationDir.CW) ? 1 : -1;

                    vfx.SetBeltLoopVfx(ownerStartGearNodeId, beltKind, true, startPos, endPos, rpm, dir, color);
                }
            }
        }
    }
}
