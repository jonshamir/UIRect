using UnityEngine;

/// <summary>
/// Shared surface for the two UIRect components (<see cref="UIRect"/> over Image and
/// <see cref="UIRectRawImage"/> over RawImage). C# single inheritance forces them to keep their
/// own serialized fields, but the style⇆<see cref="UIRectStyle"/> mapping, render-param assembly
/// and animation tick are identical, so they live once in <see cref="IUIRectExtensions"/> and
/// operate through this interface.
///
/// The 13 style members are PascalCase because a serialized public <c>field</c> cannot implement
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

    Color BorderColor { get; set; }
    float BorderWidth { get; set; }
    BorderAlign BorderAlignment { get; set; }

    bool HasShadow { get; set; }
    Color ShadowColor { get; set; }
    float ShadowSize { get; set; }
    float ShadowSpread { get; set; }
    Vector3 ShadowOffset { get; set; }

    float BevelWidth { get; set; }
    float BevelStrength { get; set; }
}
