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

        int wallsLayer = LayerMask.NameToLayer("Walls");

        if (wallsLayer >= 0)
        {
            gameObject.layer = wallsLayer;
        }
        else
        {
            Debug.LogError("The GameObject layer 'Walls' is missing.", this);
        }

        TilemapRenderer wallRenderer = GetComponent<TilemapRenderer>();

        if (wallRenderer != null)
        {
            wallRenderer.sortingLayerName = "Walls";
        }

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