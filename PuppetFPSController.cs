// Made by @mapledev.

using UnityEngine;

public class PuppetFPSController : MonoBehaviour
{
    [Header("movement")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float crouchSpeed = 2f;
    [SerializeField] private float jumpForce = 5f;
    
    [Header("camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float lookSensitivity = 2f;
    [SerializeField] private float maxLookAngle = 80f;
    
    [Header("headbobbing")]
    [SerializeField] private float bobbingSpeed = 10f;
    [SerializeField] private float bobbingAmount = 0.1f;
    private float defaultYPos;
    private float timer;
    
    [Header("smooth movement")]
    [SerializeField] private float smoothSpeed = 10f;
    private Vector3 smoothMoveVelocity;
    private Vector3 moveAmount;
    
    private Rigidbody rb;
    private float verticalRotation;
    private bool isGrounded;
    private bool isCrouching;
    private Vector3 originalHeight;
    private float crouchHeight = 0.5f;

    [Header("breathing")]
    [SerializeField] private float breathingSpeed = 1f;
    [SerializeField] private float breathingAmount = 0.05f;
    [SerializeField] private float exhaustionMultiplier = 1.5f; 
    private float breathingTimer;
    private float currentBreathingSpeed;
    private float currentBreathingAmount;

    [Header("footstep")]
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioClip[] walkFootstepSounds;
    [SerializeField] private AudioClip[] runFootstepSounds;
    [SerializeField] private float baseStepSpeed = 0.5f;
    [SerializeField] private float runStepMultiplier = 1.7f;
    [SerializeField] private float crouchStepMultiplier = 1.5f;
    private float footstepTimer;
    private float GetCurrentStepOffset() => 
        isCrouching ? baseStepSpeed * crouchStepMultiplier : 
        (Input.GetKey(KeyCode.LeftShift) ? baseStepSpeed / runStepMultiplier : baseStepSpeed);

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        Cursor.lockState = CursorLockMode.Locked;
        defaultYPos = playerCamera.transform.localPosition.y;
        originalHeight = transform.localScale;
    }

    void Update()
    {
        HandleMouseLook();
        
        HandleMovement();
        
        HandleJump();
        
        HandleCrouch();
        
        if (IsMoving() && isGrounded)
            HandleHeadbob();
        else
            HandleBreathing();
        
        if (IsMoving() && isGrounded)
            HandleFootsteps();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
        transform.rotation *= Quaternion.Euler(0, mouseX, 0);

        verticalRotation -= Input.GetAxis("Mouse Y") * lookSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
        playerCamera.transform.localRotation = Quaternion.Lerp(
            playerCamera.transform.localRotation,
            Quaternion.Euler(verticalRotation, 0, 0),
            Time.deltaTime * smoothSpeed
        );
    }

    void HandleMovement()
    {
        Vector3 moveDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")).normalized;
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? runSpeed : walkSpeed;
        currentSpeed = isCrouching ? crouchSpeed : currentSpeed;

        moveAmount = Vector3.SmoothDamp(
            moveAmount,
            moveDir * currentSpeed,
            ref smoothMoveVelocity,
            0.15f
        );
    }

    void HandleHeadbob()
    {
        timer += Time.deltaTime * bobbingSpeed;
        float bobbingY = defaultYPos + Mathf.Sin(timer) * bobbingAmount;
        float bobbingX = Mathf.Cos(timer * 0.5f) * bobbingAmount;
        
        Vector3 newPos = playerCamera.transform.localPosition;
        newPos.y = bobbingY;
        newPos.x = bobbingX;
        
        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            newPos,
            Time.deltaTime * smoothSpeed
        );
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
            Vector3 targetScale = isCrouching ? 
                new Vector3(originalHeight.x, crouchHeight, originalHeight.z) : 
                originalHeight;

            StartCoroutine(SmoothCrouch(targetScale));
        }
    }

    void HandleJump()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void HandleBreathing()
    {
        float targetSpeed = breathingSpeed;
        float targetAmount = breathingAmount;
        
        if (Input.GetKey(KeyCode.LeftShift) && IsMoving())
        {
            targetSpeed *= exhaustionMultiplier;
            targetAmount *= exhaustionMultiplier;
        }
        
        currentBreathingSpeed = Mathf.Lerp(currentBreathingSpeed, targetSpeed, Time.deltaTime * 2f);
        currentBreathingAmount = Mathf.Lerp(currentBreathingAmount, targetAmount, Time.deltaTime * 2f);
        
        breathingTimer += Time.deltaTime * currentBreathingSpeed;
        
        float breathingY = defaultYPos + Mathf.Sin(breathingTimer) * currentBreathingAmount;
        float breathingZ = Mathf.Cos(breathingTimer * 0.5f) * (currentBreathingAmount * 0.5f);
        
        Vector3 newPos = playerCamera.transform.localPosition;
        newPos.y = breathingY;
        newPos.z = breathingZ;
        
        playerCamera.transform.localPosition = Vector3.Lerp(
            playerCamera.transform.localPosition,
            newPos,
            Time.deltaTime * smoothSpeed * 0.5f
        );
    }

    void HandleFootsteps()
    {
        footstepTimer -= Time.deltaTime;
        
        if (footstepTimer <= 0)
        {
            if (footstepAudioSource != null && (walkFootstepSounds.Length > 0 || runFootstepSounds.Length > 0))
            {
                AudioClip[] currentClips = Input.GetKey(KeyCode.LeftShift) ? runFootstepSounds : walkFootstepSounds;
                
                if (currentClips.Length > 0)
                {
                    AudioClip randomStep = currentClips[Random.Range(0, currentClips.Length)];
                    footstepAudioSource.PlayOneShot(randomStep);
                }
            }
            
            footstepTimer = GetCurrentStepOffset();
        }
    }

    System.Collections.IEnumerator SmoothCrouch(Vector3 targetScale)
    {
        float elapsedTime = 0f;
        Vector3 startScale = transform.localScale;
        
        while (elapsedTime < 0.2f)
        {
            elapsedTime += Time.deltaTime;
            float percentage = elapsedTime / 0.2f;
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, percentage);
            yield return null;
        }
    }

    bool IsMoving()
    {
        return Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + transform.TransformDirection(moveAmount) * Time.fixedDeltaTime);
    }

    void OnCollisionStay(Collision collision)
    {
        isGrounded = true;
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
} 

