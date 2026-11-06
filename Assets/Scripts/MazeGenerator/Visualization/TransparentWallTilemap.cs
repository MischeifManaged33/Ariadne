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

    [SerializeField, HideInInspector]
    private bool assignWallsLayer = true;

    public bool AssignWallsLayer
    {
        get => assignWallsLayer;
        set => assignWallsLayer = value;
    }

    [SerializeField, HideInInspector]
    private bool ignoreSorting;

    public bool IgnoreSorting
    {
        get => ignoreSorting;
        set => ignoreSorting = value;
    }

    private void OnEnable()
    {
        wallTilemap = GetComponent<Tilemap>();

        if (!assignWallsLayer)
        {
            registeredTilemaps.Add(wallTilemap);
            return;
        }

        int wallsLayer = LayerMask.NameToLayer("Walls");

        if (wallsLayer >= 0)
        {
            gameObject.layer = wallsLayer;
        }
        else
        {
            Debug.LogError("The GameObject layer 'Walls' is missing.", this);
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