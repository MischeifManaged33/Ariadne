using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Dialogue_", menuName = "Dialogue/Dialogue Sequence")]
public class DialogueData : ScriptableObject
{
    [SerializeField]
    private string displayName = "Daedalus";

    [SerializeField, TextArea(2, 5)]
    private List<string> lines = new();

    public string DisplayName => displayName;

    public IReadOnlyList<string> Lines => lines;

    public bool IsEmpty => lines == null || lines.Count == 0;
}
