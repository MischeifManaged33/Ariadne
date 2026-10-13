using System;
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

        [Min(0f)]
        public float weight = 1f;

        public RoomDefinition[] designs =
            new RoomDefinition[0];
    }

    [SerializeField]
    private RoomCategory[] categories =
        new RoomCategory[0];

    public RoomDefinition PickRoom(System.Random random)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));

        if (categories == null || categories.Length == 0)
        {
            throw new InvalidOperationException(
                $"{name}: add at least one room category."
            );
        }

        double totalWeight = 0;
        RoomCategory lastEnabledCategory = null;

        foreach (RoomCategory category in categories)
        {
            if (category == null)
                continue;

            if (float.IsNaN(category.weight) ||
                float.IsInfinity(category.weight))
            {
                throw new InvalidOperationException(
                    $"{name}: category weights must be finite."
                );
            }

            // A zero-weight category is disabled.
            if (category.weight <= 0f)
                continue;

            if (category.designs == null ||
                category.designs.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{category.categoryName}: add a room prefab."
                );
            }

            foreach (RoomDefinition design in category.designs)
            {
                if (design == null)
                {
                    throw new InvalidOperationException(
                        $"{category.categoryName}: " +
                        "a room prefab reference is missing."
                    );
                }
            }

            totalWeight += category.weight;
            lastEnabledCategory = category;
        }

        if (lastEnabledCategory == null)
        {
            throw new InvalidOperationException(
                $"{name}: at least one category needs a positive weight."
            );
        }

        double roll = random.NextDouble() * totalWeight;

        RoomCategory selectedCategory =
            lastEnabledCategory;

        foreach (RoomCategory category in categories)
        {
            if (category == null || category.weight <= 0f)
                continue;

            roll -= category.weight;

            if (roll < 0)
            {
                selectedCategory = category;
                break;
            }
        }

        int designIndex =
            random.Next(selectedCategory.designs.Length);

        return selectedCategory.designs[designIndex];
    }

    [ContextMenu("Test Room Selection")]
    private void TestRoomSelection()
    {
        RoomDefinition selected =
            PickRoom(new System.Random());

        Debug.Log(
            $"Selected room prefab: {selected.name}",
            this
        );
    }
}