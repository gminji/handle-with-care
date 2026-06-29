using System;
using System.Collections.Generic;
using System.Text;
using SlopCo.Core;

namespace SlopCo.Networking
{
    /// <summary>
    /// Pure beacon (de)serialization + candidate selection — NO UnityEngine / socket dependency so it is
    /// unit-testable in EditMode (mirrors CargoMath / VoiceMath). Wire format:
    /// [magic 'S','P'][version 1B][gameIdLen 1B][gameId UTF8][port 2B LE][players 1B][maxPlayers 1B].
    /// </summary>
    public static class MatchmakerCodec
    {
        private const byte Magic0 = (byte)'S';
        private const byte Magic1 = (byte)'P';
        private const int MinLength = 8; // magic(2)+ver(1)+idLen(1)+id(0)+port(2)+players(1)+max(1)

        public static byte[] Encode(in MatchBeacon b)
        {
            byte[] id = Encoding.UTF8.GetBytes(b.GameId ?? string.Empty);
            if (id.Length > 255) Array.Resize(ref id, 255);
            var buf = new byte[4 + id.Length + 4];
            int i = 0;
            buf[i++] = Magic0;
            buf[i++] = Magic1;
            buf[i++] = GameConstants.MatchBeaconVersion;
            buf[i++] = (byte)id.Length;
            Array.Copy(id, 0, buf, i, id.Length); i += id.Length;
            buf[i++] = (byte)(b.Port & 0xFF);
            buf[i++] = (byte)((b.Port >> 8) & 0xFF);
            buf[i++] = b.Players;
            buf[i++] = b.MaxPlayers;
            return buf;
        }

        /// <summary>
        /// Decode + validate a datagram. Returns false on bad magic/version, wrong length, or a GameId that
        /// does not match <paramref name="expectedGameId"/> (so unrelated apps on the LAN are ignored).
        /// </summary>
        public static bool TryDecode(ReadOnlySpan<byte> data, string expectedGameId, out MatchBeacon beacon)
        {
            beacon = default;
            if (data.Length < MinLength) return false;
            if (data[0] != Magic0 || data[1] != Magic1) return false;
            if (data[2] != GameConstants.MatchBeaconVersion) return false;

            int idLen = data[3];
            int need = 4 + idLen + 4;
            if (data.Length < need) return false;

            string gameId = Encoding.UTF8.GetString(data.Slice(4, idLen));
            if (!string.Equals(gameId, expectedGameId, StringComparison.Ordinal)) return false;

            int p = 4 + idLen;
            ushort port = (ushort)(data[p] | (data[p + 1] << 8));
            byte players = data[p + 2];
            byte max = data[p + 3];
            beacon = new MatchBeacon(gameId, port, players, max);
            return true;
        }

        /// <summary>
        /// Pick the best joinable host: among non-full candidates, prefer the one with the MOST players
        /// (fills nearly-complete lobbies first → fewer fragmented games). Returns false when none are
        /// joinable (caller should then become a host).
        /// </summary>
        public static bool TrySelectBest(IReadOnlyList<MatchCandidate> found, out MatchCandidate best)
        {
            best = default;
            bool any = false;
            int bestPlayers = -1;
            for (int i = 0; i < (found?.Count ?? 0); i++)
            {
                MatchCandidate c = found[i];
                if (!c.Beacon.IsJoinable) continue;
                if (c.Beacon.Players > bestPlayers)
                {
                    bestPlayers = c.Beacon.Players;
                    best = c;
                    any = true;
                }
            }
            return any;
        }
    }
}
