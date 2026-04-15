using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Horizontal Mov Settings:")]
    [SerializeField] private float walkSpeed = 5f;
    private float originalSpeed;

    [Header("Push/Pull Settings:")]
    [SerializeField] private float pushSpeed = 2f;
    private bool isPushing = false;
    private PushableObject pushableObject;
    private float originalPushSpeed;
    bool isHoldingPushKey => Input.GetKey(KeyCode.E);

    [Header("Jump Mov Settings:")]
    [SerializeField] private float jumpForce = 8f;
    private float jumpBufferCounter = 0f;
    [SerializeField] private float jumpBufferTime = 0.2f;
    private float coyoteTimeCounter = 0f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private int maxJumps = 1;
    private int jumpCount;
    //[SerializeField] private AudioClip jumpSound;
    [SerializeField] private AudioClip landSound;
    private bool wasGroundedLastFrame;

    [Header("Dash Settings")]
    [SerializeField] private float dashSpeed = 15f;
    [SerializeField] private float dashTime = 0.2f;
    [SerializeField] private float dashCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    private bool isDashOnCooldown = false;
    private bool canDash = true;
    private bool isDashing = false;
    public bool isInvulnerable { get; private set; }
    [SerializeField] private AudioClip dashSound;
    [SerializeField] private AudioClip dashSound2;

    [Header("Ground Check Settings")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Audio Settings")]
    private AudioSource audioSource;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] forestSteps;
    [SerializeField] private AudioClip[] caveSteps;
    [SerializeField] private AudioClip[] rockyGroundSteps;
    [SerializeField] private AudioClip[] groundSteps;
    [SerializeField] private AudioClip[] floorSteps;

    private AudioClip[] currentFootstepClips;
    private float footstepTimer = 0f;
    [SerializeField] private float footstepInterval = 0.4f;
    [SerializeField] private AudioClip healingSound;

    [Header("Attacking")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private LayerMask enemyLayers;
    [SerializeField] private Collider attackCollider;

    private StickWeapon currentWeapon = null;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private AudioClip[] hitSounds;
    [SerializeField] private AudioClip[] wooshSounds;

    [Header("Kick Attack Settings")]
    [SerializeField] private Transform kickPoint;
    [SerializeField] private Collider kickCollider;
    [SerializeField] private AudioClip[] hitSoundsKick;
    [SerializeField] private AudioClip[] wooshSoundsKick;

    bool attack = false;
    float timeBetweenAttack, timeSinceAttack;

    [Header("Recoger Items")]
    [SerializeField] private AudioClip pickUpStickSound;
    private bool isPickingUp = false;

    private Rigidbody rb;
    private Animator anim;
    private float xAxis;
    private bool isGrounded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        audioSource = GetComponent<AudioSource>();
    }
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        originalSpeed = walkSpeed;
        originalPushSpeed = pushSpeed;
    }
    private void Update()
    {
        if (PauseManager.IsPaused)
            return;

        if (anim.GetCurrentAnimatorStateInfo(0).IsName("PickUp") && anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
        {
            return;
        }
        GetInputs();
        UpdateJumpVariables();

        if (!isDashing)
        {
            Move();
            HandleJump();
            FlipCharacter();
        }

        HandleDash();
        UpdateAnimations();
        Attack();
        HandlePush();
        PlayFootsteps();

        if (Input.GetKeyDown(KeyCode.F))
        {
            TryPickUpStick();
            TryPickUpFlower();
        }
    }

    // INPUTS
    void GetInputs()
    {
        xAxis = Input.GetAxisRaw("Horizontal");
        attack = Input.GetMouseButtonDown(0);

        if (Input.GetMouseButtonDown(1))
        {
            PerformKick();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            TryPush();
        }
        if (Input.GetKeyUp(KeyCode.E))
        {
            StopPush();
        }
    }
    //MOVIMIENTO
    #region Movimiento
    void Move()
    {
        if (isPickingUp) return;

        Vector3 move = new Vector3(xAxis * (isPushing ? pushSpeed : walkSpeed), rb.velocity.y, 0);
        rb.velocity = move;
    }
    // SISTEMA DE SALTO
    void HandleJump()
    {
        if (jumpBufferCounter > 0 && (coyoteTimeCounter > 0 || jumpCount < maxJumps))
        {
            rb.velocity = new Vector3(rb.velocity.x, jumpForce, 0);
            //if (jumpSound != null) audioSource.PlayOneShot(jumpSound);
            jumpBufferCounter = 0;
            coyoteTimeCounter = 0;
            jumpCount++;
        }

        if (Input.GetButtonUp("Jump") && rb.velocity.y > 0)
        {
            rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y * 0.5f, 0);
        }
    }
    void UpdateJumpVariables()
    {
        isGrounded = Grounded();

        if (!wasGroundedLastFrame && isGrounded)
        {
            if (landSound != null) audioSource.PlayOneShot(landSound);
        }

        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
            jumpCount = 0;

            if (!isDashOnCooldown)
            {
                canDash = true;
            }
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }

        wasGroundedLastFrame = isGrounded;
    }
    //SISTEMA DE DASH
    void HandleDash()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && canDash && !isDashing)
        {
            StartCoroutine(Dash());
        }
    }
    IEnumerator Dash()
    {
        canDash = false;
        isDashing = true;
        isDashOnCooldown = true;
        isInvulnerable = true;

        if (dashSound != null) audioSource.PlayOneShot(dashSound);
        if (dashSound2 != null) audioSource.PlayOneShot(dashSound2);

        rb.useGravity = false;

        float dashDirection = transform.forward.x != 0 ? transform.forward.x : (xAxis != 0 ? xAxis : 1);
        rb.velocity = new Vector3(dashDirection * dashSpeed, 0, 0);

        anim.SetTrigger("Dashing");
        anim.SetBool("IsDashing", true);

        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        yield return new WaitForSeconds(dashTime);

        rb.useGravity = true;
        isDashing = false;
        anim.SetBool("IsDashing", false);

        yield return new WaitForSeconds(1f);

        isInvulnerable = false;

        Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), false);

        yield return new WaitForSeconds(dashCooldown);

        isDashOnCooldown = false;
        canDash = isGrounded;
    }
    void FlipCharacter()
    {
        if (xAxis != 0)
        {
            float targetYRotation = xAxis > 0 ? 90f : -90f;
            transform.rotation = Quaternion.Euler(0, targetYRotation, 0);
        }
    }
    #endregion

    bool Grounded()
    {
        return Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundLayer);
    }

    //Interactions
    #region Interactions
    //SISTEMA DE EMPUJE
    void TryPush()
    {
        if (pushableObject != null)
        {
            isPushing = true;
            pushableObject.StartPushing();
        }
    }
    void StopPush()
    {
        if (pushableObject != null)
        {
            pushableObject.StopPushing();
        }
        isPushing = false;
    }
    void HandlePush()
    {
        if (pushableObject != null)
        {
            if (isHoldingPushKey && Mathf.Abs(xAxis) > 0.1f)
            {
                if (!isPushing)
                    TryPush();

                Vector3 pushForce = new Vector3(xAxis * pushSpeed, 0, 0);
                pushableObject.Push(pushForce);
            }
            else if (isPushing)
            {
                StopPush();
            }
        }
    }
    public void ModifyPushSpeed(float multiplier, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(SlowDownPushSpeed(multiplier, duration));
    }
    private IEnumerator SlowDownPushSpeed(float multiplier, float duration)
    {
        pushSpeed = originalPushSpeed * multiplier;

        yield return new WaitForSeconds(duration);

        pushSpeed = originalPushSpeed;
    }

    //SISTEMA DE AGARRE
    void TryPickUpStick()
    {
        if (currentWeapon != null || isPickingUp) return;

        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1f);
        foreach (Collider col in hitColliders)
        {
            if (col.CompareTag("Stick"))
            {
                EquipStick(col.gameObject.GetComponent<StickWeapon>());
                break;
            }
        }
    }
    void EquipStick(StickWeapon stick)
    {
        if (stick != null)
        {
            StartCoroutine(PickUpRoutine(stick));
        }
    }
    IEnumerator PickUpRoutine(StickWeapon stick)
    {
        if (stick != null)
        {
            isPickingUp = true;
            anim.SetTrigger("PickUp");

            rb.velocity = Vector3.zero;

            yield return new WaitForSeconds(0.6f);

            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
            }

            currentWeapon = stick;

            Rigidbody stickRb = stick.GetComponent<Rigidbody>();
            if (stickRb != null)
            {
                stickRb.isKinematic = true;
                stickRb.useGravity = false;
            }

            Collider stickCollider = stick.GetComponent<Collider>();
            if (stickCollider != null)
            {
                stickCollider.enabled = false;
            }

            stick.transform.SetParent(weaponHolder);
            stick.transform.localPosition = Vector3.zero;
            stick.transform.localRotation = Quaternion.identity;
            stick.OnPickedUp();

            if (pickUpStickSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(pickUpStickSound);
            }

            isPickingUp = false;
        }
    }
    void TryPickUpFlower()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f);
        foreach (Collider col in hitColliders)
        {
            if (col.CompareTag("HealingFlower"))
            {
                Health playerHealth = GetComponent<Health>();
                if (playerHealth != null && playerHealth.NeedsHealing())
                {
                    StartCoroutine(PickUpFlowerRoutine(col.gameObject, playerHealth));
                }
                else
                {
                    Debug.Log("Ya tienes la vida al máximo");
                }

                break;
            }
        }
    }
    IEnumerator PickUpFlowerRoutine(GameObject flower, Health playerHealth)
    {
        anim.SetTrigger("PickUp");

        float originalSpeed = walkSpeed;
        walkSpeed = 0f;

        rb.velocity = Vector3.zero;

        yield return new WaitForSeconds(0.6f);

        playerHealth.Heal(2);

        if (healingSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(healingSound, 4f);
        }

        Destroy(flower);
        walkSpeed = originalSpeed;

        Debug.Log("Recogiste una flor y recuperaste vida");
    }

    #endregion

    //SISTEMA DE ATAQUE
    #region Ataque
    //Puños
    void Attack()
    {
        timeSinceAttack += Time.deltaTime;
        if (attack && timeSinceAttack >= timeBetweenAttack)
        {
            timeSinceAttack = 0;
            anim.SetTrigger("Attacking");

            if (wooshSounds.Length > 0 && audioSource != null)
            {
                AudioClip woosh = wooshSounds[Random.Range(0, wooshSounds.Length)];
                audioSource.PlayOneShot(woosh);
            }
            if (hitSounds.Length > 0 && audioSource != null)
            {
                AudioClip hit = hitSounds[Random.Range(0, hitSounds.Length)];
                audioSource.PlayOneShot(hit);
            }

            if (currentWeapon != null)
            {
                Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, 1f, enemyLayers);

                foreach (Collider enemy in hitEnemies)
                {
                    SpiderHealth spiderHealth = enemy.GetComponent<SpiderHealth>();
                    if (spiderHealth != null)
                    {
                        int attackDamage = currentWeapon.Use();
                        spiderHealth.TakeDamage(attackDamage);
                        Debug.Log("Golpeaste a una araña: " + enemy.gameObject.name + " con el stick");
                    }

                    Health enemyHealth = enemy.GetComponent<Health>();
                    if (enemyHealth != null)
                    {
                        int attackDamage = currentWeapon.Use();
                        enemyHealth.TakeDamage(attackDamage);
                        Debug.Log("Golpeaste a un enemigo: " + enemy.gameObject.name + " con el stick");
                    }
                }
            }
            else
            {
                Collider[] hitEnemies = Physics.OverlapSphere(attackPoint.position, 0.5f, enemyLayers);

                foreach (Collider enemy in hitEnemies)
                {
                    Health enemyHealth = enemy.GetComponent<Health>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.TakeDamage(attackDamage);
                        Debug.Log("Golpeaste a: " + enemy.gameObject.name);
                    }
                }
            }
        }
    }
    public bool HasWeaponEquipped()
    {
        return currentWeapon != null;
    }

    public int UseWeapon()
    {
        if (currentWeapon != null)
        {
            return currentWeapon.Use();
        }
        return 0;
    }
    //Patada
    void PerformKick()
    {
        timeSinceAttack += Time.deltaTime;
        timeSinceAttack = 0;
        anim.SetTrigger("Kick");

        if (wooshSoundsKick.Length > 0 && audioSource != null)
        {
            AudioClip wooshkick = wooshSoundsKick[Random.Range(0, wooshSoundsKick.Length)];
            audioSource.PlayOneShot(wooshkick);
        }
        if (wooshSounds.Length > 0 && audioSource != null)
        {
            AudioClip hitkick = hitSoundsKick[Random.Range(0, hitSoundsKick.Length)];
            audioSource.PlayOneShot(hitkick);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") && !isInvulnerable)
        {
            if (other.TryGetComponent<Health>(out Health health))
            {
                health.TakeDamage(attackDamage);
                Debug.Log("Golpeaste a: " + other.gameObject.name);
            }

            SpiderHealth spiderHealth = other.GetComponent<SpiderHealth>();
            if (spiderHealth != null)
            {
                spiderHealth.TakeDamage(1);
            }
        }
    }
    #endregion

    //SISTEMA DE COLLIDER
    #region
    public void EnablePunchCollider()
    {
        if (currentWeapon != null) return;

        attackCollider.enabled = true;
    }
    public void DisablePunchCollider()
    {
        if (currentWeapon != null) return;

        attackCollider.enabled = false;
    }
    public void EnableKickCollider()
    {
        kickCollider.enabled = true;
    }
    public void DisableKickCollider()
    {
        kickCollider.enabled = false;
    }
    #endregion

    //Colisiones
    #region
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Pushable"))
        {
            pushableObject = collision.gameObject.GetComponent<PushableObject>();

            if (Input.GetKey(KeyCode.E))
            {
                TryPush();
            }
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Pushable"))
        {
            pushableObject = null;
            StopPush();
        }
    }
    #endregion

    //Animaciones
    #region Animaciones
    void UpdateAnimations()
    {
        anim.SetBool("IsMoving", xAxis != 0 && !isDashing);
        anim.SetBool("Jumping", !isGrounded);
        anim.SetBool("IsDashing", isDashing);
        anim.SetBool("IsPushing", isPushing);
    }
    #endregion

    //AUDIO Pisadas
    public void SetFootstepType(FootstepZone.FootstepType type)
    {
        switch (type)
        {
            case FootstepZone.FootstepType.Forest:
                currentFootstepClips = forestSteps;
                break;
            case FootstepZone.FootstepType.Cave:
                currentFootstepClips = caveSteps;
                break;
            case FootstepZone.FootstepType.Rocky:
                currentFootstepClips = rockyGroundSteps;
                break;
            case FootstepZone.FootstepType.Ground:
                currentFootstepClips = groundSteps;
                break;
            case FootstepZone.FootstepType.Floor:
                currentFootstepClips = floorSteps;
                break;
        }
    }
    void PlayFootsteps()
    {
        if (isGrounded && xAxis != 0 && !isDashing && !isPickingUp)
        {
            float interval = isPushing ? footstepInterval * 3f : footstepInterval;

            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f && currentFootstepClips != null && currentFootstepClips.Length > 0)
            {
                footstepSource.clip = currentFootstepClips[Random.Range(0, currentFootstepClips.Length)];
                footstepSource.Play();
                footstepTimer = interval;
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }
    public void Die()
    {
        Debug.Log("¡Game Over!");
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ShowGameOver();
        }
    }
}
