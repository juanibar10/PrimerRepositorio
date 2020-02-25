using UnityEngine;

public enum WeaponShootType
{
    Manual,
    Automatic,
    Charge,
}

[System.Serializable]
public struct CrosshairData
{
    public Sprite crosshairSprite;
    public int crosshairSize;
    public Color crosshairColor;
}

[RequireComponent(typeof(AudioSource))]
public class WeaponController : MonoBehaviour
{
    [Header("Information")]
    public string weaponName;
    public Sprite weaponIcon;

    public CrosshairData crosshairDataDefault;
    //Data for the crosshair when targeting an enemy
    public CrosshairData crosshairDataTargetInSight;

    [Header("Internal References")]
    public GameObject weaponRoot;
    public Transform weaponMuzzle;

    [Header("Shoot Parameters")]
    public WeaponShootType shootType;
    public ProjectileBase projectilePrefab;
    public float delayBetweenShots = 0.5f;
    public float bulletSpreadAngle = 1f;
    public int bulletsPerShot = 1;
    [Range(0f, 2f)]
    public float recoilForce = 1;
    [Range(0f, 1f)]
    public float aimZoomRatio = 1f;
    public Vector3 aimOffset;

    [Header("Ammo Parameters")]
    //Amount of ammo reloaded per second
    public float ammoReloadRate = 1f;
    //Delay after the last shot before starting to reload
    public float ammoReloadDelay = 2f;
    //Maximum amount of ammo in the gun charger
    public float maxChargerAmmo = 8;
    //Maximum amount of ammo in the gun 
    public float maxAmmo = 68;

    [Header("Charging parameters (charging weapons only)")]
    public float maxChargeDuration = 2f;
    public float ammoUsedOnStartCharge = 1f;
    public float ammoUsageRateWhileCharging = 1f;

    [Header("Audio & Visual")]
    public GameObject muzzleFlashPrefab;
    public AudioClip shootSFX;
    public AudioClip changeWeaponSFX;

    public float currentAmmoInCharger;
    public float currentAmmoInWeapon;
    float lastTimeShot = Mathf.NegativeInfinity;
    float timeBeginCharge;
    Vector3 lastMuzzlePosition;

    [Header("Private")]
    public GameObject owner;
    public GameObject sourcePrefab;
    public bool isCharging;
    public float currentAmmoRatio;
    public bool isWeaponActive;
    public bool isCooling;
    public float currentCharge;
    public Vector3 muzzleWorldVelocity;
    public float GetAmmoNeededToShoot() 
    {
        if (shootType != WeaponShootType.Charge)
            return 1 / maxChargerAmmo;
        else
            return ammoUsedOnStartCharge / maxChargerAmmo;
    }

    AudioSource shootAudioSource;

    void Awake()
    {
        currentAmmoInCharger = maxChargerAmmo;
        currentAmmoInWeapon = maxAmmo;
        lastMuzzlePosition = weaponMuzzle.position;

        shootAudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        UpdateAmmo();
        UpdateCharge();

        if (Time.deltaTime > 0)
        {
            muzzleWorldVelocity = (weaponMuzzle.position - lastMuzzlePosition) / Time.deltaTime;
            lastMuzzlePosition = weaponMuzzle.position;
        }

        Reload();
    }

    public void Reload()
    {
        if (currentAmmoInCharger != maxChargerAmmo && Input.GetButtonDown("Reload") && currentAmmoInCharger != 0)
        {
            //hacer que sea evento de animacion
            OnReload();
        }
        else if (currentAmmoInCharger == 0)
        {
            //hacer que sea evento de animacion
            OnReload();
        }
    }

    public void OnReload()
    {
        if ((currentAmmoInWeapon - (maxChargerAmmo - currentAmmoInCharger)) >= 0)
        {
            currentAmmoInWeapon -= maxChargerAmmo - currentAmmoInCharger;
            currentAmmoInCharger = maxChargerAmmo;
        }
        else if (currentAmmoInWeapon != 0)
        {
            currentAmmoInCharger += currentAmmoInWeapon;
            currentAmmoInWeapon = 0;
        }
        else if (currentAmmoInWeapon == 0)
            return;
    }

    void UpdateAmmo()
    {
        if (maxChargerAmmo == Mathf.Infinity)
        {
            currentAmmoRatio = 1f;
        }
        else
        {
            currentAmmoRatio = currentAmmoInCharger / maxChargerAmmo;
        }
    }

    void UpdateCharge()
    {
        if (isCharging)
        {
            if (currentCharge < 1f)
            {
                float chargeLeft = 1f - currentCharge;

                // Calculate how much charge ratio to add this frame
                float chargeAdded = 0f;
                if (maxChargeDuration <= 0f)
                {
                    chargeAdded = chargeLeft;
                }
                chargeAdded = (1f / maxChargeDuration) * Time.deltaTime;
                chargeAdded = Mathf.Clamp(chargeAdded, 0f, chargeLeft);

                // See if we can actually add this charge
                float ammoThisChargeWouldRequire = chargeAdded * ammoUsageRateWhileCharging;
                //if (ammoThisChargeWouldRequire <= m_CurrentAmmo)
                {
                    // Use ammo based on charge added
                    UseAmmo(ammoThisChargeWouldRequire);

                    // set current charge ratio
                    currentCharge = Mathf.Clamp01(currentCharge + chargeAdded);
                }
            }
        }
    }

    public void ShowWeapon(bool show)
    {
        weaponRoot.SetActive(show);

        if (show && changeWeaponSFX)
        {
            shootAudioSource.PlayOneShot(changeWeaponSFX);
        }

        isWeaponActive = show;
    }

    public void UseAmmo(float amount)
    {
        currentAmmoInCharger = Mathf.Clamp(currentAmmoInCharger - amount, 0f, maxChargerAmmo);
        lastTimeShot = Time.time;
    }

    public bool HandleShootInputs(bool inputDown, bool inputHeld, bool inputUp)
    {
        switch (shootType)
        {
            case WeaponShootType.Manual:
                if (inputDown)
                {
                    return TryShoot();
                }
                return false;

            case WeaponShootType.Automatic:
                if (inputHeld)
                {
                    return TryShoot();
                }
                return false;

            case WeaponShootType.Charge:
                if (inputHeld)
                {
                    TryBeginCharge();
                }
                if (inputUp)
                {
                    return TryReleaseCharge();
                }
                return false;

            default:
                return false;
        }
    }

    bool TryShoot()
    {
        if (currentAmmoInCharger >= 1f
            && lastTimeShot + delayBetweenShots < Time.time)
        {
            HandleShoot();
            currentAmmoInCharger -= 1;

            return true;
        }

        return false;
    }

    bool TryBeginCharge()
    {
        if (!isCharging
            && currentAmmoInCharger >= ammoUsedOnStartCharge
            && lastTimeShot + delayBetweenShots < Time.time)
        {
            UseAmmo(ammoUsedOnStartCharge);
            isCharging = true;

            return true;
        }

        return false;
    }

    bool TryReleaseCharge()
    {
        if (isCharging)
        {
            HandleShoot();

            currentCharge = 0f;
            isCharging = false;

            return true;
        }
        return false;
    }

    void HandleShoot()
    {
        // spawn all bullets with random direction
        for (int i = 0; i < bulletsPerShot; i++)
        {
            Vector3 shotDirection = GetShotDirectionWithinSpread(weaponMuzzle);
            ProjectileBase newProjectile = Instantiate(projectilePrefab, weaponMuzzle.position, Quaternion.LookRotation(shotDirection));
            newProjectile.Shoot(this);
        }

        // muzzle flash
        if (muzzleFlashPrefab != null)
        {
            GameObject muzzleFlashInstance = Instantiate(muzzleFlashPrefab, weaponMuzzle.position, weaponMuzzle.rotation, weaponMuzzle.transform);
            Destroy(muzzleFlashInstance, 2f);
        }

        lastTimeShot = Time.time;

        // play shoot SFX
        if (shootSFX)
        {
            shootAudioSource.PlayOneShot(shootSFX);
        }
    }

    public Vector3 GetShotDirectionWithinSpread(Transform shootTransform)
    {
        float spreadAngleRatio = bulletSpreadAngle / 180f;
        Vector3 spreadWorldDirection = Vector3.Slerp(shootTransform.forward, UnityEngine.Random.insideUnitSphere, spreadAngleRatio);

        return spreadWorldDirection;
    }
}
