using UnityEngine;

// Default material for the meshes
public static class IndicatorMaterial
{
    public static Material CreateDefault()
    {
        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");

        if (shader == null) {
            Debug.LogError("No usable sprite shader was found for an indicator. Assign a Material instead.");
            return null;
        }

        return new Material(shader) {
            name = "Indicator (Runtime)",
            mainTexture = Texture2D.whiteTexture
        };
    }
}
