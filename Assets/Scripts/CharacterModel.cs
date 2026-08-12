using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterModel : MonoBehaviour
{
    public Animator animator;
    public InputActionReference moveAction; // drag your thumbstick/move action here

    private void Update()
    {
        Vector2 input = moveAction.action.ReadValue<Vector2>();
        bool walking = input.magnitude > 0.1f; // adjust threshold as needed
        animator.SetBool("isWalking", walking);
    }
}