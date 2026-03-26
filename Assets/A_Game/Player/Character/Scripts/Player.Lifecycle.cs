


using UnityEngine;
using Game.Core;

namespace Game.Player
{
    
    public partial class Player
    {
        
        void Awake()
        {
            Inventory = new InventoryData(InventoryCapacity);
    
            rb = rb != null ? rb : GetComponent<Rigidbody2D>();
            groundProbe = groundProbe != null ? groundProbe : GetComponentInChildren<GroundProbe>(true);
            fluidProbe = fluidProbe != null ? fluidProbe : GetComponentInChildren<FluidProbe>(true);
            pickupSensor = pickupSensor != null ? pickupSensor : GetComponentInChildren<PickupSensor>(true);
            platformDropThroughService = platformDropThroughService != null ? platformDropThroughService : GetComponentInChildren<PlatformDropThroughService>(true);

            _defaultGravityScale = rb.gravityScale;
    
            if (playerPhysicsCollider == null)
                Debug.LogError("[Player] playerPhysicsCollider is not assigned. Assign the non-trigger collider used for physics.");
    
            var s = skinRoot.localScale;
            _baseSkinScaleX = Mathf.Abs(s.x);
            _baseSkinScaleY = s.y;
            _baseSkinScaleZ = s.z;
    
            _leftArmOrder = leftArmRenderer.sortingOrder;
            _rightArmOrder = rightArmRenderer.sortingOrder;
            _leftLegOrder = leftLegRenderer.sortingOrder;
            _rightLegOrder = rightLegRenderer.sortingOrder;
            _rightHandItemOrder = rightHandItemRenderer.sortingOrder;
    
            _allRenderers = new SpriteRenderer[]
            {
                bodyRenderer,
                leftArmRenderer,
                rightArmRenderer,
                leftLegRenderer,
                rightLegRenderer
            };
    
            _originalColors = new Color[_allRenderers.Length];
            for (int i = 0; i < _allRenderers.Length; i++)
                _originalColors[i] = _allRenderers[i].color;
    
            _leftArmBaseRot = leftArmRenderer.transform.localRotation;
            _rightArmBaseRot = rightArmRenderer.transform.localRotation;
            _leftLegBaseRot = leftLegRenderer.transform.localRotation;
            _rightLegBaseRot = rightLegRenderer.transform.localRotation;
    
            if (groundProbe == null)
                Debug.LogError("[Player] groundProbe is not assigned. Assign a GroundProbe component.");

            if (fluidProbe == null)
                Debug.LogError("[Player] fluidProbe is not assigned. Assign a FluidProbe component.");

            if (pickupSensor != null)
                pickupSensor.Bind(this);
            else
                Debug.LogWarning("[Player] pickupSensor is not assigned. Dropped item pickup will not work until a PickupSensor is wired.");

            if (platformDropThroughService == null)
                Debug.LogWarning("[Player] platformDropThroughService is not assigned. Platform drop-through will not work until a PlatformDropThroughService is wired.");
    
            ApplyFacingAndSorting();
            InitHeartsUI();
        }
    
        
        void Update()
        {
            _moveInput = Input.GetAxisRaw("Horizontal");
    
            if (_moveInput > 0.01f) SetFacing(1);
            
            else if (_moveInput < -0.01f) SetFacing(-1);
    
            _isGrounded = groundProbe != null && groundProbe.IsGrounded;

            if (fluidProbe != null)
            {
                fluidProbe.Refresh();
                _isInFluid = fluidProbe.IsInFluid;
                _isHeadSubmerged = fluidProbe.IsHeadSubmerged;
            }
            else
            {
                _isInFluid = false;
                _isHeadSubmerged = false;
            }
    
            rb.gravityScale = _isInFluid ? 0f : _defaultGravityScale;
    
            
            
            
            
            if (!_isInFluid && _isGrounded && Input.GetKeyDown(KeyCode.S))
            {
                platformDropThroughService?.TryDropThrough();
            }
    
            bool jumpDown = Input.GetButtonDown("Jump");
            bool jumpHeld = Input.GetButton("Jump");
    
            if (_isInFluid)
            {
                _jumpRequested = false;
                _swimUpHeld = jumpHeld;
            }
            else
            {
                _swimUpHeld = false;
    
                if (jumpDown && _isGrounded && stamina >= staminaJumpCost)
                {
                    _jumpRequested = true;
                    stamina -= staminaJumpCost;
                }
    
                
                
                if (jumpHeld && _isGrounded && !_wasGrounded && rb.velocity.y <= 0.01f && stamina >= staminaJumpCost)
                {
                    _jumpRequested = true;
                    stamina -= staminaJumpCost;
                }
            }
    
            if (_wasGrounded && !_isGrounded)
            {
                _isFalling = true;
                _fallStartY = transform.position.y;
                currentFallDistance = 0f;
            }
    
            if (_isFalling && !_isGrounded)
            {
                float diff = _fallStartY - transform.position.y;
                if (diff > currentFallDistance)
                    currentFallDistance = diff;
            }
    
            if (!_wasGrounded && _isGrounded && _isFalling)
            {
                lastFallDistance = currentFallDistance;
                _isFalling = false;
    
                int fallBlocks = Mathf.FloorToInt(lastFallDistance);
                int over = Mathf.Max(0, fallBlocks - 4);
                int fallDamage = over * 2;
    
                if (fallDamage > 0)
                    TakeDamage(fallDamage);
            }
    
            _wasGrounded = _isGrounded;
    
            UpdateWalkAnimation();
    
            float dt = Time.deltaTime;
    
            stamina += staminaRegenPerSecond * dt;
    
            bool isMovingHoriz = Mathf.Abs(_moveInput) > 0.01f;
            if (isMovingHoriz)
                stamina -= staminaMoveCostPerSecond * dt;
    
            stamina = Mathf.Clamp(stamina, 0f, 100f);
    
            if (_isHeadSubmerged) oxygen -= oxygenDrainPerSecond * dt;
            else oxygen += oxygenRecoverPerSecond * dt;
    
            oxygen = Mathf.Clamp(oxygen, 0f, 100f);
    
            if (oxygen <= 0f && _isHeadSubmerged)
            {
                _drownTickTimer -= dt;
                if (_drownTickTimer <= 0f)
                {
                    TakeDamage(drownDamage);
                    _drownTickTimer = drownDamageInterval;
                }
            }
            else
            {
                _drownTickTimer = 0f;
            }
    
            if (_attackCooldownTimer > 0f)
                _attackCooldownTimer -= dt;
    
            UpdateSurvivalUI();
            UpdateHeartsUI();
        }
    
        
        void FixedUpdate()
        {
            float fdt = Time.fixedDeltaTime;
            Vector2 v = rb.velocity;
    
            if (_isInFluid)
            {
                float targetX = _moveInput * moveSpeed * fluidMoveSpeedMultiplier;
                v.x = Mathf.Lerp(v.x, targetX, 1f - Mathf.Exp(-fluidHorizontalDamping * fdt));
    
                if (_swimUpHeld)
                {
                    float targetY = maxSwimUpSpeed;
                    v.y = Mathf.Lerp(v.y, targetY, 1f - Mathf.Exp(-fluidVerticalDamping * fdt));
                }
                else
                {
                    float targetY = -Mathf.Abs(fluidSinkSpeed);
                    v.y = Mathf.Lerp(v.y, targetY, 1f - Mathf.Exp(-fluidVerticalDamping * fdt));
                }
    
                _jumpRequested = false;
            }
            else
            {
                v.x = _moveInput * moveSpeed;
    
                if (_jumpRequested)
                {
                    _jumpRequested = false;
                    v.y = jumpForce;
                }
            }
    
            rb.velocity = v;
        }
    }
}
