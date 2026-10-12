using UnityEngine;

namespace Icefied.Core
{
    public enum BossKind { Walrus, PolarBear, Yeti }

    public enum GiftKind { Freeze, Speed }

    public class LevelData
    {
        public string Name;
        public int Width = 54;
        public int Height = 90;
        public float TargetPercent = 80f;

        public int Penguins;
        public float PenguinSpeed;      // hücre / saniye

        public BossKind Boss;
        public float BossSpeed;         // hücre / saniye
        public float BossRadius;        // hücre
        public float ChargeInterval;    // 0 = boss saldırı yapmaz

        public GiftKind[] Gifts;

        public Color WaterDeep;
        public Color WaterShallow;

        /// <summary>PvP arenası: ortada kutup ayısı, ikisi için de tehlike.</summary>
        public static readonly LevelData Versus = new LevelData
        {
            Name = "Ice Race",
            Penguins = 3, PenguinSpeed = 6.5f,
            Boss = BossKind.PolarBear, BossSpeed = 5f, BossRadius = 5f, ChargeInterval = 7f,
            Gifts = new[] { GiftKind.Freeze, GiftKind.Speed },
            WaterDeep = new Color(0.04f, 0.33f, 0.68f), WaterShallow = new Color(0.24f, 0.78f, 0.95f)
        };

        public static readonly LevelData[] All =
        {
            new LevelData
            {
                Name = "Frosty Bay",
                Penguins = 2, PenguinSpeed = 6f,
                Boss = BossKind.Walrus, BossSpeed = 4.5f, BossRadius = 4.6f, ChargeInterval = 0f,
                Gifts = new[] { GiftKind.Freeze },
                WaterDeep = new Color(0.02f, 0.42f, 0.72f), WaterShallow = new Color(0.22f, 0.84f, 0.93f)
            },
            new LevelData
            {
                Name = "Polar Pass",
                Penguins = 3, PenguinSpeed = 7.5f,
                Boss = BossKind.PolarBear, BossSpeed = 6f, BossRadius = 5.0f, ChargeInterval = 6.5f,
                Gifts = new[] { GiftKind.Speed, GiftKind.Freeze },
                WaterDeep = new Color(0.05f, 0.30f, 0.66f), WaterShallow = new Color(0.25f, 0.72f, 0.95f)
            },
            new LevelData
            {
                Name = "Yeti Peak",
                Penguins = 5, PenguinSpeed = 8.5f,
                Boss = BossKind.Yeti, BossSpeed = 7f, BossRadius = 5.6f, ChargeInterval = 4.5f,
                Gifts = new[] { GiftKind.Freeze, GiftKind.Speed },
                WaterDeep = new Color(0.10f, 0.20f, 0.55f), WaterShallow = new Color(0.36f, 0.62f, 0.96f)
            }
        };
    }
}
