using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Shared surface for the two UIRect components (<see cref="UIRectImage"/> over Image and
    /// <see cref="UIRectRawImage"/> over RawImage). C# single inheritance forces them to keep their
    /// own serialized fields, but the style⇆<see cref="UIRectStyle"/> mapping, render-param assembly
    /// and animation tick are identical, so they live once in <see cref="IUIRectExtensions"/> and
    /// operate through this interface.
    ///
    /// The style members are PascalCase because a serialized public <c>field</c> cannot implement
    /// an interface <c>property</c> and the camelCase field names are already taken; each component
    /// forwards them in one line. <c>color</c>, <see cref="Size"/> and <see cref="Style"/> are
    /// satisfied directly by existing public members (the first two inherited from Graphic).
    /// </summary>
    public interface IUIRect
    {
        Vector2 Size { get; }
        Color color { get; }
        UIRectStyle Style { get; set; }

        Color FillColor { get; set; }
        Vector4 Radius { get; set; }
        Vector3 Translate { get; set; }

        Color StrokeColor { get; set; }
        float StrokeWidth { get; set; }
        StrokeAlign StrokeAlignment { get; set; }

        List<UIRectShadow> Shadows { get; set; }

        float BevelWidth { get; set; }
        float BevelStrength { get; set; }
    }
}
