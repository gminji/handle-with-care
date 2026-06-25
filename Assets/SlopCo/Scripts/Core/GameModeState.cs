namespace SlopCo.Core
{
    /// <summary>
    /// Lightweight global for the active play mode. In <c>Solo</c>, two-person cargo (incl. the bomb)
    /// is carryable by one player at full strength so the bomb-delivery loop works alone — no friend
    /// required. Set when launching from the menu; reset on leaving. Host-side authority reads it
    /// (in solo the host is the only player, so the one process owns the truth).
    /// </summary>
    public static class GameModeState
    {
        public static bool Solo;
        /// <summary>Guided tutorial run (implies Solo): calm fuse + step-by-step coaching overlay.</summary>
        public static bool Tutorial;
        /// <summary>Chosen map index, or -1 for random (the host rolls one at session start). Set from
        /// the menu's map picker; read host-side by <c>MapManager</c>. Reset to -1 on leaving.</summary>
        public static int SelectedMap = -1;
    }
}
