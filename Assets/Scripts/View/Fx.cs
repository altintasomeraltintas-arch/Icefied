using UnityEngine;

namespace Icefied.View
{
    /// <summary>Paylaşılan partikül sistemleri + kamera sarsıntısı.</summary>
    public class Fx : MonoBehaviour
    {
        ParticleSystem _soft;     // su sıçraması, köpük, iz
        ParticleSystem _shards;   // buz kırıkları
        ParticleSystem _confetti;
        ParticleSystem _glow;     // additive parıltı

        float _shakeAmp, _shakeTime, _shakeDur;
        public Vector3 ShakeOffset { get; private set; }

        public void Init()
        {
            _soft = Make("SoftFx", Mats.Fx(Color.white, 0, true), 1.4f, true);
            _shards = Make("Shards", Mats.Fx(Color.white, 1, true), 2.2f, true);
            _confetti = Make("Confetti", Mats.Fx(Color.white, 1, true), 0.55f, false);
            _glow = Make("Glow", Mats.Fx(Color.white, 0, true, true), 0f, true);

            var rot = _confetti.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            var noise = _confetti.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.8f;
        }

        ParticleSystem Make(string name, Material mat, float gravity, bool shrink)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;

            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            if (shrink)
            {
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.sortingFudge = -10;

            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color c, float rotation = 0f)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = c,
                rotation = rotation
            };
            ps.Emit(p, 1);
        }

        public void Splash(Vector3 pos, int count, float power, float spread = 0.3f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 r = Random.insideUnitCircle;
                var off = new Vector3(r.x, 0, r.y) * spread;
                var vel = new Vector3(r.x * 1.2f, Random.Range(1.5f, 3.2f) * power, r.y * 1.2f);
                var c = Color.Lerp(Color.white, new Color(0.7f, 0.95f, 1f), Random.value);
                Emit(_soft, pos + off, vel, Random.Range(0.08f, 0.2f), Random.Range(0.5f, 0.9f), c);
            }
        }

        public void Shards(Vector3 pos, int count, Color tint, float power = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) + 0.4f;
                var c = Color.Lerp(Color.white, tint, Random.value * 0.6f);
                Emit(_shards, pos, dir * Random.Range(1.5f, 3.5f) * power, Random.Range(0.05f, 0.13f),
                     Random.Range(0.5f, 0.9f), c, Random.Range(0f, 360f));
            }
        }

        public void Confetti(Vector3 pos, int count)
        {
            Color[] palette =
            {
                Mats.Hex("#FF4B6E"), Mats.Hex("#FFC93C"), Mats.Hex("#38C6FF"),
                Mats.Hex("#5EE07A"), Mats.Hex("#B57BFF"), Color.white
            };
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.insideUnitSphere;
                dir.y = Random.Range(0.8f, 1.6f);
                Emit(_confetti, pos, dir * Random.Range(3f, 6.5f), Random.Range(0.1f, 0.18f),
                     Random.Range(1.8f, 2.8f), palette[Random.Range(0, palette.Length)], Random.Range(0f, 360f));
            }
        }

        public void Glow(Vector3 pos, Color c, int count, float spread = 0.2f, float size = 0.25f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 r = Random.insideUnitSphere * spread;
                Emit(_glow, pos + r, Vector3.up * Random.Range(0.2f, 0.8f) + r, Random.Range(size * 0.5f, size),
                     Random.Range(0.4f, 0.8f), c);
            }
        }

        public void Wake(Vector3 pos, Vector3 back)
        {
            Vector2 r = Random.insideUnitCircle * 0.06f;
            Emit(_soft, pos + new Vector3(r.x, 0.02f, r.y), back * 0.4f + Vector3.up * 0.15f,
                 Random.Range(0.08f, 0.15f), 0.6f, new Color(1f, 1f, 1f, 0.85f));
        }

        public void Shake(float amplitude, float duration)
        {
            if (amplitude < _shakeAmp * (_shakeTime / Mathf.Max(0.0001f, _shakeDur))) return;
            _shakeAmp = amplitude;
            _shakeDur = duration;
            _shakeTime = duration;
        }

        void LateUpdate()
        {
            if (_shakeTime > 0f)
            {
                _shakeTime -= Time.deltaTime;
                float k = Mathf.Clamp01(_shakeTime / _shakeDur);
                float t = Time.time * 38f;
                ShakeOffset = new Vector3(Mathf.PerlinNoise(t, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, t) - 0.5f, 0f)
                              * (2f * _shakeAmp * k * k);
            }
            else ShakeOffset = Vector3.zero;
        }
    }
}
