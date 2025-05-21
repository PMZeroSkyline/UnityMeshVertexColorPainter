// COPYRIGHT (C) 2025, 2025 PMZeroSkyline. ALL RIGHTS RESERVED.
using UnityEngine;
using UnityEditor;
using System.Reflection;

public class MeshVertexColorPainterWindow : EditorWindow
{
    private GameObject targetObject;
    private MeshFilter targetMeshFilter;
    private float brushSize = 0.1f;
    private float brushFalloff = 0.5f;
    private Color paintColor = Color.red;
    private bool isPainting = false;
    private bool R = true;
    private bool G = true;
    private bool B = true;
    private bool A = true;

    private delegate bool HandleUtility_IntersectRayMesh(Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit);
    private static HandleUtility_IntersectRayMesh IntersectRayMesh = null;

    [MenuItem("Window/Mesh Vertex Color Painter")]
    public static void ShowWindow()
    {
        GetWindow<MeshVertexColorPainterWindow>("Vertex Color Painter");
    }
    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        SetupIntersectRayMesh();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Vertex Color Painter", EditorStyles.boldLabel);
        if (Selection.activeGameObject != null && Selection.activeGameObject != targetObject)
        {
            targetObject = Selection.activeGameObject;
        }
        if (targetObject != null)
        {
            targetMeshFilter = targetObject.GetComponent<MeshFilter>();
            if (targetMeshFilter == null)
            {
                EditorGUILayout.HelpBox("Selected object does not have a MeshFilter.", MessageType.Error);
            }
        }
        brushSize = EditorGUILayout.Slider("Brush Size", brushSize, 0.01f, 100.0f);
        brushFalloff = EditorGUILayout.Slider("Brush Falloff", brushFalloff, 0.01f, 1.0f);
        paintColor = EditorGUILayout.ColorField("Paint Color", paintColor);
        R = EditorGUILayout.Toggle("R", R);
        G = EditorGUILayout.Toggle("G", G);
        B = EditorGUILayout.Toggle("B", B);
        A = EditorGUILayout.Toggle("A", A);

        EditorGUILayout.HelpBox("Left-click in Scene view to paint. Ctrl-Z Undo.", MessageType.Info);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null || IntersectRayMesh == null)
            return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (IntersectRayMesh(ray, targetMeshFilter.sharedMesh, targetMeshFilter.transform.localToWorldMatrix, out RaycastHit hit))
        {
            Handles.color = new Color(0.1f, 0.1f, 0.1f, 0.1f);
            Handles.DrawSolidDisc(hit.point, hit.normal, brushSize);
            Handles.color = Color.white;
            Handles.DrawWireDisc(hit.point, hit.normal, brushSize);
            Handles.DrawWireDisc(hit.point, hit.normal, (1.0f - brushFalloff) * brushSize);

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                isPainting = true;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                isPainting = false;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && isPainting && !e.alt)
            {
                PaintVertexColors(targetMeshFilter, hit);
                e.Use();
            }
        }
    }
    void OnUndoRedo()
    {
        Debug.Log("Undo");
        if (targetMeshFilter != null && targetMeshFilter.sharedMesh != null)
        {
            Mesh mesh = targetMeshFilter.sharedMesh;
            mesh.colors = mesh.colors; // force set dirty
            EditorUtility.SetDirty(mesh);
        }
    }
    private void PaintVertexColors(MeshFilter meshFilter, RaycastHit hit)
    {
        Mesh mesh = meshFilter.sharedMesh;

        Vector3[] vertices = mesh.vertices;
        Color[] colors = mesh.colors.Length == vertices.Length ? mesh.colors : new Color[vertices.Length];
        // Vector3 localHitPoint = meshFilter.transform.InverseTransformPoint(hit.point);

        Undo.RecordObject(mesh, "Paint Vertex Colors");

        for (int i = 0; i < vertices.Length; i++)
        {
            // float dist = Vector3.Distance(vertices[i], localHitPoint) / meshFilter.transform.lossyScale.x;
            float dist = Vector3.Distance(meshFilter.transform.TransformPoint(vertices[i]), hit.point);

            if (dist > brushSize) continue;

            float falloff = Mathf.Clamp01(1.0f - dist / brushSize);
            falloff = Mathf.Pow(falloff, 1.0f / Mathf.Max(0.001f, brushFalloff));
            // colors[i] = Color.Lerp(colors[i], paintColor, falloff);
            colors[i] = new Color(  Mathf.Lerp(colors[i].r, paintColor.r, R ? falloff : 0.0f),
                                    Mathf.Lerp(colors[i].g, paintColor.g, G ? falloff : 0.0f),
                                    Mathf.Lerp(colors[i].b, paintColor.b, B ? falloff : 0.0f),
                                    Mathf.Lerp(colors[i].a, paintColor.a, A ? falloff : 0.0f));
        }

        mesh.colors = colors;
        EditorUtility.SetDirty(mesh);
    }

    private void SetupIntersectRayMesh()
    {
        if (IntersectRayMesh != null) return;

        MethodInfo method = typeof(HandleUtility).GetMethod(
            "IntersectRayMesh",
            BindingFlags.Static | BindingFlags.NonPublic,
            null,
            new[] { typeof(Ray), typeof(Mesh), typeof(Matrix4x4), typeof(RaycastHit).MakeByRefType() },
            null
        );

        if (method != null)
        {
            IntersectRayMesh = (Ray ray, Mesh mesh, Matrix4x4 matrix, out RaycastHit hit) =>
            {
                object[] parameters = new object[] { ray, mesh, matrix, null };
                bool result = (bool)method.Invoke(null, parameters);
                hit = (RaycastHit)parameters[3];
                return result;
            };
        }
        else
        {
            Debug.LogError("Could not find HandleUtility.IntersectRayMesh via reflection.");
        }
    }
}
