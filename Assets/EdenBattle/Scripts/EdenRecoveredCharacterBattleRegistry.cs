using System;

/// <summary>
/// Single registration point for recovered characters. Adding a character
/// requires one character class plus one entry here; the shared player stays
/// unchanged.
/// </summary>
public static class EdenRecoveredCharacterBattleRegistry
{
    public static bool Supports(string cardId)
    {
        return string.Equals(cardId, "11300018", StringComparison.Ordinal) ||
            string.Equals(cardId, "11301023", StringComparison.Ordinal) ||
            string.Equals(cardId, "11301006", StringComparison.Ordinal) ||
            string.Equals(cardId, "11301005", StringComparison.Ordinal);
    }

    public static EdenRecoveredCharacterBattle ForCard(string cardId)
    {
        if (string.Equals(cardId, "11300018", StringComparison.Ordinal))
            return new EdenBattle11300018();
        if (string.Equals(cardId, "11301023", StringComparison.Ordinal))
            return new EdenBattle11301023();
        if (string.Equals(cardId, "11301006", StringComparison.Ordinal))
            return new EdenBattle11301006();
        if (string.Equals(cardId, "11301005", StringComparison.Ordinal))
            return new EdenBattle11301005();

        throw new ArgumentException(
            "Recovered battle skill is unavailable: " + cardId,
            "cardId");
    }
}
