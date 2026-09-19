using System;
using System.Collections.Generic;

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Tracks which suppliers' one-time "you can synthesize" welcome message has
    /// already been delivered (sent directly, or successfully embedded into their
    /// unlock dialogue) so ConversationManager.InitSupplierWelcome doesn't repeat it
    /// on every subsequent load — "already unlocked" is not itself one-shot, since a
    /// supplier's Discovered* dictionary can legitimately stay empty for a long time
    /// after the relationship unlocks, if the player just hasn't used that service yet.
    ///
    /// Persisted to UnicornsWelcomedSuppliers.json alongside DiscoveredCustomSeeds.json.
    /// </summary>
    public static class WelcomedSuppliersRegistry
    {
        public static readonly HashSet<string> Welcomed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static bool HasBeenWelcomed(string npcName) => Welcomed.Contains(npcName);

        public static void MarkWelcomed(string npcName) => Welcomed.Add(npcName);

        public static void Clear() => Welcomed.Clear();
    }
}
