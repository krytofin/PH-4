using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float jumpHeight = 2f;

    [Header("Stamina System")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float staminaDepletionRate = 20f;
    [SerializeField] private float staminaRegenRate = 15f;
    [SerializeField] private float staminaRegenDelay = 2f;
    [SerializeField] private Image staminaBar;
    [SerializeField] private CanvasGroup staminaBarGroup;
    [SerializeField] private float fadeSpeed = 3f;

    [Header("Mouse Look")]
    [SerializeField] private Transform cameraHolder;
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float verticalClamp = 70f;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Sprint Effects")]
    [SerializeField] private float sprintFOV = 70f;
    [SerializeField] private float fovChangeSpeed = 5f;
    [SerializeField] private AudioSource sprintSound;

    private CharacterController controller;
    private PlayerInputActions inputActions;
    private Camera playerCamera;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private Vector3 velocity;
    
    private float yRotation;
    private float xRotation;
    private float currentSpeed;
    private float normalFOV;
    private bool isSprinting;
    private bool canSprint = true;
    
    private float currentStamina;
    private float staminaRegenTimer;
    private bool wasStaminaFull = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new PlayerInputActions();
        playerCamera = cameraHolder.GetComponentInChildren<Camera>();
        normalFOV = playerCamera.fieldOfView;
        currentStamina = maxStamina;

        // Set camera near clip plane to prevent seeing through objects
        if (playerCamera != null)
        {
            playerCamera.nearClipPlane = 0.1f;
            // Apply camera offset
            cameraHolder.localPosition = cameraOffset;
        }

        // Initialize UI elements
        if (staminaBarGroup != null)
        {
            staminaBarGroup.alpha = 1f;
        }
    }

    private void OnEnable()
    {
        inputActions.Player.Enable();
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;
        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;
        inputActions.Player.Sprint.performed += OnStartSprint;
        inputActions.Player.Sprint.canceled += OnStopSprint;
    }

    private void OnDisable()
    {
        inputActions.Player.Disable();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        currentSpeed = walkSpeed;
        UpdateStaminaUI();
    }

    private void Update()
    {
        HandleMovement();
        HandleLook();
        HandleSprintEffects();
        HandleStamina();
        HandleStaminaBarVisibility();
    }

    private void HandleMovement()
    {
        float targetSpeed = (isSprinting && canSprint) ? sprintSpeed : walkSpeed;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        if (controller.isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }

    private void HandleLook()
    {
        if (lookInput == Vector2.zero)
            return;

        Vector2 mouseDelta = lookInput * mouseSensitivity;
        yRotation += mouseDelta.x;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        xRotation -= mouseDelta.y;
        xRotation = Mathf.Clamp(xRotation, -verticalClamp, verticalClamp);
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }

    private void OnStartSprint(InputAction.CallbackContext context)
    {
        if (currentStamina > 0)
        {
            isSprinting = true;
            if (sprintSound != null && !sprintSound.isPlaying)
            {
                sprintSound.Play();
            }
        }
    }

    private void OnStopSprint(InputAction.CallbackContext context)
    {
        isSprinting = false;
        if (sprintSound != null && sprintSound.isPlaying)
        {
            sprintSound.Stop();
        }
    }

    private void HandleSprintEffects()
    {
        bool shouldApplyEffects = isSprinting && canSprint;
        float targetFOV = shouldApplyEffects ? sprintFOV : normalFOV;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView, 
            targetFOV, 
            fovChangeSpeed * Time.deltaTime
        );
    }

    private void HandleStamina()
    {
        if (isSprinting && moveInput.magnitude > 0.1f)
        {
            currentStamina -= staminaDepletionRate * Time.deltaTime;
            staminaRegenTimer = 0f;
            
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                canSprint = false;
                isSprinting = false;
                if (sprintSound != null && sprintSound.isPlaying)
                {
                    sprintSound.Stop();
                }
            }
        }
        else
        {
            staminaRegenTimer += Time.deltaTime;
            
            if (staminaRegenTimer >= staminaRegenDelay)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
                
                if (currentStamina >= maxStamina * 0.2f)
                {
                    canSprint = true;
                }
            }
        }
        
        UpdateStaminaUI();
    }

    private void UpdateStaminaUI()
    {
        if (staminaBar != null && staminaBarGroup != null)
        {
            staminaBar.fillAmount = currentStamina / maxStamina;
            
            if (currentStamina < maxStamina * 0.3f)
                staminaBar.color = Color.red;
            else if (currentStamina < maxStamina * 0.6f)
                staminaBar.color = Color.yellow;
            else
                staminaBar.color = Color.green;
        }
    }

    private void HandleStaminaBarVisibility()
    {
        if (staminaBarGroup == null) return;
        
        bool isStaminaFullNow = Mathf.Approximately(currentStamina, maxStamina);
        
        // Если состояние изменилось (полная/не полная)
        if (isStaminaFullNow != wasStaminaFull)
        {
            wasStaminaFull = isStaminaFullNow;
            
            if (isStaminaFullNow && !isSprinting)
            {
                StartCoroutine(FadeStaminaBar(0f));
            }
            else
            {
                StopAllCoroutines();
                StartCoroutine(FadeStaminaBar(1f));
            }
        }
    }

    private System.Collections.IEnumerator FadeStaminaBar(float targetAlpha)
    {
        if (staminaBarGroup == null) yield break;
        
        while (!Mathf.Approximately(staminaBarGroup.alpha, targetAlpha))
        {
            staminaBarGroup.alpha = Mathf.MoveTowards(
                staminaBarGroup.alpha, 
                targetAlpha, 
                fadeSpeed * Time.deltaTime
            );
            yield return null;
        }
    }
}