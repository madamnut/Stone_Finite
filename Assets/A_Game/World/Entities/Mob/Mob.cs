using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 紐?怨듯넻 踰좎씠??
/// - Entity ?곸냽
/// - MobId(醫낅쪟) + MobPosition(?쇰━ ?꾩튂) + HP 蹂댁쑀
/// - ?몄씠釉?濡쒕뱶??MobId + ?꾩튂 + HP ?ㅻ８
/// </summary>
namespace Game.World
{
public partial class Mob : Entity
{
    [Header("Mob Info")]
    [SerializeField] private string mobId;

    // ?몄씠釉?濡쒕뱶???쇰━ ?꾩튂(?붾뱶 醫뚰몴)
    [SerializeField] private Vector2 mobPosition;

    [Header("HP")]
    public int maxHp = 10;                  // ?꾨━?밸쭏??媛쒕퀎 ?ㅼ젙
    [SerializeField] private int currentHp; // ?고????몄씠釉뚯슜

    [Header("Corpse")]
    [Tooltip("??紐뱀씠 二쎌뿀?????앹꽦???쒖껜 corpseId (MobLibrary?먯꽌 mobId + \"_Corpse\" 洹쒖튃?쇰줈 ?명똿)")]
    [SerializeField] private string corpseIdOnDeath;

    [Tooltip("?쒖껜 ?ㅽ룿???ъ슜??CorpseLibrary (鍮꾩뼱 ?덉쑝硫?FindObjectOfType濡???踰?李얠쓬)")]
    [SerializeField] private CorpseLibrary corpseLibrary;

    /// <summary>紐?醫낅쪟 ?앸퀎??ID (?? "Cow", "Wolf")</summary>
    public string MobId
    {
        get => mobId;
        set => mobId = value;
    }

    /// <summary>
    /// 紐뱀쓽 ?쇰━ ?꾩튂.
    /// ?ㅼ젙 ??transform.position ???④퍡 媛깆떊.
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

    /// <summary>??紐뱀씠 二쎌뿀?????앹꽦???쒖껜 corpseId (?? "Cow_Corpse")</summary>
    public string CorpseIdOnDeath => corpseIdOnDeath;

    /// <summary>MobLibrary ?깆뿉??corpseId ?명똿???명꽣</summary>
    public void SetCorpseId(string id)
    {
        corpseIdOnDeath = id;
    }

    /// <summary>理쒕? HP (?쎄린 ?꾩슜 ?묎렐??</summary>
    public int MaxHp => maxHp;

    /// <summary>?꾩옱 HP</summary>
    public int CurrentHp => currentHp;

    /// <summary>?댁븘?덈뒗吏 ?щ? (HP&gt;0)</summary>
    public bool IsAlive => currentHp > 0;

    public override EntityKind Kind => EntityKind.Mob;

    // ?ш린瑜?virtual + protected 濡?蹂寃?
    protected virtual void Awake()
    {
        // ?꾨━??湲곕낯媛?蹂댁젙
        if (maxHp < 1)
            maxHp = 1;

        // ?덈줈 ?ㅽ룿??紐??꾨━??湲곗? currentHp == 0) ? ??쇰줈 ?쒖옉
        // ?몄씠釉뚯뿉??濡쒕뱶??紐뱀? ?섏쨷??FromSaveData ?먯꽌 currentHp 瑜???뼱?
        if (currentHp <= 0 || currentHp > maxHp)
            currentHp = maxHp;
    }

    //????????????????????????????????????????????
    // HP / ?곕?吏
    //????????????????????????????????????????????

    /// <summary>HP瑜?吏곸젒 ?명똿 (?몄씠釉?濡쒕뱶 ?깆뿉???ъ슜)</summary>
    public void SetHp(int hp, int? newMaxHp = null)
    {
        if (newMaxHp.HasValue && newMaxHp.Value > 0)
            maxHp = newMaxHp.Value;

        if (maxHp < 1)
            maxHp = 1;

        currentHp = Mathf.Clamp(hp, 0, maxHp);
    }

    /// <summary>?곕?吏 ?곸슜. amount&gt;0留??섎? ?덉쓬.</summary>
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

    /// <summary>??/ ?뚮났. amount&gt;0留??섎? ?덉쓬.</summary>
    public virtual void Heal(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive)   return;

        currentHp += amount;
        if (currentHp > maxHp)
            currentHp = maxHp;
    }

    /// <summary>?곕?吏 ???? ?뚯깮 ?대옒?ㅼ뿉???댄럺???ъ슫?????ㅻ쾭?쇱씠?쒖슜.</summary>
    protected virtual void OnDamaged(int amount)
    {
        // 湲곕낯 援ы쁽 ?놁쓬
    }

    /// <summary>?щ쭩 泥섎━. 湲곕낯? OnDeath ?몄텧.</summary>
    protected virtual void Die()
    {
        OnDeath();
    }

    /// <summary>
    /// ?щ쭩 ??泥섎━.
    /// - 湲곕낯 援ы쁽:
    ///   1) corpseIdOnDeath 媛 ?ㅼ젙?섏뼱 ?덉쑝硫?CorpseLibrary ?듯빐 ?쒖껜 ?ㅽ룿
    ///   2) ?먭린 ?먯떊 Destroy
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
                // ?쒖껜 ?ㅽ룿 ?꾩튂???꾩옱 ?쇰━ ?꾩튂 湲곗? 
                Vector2 pos = transform.position;
                WorldEntityFactory.SpawnCorpse(lib, corpseIdOnDeath, pos);
            }
        }

        Destroy(gameObject);
    }
#endif

    //????????????????????????????????????????????
    // ?몄씠釉?/ 濡쒕뱶
    //????????????????????????????????????????????

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
        // ?꾩옱 ?꾩튂瑜?mobPosition???숆린??
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
            Position    = mobPosition, // ?꾩튂??怨듯넻 ?꾨뱶濡????
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        // ?꾩튂 蹂듭썝 (MobPosition ?듯빐 transform??媛숈씠 媛깆떊)
        MobPosition = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<MobPayload>(data.PayloadJson);
                if (payload != null)
                {
                    mobId = payload.mobId;

                    // ?몄씠釉?湲곗? maxHp / currentHp 蹂듭썝
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

        // corpseIdOnDeath ?????=mobId)?먯꽌 ??긽 ?ㅼ떆 ?좊룄?????덉쑝誘濡?
        // 蹂꾨룄 ?몄씠釉?濡쒕뱶???섏? ?딄퀬, MobLibrary 履쎌뿉???ㅽ룿 ???명똿?섎뒗 洹쒖튃???ъ슜.
    }
#endif
}
}
