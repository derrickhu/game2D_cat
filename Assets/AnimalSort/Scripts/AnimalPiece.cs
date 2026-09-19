using UnityEngine;

public class AnimalPiece : MonoBehaviour
{
    public AnimalType Type { get; private set; }

    SpriteRenderer body;
    Animator animator;

    public void Setup(AnimalType type, RuntimeAnimatorController idle)
    {
        Type = type;
        name = "Animal_" + type;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        Transform visual = transform.Find("Visual");
        if (visual == null)
        {
            var go = new GameObject("Visual");
            visual = go.transform;
            visual.SetParent(transform, false);
        }

        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;

        body = visual.GetComponent<SpriteRenderer>();
        if (body == null)
            body = visual.gameObject.AddComponent<SpriteRenderer>();

        body.sprite = AnimalSpriteFactory.Get(type);
        body.sortingOrder = 20;

        animator = visual.GetComponent<Animator>();
        if (animator == null)
            animator = visual.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = idle;
        animator.enabled = false;
    }

    public void SetSorting(int order)
    {
        if (body != null)
            body.sortingOrder = order;
    }

    public void SetHeld(bool held)
    {
        if (animator == null) return;
        animator.enabled = held;
        if (!held)
        {
            Transform visual = transform.Find("Visual");
            if (visual != null)
            {
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;
            }
        }
    }
}
