using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using Debug = UnityEngine.Debug;

namespace SlopCo.Networking
{
    /// <summary>
    /// Free, build-now matchmaker: discovers LAN hosts via UDP broadcast beacons, then joins the best
    /// non-full host (or becomes a host if none answer). No join code, no cloud account. Runs above
    /// <see cref="INetworkSession"/> so it works on the existing UnityTransport path.
    ///
    /// Threading: every await omits ConfigureAwait(false) so continuations resume on Unity's main thread,
    /// making the NGO Start/Leave calls legal. Internet-scale matchmaking is an upgrade path behind
    /// <see cref="IMatchmaker"/> (UGS Matchmaker / Steam LobbyList).
    /// </summary>
    public sealed class LanMatchmaker : IMatchmaker
    {
        public bool IsMatching { get; private set; }
        public event Action<string> OnStatus;

        private CancellationTokenSource _cts;

        public async Task<MatchResult> QuickMatchAsync(int maxPlayers)
        {
            var session = ServiceLocator.Get<NetworkSessionManager>()?.Session;
            if (session == null) return new MatchResult(MatchOutcome.Failed, "No NetworkSessionManager.");
            if (session.IsActive) return new MatchResult(MatchOutcome.Failed, "Already in a session.");

            IsMatching = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            UdpClient listener = null;
            try
            {
                OnStatus?.Invoke("Searching for games…");
                listener = OpenListener();
                if (listener == null) return await HostFallback(session, maxPlayers, token);

                var candidates = await ScanAsync(listener, GameConstants.DiscoveryTimeoutSeconds, token);
                if (candidates.Count == 0)
                {
                    // Jittered single rescan: relieves the "both discover nothing → both host" race.
                    float jitter = UnityEngine.Random.Range(0f, GameConstants.MatchRescanJitterSeconds);
                    if (jitter > 0f) await Task.Delay(TimeSpan.FromSeconds(jitter), token);
                    candidates = await ScanAsync(listener, GameConstants.DiscoveryTimeoutSeconds, token);
                }
                if (token.IsCancellationRequested) return new MatchResult(MatchOutcome.Cancelled, "Cancelled.");

                var pool = new List<MatchCandidate>(candidates);
                while (MatchmakerCodec.TrySelectBest(pool, out var best))
                {
                    if (token.IsCancellationRequested) return new MatchResult(MatchOutcome.Cancelled, "Cancelled.");
                    OnStatus?.Invoke($"Joining {best.Address}…");
                    bool started = await session.JoinAsync(best.Address);
                    if (started && await WaitForConnectedAsync(GameConstants.JoinConfirmSeconds, token))
                        return new MatchResult(MatchOutcome.JoinedExisting, best.Address);

                    // Join didn't confirm (full/dead host): leave, wait for NGO to fully stop, drop, try next.
                    session.Leave();
                    await WaitForNetworkIdleAsync(GameConstants.NetworkIdleTimeoutSeconds, token);
                    pool.RemoveAll(c => c.Address == best.Address);
                }

                return await HostFallback(session, maxPlayers, token);
            }
            catch (OperationCanceledException)
            {
                return new MatchResult(MatchOutcome.Cancelled, "Cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[LanMatchmaker] Quick match error: {e}");
                return new MatchResult(MatchOutcome.Failed, e.Message);
            }
            finally
            {
                CloseListener(listener);
                _cts?.Dispose();
                _cts = null;
                IsMatching = false;
            }
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { /* already finished */ }
        }

        // ── Host fallback ───────────────────────────────────────
        private async Task<MatchResult> HostFallback(INetworkSession session, int maxPlayers, CancellationToken token)
        {
            if (token.IsCancellationRequested) return new MatchResult(MatchOutcome.Cancelled, "Cancelled.");
            // In case a prior failed-join Leave() is still settling, wait for NGO idle before StartHost.
            await WaitForNetworkIdleAsync(GameConstants.NetworkIdleTimeoutSeconds, token);
            OnStatus?.Invoke("No game found — hosting…");
            bool ok = await session.HostAsync(maxPlayers);
            return ok
                ? new MatchResult(MatchOutcome.BecameHost, "host")
                : new MatchResult(MatchOutcome.Failed, "Host start failed.");
        }

        // ── UDP discovery ───────────────────────────────────────
        private static UdpClient OpenListener()
        {
            try
            {
                var udp = new UdpClient(); // unbound so we can set ReuseAddress BEFORE binding
                udp.Client.ExclusiveAddressUse = false;
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, GameConstants.DiscoveryPort));
                udp.EnableBroadcast = true;
                return udp;
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[LanMatchmaker] Could not bind discovery port {GameConstants.DiscoveryPort}: {e.Message}. Will host instead.");
                return null;
            }
        }

        private static async Task<List<MatchCandidate>> ScanAsync(UdpClient listener, float seconds, CancellationToken token)
        {
            var found = new Dictionary<string, MatchCandidate>();
            var sw = Stopwatch.StartNew();
            Task<UdpReceiveResult> pending = null;
            try
            {
                while (sw.Elapsed.TotalSeconds < seconds)
                {
                    if (token.IsCancellationRequested) break;
                    double remaining = seconds - sw.Elapsed.TotalSeconds;
                    pending ??= listener.ReceiveAsync();
                    var delay = Task.Delay(TimeSpan.FromSeconds(Math.Max(0.02, remaining)), token);
                    var done = await Task.WhenAny(pending, delay);
                    if (done != pending) break; // timed out

                    UdpReceiveResult res;
                    try { res = pending.Result; }
                    catch { pending = null; continue; }
                    pending = null;

                    if (MatchmakerCodec.TryDecode(res.Buffer, GameConstants.MatchGameId, out var beacon))
                    {
                        string addr = res.RemoteEndPoint.Address.ToString();
                        found[addr] = new MatchCandidate(addr, beacon); // dedupe / refresh by address
                    }
                }
            }
            finally
            {
                // Observe any orphaned receive so disposing the socket doesn't raise an unobserved exception.
                if (pending != null)
                    pending.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            }
            return new List<MatchCandidate>(found.Values);
        }

        private static void CloseListener(UdpClient listener)
        {
            if (listener == null) return;
            try { listener.Dispose(); } // also cancels a dangling ReceiveAsync (ObjectDisposedException, observed above)
            catch (Exception e) { Debug.LogWarning($"[LanMatchmaker] listener dispose: {e.Message}"); }
        }

        // ── NGO state polling ───────────────────────────────────
        private static async Task<bool> WaitForConnectedAsync(float seconds, CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsConnectedClient) return true;
                if (token.IsCancellationRequested) return false;
                await Task.Delay(50, token);
            }
            return false;
        }

        private static async Task WaitForNetworkIdleAsync(float seconds, CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || (!nm.IsListening && !nm.ShutdownInProgress)) return;
                await Task.Delay(50, token);
            }
        }
    }
}
