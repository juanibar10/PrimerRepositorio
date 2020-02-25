using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerWeaponsManager : MonoBehaviour
{
    public enum WeaponSwitchState
    {
        Up,
        Down,
        PutDownPrevious,
        PutUpNew,
    }

    public List<WeaponController> startingWeapons = new List<WeaponController>();

    [Header("References")]
    public Camera weaponCamera;
    public Transform weaponParentSocket;
    public Transform defaultWeaponPosition;
    public Transform aimingWeaponPosition;
    public Transform downWeaponPosition;

    [Header("Weapon Bob")]
    public float bobFrequency = 10f;
    public float bobSharpness = 10f;
    public float defaultBobAmount = 0.05f;
    public float aimingBobAmount = 0.02f;

    [Header("Weapon Recoil")]
    public float recoilSharpness = 50f;
    public float maxRecoilDistance = 0.5f;
    public float recoilRestitutionSharpness = 10f;

    [Header("Misc")]
    public float aimingAnimationSpeed = 10f;
    public float defaultFOV = 60f;
    public float weaponFOVMultiplier = 1f;
    public float weaponSwitchDelay = 1f;
    public LayerMask FPSWeaponLayer;

    public bool isAiming;
    public bool isPointingAtEnemy;
    public int activeWeaponIndex;

    public UnityAction<WeaponController> onSwitchedToWeapon;
    public UnityAction<WeaponController, int> onAddedWeapon;
    public UnityAction<WeaponController, int> onRemovedWeapon;

    WeaponController[] weaponSlots = new WeaponController[9]; // 9 available weapon slots
    PlayerInputHandler inputHandler;
    PlayerCharacterController player;
    float weaponBobFactor;
    Vector3 lastCharacterPosition;
    Vector3 weaponMainLocalPosition;
    Vector3 weaponBobLocalPosition;
    Vector3 weaponRecoilLocalPosition;
    Vector3 accumulatedRecoil;
    float timeStartedWeaponSwitch;
    WeaponSwitchState weaponSwitchState;
    int weaponSwitchNewWeaponIndex;

    private void Start()
    {
        activeWeaponIndex = -1;
        weaponSwitchState = WeaponSwitchState.Down;

        inputHandler = GetComponent<PlayerInputHandler>();

        player = GetComponent<PlayerCharacterController>();

        SetFOV(defaultFOV);

        onSwitchedToWeapon += OnWeaponSwitched;

        // Add starting weapons
        foreach (var weapon in startingWeapons)
        {
            AddWeapon(weapon);
        }
        SwitchWeapon(true);
    }

    private void Update()
    {
        // shoot handling
        WeaponController activeWeapon = GetActiveWeapon();

        if (activeWeapon && weaponSwitchState == WeaponSwitchState.Up)
        {
            // handle aiming down sights
            isAiming = inputHandler.GetAimInputHeld();

            // handle shooting
            bool hasFired = activeWeapon.HandleShootInputs(
                inputHandler.GetFireInputDown(),
                inputHandler.FireHeld(),
                inputHandler.GetFireInputReleased());

            // Handle accumulating recoil
            if (hasFired)
            {
                accumulatedRecoil += Vector3.back * activeWeapon.recoilForce;
                accumulatedRecoil = Vector3.ClampMagnitude(accumulatedRecoil, maxRecoilDistance);
            }
        }

        // weapon switch handling
        if (!isAiming &&
            (activeWeapon == null || !activeWeapon.isCharging) &&
            (weaponSwitchState == WeaponSwitchState.Up || weaponSwitchState == WeaponSwitchState.Down))
        {
            int switchWeaponInput = inputHandler.GetSwitchWeaponInput();
            if (switchWeaponInput != 0)
            {
                bool switchUp = switchWeaponInput > 0;
                SwitchWeapon(switchUp);
            }
            else
            {
                switchWeaponInput = inputHandler.GetSelectWeaponInput();
                if (switchWeaponInput != 0)
                {
                    if (GetWeaponAtSlotIndex(switchWeaponInput - 1) != null)
                        SwitchToWeaponIndex(switchWeaponInput - 1);
                }
            }
        }

        // Pointing at enemy handling
        isPointingAtEnemy = false;
        if (activeWeapon)
        {
            if (Physics.Raycast(weaponCamera.transform.position, weaponCamera.transform.forward, out RaycastHit hit, 1000, -1, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<EnemyController>())
                {
                    isPointingAtEnemy = true;
                }
            }
        }
    }


    // Update various animated features in LateUpdate because it needs to override the animated arm position
    private void LateUpdate()
    {
        UpdateWeaponAiming();
        UpdateWeaponBob();
        UpdateWeaponRecoil();
        UpdateWeaponSwitching();

        // Set final weapon socket position based on all the combined animation influences
        weaponParentSocket.localPosition = weaponMainLocalPosition + weaponBobLocalPosition + weaponRecoilLocalPosition;
    }

    // Sets the FOV of the main camera and the weapon camera simultaneously
    public void SetFOV(float fov)
    {
        player.playerCamera.fieldOfView = fov;
        weaponCamera.fieldOfView = fov * weaponFOVMultiplier;
    }

    // Iterate on all weapon slots to find the next valid weapon to switch to
    public void SwitchWeapon(bool ascendingOrder)
    {
        int newWeaponIndex = -1;
        int closestSlotDistance = weaponSlots.Length;
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            // If the weapon at this slot is valid, calculate its "distance" from the active slot index (either in ascending or descending order)
            // and select it if it's the closest distance yet
            if (i != activeWeaponIndex && GetWeaponAtSlotIndex(i) != null)
            {
                int distanceToActiveIndex = GetDistanceBetweenWeaponSlots(activeWeaponIndex, i, ascendingOrder);

                if (distanceToActiveIndex < closestSlotDistance)
                {
                    closestSlotDistance = distanceToActiveIndex;
                    newWeaponIndex = i;
                }
            }
        }

        // Handle switching to the new weapon index
        SwitchToWeaponIndex(newWeaponIndex);
    }

    // Switches to the given weapon index in weapon slots if the new index is a valid weapon that is different from our current one
    public void SwitchToWeaponIndex(int newWeaponIndex, bool force = false)
    {
        if (force || (newWeaponIndex != activeWeaponIndex && newWeaponIndex >= 0))
        {
            // Store data related to weapon switching animation
            weaponSwitchNewWeaponIndex = newWeaponIndex;
            timeStartedWeaponSwitch = Time.time;

            // Handle case of switching to a valid weapon for the first time (simply put it up without putting anything down first)
            if (GetActiveWeapon() == null)
            {
                weaponMainLocalPosition = downWeaponPosition.localPosition;
                weaponSwitchState = WeaponSwitchState.PutUpNew;
                activeWeaponIndex = weaponSwitchNewWeaponIndex;

                WeaponController newWeapon = GetWeaponAtSlotIndex(weaponSwitchNewWeaponIndex);
                if (onSwitchedToWeapon != null)
                {
                    onSwitchedToWeapon.Invoke(newWeapon);
                }
            }
            // otherwise, remember we are putting down our current weapon for switching to the next one
            else
            {
                weaponSwitchState = WeaponSwitchState.PutDownPrevious;
            }
        }
    }

    public bool HasWeapon(WeaponController weaponPrefab)
    {
        // Checks if we already have a weapon coming from the specified prefab
        foreach (var w in weaponSlots)
        {
            if (w != null && w.sourcePrefab == weaponPrefab.gameObject)
            {
                return true;
            }
        }

        return false;
    }

    // Updates weapon position and camera FoV for the aiming transition
    void UpdateWeaponAiming()
    {
        if (weaponSwitchState == WeaponSwitchState.Up)
        {
            WeaponController activeWeapon = GetActiveWeapon();
            if (isAiming && activeWeapon)
            {
                weaponMainLocalPosition = Vector3.Lerp(weaponMainLocalPosition, aimingWeaponPosition.localPosition + activeWeapon.aimOffset, aimingAnimationSpeed * Time.deltaTime);
                SetFOV(Mathf.Lerp(player.playerCamera.fieldOfView, activeWeapon.aimZoomRatio * defaultFOV, aimingAnimationSpeed * Time.deltaTime));
            }
            else
            {
                weaponMainLocalPosition = Vector3.Lerp(weaponMainLocalPosition, defaultWeaponPosition.localPosition, aimingAnimationSpeed * Time.deltaTime);
                SetFOV(Mathf.Lerp(player.playerCamera.fieldOfView, defaultFOV, aimingAnimationSpeed * Time.deltaTime));
            }
        }
    }

    // Updates the weapon bob animation based on character speed
    void UpdateWeaponBob()
    {
        if (Time.deltaTime > 0f)
        {
            Vector3 playerCharacterVelocity = (player.transform.position - lastCharacterPosition) / Time.deltaTime;

            // calculate a smoothed weapon bob amount based on how close to our max grounded movement velocity we are
            float characterMovementFactor = 0f;
            if (player.isGrounded)
            {
                characterMovementFactor = Mathf.Clamp01(playerCharacterVelocity.magnitude / (player.maxSpeedOnGround * player.sprintSpeedModifier));
            }
            weaponBobFactor = Mathf.Lerp(weaponBobFactor, characterMovementFactor, bobSharpness * Time.deltaTime);

            // Calculate vertical and horizontal weapon bob values based on a sine function
            float bobAmount = isAiming ? aimingBobAmount : defaultBobAmount;
            float frequency = bobFrequency;
            float hBobValue = Mathf.Sin(Time.time * frequency) * bobAmount * weaponBobFactor;
            float vBobValue = ((Mathf.Sin(Time.time * frequency * 2f) * 0.5f) + 0.5f) * bobAmount * weaponBobFactor;

            // Apply weapon bob
            weaponBobLocalPosition.x = hBobValue;
            weaponBobLocalPosition.y = Mathf.Abs(vBobValue);

            lastCharacterPosition = player.transform.position;
        }
    }

    // Updates the weapon recoil animation
    void UpdateWeaponRecoil()
    {
        // if the accumulated recoil is further away from the current position, make the current position move towards the recoil target
        if (weaponRecoilLocalPosition.z >= accumulatedRecoil.z * 0.99f)
        {
            weaponRecoilLocalPosition = Vector3.Lerp(weaponRecoilLocalPosition, accumulatedRecoil, recoilSharpness * Time.deltaTime);
        }
        // otherwise, move recoil position to make it recover towards its resting pose
        else
        {
            weaponRecoilLocalPosition = Vector3.Lerp(weaponRecoilLocalPosition, Vector3.zero, recoilRestitutionSharpness * Time.deltaTime);
            accumulatedRecoil = weaponRecoilLocalPosition;
        }
    }

    // Updates the animated transition of switching weapons
    void UpdateWeaponSwitching()
    {
        // Calculate the time ratio (0 to 1) since weapon switch was triggered
        float switchingTimeFactor = 0f;
        if (weaponSwitchDelay == 0f)
        {
            switchingTimeFactor = 1f;
        }
        else
        {
            switchingTimeFactor = Mathf.Clamp01((Time.time - timeStartedWeaponSwitch) / weaponSwitchDelay);
        }

        // Handle transiting to new switch state
        if (switchingTimeFactor >= 1f)
        {
            if (weaponSwitchState == WeaponSwitchState.PutDownPrevious)
            {
                // Deactivate old weapon
                WeaponController oldWeapon = GetWeaponAtSlotIndex(activeWeaponIndex);
                if (oldWeapon != null)
                {
                    oldWeapon.ShowWeapon(false);
                }

                activeWeaponIndex = weaponSwitchNewWeaponIndex;
                switchingTimeFactor = 0f;

                // Activate new weapon
                WeaponController newWeapon = GetWeaponAtSlotIndex(activeWeaponIndex);
                if (onSwitchedToWeapon != null)
                {
                    onSwitchedToWeapon.Invoke(newWeapon);
                }

                if (newWeapon)
                {
                    timeStartedWeaponSwitch = Time.time;
                    weaponSwitchState = WeaponSwitchState.PutUpNew;
                }
                else
                {
                    // if new weapon is null, don't follow through with putting weapon back up
                    weaponSwitchState = WeaponSwitchState.Down;
                }
            }
            else if (weaponSwitchState == WeaponSwitchState.PutUpNew)
            {
                weaponSwitchState = WeaponSwitchState.Up;
            }
        }

        // Handle moving the weapon socket position for the animated weapon switching
        if (weaponSwitchState == WeaponSwitchState.PutDownPrevious)
        {
            weaponMainLocalPosition = Vector3.Lerp(defaultWeaponPosition.localPosition, downWeaponPosition.localPosition, switchingTimeFactor);
        }
        else if (weaponSwitchState == WeaponSwitchState.PutUpNew)
        {
            weaponMainLocalPosition = Vector3.Lerp(downWeaponPosition.localPosition, defaultWeaponPosition.localPosition, switchingTimeFactor);
        }
    }

    // Adds a weapon to our inventory
    public bool AddWeapon(WeaponController weaponPrefab)
    {
        // if we already hold this weapon type (a weapon coming from the same source prefab), don't add the weapon
        if (HasWeapon(weaponPrefab))
        {
            return false;
        }

        // search our weapon slots for the first free one, assign the weapon to it, and return true if we found one. Return false otherwise
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            // only add the weapon if the slot is free
            if (weaponSlots[i] == null)
            {
                // spawn the weapon prefab as child of the weapon socket
                WeaponController weaponInstance = Instantiate(weaponPrefab, weaponParentSocket);
                weaponInstance.transform.localPosition = Vector3.zero;
                weaponInstance.transform.localRotation = Quaternion.identity;

                // Set owner to this gameObject so the weapon can alter projectile/damage logic accordingly
                weaponInstance.owner = gameObject;
                weaponInstance.sourcePrefab = weaponPrefab.gameObject;
                weaponInstance.ShowWeapon(false);

                // Assign the first person layer to the weapon
                int layerIndex = Mathf.RoundToInt(Mathf.Log(FPSWeaponLayer.value, 2)); // This function converts a layermask to a layer index
                foreach (Transform t in weaponInstance.gameObject.GetComponentsInChildren<Transform>(true))
                {
                    t.gameObject.layer = layerIndex;
                }

                weaponSlots[i] = weaponInstance;

                if (onAddedWeapon != null)
                {
                    onAddedWeapon.Invoke(weaponInstance, i);
                }

                return true;
            }
        }

        // Handle auto-switching to weapon if no weapons currently
        if (GetActiveWeapon() == null)
        {
            SwitchWeapon(true);
        }

        return false;
    }

    public bool RemoveWeapon(WeaponController weaponInstance)
    {
        // Look through our slots for that weapon
        for (int i = 0; i < weaponSlots.Length; i++)
        {
            // when weapon found, remove it
            if (weaponSlots[i] == weaponInstance)
            {
                weaponSlots[i] = null;

                if (onRemovedWeapon != null)
                {
                    onRemovedWeapon.Invoke(weaponInstance, i);
                }

                Destroy(weaponInstance.gameObject);

                // Handle case of removing active weapon (switch to next weapon)
                if (i == activeWeaponIndex)
                {
                    SwitchWeapon(true);
                }

                return true;
            }
        }

        return false;
    }

    public WeaponController GetActiveWeapon()
    {
        return GetWeaponAtSlotIndex(activeWeaponIndex);
    }

    public WeaponController GetWeaponAtSlotIndex(int index)
    {
        // find the active weapon in our weapon slots based on our active weapon index
        if (index >= 0 &&
            index < weaponSlots.Length)
        {
            return weaponSlots[index];
        }

        // if we didn't find a valid active weapon in our weapon slots, return null
        return null;
    }

    // Calculates the "distance" between two weapon slot indexes
    // For example: if we had 5 weapon slots, the distance between slots #2 and #4 would be 2 in ascending order, and 3 in descending order
    int GetDistanceBetweenWeaponSlots(int fromSlotIndex, int toSlotIndex, bool ascendingOrder)
    {
        int distanceBetweenSlots = 0;

        if (ascendingOrder)
        {
            distanceBetweenSlots = toSlotIndex - fromSlotIndex;
        }
        else
        {
            distanceBetweenSlots = -1 * (toSlotIndex - fromSlotIndex);
        }

        if (distanceBetweenSlots < 0)
        {
            distanceBetweenSlots = weaponSlots.Length + distanceBetweenSlots;
        }

        return distanceBetweenSlots;
    }

    void OnWeaponSwitched(WeaponController newWeapon)
    {
        if (newWeapon != null)
        {
            newWeapon.ShowWeapon(true);
        }
    }
}
