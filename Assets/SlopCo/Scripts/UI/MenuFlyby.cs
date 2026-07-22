using UnityEngine;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// Title-screen cinematic: while the front-end is on the MainMenu (<see cref="UIManager.OnTitleScreen"/>),
    /// this drives the single Main Camera in a slow 45°-elevation orbit over the real game maps and cycles
    /// through them, so the menu sits over a live "flyover of our maps" instead of an empty backdrop — a
    /// stronger first impression for trailers/store shots. Pure orbit math lives in <see cref="FlybyOrbit"/>.
    ///
    /// Camera ownership is state-exclusive: this runs only on the title; <see cref="SlopCo.Player.PlayerController"/>
    /// drives the camera only in-session (IsOwner). When the player leaves the title the preview maps are hidden
    /// and the camera is released, so the networked MapManager / player camera take over cleanly.
    /// </summary>
    public sealed class MenuFlyby : MonoBehaviour
    {
        [SerializeField] private UIManager ui;
        [SerializeField] private Camera cam;
        [Tooltip("The map geometry roots to showcase (same roots MapManager toggles, e.g. Map_0, Map_1).")]
        [SerializeField] private GameObject[] mapRoots;
        [Tooltip("World point the camera orbits/looks at (roughly the maps' shared center).")]
        [SerializeField] private Vector3 lookCenter = Vector3.zero;
        [SerializeField] private float orbitRadius = 26f;
        [SerializeField] private float elevationDeg = 45f;     // the requested sky angle
        [SerializeField] private float orbitSpeedDeg = 8f;      // slow cinematic sweep
        [SerializeField] private float lookHeight = 1.5f;       // look slightly above the center
        [SerializeField] private float cycleSeconds = 12f;      // seconds before showcasing the next map

        private float _angle;
        private float _cycleT;
        private int _mapIdx = -1;
        private bool _active;

        private void OnEnable()
        {
            if (ui == null) ui = FindFirstObjectByType<UIManager>();
            if (cam == null) cam = Camera.main;
        }

        private void OnDisable() => Deactivate();

        private void LateUpdate()
        {
            bool onTitle = ui != null && ui.OnTitleScreen;

            if (onTitle && !_active) Activate();
            else if (!onTitle && _active) Deactivate();
            if (!_active) return;

            if (cam == null) { cam = Camera.main; if (cam == null) return; }

            // Cycle to the next map every cycleSeconds (so all maps get screen time).
            if (HasMaps)
            {
                _cycleT += Time.deltaTime;
                if (_cycleT >= cycleSeconds) ShowMap((_mapIdx + 1) % mapRoots.Length);
            }

            // Advance the slow orbit and place the camera at the 45° vantage looking at the map.
            _angle = FlybyOrbit.Advance(_angle, orbitSpeedDeg * Mathf.Deg2Rad, Time.deltaTime);
            float h = FlybyOrbit.HeightForElevation(orbitRadius, elevationDeg);
            Vector3 eye = lookCenter + new Vector3(
                FlybyOrbit.OffsetX(orbitRadius, _angle), h, FlybyOrbit.OffsetZ(orbitRadius, _angle));
            cam.transform.position = eye;
            Vector3 look = (lookCenter + Vector3.up * lookHeight) - eye;
            if (look.sqrMagnitude > 0.0001f)
                cam.transform.rotation = Quaternion.LookRotation(look);
        }

        private bool HasMaps => mapRoots != null && mapRoots.Length > 0;

        private void Activate()
        {
            _active = true;
            _angle = 0f;
            _mapIdx = -1;
            _cycleT = 0f;
            if (HasMaps) ShowMap(0);
        }

        private void Deactivate()
        {
            // Map-root visibility is owned solely by MapManager.Apply() once a session starts.
            // MenuFlyby must NOT toggle the shared map roots here: MapManager.OnNetworkSpawn→Apply()
            // activates the chosen root first, and this Deactivate runs afterwards — hiding the roots
            // here left the active map permanently disabled (Apply never re-runs per-frame), so only the
            // truck (moved, never toggled) stayed visible. Just reset preview state; ShowMap()'s exclusive
            // toggle handles title re-entry.
            if (!_active && _mapIdx < 0) return;
            _active = false;
            _mapIdx = -1;
        }

        private void ShowMap(int i)
        {
            if (!HasMaps) return;
            i = ((i % mapRoots.Length) + mapRoots.Length) % mapRoots.Length;
            for (int k = 0; k < mapRoots.Length; k++)
                if (mapRoots[k] != null) mapRoots[k].SetActive(k == i);
            _mapIdx = i;
            _cycleT = 0f;
        }
    }
}
