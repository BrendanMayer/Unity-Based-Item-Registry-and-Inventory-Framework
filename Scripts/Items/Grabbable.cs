using UnityEngine;

public class Grabbable : MonoBehaviour, IInteractable
{
    public bool interacting;

    public IInteractable.InteractionType interactionType => IInteractable.InteractionType.Grabbable;
    public string displayText => "Pick Up Item";
    public int priority => 2;
    public bool canInteract(Interactor who) => true;

    [Header("Hold settings")]
    public float holdDistance = 3f;
    public float followStrength = 15f;      // higher = snappier follow (exp smoothing)
    public bool alignToCamera = false;      // set true if you want it to face forward while held

    Rigidbody rb;

    // Velocity estimation
    Vector3 lastPos; 
    Quaternion lastRot;
    Vector3 estLinearVel;
    Vector3 estAngularVel; // in radians/sec

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastPos = transform.position;
        lastRot = transform.rotation;
    }

    public void Interact(Interactor who, bool isInteracting)
    {
        interacting = isInteracting;

        if (interacting)
        {
            // Start holding
            rb.isKinematic = true;          // we drive it manually while held
            estLinearVel = Vector3.zero;
            estAngularVel = Vector3.zero;
            lastPos = rb.position;
            lastRot = rb.rotation;
        }
        else
        {
            // Release: give it the last estimated velocities so it continues naturally
            rb.isKinematic = false;
            rb.linearVelocity = estLinearVel;
            rb.angularVelocity = estAngularVel;

            // Optional: add player's velocity so throws while moving feel right
            var playerRB = who.GetComponent<Rigidbody>();
            if (playerRB) rb.linearVelocity += playerRB.linearVelocity;
        }
    }

    void FixedUpdate()
    {
        if (!interacting) return;
        if (!Camera.main) return;

        // Target follow point 3m ahead of camera
        Vector3 camPos = Camera.main.transform.position;
        Vector3 camFwd = Camera.main.transform.forward;
        Vector3 targetPos = camPos + camFwd * holdDistance;

        // Exponential smoothing (frame-rate independent)
        float a = 1f - Mathf.Exp(-followStrength * Time.fixedDeltaTime);
        Vector3 newPos = Vector3.Lerp(rb.position, targetPos, a);

        // Optional rotation alignment while held
        Quaternion targetRot = alignToCamera
            ? Quaternion.LookRotation(camFwd, Vector3.up)
            : rb.rotation;
        Quaternion newRot = Quaternion.Slerp(rb.rotation, targetRot, a);

        // Estimate velocities BEFORE moving for this step
        estLinearVel = (newPos - rb.position) / Time.fixedDeltaTime;

        // Angular velocity from delta rotation
        Quaternion delta = newRot * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        if (Mathf.Abs(angleRad) > 1e-4f && axis.sqrMagnitude > 0f)
            estAngularVel = axis.normalized * (angleRad / Time.fixedDeltaTime);
        else
            estAngularVel = Vector3.zero;

        // Move while kinematic (no forces)
        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);

        lastPos = newPos;
        lastRot = newRot;
    }
}
