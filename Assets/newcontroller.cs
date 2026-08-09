using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelPlayerController : MonoBehaviour
{
    [SerializeField] Camera cam;

    public float moveSpeed = 6f;
    public float jumpHeight = 1.5f;
    public float gravity = -20f;
    public float mouseSensitivity = 2f;

    float verticalVelocity = 0f;
    float yaw = 0f;
    float pitch = 0f;

    InputAction moveAction;
    InputAction lookAction;
    InputAction jumpAction;
    InputAction dig;

    Vector2 moveInput;
    Vector2 lookInput;
    bool jumpQueued;
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
        
        dig = new InputAction("Dig", InputActionType.Button);
        dig.AddBinding("<Mouse>/leftButton").WithInteraction("hold(duration=2)");
        dig.performed += ctx => doAction = voxelAction.dig;
    }
    
    void OnEnable()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();
        RotateCamera();
        MoveController();
        // MoveHeightBased();
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
