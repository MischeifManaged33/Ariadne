using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RoomCatalogue",
    menuName = "Ariadne/Room Catalogue"
)]
public class RoomCatalogue : ScriptableObject
{
    [Serializable]
    public class RoomCategory
    {
        public string categoryName;

        [Min(0)]
        public int minimumRooms = 0;

        [Min(0)]
        public int maximumRooms = 10;

        [Tooltip("Weight used to fill slots after minimums are satisfied.")]
        [Min(0f)]
        public float weight = 1f;

        [Tooltip("Allow the same design more than once in this category.")]
        public bool allowDuplicateDesigns = true;

        public RoomDefinition[] designs =
            new RoomDefinition[0];
    }

    [SerializeField]
    private RoomCategory[] categories =
        new RoomCategory[0];

    // Counts and available designs belong to this generation only.
    private class CategoryState
    {
        public RoomCategory category;
        public List<RoomDefinition> available;
        public int spawnedCount;

        public bool CanPick =>
            spawnedCount < category.maximumRooms &&
            available.Count > 0;
    }

    public List<RoomDefinition> PickRooms(
        int roomCount,
        System.Random random)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        if (roomCount < 1)
            throw new ArgumentOutOfRangeException(nameof(roomCount));

        if (categories == null || categories.Length == 0)
        {
            throw new InvalidOperationException(
                $"{name}: add at least one room category."
            );
        }

        List<CategoryState> states = new List<CategoryState>();
        long totalMinimum = 0;

        foreach (RoomCategory category in categories)
        {
            if (category == null)
                continue;

            if (category.minimumRooms < 0 ||
                category.maximumRooms < category.minimumRooms)
            {
                throw new InvalidOperationException(
                    $"{category.categoryName}: Maximum Rooms must " +
                    "be at least Minimum Rooms, and both must be nonnegative."
                );
            }

            if (float.IsNaN(category.weight) ||
                float.IsInfinity(category.weight) ||
                category.weight < 0f)
            {
                throw new InvalidOperationException(
                    $"{category.categoryName}: weight must be " +
                    "finite and nonnegative."
                );
            }

            totalMinimum += category.minimumRooms;

            // Maximum zero disables this category.
            if (category.maximumRooms == 0)
                continue;

            // Zero weight with no minimum means this category is unused.
            if (category.minimumRooms == 0 && category.weight == 0f)
                continue;

            if (category.designs == null || category.designs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{category.categoryName}: add a room prefab."
                );
            }

            List<RoomDefinition> available =
                new List<RoomDefinition>();

            foreach (RoomDefinition design in category.designs)
            {
                if (design == null)
                {
                    throw new InvalidOperationException(
                        $"{category.categoryName}: a prefab reference is missing."
                    );
                }

                // Listing a prefab twice should not bypass duplicate limits.
                if (!available.Contains(design))
                    available.Add(design);
            }

            if (!category.allowDuplicateDesigns &&
                available.Count < category.minimumRooms)
            {
                throw new InvalidOperationException(
                    $"{category.categoryName}: its minimum requires " +
                    "more unique designs than are available."
                );
            }

            states.Add(new CategoryState
            {
                category = category,
                available = available
            });
        }

        if (totalMinimum > roomCount)
        {
            throw new InvalidOperationException(
                $"{name}: category minimums require {totalMinimum} rooms, " +
                $"but the generator's Room Count is {roomCount}."
            );
        }

        List<RoomDefinition> selected =
            new List<RoomDefinition>();

        // First guarantee each category's minimum.
        foreach (CategoryState state in states)
        {
            for (int i = 0; i < state.category.minimumRooms; i++)
                selected.Add(TakeDesign(state, random));
        }

        // Fill remaining slots using weights and remaining capacity.
        while (selected.Count < roomCount)
        {
            double totalWeight = 0;
            CategoryState chosen = null;

            foreach (CategoryState state in states)
            {
                if (!state.CanPick || state.category.weight <= 0f)
                    continue;

                totalWeight += state.category.weight;
                chosen = state;
            }

            if (chosen == null)
            {
                throw new InvalidOperationException(
                    $"{name}: cannot fill Room Count ({roomCount}). " +
                    "Increase category maximums, add unique designs, " +
                    "enable duplicates, or give an eligible category " +
                    "a positive weight."
                );
            }

            double roll = random.NextDouble() * totalWeight;

            foreach (CategoryState state in states)
            {
                if (!state.CanPick || state.category.weight <= 0f)
                    continue;

                roll -= state.category.weight;

                if (roll < 0)
                {
                    chosen = state;
                    break;
                }
            }

            selected.Add(TakeDesign(chosen, random));
        }

        return selected;
    }

    private RoomDefinition TakeDesign(
        CategoryState state,
        System.Random random)
    {
        int index = random.Next(state.available.Count);
        RoomDefinition design = state.available[index];

        state.spawnedCount++;

        if (!state.category.allowDuplicateDesigns)
            state.available.RemoveAt(index);

        return design;
    }
}