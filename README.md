# UIRect

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

UIRect is a rounded rectangle-drawing library for Unity UI (a.k.a. uGUI)
UIRect extends the standard Image component with styling capabilities including rounded corners, borders, shadows, and animations.
- Basic UI features you would expect - fill, rounding, border, shadow, etc.
- High-quality, antialiased rendering with
- Performant, no external dependencies
- Custom editor for tweaking in Unity
- Scripting API loosely inspired by CSS

## Why is this needed?

Although Unity has been pushing [UI Toolkit](https://docs.unity3d.com/6000.1/Documentation/Manual/UIElements.html) as the main UI framework for Unity apps, uGUI remains widely used for its simplicity, familiarity and true 3D world-space support.
However, creating basic effects such as borders and rounded corners remains surprisingly difficult with multiple assets and libraries, some paid and some free, with various issues (Not all effects are supported, no proper antialiasing, performance issues, etc.)
This package attempts to fix this by creating a free and open source UI primitive to build on.

## Features

- **Rounded Corners** - Independent control for each corner radius
- **Borders** - Customizable width, color, and alignment (inside, middle, outside)
- **Shadows** - Soft shadows with size, spread, offset, and color control
- **Bevels** - Parallax-mapped bevels with specular highlights
- **Smooth Animations** - Built-in animation system with custom easing curves
- **GPU-Accelerated** - All rendering done in shader for optimal performance
- **No Dependencies** - Pure Unity implementation, no third-party packages required
- **RawImage Variant** - `UIRectRawImage` for videos, RenderTextures, and other dynamic textures

## Installation

### Option 1: Unity Package Manager (Git URL)

1. Open Unity Package Manager (Window > Package Manager)
2. Click the `+` button and select "Add package from git URL..."
3. Enter: `https://github.com/jonshamir/UIRect.git`

### Option 2: Manual Installation

1. Clone or download this repository
2. Copy the repository folder into your project's `Packages` folder and rename it to `com.jonshamir.uirect`

### Getting Started

Add a `UIRectImage` component to any UI GameObject (replaces Unity's Image component), or use the menu: **GameObject > UI > UIRect**

## Basic Usage

### Adding UIRect to Your UI

```csharp
// UIRectImage extends Unity's Image component
UIRectImage rect = gameObject.AddComponent<UIRectImage>();

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
    FillColor = Color.white,
    Radius = new Vector4(15, 15, 15, 15),
    BorderColor = new Color(0.8f, 0.8f, 0.8f),
    BorderWidth = 2,
    Shadows = new()
    {
        new()
        {
            color = new(0, 0, 0, 0.3f),
            size = 10,
            offset = new(0, -5, 0)
        }
    }
};

uiRect.Style = cardStyle;
```

An element can have any number of shadows, outer or inner (`isInner = true`, like CSS `box-shadow: inset`).
List index 0 is drawn topmost; outer shadows always render behind the fill and inner shadows on top of it.

Any style attributes that are not included (or `null`) will not be overwritten, inspired by CSS.
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

## Animation
UIRect includes a built-in animation system that smoothly interpolates between styles without requiring an animation library.

### Simple Animation

```csharp
UIRectStyle hoverStyle = new UIRectStyle
{
    FillColor = Color.cyan,
    BorderWidth = 8,
    Shadows = new()
    {
        new()
        {
            color = new(0, 0, 0, 0.3f),
            size = 20,
            offset = new(0, -5, 0)
        }
    }
};

// Animate over 0.5 seconds with default easing
uiRect.AnimateTo(hoverStyle, 0.5f);
```

Shadows animate index-by-index: give the start and end styles a `Shadows` list of the same
length and matching entries interpolate. Extra entries fade in or out as the count changes.

### Animation with Callback

```csharp
uiRect.AnimateTo(targetStyle, 0.5f, null, () =>
{
    Debug.Log("Animation complete!");
});
```

### Creating a Button Hover Effect

```csharp
public class UIRectButton : MonoBehaviour
{
    public UIRectImage uiRect;

    private UIRectStyle normalStyle;
    private UIRectStyle hoverStyle;

    void Start()
    {
        normalStyle = new UIRectStyle
        {
            FillColor = Color.white,
            BorderWidth = 2,
            BorderColor = Color.gray,
            Radius = new Vector4(10, 10, 10, 10),
            Shadows = new()
            {
                new()
                {
                    color = new(0, 0, 0, 0.3f),
                    size = 5,
                    offset = new(0, -2, 0)
                }
            }
        };

        hoverStyle = new UIRectStyle
        {
            FillColor = new Color(0.9f, 0.9f, 1f),
            BorderColor = Color.blue,
            Shadows = new()
            {
                new()
                {
                    color = new(0, 0, 0, 0.3f),
                    size = 15,
                    offset = new(0, -2, 0)
                }
            }
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

## Performance Notes

- All rendering is GPU-accelerated via custom shader
- Animation ticking runs in `Update()` and early-outs cheaply when the component is idle
- Animations use unscaled time, so they keep running while the game is paused (`Time.timeScale = 0`)
- Minimal CPU overhead during animations
- Shares materials between instances to supports batching

## Compatibility

- Unity 2021.3 or later

## Credits

Created by [Jon Shamir](https://jonshamir.com)
Shadow effects based on [Fast Rounded Rectangle Shadows by Evan Wallace](https://madebyevan.com/shaders/fast-rounded-rectangle-shadows/)