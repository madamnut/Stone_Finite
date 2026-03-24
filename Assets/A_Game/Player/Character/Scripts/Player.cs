using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

namespace Game.Player
{
    
    public partial class Player : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;   // 醫뚯슦 ?대룞 ?띾룄
        [SerializeField] private float jumpForce = 7f;   // ?먰봽 ??
    
        [Header("Components")]
        [SerializeField] private Rigidbody2D rb;                 // Rigidbody2D
        [SerializeField] private Collider2D groundCheckCollider; // 諛쒕컩 洹몃씪?대뱶 泥댄겕(Trigger)
        [SerializeField] private LayerMask groundLayerMask;      // Ground ?덉씠??
    
        // ???뚮옯???대젮媛湲??쒕∼ ?ㅻ（): "?뚮젅?댁뼱 臾쇰━ 肄쒕씪?대뜑" <-> "?꾩옱 ?묒큺 以묒씤 ?뚮옯??肄쒕씪?대뜑??留?0.1珥?Ignore
        [Header("Platform Drop-Through")]
        [SerializeField] private float dropThroughTime = 0.10f;   // S ?뚮????????쒓컙留뚰겮留?臾댁떆 (?좎??대룄 0.1珥?
        [SerializeField] private LayerMask platformLayerMask;     // ???뚮옯???덉씠???? 吏??
        [SerializeField] private Collider2D playerPhysicsCollider; // ??吏곸젒 ?좊떦: Trigger ?꾨땶 "?ㅼ젣" 臾쇰━ 肄쒕씪?대뜑
        private Coroutine _dropCo;
        private readonly List<Collider2D> _dropPlatforms = new List<Collider2D>(16);
        private ContactFilter2D _platformContactFilter;
    
        [Header("Fluid (Triggers)")]
        [SerializeField] private Collider2D bodyTriggerCollider; // 紐명넻 ?몃━嫄??좎껜 ?묒큺 ?먯젙)
        [SerializeField] private Collider2D headTriggerCollider; // 癒몃━ ?몃━嫄??좉?/??李멸린 ?먯젙)
        [SerializeField] private LayerMask fluidLayerMask;       // Fluid ?덉씠??
    
        [Header("Fluid Movement (Recommended Preset)")]
        [SerializeField] private float fluidMoveSpeedMultiplier = 0.5f; // 臾쇱냽 醫뚯슦 ?먮젮吏?
        [SerializeField] private float fluidHorizontalDamping = 10f;    // 臾쇱냽 醫뚯슦 ???
        [SerializeField] private float fluidVerticalDamping = 6f;       // 臾쇱냽 ?곹븯 ???
        [SerializeField] private float fluidSinkSpeed = 1.5f;           // 臾쇱냽 媛留뚰엳 ?덉쑝硫?泥쒖쿇??媛?쇱븠??紐⑺몴 y?띾룄)
        [SerializeField] private float swimUpAcceleration = 35f;        // (誘몄궗?? 媛??諛⑹떇 ?곌퀬 ?띠쑝硫??ъ슜
        [SerializeField] private float maxSwimUpSpeed = 6.5f;           // ?꾨줈 ?щ씪媛??理쒕? ?띾룄(紐⑺몴)
    
        [Header("Visual (Skin Root)")]
        [SerializeField] private Transform skinRoot;             // 移대찓???쒖쇅 ?ㅽ궓 猷⑦듃
    
        [Header("Visual (Body + Limbs)")]
        [SerializeField] private SpriteRenderer bodyRenderer;    // 癒몃━ ?ы븿 紐??ㅽ봽?쇱씠??
        [SerializeField] private SpriteRenderer leftArmRenderer;
        [SerializeField] private SpriteRenderer rightArmRenderer;
        [SerializeField] private SpriteRenderer leftLegRenderer;
        [SerializeField] private SpriteRenderer rightLegRenderer;
    
        [Header("Visual (Right Hand Item)")]
        public SpriteRenderer rightHandItemRenderer; // ?ㅻⅨ?먯뿉 遺숈? ?꾩씠???ㅽ봽?쇱씠??
    
        [Header("Walk Animation")]
        [SerializeField] private float walkSwingSpeed = 10f;     // ?섎몢瑜대뒗 ?띾룄
        [SerializeField] private float walkArmAmplitude = 20f;   // ???뚯쟾 媛곷룄(??
        [SerializeField] private float walkLegAmplitude = 25f;   // ?ㅻ━ ?뚯쟾 媛곷룄(??
        [SerializeField] private float walkReturnSpeed = 10f;    // 硫덉톬????湲곕낯 ?먯꽭濡?蹂듦? ?띾룄
    
        [Header("Damage Flash")]
        [SerializeField] private float damageFlashDuration = 0.1f;
    
        [Header("Audio")]
        [SerializeField] private AudioManager audioManager;
    
        [Header("UI (Survival Bars)")]
        [SerializeField] private Image hungerFillImage;   // Filled ???
        [SerializeField] private Image thirstFillImage;   // Filled ???
        [SerializeField] private Image staminaFillImage;  // Filled ???(?ㅽ깭誘몃꼫)
        [SerializeField] private Image oxygenFillImage;   // Filled ???(怨듦린)
    
        [Header("Hearts UI")]
        [SerializeField] private Transform heartRoot;     // ?섑듃 遺紐?Transform
        [SerializeField] private GameObject heartPrefab;  // ?섑듃 ?꾨━??(Heart.cs ?ы븿)
        [SerializeField] private SpriteAtlas heartAtlas;  // 0~4 ?ㅽ봽?쇱씠????λ맂 ?꾪??쇱뒪
    
        private Heart[] heartObjects;
    
        private float _moveInput;
        private bool _isGrounded;
        private bool _wasGrounded;
        private bool _jumpRequested;
    
        // ?숉븯 嫄곕━ 痢≪젙
        private bool _isFalling;
        private float _fallStartY;
        public float currentFallDistance; // 吏꾪뻾 以??숉븯 嫄곕━
        public float lastFallDistance;    // 留덉?留?李⑹? ?뚯쓽 ?숉븯 嫄곕━
    
        // ?앹〈 ?ㅽ꺈
        [Header("Survival Stats")]
        [Range(0, 40)] public int health = 40;   // 理쒕? 泥대젰 40
        [Range(0, 100)] public int hunger = 100;
        [Range(0, 100)] public int thirst = 100;
        [Range(0, 100)] public float stamina = 100f; // ?ㅽ깭誘몃꼫 (0~100, ?쒓컙 湲곕컲 ?뚮났/?뚮え)
    
        [Header("Oxygen")]
        [Range(0, 100)] public float oxygen = 100f;
        [SerializeField] private float oxygenDrainPerSecond = 6f;   // 癒몃━ ?좉? ??泥쒖쿇??媛먯냼
        [SerializeField] private float oxygenRecoverPerSecond = 10f;  // 癒몃━ ???좉린硫?臾쇱냽?댁뼱?? ??鍮좊Ⅴ寃??뚮났
        [SerializeField] private float drownDamageInterval = 1.0f; // oxygen==0?????곕?吏 ??媛꾧꺽
        [SerializeField] private int drownDamage = 5;    // ?깅떦 5 ?쇳빐
    
        [Header("Stamina Settings")]
        [SerializeField] private float staminaRegenPerSecond = 2f;  // 珥덈떦 2 ?뚮났
        [SerializeField] private float staminaMoveCostPerSecond = 4f;  // ?대룞 以?珥덈떦 4 ?뚮え
        [SerializeField] private float staminaJumpCost = 5f;  // ?먰봽 ??5 ?뚮え
    
        // 怨듦꺽 荑⑤떎??
        float _attackCooldownTimer = 0f;  // 0 ?댄븯????怨듦꺽 媛??
    
        // ?몃깽?좊━
        private const int InventoryCapacity = 50;
        public InventoryData Inventory { get; private set; }
    
        // ?대???(?곕?吏 ?뚮옒?쒖슜)
        SpriteRenderer[] _allRenderers;
        Color[] _originalColors;
        Coroutine _flashCo;
    
        // 醫뚯슦 諛⑺뼢 (-1: ?쇱そ, 1: ?ㅻⅨ履?
        int _facing = -1;
        float _baseSkinScaleX = 1f;
        float _baseSkinScaleY = 1f;
        float _baseSkinScaleZ = 1f;
    
        // 湲곕낯 ?뚰똿 ?쒖꽌 (?쇱そ??蹂닿퀬 ?덈뒗 ?곹깭 湲곗?)
        int _leftArmOrder;
        int _rightArmOrder;
        int _leftLegOrder;
        int _rightLegOrder;
        int _rightHandItemOrder;
    
        // 蹂댄뻾 ?좊땲硫붿씠?섏슜
        float _walkAnimPhase = 0f;
        Quaternion _leftArmBaseRot;
        Quaternion _rightArmBaseRot;
        Quaternion _leftLegBaseRot;
        Quaternion _rightLegBaseRot;
    
        // ?좎껜 ?곹깭(?대?)
        bool _isInFluid;
        bool _isHeadSubmerged;
        bool _swimUpHeld;
    
        // ?듭궗 ??
        float _drownTickTimer = 0f;
    
        // Fluid Overlap (Trigger ?ы븿)
        ContactFilter2D _fluidFilter;
        readonly List<Collider2D> _fluidHits = new List<Collider2D>(8);
    
        // 臾쇱냽?먯꽌??以묐젰 ?꾧린??
        float _defaultGravityScale;
    }
}
