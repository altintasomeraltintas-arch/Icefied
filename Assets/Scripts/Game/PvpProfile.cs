using UnityEngine;

namespace Icefied.Game
{
    /// <summary>Kupa, lig ve botun zorluk ayarı. PlayerPrefs'te saklanır.</summary>
    public static class PvpProfile
    {
        const string TrophyKey = "icefied.pvp.trophies";
        const string MatchesKey = "icefied.pvp.matches";
        const string SkillKey = "icefied.pvp.skill";

        const int EasyMatches = 3;        // yeni oyuncu ilk 3 maçı kolay rakiple oynar
        const float EasySkill = 0.05f;
        const float StartSkill = 0.35f;
        // Kazanınca bot güçlenir, kaybedince zayıflar. Denge noktası: kazanma oranı = Down / (Up + Down) ≈ %57.
        const float SkillUp = 0.06f;
        const float SkillDown = 0.08f;

        public const int WinTrophies = 30;
        public const int LossTrophies = 20;

        public static int Trophies => PlayerPrefs.GetInt(TrophyKey, 0);
        public static int Matches => PlayerPrefs.GetInt(MatchesKey, 0);
        public static string League => LeagueOf(Trophies);

        public static string LeagueOf(int trophies) => trophies >= 1000 ? "GOLD" : trophies >= 400 ? "SILVER" : "BRONZE";

        public static Color LeagueColor(string league) =>
            league == "GOLD" ? new Color(1f, 0.79f, 0.24f) : league == "SILVER" ? new Color(0.78f, 0.84f, 0.92f) : new Color(0.85f, 0.55f, 0.32f);

        /// <summary>Bir sonraki maçın bot becerisi (0..1).</summary>
        public static float NextBotSkill => Matches < EasyMatches ? EasySkill : PlayerPrefs.GetFloat(SkillKey, StartSkill);

        /// <summary>Maç sonucunu kaydeder ve kupa değişimini döner. result: 1 galibiyet, 0 beraberlik, -1 mağlubiyet.</summary>
        public static int Record(int result)
        {
            int delta = result > 0 ? WinTrophies + Random.Range(0, 5) : result < 0 ? -LossTrophies : 0;
            int trophies = Mathf.Max(0, Trophies + delta);
            delta = trophies - Trophies;

            float skill = PlayerPrefs.GetFloat(SkillKey, StartSkill);
            if (Matches >= EasyMatches)
                skill = Mathf.Clamp(skill + (result > 0 ? SkillUp : result < 0 ? -SkillDown : 0f), 0.05f, 1f);

            PlayerPrefs.SetInt(TrophyKey, trophies);
            PlayerPrefs.SetInt(MatchesKey, Matches + 1);
            PlayerPrefs.SetFloat(SkillKey, skill);
            PlayerPrefs.Save();
            return delta;
        }

        // ---------------------------------------------------------------- rakip kimliği

        static readonly string[] Names =
        {
            "IceKing", "Polar_Pro", "FrostByte", "Yeti99", "NordicNinja", "Snowflake", "BlueWhale", "Glacier",
            "ArcticFox", "Buzkiran", "Pinguino", "Walrus_OP", "ColdSnap", "Icebreaker", "Aurora", "Floe"
        };

        static readonly string[] Countries = { "TR", "DE", "US", "BR", "FR", "GB", "ES", "IT", "PL", "NL", "SE", "JP" };

        public struct Opponent
        {
            public string Name;
            public string Country;
            public int Trophies;
            public Color Avatar;
        }

        public static Opponent RandomOpponent()
        {
            return new Opponent
            {
                Name = Names[Random.Range(0, Names.Length)],
                Country = Countries[Random.Range(0, Countries.Length)],
                Trophies = Mathf.Max(0, Trophies + Random.Range(-40, 41)),
                Avatar = Color.HSVToRGB(Random.value, 0.55f, 0.95f)
            };
        }
    }
}
