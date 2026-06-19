using System.Text;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

public enum InputCodeLength
{
    Four = 4,
    Six = 6,
    Eight = 8
}

[CreateAssetMenu(fileName = "InputCodeMinigameDefinition", menuName = "Breezeblocks/Missions/Input Code Minigame Definition")]
public sealed class InputCodeMinigameDefinition : ScriptableObject
{
    [FoldoutGroup("Code")]
    [SerializeField] private InputCodeLength codeLength = InputCodeLength.Four;

    [FoldoutGroup("Code")]
    [SerializeField] private string correctCombination = "1234";

    [FoldoutGroup("Attempts"), MinValue(1)]
    [SerializeField] private int maxAttempts = 3;

    public int RequiredDigitCount => (int)codeLength;
    public string CorrectCombination => NormalizeCombination(correctCombination, RequiredDigitCount);
    public int MaxAttempts => Mathf.Max(1, maxAttempts);

    /// <summary>
    /// Normalizes the authored code so it always contains only the configured number of digits.
    /// </summary>
    private void OnValidate()
    {
        maxAttempts = Mathf.Max(1, maxAttempts);
        correctCombination = NormalizeCombination(correctCombination, RequiredDigitCount);
    }

    /// <summary>
    /// Returns whether a submitted code exactly matches the authored solution.
    /// </summary>
    public bool IsCorrect(string submittedCode)
    {
        return string.Equals(submittedCode, CorrectCombination, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Removes non-digit characters and pads missing digits with zeroes for stable designer input.
    /// </summary>
    private static string NormalizeCombination(string value, int requiredDigitCount)
    {
        StringBuilder builder = new(requiredDigitCount);
        string source = value ?? string.Empty;
        for (int i = 0; i < source.Length && builder.Length < requiredDigitCount; i++)
        {
            if (char.IsDigit(source[i]))
                builder.Append(source[i]);
        }

        while (builder.Length < requiredDigitCount)
            builder.Append('0');

        return builder.ToString();
    }
}

}
