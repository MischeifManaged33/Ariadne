using UnityEngine;

// Helper for converting between screen space and ground space for isometric
public static class Isometric
{
    public const float DefaultYScale = 0.5f;

    public static Vector2 ToGround(Vector2 screen, float yScale)
    {
        return new Vector2(screen.x, yScale > 0f ? screen.y / yScale : screen.y);
    }

    public static Vector2 ToScreen(Vector2 ground, float yScale)
    {
        return new Vector2(ground.x, ground.y * yScale);
    }

    public static float GroundDistance(Vector2 from, Vector2 to, float yScale)
    {
        return ToGround(to - from, yScale).magnitude;
    }

    public static Vector2 GroundDirection(Vector2 from, Vector2 to, float yScale)
    {
        var ground = ToGround(to - from, yScale);
        return ground.sqrMagnitude > 0.0001f ? ground.normalized : Vector2.zero;
    }
}
