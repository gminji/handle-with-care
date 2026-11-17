#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using SlopCo.Cargo;
using SlopCo.Core;
using SlopCo.Gameplay;
using SlopCo.Player;

namespace SlopCo.UI
{
    /// <summary>
    /// Developer overlay, toggled with F3 — compiled into the Editor and development builds only.
    /// FRAME: fps, average and worst frame time, GC allocated per frame (ProfilerRecorder), managed heap.
    /// NET: role, tick rate, spawned objects, and every peer's round-trip time from UnityTransport.
    /// RUN / MAP / NAV: phase, day, time left, crew census, layout seed and how many seeds the host rejected as
    /// undeliverable, the bot NavMesh's state, the live bombs' lowest fuse.
    /// BOTS (host, where they think): each bot's intent, route progress and stall clock, with its remaining route
    /// drawn in the world and a label over its head.
    /// Installs itself at startup (no scene edit). Text is rebuilt 4×/s from cached lists, so the overlay barely
    /// shows up in the allocation figure it reports.
    /// </summary>
    public sealed class DebugOverlay : MonoBehaviour
    {
        /// <summary>On/off. F3 at runtime; scriptable for capture tools.</summary>
        public static bool Visible { get; set; }

        private const float TextRefresh = 0.25f;
        private const int FrameWindow = 120;
        private const float PanelWidth = 470f;

        private readonly FrameStats _frames = new FrameStats(FrameWindow);
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly Dictionary<AiBrain, LineRenderer> _routes = new Dictionary<AiBrain, LineRenderer>();
        private readonly Dictionary<AiBrain, string> _botLabels = new Dictionary<AiBrain, string>();
        private readonly List<AiBrain> _gone = new List<AiBrain>();
        private ProfilerRecorder _gcAlloc;
        private float _nextText;
        private GUIContent _content = new GUIContent(string.Empty);
        private GUIStyle _panel, _text, _tag;
        private Texture2D _panelBg;
        private Material _lineMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Visible = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var go = new GameObject(nameof(DebugOverlay));
            DontDestroyOnLoad(go);
            go.AddComponent<DebugOverlay>();
        }

        private void OnEnable() => _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        private void OnDisable() => _gcAlloc.Dispose();

        private void OnDestroy()
        {
            foreach (var lr in _routes.Values) if (lr != null) Destroy(lr.gameObject);
            if (_lineMat != null) Destroy(_lineMat);
            if (_panelBg != null) Destroy(_panelBg);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) Visible = !Visible;

            _frames.Push(Time.unscaledDeltaTime, _gcAlloc.Valid ? _gcAlloc.LastValue : -1);

            if (!Visible)
            {
                foreach (var lr in _routes.Values) if (lr != null) lr.enabled = false;
                return;
            }
            if (Time.unscaledTime >= _nextText)
            {
                _nextText = Time.unscaledTime + TextRefresh;
                _content.text = BuildText();
            }
            DrawRoutes();
        }

        // ── Text ─────────────────────────────────────────────────────────

        private string BuildText()
        {
            var sb = _sb;
            sb.Clear();

            Head(sb, "FRAME").Append(_frames.Fps.ToString("0")).Append(" fps   avg ")
              .Append(_frames.AverageMs.ToString("0.0")).Append(" ms   worst ").Append(_frames.MaxMs.ToString("0.0")).Append(" ms");
            if (Application.isEditor) sb.Append("   <color=#8a8a8a>(memory incl. Editor)</color>");
            sb.Append('\n');
            sb.Append("      GC ").Append(DebugOverlayText.Bytes(_frames.AverageGcBytes)).Append("/frame   allocating ")
              .Append((_frames.AllocatingFrameShare * 100f).ToString("0")).Append("% of frames   heap ")
              .Append(DebugOverlayText.Bytes(System.GC.GetTotalMemory(false))).Append('\n');

            var nm = NetworkManager.Singleton;
            Head(sb, "NET");
            if (nm == null) sb.Append("no NetworkManager\n");
            else
            {
                sb.Append(DebugOverlayText.Role(nm.IsListening, nm.IsServer, nm.IsClient));
                if (nm.IsListening)
                {
                    var utp = nm.NetworkConfig.NetworkTransport as UnityTransport;
                    sb.Append("   id ").Append(nm.LocalClientId).Append("   tick ").Append(nm.NetworkConfig.TickRate)
                      .Append(" Hz   objects ").Append(nm.SpawnManager != null ? nm.SpawnManager.SpawnedObjectsList.Count : 0);
                    if (nm.IsServer)
                    {
                        sb.Append("   peers ").Append(nm.ConnectedClientsIds.Count);
                        foreach (var id in nm.ConnectedClientsIds)
                        {
                            if (id == NetworkManager.ServerClientId) continue;
                            sb.Append("\n      client ").Append(id).Append("  rtt ");
                            if (utp != null) sb.Append(utp.GetCurrentRtt(id)).Append(" ms"); else sb.Append('?');
                        }
                    }
                    else
                    {
                        sb.Append("   rtt ");
                        if (utp != null) sb.Append(utp.GetCurrentRtt(NetworkManager.ServerClientId)).Append(" ms"); else sb.Append('?');
                    }
                }
                sb.Append('\n');
            }

            var rm = ServiceLocator.Get<RoundManager>();
            if (rm != null && rm.IsSpawned)
            {
                Head(sb, "RUN").Append(rm.Phase.Value).Append("   day ").Append(rm.RoundNumber.Value).Append("   ")
                  .Append(rm.TimeRemaining.Value.ToString("0")).Append(" s left   crew ").Append(CrewCensus.Count);
                if (DisconnectVote.GameFrozen) sb.Append("   <color=#ff8>FROZEN (vote)</color>");
                sb.Append('\n');
            }

            var mm = ServiceLocator.Get<MapManager>();
            if (mm != null && mm.IsSpawned)
            {
                Head(sb, "MAP").Append(mm.ActiveMap.Value).Append("   seed ").Append(mm.LayoutSeed.Value);
                if (mm.IsServer) sb.Append("   seeds rejected ").Append(mm.LayoutRerolls);
                sb.Append('\n');
            }

            var nav = BotNavMesh.Current;
            if (nav != null) Head(sb, "NAV").Append(nav.Ready ? "ready" : "not baked").Append("   bake #").Append(nav.Version).Append('\n');

            int bombs = 0;
            float minFuse = 1f;
            for (int i = 0; i < CargoBomb.Live.Count; i++)
            {
                var b = CargoBomb.Live[i];
                if (b == null) continue;
                bombs++;
                minFuse = Mathf.Min(minFuse, b.Fuse);
            }
            if (bombs > 0) Head(sb, "BOMB").Append(bombs).Append(" live   lowest fuse ").Append((minFuse * 100f).ToString("0")).Append("%\n");

            _botLabels.Clear();
            bool botHeader = false;
            for (int i = 0; i < AiBrain.Live.Count; i++)
            {
                var bot = AiBrain.Live[i];
                if (bot == null || bot.Id == 0) continue;
                if (!botHeader) { Head(sb, "BOTS").Append('\n'); botHeader = true; }
                string label = DebugOverlayText.BotLabel(bot.Intent.ToString(), bot.RouteCornerIndex, bot.RouteCornerCount,
                                                         bot.StallClock, bot.Hops);
                _botLabels[bot] = label;
                sb.Append("      #").Append(bot.Id).Append("  ").Append(label).Append('\n');
            }

            sb.Append("<color=#8a8a8a>F3 to hide</color>");
            return sb.ToString();
        }

        private static StringBuilder Head(StringBuilder sb, string name) =>
            sb.Append("<b><color=#7fdfff>").Append(name.PadRight(5)).Append("</color></b> ");

        // ── World: each bot's remaining route ────────────────────────────

        private void DrawRoutes()
        {
            for (int i = 0; i < AiBrain.Live.Count; i++)
            {
                var bot = AiBrain.Live[i];
                if (bot == null) continue;
                var lr = RouteLine(bot);
                int ahead = bot.RouteCornerCount - bot.RouteCornerIndex;
                if (ahead <= 0) { lr.enabled = false; continue; }

                lr.enabled = true;
                lr.positionCount = ahead + 1;
                Vector3 lift = Vector3.up * 0.1f;
                lr.SetPosition(0, bot.transform.position + lift);
                for (int k = 0; k < ahead; k++) lr.SetPosition(k + 1, bot.RouteCorner(bot.RouteCornerIndex + k) + lift);
                Color c = IntentColor(bot.Intent);
                lr.startColor = c;
                lr.endColor = c;
            }

            _gone.Clear();
            foreach (var kv in _routes)
                if (kv.Key == null || !kv.Key.isActiveAndEnabled) _gone.Add(kv.Key);
            foreach (var bot in _gone)
            {
                if (_routes[bot] != null) Destroy(_routes[bot].gameObject);
                _routes.Remove(bot);
            }
        }

        private LineRenderer RouteLine(AiBrain bot)
        {
            if (_routes.TryGetValue(bot, out var existing) && existing != null) return existing;
            if (_lineMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _lineMat = new Material(sh);
            }
            var go = new GameObject("BotRoute");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = _lineMat;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.07f;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _routes[bot] = lr;
            return lr;
        }

        private static Color IntentColor(BotIntent intent) => intent switch
        {
            BotIntent.Haul => new Color(1f, 0.6f, 0.15f),
            BotIntent.Claim => new Color(0.25f, 0.9f, 1f),
            BotIntent.Join => new Color(0.4f, 1f, 0.4f),
            _ => new Color(0.7f, 0.7f, 0.7f),
        };

        // ── IMGUI ────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!Visible) return;
            EnsureStyles();

            float h = _text.CalcHeight(_content, PanelWidth);
            GUI.Box(new Rect(8f, 8f, PanelWidth + 16f, h + 16f), GUIContent.none, _panel);
            GUI.Label(new Rect(16f, 16f, PanelWidth, h), _content, _text);

            var cam = Camera.main;
            if (cam == null) return;
            foreach (var kv in _botLabels)
            {
                var bot = kv.Key;
                if (bot == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(bot.transform.position + Vector3.up * 2.3f);
                if (sp.z <= 0f) continue;
                // Bots sharing one cargo stand shoulder to shoulder: stagger by id so their labels don't overprint.
                float lift = (bot.Id % 3) * 15f;
                GUI.Label(new Rect(sp.x - 110f, Screen.height - sp.y - 12f - lift, 220f, 24f), kv.Value, _tag);
            }
        }

        private void EnsureStyles()
        {
            if (_panel != null) return;
            _panelBg = new Texture2D(1, 1);
            _panelBg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.62f));
            _panelBg.Apply();
            _panel = new GUIStyle(GUI.skin.box);
            _panel.normal.background = _panelBg;
            _text = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13, wordWrap = true };
            _text.normal.textColor = Color.white;
            _tag = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontStyle = FontStyle.Bold };
        }
    }
}
#endif
