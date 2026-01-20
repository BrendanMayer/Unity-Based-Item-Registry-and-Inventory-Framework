using System.Collections.Generic;
using UnityEngine;

public class BuildingSystem : MonoBehaviour
{
    [Header("Prefabs & Layers")]
    [SerializeField] private GameObject prefabToBuild;
    [SerializeField] private LayerMask buildableSurfaces;   // what the ray is allowed to hit
    [SerializeField] private LayerMask obstructionLayers;   // what blocks placement

    [Header("Preview Look")]
    [SerializeField, Tooltip("Color when placement is valid")] private Color validTint = new Color(0f, 1f, 0f, 0.35f);
    [SerializeField, Tooltip("Color when placement is invalid")] private Color invalidTint = new Color(1f, 0f, 0f, 0.35f);

    [Header("Placement Options")]
    [SerializeField] private float rayDistance = 100f;
    [SerializeField, Tooltip("Snap world position to this grid (0 = no snapping).")]
    private float gridSize = 0.5f;
    [SerializeField, Tooltip("Extra padding when checking for overlaps.")]
    private Vector3 overlapPadding = new Vector3(0.01f, 0.01f, 0.01f);
    public float GridSize => gridSize;

    [Header("Scroll Rotation")]
    [SerializeField, Tooltip("Degrees to rotate per scroll step (positive scroll rotates +step, negative rotates -step).")]
    private float scrollStepDegrees = 15f;

    public bool IsBuilding { get; private set; }

    // --- internals ---
    private Camera _cam;
    private GameObject _preview;
    private Renderer[] _previewRenderers;
    private Collider[] _previewColliders;
    private MaterialPropertyBlock _mpb;
    private Bounds _previewBoundsLocal; // local-space AABB of the combined render bounds
    private static readonly int _colorID = Shader.PropertyToID("_BaseColor"); // works for URP Lit; fallback handled below
    private Inventory inventory;

    // accumulated user spin (in degrees) applied around the surface normal
    private float _userSpinDeg;

    // temp buffer for overlap queries (no GC)
    private readonly Collider[] _overlapBuffer = new Collider[32];

    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null)
            Debug.LogWarning("BuildingSystem: No Camera.main found. Assign _cam manually.");

        _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        inventory = GetComponent<Inventory>();
    }

    void Update()
    {
        if (!IsBuilding || _preview == null) return;

        // Ray from center of screen
        Ray ray = _cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, rayDistance, buildableSurfaces, QueryTriggerInteraction.Ignore))
        {
            Vector3 pos = hit.point;

            // Base rotation: align object's up to the surface normal only (no camera-facing)
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, hit.normal);

            // Read scroll and apply fixed-step rotation around the surface normal
            float scroll = PlayerInput.instance.ReadScrollValue(); // expects your method returning positive/negative delta
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                // rotate +step for scroll>0, -step for scroll<0
                _userSpinDeg += Mathf.Sign(scroll) * scrollStepDegrees;

                // keep value tidy (0..360)
                _userSpinDeg = Mathf.Repeat(_userSpinDeg + 360f, 360f);
            }

            // apply user spin around the hit normal
            rot = Quaternion.AngleAxis(_userSpinDeg, hit.normal) * rot;

            // grid snap position
            if (gridSize > 0f)
                pos = Snap(pos, gridSize);

            // rest the prefab on the surface
            Vector3 offset = GetBottomOffsetAlongNormal(rot, hit.normal);
            _preview.transform.SetPositionAndRotation(pos + offset, rot);

            // placement validity
            bool isValid = IsOnBuildableSurface(hit) && !IsOverlappingWorld();
            SetPreviewTint(isValid ? validTint : invalidTint);

            // place on confirm
            if (isValid && PlayerInput.instance.ConfirmBuild())
            {
                Place(pos + offset, rot);
            }
        }
        else
        {
            SetPreviewTint(invalidTint);
        }
    }

    public void ToggleBuildingMode(GameObject buildableObject)
    {
        IsBuilding = !IsBuilding;
        prefabToBuild = buildableObject;

        if (IsBuilding)
        {
            _userSpinDeg = 0f; // reset spin when entering build mode
            CreateOrRecyclePreview();
        }
        else
        {
            DestroyPreview();
        }
    }

    private void CreateOrRecyclePreview()
    {
        if (prefabToBuild == null)
        {
            Debug.LogWarning("BuildingSystem: No prefabToBuild assigned.");
            IsBuilding = false;
            return;
        }

        if (_preview != null) return;

        _preview = Instantiate(prefabToBuild);
        _preview.name = $"{prefabToBuild.name}_Preview";
        _preview.layer = gameObject.layer; // optional: separate layer

        // Disable colliders so the preview doesn’t collide with itself
        _previewColliders = _preview.GetComponentsInChildren<Collider>(true);
        foreach (var c in _previewColliders) c.enabled = false;

        // Cache renderers and compute combined local bounds
        _previewRenderers = _preview.GetComponentsInChildren<Renderer>(true);
        _previewBoundsLocal = CalculateLocalAABB(_preview, _previewRenderers);

        // make it semi-transparent via MPB (don’t swap materials each frame)
        SetPreviewTint(invalidTint);
    }

    private void DestroyPreview()
    {
        if (_preview != null) Destroy(_preview);
        _preview = null;
        _previewRenderers = null;
        _previewColliders = null;
    }

    public event System.Action OnPlaced;

    private void Place(Vector3 pos, Quaternion rot)
    {
        var go = Instantiate(prefabToBuild, pos, rot);

        // Fire the event so Inventory (or anything else) can respond
        OnPlaced?.Invoke();
    }

    private bool IsOnBuildableSurface(RaycastHit surfaceHit)
    {
        // Already filtered by buildableSurfaces. Add extra rules if needed.
        return true;
    }

    private bool IsOverlappingWorld()
    {
        if (_previewRenderers == null || _previewRenderers.Length == 0) return false;

        // World-space oriented box based on preview’s bounds
        Bounds world = TransformBounds(_preview.transform.localToWorldMatrix, _previewBoundsLocal);
        world.Expand(overlapPadding);

        // Use OverlapBoxNonAlloc to avoid GC; ignore triggers by default
        int count = Physics.OverlapBoxNonAlloc(
            world.center,
            world.extents,
            _overlapBuffer,
            _preview.transform.rotation,
            obstructionLayers,
            QueryTriggerInteraction.Ignore
        );

        // We disabled the preview’s own colliders, so any hit means we’re overlapping something else
        return count > 0;
    }

    private void SetPreviewTint(Color c)
    {
        if (_previewRenderers == null) return;

        foreach (var r in _previewRenderers)
        {
            // Try URP/HDRP "_BaseColor", fallback to standard "_Color"
            _mpb.Clear();
            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(_colorID))
                _mpb.SetColor(_colorID, c);
            else
                _mpb.SetColor("_Color", c);

            r.SetPropertyBlock(_mpb);
        }
    }

    // --- helpers ---

    private static Vector3 Snap(Vector3 v, float step)
    {
        v.x = Mathf.Round(v.x / step) * step;
        v.y = Mathf.Round(v.y / step) * step;
        v.z = Mathf.Round(v.z / step) * step;
        return v;
    }

    private static Bounds CalculateLocalAABB(GameObject root, Renderer[] renderers)
    {
        var m = root.transform.worldToLocalMatrix;
        bool hasAny = false;
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);

        foreach (var r in renderers)
        {
            Bounds w = r.bounds;
            // Convert world-space AABB corners into local, then recompute AABB
            Vector3[] corners = new Vector3[8];
            GetAABBCorners(w, corners);
            for (int i = 0; i < 8; i++)
            {
                Vector3 pLocal = m.MultiplyPoint3x4(corners[i]);
                if (!hasAny) { local = new Bounds(pLocal, Vector3.zero); hasAny = true; }
                else local.Encapsulate(pLocal);
            }
        }
        if (!hasAny) local = new Bounds(Vector3.zero, Vector3.zero);
        return local;
    }

    private static Bounds TransformBounds(Matrix4x4 matrix, Bounds local)
    {
        // Transform an AABB by matrix by transforming corners and recomputing an AABB
        Vector3[] corners = new Vector3[8];
        GetAABBCorners(local, corners);
        Bounds b = new Bounds(matrix.MultiplyPoint3x4(corners[0]), Vector3.zero);
        for (int i = 1; i < 8; i++)
            b.Encapsulate(matrix.MultiplyPoint3x4(corners[i]));
        return b;
    }

    private static void GetAABBCorners(Bounds b, IList<Vector3> dst8)
    {
        Vector3 min = b.min, max = b.max;
        dst8[0] = new Vector3(min.x, min.y, min.z);
        dst8[1] = new Vector3(max.x, min.y, min.z);
        dst8[2] = new Vector3(min.x, max.y, min.z);
        dst8[3] = new Vector3(max.x, max.y, min.z);
        dst8[4] = new Vector3(min.x, min.y, max.z);
        dst8[5] = new Vector3(max.x, min.y, max.z);
        dst8[6] = new Vector3(min.x, max.y, max.z);
        dst8[7] = new Vector3(max.x, max.y, max.z);
    }

    // Return an offset so the prefab *rests* on the surface along `normal`.
    private Vector3 GetBottomOffsetAlongNormal(Quaternion rotation, Vector3 normal)
    {
        normal = normal.normalized;

        // Transform local AABB corners by rotation only (pivot at origin), then
        // find the smallest projection on the normal. That tells us how deep the
        // lowest corner is along the normal. Offset by -min to bring it to 0.
        Vector3[] corners = new Vector3[8];
        GetAABBCorners(_previewBoundsLocal, corners);

        float minDot = float.PositiveInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 worldDir = rotation * corners[i]; // no translation
            float d = Vector3.Dot(worldDir, normal);
            if (d < minDot) minDot = d;
        }

        const float skin = 0.001f;
        return normal * (-minDot + skin);
    }
}
