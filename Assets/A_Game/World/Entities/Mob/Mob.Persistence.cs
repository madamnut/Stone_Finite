using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.World
{
public partial class Mob
{
    [Serializable]
    private class MobPayload
    {
        public string mobId;
        public int maxHp;
        public int currentHp;
    }

    public override EntitySaveData ToSaveData()
    {
        mobPosition = transform.position;

        var payload = new MobPayload
        {
            mobId = mobId,
            maxHp = maxHp,
            currentHp = currentHp
        };

        return new EntitySaveData
        {
            Kind = EntityKind.Mob,
            Position = mobPosition,
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        MobPosition = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<MobPayload>(data.PayloadJson);
                if (payload != null)
                {
                    mobId = payload.mobId;

                    if (payload.maxHp > 0)
                        maxHp = payload.maxHp;
                    else if (maxHp < 1)
                        maxHp = 1;

                    currentHp = Mathf.Clamp(payload.currentHp, 0, maxHp);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mob] payload ???뼓 ??쎈솭: {ex.Message}");
            }
        }
    }
}
}
