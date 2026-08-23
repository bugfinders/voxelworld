using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelPlayerController : MonoBehaviour
{
    [SerializeField] Camera cam;

    public float moveSpeed = 6f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float mouseSensitivity = 2f;
    public float digRepeatInterval = 2f;

    float verticalVelocity = 0f;
    float yaw = 0f;
    float pitch = 0f;

    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;
    InputAction interactionAction;

    Vector2 moveInput;
    Vector2 lookInput;
    bool jumpQueued;
    bool interactionHeld;
    bool placeRequested;
    bool mineRequested;
    bool useRequested;
    bool minedDuringInteraction;
    float digTimer;
    public voxelAction doAction;
    private CharacterController cc;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        yaw = transform.eulerAngles.y;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", InputActionType.Value);
        lookAction.AddBinding("<Mouse>/delta");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
        jumpAction.performed += ctx => jumpQueued = true;

        interactionAction = new InputAction("Interact", InputActionType.Button);
        interactionAction.AddBinding("<Mouse>/leftButton");
        interactionAction.performed += ctx =>
        {
            placeRequested = true;
            interactionHeld = true;
            minedDuringInteraction = false;
            digTimer = digRepeatInterval;
        };
        interactionAction.canceled += ctx =>
        {
            interactionHeld = false;
            if (!minedDuringInteraction)
                useRequested = true;
        };
    }

    /// <summary>
    /// Applies the user-configured mouse sensitivity to the active controller.
    /// </summary>
    public void SetMouseSensitivity(float value)
    {
        mouseSensitivity = Mathf.Clamp(value, 0.5f, 6f);
    }

    /// <summary>
    /// Synchronizes the controller look state with the restored player and camera transforms.
    /// </summary>
    public void SyncLookToTransforms()
    {
        yaw = transform.eulerAngles.y;
        if (cam == null)
            return;

        pitch = cam.transform.localEulerAngles.x;
        if (pitch > 180f)
            pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
    }

    void OnEnable()
    {
        // Cursor state is managed by the gameplay menu.
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        interactionAction.Enable();
    }

    void OnDisable()
    {
        /*Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;*/
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        interactionAction.Disable();
        interactionHeld = false;
        placeRequested = false;
        mineRequested = false;
        useRequested = false;
        minedDuringInteraction = false;
        doAction = voxelAction.nothing;
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();

        TickDig();

        RotateCamera();
        MoveController();
        // MoveHeightBased();
    }

    // Called when the aimed-at voxel changes, so switching target restarts the
    // hold rather than inheriting progress made on the previous one.
    public void ResetDigTimer()
    {
        digTimer = digRepeatInterval;
        mineRequested = false;
        if (doAction == voxelAction.dig) doAction = voxelAction.nothing;
    }

    /// <summary>
    /// Stops the current left-click mining hold after a station interaction.
    /// </summary>
    public void CancelInteractionHold()
    {
        interactionHeld = false;
        digTimer = 0f;
        mineRequested = false;
        placeRequested = false;
        useRequested = false;
        if (doAction == voxelAction.dig)
            doAction = voxelAction.nothing;
    }

    /// <summary>
    /// Returns and clears one queued workbench-use request from a short click.
    /// </summary>
    public bool ConsumeUseRequest()
    {
        if (VoxelInventoryUI.IsDragging)
        {
            useRequested = false;
            return false;
        }

        bool request = useRequested;
        useRequested = false;
        return isActiveAndEnabled && request;
    }

    /// <summary>
    /// Returns and clears one queued left-click placement request.
    /// </summary>
    public bool ConsumePlaceRequest()
    {
        if (VoxelInventoryUI.IsDragging)
        {
            placeRequested = false;
            return false;
        }

        bool request = placeRequested;
        placeRequested = false;
        return isActiveAndEnabled && request;
    }

    /// <summary>
    /// Returns and clears one queued repeat-mining request.
    /// </summary>
    public bool ConsumeMineRequest()
    {
        if (VoxelInventoryUI.IsDragging)
        {
            mineRequested = false;
            if (doAction == voxelAction.dig)
                doAction = voxelAction.nothing;
            return false;
        }

        bool request = mineRequested || doAction == voxelAction.dig;
        mineRequested = false;
        if (doAction == voxelAction.dig) doAction = voxelAction.nothing;
        return isActiveAndEnabled && request;
    }

    // Raises a mining request once per digRepeatInterval while left-click stays held.
    private void TickDig()
    {
        // Hold's canceled phase is unreliable once performed has fired, so disarm
        // off the raw button state instead.
        if (interactionHeld && !interactionAction.IsPressed()) interactionHeld = false;

        if (!interactionHeld)
        {
            digTimer = 0f;
            return;
        }

        digTimer -= Time.deltaTime;
        if (digTimer <= 0f)
        {
            mineRequested = true;
            minedDuringInteraction = true;
            doAction = voxelAction.dig;
            digTimer = digRepeatInterval;
        }
    }

    private void MoveController()
    {
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 horizontal = (right * moveInput.x + forward * moveInput.y) * moveSpeed;

        if (cc.isGrounded)
        {
            verticalVelocity = 0f;

            if (jumpQueued)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpQueued = false;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = horizontal;
        velocity.y = verticalVelocity;

        cc.Move(velocity * Time.deltaTime);
    }

    void RotateCamera()
    {
        if (VoxelInventoryUI.IsAnyWindowVisible)
        {
            lookInput = Vector2.zero;
            return;
        }

        float yawDelta = lookInput.x * mouseSensitivity;
        float pitchDelta = lookInput.y * mouseSensitivity;

        yaw += yawDelta;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        pitch -= pitchDelta;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}

public enum voxelAction
{
    nothing = 0,
    dig = 1,
    place = 2
}
