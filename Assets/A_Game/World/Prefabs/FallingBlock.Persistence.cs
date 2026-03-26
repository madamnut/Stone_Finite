using Newtonsoft.Json;
using UnityEngine;

namespace Game.World
{
    public partial class FallingBlock
    {
        [System.Serializable]
        private class FallingBlockPayload
        {
            public ushort cellId;
        }

        public override EntitySaveData ToSaveData()
        {
            if (placed)
                return null;

            var payload = new FallingBlockPayload
            {
                cellId = cellId
            };

            return new EntitySaveData
            {
                Kind = EntityKind.FallingBlock,
                Position = transform.position,
                PayloadJson = JsonConvert.SerializeObject(payload)
            };
        }

        public override void FromSaveData(EntitySaveData data)
        {
            transform.position = data.Position;

            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                var payload = JsonConvert.DeserializeObject<FallingBlockPayload>(data.PayloadJson);
                if (payload != null)
                    cellId = payload.cellId;
            }

            ApplySprite();
        }
    }
}
