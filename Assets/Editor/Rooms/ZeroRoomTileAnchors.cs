#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ZeroRoomTileAnchors
{
    private const string RoomFolder = "Assets/Prefabs/Rooms";

    private const string MenuPath =
        "Tools/Ariadne/Set Room Prefab Tile Anchors To Zero";

    [MenuItem(MenuPath)]
    private static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError(
                "Save your prefab and close Prefab Mode " +
                "before running this command."
            );
            return;
        }

        if (!AssetDatabase.IsValidFolder(RoomFolder))
        {
            Debug.LogError(
                $"Room prefab folder not found: {RoomFolder}"
            );
            return;
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { RoomFolder }
        );

        string[] paths = Array.ConvertAll(
            guids,
            AssetDatabase.GUIDToAssetPath
        );

        Array.Sort(paths, StringComparer.Ordinal);

        int changedPrefabs = 0;
        int changedTilemaps = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i];

                if (!path.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Reset Room Tile Anchors",
                    path,
                    (float)i / Mathf.Max(1, paths.Length)
                );

                GameObject root = null;

                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);

                    int changed = 0;

                    foreach (Tilemap tilemap in
                             root.GetComponentsInChildren<Tilemap>(true))
                    {
                        if (tilemap.tileAnchor.Equals(Vector3.zero))
                            continue;

                        tilemap.tileAnchor = Vector3.zero;

                        // Preserve changes on nested prefab
                        // instances and prefab variants.
                        PrefabUtility
                            .RecordPrefabInstancePropertyModifications(
                                tilemap
                            );

                        changed++;
                    }

                    if (changed == 0)
                        continue;

                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        path,
                        out bool success
                    );

                    if (!success)
                    {
                        throw new InvalidOperationException(
                            $"Could not save {path}"
                        );
                    }

                    changedPrefabs++;
                    changedTilemaps += changed;

                    Debug.Log(
                        $"Set {changed} Tilemap anchor(s) " +
                        $"to zero: {path}"
                    );
                }
                catch (Exception exception)
                {
                    failed++;

                    Debug.LogError(
                        $"Could not update {path}: " +
                        exception.Message
                    );
                }
                finally
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log(
            $"Finished: {changedTilemaps} Tilemaps changed " +
            $"in {changedPrefabs} prefabs; {failed} failures. " +
            "Restore markers from saved cells on affected " +
            "rooms, then save those prefabs."
        );
    }

    [MenuItem(MenuPath, true)]
    private static bool CanRun()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif