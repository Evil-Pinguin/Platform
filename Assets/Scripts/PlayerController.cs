using UnityEngine;

/// <summary>
/// Simple 2D platform controller for the walking test scene.
/// Horizontal movement uses the legacy Horizontal axis (A/D and arrow keys).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.12f;

    [Header("Walk animation")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private float frameDuration = 0.12f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Vector3 spawnPosition;
    private float horizontalInput;
    private float animationTimer;
    private int animationFrame;
    private GUIStyle hintStyle;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        spawnPosition = transform.position;

        if (idleSprite == null && walkFrames != null && walkFrames.Length > 0)
        {
            idleSprite = walkFrames[0];
        }

        spriteRenderer.sprite = idleSprite;
    }

    private void Update()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump") && IsGrounded())
        {
            body.velocity = new Vector2(body.velocity.x, jumpForce);
        }

        UpdateWalkAnimation();

        // Return to the starting point if the character falls off the test platform.
        if (transform.position.y < -12f)
        {
            transform.position = spawnPosition;
            body.velocity = Vector2.zero;
        }
    }

    private void FixedUpdate()
    {
        body.velocity = new Vector2(horizontalInput * moveSpeed, body.velocity.y);
    }

    private void UpdateWalkAnimation()
    {
        bool isWalking = Mathf.Abs(horizontalInput) > 0.01f;
        if (!isWalking || walkFrames == null || walkFrames.Length == 0)
        {
            animationTimer = 0f;
            animationFrame = 0;
            spriteRenderer.sprite = idleSprite;
        }
        else
        {
            animationTimer += Time.deltaTime;
            if (animationTimer >= frameDuration)
            {
                animationTimer -= frameDuration;
                animationFrame = (animationFrame + 1) % walkFrames.Length;
            }

            spriteRenderer.sprite = walkFrames[animationFrame];
        }

        // The supplied walk cycle faces right. Flip it when moving left.
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            spriteRenderer.flipX = horizontalInput < 0f;
        }
    }

    private bool IsGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }

    private void OnGUI()
    {
        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white },
                padding = new RectOffset(14, 14, 10, 10)
            };
        }

        GUI.Box(new Rect(16f, 16f, 328f, 58f), GUIContent.none);
        GUI.Label(new Rect(16f, 16f, 328f, 58f), "A / D или ← / → — ходьба\nSpace — прыжок", hintStyle);
    }
}
