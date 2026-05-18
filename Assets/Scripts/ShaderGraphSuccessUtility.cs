using UnityEngine;

/// <summary>
/// Shader Graph / URP : propriété bool « Success » exposée comme float 0/1 (<c>_Success</c> ou <c>Success</c>).
/// </summary>
public static class ShaderGraphSuccessUtility
{
    /// <summary>
    /// Remplace un slot de shared material puis règle Success sur toutes les instances du renderer.
    /// </summary>
    public static void ApplySharedMaterialWithSuccess(Renderer renderer, int materialIndex, Material sharedMaterial, bool success)
    {
        if (renderer == null || sharedMaterial == null) return;

        Material[] shared = renderer.sharedMaterials;
        if (shared == null || shared.Length == 0) return;

        int i = Mathf.Clamp(materialIndex, 0, shared.Length - 1);
        shared[i] = sharedMaterial;
        renderer.sharedMaterials = shared;

        SetSuccessOnRendererMaterials(renderer, success);
    }

    /// <summary>
    /// Met Success sur chaque <see cref="Material"/> du renderer qui expose la propriété.
    /// </summary>
    public static void SetSuccessOnRendererMaterials(Renderer renderer, bool success)
    {
        if (renderer == null) return;

        Material[] mats = renderer.materials;
        if (mats == null) return;

        float v = success ? 1f : 0f;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] == null) continue;
            TrySetSuccessFloat(mats[i], v);
        }
    }

    private static void TrySetSuccessFloat(Material mat, float v)
    {
        if (mat.HasProperty("_Success"))
        {
            mat.SetFloat("_Success", v);
            mat.SetInt("_Success", (int)v);
            return;
        }

        if (mat.HasProperty("Success"))
        {
            mat.SetFloat("Success", v);
            mat.SetInt("Success", (int)v);
        }
    }
}
