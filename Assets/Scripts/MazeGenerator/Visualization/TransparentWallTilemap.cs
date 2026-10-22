using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
[DisallowMultipleComponent]
public class TransparentWallTilemap : MonoBehaviour
{
    private static readonly HashSet<Tilemap> registeredTilemaps = new();

    public static IEnumerable<Tilemap> RegisteredTilemaps =>
        registeredTilemaps;

    private Tilemap wallTilemap;

    private void OnEnable()
    {
        wallTilemap = GetComponent<Tilemap>();
        registeredTilemaps.Add(wallTilemap);
    }

    private void OnDisable()
    {
        if (wallTilemap != null)
        {
            registeredTilemaps.Remove(wallTilemap);
        }
    }
}