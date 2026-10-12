using Icefied.Core;
using Icefied.View;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Icefied.Game
{
    public enum LevelState { Preview, Playing, Dying, Won, Lost }

    /// <summary>Tek oyunculu level: 3 can, %80 hedef, yıldızlar.</summary>
    public class LevelController : MatchBase
    {
        const int StartLives = 3;

        public int Index { get; private set; }
        public LevelState State { get; private set; }
        public int Lives { get; private set; }
        public float SpeedTimer => Mathf.Max(0f, _boat.SpeedBoost);
        public int Stars => Percent >= 92f ? 3 : Percent >= 86f ? 2 : 1;
        public override bool IsPlaying => State == LevelState.Playing;

        BoatAgent _boat;
        float _stateTimer;

        public void Setup(GameRoot root, int index)
        {
            Index = index;
            Lives = StartLives;
            SetupBoard(root, LevelData.All[index]);

            var start = new Vector2Int(Data.Width / 2, 0);
            var view = CharacterFactory.Boat(transform, 0.72f, CharacterFactory.RedHull);
            _boat = new BoatAgent(0, CharacterFactory.RedHull, new HumanBrain(root.Input), start, view, 0f)
            {
                OnClose = Close,
                FindOwner = id => id == 0 ? _boat : null
            };

            SpawnBoss(new Vector2(Data.Width * 0.5f, Data.Height * 0.62f));
            var avoid = new[] { new Vector2(Data.Width * 0.5f, 0.5f) };
            for (int i = 0; i < Data.Penguins; i++)
                if (TryFindSpawn(1.6f, out var p, avoid)) SpawnPenguin(p);
            foreach (var kind in Data.Gifts)
                if (TryFindSpawn(2f, out var p, avoid)) SpawnGift(kind, p);

            State = LevelState.Preview;
            SyncVisuals(0f);
        }

        public void Begin()
        {
            Root.Input.Reset();
            State = LevelState.Playing;
        }

        public override Vector2? ChargeTarget(Vector2 from) => _boat.Drawing ? _boat.Pos : (Vector2?)null;

        // ================================================================ loop

        void Update()
        {
            float dt = Time.deltaTime;
            switch (State)
            {
                case LevelState.Preview:
                    TickEnemies(dt);
                    break;

                case LevelState.Playing:
#if UNITY_EDITOR
                    // Editör hilesi: K = level'ı anında kazan (UI akışını test etmek için).
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb != null && kb.kKey.wasPressedThisFrame) { Win(); break; }
#endif
                    FreezeTimer = Mathf.Max(0f, FreezeTimer - dt);
                    _boat.Step(this, dt);
                    if (State == LevelState.Playing)
                    {
                        TickEnemies(dt);
                        CheckCollisions();
                    }
                    break;

                case LevelState.Dying:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f) Respawn();
                    break;
            }

            SyncVisuals(dt);
        }

        void SyncVisuals(float dt)
        {
            SyncWorldVisuals(dt);
            _boat.SyncVisual(this, dt, State == LevelState.Playing);
        }

        // ================================================================ cut

        void Close(BoatAgent boat)
        {
            float before = Percent;
            var seed = BossSeed();
            Grid.CloseTrail(boat.Trail, seed.x, seed.y, Detached);
            boat.Trail.Clear();
            boat.Drawing = false;
            boat.Rewinding = false;
            RefreshBoardNow();

            float gained = Percent - before;
            MarkDetached();
            var piece = SpawnPiece(out Vector3 pieceCenter, Color.white);
            int caught = CatchEnemies(piece).Count;

            foreach (var g in TakeDetachedGifts())
            {
                var color = g.Kind == GiftKind.Freeze ? Mats.Hex("#38C6FF") : Mats.Hex("#FFC93C");
                if (g.Kind == GiftKind.Freeze)
                {
                    FreezeTimer = 4.5f;
                    Root.UI.Banner("FREEZE!", color);
                }
                else
                {
                    boat.SpeedBoost = 6f;
                    Root.UI.Banner("SPEED UP!", color);
                }
            }

            Root.Sfx.Play(Root.Sfx.Crack, 0.9f);
            Root.Sfx.Play(Root.Sfx.Splash, Mathf.Clamp(0.4f + gained * 0.05f, 0.4f, 1f));
            Root.Fx.Shake(0.04f + Mathf.Min(gained, 30f) * 0.006f, 0.35f);
            if (gained >= 0.5f)
                Root.UI.FloatText(pieceCenter + Vector3.up * 0.4f, $"+{gained:0}%", Color.white, 72);
            if (caught > 0)
            {
                Root.Sfx.Play(Root.Sfx.Pop, 0.8f);
                Root.UI.Banner(caught > 1 ? $"x{caught} CATCH!" : "GOTCHA!", Mats.Hex("#FFC93C"));
            }
            else if (gained >= 15f) Root.UI.Banner("AMAZING!", Mats.Hex("#FF6FD8"));
            else if (gained >= 8f) Root.UI.Banner("GREAT!", Mats.Hex("#5EE07A"));

            if (Percent >= Data.TargetPercent) Win();
        }

        // ================================================================ hit / death

        void CheckCollisions()
        {
            if (!_boat.Vulnerable) return;
            Vector2 bp = _boat.Pos;
            foreach (var e in Enemies)
            {
                if (!e.Alive) continue;
                float r = e.Radius + BoatAgent.HitRadius;
                if (e.TouchesTrail(Grid, _boat.Id) || (e.Pos - bp).sqrMagnitude < r * r)
                {
                    Die();
                    return;
                }
            }
        }

        void Die()
        {
            State = LevelState.Dying;
            _stateTimer = 0.9f;
            Lives--;
            Vector3 p = _boat.View.position + Vector3.up * 0.15f;
            Root.Fx.Shards(p, 26, Mats.Hex("#FF6B6B"), 1.2f);
            Root.Fx.Splash(p, 14, 1.2f);
            foreach (var c in _boat.Trail)
                if (Random.value < 0.25f)
                    Root.Fx.Shards(CellToWorld(new Vector2(c.x + 0.5f, c.y + 0.5f), IceTop), 2, Mats.Hex("#9FE8FF"), 0.6f);
            Root.Fx.Shake(0.18f, 0.45f);
            Root.Sfx.Play(Root.Sfx.Die);
            Root.UI.FlashDamage();
        }

        void Respawn()
        {
            _boat.ResetTrail(this);

            if (Lives <= 0)
            {
                State = LevelState.Lost;
                Root.OnLevelLost();
                return;
            }

            _boat.Invuln = 1.6f;
            Root.Input.Reset();
            State = LevelState.Playing;
        }

        // ================================================================ win

        void Win()
        {
            State = LevelState.Won;
            Root.Sfx.Play(Root.Sfx.Win, 0.9f, 0f);
            StartCoroutine(BossDefeat(Boss));
            foreach (var e in Enemies)
                if (e.Alive && !e.IsBoss) StartCoroutine(JumpIntoSea(e));
            int stars = Stars;
            Tween.After(0.5f, () => { if (this != null) Root.Fx.Confetti(BoardCenter + Vector3.up * 2.5f, 160); });
            Tween.After(1.7f, () => { if (this != null) Root.OnLevelWon(stars); });
        }
    }
}
