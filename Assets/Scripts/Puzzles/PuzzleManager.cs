using UnityEngine;
using System.Collections.Generic;

public class PuzzleManager : MonoBehaviour
{
    [SerializeField]
    private PuzzleDoor door;

    private readonly List<PressurePlate> plates = new();

    public void RegisterPlate(PressurePlate plate)
    {
        if (!plates.Contains(plate))
            plates.Add(plate);
    }

    public void CheckPuzzle()
    {
        if (plates.Count == 0)
            return;

        foreach (PressurePlate plate in plates)
        {
            if (!plate.IsPressed)
            {
                door.Close();
                return;
            }
        }

        door.Open();
    }
}
