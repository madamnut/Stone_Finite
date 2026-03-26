


using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Core;

namespace Game.World
{
    public partial class Toolbench
    {
        
        public override SaveData ToSaveData()
        {
            var root = new JObject();

            
            JToken PackItem(ItemData it)
            {
                if (it == null || it.Count <= 0) return JValue.CreateNull();
                var o = new JObject();

                o["id"] = it.ItemId;
                o["count"] = it.Count;
                o["dur"] = it.Durability;
                return o;
            }

            root["material"] = PackItem(_material);
            root["tool"] = PackItem(_tool);
            root["preview"] = PackItem(_preview);

            return new SaveData
            {
                DefId = DefId,
                InstId = InstId,
                Origin = Origin,
                Width = Width,
                Height = Height,
                PayloadJson = root.ToString(),
                OriginalSolidIds = SnapshotOriginalSolidIds()
            };
        }

        
        public override void FromSaveData(SaveData data)
        {
            RestoreBaseSaveData(data);

            _material = _tool = _preview = null;
            _candidates.Clear();
            _remappedInputActions = null;
            _matchedRecipe = null;

            _droppedOnDestroy = false;

            _prevMatId = _prevToolId = null;
            _prevMatDur = _prevToolDur = 0;
            _prevMatCount = _prevToolCount = 0;

            if (string.IsNullOrEmpty(data.PayloadJson))
                return;

            JObject root = null;
            try { root = JObject.Parse(data.PayloadJson); }
            catch { root = null; }
            if (root == null) return;

            _material = UnpackSavedItem(root["material"]);
            _tool = UnpackSavedItem(root["tool"]);
            _preview = UnpackSavedItem(root["preview"]);

            InvalidateIfInputsChanged();
        }

        
        public override void OnCellBroken(Vector2Int brokenCell)
        {
            if (!_droppedOnDestroy)
            {
                _droppedOnDestroy = true;
                DropIfAny(_material);
                DropIfAny(_tool);
                DropIfAny(_preview);
            }

            base.OnCellBroken(brokenCell);
        }

        
        void DropIfAny(ItemData it)
        {
            if (it == null || it.Count <= 0) return;
            if (World == null || World.itemDropper == null) return;

            Vector3 origin = new Vector3(
                Origin.x + (Width * 0.5f),
                Origin.y + (Height * 0.5f),
                0f
            );

            var copy = CloneItem(it);
            World.itemDropper.SpawnDroppedItem(copy, origin);
        }
    }
}
