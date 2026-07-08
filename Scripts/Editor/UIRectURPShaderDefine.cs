#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mirrors "is the URP package installed" into a GLOBAL scripting define, <c>UIRECT_URP_SHADER</c>,
/// so the URP-only shader (<c>UIRectBlurURP.shader</c>) can <c>#if</c> on it.
///
/// Why this is needed: <c>.shader</c> files are compiled by Unity regardless of assembly-definition
/// defines, so the URP shader's <c>#include</c> of URP ShaderLibrary headers would error in Built-in-only
/// projects that lack the URP package. Asmdef <c>versionDefines</c> (like <c>UIRECT_URP</c>) are visible
/// to C# only, never to shaders - but global Player Settings scripting defines ARE visible to shaders.
///
/// Runs on load / recompile and only writes when the value differs, so it doesn't cause recompile loops.
/// Maintains the define for the active build target group (the one shaders compile against in-editor and
/// the usual build target); switching platform triggers a domain reload that re-runs this.
/// </summary>
[InitializeOnLoad]
internal static class UIRectURPShaderDefine
{
    private const string Symbol = "UIRECT_URP_SHADER";

    static UIRectURPShaderDefine()
    {
        // URP runtime type resolves only when the URP package is present in the project.
        bool hasURP = System.Type.GetType(
            "UnityEngine.Rendering.Universal.ScriptableRendererFeature, Unity.RenderPipelines.Universal.Runtime") != null;

        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        if (group == BuildTargetGroup.Unknown)
            return;

#pragma warning disable CS0618 // per-group define API is fine on the supported Unity range (2021.3+)
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
        var list = new System.Collections.Generic.List<string>(
            defines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
        bool present = list.Contains(Symbol);

        if (hasURP == present)
            return; // already correct - avoid a needless recompile

        if (hasURP)
            list.Add(Symbol);
        else
            list.Remove(Symbol);

        PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", list));
#pragma warning restore CS0618
    }
}
#endif
