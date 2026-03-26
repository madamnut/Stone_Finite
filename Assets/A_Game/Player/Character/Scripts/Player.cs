


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using Game.UI;
using Game.Core;
using Game.Support;

namespace Game.Player
{
    
    public partial class Player : MonoBehaviour, IInventoryOwner
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;   
        [SerializeField] private float jumpForce = 7f;   
    
        [Header("Components")]
        [SerializeField] private Rigidbody2D rb;                 
        [SerializeField] private GroundProbe groundProbe;
    
        
        [Header("Platform Drop-Through")]
        [SerializeField] private Collider2D playerPhysicsCollider;
        [SerializeField] private PlatformDropThroughService platformDropThroughService;
    
        [Header("Fluid (Triggers)")]
        [SerializeField] private FluidProbe fluidProbe;
    
        [Header("Fluid Movement (Recommended Preset)")]
        [SerializeField] private float fluidMoveSpeedMultiplier = 0.5f; 
        [SerializeField] private float fluidHorizontalDamping = 10f;    
        [SerializeField] private float fluidVerticalDamping = 6f;       
        [SerializeField] private float fluidSinkSpeed = 1.5f;           
        [SerializeField] private float swimUpAcceleration = 35f;        
        [SerializeField] private float maxSwimUpSpeed = 6.5f;           
    
        [Header("Visual (Skin Root)")]
        [SerializeField] private Transform skinRoot;             
    
        [Header("Visual (Body + Limbs)")]
        [SerializeField] private SpriteRenderer bodyRenderer;    
        [SerializeField] private SpriteRenderer leftArmRenderer;
        [SerializeField] private SpriteRenderer rightArmRenderer;
        [SerializeField] private SpriteRenderer leftLegRenderer;
        [SerializeField] private SpriteRenderer rightLegRenderer;
    
        [Header("Visual (Right Hand Item)")]

        public SpriteRenderer rightHandItemRenderer; 
    
        [Header("Walk Animation")]
        [SerializeField] private float walkSwingSpeed = 10f;     
        [SerializeField] private float walkArmAmplitude = 20f;   
        [SerializeField] private float walkLegAmplitude = 25f;   
        [SerializeField] private float walkReturnSpeed = 10f;    
    
        [Header("Damage Flash")]
        [SerializeField] private float damageFlashDuration = 0.1f;
    
        [Header("Audio")]
        [SerializeField] private AudioManager audioManager;
        [SerializeField] private PickupSensor pickupSensor;
    
        [Header("UI (Survival Bars)")]
        [SerializeField] private Image hungerFillImage;   
        [SerializeField] private Image thirstFillImage;   
        [SerializeField] private Image staminaFillImage;  
        [SerializeField] private Image oxygenFillImage;   
    
        [Header("Hearts UI")]
        [SerializeField] private Transform heartRoot;     
        [SerializeField] private GameObject heartPrefab;  
        [SerializeField] private SpriteAtlas heartAtlas;  
    
        private Heart[] heartObjects;
    
        private float _moveInput;
        private bool _isGrounded;
        private bool _wasGrounded;
        private bool _jumpRequested;
    
        
        private bool _isFalling;
        private float _fallStartY;
        public float currentFallDistance; 
        public float lastFallDistance;    
    
        
        [Header("Survival Stats")]
        [Range(0, 40)] public int health = 40;   
        [Range(0, 100)] public int hunger = 100;
        [Range(0, 100)] public int thirst = 100;
        [Range(0, 100)] public float stamina = 100f; 
    
        [Header("Oxygen")]
        [Range(0, 100)] public float oxygen = 100f;
        [SerializeField] private float oxygenDrainPerSecond = 6f;   
        [SerializeField] private float oxygenRecoverPerSecond = 10f;  
        [SerializeField] private float drownDamageInterval = 1.0f; 
        [SerializeField] private int drownDamage = 5;    
    
        [Header("Stamina Settings")]
        [SerializeField] private float staminaRegenPerSecond = 2f;  
        [SerializeField] private float staminaMoveCostPerSecond = 4f;  
        [SerializeField] private float staminaJumpCost = 5f;  
    
        
        float _attackCooldownTimer = 0f;  
    
        
        private const int InventoryCapacity = 50;
        public InventoryData Inventory { get; private set; }
    
        
        SpriteRenderer[] _allRenderers;
        Color[] _originalColors;
        Coroutine _flashCo;
    
        
        int _facing = -1;
        float _baseSkinScaleX = 1f;
        float _baseSkinScaleY = 1f;
        float _baseSkinScaleZ = 1f;
    
        
        int _leftArmOrder;
        int _rightArmOrder;
        int _leftLegOrder;
        int _rightLegOrder;
        int _rightHandItemOrder;
    
        
        float _walkAnimPhase = 0f;
        Quaternion _leftArmBaseRot;
        Quaternion _rightArmBaseRot;
        Quaternion _leftLegBaseRot;
        Quaternion _rightLegBaseRot;
    
        
        bool _isInFluid;
        bool _isHeadSubmerged;
        bool _swimUpHeld;
    
        
        float _drownTickTimer = 0f;
    
        
        float _defaultGravityScale;
    }
}
