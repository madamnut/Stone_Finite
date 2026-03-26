


using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Game.Data;
using Game.World;
using Game.UI;
using Game.Core;
using Game.Support;

namespace Game.Player
{
    public partial class InteractionController : MonoBehaviour
    {
        public enum GameState { Ingame, Inpanel, Inmenu }
        enum LayerMode { Solid, BG, Utility }
    

        const string LOG_MB = "[MBUILD]";
    
        [Header("UI")]
        public GameObject inventoryPanel;
        public GameObject pauseMenuRoot;
    
        [Header("Pause Buttons")]
        public Button resumeButton;
        public Button exitButton;
    
        [Header("Key Settings")]
        public KeyCode toggleInventoryKey = KeyCode.E;
        public KeyCode toggleBreakModeKey = KeyCode.V;   
        public KeyCode toggleUtilityModeKey = KeyCode.T; 
    
        [Header("Player/Hotbar/Cursor")]
        public Player player;
        public Hotbar hotbar;
        public ItemSlot cursorSlot;
    
        [Header("Cursor Textures (World)")]
        public Texture2D breakCursorTex;
        public Texture2D combatCursorTex;
        public Texture2D utilityCursorTex;
        public Vector2 breakHotspot = new Vector2(7, 6);
        public Vector2 combatHotspot = new Vector2(5, 4);
        public Vector2 utilityHotspot = new Vector2(7, 6);
    
        [Header("World References")]
        public WorldManager worldManager;
        public MultiblockManager multiblockManager;
        public GearNetworkManager gearNetworkManager;
        public Camera worldCamera;
        public int cellSize = 1;
    
        [Header("Highlight Sprites")]
        public Sprite HighLight_Solid;
        public Sprite HighLight_Solid_CAN;
        public Sprite HighLight_Solid_CANNOT;
        public Sprite HighLight_BG;
        public Sprite HighLight_BG_CAN;
        public Sprite HighLight_BG_CANNOT;
    
        [Header("Highlight Sprites (Utility)")]
        public Sprite HighLight_Utility;
        public Sprite HighLight_Utility_CAN;
        public Sprite HighLight_Utility_CANNOT;
    
        [Header("Highlight Pulse")]
        [Range(0.8f, 1.0f)] public float minScale = 0.92f;
        [Range(1.0f, 1.2f)] public float maxScale = 1.08f;
        public float period = 1f;
    
        [Header("Libraries")]
        public RecipeLibrary recipeLibrary;
        public ItemLibrary itemLibrary;
        public CorpseLibrary corpseLibrary;
        public CellLibrary cellLibrary;
        public UtilityLibrary utilityLibrary;
    
        [Header("UI Prefabs")]
        public GameObject handcraftModule;
    
        GameObject _moduleInstance;
        public GameObject CurrentModuleInstance => _moduleInstance;
    
        [Header("Audio")]
        public AudioManager sound;
    
        [Header("Corpse Hover")]
        public CorpseHoverQueryService corpseHoverQueryService;
    
        [Header("Melee Attack Parts")]
        public Transform meleeRoot;
        public Transform meleeAngle;
        public Transform meleeOffset;
        public SpriteRenderer meleeSprite;
        public CombatHitSensor combatHitSensor;
    
        bool _attackActive = false;
        readonly HashSet<Mob> _hitMobsThisAttack = new HashSet<Mob>();
        int _currentAttackDamage = 1;
    
        GameState _state = GameState.Ingame;
        LayerMode _layerMode = LayerMode.Solid;
        LayerMode _prevLayerModeBeforeUtility = LayerMode.Solid;
    
        GameObject _hlGO;
        SpriteRenderer _hlSR;
        float _timer;
        int _hotbarScope = 0;
    
        bool _combatMode = false;
    
        Coroutine _attackCo;
        Corpse _hoverCorpse;
    
        bool _beltPending = false;
        Vector2Int _beltStartCell;
        string _beltPendingKind = null;
        int _beltPendingScope = -1;
        ItemData _beltPendingHeldRef = null;
    
        ushort _utilityOccupiedId = 0;
    
        
        void Awake()
        {
            inventoryPanel.SetActive(true);
            inventoryPanel.SetActive(false);
            pauseMenuRoot.SetActive(false);
            Time.timeScale = 1f;
    
            _hlGO = new GameObject("CellHighlight");
            _hlSR = _hlGO.AddComponent<SpriteRenderer>();
            _hlSR.sprite = HighLight_Solid;
            _hlSR.sortingOrder = 1000;
            _hlGO.SetActive(false);
    
            hotbar.SetScope(_hotbarScope);
    
            recipeLibrary.itemLibrary = itemLibrary;
    
            resumeButton.onClick.AddListener(OnClickResume);
            exitButton.onClick.AddListener(OnClickQuitToLobby);
    
            if (cellLibrary != null)
            {
                if (cellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out ushort occ))
                    _utilityOccupiedId = occ;
            }
    
            if (utilityLibrary == null)
                utilityLibrary = FindObjectOfType<UtilityLibrary>();
    
            InitializeBuildServices();
            InitializeMultiblockUiBridge();
            ApplyWorldCursor();
            corpseHoverQueryService = corpseHoverQueryService != null ? corpseHoverQueryService : GetComponentInChildren<CorpseHoverQueryService>(true);
            if (corpseHoverQueryService == null)
                Debug.LogWarning("[InteractionController] corpseHoverQueryService is not assigned. Corpse hover highlighting will not work until a CorpseHoverQueryService is wired.");
            if (meleeRoot != null)
                combatHitSensor = combatHitSensor != null ? combatHitSensor : meleeRoot.GetComponentInChildren<CombatHitSensor>(true);
            if (combatHitSensor != null)
                combatHitSensor.Bind(this);
            else
                Debug.LogWarning("[InteractionController] combatHitSensor is not assigned. Melee hit detection will not work until a CombatHitSensor is wired.");
            if (meleeRoot != null)
                meleeRoot.gameObject.SetActive(false);
        }
    
        
        void Update()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (scroll > 0.01f || scroll < -0.01f)
            {
                int prev = _hotbarScope;
                _hotbarScope = (scroll > 0f) ? (_hotbarScope + 9) % 10 : (_hotbarScope + 1) % 10;
                if (_hotbarScope != prev) CancelBeltPlacement();
                hotbar.SetScope(_hotbarScope);
                RefreshHeldHandSprite();
            }
    
            if (_state == GameState.Ingame)
            {
                int prevScope = _hotbarScope;
    
                if      (Input.GetKeyDown(KeyCode.Alpha1)) _hotbarScope = 0;
                
                else if (Input.GetKeyDown(KeyCode.Alpha2)) _hotbarScope = 1;
                
                else if (Input.GetKeyDown(KeyCode.Alpha3)) _hotbarScope = 2;
                
                else if (Input.GetKeyDown(KeyCode.Alpha4)) _hotbarScope = 3;
                
                else if (Input.GetKeyDown(KeyCode.Alpha5)) _hotbarScope = 4;
                
                else if (Input.GetKeyDown(KeyCode.Alpha6)) _hotbarScope = 5;
                
                else if (Input.GetKeyDown(KeyCode.Alpha7)) _hotbarScope = 6;
                
                else if (Input.GetKeyDown(KeyCode.Alpha8)) _hotbarScope = 7;
                
                else if (Input.GetKeyDown(KeyCode.Alpha9)) _hotbarScope = 8;
                
                else if (Input.GetKeyDown(KeyCode.Alpha0)) _hotbarScope = 9;
    
                if (_hotbarScope != prevScope)
                {
                    CancelBeltPlacement();
                    hotbar.SetScope(_hotbarScope);
                    RefreshHeldHandSprite();
                }
            }
    
            if (_beltPending)
            {
                if (_hotbarScope != _beltPendingScope || GetHeldItem() != _beltPendingHeldRef)
                    CancelBeltPlacement();
            }
    
            if (_state == GameState.Ingame && Input.GetKeyDown(toggleUtilityModeKey))
            {
                CancelBeltPlacement();
    
                if (_layerMode != LayerMode.Utility)
                {
                    _prevLayerModeBeforeUtility = _layerMode;
                    _layerMode = LayerMode.Utility;
                }
                else
                {
                    _layerMode = _prevLayerModeBeforeUtility;
                }
    
                ApplyHighlightBaseSprite();
                ApplyWorldCursor();
            }
    
            ItemData scopeHeld = GetHeldItem();
            bool hasWeapon = (scopeHeld != null && scopeHeld.HasTag("Weapon"));
    
            if (_layerMode != LayerMode.Utility)
            {
                if (hasWeapon && !_combatMode)
                {
                    _combatMode = true;
                    ApplyWorldCursor();
                }
                
                else if (!hasWeapon && _combatMode)
                {
                    _combatMode = false;
                    ApplyWorldCursor();
                }
            }
    
            bool invDown = Input.GetKeyDown(toggleInventoryKey);
            bool escDown = Input.GetKeyDown(KeyCode.Escape);
    
            if (invDown)
            {
                CancelBeltPlacement();
    
                if (_state == GameState.Ingame) OpenModule(handcraftModule);
                
                else if (_state == GameState.Inpanel) CloseInventoryPanelToIngame();
            }
    
            if (escDown)
            {
                CancelBeltPlacement();
    
                if (_state == GameState.Inpanel) CloseInventoryPanelToIngame();
                
                else if (_state == GameState.Inmenu)
                {
                    _state = GameState.Ingame;
                    pauseMenuRoot.SetActive(false);
                    Time.timeScale = 1f;
                }
                else
                {
                    _state = GameState.Inmenu;
                    pauseMenuRoot.SetActive(true);
                    Time.timeScale = 0f;
                    HideWorldHoverState();
                }
            }
    
            if (Input.GetKeyDown(toggleBreakModeKey) && _state == GameState.Ingame && _layerMode != LayerMode.Utility)
            {
                _layerMode = (_layerMode == LayerMode.Solid) ? LayerMode.BG : LayerMode.Solid;
                ApplyHighlightBaseSprite();
            }
    
            if (_state != GameState.Ingame)
            {
                HideWorldHoverState();
                return;
            }
    
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HideWorldHoverState();
                return;
            }
    
            UpdateHighlight();
            UpdateCorpseHoverState();
    
            if (Input.GetMouseButtonDown(0)) HandleLeftClick();
            if (Input.GetMouseButtonDown(1)) HandleRightClick();
        }
    
        
        void ApplyWorldCursor()
        {
            if (_layerMode == LayerMode.Utility)
            {
                UnityEngine.Cursor.SetCursor(utilityCursorTex, utilityHotspot, CursorMode.Auto);
                return;
            }
    
            if (_combatMode)
                UnityEngine.Cursor.SetCursor(combatCursorTex, combatHotspot, CursorMode.Auto);
            else
                UnityEngine.Cursor.SetCursor(breakCursorTex, breakHotspot, CursorMode.Auto);
        }
    
        
        void ApplyHighlightBaseSprite()
        {
            if (_hlSR == null) return;
    
            if (_layerMode == LayerMode.Utility)
            {
                _hlSR.sprite = (HighLight_Utility != null) ? HighLight_Utility : HighLight_Solid;
                return;
            }
    
            _hlSR.sprite = (_layerMode == LayerMode.Solid) ? HighLight_Solid : HighLight_BG;
        }
    
        
        void CancelBeltPlacement()
        {
            _beltPending = false;
            _beltStartCell = default;
            _beltPendingKind = null;
            _beltPendingScope = -1;
            _beltPendingHeldRef = null;
        }
    }
}
