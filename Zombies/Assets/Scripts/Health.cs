using UnityEngine;
using UnityEngine.Events;

public class Health : MonoBehaviour
{
    public float maxHealth;
    public float criticalHealthRatio;

    public UnityAction<float, GameObject> onDamaged;
    public UnityAction<float> onHealed;
    public UnityAction onDie;

    public float currentHealth;
    public bool invencible;
    public bool canPickUp() => currentHealth < maxHealth;
    public float getRatio() => currentHealth / maxHealth;
    public bool isCritical() => getRatio() <= criticalHealthRatio;
    bool isDead;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void Heal(float healAmount)
    {
        float healthBefore = currentHealth;

        currentHealth += healAmount;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        float trueHealAmount = currentHealth - healthBefore;
        if(trueHealAmount > 0f && onHealed != null)
        {
            onHealed.Invoke(trueHealAmount);
        }
    }

    public void TakeDamage(float damage, GameObject damageSource)
    {
        if (invencible)
            return;

        float healthBefore = currentHealth;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        float trueDamageAmount = healthBefore - currentHealth;
        if(trueDamageAmount >0f && onDamaged != null)
        {
            onDamaged.Invoke(trueDamageAmount, damageSource);
        }

        HandleDeath();
    }

    public void Kill()
    {
        currentHealth = 0f;
        if(onDamaged != null)
        {
            onDamaged.Invoke(maxHealth, null);
        }
        HandleDeath();
    }

    public void HandleDeath()
    {
        if (isDead)
            return;

        if (currentHealth <= 0f)
        {
            if (onDie != null)
            {
                isDead = true;
                onDie.Invoke();
            }
        }
    }
}
