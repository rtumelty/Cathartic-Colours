using UnityEngine;

public class Block : MonoBehaviour
{
    [SerializeField] private Animator animator;

    private int size = 0;

    public int Size
    {
        get => size;
        set
        {
            size = value;
            animator.SetInteger("Size", value);
        }
    }

    public void Disappear()
    {
        animator.SetTrigger("Disappear");
    }

    public void Dispose()
    {
        Destroy(gameObject);
    }
}
