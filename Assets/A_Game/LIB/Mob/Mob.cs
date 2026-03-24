using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// ëª?ê³µí†µ ë² ì´??
/// - Entity ?ì†
/// - MobId(ì¢…ë¥˜) + MobPosition(?¼ë¦¬ ?„ì¹˜) + HP ë³´ìœ 
/// - ?¸ì´ë¸?ë¡œë“œ??MobId + ?„ì¹˜ + HP ?¤ë£¸
/// </summary>
using Game.World;
public class Mob : Entity
{
    [Header("Mob Info")]
    [SerializeField] private string mobId;

    // ?¸ì´ë¸?ë¡œë“œ???¼ë¦¬ ?„ì¹˜(?”ë“œ ì¢Œí‘œ)
    [SerializeField] private Vector2 mobPosition;

    [Header("HP")]
    public int maxHp = 10;                  // ?„ë¦¬?¹ë§ˆ??ê°œë³„ ?¤ì •
    [SerializeField] private int currentHp; // ?°í????¸ì´ë¸Œìš©

    [Header("Corpse")]
    [Tooltip("??ëª¹ì´ ì£½ì—ˆ?????ì„±???œì²´ corpseId (MobLibrary?ì„œ mobId + \"_Corpse\" ê·œì¹™?¼ë¡œ ?¸íŒ…)")]
    [SerializeField] private string corpseIdOnDeath;

    [Tooltip("?œì²´ ?¤í°???¬ìš©??CorpseLibrary (ë¹„ì–´ ?ˆìœ¼ë©?FindObjectOfTypeë¡???ë²?ì°¾ìŒ)")]
    [SerializeField] private CorpseLibrary corpseLibrary;

    /// <summary>ëª?ì¢…ë¥˜ ?ë³„??ID (?? "Cow", "Wolf")</summary>
    public string MobId
    {
        get => mobId;
        set => mobId = value;
    }

    /// <summary>
    /// ëª¹ì˜ ?¼ë¦¬ ?„ì¹˜.
    /// ?¤ì • ??transform.position ???¨ê»˜ ê°±ì‹ .
    /// </summary>
    public Vector2 MobPosition
    {
        get => mobPosition;
        set
        {
            mobPosition = value;
            transform.position = new Vector3(value.x, value.y, transform.position.z);
        }
    }

    /// <summary>??ëª¹ì´ ì£½ì—ˆ?????ì„±???œì²´ corpseId (?? "Cow_Corpse")</summary>
    public string CorpseIdOnDeath => corpseIdOnDeath;

    /// <summary>MobLibrary ?±ì—??corpseId ?¸íŒ…???¸í„°</summary>
    public void SetCorpseId(string id)
    {
        corpseIdOnDeath = id;
    }

    /// <summary>ìµœë? HP (?½ê¸° ?„ìš© ?‘ê·¼??</summary>
    public int MaxHp => maxHp;

    /// <summary>?„ì¬ HP</summary>
    public int CurrentHp => currentHp;

    /// <summary>?´ì•„?ˆëŠ”ì§€ ?¬ë? (HP&gt;0)</summary>
    public bool IsAlive => currentHp > 0;

    public override EntityKind Kind => EntityKind.Mob;

    // ?¬ê¸°ë¥?virtual + protected ë¡?ë³€ê²?
    protected virtual void Awake()
    {
        // ?„ë¦¬??ê¸°ë³¸ê°?ë³´ì •
        if (maxHp < 1)
            maxHp = 1;

        // ?ˆë¡œ ?¤í°??ëª??„ë¦¬??ê¸°ì? currentHp == 0) ?€ ?€?¼ë¡œ ?œì‘
        // ?¸ì´ë¸Œì—??ë¡œë“œ??ëª¹ì? ?˜ì¤‘??FromSaveData ?ì„œ currentHp ë¥???–´?€
        if (currentHp <= 0 || currentHp > maxHp)
            currentHp = maxHp;
    }

    //?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
    // HP / ?°ë?ì§€
    //?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€

    /// <summary>HPë¥?ì§ì ‘ ?¸íŒ… (?¸ì´ë¸?ë¡œë“œ ?±ì—???¬ìš©)</summary>
    public void SetHp(int hp, int? newMaxHp = null)
    {
        if (newMaxHp.HasValue && newMaxHp.Value > 0)
            maxHp = newMaxHp.Value;

        if (maxHp < 1)
            maxHp = 1;

        currentHp = Mathf.Clamp(hp, 0, maxHp);
    }

    /// <summary>?°ë?ì§€ ?ìš©. amount&gt;0ë§??˜ë? ?ˆìŒ.</summary>
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

    /// <summary>??/ ?Œë³µ. amount&gt;0ë§??˜ë? ?ˆìŒ.</summary>
    public virtual void Heal(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive)   return;

        currentHp += amount;
        if (currentHp > maxHp)
            currentHp = maxHp;
    }

    /// <summary>?°ë?ì§€ ???? ?Œìƒ ?´ë˜?¤ì—???´í™???¬ìš´?????¤ë²„?¼ì´?œìš©.</summary>
    protected virtual void OnDamaged(int amount)
    {
        // ê¸°ë³¸ êµ¬í˜„ ?†ìŒ
    }

    /// <summary>?¬ë§ ì²˜ë¦¬. ê¸°ë³¸?€ OnDeath ?¸ì¶œ.</summary>
    protected virtual void Die()
    {
        OnDeath();
    }

    /// <summary>
    /// ?¬ë§ ??ì²˜ë¦¬.
    /// - ê¸°ë³¸ êµ¬í˜„:
    ///   1) corpseIdOnDeath ê°€ ?¤ì •?˜ì–´ ?ˆìœ¼ë©?CorpseLibrary ?µí•´ ?œì²´ ?¤í°
    ///   2) ?ê¸° ?ì‹  Destroy
    /// </summary>
    protected virtual void OnDeath()
    {
        if (!string.IsNullOrEmpty(corpseIdOnDeath))
        {
            var lib = corpseLibrary;
            if (lib == null)
                lib = FindObjectOfType<CorpseLibrary>();

            if (lib != null)
            {
                // ?œì²´ ?¤í° ?„ì¹˜???„ì¬ ?¼ë¦¬ ?„ì¹˜ ê¸°ì? 
                Vector2 pos = transform.position;
                lib.SpawnCorpse(corpseIdOnDeath, pos);
            }
        }

        Destroy(gameObject);
    }

    //?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€
    // ?¸ì´ë¸?/ ë¡œë“œ
    //?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€?€

    [Serializable]
    private class MobPayload
    {
        public string mobId;
        public int    maxHp;
        public int    currentHp;
    }

    public override EntitySaveData ToSaveData()
    {
        // ?„ì¬ ?„ì¹˜ë¥?mobPosition???™ê¸°??
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
            Position    = mobPosition, // ?„ì¹˜??ê³µí†µ ?„ë“œë¡??€??
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        // ?„ì¹˜ ë³µì› (MobPosition ?µí•´ transform??ê°™ì´ ê°±ì‹ )
        MobPosition = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<MobPayload>(data.PayloadJson);
                if (payload != null)
                {
                    mobId = payload.mobId;

                    // ?¸ì´ë¸?ê¸°ì? maxHp / currentHp ë³µì›
                    if (payload.maxHp > 0)
                        maxHp = payload.maxHp;
                    else if (maxHp < 1)
                        maxHp = 1;

                    currentHp = Mathf.Clamp(payload.currentHp, 0, maxHp);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Mob] payload ?Œì‹± ?¤íŒ¨: {ex.Message}");
            }
        }

        // corpseIdOnDeath ???€??=mobId)?ì„œ ??ƒ ?¤ì‹œ ? ë„?????ˆìœ¼ë¯€ë¡?
        // ë³„ë„ ?¸ì´ë¸?ë¡œë“œ???˜ì? ?Šê³ , MobLibrary ìª½ì—???¤í° ???¸íŒ…?˜ëŠ” ê·œì¹™???¬ìš©.
    }
}
