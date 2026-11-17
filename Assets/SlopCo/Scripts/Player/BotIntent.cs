namespace SlopCo.Player
{
    /// <summary>What an AI bot is trying to do this frame — the readable name of <see cref="AiBrain"/>'s priority
    /// loop (carry it home › claim a loose cargo › join a crew that needs hands › nothing to do).</summary>
    public enum BotIntent : byte
    {
        Idle = 0,    // no cargo worth walking to
        Claim = 1,   // heading for a LOOSE cargo nobody holds
        Join = 2,    // heading for a held cargo that still needs more hands
        Haul = 3,    // carrying: heading for the van
    }

    /// <summary>Pure classifier for <see cref="BotIntent"/> (EditMode-testable, no scene).</summary>
    public static class BotIntents
    {
        public static BotIntent Classify(bool carrying, bool hasTarget, bool targetIsJoin)
        {
            if (carrying) return BotIntent.Haul;
            if (!hasTarget) return BotIntent.Idle;
            return targetIsJoin ? BotIntent.Join : BotIntent.Claim;
        }
    }
}
