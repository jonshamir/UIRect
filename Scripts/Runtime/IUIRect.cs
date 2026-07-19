using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Shared surface for the two UIRect components (<see cref="UIRectImage"/> over Image and
    /// <see cref="UIRectRawImage"/> over RawImage). Single inheritance forces each to keep its own
    /// serialized fields, but the style logic in <see cref="IUIRectExtensions"/> operates once
    /// through this interface. Members are PascalCase properties that each component forwards to its
    /// camelCase fields in one line.
    /// </summary>
    public interface IUIRect
    {
        Vector2 Size { get; }
        Color color { get; }
        UIRectStyle Style { get; set; }

        Color FillColor { get; set; }
        Vector4 Radius { get; set; }
        Vector3 Translate { get; set; }
        Vector2 Skew { get; set; }

        Color BorderColor { get; set; }
        float BorderWidth { get; set; }
        BorderAlign BorderAlignment { get; set; }

        List<UIRectShadow> Shadows { get; set; }

        float BevelWidth { get; set; }
        float BevelStrength { get; set; }
    }
}
