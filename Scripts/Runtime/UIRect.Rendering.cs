using UnityEngine;
using UnityEngine.UI;

public partial class UIRect
{
    #region Rendering

    // Edits the UI vertices with the data read on the GPU (see UIRectRenderer)
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);
        UIRectRenderer.Populate(vh, BuildRenderParams());
    }

    private UIRectRenderParams BuildRenderParams() => new UIRectRenderParams
    {
        size = Size,
        color = color,
        fillColor = fillColor,
        radius = radius,
        translate = translate,
        borderColor = borderColor,
        borderWidth = borderWidth,
        borderAlign = borderAlign,
        hasShadow = hasShadow,
        shadowColor = shadowColor,
        shadowSize = shadowSize,
        shadowSpread = shadowSpread,
        shadowOffset = shadowOffset,
        bevelWidth = bevelWidth,
        bevelStrength = bevelStrength,
    };

    #endregion
}
