using UnityEngine;
using UnityEngine.InputSystem;

public class VoxelPlayerController : MonoBehaviour
{
    [SerializeField] Camera cam;
    [SerializeField] NewWorld world;

    public float moveSpeed = 6f;

    // Apex of the jump. Must sit between 1 and 2 so a one-voxel ledge is
    // reachable and a two-voxel one is not.
    public float jumpHeight = 1.25f;
    public float gravity = -9.8f;
    public float mouseSensitivity = 2f;
    public float digRepeatInterval = 2f;

    // The player is a capsule, so transform.position is its CENTRE. VoxelBody
    // works in feet space, so everything crossing that boundary is offset by
    // half the height.
    public float playerHeight = 2f;
    public float playerRadius = 0.3f;

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
    bool digHeld;
    float digTimer;
    public voxelAction doAction;

    VoxelBody body;

    public bool IsGrounded => body != null && body.Grounded;

    void Awake()
    {
        if (world == null) world = FindAnyObjectByType<NewWorld>(FindObjectsInactive.Include);
        if (world == null)
            Debug.LogError("VoxelPlayerController has no NewWorld: nothing will block the player");

        // Serialized scene values override the defaults above, so a stale or
        // zeroed inspector field would silently disable falling and jumping.
        float safeGravity = SanitiseGravity(gravity);
        if (safeGravity != gravity)
            Debug.LogWarning($"gravity was {gravity}; falling and jumping need a negative value. Using {safeGravity}.");
        gravity = safeGravity;

        float safeJump = SanitiseJumpHeight(jumpHeight);
        if (safeJump != jumpHeight)
            Debug.LogWarning($"jumpHeight of {jumpHeight} breaks the one-voxel step rule; it must be between 1 and 2. Using {safeJump}.");
        jumpHeight = safeJump;

        if (GetComponent<Rigidbody>() != null)
            Debug.LogWarning("Player still has a Rigidbody; physics will fight the voxel movement. Remove it.");

        body = new VoxelBody(IsWorldSolid, playerHeight, playerRadius);
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
        // The hold only arms repeating; the cadence is driven in Update.
        dig.performed += ctx => { digHeld = true; digTimer = 0f; };
        dig.canceled += ctx => digHeld = false;
    }
    
    void OnEnable()
    {
        /*Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;*/
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        dig.Enable();

        // The world repositions the player while this component is disabled, so
        // pick the transform back up rather than trusting the cached position.
        body.Position = FeetOf(transform.position);
        body.SnapToGround();
        transform.position = CentreOf(body.Position);
        jumpQueued = false;
    }

    void OnDisable()
    {
        /*Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;*/
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        dig.Disable();
    }

    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        lookInput = lookAction.ReadValue<Vector2>();

        TickDig();

        RotateCamera();
        MovePlayer();
    }

    // Called when the aimed-at voxel changes, so switching target restarts the
    // hold rather than inheriting progress made on the previous one.
    public void ResetDigTimer()
    {
        digTimer = digRepeatInterval;
        doAction = voxelAction.nothing;
    }

    // Raises doAction once per digRepeatInterval while the button stays held.
    // The terrain clears the flag when it acts on it.
    private void TickDig()
    {
        // Hold's canceled phase is unreliable once performed has fired, so disarm
        // off the raw button state instead.
        if (digHeld && !dig.IsPressed()) digHeld = false;

        if (!digHeld)
        {
            digTimer = 0f; // next arming digs immediately
            return;
        }

        digTimer -= Time.deltaTime;
        if (digTimer <= 0f)
        {
            doAction = voxelAction.dig;
            digTimer = digRepeatInterval;
        }
    }

    /// <summary>A non-negative gravity leaves the player floating and makes jumping a no-op.</summary>
    public static float SanitiseGravity(float gravity) => gravity >= 0f ? -9.8f : gravity;

    /// <summary>
    /// The jump apex is the step height, so it has to clear one voxel and fall short
    /// of two. Anything outside that range breaks the movement rules.
    /// </summary>
    public static float SanitiseJumpHeight(float jumpHeight) =>
        jumpHeight > 1f && jumpHeight < 2f ? jumpHeight : Mathf.Clamp(jumpHeight, 1.15f, 1.9f);

    /// <summary>Distance from the capsule's pivot down to its feet.</summary>
    public float PivotHeight => playerHeight * 0.5f;

    Vector3 FeetOf(Vector3 centre) => centre - Vector3.up * PivotHeight;
    Vector3 CentreOf(Vector3 feet) => feet + Vector3.up * PivotHeight;

    /// <summary>Stands the player with its feet on <paramref name="feet"/>, then settles it.</summary>
    public void PlaceFeetAt(Vector3 feet)
    {
        if (body == null) body = new VoxelBody(IsWorldSolid, playerHeight, playerRadius);

        body.Position = feet;
        body.SnapToGround();
        transform.position = CentreOf(body.Position);
    }

    bool IsWorldSolid(int x, int y, int z) => world != null && world.IsSolid(x, y, z);

    private void MovePlayer()
    {
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 horizontal = (right * moveInput.x + forward * moveInput.y) * moveSpeed;

        // Not buffered: a jump pressed mid-air is dropped, not replayed on landing.
        if (jumpQueued)
        {
            body.TryJump(jumpHeight, gravity);
            jumpQueued = false;
        }

        body.Step(horizontal, gravity, Time.deltaTime);

        transform.position = CentreOf(body.Position);
        verticalVelocity = body.VerticalVelocity;
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
