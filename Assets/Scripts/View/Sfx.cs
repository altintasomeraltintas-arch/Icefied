using UnityEngine;

namespace Icefied.View
{
    /// <summary>Asset gerektirmeyen prosedürel ses efektleri. Sonra gerçek seslerle değiştirilebilir.</summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;

        AudioSource _src;
        public AudioClip Pop, Crack, Crunch, Splash, Die, Win, Collect, Click, Star, Roar, Warn;

        public void Init()
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;

            Pop = Make("pop", 0.12f, (t, d) => Sine(Mathf.Lerp(900, 380, t / d), t) * Env(t, d, 0.005f));
            Click = Make("click", 0.05f, (t, d) => Sine(1400, t) * Env(t, d, 0.002f) * 0.6f);
            Crunch = Make("crunch", 0.06f, (t, d) => Noise() * Env(t, d, 0.002f) * 0.35f);
            Crack = Make("crack", 0.35f, (t, d) =>
                (Noise() * Mathf.Exp(-t * 30f) * 0.9f + Sine(Mathf.Lerp(140, 60, t / d), t) * Mathf.Exp(-t * 9f)) * 0.8f);
            float lp = 0f;
            Splash = Make("splash", 0.7f, (t, d) =>
            {
                float k = Mathf.Lerp(0.5f, 0.05f, t / d);
                lp += (Noise() - lp) * k;
                return lp * Env(t, d, 0.01f) * 1.6f;
            });
            Die = Make("die", 0.55f, (t, d) => Square(Mathf.Lerp(520, 90, t / d), t) * Env(t, d, 0.005f) * 0.35f);
            Collect = Make("collect", 0.35f, (t, d) =>
                (Sine(t < 0.12f ? 988 : 1319, t) + 0.3f * Sine(t < 0.12f ? 1976 : 2637, t)) * Env(t, d, 0.004f) * 0.55f);
            Star = Make("star", 0.3f, (t, d) =>
                (Sine(1568, t) + 0.4f * Sine(3136, t)) * Env(t, d, 0.003f) * 0.5f);
            Win = Make("win", 1.0f, (t, d) =>
            {
                float[] notes = { 523.3f, 659.3f, 784f, 1046.5f };
                int i = Mathf.Min(3, (int)(t / 0.13f));
                float nt = t - i * 0.13f;
                float env = Mathf.Exp(-nt * (i == 3 ? 3f : 10f));
                return (Sine(notes[i], t) + 0.35f * Sine(notes[i] * 2f, t)) * env * 0.45f;
            });
            Roar = Make("roar", 0.5f, (t, d) =>
                (Square(Mathf.Lerp(110, 70, t / d), t) * 0.5f + Noise() * 0.5f) * Env(t, d, 0.03f) * 0.4f);
            Warn = Make("warn", 0.25f, (t, d) => Square(t % 0.12f < 0.06f ? 880 : 660, t) * Env(t, d, 0.005f) * 0.18f);
        }

        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null || _src == null) return;
            _src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            _src.PlayOneShot(clip, volume);
        }

        delegate float Gen(float t, float duration);

        static AudioClip Make(string name, float duration, Gen gen)
        {
            int n = Mathf.CeilToInt(duration * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
                data[i] = Mathf.Clamp(gen(i / (float)Rate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
        static float Square(float f, float t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));
        static float Noise() => Random.value * 2f - 1f;

        static float Env(float t, float d, float attack)
        {
            float a = Mathf.Clamp01(t / attack);
            float r = 1f - Mathf.Clamp01(t / d);
            return a * r * r;
        }
    }
}
