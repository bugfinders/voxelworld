using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CapsuleCollider))]
public class CapsulePlayerController : MonoBehaviour
{
    public Camera cam;

    // Movement
    public float moveSpeed = 8f;
    public float gravity = -20f;
    public float jumpHeight = 1.5f;
    public float mouseSensitivity = 2f;

    private float verticalVelocity;
    private float pitch;

    // Input
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpQueued;

    // Capsule
    private CapsuleCollider capsule;

    void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();

        // Input actions
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

        // Events
        moveAction.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        moveAction.canceled += ctx => moveInput = Vector2.zero;

        lookAction.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        lookAction.canceled += ctx => lookInput = Vector2.zero;

        jumpAction.performed += ctx => jumpQueued = true;
    }

    void Start()
    {
        // Hide any visible meshes
        foreach (var r in GetComponentsInChildren<MeshRenderer>())
            r.enabled = false;

        // Create pivot marker
        GameObject pivotMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pivotMarker.transform.SetParent(transform, false);
        pivotMarker.transform.localPosition = Vector3.zero;
        pivotMarker.transform.localScale = Vector3.one * 0.1f;

        var mr = pivotMarker.GetComponent<MeshRenderer>();
        mr.material.color = Color.red;
    }
    
    void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
    }

    void Update()
    {
        RotateCamera();
        MovePlayer();
    }

    // -------------------------
    // CAMERA ROTATION (FIXED)
    // -------------------------
    void RotateCamera()
    {
        float yaw = lookInput.x * mouseSensitivity;
        float pitchDelta = lookInput.y * mouseSensitivity;

        // Rotate player horizontally
        transform.Rotate(Vector3.up * yaw);

        // Rotate camera vertically
        pitch -= pitchDelta;
        pitch = Mathf.Clamp(pitch, -80f, 80f);

        cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Keep camera on player
        cam.transform.position = transform.position + new Vector3(0, 1.6f, 0);
    }

    // -------------------------
    // GROUND CHECK
    // -------------------------
    bool Grounded()
    {
        float radius = capsule.radius * 0.95f;

        // world‑space bottom of capsule, rotation‑independent
        Vector3 bottom = transform.position + Vector3.down * (capsule.height * 0.5f - radius);

        return Physics.SphereCast(bottom, radius, Vector3.down, out _, 0.05f);
    }


    // -------------------------
    // MOVEMENT + COLLISION
    // -------------------------
    void MovePlayer()
    {
        // Camera-relative movement
        Vector3 forward = cam.transform.forward;
        Vector3 right = cam.transform.right;

        forward.y = 0;
        right.y = 0;

        forward.Normalize();
        right.Normalize();

        Vector3 horizontal = forward * moveInput.y + right * moveInput.x;
        horizontal *= moveSpeed;

        bool grounded = Grounded();

        // Gravity + jump
        if (grounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = -1f;

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

        MoveWithCollisions(velocity * Time.deltaTime);
    }

    void GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius)
    {
        radius = capsule.radius;
        float height = Mathf.Max(capsule.height, radius * 2f);

        Vector3 center = transform.position + capsule.center;
        Vector3 up = transform.up;

        float half = (height * 0.5f) - radius;

        p1 = center + up * half;
        p2 = center - up * half;
    }

    void MoveWithCollisions(Vector3 delta)
    {
        if (delta == Vector3.zero)
            return;

        GetCapsulePoints(out Vector3 p1, out Vector3 p2, out float radius);

        Vector3 dir = delta.normalized;
        float dist = delta.magnitude;

        if (Physics.CapsuleCast(p1, p2, radius, dir, out RaycastHit hit, dist))
        {
            // Slide along surface
            Vector3 slide = Vector3.ProjectOnPlane(delta, hit.normal);
            transform.position += slide;
        }
        else
        {
            transform.position += delta;
        }
    }
}
