


using System;
using UnityEngine;
using Newtonsoft.Json;







namespace Game.World
{
public partial class Mob : Entity
{
    [Header("Mob Info")]
    [SerializeField] private string mobId;

    
    [SerializeField] private Vector2 mobPosition;

    [Header("HP")]

    public int maxHp = 10;                  
    [SerializeField] private int currentHp; 

    [Header("Corpse")]
    [Tooltip("??紐뱀씠 二쎌뿀?????앹꽦???쒖껜 corpseId (MobLibrary?먯꽌 mobId + \"_Corpse\" 洹쒖튃?쇰줈 ?명똿)")]
    [SerializeField] private string corpseIdOnDeath;

    [Tooltip("?쒖껜 ?ㅽ룿???ъ슜??CorpseLibrary (鍮꾩뼱 ?덉쑝硫?FindObjectOfType濡???踰?李얠쓬)")]
    [SerializeField] private CorpseLibrary corpseLibrary;

    
    public string MobId
    {
        get => mobId;
        set => mobId = value;
    }

    
    
    
    
    public Vector2 MobPosition
    {
        get => mobPosition;
        set
        {
            mobPosition = value;
            transform.position = new Vector3(value.x, value.y, transform.position.z);
        }
    }

    
    public string CorpseIdOnDeath => corpseIdOnDeath;

    
    
    public void SetCorpseId(string id)
    {
        corpseIdOnDeath = id;
    }

    
    public int MaxHp => maxHp;

    
    public int CurrentHp => currentHp;

    
    public bool IsAlive => currentHp > 0;

    public override EntityKind Kind => EntityKind.Mob;

    
    
    protected virtual void Awake()
    {
        
        if (maxHp < 1)
            maxHp = 1;

        
        
        if (currentHp <= 0 || currentHp > maxHp)
            currentHp = maxHp;
    }

    
    
    

    
    
    public void SetHp(int hp, int? newMaxHp = null)
    {
        if (newMaxHp.HasValue && newMaxHp.Value > 0)
            maxHp = newMaxHp.Value;

        if (maxHp < 1)
            maxHp = 1;

        currentHp = Mathf.Clamp(hp, 0, maxHp);
    }

    
#if false
    public virtual void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive)   return;

        currentHp -= amount;
        if (currentHp < 0) currentHp = 0;

        OnDamaged(amount);

        if (currentHp <= 0)
            Die();
    }

    
    public virtual void Heal(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive)   return;

        currentHp += amount;
        if (currentHp > maxHp)
            currentHp = maxHp;
    }

    
    protected virtual void OnDamaged(int amount)
    {
        
    }

    
    protected virtual void Die()
    {
        OnDeath();
    }

    
    
    
    
    
    
    protected virtual void OnDeath()
    {
        if (!string.IsNullOrEmpty(corpseIdOnDeath))
        {
            var lib = corpseLibrary;
            if (lib == null)
                lib = FindObjectOfType<CorpseLibrary>();

            if (lib != null)
            {
                
                Vector2 pos = transform.position;
                WorldEntityFactory.SpawnCorpse(lib, corpseIdOnDeath, pos);
            }
        }

        Destroy(gameObject);
    }
#endif

    
    
    

#if false
    [Serializable]
    private class MobPayload
    {
        public string mobId;
        public int    maxHp;
        public int    currentHp;
    }

    public override EntitySaveData ToSaveData()
    {
        
        mobPosition = transform.position;

        var payload = new MobPayload
        {
            mobId     = this.mobId,
            maxHp     = this.maxHp,
            currentHp = this.currentHp
        };

        return new EntitySaveData
        {
            Kind        = EntityKind.Mob,
            Position    = mobPosition, 
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
                Debug.LogError($"[Mob] payload ?뚯떛 ?ㅽ뙣: {ex.Message}");
            }
        }

        
        
    }
#endif
}
}
