using UnityEngine;
using UnityEngine.Events;

public class ProjectileBase : MonoBehaviour
{
    public GameObject owner;
    public Vector3 initialPosition;
    public Vector3 initialDirection;
    public Vector3 inheritedMuzzleVelocity;
    public float initialCharge;

    public UnityAction onShoot;

    public void Shoot(WeaponController controller)
    {
        owner = controller.owner;
        initialPosition = transform.position;
        initialDirection = transform.forward;
        inheritedMuzzleVelocity = controller.muzzleWorldVelocity;
        initialCharge = controller.currentCharge;

        if (onShoot != null)
        {
            onShoot.Invoke();
        }
    }
}
