


using UnityEngine;

namespace Game.World
{
    public sealed partial class GearNetworkManager
    {
        
        public bool TryAttachSourceAtCell(Vector2Int anyGearCell, string sourceId, out int sourceNodeId)
        {

            sourceNodeId = -1;

            if (string.IsNullOrEmpty(sourceId)) return false;
            if (!TryGetGearNodeIdAtCell(anyGearCell, out var gearNodeId)) return false;

            var gear = _gearNodes[gearNodeId];
            if (_gearCenterToSourceNodeId.ContainsKey(gear.Center)) return false;

            return TryAddSource(gear.Center, sourceId, out sourceNodeId);
        }

        
        public bool TryAddSource(Vector2Int attachedGearCenter, string sourceId, out int sourceNodeId)
        {
            sourceNodeId = -1;

            if (string.IsNullOrEmpty(sourceId)) return false;
            if (!IsGearCenterCell(attachedGearCenter)) return false;
            if (!_gearCenterToNodeId.TryGetValue(attachedGearCenter, out var gearNodeId)) return false;

            if (!_sourceSpecById.TryGetValue(sourceId, out var spec))
            {
                if (!TryMapSourceKind(sourceId, out var k))
                    return false;

                spec = new SourceSpec { kind = k, rpm = 0, stressCapacity = 0 };
                _sourceSpecById[sourceId] = spec;
            }

            if (_gearCenterToSourceNodeId.ContainsKey(attachedGearCenter))
                return false;

            sourceNodeId = _nextNodeId++;

            var source = new SourceNode(
                sourceNodeId,
                attachedGearCenter,
                spec.kind,
                spec.stressCapacity,
                spec.rpm
            );

            source.Dir = SourceNode.RotationDir.CW;
            source.Rpm = 0;

            _sourceNodes.Add(sourceNodeId, source);
            _sourceIdByNodeId[sourceNodeId] = sourceId;
            _gearCenterToSourceNodeId[attachedGearCenter] = sourceNodeId;

            if (!_suppressRebuild)
                RebuildNetworksFrom(gearNodeId);

            EnsureVfxRef();
            if (vfx != null)
            {
                Vector3 pos = CellCenterToWorld(attachedGearCenter);
                vfx.SetRotatingLoopVfx(sourceNodeId, sourceId, true, pos, rpm: 0f, rotationDir: 1);
            }

            return true;
        }

        
        public bool TryRemoveSource(int sourceNodeId)
        {
            if (!_sourceNodes.TryGetValue(sourceNodeId, out var source))
                return false;

            EnsureVfxRef();
            if (vfx != null)
                vfx.DespawnAllForOwner(sourceNodeId);

            _sourceNodes.Remove(sourceNodeId);
            _sourceIdByNodeId.Remove(sourceNodeId);

            if (_gearCenterToSourceNodeId.TryGetValue(source.AttachedGearCenter, out int cur) && cur == sourceNodeId)
                _gearCenterToSourceNodeId.Remove(source.AttachedGearCenter);

            if (!_suppressRebuild && _gearCenterToNodeId.TryGetValue(source.AttachedGearCenter, out var gearNodeId))
                RebuildNetworksFrom(gearNodeId);

            return true;
        }

        
        public bool TryGetSourceAtGearCell(Vector2Int anyGearCell, out string sourceId)
        {
            sourceId = null;

            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
            if (!_gearNodes.TryGetValue(gearNodeId, out var gear))
                return false;
            if (!_gearCenterToSourceNodeId.TryGetValue(gear.Center, out int sourceNodeId))
                return false;

            return _sourceIdByNodeId.TryGetValue(sourceNodeId, out sourceId);
        }

        
        public bool TryRemoveSourceAtGearCell(Vector2Int anyGearCell, out string sourceId)
        {
            sourceId = null;

            if (!TryGetGearNodeIdAtCell(anyGearCell, out int gearNodeId))
                return false;
            if (!_gearNodes.TryGetValue(gearNodeId, out var gear))
                return false;
            if (!_gearCenterToSourceNodeId.TryGetValue(gear.Center, out int sourceNodeId))
                return false;

            _sourceIdByNodeId.TryGetValue(sourceNodeId, out sourceId);
            return TryRemoveSource(sourceNodeId);
        }
    }
}
