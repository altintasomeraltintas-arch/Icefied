using Icefied.Core;
using Icefied.Game;
using Icefied.UI;
using Icefied.View;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Icefied
{
    /// <summary>
    /// Oyunun giriş noktası. Sahnedeki tek obje budur; kamera, ışık, okyanus, UI ve level'ları koddan kurar.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameRoot : MonoBehaviour
    {
        const string ProgressKey = "icefied.level";
        const float CameraPitch = 58f;
        const float CameraFov = 32f;

        public static GameRoot I { get; private set; }

        public Camera Cam { get; private set; }
        public Fx Fx { get; private set; }
        public Sfx Sfx { get; private set; }
        public GameUI UI { get; private set; }
        public DirectionInput Input { get; } = new DirectionInput();
        public LevelController Level { get; private set; }
        public VersusController Versus { get; private set; }

        /// <summary>Şu an sahnedeki maç (tek oyunculu level ya da PvP).</summary>
        public MatchBase Match => Versus != null ? (MatchBase)Versus : Level;

        int _levelIndex;
        Vector3 _camBase;
        Vector2 _fitKey;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            Mats.Init();
            UIFactory.Init();
            BuildCamera();
            BuildLight();
            BuildOcean();
            BuildPostFx();

            Fx = new GameObject("Fx").AddComponent<Fx>();
            Fx.Init();
            Sfx = Cam.gameObject.AddComponent<Sfx>();
            Sfx.Init();
            UI = new GameObject("UI").AddComponent<GameUI>();
            UI.Build();

            _levelIndex = Mathf.Clamp(PlayerPrefs.GetInt(ProgressKey, 0), 0, LevelData.All.Length - 1);
            GoHome();
        }

        void Update()
        {
            Input.Update();
        }

        void LateUpdate()
        {
            FitCamera();
            Cam.transform.position = _camBase + Cam.transform.rotation * Fx.ShakeOffset;
        }

        // ============================================================ flow

        void ClearMatch()
        {
            if (Level != null) Destroy(Level.gameObject);
            if (Versus != null) Destroy(Versus.gameObject);
            Level = null;
            Versus = null;
            _fitKey = Vector2.zero;
        }

        void GoHome()
        {
            LoadLevel(_levelIndex);
            UI.ShowHome(_levelIndex, StartLevel, StartVersus);
        }

        void LoadLevel(int index)
        {
            ClearMatch();
            var go = new GameObject($"Level {index + 1}");
            Level = go.AddComponent<LevelController>();
            Level.Setup(this, index);
            UI.BindLevel(Level);
            _fitKey = Vector2.zero;
        }

        void StartLevel()
        {
            UI.ShowHud(true);
            Level.Begin();
        }

        public void OnLevelWon(int stars)
        {
            bool last = _levelIndex >= LevelData.All.Length - 1;
            int next = last ? 0 : _levelIndex + 1;
            PlayerPrefs.SetInt(ProgressKey, next);
            PlayerPrefs.Save();

            UI.ShowWin(stars, Level.Percent, last, () =>
            {
                _levelIndex = next;
                LoadLevel(next);
                if (last) UI.ShowHome(next, StartLevel, StartVersus);
                else StartLevel();
            });
        }

        public void OnLevelLost()
        {
            UI.ShowLose(() =>
            {
                LoadLevel(_levelIndex);
                StartLevel();
            });
        }

        // ============================================================ versus

        void StartVersus()
        {
            var opp = PvpProfile.RandomOpponent();
            ClearMatch();
            var go = new GameObject("Versus");
            Versus = go.AddComponent<VersusController>();
            Versus.Setup(this, opp.Name, PvpProfile.NextBotSkill);
            UI.BindVersus(Versus);
            UI.ShowMatchmaking(opp, () =>
            {
                UI.ShowVersusHud(true);
                Versus.Begin();
            });
        }

        public void OnVersusOver(VersusController vc)
        {
            int result = vc.Winner == vc.Player ? 1 : vc.Winner == null ? 0 : -1;
            int delta = PvpProfile.Record(result);
            UI.ShowVersusResult(vc, result, delta, PvpProfile.Trophies, PvpProfile.League, StartVersus, GoHome);
        }

        // ============================================================ scene

        void BuildCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            Cam = go.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = Mats.Hex("#0B4F8A");
            Cam.fieldOfView = CameraFov;
            Cam.nearClipPlane = 0.3f;
            Cam.farClipPlane = 200f;
            go.AddComponent<AudioListener>();
            go.transform.rotation = Quaternion.Euler(CameraPitch, 0, 0);

            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        void BuildLight()
        {
            var go = new GameObject("Sun");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
        }

        void BuildOcean()
        {
            var go = new GameObject("Ocean");
            go.AddComponent<MeshFilter>().sharedMesh = IceBoardView.FlatQuad(120f, Color.white);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Water;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildPostFx()
        {
            var go = new GameObject("PostFx");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.55f);
            bloom.threshold.Override(0.92f);
            bloom.scatter.Override(0.65f);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Mats.Hex("#06214A"));

            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(12f);
            color.contrast.Override(6f);

            volume.sharedProfile = profile;
        }

        /// <summary>Buz tahtasını her ekran oranında genişliğe sığdırır; üstte HUD, altta başparmak için yer bırakır.</summary>
        void FitCamera()
        {
            var key = new Vector2(Screen.width, Screen.height);
            var match = Match;
            if (key == _fitKey || match == null) return;
            _fitKey = key;

            float w = match.Data.Width * MatchBase.CellSize;
            float h = match.Data.Height * MatchBase.CellSize;
            var rot = Quaternion.Euler(CameraPitch, 0, 0);
            Cam.transform.rotation = rot;
            Vector3 fwd = rot * Vector3.forward;

            var corners = new[]
            {
                new Vector3(-w / 2, MatchBase.IceTop, -h / 2), new Vector3(w / 2, MatchBase.IceTop, -h / 2),
                new Vector3(-w / 2, MatchBase.IceTop, h / 2), new Vector3(w / 2, MatchBase.IceTop, h / 2),
                new Vector3(-w / 2, 0f, -h / 2), new Vector3(w / 2, 0f, -h / 2)
            };
            const float minX = 0.035f, maxX = 0.965f, minY = 0.07f, maxY = 0.81f;

            Vector3 target = Vector3.zero;
            float dist = 30f;
            for (int pass = 0; pass < 4; pass++)
            {
                float lo = 3f, hi = 120f;
                for (int i = 0; i < 24; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    Cam.transform.position = target - fwd * mid;
                    bool fits = true;
                    foreach (var c in corners)
                    {
                        var v = Cam.WorldToViewportPoint(c);
                        if (v.x < minX || v.x > maxX || v.y < minY || v.y > maxY) { fits = false; break; }
                    }
                    if (fits) hi = mid; else lo = mid;
                }
                dist = hi;
                Cam.transform.position = target - fwd * dist;

                float yMin = 1f, yMax = 0f;
                foreach (var c in corners)
                {
                    float y = Cam.WorldToViewportPoint(c).y;
                    yMin = Mathf.Min(yMin, y);
                    yMax = Mathf.Max(yMax, y);
                }
                float offset = (yMin + yMax) * 0.5f - (minY + maxY) * 0.5f;
                target.z += offset * h;
            }

            _camBase = target - fwd * dist;
            Cam.transform.position = _camBase;
        }
    }
}
