using System.Collections.Generic;
using Icefied.Core;
using Icefied.View;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Icefied.Game
{
    public enum VersusState { Preview, Playing, Over }

    /// <summary>Bir oyuncunun maç içi skoru ve istatistikleri.</summary>
    public class VersusSide
    {
        public BoatAgent Boat;
        public string Name;
        public int Score;
        public int Cells;          // kopardığı buz hücresi
        public int Penguins;       // batırdığı penguen (altın da 1 sayılır)
        public int Breaks;         // kırdığı rakip izi
        public float Percent;      // kopardığı buzun başlangıç buzuna oranı
        public float ShownScore;   // UI'daki yaylanan değer
    }

    /// <summary>
    /// "Ice Race": iki tekne aynı buzu aynı anda keser. Kesilen buz (%1 = 10), batan penguen (50, altın 150),
    /// hediye (30 + güç), rakip izini kırmak (75), büyük kesim (+50) ve bitirici kesim (+100) puan verir.
    /// Maç toplam %80 kesimde ya da 90 saniyede biter, puanı yüksek olan kazanır. Son 10 saniyede puanlar x2.
    /// Kırmızı (id 0) oyuncu alttan, mavi (id 1) bot üstten başlar.
    /// </summary>
    public class VersusController : MatchBase
    {
        // ---------------------------------------------------------------- kurallar
        public const float MatchTime = 90f;
        public const float TargetTotal = 80f;      // toplam kopan buz bu yüzdeye ulaşınca maç biter
        public const float FinalRush = 10f;        // son 10 saniyede puanlar x2
        const float StunTime = 2f;
        const float InvulnAfterStun = 1.5f;
        const float CloseGrace = 0.5f;             // süre bitince kapanmak üzere olan ize tanınan tolerans
        const float PenguinRespawn = 4f;
        const float GiftRespawn = 12f;
        const float GoldenChance = 0.2f;

        // ---------------------------------------------------------------- puanlar
        public const int PointsPerPercent = 10;    // %1 buz = 10 puan
        public const int PenguinPoints = 50;
        public const int GoldenPenguinPoints = 150;
        public const int BreakPoints = 75;         // rakibin izini kırmak
        public const int GiftPoints = 30;          // + Freeze/Speed etkisi
        public const int BigCutPoints = 50;        // tek kesimde %15+
        public const float BigCutPercent = 15f;
        public const int FinisherPoints = 100;     // toplamı %80'e ulaştıran kesim; boss o anda batar

        public VersusState State { get; private set; }
        public float TimeLeft { get; private set; }
        public bool Rush => State == VersusState.Playing && TimeLeft <= FinalRush;
        public override bool IsPlaying => State == VersusState.Playing;

        public readonly VersusSide Red = new VersusSide();
        public readonly VersusSide Blue = new VersusSide();
        public VersusSide Player => Red;
        public VersusSide Opponent => Blue;
        public BotBrain Bot { get; private set; }

        /// <summary>Kazanan; beraberlikte null.</summary>
        public VersusSide Winner { get; private set; }

        float _grace;
        float _giftTimer;
        bool _rushAnnounced;
        readonly List<float> _penguinQueue = new List<float>();

        public void Setup(GameRoot root, string opponentName, float botSkill)
        {
            SetupBoard(root, LevelData.Versus);
            TimeLeft = MatchTime;

            var redStart = new Vector2Int(Data.Width / 2, 0);
            var blueStart = new Vector2Int(Data.Width / 2, Data.Height - 1);

            var redView = CharacterFactory.Boat(transform, 0.72f, CharacterFactory.RedHull);
            Red.Boat = new BoatAgent(0, CharacterFactory.RedHull, new HumanBrain(root.Input), redStart, redView, 0f);
            Red.Name = "YOU";

            var blueView = CharacterFactory.Boat(transform, 0.72f, CharacterFactory.BlueHull);
            Bot = new BotBrain(botSkill);
            Blue.Boat = new BoatAgent(1, CharacterFactory.BlueHull, Bot, blueStart, blueView, 180f) { PlaySounds = false };
            Blue.Name = opponentName;
            Bot.Bind(this, Blue.Boat, Red.Boat);

            foreach (var s in new[] { Red, Blue })
            {
                s.Boat.OnClose = Close;
                s.Boat.OnBreakOther = BreakByRival;
                s.Boat.FindOwner = id => id == 0 ? Red.Boat : id == 1 ? Blue.Boat : null;
            }

            SpawnBoss(new Vector2(Data.Width * 0.5f, Data.Height * 0.5f));
            var avoid = new[] { (Vector2)redStart, (Vector2)blueStart };
            for (int i = 0; i < Data.Penguins; i++)
                if (TryFindSpawn(1.6f, out var p, avoid, 20f, 0.1f, 0.9f)) SpawnPenguin(p, i == 0);
            foreach (var kind in Data.Gifts)
                if (TryFindSpawn(2f, out var p, avoid, 20f, 0.1f, 0.9f)) SpawnGift(kind, p);
            _giftTimer = GiftRespawn;

            State = VersusState.Preview;
            SyncVisuals(0f);
        }

        public void Begin()
        {
            Root.Input.Reset();
            State = VersusState.Playing;
        }

        public VersusSide SideOf(BoatAgent b) => b == Red.Boat ? Red : Blue;
        public VersusSide RivalOf(BoatAgent b) => b == Red.Boat ? Blue : Red;

        /// <summary>Boss en yakın, çizim yapan ve korunmasız tekneye saldırır.</summary>
        public override Vector2? ChargeTarget(Vector2 from)
        {
            Vector2? best = null;
            float bestD = float.MaxValue;
            foreach (var s in new[] { Red, Blue })
            {
                if (!s.Boat.Vulnerable) continue;
                float d = (s.Boat.Pos - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s.Boat.Pos; }
            }
            return best;
        }

        // ================================================================ loop

        void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case VersusState.Preview:
                    TickEnemies(dt);
                    break;

                case VersusState.Playing:
                    TickPlaying(dt);
                    break;
            }
            SyncVisuals(dt);
        }

        void TickPlaying(float dt)
        {
            if (TimeLeft > 0f)
            {
                TimeLeft = Mathf.Max(0f, TimeLeft - dt);
                if (!_rushAnnounced && TimeLeft <= FinalRush)
                {
                    _rushAnnounced = true;
                    Root.UI.Banner("x2 POINTS!", Mats.Hex("#FF5A7A"));
                    Root.Sfx.Play(Root.Sfx.Warn, 0.8f, 0f);
                }
            }

            Bot.Tick(dt);

            // Sırayı her karede karıştır: aynı karede iki kapanış olursa "ilk varan" adil biçimde belirlenir.
            bool redFirst = Random.value < 0.5f;
            StepBoat(redFirst ? Red : Blue, dt);
            StepBoat(redFirst ? Blue : Red, dt);
            if (State != VersusState.Playing) return;

            TickEnemies(dt);
            CheckHeadOn();
            CheckEnemyHits(Red);
            CheckEnemyHits(Blue);
            TickRespawns(dt);

            if (TimeLeft <= 0f)
            {
                // Kural 9: kapanışa giden bir tekne varsa kısa bir tolerans tanı.
                if (AboutToClose(Red.Boat) || AboutToClose(Blue.Boat))
                {
                    _grace += dt;
                    if (_grace < CloseGrace) return;
                }
                End("TIME UP!");
            }
        }

        void StepBoat(VersusSide s, float dt)
        {
            if (State != VersusState.Playing) return;
            bool wasStunned = s.Boat.Stun > 0f;
            s.Boat.Step(this, dt);
            if (wasStunned && s.Boat.Stun <= 0f) s.Boat.EnsureOnEdge(this);
        }

        bool AboutToClose(BoatAgent b)
        {
            return b.Drawing && !b.Rewinding && b.Moving && Grid.Get(b.Target.x, b.Target.y) == Cell.Water;
        }

        void SyncVisuals(float dt)
        {
            SyncWorldVisuals(dt);
            bool playing = State == VersusState.Playing;
            Red.Boat.SyncVisual(this, dt, playing);
            Blue.Boat.SyncVisual(this, dt, playing);
        }

        // ================================================================ puan

        int Award(VersusSide s, int points, Vector3 at, string label = null)
        {
            if (Rush) points *= 2;
            s.Score += points;
            if (label != null)
                Root.UI.FloatText(at + Vector3.up * 0.4f, $"{label} +{points}", s.Boat.Color, 60);
            return points;
        }

        // ================================================================ kesim

        void Close(BoatAgent boat)
        {
            var side = SideOf(boat);
            var rival = RivalOf(boat);
            var seed = BossSeed();
            int iceBefore = Grid.IceCount;

            Grid.CloseTrail(boat.Trail, seed.x, seed.y, Detached);
            boat.Trail.Clear();
            boat.Drawing = false;
            boat.Rewinding = false;
            MarkDetached();

            // Kural 4: rakibin açık izi kopan bölgede kaldıysa izi kırılır, rakip en yakın kenarda yeniden doğar.
            bool rivalCaught = false;
            foreach (var c in rival.Boat.Trail)
                if (DetachedMask[Grid.Index(c.x, c.y)]) { rivalCaught = true; break; }
            if (!rivalCaught && rival.Boat.Drawing)
            {
                var rc = rival.Boat.Cell;
                rivalCaught = Grid.InBounds(rc.x, rc.y) && DetachedMask[Grid.Index(rc.x, rc.y)];
            }

            RefreshBoardNow();

            int cells = iceBefore - Grid.IceCount;
            float gained = 100f * cells / Grid.InitialIceCount;
            side.Cells += cells;
            side.Percent = 100f * side.Cells / Grid.InitialIceCount;

            var piece = SpawnPiece(out Vector3 pieceCenter, boat.Color);
            int icePoints = Mathf.RoundToInt(gained * PointsPerPercent);
            if (icePoints > 0) Award(side, icePoints, pieceCenter, $"{gained:0}%");

            var caught = CatchEnemies(piece);
            int golden = 0;
            foreach (var e in caught)
            {
                side.Penguins++;
                if (e.IsGolden) golden++;
                Award(side, e.IsGolden ? GoldenPenguinPoints : PenguinPoints, e.transform.position, e.IsGolden ? "GOLD" : null);
                _penguinQueue.Add(PenguinRespawn + Random.Range(0f, 1.5f));
            }

            foreach (var g in TakeDetachedGifts())
            {
                Award(side, GiftPoints, g.T.position);
                ApplyGift(side, rival, g.Kind);
            }

            if (gained >= BigCutPercent) Award(side, BigCutPoints, pieceCenter + Vector3.up * 0.5f, "BIG CUT");

            if (rivalCaught) BreakTrail(rival, side, "SUNK!");

            bool mine = side == Player;
            Root.Sfx.Play(Root.Sfx.Crack, mine ? 0.9f : 0.5f);
            Root.Sfx.Play(Root.Sfx.Splash, Mathf.Clamp(0.3f + gained * 0.05f, 0.3f, mine ? 1f : 0.6f));
            Root.Fx.Shake((0.03f + Mathf.Min(gained, 30f) * 0.005f) * (mine ? 1f : 0.6f), 0.3f);
            if (caught.Count > 0)
            {
                Root.Sfx.Play(Root.Sfx.Pop, 0.8f);
                if (mine) Root.UI.Banner(golden > 0 ? "GOLDEN CATCH!" : caught.Count > 1 ? $"x{caught.Count} CATCH!" : "GOTCHA!", Mats.Hex("#FFC93C"));
            }
            else if (mine && gained >= BigCutPercent) Root.UI.Banner("AMAZING!", Mats.Hex("#FF6FD8"));
            else if (mine && gained >= 8f) Root.UI.Banner("GREAT!", Mats.Hex("#5EE07A"));

            if (Percent >= TargetTotal)
            {
                Award(side, FinisherPoints, BoardCenter + Vector3.up * 0.8f, "FINISHER");
                StartCoroutine(BossDefeat(Boss));
                End("ICE CLEARED!");
                return;
            }

            // Kural 7: kalan buz boss'un alanının ~2 katının altına indiyse kesim yapacak yer kalmamıştır.
            float bossArea = Mathf.PI * Data.BossRadius * Data.BossRadius;
            if (Grid.IceCount < bossArea * 2f) End("ICE CLEARED!");
        }

        void ApplyGift(VersusSide side, VersusSide rival, GiftKind kind)
        {
            if (kind == GiftKind.Freeze)
            {
                // Rakibi dondurur; çizim yapıyorsa geri saramaz. Bu sırada kırılırsa puan dondurana gitmez (kural 11).
                rival.Boat.Frozen = 3f;
                Root.UI.Banner(side == Player ? "FREEZE!" : "FROZEN!", Mats.Hex("#38C6FF"));
                Root.Fx.Glow(rival.Boat.View.position + Vector3.up * 0.3f, Mats.Hex("#9FE8FF"), 20);
            }
            else
            {
                side.Boat.SpeedBoost = 6f;
                if (side == Player) Root.UI.Banner("SPEED UP!", Mats.Hex("#FFC93C"));
            }
        }

        // ================================================================ iz kırma

        /// <summary>Kural 5: rakibin izine giren onu kırar.</summary>
        void BreakByRival(BoatAgent attacker, BoatAgent victim)
        {
            if (victim == null || victim == attacker) return;
            BreakTrail(SideOf(victim), SideOf(attacker), "BREAK!");
        }

        /// <summary>
        /// Kural 8: her iz kırılması aynı sonucu doğurur — iz geri dolar, tekne izin başladığı yere döner,
        /// 2 sn sersemler, ardından 1.5 sn dokunulmaz olur. Puanı sadece rakip kırdıysa rakip alır.
        /// </summary>
        void BreakTrail(VersusSide victim, VersusSide by, string label)
        {
            var b = victim.Boat;
            Vector3 at = b.View.position + Vector3.up * 0.15f;
            foreach (var c in b.Trail)
                if (Random.value < 0.25f)
                    Root.Fx.Shards(CellToWorld(new Vector2(c.x + 0.5f, c.y + 0.5f), IceTop), 2, b.Color, 0.6f);

            b.ResetTrail(this);
            b.Stun = StunTime;
            b.Invuln = StunTime + InvulnAfterStun;
            b.Frozen = 0f;

            Root.Fx.Shards(at, 20, b.Color, 1.1f);
            Root.Fx.Splash(at, 10, 1f);
            Root.Sfx.Play(Root.Sfx.Die, victim == Player ? 1f : 0.6f);

            if (by != null)
            {
                by.Breaks++;
                Award(by, BreakPoints, at, label);
            }

            if (victim == Player)
            {
                Root.Fx.Shake(0.14f, 0.4f);
                Root.UI.FlashDamage();
                if (by != null) Root.UI.Banner("CUT OFF!", Mats.Hex("#3D8BFF"));
            }
            else if (by == Player)
            {
                Root.Fx.Shake(0.08f, 0.3f);
                Root.UI.Banner(label, Mats.Hex("#FF4B4B"));
            }
        }

        /// <summary>Kafa kafaya: iki tekne aynı karede ve ikisi de çiziyorsa ikisinin izi de kırılır.</summary>
        void CheckHeadOn()
        {
            var a = Red.Boat;
            var b = Blue.Boat;
            if (!a.Vulnerable || !b.Vulnerable) return;
            if ((a.Pos - b.Pos).sqrMagnitude > 0.8f * 0.8f) return;
            BreakTrail(Red, null, null);
            BreakTrail(Blue, null, null);
        }

        void CheckEnemyHits(VersusSide s)
        {
            var b = s.Boat;
            if (!b.Vulnerable) return;
            Vector2 bp = b.Pos;
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                float r = e.Radius + BoatAgent.HitRadius;
                if (e.TouchesTrail(Grid, b.Id) || (e.Pos - bp).sqrMagnitude < r * r)
                {
                    BreakTrail(s, null, null);
                    return;
                }
            }
        }

        // ================================================================ yeniden doğma

        void TickRespawns(float dt)
        {
            for (int i = _penguinQueue.Count - 1; i >= 0; i--)
            {
                _penguinQueue[i] -= dt;
                if (_penguinQueue[i] > 0f) continue;
                // Kural 10: izlerden (TryFindSpawn) ve teknelerden en az 10 hücre uzağa doğ.
                var avoid = new[] { Red.Boat.Pos, Blue.Boat.Pos };
                if (TryFindSpawn(1.6f, out var p, avoid, 10f, 0f, 1f))
                {
                    var e = SpawnPenguin(p, Random.value < GoldenChance);
                    StartCoroutine(JumpOntoIce(e));
                    _penguinQueue.RemoveAt(i);
                }
                else _penguinQueue[i] = 1f;
            }

            if (Gifts.Count >= Data.Gifts.Length) return;
            _giftTimer -= dt;
            if (_giftTimer > 0f) return;
            _giftTimer = GiftRespawn;
            var kind = Data.Gifts[Random.Range(0, Data.Gifts.Length)];
            if (TryFindSpawn(2f, out var gp, new[] { Red.Boat.Pos, Blue.Boat.Pos }, 10f, 0f, 1f)) SpawnGift(kind, gp);
        }

        // ================================================================ bitiş

        void End(string reason)
        {
            if (State == VersusState.Over) return;
            State = VersusState.Over;
            Red.Boat.ResetTrail(this);
            Blue.Boat.ResetTrail(this);

            // Puan → beraberlikte kopan alan → o da eşitse batan penguen.
            int cmp = Red.Score.CompareTo(Blue.Score);
            if (cmp == 0) cmp = Red.Cells.CompareTo(Blue.Cells);
            if (cmp == 0) cmp = Red.Penguins.CompareTo(Blue.Penguins);
            Winner = cmp > 0 ? Red : cmp < 0 ? Blue : null;

            Root.UI.Banner(reason, Mats.Hex("#FFC93C"));
            bool won = Winner == Player;
            Root.Sfx.Play(won ? Root.Sfx.Win : Root.Sfx.Die, 0.9f, 0f);
            if (won)
            {
                Tween.After(0.5f, () => { if (this != null) Root.Fx.Confetti(BoardCenter + Vector3.up * 2.5f, 160); });
            }
            foreach (var e in Enemies)
                if (e.Alive && !e.IsBoss) StartCoroutine(JumpIntoSea(e));
            Tween.After(1.8f, () => { if (this != null) Root.OnVersusOver(this); });
        }
    }
}
