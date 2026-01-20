using UnityEngine;

/// <summary>
/// Renders a dynamic XZ world grid under the camera using a single quad + grid shader.
/// Matches BuildingSystem's gridSize if provided.
/// </summary>
[ExecuteAlways]
public class GridOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private BuildingSystem buildingSystem; // optional; reads grid size if set
    [SerializeField] private Material gridMaterial;         // use the GridOverlay shader below

    [Header("Grid Settings")]
    [SerializeField, Tooltip("Grid cell size (overridden by BuildingSystem if assigned)")]
    private float cellSize = 0.5f;
    [SerializeField, Tooltip("Draw a thicker line every N cells")]
    private int majorLineEvery = 4;
    [SerializeField, Tooltip("World-space size of the grid quad around the camera")]
    private float gridWorldSize = 100f;
    [SerializeField, Tooltip("Vertical offset to avoid z-fighting with ground")]
    private float yOffset = 0.01f;
    [SerializeField, Tooltip("Rotate the grid; 0 keeps it on XZ plane")]
    private float gridYaw = 0f;

    [Header("Appearance")]
    [SerializeField] private Color minorColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color majorColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField, Range(0.0005f, 0.02f)] private float minorThickness = 0.003f; // in UV space
    [SerializeField, Range(0.0005f, 0.03f)] private float majorThickness = 0.006f;
    [SerializeField, Tooltip("Fade out with distance from center of the quad (0 = off)")]
    [Range(0f, 1f)] private float radialFade = 0.4f;

    // internals
    private Mesh _quadMesh;
    private Matrix4x4 _matrix;
    private static readonly int _CellSizeID = Shader.PropertyToID("_CellSize");
    private static readonly int _MajorEveryID = Shader.PropertyToID("_MajorEvery");
    private static readonly int _MinorColorID = Shader.PropertyToID("_MinorColor");
    private static readonly int _MajorColorID = Shader.PropertyToID("_MajorColor");
    private static readonly int _MinorThicknessID = Shader.PropertyToID("_MinorThickness");
    private static readonly int _MajorThicknessID = Shader.PropertyToID("_MajorThickness");
    private static readonly int _RadialFadeID = Shader.PropertyToID("_RadialFade");

    private void OnEnable()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        EnsureMesh();
    }

    private void OnDisable()
    {
        if (_quadMesh != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(_quadMesh);
            else Destroy(_quadMesh);
#else
            Destroy(_quadMesh);
#endif
            _quadMesh = null;
        }
    }

    private void Start()
    {
        buildingSystem = GetComponent<BuildingSystem>();
        targetCamera = Camera.main;
    }

    private void Update()
    {
        if (gridMaterial == null || targetCamera == null) return;

        // If a BuildingSystem is assigned, take its grid size (ignore if 0 = no snapping)
        float size = (buildingSystem != null && buildingSystem.GridSize > 0f)
            ? buildingSystem.GridSize
            : Mathf.Max(0.01f, cellSize);

        // Set shader params
        gridMaterial.SetFloat(_CellSizeID, size);
        gridMaterial.SetInt(_MajorEveryID, Mathf.Max(1, majorLineEvery));
        gridMaterial.SetColor(_MinorColorID, minorColor);
        gridMaterial.SetColor(_MajorColorID, majorColor);
        gridMaterial.SetFloat(_MinorThicknessID, minorThickness);
        gridMaterial.SetFloat(_MajorThicknessID, majorThickness);
        gridMaterial.SetFloat(_RadialFadeID, radialFade);

        // Keep the grid centered under the camera on the XZ plane (with optional yaw)
        Vector3 center = targetCamera.transform.position;
        center.y = 0f; // XZ plane at y=0; move if you want a different height

        // Snap the grid origin to cell size so lines stay stable as you move
        center.x = Mathf.Round(center.x / size) * size;
        center.z = Mathf.Round(center.z / size) * size;

        // Build matrix: translate to center, rotate yaw, scale to size
        Quaternion rot = Quaternion.Euler(0f, gridYaw, 0f);
        Vector3 pos = new Vector3(center.x, yOffset, center.z);
        Vector3 scl = new Vector3(gridWorldSize, 1f, gridWorldSize);
        _matrix = Matrix4x4.TRS(pos, rot, scl);
    }

    private void OnRenderObject()
    {
        if (gridMaterial == null || _quadMesh == null) return;

        // Render after opaque; tweak queue via material if needed
        gridMaterial.SetPass(0);
        Graphics.DrawMeshNow(_quadMesh, _matrix);
    }

    private void EnsureMesh()
    {
        if (_quadMesh != null) return;

        // A simple unit quad centered at origin on XZ, UVs 0..1 (shader tiles UVs by scale)
        _quadMesh = new Mesh { name = "GridOverlay_Quad" };
        _quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f,  0.5f),
            new Vector3( 0.5f, 0f,  0.5f),
            new Vector3( 0.5f, 0f, -0.5f),
        };
        _quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
        };
        _quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        _quadMesh.RecalculateNormals();
        _quadMesh.RecalculateBounds();
    }
}
