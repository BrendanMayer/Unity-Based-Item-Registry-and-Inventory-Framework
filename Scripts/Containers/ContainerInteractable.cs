using UnityEngine;

public class ContainerInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string containerName = "Container";
    [SerializeField] private ContainerInventory containerSlots;
    [SerializeField] private bool currentlyInteracting;
    [SerializeField] private Animator animator;

    public IInteractable.InteractionType interactionType => IInteractable.InteractionType.Container;
    public string displayText => "Open " + containerName;
    public int priority => 5;
    public bool canInteract(Interactor who) => true;

    private void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
    }

    public void Interact(Interactor who, bool isInteracting)
    {
        currentlyInteracting = isInteracting;

        if (currentlyInteracting)
        {
            UIManager.instance.OpenContainer(containerSlots);
            if (animator) animator.SetTrigger("Open");
        }
        else
        {
            UIManager.instance.ShowContainerUI(false);
            if (animator) animator.SetTrigger("Close");
        }
    }
}
