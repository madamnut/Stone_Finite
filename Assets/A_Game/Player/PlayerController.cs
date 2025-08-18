using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Units per second the player moves.")]
    [SerializeField] private float moveSpeed = 5f;

    private Vector2 input;

    private void Update()
    {
        input.x = Input.GetAxisRaw("Horizontal");
        input.y = Input.GetAxisRaw("Vertical");
        input = input.normalized;

        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
    }
}
