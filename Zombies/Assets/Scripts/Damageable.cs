using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Damageable : MonoBehaviour
{
    public float damageMultiplier = 1f;
    [Range(0, 1)]
    public float sensibilityToSelfDamage = 0.5f;

    public Health health;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (!health)
            health = GetComponentInParent<Health>();
    }

    public void InflictDamage(float damage, bool isExplosionDamage, GameObject damageSource)
    {
        if (health)
        {
            float totalDamage = damage;

            if (!isExplosionDamage)
            {
                totalDamage *= damageMultiplier;
            }

            if (health.gameObject == damageSource)
            {
                totalDamage *= sensibilityToSelfDamage;
            }
            
            health.TakeDamage(totalDamage, damageSource);
        }
    }
}
