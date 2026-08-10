using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FoundAnomalyEntryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text numberText;
    [SerializeField] private TMP_Text areaText;
    [SerializeField] private TMP_Text objectText;
    [SerializeField] private TMP_Text actionText;

    private void Awake() => ResolveReferences();

    public void Bind(int number, string areaLabel, string objectLabel, string actionLabel)
    {
        ResolveReferences();

        if (numberText != null)
            numberText.text = $"{number}.";

        if (areaText != null)
            areaText.text = $"{areaLabel}에서";

        if (objectText != null)
            objectText.text = AddSubjectParticle(objectLabel);

        if (actionText != null)
            actionText.text = actionLabel;
    }

    private void ResolveReferences()
    {
        if (numberText != null && areaText != null && objectText != null && actionText != null)
            return;

        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            switch (text.name)
            {
                case "NumberText":
                    numberText = text;
                    break;
                case "AreaText":
                    areaText = text;
                    break;
                case "ObjectText":
                    objectText = text;
                    break;
                case "ActionText":
                    actionText = text;
                    break;
            }
        }
    }

    private static string AddSubjectParticle(string value)
    {
        string trimmedValue = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (trimmedValue.Length == 0)
            return string.Empty;

        char lastCharacter = trimmedValue[trimmedValue.Length - 1];
        bool hasFinalConsonant = lastCharacter >= '\uAC00' &&
                                 lastCharacter <= '\uD7A3' &&
                                 (lastCharacter - '\uAC00') % 28 != 0;
        return $"{trimmedValue}{(hasFinalConsonant ? "이" : "가")}";
    }
}
