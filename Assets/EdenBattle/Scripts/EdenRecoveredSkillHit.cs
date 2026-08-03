/// <summary>
/// One defender-state cue recovered from a character's ultimate Lua script.
/// </summary>
public struct EdenRecoveredSkillHit
{
    public readonly float timeSeconds;
    public readonly string defenderState;
    public readonly bool isFinal;

    public EdenRecoveredSkillHit(
        float timeSeconds,
        string defenderState,
        bool isFinal)
    {
        this.timeSeconds = timeSeconds;
        this.defenderState = defenderState;
        this.isFinal = isFinal;
    }
}
