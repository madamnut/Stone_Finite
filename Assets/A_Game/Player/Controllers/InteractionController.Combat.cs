using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    
    public partial class InteractionController
    {
        void TryWeaponAttack()
        {
            if (_attackCo != null)
                return;
    
            var items = player.Inventory.items;
            if (_hotbarScope < 0 || _hotbarScope >= items.Count)
                return;
    
            var held = items[_hotbarScope];
            if (held == null || held.Count <= 0)
                return;
    
            if (held.WeaponActions == null || held.WeaponActions.Count == 0)
                return;
    
            string actionName = null;
            Dictionary<string, object> paramDict = null;
    
            foreach (var kv in held.WeaponActions)
            {
                actionName = kv.Key;
                paramDict = kv.Value;
                break;
            }
    
            if (string.IsNullOrEmpty(actionName))
                return;
    
            if (paramDict == null)
                paramDict = new Dictionary<string, object>();
    
            float staminaCost = 0f;
            float cooldown = 0f;
            float damage = 1f;
    
            if (paramDict.TryGetValue("staminaCost", out var scObj) && scObj != null)
            {
                if (scObj is float f) staminaCost = f;
                else if (scObj is double d) staminaCost = (float)d;
                else if (scObj is int i) staminaCost = i;
                else if (scObj is long l) staminaCost = l;
                else if (float.TryParse(scObj.ToString(), out var tmp)) staminaCost = tmp;
            }
    
            if (paramDict.TryGetValue("cooldown", out var cdObj) && cdObj != null)
            {
                if (cdObj is float f) cooldown = f;
                else if (cdObj is double d) cooldown = (float)d;
                else if (cdObj is int i) cooldown = i;
                else if (cdObj is long l) cooldown = l;
                else if (float.TryParse(cdObj.ToString(), out var tmp)) cooldown = tmp;
            }
    
            if (paramDict.TryGetValue("damage", out var dmgObj) && dmgObj != null)
            {
                if (dmgObj is float f) damage = f;
                else if (dmgObj is double d) damage = (float)d;
                else if (dmgObj is int i) damage = i;
                else if (dmgObj is long l) damage = l;
                else if (float.TryParse(dmgObj.ToString(), out var tmp)) damage = tmp;
            }
    
            if (!player.TryConsumeStaminaForAttack(staminaCost))
                return;
    
            player.StartAttackCooldown(cooldown);
    
            Vector3 mouseWorld3 = worldCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 mouseWorld = new Vector2(mouseWorld3.x, mouseWorld3.y);
            Vector2 origin = meleeAngle.position;
    
            Vector2 dir = mouseWorld - origin;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;
    
            float angleFromUp = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            bool isLeftSide = (mouseWorld.x < origin.x);
    
            meleeRoot.gameObject.SetActive(true);
    
            meleeSprite.enabled = true;
            meleeSprite.sprite = held.Icon;
    
            meleeAngle.rotation = Quaternion.Euler(0f, 0f, angleFromUp);
    
            _currentAttackDamage = Mathf.Max(1, Mathf.RoundToInt(damage));
            _attackActive = true;
            _hitMobsThisAttack.Clear();
    
            if (actionName == "Swing")
            {
                sound.PlayWeaponSwing();
                _attackCo = StartCoroutine(CoSwing(angleFromUp, isLeftSide));
            }
            else if (actionName == "Thrust")
            {
                sound.PlayWeaponThrust();
                _attackCo = StartCoroutine(CoThrust(angleFromUp));
            }
        }
    
        IEnumerator CoSwing(float centerAngle, bool isLeftSide)
        {
            float duration = 0.25f;
            float halfRange = 60f;
    
            float startAngle;
            float endAngle;
    
            if (isLeftSide)
            {
                startAngle = centerAngle - halfRange;
                endAngle = centerAngle + halfRange;
            }
            else
            {
                startAngle = centerAngle + halfRange;
                endAngle = centerAngle - halfRange;
            }
    
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float ang = Mathf.Lerp(startAngle, endAngle, u);
                meleeAngle.rotation = Quaternion.Euler(0f, 0f, ang);
                yield return null;
            }
    
            meleeAngle.rotation = Quaternion.Euler(0f, 0f, centerAngle);
    
            meleeRoot.gameObject.SetActive(false);
    
            _attackActive = false;
            _hitMobsThisAttack.Clear();
    
            _attackCo = null;
        }
    
        IEnumerator CoThrust(float centerAngle)
        {
            meleeAngle.rotation = Quaternion.Euler(0f, 0f, centerAngle);
    
            float duration = 0.5f;
            float startY = -0.5f;
            float endY = 0.5f;
    
            Vector3 basePos = meleeOffset.localPosition;
            float baseX = basePos.x;
            float baseZ = basePos.z;
    
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
    
                float y;
                if (u < 0.5f)
                {
                    float k = u * 2f;
                    y = Mathf.Lerp(startY, endY, k);
                }
                else
                {
                    float k = (u - 0.5f) * 2f;
                    y = Mathf.Lerp(endY, startY, k);
                }
    
                meleeOffset.localPosition = new Vector3(baseX, y, baseZ);
                yield return null;
            }
    
            meleeOffset.localPosition = new Vector3(baseX, 0f, baseZ);
    
            meleeRoot.gameObject.SetActive(false);
    
            _attackActive = false;
            _hitMobsThisAttack.Clear();
    
            _attackCo = null;
        }
    
        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_attackActive)
                return;
    
            var mob = other.GetComponentInParent<Mob>();
            if (mob == null)
                return;
    
            if (_hitMobsThisAttack.Contains(mob))
                return;
    
            mob.TakeDamage(_currentAttackDamage);
            _hitMobsThisAttack.Add(mob);
        }
    }
}
