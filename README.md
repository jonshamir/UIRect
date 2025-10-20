# UIRect

A powerful, shader-based UI component for Unity that extends the standard Image component with advanced styling capabilities including rounded corners, borders, shadows, and smooth animations - all without external dependencies.

## Features

- **Rounded Corners** - Independent control for each corner radius
- **Borders** - Customizable width, color, and alignment (inside, middle, outside)
- **Shadows** - Soft shadows with size, spread, offset, and color control
- **Bevels** - Optional bevel effects for depth
- **Smooth Animations** - Built-in animation system with custom easing curves
- **GPU-Accelerated** - All rendering done in shader for optimal performance
- **No Dependencies** - Pure Unity implementation, no third-party packages required

## Installation

1. Add the package to your Unity project's `Packages` folder
2. Import into your project
3. Add a `UIRect` component to any UI GameObject (replaces Unity's Image component)

## Basic Usage

### Adding UIRect to Your UI

```csharp
// UIRect extends Unity's Image component
UIRect rect = gameObject.AddComponent<UIRect>();

// Set basic properties
rect.fillColor = Color.blue;
rect.radius = new Vector4(20, 20, 20, 20); // All corners 20px
rect.borderWidth = 5;
rect.borderColor = Color.white;
```

### Styling with UIRectStyle

Use `UIRectStyle` to define and apply styles:

```csharp
UIRectStyle cardStyle = new UIRectStyle
{
    BackgroundColor = Color.white,
    Radius = new Vector4(15, 15, 15, 15),
    BorderColor = new Color(0.8f, 0.8f, 0.8f),
    BorderWidth = 2,
    HasShadow = true,
    ShadowColor = new Color(0, 0, 0, 0.3f),
    ShadowSize = 10,
    ShadowOffset = new Vector3(0, -5, 0)
};

uiRect.Style = cardStyle;
```

## Animation System

UIRect includes a built-in animation system that smoothly interpolates between styles without requiring DOTween or any other animation library.

### Simple Animation

```csharp
UIRectStyle hoverStyle = new UIRectStyle
{
    BackgroundColor = Color.cyan,
    BorderWidth = 8,
    ShadowSize = 20
};

// Animate over 0.5 seconds with default easing
uiRect.AnimateTo(hoverStyle, 0.5f);
```

### Custom Easing

```csharp
// Create a bounce curve
AnimationCurve bounce = new AnimationCurve(
    new Keyframe(0, 0),
    new Keyframe(0.3f, 1.3f),  // Overshoot
    new Keyframe(0.5f, 0.8f),  // Undershoot
    new Keyframe(0.7f, 1.1f),  // Small bounce
    new Keyframe(1, 1)
);

uiRect.AnimateTo(hoverStyle, 1.0f, bounce);
```

### Animation with Callback

```csharp
uiRect.AnimateTo(targetStyle, 0.5f, null, () =>
{
    Debug.Log("Animation complete!");
});
```

### Built-in Easing Curves

```csharp
// Linear
uiRect.AnimateTo(style, 0.5f, AnimationCurve.Linear(0, 0, 1, 1));

// Ease In/Out (default)
uiRect.AnimateTo(style, 0.5f, AnimationCurve.EaseInOut(0, 0, 1, 1));

// Custom overshoot
AnimationCurve overshoot = new AnimationCurve(
    new Keyframe(0, 0),
    new Keyframe(0.6f, 1.4f),  // 40% overshoot
    new Keyframe(1, 1)
);
uiRect.AnimateTo(style, 0.5f, overshoot);
```

### Stopping Animations

```csharp
// Stop any running animation
uiRect.StopAnimation();
```

## Property Reference

### Background
- `fillColor` - Background color (Color)
- `radius` - Corner radii as Vector4 (top-left, top-right, bottom-right, bottom-left)
- `translate` - Position offset (Vector3)

### Border
- `borderColor` - Border color (Color)
- `borderWidth` - Border thickness in pixels (float)
- `borderAlign` - Border alignment: Inside, Middle, Outside (BorderAlign enum)

### Shadow
- `hasShadow` - Enable/disable shadow rendering (bool)
- `shadowColor` - Shadow color with alpha (Color)
- `shadowSize` - Shadow blur radius in pixels (float)
- `shadowSpread` - Shadow expansion before blur (float)
- `shadowOffset` - Shadow position offset (Vector3)

### Bevel
- `bevelWidth` - Bevel size in pixels (float)
- `bevelStrength` - Bevel intensity (float)

## UIRectStyle Properties

All properties in `UIRectStyle` are nullable (`Color?`, `float?`, etc.), allowing partial style updates:

```csharp
// Only change the border, leave everything else unchanged
UIRectStyle borderUpdate = new UIRectStyle
{
    BorderWidth = 10,
    BorderColor = Color.red
    // All other properties remain as they were
};

uiRect.Style = borderUpdate;
```

## Advanced Examples

### Creating a Button Hover Effect

```csharp
public class UIRectButton : MonoBehaviour
{
    public UIRect uiRect;

    private UIRectStyle normalStyle;
    private UIRectStyle hoverStyle;

    void Start()
    {
        normalStyle = new UIRectStyle
        {
            BackgroundColor = Color.white,
            BorderWidth = 2,
            BorderColor = Color.gray,
            Radius = new Vector4(10, 10, 10, 10),
            ShadowSize = 5
        };

        hoverStyle = new UIRectStyle
        {
            BackgroundColor = new Color(0.9f, 0.9f, 1f),
            BorderWidth = 3,
            BorderColor = Color.blue,
            Radius = new Vector4(10, 10, 10, 10),
            ShadowSize = 15
        };

        uiRect.Style = normalStyle;
    }

    void OnPointerEnter()
    {
        uiRect.AnimateTo(hoverStyle, 0.2f);
    }

    void OnPointerExit()
    {
        uiRect.AnimateTo(normalStyle, 0.2f);
    }
}
```

### Animating a Card Flip Effect

```csharp
IEnumerator FlipCard()
{
    // Shrink horizontally
    UIRectStyle shrinkStyle = new UIRectStyle { Radius = new Vector4(50, 0, 0, 50) };
    uiRect.AnimateTo(shrinkStyle, 0.15f);
    yield return new WaitForSeconds(0.15f);

    // Change content/color
    uiRect.fillColor = Color.red;

    // Expand back
    UIRectStyle expandStyle = new UIRectStyle { Radius = new Vector4(20, 20, 20, 20) };
    AnimationCurve bounce = AnimationCurve.EaseInOut(0, 0, 1, 1);
    uiRect.AnimateTo(expandStyle, 0.15f, bounce);
}
```

### Dynamic Shadow Based on Scroll Position

```csharp
void Update()
{
    float scrollAmount = scrollRect.verticalNormalizedPosition;

    UIRectStyle style = new UIRectStyle
    {
        ShadowSize = Mathf.Lerp(0, 20, scrollAmount),
        ShadowOffset = new Vector3(0, Mathf.Lerp(0, -10, scrollAmount), 0)
    };

    uiRect.Style = style;
}
```

## Performance Notes

- All rendering is GPU-accelerated via custom shader
- Animation updates occur in `Update()` only while animating
- Minimal CPU overhead during animations
- Supports batching with other UI elements using the same material

## Technical Details

- **Shader**: Uses custom UI shader with SDF-based rounded rectangle rendering
- **Vertex Data**: Packs style information into UV channels for shader access
- **Material Caching**: Shares materials between instances for optimal performance
- **Animation**: LerpUnclamped allows overshoot/undershoot effects in curves

## Compatibility

- Unity 2020.3 or later
- Works with Unity UI (uGUI)
- Compatible with Canvas rendering modes: Screen Space, World Space

## License

[Add your license information here]

## Credits

Created by Jon Shamir
