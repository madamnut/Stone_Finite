using System.Collections;
using UnityEngine;
using Game.UI;
using Game.Core;
using Game.Support;
using Game.World;

namespace Game.Player
{
    
    public partial class Player
    {
        internal void HandlePickupTrigger(Collider2D other)
        {
            if (!other.CompareTag("DroppedItem")) return;
    
            var drop = other.GetComponent<DroppedItem>();
            if (drop == null)
                return;

            int before = drop.ItemData.Count;
    
            int left = Inventory.AddItem(drop.ItemData);
    
            int picked = before - left;
            if (picked > 0)
                audioManager.PlayPop();
    
            if (left == 0)
                Destroy(other.gameObject);
            else
                drop.ItemData.Count = left;
        }
    
        public bool TryConsumeStaminaForAttack(float staminaCost)
        {
            if (_attackCooldownTimer > 0f)
                return false;
    
            if (stamina < staminaCost)
                return false;
    
            stamina -= staminaCost;
            if (stamina < 0f) stamina = 0f;
    
            return true;
        }
    
        public void StartAttackCooldown(float cooldown)
        {
            _attackCooldownTimer = cooldown;
        }
    
        public void TakeDamage(int damage)
        {
            if (damage <= 0) return;
    
            health -= damage;
            if (health < 0) health = 0;
            if (health > 40) health = 40;
    
            UpdateHeartsUI();
    
            audioManager.PlayPlayerTookDamage();
    
            if (_flashCo != null)
                StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(CoFlashRed());
        }
    
        IEnumerator CoFlashRed()
        {
            for (int i = 0; i < _allRenderers.Length; i++)
                _allRenderers[i].color = Color.red;
    
            yield return new WaitForSeconds(damageFlashDuration);
    
            for (int i = 0; i < _allRenderers.Length; i++)
                _allRenderers[i].color = _originalColors[i];
    
            _flashCo = null;
        }
    
        void UpdateSurvivalUI()
        {
            hungerFillImage.fillAmount = Mathf.Clamp01(hunger / 100f);
            thirstFillImage.fillAmount = Mathf.Clamp01(thirst / 100f);
            staminaFillImage.fillAmount = Mathf.Clamp01(stamina / 100f);
            oxygenFillImage.fillAmount = Mathf.Clamp01(oxygen / 100f);
        }
    
        void InitHeartsUI()
        {
            for (int i = heartRoot.childCount - 1; i >= 0; i--)
                Destroy(heartRoot.GetChild(i).gameObject);
    
            int maxHearts = 40 / 4;
            heartObjects = new Heart[maxHearts];
    
            for (int i = 0; i < maxHearts; i++)
            {
                GameObject h = Instantiate(heartPrefab, heartRoot);
                heartObjects[i] = h.GetComponent<Heart>();
            }
    
            UpdateHeartsUI();
        }
    
        void UpdateHeartsUI()
        {
            int maxHearts = heartObjects.Length;
    
            for (int i = 0; i < maxHearts; i++)
            {
                int heartStart = i * 4;
                int heartValue = health - heartStart;
                int fill = Mathf.Clamp(heartValue, 0, 4);
    
                heartObjects[i].SetHeart(heartAtlas, fill);
            }
        }
    }
}
