using UnityEngine;

public class PlayerInputHandler : MonoBehaviour
{
    public float cameraSensitivity = 1f;
    public bool invertYAxis = false;
    public bool invertXAxis = false;

    PlayerCharacterController player;
    bool FireInputHeld;

    private void Start()
    {
        player = GetComponent<PlayerCharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        FireInputHeld = FireHeld();
    }

    public bool CanProcessInput()
    {
        //poner condiciones para cuando puede usar Input
        return Cursor.lockState == CursorLockMode.Locked;
    }

    public Vector3 MoveInput()
    {
        if (CanProcessInput())
        {
            Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));

            //Clampear la velocidad para que en diagonal no supere la velocidad máxima
            move = Vector3.ClampMagnitude(move, 1);

            return move;
        }

        return Vector3.zero;
    }

    public float InputCameraHorizontal()
    {
        return GetMouseAxis("Mouse X");
    }
    public float InputCameraVertical()
    {
        return GetMouseAxis("Mouse Y");
    }

    public bool JumpInputDown()
    {
        if (CanProcessInput())
        {
            return Input.GetButtonDown("Jump");
        }
        return false;
    }

    public bool JumpInputHeld()
    {
        if (CanProcessInput())
        {
            return Input.GetButton("Jump");
        }

        return false;
    }
    public bool GetFireInputDown()
    {
        return FireHeld() && !FireInputHeld;
    }

    public bool GetFireInputReleased()
    {
        return !FireHeld() && FireInputHeld;
    }

    public bool FireHeld()
    {
        if (CanProcessInput())
            return Input.GetButton("Fire");

        return false;
    }

    public bool GetAimInputHeld()
    {
        if (CanProcessInput())
        {
            bool i = Input.GetButton("Aim");
            return i;
        }

        return false;
    }

    public bool GetSprintInputHeld()
    {
        if (CanProcessInput())
            return Input.GetButton("Sprint");

        return false;
    }

    public bool GetCrouchInputDown()
    {
        if (CanProcessInput())
            return Input.GetButtonDown("Crouch");

        return false;
    }

    public bool GetCrouchInputReleased()
    {
        if (CanProcessInput())
            return Input.GetButtonUp("Crouch");

        return false;
    }

    public int GetSwitchWeaponInput()
    {
        if (CanProcessInput())
        {
            string axisName = "Mouse ScrollWheel";

            if (Input.GetAxis(axisName) > 0f)
                return -1;
            else if (Input.GetAxis(axisName) < 0f)
                return 1;
            else if (Input.GetAxis("NextWeapon") > 0f)
                return -1;
            else if (Input.GetAxis("NextWeapon") < 0f)
                return 1;
        }

        return 0;
    }

    public int GetSelectWeaponInput()
    {
        if (CanProcessInput())
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
                return 1;
            else if (Input.GetKeyDown(KeyCode.Alpha2))
                return 2;
            else if (Input.GetKeyDown(KeyCode.Alpha3))
                return 3;
            else
                return 0;
        }

        return 0;
    }

    float GetMouseAxis(string mouseInputAxis)
    {
        if (CanProcessInput())
        {
            float i = Input.GetAxisRaw(mouseInputAxis);

            if (invertYAxis)
                i *= -1f;

            i *= cameraSensitivity;
            return i;
        }
        return 0f;
    }
}
