using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIRect;

public class SphereMenu : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject menuItemPrefab;
    public float radius = 200f;
    public int subdivisions = 1; // 0 = icosahedron (12 vertices), 1 = 42 vertices, 2 = 162 vertices
    public float rotationSpeed = 0.2f;
    public float drag = 2f;

    [Header("Item colors")]
    // Fill colors for the menu items, picked at random per item. Defaults to the supplied palette.
    public Color[] itemColors = new Color[]
    {
        new Color(0.294f, 0.200f, 0.161f), // #4B3329 cocoa brown
        new Color(0.227f, 0.180f, 0.212f), // #3A2E36 charcoal eggplant
        new Color(0.647f, 0.416f, 0.420f), // #A56A6B dusty rose
        new Color(0.357f, 0.173f, 0.220f), // #5B2C38 wine
        new Color(0.431f, 0.290f, 0.325f), // #6E4A53 muted plum
        new Color(0.290f, 0.212f, 0.412f), // #4A3669 indigo
        new Color(0.737f, 0.361f, 0.275f), // #BC5C46 terracotta
        new Color(0.486f, 0.373f, 0.486f), // #7C5F7C dusty lavender
    };

    [Header("Click scale wave")]
    public float scaleDownAmount = 0.6f;    // size at the dip, as a fraction of the rest size
    public float scaleDuration = 0.2f;      // duration of each scale-down / scale-up tween
    public float staggerPerUnit = 0.0015f;  // extra start delay (s) per unit distance from the clicked item
    public float scaleHoldDuration = 2f;    // time spent dipped before items scale back up

    private Transform itemsContainer;
    private List<UIRectImage> menuItems = new List<UIRectImage>();
    private List<Vector3> menuItemRestScales = new List<Vector3>();
    private readonly List<Coroutine> scaleRoutines = new List<Coroutine>();
    private Vector2 lastDragPosition;
    private Vector2 angularVelocity;
    private bool isDragging;

    private Camera eventCamera;

    private const float VelocitySmoothing = 0.2f;

    void Start()
    {
        CreateItemsContainer();
        GenerateMenuItems();
        SetupHoverRaycasting();
    }

    // Items face outward (LookRotation along the outward normal), so the near/visible items
    // face the camera. The GraphicRaycaster's ignoreReversedGraphics (on by default) treats
    // those as back-facing and skips them, leaving only the far items hittable — so hover never
    // lands on the item you actually see. Disable that filter and instead drive hit-testing
    // ourselves via raycastTarget in UpdateItemHoverFacing (front-facing items only).
    void SetupHoverRaycasting()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        eventCamera = canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        GraphicRaycaster raycaster = GetComponentInParent<GraphicRaycaster>();
        if (raycaster != null)
            raycaster.ignoreReversedGraphics = false;
    }

    // Each frame, make only the camera-facing (near, visible) items raycast targets. This way
    // the near item wins the hover even when a far item overlaps it on screen, and a hovered
    // item that rotates to the back drops out (firing OnPointerExit, reverting its border).
    void UpdateItemHoverFacing()
    {
        if (eventCamera == null)
            return;

        Vector3 camForward = eventCamera.transform.forward;
        foreach (UIRectImage item in menuItems)
        {
            bool facingCamera = Vector3.Dot(camForward, item.transform.forward) < 0f;
            item.raycastTarget = facingCamera;
        }
    }

    // Random fill color drawn from the palette. Falls back to the previous random-HSV behavior
    // if no palette colors are configured.
    Color PickItemColor()
    {
        if (itemColors == null || itemColors.Length == 0)
            return Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.7f, 1f);
        return itemColors[Random.Range(0, itemColors.Length)];
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

            UIRectImage uiRect = item.GetComponent<UIRectImage>();
            uiRect.fillColor = PickItemColor();
            uiRect.borderAlign = BorderAlign.Middle;
            menuItems.Add(uiRect);
            menuItemRestScales.Add(item.transform.localScale);

            SphereMenuItem menuItem = item.AddComponent<SphereMenuItem>();
            menuItem.menu = this;
        }
    }

    // Plays two staggered scale "ripples" that both spread outward from the clicked item:
    // a shrink wave on click, then a grow wave that begins scaleHoldDuration after the click.
    // Both phases are scheduled in absolute time from the click, so the grow ripple stays a
    // clean travelling wave (anchored at ~2s for the clicked item) instead of each item just
    // independently holding. Re-clicking restarts the wave from each item's current scale.
    public void OnItemClicked(SphereMenuItem clickedItem)
    {
        foreach (Coroutine routine in scaleRoutines)
            if (routine != null)
                StopCoroutine(routine);
        scaleRoutines.Clear();

        float clickTime = Time.time;
        Vector3 clickedPosition = clickedItem.transform.localPosition;
        for (int i = 0; i < menuItems.Count; i++)
        {
            Transform itemTransform = menuItems[i].transform;
            float distance = Vector3.Distance(itemTransform.localPosition, clickedPosition);
            float ripple = distance * staggerPerUnit;     // wave-front offset for this item
            float downAt = ripple;                         // shrink ripple, from the click
            float upAt = scaleHoldDuration + ripple;       // grow ripple, same outward order
            scaleRoutines.Add(StartCoroutine(
                ScaleWave(itemTransform, menuItemRestScales[i], clickTime, downAt, upAt)));
        }
    }

    IEnumerator ScaleWave(Transform target, Vector3 restScale, float clickTime, float downAt, float upAt)
    {
        yield return WaitUntil(clickTime + downAt);
        yield return ScaleTo(target, restScale * scaleDownAmount, scaleDuration);

        yield return WaitUntil(clickTime + upAt);
        yield return ScaleTo(target, restScale, scaleDuration);
    }

    // Yields until the given absolute (Time.time) moment, so both ripple fronts stay anchored to
    // the click rather than drifting by however long each scale tween happens to take.
    IEnumerator WaitUntil(float time)
    {
        while (Time.time < time)
            yield return null;
    }

    // Smoothly scales from the current localScale to `to`, reading the start fresh so an
    // interrupted wave continues from wherever the item happens to be.
    IEnumerator ScaleTo(Transform target, Vector3 to, float duration)
    {
        Vector3 from = target.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            target.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }
        target.localScale = to;
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
        UpdateItemHoverFacing();

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
