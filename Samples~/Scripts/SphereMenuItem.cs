using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UIRect;

/// <summary>
/// Adds a hover stroke to a single SphereMenu item. On pointer enter it animates a stroke in,
/// on pointer exit it animates the stroke back out. Only the stroke fields of the style are set,
/// so the item's fill color is left untouched. On press it pushes itself in (animating
/// <c>translate.z</c>) on mouse down and springs back out on mouse up, and on a genuine click it
/// tells the owning <see cref="SphereMenu"/> to play its scale wave.
/// </summary>
[RequireComponent(typeof(UIRectImage))]
public class SphereMenuItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [FormerlySerializedAs("hoverBorderColor")] public Color hoverStrokeColor = Color.white;
    [FormerlySerializedAs("hoverBorderWidth")] public float hoverStrokeWidth = 4f;
    public float animDuration = 0.1f;

    [Header("Press push")]
    // How far (local units) the rect is pushed in along -Z (toward the sphere centre, away from
    // the viewer) while held down. The shader-space translate is animated, so layout is unaffected.
    public float pushDepth = 10f;
    public float pushDuration = 0.06f;

    // Set by SphereMenu when the item is spawned (falls back to a parent lookup).
    [HideInInspector] public SphereMenu menu;

    private UIRectImage _uiRect;

    void Awake()
    {
        _uiRect = GetComponent<UIRectImage>();
        if (menu == null)
            menu = GetComponentInParent<SphereMenu>();
    }

    // Push the rect in (-Z) on mouse down; held while the button stays pressed. Only Translate is
    // set, so the hover stroke and fill are left untouched.
    public void OnPointerDown(PointerEventData eventData)
    {
        _uiRect.AnimateTo(new UIRectStyle { Translate = new Vector3(0f, 0f, -pushDepth) }, pushDuration);
    }

    // Spring the rect back out on release. OnPointerUp is delivered to the pressed item even if the
    // pointer has dragged off it, so the push always reverts.
    public void OnPointerUp(PointerEventData eventData)
    {
        _uiRect.AnimateTo(new UIRectStyle { Translate = Vector3.zero }, pushDuration);
    }

    // A genuine click (press + release without a drag) triggers the menu's scale wave.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (menu != null)
            menu.OnItemClicked(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _uiRect.AnimateTo(new UIRectStyle
        {
            StrokeColor = hoverStrokeColor,
            StrokeWidth = hoverStrokeWidth,
        }, animDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _uiRect.AnimateTo(new UIRectStyle
        {
            StrokeColor = hoverStrokeColor,
            StrokeWidth = 0f,
        }, animDuration);
    }
}
