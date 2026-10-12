using System;
using Icefied.Core;
using Icefied.Game;
using Icefied.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Icefied.UI
{
    public class GameUI : MonoBehaviour
    {
        static readonly Color Navy = Mats.Hex("#14306B");
        static readonly Color NavyDeep = Mats.Hex("#0B1D47");
        static readonly Color Cream = Mats.Hex("#FFF8EC");
        static readonly Color Green = Mats.Hex("#45C960");
        static readonly Color Orange = Mats.Hex("#FF9A2E");
        static readonly Color Gold = Mats.Hex("#FFC93C");
        static readonly Color Pink = Mats.Hex("#FF5A7A");
        static readonly Color Cyan = Mats.Hex("#3FD6FF");
        static readonly Color Red = Mats.Hex("#FF4B4B");
        static readonly Color Blue = Mats.Hex("#3D8BFF");

        RectTransform _canvasRect, _safe;
        RectTransform _hud, _home, _modal, _fxLayer;
        Text _levelText, _percentText, _powerText;
        RectTransform _fill, _powerPill;
        Image[] _hearts;
        RectTransform _joyRing, _joyKnob;
        Image _damage;
        float _shownFill;
        int _shownLives = -1;
        Rect _lastSafe;

        // PvP HUD
        RectTransform _vsHud, _redFill, _timerPill, _rushPill;
        Text _redLabel, _blueLabel, _redPct, _bluePct, _timerText, _totalText;
        Image _timerBg;
        float _tugShown = 0.5f, _tugVel;

        // ============================================================ build

        public void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0f;
            gameObject.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)transform;

            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            _damage = UIFactory.Img(transform, "Damage", null, new Color(1f, 0.15f, 0.2f, 0f));
            _damage.rectTransform.Stretch();

            _safe = UIFactory.Rect("Safe", transform).Stretch();
            BuildHud();
            BuildVersusHud();
            BuildJoystick();
            _home = UIFactory.Rect("Home", _safe).Stretch();
            _modal = UIFactory.Rect("Modal", transform).Stretch();
            _fxLayer = UIFactory.Rect("FxLayer", transform).Stretch();
            _hud.gameObject.SetActive(false);
            _vsHud.gameObject.SetActive(false);
            ApplySafeArea();
        }

        void BuildHud()
        {
            _hud = UIFactory.Rect("HUD", _safe).Stretch();
            var top = new Vector2(0.5f, 1f);

            // Level etiketi
            var pill = UIFactory.Rect("LevelPill", _hud).Place(top, new Vector2(-330, -95), new Vector2(320, 104));
            var pillShadow = UIFactory.Img(pill, "Shadow", UIFactory.Rounded, new Color(0, 0, 0, 0.25f), 0.8f);
            pillShadow.rectTransform.Stretch();
            pillShadow.rectTransform.offsetMin += new Vector2(0, -10);
            pillShadow.rectTransform.offsetMax += new Vector2(0, -10);
            UIFactory.Img(pill, "Bg", UIFactory.Rounded, Navy, 0.8f).rectTransform.Stretch();
            UIFactory.Img(pill, "Gloss", UIFactory.Rounded, new Color(1, 1, 1, 0.12f), 0.6f).rectTransform.Stretch(8)
                .anchorMin = new Vector2(0, 0.5f);
            _levelText = UIFactory.Label(pill, "LEVEL 1", 52, Color.white, NavyDeep, 3f);

            // Canlar
            _hearts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var h = UIFactory.Rect("Heart" + i, _hud).Place(top, new Vector2(250 + i * 100, -95), new Vector2(96, 96));
                var hs = UIFactory.Img(h, "Shadow", UIFactory.Heart, new Color(0, 0, 0, 0.25f));
                hs.rectTransform.Stretch();
                hs.rectTransform.anchoredPosition = new Vector2(0, -8);
                var hi = UIFactory.Img(h, "Fill", UIFactory.Heart, Pink);
                hi.rectTransform.Stretch();
                var shine = UIFactory.Img(hi.transform, "Shine", UIFactory.Circle, new Color(1, 1, 1, 0.55f));
                shine.rectTransform.Place(new Vector2(0.32f, 0.68f), Vector2.zero, new Vector2(22, 16));
                _hearts[i] = hi;
            }

            // İlerleme çubuğu
            var bar = UIFactory.Rect("Progress", _hud).Place(top, new Vector2(0, -215), new Vector2(920, 78));
            var barShadow = UIFactory.Img(bar, "Shadow", UIFactory.Rounded, new Color(0, 0, 0, 0.25f), 0.6f);
            barShadow.rectTransform.Stretch();
            barShadow.rectTransform.offsetMin += new Vector2(0, -8);
            barShadow.rectTransform.offsetMax += new Vector2(0, -8);
            UIFactory.Img(bar, "Track", UIFactory.Rounded, NavyDeep, 0.6f).rectTransform.Stretch();
            var fillArea = UIFactory.Rect("FillArea", bar).Stretch(9);
            var fill = UIFactory.Img(fillArea, "Fill", UIFactory.Rounded, Cyan, 0.48f);
            _fill = fill.rectTransform;
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = new Vector2(0.05f, 1f);
            _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            var fillGloss = UIFactory.Img(_fill, "Gloss", UIFactory.Rounded, new Color(1, 1, 1, 0.35f), 0.3f);
            fillGloss.rectTransform.anchorMin = new Vector2(0, 0.55f);
            fillGloss.rectTransform.anchorMax = new Vector2(1, 0.85f);
            fillGloss.rectTransform.offsetMin = new Vector2(10, 0);
            fillGloss.rectTransform.offsetMax = new Vector2(-10, 0);
            _percentText = UIFactory.Label(bar, "0%", 46, Color.white, NavyDeep, 3f);

            var goal = UIFactory.Rect("Goal", bar).Place(new Vector2(1f, 0.5f), new Vector2(-4, 0), new Vector2(92, 92));
            UIFactory.Img(goal, "Shadow", UIFactory.Star, new Color(0, 0, 0, 0.3f)).rectTransform.Stretch()
                .anchoredPosition = new Vector2(0, -6);
            UIFactory.Img(goal, "Star", UIFactory.Star, Gold).rectTransform.Stretch();

            // Güçlendirme göstergesi
            _powerPill = UIFactory.Rect("Power", _hud).Place(top, new Vector2(0, -312), new Vector2(360, 76));
            UIFactory.Img(_powerPill, "Bg", UIFactory.Rounded, new Color(0.04f, 0.12f, 0.3f, 0.75f), 0.55f)
                .rectTransform.Stretch();
            _powerText = UIFactory.Label(_powerPill, "", 40, Color.white, NavyDeep, 2f);
            _powerPill.gameObject.SetActive(false);
        }

        void BuildVersusHud()
        {
            _vsHud = UIFactory.Rect("VersusHUD", _safe).Stretch();
            var top = new Vector2(0.5f, 1f);

            // Halat çekme çubuğu: kırmızı soldan, mavi sağdan dolar.
            var bar = UIFactory.Rect("Tug", _vsHud).Place(top, new Vector2(0, -100), new Vector2(1000, 104));
            var shadow = UIFactory.Img(bar, "Shadow", UIFactory.Rounded, new Color(0, 0, 0, 0.25f), 0.7f);
            shadow.rectTransform.Stretch();
            shadow.rectTransform.offsetMin += new Vector2(0, -10);
            shadow.rectTransform.offsetMax += new Vector2(0, -10);
            UIFactory.Img(bar, "Track", UIFactory.Rounded, NavyDeep, 0.7f).rectTransform.Stretch();
            var area = UIFactory.Rect("Area", bar).Stretch(9);
            UIFactory.Img(area, "Blue", UIFactory.Rounded, Blue, 0.55f).rectTransform.Stretch();
            var red = UIFactory.Img(area, "Red", UIFactory.Rounded, Red, 0.55f);
            _redFill = red.rectTransform;
            _redFill.anchorMin = Vector2.zero;
            _redFill.anchorMax = new Vector2(0.5f, 1f);
            _redFill.offsetMin = _redFill.offsetMax = Vector2.zero;
            var gloss = UIFactory.Img(area, "Gloss", UIFactory.Rounded, new Color(1, 1, 1, 0.22f), 0.35f);
            gloss.rectTransform.anchorMin = new Vector2(0, 0.55f);
            gloss.rectTransform.anchorMax = new Vector2(1, 0.88f);
            gloss.rectTransform.offsetMin = new Vector2(12, 0);
            gloss.rectTransform.offsetMax = new Vector2(-12, 0);
            var knot = UIFactory.Img(_redFill, "Knot", UIFactory.Circle, Cream);
            knot.rectTransform.anchorMin = knot.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            knot.rectTransform.sizeDelta = new Vector2(46, 46);
            knot.rectTransform.anchoredPosition = Vector2.zero;

            var left = UIFactory.Rect("RedLabel", bar).Place(new Vector2(0f, 0.5f), new Vector2(210, 2), new Vector2(380, 90));
            _redLabel = UIFactory.Label(left, "YOU 0", 50, Color.white, UIFactory.Darken(Red, 0.45f), 3f);
            _redLabel.alignment = TextAnchor.MiddleLeft;
            var right = UIFactory.Rect("BlueLabel", bar).Place(new Vector2(1f, 0.5f), new Vector2(-210, 2), new Vector2(380, 90));
            _blueLabel = UIFactory.Label(right, "0 BOT", 50, Color.white, UIFactory.Darken(Blue, 0.45f), 3f);
            _blueLabel.alignment = TextAnchor.MiddleRight;

            // Kopan buz yüzdeleri
            var lp = UIFactory.Rect("RedPct", _vsHud).Place(top, new Vector2(-370, -186), new Vector2(260, 60));
            _redPct = UIFactory.Label(lp, "0% ICE", 36, Color.white, NavyDeep, 2f);
            var rp = UIFactory.Rect("BluePct", _vsHud).Place(top, new Vector2(370, -186), new Vector2(260, 60));
            _bluePct = UIFactory.Label(rp, "0% ICE", 36, Color.white, NavyDeep, 2f);

            // Sayaç
            _timerPill = UIFactory.Rect("Timer", _vsHud).Place(top, new Vector2(0, -205), new Vector2(240, 100));
            _timerBg = UIFactory.Img(_timerPill, "Bg", UIFactory.Rounded, Navy, 0.7f);
            _timerBg.rectTransform.Stretch();
            _timerText = UIFactory.Label(_timerPill, "1:30", 60, Color.white, NavyDeep, 3f);

            var total = UIFactory.Rect("Total", _vsHud).Place(top, new Vector2(0, -282), new Vector2(500, 56));
            _totalText = UIFactory.Label(total, "", 34, new Color(1, 1, 1, 0.92f), NavyDeep, 2f);

            _rushPill = UIFactory.Rect("Rush", _vsHud).Place(top, new Vector2(190, -205), new Vector2(110, 70));
            UIFactory.Img(_rushPill, "Bg", UIFactory.Rounded, Pink, 0.55f).rectTransform.Stretch();
            UIFactory.Label(_rushPill, "x2", 46, Color.white, UIFactory.Darken(Pink, 0.5f), 3f);
            _rushPill.gameObject.SetActive(false);
        }

        void UpdateVersusHud(VersusController vs)
        {
            float dt = Time.deltaTime;
            var red = vs.Player;
            var blue = vs.Opponent;
            red.ShownScore = Mathf.MoveTowards(red.ShownScore, red.Score, Mathf.Max(60f, Mathf.Abs(red.Score - red.ShownScore) * 6f) * dt);
            blue.ShownScore = Mathf.MoveTowards(blue.ShownScore, blue.Score, Mathf.Max(60f, Mathf.Abs(blue.Score - blue.ShownScore) * 6f) * dt);
            _redLabel.text = $"{red.Name} {Mathf.RoundToInt(red.ShownScore)}";
            _blueLabel.text = $"{Mathf.RoundToInt(blue.ShownScore)} {blue.Name}";
            _redPct.text = $"{red.Percent:0}% ICE";
            _bluePct.text = $"{blue.Percent:0}% ICE";

            // Halat: puan oranına yaylanarak gider (kesim anında esneyip yerine oturur).
            int sum = red.Score + blue.Score;
            float target = sum == 0 ? 0.5f : Mathf.Lerp(0.12f, 0.88f, (float)red.Score / sum);
            _tugVel += ((target - _tugShown) * 90f - _tugVel * 9f) * dt;
            _tugShown += _tugVel * dt;
            _redFill.anchorMax = new Vector2(Mathf.Clamp(_tugShown, 0.06f, 0.94f), 1f);

            float t = Mathf.Ceil(vs.TimeLeft);
            _timerText.text = $"{(int)t / 60}:{(int)t % 60:00}";
            bool rush = vs.Rush;
            _rushPill.gameObject.SetActive(rush);
            if (rush)
            {
                float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * 1.5f)), 6f);
                _timerPill.localScale = Vector3.one * (1f + beat * 0.15f);
                _timerBg.color = Color.Lerp(Pink, Red, beat);
            }
            else
            {
                _timerPill.localScale = Vector3.one;
                _timerBg.color = Navy;
            }
            _totalText.text = $"ICE  {vs.Percent:0}% / {VersusController.TargetTotal:0}%";
        }

        void BuildJoystick()
        {
            _joyRing = UIFactory.Img(transform, "JoyRing", UIFactory.Ring, new Color(1, 1, 1, 0.3f)).rectTransform;
            _joyRing.sizeDelta = new Vector2(230, 230);
            _joyKnob = UIFactory.Img(transform, "JoyKnob", UIFactory.Circle, new Color(1, 1, 1, 0.5f)).rectTransform;
            _joyKnob.sizeDelta = new Vector2(110, 110);
            _joyRing.gameObject.SetActive(false);
            _joyKnob.gameObject.SetActive(false);
        }

        void ApplySafeArea()
        {
            var s = Screen.safeArea;
            if (s == _lastSafe || Screen.width == 0 || Screen.height == 0) return;
            _lastSafe = s;
            _safe.anchorMin = new Vector2(s.xMin / Screen.width, s.yMin / Screen.height);
            _safe.anchorMax = new Vector2(s.xMax / Screen.width, s.yMax / Screen.height);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        // ============================================================ tick

        void Update()
        {
            ApplySafeArea();
            var root = GameRoot.I;
            var match = root != null ? root.Match : null;
            if (match == null) return;
            var level = root.Level;

            if (root.Versus != null && _vsHud.gameObject.activeSelf) UpdateVersusHud(root.Versus);

            if (level != null && _hud.gameObject.activeSelf)
            {
                float p = level.Percent;
                float target = Mathf.Clamp01(p / level.Data.TargetPercent);
                _shownFill = Mathf.Lerp(_shownFill, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
                _fill.anchorMax = new Vector2(Mathf.Max(0.06f, _shownFill), 1f);
                _percentText.text = $"{p:0}%  /  {level.Data.TargetPercent:0}%";

                if (level.Lives != _shownLives)
                {
                    if (_shownLives > level.Lives && level.Lives >= 0 && level.Lives < _hearts.Length)
                        PopOut(_hearts[level.Lives]);
                    _shownLives = level.Lives;
                    for (int i = 0; i < _hearts.Length; i++)
                        if (i < level.Lives) { _hearts[i].color = Pink; _hearts[i].transform.localScale = Vector3.one; }
                }

                string power = level.FreezeTimer > 0f ? $"FREEZE  {level.FreezeTimer:0.0}s"
                             : level.SpeedTimer > 0f ? $"SPEED  {level.SpeedTimer:0.0}s" : null;
                _powerPill.gameObject.SetActive(power != null);
                if (power != null) _powerText.text = power;
            }

            var input = root.Input;
            bool showJoy = input.PointerActive && match.IsPlaying;
            _joyRing.gameObject.SetActive(showJoy);
            _joyKnob.gameObject.SetActive(showJoy);
            if (showJoy)
            {
                _joyRing.anchoredPosition = ScreenToCanvas(input.Anchor);
                _joyKnob.anchoredPosition = ScreenToCanvas(input.Current);
            }
        }

        Vector2 ScreenToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
            return local;
        }

        void PopOut(Image heart)
        {
            var tr = heart.transform;
            Tween.Do(0.35f, Ease.OutCubic, k =>
            {
                if (tr == null) return;
                tr.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.5f) * (1f - k * 0.25f);
                heart.color = Color.Lerp(Pink, new Color(0.2f, 0.25f, 0.4f, 0.55f), k);
            });
        }

        // ============================================================ screens

        public void BindLevel(LevelController level)
        {
            _levelText.text = $"LEVEL {level.Index + 1}";
            _shownFill = 0f;
            _shownLives = -1;
        }

        public void ShowHud(bool show) => _hud.gameObject.SetActive(show);

        public void ShowVersusHud(bool show) => _vsHud.gameObject.SetActive(show);

        public void BindVersus(VersusController vs)
        {
            _tugShown = 0.5f;
            _tugVel = 0f;
            ShowHud(false);
            ShowVersusHud(false);
            _home.gameObject.SetActive(false);
        }

        public void ShowHome(int levelIndex, Action onPlay, Action onVersus)
        {
            Clear(_home);
            Clear(_modal);
            _home.gameObject.SetActive(true);
            ShowHud(false);
            ShowVersusHud(false);

            var dim = UIFactory.Img(_home, "Dim", null, new Color(0.02f, 0.08f, 0.2f, 0.25f));
            dim.rectTransform.Stretch(-400);

            var title = UIFactory.Rect("Title", _home).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 560), new Vector2(1000, 260));
            var t = UIFactory.Label(title, "ICEFIED", 200, Color.white, Mats.Hex("#1B4FA8"), 9f);
            var t2 = t.gameObject.AddComponent<Outline>();
            t2.effectColor = Mats.Hex("#1B4FA8");
            t2.effectDistance = new Vector2(-9, 9);
            var sub = UIFactory.Rect("Sub", _home).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 410), new Vector2(1000, 80));
            UIFactory.Label(sub, "CUT THE ICE  •  SINK THE BEASTS", 44, Color.white, new Color(0.05f, 0.2f, 0.45f, 0.8f), 3f);
            StartCoroutine(Tween.Run(0.7f, Ease.OutBack, k => { if (title != null) title.localScale = Vector3.one * k; }));

            var data = LevelData.All[levelIndex];
            var info = UIFactory.Rect("Info", _home).Place(new Vector2(0.5f, 0f), new Vector2(0, 520), new Vector2(800, 90));
            UIFactory.Img(info, "Bg", UIFactory.Rounded, new Color(0.04f, 0.12f, 0.3f, 0.7f), 0.6f).rectTransform.Stretch();
            UIFactory.Label(info, $"LEVEL {levelIndex + 1}  ·  {data.Name.ToUpperInvariant()}", 46, Color.white, NavyDeep, 2f);

            var play = UIFactory.Button(_home, "PLAY", Green, new Vector2(600, 200), () =>
            {
                _home.gameObject.SetActive(false);
                onPlay();
            }, 96);
            play.Place(new Vector2(0.5f, 0f), new Vector2(-170, 300), new Vector2(520, 200));
            play.GetComponent<Pressable>().Breathe = true;

            var vs = UIFactory.Button(_home, "VS", Blue, new Vector2(300, 200), () =>
            {
                _home.gameObject.SetActive(false);
                onVersus();
            }, 96);
            vs.Place(new Vector2(0.5f, 0f), new Vector2(300, 300), new Vector2(300, 200));

            // Kupa + lig
            string league = PvpProfile.League;
            var trophy = UIFactory.Rect("Trophies", _home).Place(new Vector2(0.5f, 1f), new Vector2(0, -110), new Vector2(560, 96));
            UIFactory.Img(trophy, "Bg", UIFactory.Rounded, new Color(0.04f, 0.12f, 0.3f, 0.75f), 0.6f).rectTransform.Stretch();
            var cup = UIFactory.Img(trophy, "Cup", UIFactory.Star, Gold);
            cup.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(60, 0), new Vector2(76, 76));
            var tl = UIFactory.Label(trophy, $"{PvpProfile.Trophies}   ·   {league}", 46, Color.white, NavyDeep, 2f);
            tl.rectTransform.offsetMin = new Vector2(70, 0);
            var leagueDot = UIFactory.Img(trophy, "League", UIFactory.Circle, PvpProfile.LeagueColor(league));
            leagueDot.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-52, 0), new Vector2(52, 52));

            var hint = UIFactory.Rect("Hint", _home).Place(new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1000, 60));
            UIFactory.Label(hint, "Drag anywhere to steer  ·  Lift to rewind", 36, new Color(1, 1, 1, 0.9f),
                            new Color(0.05f, 0.2f, 0.45f, 0.6f), 2f);
        }

        public void ShowWin(int stars, float percent, bool lastLevel, Action onNext)
        {
            ShowHud(false);
            var card = Modal(lastLevel ? "ALL CLEAR!" : "LEVEL COMPLETE!", Green);

            var starRow = UIFactory.Rect("Stars", card).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 230), new Vector2(700, 260));
            for (int i = 0; i < 3; i++)
            {
                bool mid = i == 1;
                float size = mid ? 240 : 190;
                var slot = UIFactory.Rect("Star" + i, starRow)
                    .Place(new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 215, mid ? 30 : -10), new Vector2(size, size));
                slot.localRotation = Quaternion.Euler(0, 0, (1 - i) * 12);
                UIFactory.Img(slot, "Empty", UIFactory.Star, Mats.Hex("#E5DCCB")).rectTransform.Stretch();
                if (i >= stars) continue;
                var gold = UIFactory.Img(slot, "Gold", UIFactory.Star, Gold);
                gold.rectTransform.Stretch();
                var shine = UIFactory.Img(gold.transform, "Shine", UIFactory.Circle, new Color(1, 1, 1, 0.55f));
                shine.rectTransform.Place(new Vector2(0.38f, 0.62f), Vector2.zero, new Vector2(size * 0.16f, size * 0.12f));
                var gt = gold.transform;
                gt.localScale = Vector3.zero;
                float delay = 0.35f + i * 0.28f;
                StartCoroutine(Tween.Run(0.45f, Ease.OutBack, k => { if (gt != null) gt.localScale = Vector3.one * k; }, delay));
                Tween.After(delay, () => GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Star, 0.8f, 0f));
            }

            var pct = UIFactory.Rect("Percent", card).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(760, 100));
            UIFactory.Label(pct, $"{percent:0}% CLEARED", 74, Navy, new Color(0, 0, 0, 0), 0f);

            var hint = UIFactory.Rect("Hint", card).Place(new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(760, 80));
            string hintText = lastLevel ? "You beat all 3 levels!"
                            : stars < 3 ? "Finish with one big cut for more stars" : "Perfect cutting!";
            UIFactory.Label(hint, hintText, 40, Mats.Hex("#6B7A99"), new Color(0, 0, 0, 0), 0f);

            var btn = UIFactory.Button(card, lastLevel ? "PLAY AGAIN" : "NEXT", Green, new Vector2(560, 180), () =>
            {
                Clear(_modal);
                onNext();
            }, 84);
            btn.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(560, 180));
            btn.GetComponent<Pressable>().Breathe = true;
        }

        public void ShowLose(Action onRetry)
        {
            ShowHud(false);
            var card = Modal("OUT OF LIVES", Pink);

            var icon = UIFactory.Rect("Icon", card).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(240, 240));
            UIFactory.Img(icon, "Heart", UIFactory.Heart, Mats.Hex("#E5DCCB")).rectTransform.Stretch();
            var crack = UIFactory.Rect("Crack", icon).Place(new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(18, 180));
            crack.gameObject.AddComponent<Image>().color = Cream;
            crack.localRotation = Quaternion.Euler(0, 0, -12);

            var msg = UIFactory.Rect("Msg", card).Place(new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(760, 90));
            UIFactory.Label(msg, "The ice got the best of you!", 48, Navy, new Color(0, 0, 0, 0), 0f);

            var btn = UIFactory.Button(card, "RETRY", Orange, new Vector2(560, 180), () =>
            {
                Clear(_modal);
                onRetry();
            }, 84);
            btn.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(560, 180));
            btn.GetComponent<Pressable>().Breathe = true;
        }

        // ============================================================ versus screens

        public void ShowMatchmaking(PvpProfile.Opponent opp, Action onDone)
        {
            Clear(_modal);
            var dim = UIFactory.Img(_modal, "Dim", null, new Color(0.02f, 0.06f, 0.16f, 0.72f));
            dim.raycastTarget = true;
            dim.rectTransform.Stretch();
            StartCoroutine(MatchmakingRoutine(opp, onDone));
        }

        System.Collections.IEnumerator MatchmakingRoutine(PvpProfile.Opponent opp, Action onDone)
        {
            var center = new Vector2(0.5f, 0.5f);
            var search = UIFactory.Rect("Search", _modal).Place(center, Vector2.zero, new Vector2(1000, 600));
            var ring = UIFactory.Img(search, "Ring", UIFactory.Ring, Cyan);
            ring.rectTransform.Place(center, new Vector2(0, 90), new Vector2(220, 220));
            var dot = UIFactory.Img(ring.transform, "Dot", UIFactory.Circle, Color.white);
            dot.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -12), new Vector2(40, 40));
            var lbl = UIFactory.Rect("Label", search).Place(center, new Vector2(0, -110), new Vector2(1000, 90));
            var text = UIFactory.Label(lbl, "SEARCHING FOR OPPONENT", 54, Color.white, NavyDeep, 3f);

            float wait = UnityEngine.Random.Range(2f, 4f);
            for (float t = 0f; t < wait; t += Time.deltaTime)
            {
                if (ring == null) yield break;
                ring.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * 300f);
                text.text = "SEARCHING FOR OPPONENT" + new string('.', 1 + (int)(t * 3f) % 3);
                yield return null;
            }
            if (search == null) yield break;
            Destroy(search.gameObject);
            GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Collect, 0.8f, 0f);

            var card = UIFactory.Rect("VsCard", _modal).Place(center, Vector2.zero, new Vector2(1000, 700));
            PlayerBadge(card, new Vector2(-250, 60), "YOU", "", PvpProfile.Trophies, Red, true);
            PlayerBadge(card, new Vector2(250, 60), opp.Name, opp.Country, opp.Trophies, opp.Avatar, false);
            var vs = UIFactory.Rect("VS", card).Place(center, new Vector2(0, 80), new Vector2(240, 200));
            UIFactory.Label(vs, "VS", 150, Gold, UIFactory.Darken(Gold, 0.4f), 7f);
            vs.localScale = Vector3.zero;
            StartCoroutine(Tween.Run(0.5f, Ease.OutBack, k => { if (vs != null) vs.localScale = Vector3.one * k; }, 0.25f));
            var arena = UIFactory.Rect("Arena", card).Place(center, new Vector2(0, -260), new Vector2(900, 70));
            UIFactory.Label(arena, $"{LevelData.Versus.Name.ToUpperInvariant()}  ·  {VersusController.MatchTime:0}s", 44,
                            new Color(1, 1, 1, 0.9f), NavyDeep, 2f);

            yield return new WaitForSeconds(2.2f);
            if (card == null) yield break;
            Clear(_modal);
            Banner("GO!", Green);
            onDone();
        }

        void PlayerBadge(RectTransform parent, Vector2 pos, string name, string country, int trophies, Color avatar, bool left)
        {
            var center = new Vector2(0.5f, 0.5f);
            var b = UIFactory.Rect(name, parent).Place(center, pos, new Vector2(400, 520));
            var ring = UIFactory.Img(b, "Ring", UIFactory.Circle, left ? Red : Blue);
            ring.rectTransform.Place(center, new Vector2(0, 90), new Vector2(250, 250));
            var face = UIFactory.Img(ring.transform, "Face", UIFactory.Circle, avatar);
            face.rectTransform.Stretch(14);
            UIFactory.Label(face.transform, name.Substring(0, 1).ToUpperInvariant(), 130, Color.white,
                            UIFactory.Darken(avatar, 0.5f), 5f);
            var n = UIFactory.Rect("Name", b).Place(center, new Vector2(0, -80), new Vector2(400, 70));
            UIFactory.Label(n, name, 52, Color.white, NavyDeep, 3f);
            var info = UIFactory.Rect("Info", b).Place(center, new Vector2(0, -150), new Vector2(400, 60));
            string flag = string.IsNullOrEmpty(country) ? "" : country + "  ·  ";
            UIFactory.Label(info, $"{flag}{trophies}", 40, Gold, NavyDeep, 2f);

            float from = left ? -700f : 700f;
            b.anchoredPosition = pos + new Vector2(from, 0);
            StartCoroutine(Tween.Run(0.45f, Ease.OutBack, k =>
            {
                if (b != null) b.anchoredPosition = pos + new Vector2(from * (1f - k), 0);
            }));
        }

        public void ShowVersusResult(VersusController vs, int result, int trophyDelta, int trophies, string league,
                                     Action onRematch, Action onHome)
        {
            ShowVersusHud(false);
            string header = result > 0 ? "VICTORY!" : result < 0 ? "DEFEAT" : "DRAW";
            var card = Modal(header, result > 0 ? Green : result < 0 ? Pink : Orange);
            var center = new Vector2(0.5f, 0.5f);

            ResultColumn(card, new Vector2(-200, 170), vs.Player, Red, vs.Winner == vs.Player);
            ResultColumn(card, new Vector2(200, 170), vs.Opponent, Blue, vs.Winner == vs.Opponent);

            var tr = UIFactory.Rect("Trophies", card).Place(center, new Vector2(0, -120), new Vector2(700, 90));
            var cup = UIFactory.Img(tr, "Cup", UIFactory.Star, Gold);
            cup.rectTransform.Place(center, new Vector2(-190, 0), new Vector2(80, 80));
            string sign = trophyDelta > 0 ? "+" : "";
            UIFactory.Label(tr, $"{sign}{trophyDelta}    {trophies}", 62, trophyDelta >= 0 ? Green : Pink, new Color(0, 0, 0, 0), 0f)
                .rectTransform.offsetMin = new Vector2(60, 0);
            var lg = UIFactory.Rect("League", card).Place(center, new Vector2(0, -190), new Vector2(700, 56));
            UIFactory.Label(lg, $"{league} LEAGUE", 40, UIFactory.Darken(PvpProfile.LeagueColor(league), 0.75f), new Color(0, 0, 0, 0), 0f);

            var again = UIFactory.Button(card, "REMATCH", Green, new Vector2(400, 160), () =>
            {
                Clear(_modal);
                onRematch();
            }, 64);
            again.Place(center, new Vector2(-200, -320), new Vector2(380, 160));
            again.GetComponent<Pressable>().Breathe = true;
            var home = UIFactory.Button(card, "HOME", Navy, new Vector2(300, 160), () =>
            {
                Clear(_modal);
                onHome();
            }, 64);
            home.Place(center, new Vector2(220, -320), new Vector2(300, 160));
        }

        void ResultColumn(RectTransform card, Vector2 pos, VersusSide side, Color color, bool winner)
        {
            var center = new Vector2(0.5f, 0.5f);
            var col = UIFactory.Rect(side.Name, card).Place(center, pos, new Vector2(360, 380));
            UIFactory.Img(col, "Bg", UIFactory.Rounded, new Color(color.r, color.g, color.b, winner ? 0.22f : 0.1f), 0.8f)
                .rectTransform.Stretch();
            var n = UIFactory.Rect("Name", col).Place(new Vector2(0.5f, 1f), new Vector2(0, -50), new Vector2(360, 70));
            UIFactory.Label(n, side.Name, 46, color, new Color(0, 0, 0, 0), 0f);
            var sc = UIFactory.Rect("Score", col).Place(new Vector2(0.5f, 1f), new Vector2(0, -135), new Vector2(360, 100));
            UIFactory.Label(sc, side.Score.ToString(), 88, Navy, new Color(0, 0, 0, 0), 0f);
            var st = UIFactory.Rect("Stats", col).Place(new Vector2(0.5f, 0f), new Vector2(0, 100), new Vector2(340, 160));
            var stats = UIFactory.Label(st, $"ICE  {side.Percent:0}%\nPENGUINS  {side.Penguins}\nBREAKS  {side.Breaks}", 34,
                                        Mats.Hex("#6B7A99"), new Color(0, 0, 0, 0), 0f);
            stats.lineSpacing = 1.15f;
            if (winner)
            {
                var crown = UIFactory.Img(col, "Crown", UIFactory.Star, Gold);
                crown.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 10), new Vector2(80, 80));
            }
        }

        RectTransform Modal(string header, Color headerColor)
        {
            Clear(_modal);
            var dim = UIFactory.Img(_modal, "Dim", null, new Color(0.02f, 0.06f, 0.16f, 0f));
            dim.raycastTarget = true;
            dim.rectTransform.Stretch();
            StartCoroutine(Tween.Run(0.25f, Ease.OutCubic, k => { if (dim != null) dim.color = new Color(0.02f, 0.06f, 0.16f, 0.6f * k); }));

            var card = UIFactory.Rect("Card", _modal).Place(new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(880, 900));
            var shadow = UIFactory.Img(card, "Shadow", UIFactory.Rounded, new Color(0, 0, 0, 0.3f), 1.2f);
            shadow.rectTransform.Stretch();
            shadow.rectTransform.offsetMin += new Vector2(0, -22);
            shadow.rectTransform.offsetMax += new Vector2(0, -22);
            UIFactory.Img(card, "Border", UIFactory.Rounded, Mats.Hex("#D9CDB6"), 1.2f).rectTransform.Stretch();
            UIFactory.Img(card, "Bg", UIFactory.Rounded, Cream, 1.1f).rectTransform.Stretch(8);

            var ribbon = UIFactory.Rect("Ribbon", card).Place(new Vector2(0.5f, 1f), new Vector2(0, 0), new Vector2(760, 160));
            var rs = UIFactory.Img(ribbon, "Shadow", UIFactory.Rounded, UIFactory.Darken(headerColor, 0.65f), 0.9f);
            rs.rectTransform.Stretch();
            rs.rectTransform.offsetMin += new Vector2(0, -14);
            rs.rectTransform.offsetMax += new Vector2(0, -14);
            UIFactory.Img(ribbon, "Face", UIFactory.Rounded, headerColor, 0.9f).rectTransform.Stretch();
            var gl = UIFactory.Img(ribbon, "Gloss", UIFactory.Rounded, new Color(1, 1, 1, 0.25f), 0.6f);
            gl.rectTransform.anchorMin = new Vector2(0.04f, 0.52f);
            gl.rectTransform.anchorMax = new Vector2(0.96f, 0.88f);
            gl.rectTransform.offsetMin = gl.rectTransform.offsetMax = Vector2.zero;
            UIFactory.Label(ribbon, header, 66, Color.white, UIFactory.Darken(headerColor, 0.5f), 5f);

            card.localScale = Vector3.one * 0.5f;
            StartCoroutine(Tween.Run(0.45f, Ease.OutBack, k => { if (card != null) card.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, k); }));
            return card;
        }

        static void Clear(RectTransform layer)
        {
            for (int i = layer.childCount - 1; i >= 0; i--) Destroy(layer.GetChild(i).gameObject);
        }

        // ============================================================ juice

        public void Banner(string text, Color color)
        {
            var r = UIFactory.Rect("Banner", _fxLayer).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1000, 200));
            var label = UIFactory.Label(r, text, 124, Color.white, UIFactory.Darken(color, 0.55f), 7f);
            var inner = label.gameObject.AddComponent<Outline>();
            inner.effectColor = color;
            inner.effectDistance = new Vector2(-4, 4);
            r.localScale = Vector3.zero;
            r.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(-6f, 6f));
            StartCoroutine(BannerRoutine(r, label));
        }

        System.Collections.IEnumerator BannerRoutine(RectTransform r, Text label)
        {
            yield return Tween.Run(0.35f, Ease.OutBack, k => { if (r != null) r.localScale = Vector3.one * k; });
            yield return new WaitForSeconds(0.45f);
            Vector2 start = r.anchoredPosition;
            yield return Tween.Run(0.3f, Ease.InCubic, k =>
            {
                if (r == null) return;
                r.anchoredPosition = start + new Vector2(0, 80 * k);
                label.color = new Color(1, 1, 1, 1 - k);
                r.localScale = Vector3.one * (1f + 0.15f * k);
            });
            if (r != null) Destroy(r.gameObject);
        }

        public void FloatText(Vector3 world, string text, Color color, int size)
        {
            var cam = GameRoot.I.Cam;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            var r = UIFactory.Rect("Float", _fxLayer);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(400, 120);
            r.anchoredPosition = ScreenToCanvas(sp);
            var label = UIFactory.Label(r, text, size, color, Mats.Hex("#0B3A7A"), 5f);
            r.localScale = Vector3.zero;
            Vector2 start = r.anchoredPosition;
            StartCoroutine(Tween.Run(1.1f, Ease.Linear, k =>
            {
                if (r == null) return;
                r.localScale = Vector3.one * (k < 0.25f ? Ease.OutBack(k / 0.25f) : 1f);
                r.anchoredPosition = start + new Vector2(0, Ease.OutCubic(k) * 140f);
                label.color = new Color(color.r, color.g, color.b, k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f);
                if (k >= 1f) Destroy(r.gameObject);
            }));
        }

        public void FlashDamage()
        {
            StartCoroutine(Tween.Run(0.5f, Ease.Linear, k =>
            {
                float a = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
                _damage.color = new Color(1f, 0.15f, 0.2f, 0.35f * a);
            }));
        }
    }
}
