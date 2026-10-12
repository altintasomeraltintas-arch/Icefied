using System;
using System.Collections;
using UnityEngine;

namespace Icefied.View
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f || t >= 1f) return t;
            const float c4 = 2f * Mathf.PI / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }
    }

    public static class Tween
    {
        /// <summary>step(e) her karede 0..1 arası eased değerle çağrılır.</summary>
        public static IEnumerator Run(float duration, Func<float, float> ease, Action<float> step, float delay = 0f)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                step(ease(Mathf.Clamp01(t / duration)));
                yield return null;
            }
            step(ease(1f));
        }

        public static Coroutine Scale(Transform tr, Vector3 from, Vector3 to, float dur, Func<float, float> ease, float delay = 0f)
        {
            return GameRoot.I.StartCoroutine(Run(dur, ease, e =>
            {
                if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, e);
            }, delay));
        }

        public static Coroutine Do(float dur, Func<float, float> ease, Action<float> step, float delay = 0f)
        {
            return GameRoot.I.StartCoroutine(Run(dur, ease, step, delay));
        }

        public static Coroutine After(float delay, Action action)
        {
            return GameRoot.I.StartCoroutine(AfterRoutine(delay, action));
        }

        static IEnumerator AfterRoutine(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action();
        }
    }
}
