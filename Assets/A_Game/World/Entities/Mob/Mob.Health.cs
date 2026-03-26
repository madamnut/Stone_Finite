using UnityEngine;

namespace Game.World
{
public partial class Mob
{
    public virtual void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive) return;

        currentHp -= amount;
        if (currentHp < 0) currentHp = 0;

        OnDamaged(amount);

        if (currentHp <= 0)
            Die();
    }

    public virtual void Heal(int amount)
    {
        if (amount <= 0) return;
        if (!IsAlive) return;

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
}
}
