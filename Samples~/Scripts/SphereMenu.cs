using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SphereMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject menuItemPrefab;
    public float radius = 200f;
    public int subdivisions = 1; // 0 = icosahedron (12 vertices), 1 = 42 vertices, 2 = 162 vertices
    public float rotationSpeed = 0.2f;
    public float drag = 2f;

    private Transform itemsContainer;
    private List<UIRect> menuItems = new List<UIRect>();
    private Vector2 lastDragPosition;
    private Vector2 angularVelocity;
    private bool isDragging;

    private const float VelocitySmoothing = 0.2f;

    void Start()
    {
        CreateItemsContainer();
        GenerateMenuItems();
    }

    void CreateItemsContainer()
    {
        GameObject container = new GameObject("ItemsContainer");
        itemsContainer = container.transform;
        itemsContainer.SetParent(transform, false);
        itemsContainer.localPosition = Vector3.zero;
        itemsContainer.localRotation = Quaternion.identity;
        itemsContainer.localScale = Vector3.one;
    }

    void GenerateMenuItems()
    {
        List<Vector3> vertices = GenerateIcosphereVertices(subdivisions);

        foreach (Vector3 vertex in vertices)
        {
            Vector3 localPosition = vertex * radius;
            Vector3 outwardNormal = vertex.normalized;
            Quaternion localRotation = Quaternion.LookRotation(outwardNormal, Vector3.up);

            GameObject item = Instantiate(menuItemPrefab, itemsContainer);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = localRotation;

            UIRect uiRect = item.GetComponent<UIRect>();
            uiRect.fillColor = Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.7f, 1f);
            menuItems.Add(uiRect);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        angularVelocity = Vector2.zero;
        lastDragPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.position - lastDragPosition;
        lastDragPosition = eventData.position;

        // Convert pixel delta to rotation, then to angular velocity (degrees/sec)
        Vector2 rotationThisFrame = new Vector2(delta.y, -delta.x) * rotationSpeed;
        Vector2 frameAngularVelocity = rotationThisFrame / Time.deltaTime;
        angularVelocity = Vector2.Lerp(angularVelocity, frameAngularVelocity, VelocitySmoothing);
        // Rotation applied in Update for consistency
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    List<Vector3> GenerateIcosphereVertices(int subdivisionLevel)
    {
        // Golden ratio
        float t = (1f + Mathf.Sqrt(5f)) / 2f;

        // Icosahedron vertices
        List<Vector3> vertices = new List<Vector3>
        {
            new Vector3(-1,  t,  0).normalized,
            new Vector3( 1,  t,  0).normalized,
            new Vector3(-1, -t,  0).normalized,
            new Vector3( 1, -t,  0).normalized,
            new Vector3( 0, -1,  t).normalized,
            new Vector3( 0,  1,  t).normalized,
            new Vector3( 0, -1, -t).normalized,
            new Vector3( 0,  1, -t).normalized,
            new Vector3( t,  0, -1).normalized,
            new Vector3( t,  0,  1).normalized,
            new Vector3(-t,  0, -1).normalized,
            new Vector3(-t,  0,  1).normalized
        };

        // Icosahedron faces (triangles)
        List<int[]> faces = new List<int[]>
        {
            new int[] {0, 11, 5}, new int[] {0, 5, 1}, new int[] {0, 1, 7}, new int[] {0, 7, 10}, new int[] {0, 10, 11},
            new int[] {1, 5, 9}, new int[] {5, 11, 4}, new int[] {11, 10, 2}, new int[] {10, 7, 6}, new int[] {7, 1, 8},
            new int[] {3, 9, 4}, new int[] {3, 4, 2}, new int[] {3, 2, 6}, new int[] {3, 6, 8}, new int[] {3, 8, 9},
            new int[] {4, 9, 5}, new int[] {2, 4, 11}, new int[] {6, 2, 10}, new int[] {8, 6, 7}, new int[] {9, 8, 1}
        };

        // Subdivide
        for (int i = 0; i < subdivisionLevel; i++)
        {
            List<int[]> newFaces = new List<int[]>();
            Dictionary<long, int> midpointCache = new Dictionary<long, int>();

            foreach (int[] face in faces)
            {
                int a = GetMidpoint(face[0], face[1], ref vertices, midpointCache);
                int b = GetMidpoint(face[1], face[2], ref vertices, midpointCache);
                int c = GetMidpoint(face[2], face[0], ref vertices, midpointCache);

                newFaces.Add(new int[] {face[0], a, c});
                newFaces.Add(new int[] {face[1], b, a});
                newFaces.Add(new int[] {face[2], c, b});
                newFaces.Add(new int[] {a, b, c});
            }
            faces = newFaces;
        }

        return vertices;
    }

    int GetMidpoint(int p1, int p2, ref List<Vector3> vertices, Dictionary<long, int> cache)
    {
        long smallerIndex = Mathf.Min(p1, p2);
        long greaterIndex = Mathf.Max(p1, p2);
        long key = (smallerIndex << 16) + greaterIndex;

        if (cache.TryGetValue(key, out int ret))
            return ret;

        Vector3 middle = ((vertices[p1] + vertices[p2]) / 2f).normalized;
        int index = vertices.Count;
        vertices.Add(middle);
        cache[key] = index;
        return index;
    }

    void Update()
    {
        // Always decay - handles "stopped moving but still holding" case
        // During active drag, OnDrag sets velocity fresh so decay doesn't matter
        angularVelocity *= Mathf.Exp(-drag * Time.deltaTime);

        // Apply rotation (whether dragging or not) - single source of truth
        if (angularVelocity.sqrMagnitude > 0.001f)
        {
            Vector2 rotation = angularVelocity * Time.deltaTime;
            itemsContainer.Rotate(Vector3.right, rotation.x, Space.World);
            itemsContainer.Rotate(Vector3.up, rotation.y, Space.World);
        }
    }
}
