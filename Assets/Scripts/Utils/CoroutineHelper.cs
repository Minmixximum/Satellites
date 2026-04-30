using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SatelliteEdgeComputing.Utils
{
    /// <summary>
    /// 协程助手类
    /// </summary>
    public static class CoroutineHelper
    {
        private static MonoBehaviour coroutineRunner;

        private static MonoBehaviour CoroutineRunner
        {
            get
            {
                if (coroutineRunner == null)
                {
                    GameObject runnerObj = new GameObject("CoroutineHelper");
                    coroutineRunner = runnerObj.AddComponent<CoroutineRunnerBehaviour>();
                    UnityEngine.Object.DontDestroyOnLoad(runnerObj);
                }
                return coroutineRunner;
            }
        }

        /// <summary>
        /// 启动协程
        /// </summary>
        public static Coroutine Start(IEnumerator routine)
        {
            return CoroutineRunner.StartCoroutine(routine);
        }

        /// <summary>
        /// 停止协程
        /// </summary>
        public static void Stop(Coroutine routine)
        {
            if (routine != null && coroutineRunner != null)
            {
                CoroutineRunner.StopCoroutine(routine);
            }
        }

        /// <summary>
        /// 停止所有协程
        /// </summary>
        public static void StopAll()
        {
            if (coroutineRunner != null)
            {
                CoroutineRunner.StopAllCoroutines();
            }
        }

        /// <summary>
        /// 延迟执行动作
        /// </summary>
        public static Coroutine Delay(float delay, Action action)
        {
            return Start(DelayRoutine(delay, action));
        }

        private static IEnumerator DelayRoutine(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);
            action?.Invoke();
        }

        /// <summary>
        /// 延迟一帧执行动作
        /// </summary>
        public static Coroutine NextFrame(Action action)
        {
            return Start(NextFrameRoutine(action));
        }

        private static IEnumerator NextFrameRoutine(Action action)
        {
            yield return null;
            action?.Invoke();
        }

        /// <summary>
        /// 等待条件满足
        /// </summary>
        public static Coroutine WaitUntil(Func<bool> condition, Action action)
        {
            return Start(WaitUntilRoutine(condition, action));
        }

        private static IEnumerator WaitUntilRoutine(Func<bool> condition, Action action)
        {
            yield return new WaitUntil(condition);
            action?.Invoke();
        }

        /// <summary>
        /// 等待条件满足或超时
        /// </summary>
        public static Coroutine WaitUntilOrTimeout(Func<bool> condition, float timeout, Action action, Action timeoutAction = null)
        {
            return Start(WaitUntilOrTimeoutRoutine(condition, timeout, action, timeoutAction));
        }

        private static IEnumerator WaitUntilOrTimeoutRoutine(Func<bool> condition, float timeout, Action action, Action timeoutAction)
        {
            float startTime = Time.time;
            while (Time.time - startTime < timeout)
            {
                if (condition())
                {
                    action?.Invoke();
                    yield break;
                }
                yield return null;
            }
            timeoutAction?.Invoke();
        }

        /// <summary>
        /// 顺序执行多个动作（每个动作延迟指定时间）
        /// </summary>
        public static Coroutine Sequence(float interval, params Action[] actions)
        {
            return Start(SequenceRoutine(interval, actions));
        }

        private static IEnumerator SequenceRoutine(float interval, Action[] actions)
        {
            foreach (var action in actions)
            {
                action?.Invoke();
                yield return new WaitForSeconds(interval);
            }
        }

        /// <summary>
        /// 顺序执行多个协程
        /// </summary>
        public static Coroutine SequenceCoroutines(params IEnumerator[] routines)
        {
            return Start(SequenceCoroutinesRoutine(routines));
        }

        private static IEnumerator SequenceCoroutinesRoutine(IEnumerator[] routines)
        {
            foreach (var routine in routines)
            {
                if (routine != null)
                {
                    yield return routine;
                }
            }
        }

        /// <summary>
        /// 并行执行多个协程，等待所有完成
        /// </summary>
        public static Coroutine Parallel(params IEnumerator[] routines)
        {
            return Start(ParallelRoutine(routines));
        }

        private static IEnumerator ParallelRoutine(IEnumerator[] routines)
        {
            List<Coroutine> runningCoroutines = new List<Coroutine>();

            foreach (var routine in routines)
            {
                if (routine != null)
                {
                    runningCoroutines.Add(CoroutineRunner.StartCoroutine(routine));
                }
            }

            // 等待所有协程完成
            foreach (var coroutine in runningCoroutines)
            {
                yield return coroutine;
            }
        }

        /// <summary>
        /// 执行重复动作
        /// </summary>
        public static Coroutine Repeat(float interval, Action action, int repeatCount = -1)
        {
            return Start(RepeatRoutine(interval, action, repeatCount));
        }

        private static IEnumerator RepeatRoutine(float interval, Action action, int repeatCount)
        {
            int count = 0;
            while (repeatCount < 0 || count < repeatCount)
            {
                action?.Invoke();
                yield return new WaitForSeconds(interval);
                count++;
            }
        }

        /// <summary>
        /// 执行Lerp动画
        /// </summary>
        public static Coroutine Lerp(float duration, Action<float> lerpAction, Action completed = null, Func<float, float> easeFunction = null)
        {
            return Start(LerpRoutine(duration, lerpAction, completed, easeFunction));
        }

        private static IEnumerator LerpRoutine(float duration, Action<float> lerpAction, Action completed, Func<float, float> easeFunction)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                if (easeFunction != null)
                {
                    t = easeFunction(t);
                }

                lerpAction?.Invoke(t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            lerpAction?.Invoke(1f);
            completed?.Invoke();
        }

        /// <summary>
        /// 执行振动效果
        /// </summary>
        public static Coroutine Shake(float duration, float intensity, Transform target, Action completed = null)
        {
            return Start(ShakeRoutine(duration, intensity, target, completed));
        }

        private static IEnumerator ShakeRoutine(float duration, float intensity, Transform target, Action completed)
        {
            Vector3 originalPosition = target.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float decay = 1f - (elapsed / duration);
                Vector3 offset = UnityEngine.Random.insideUnitSphere * intensity * decay;
                target.localPosition = originalPosition + offset;

                elapsed += Time.deltaTime;
                yield return null;
            }

            target.localPosition = originalPosition;
            completed?.Invoke();
        }

        /// <summary>
        /// 执行脉冲效果
        /// </summary>
        public static Coroutine Pulse(float duration, float scale, Transform target, Action completed = null)
        {
            return Start(PulseRoutine(duration, scale, target, completed));
        }

        private static IEnumerator PulseRoutine(float duration, float scale, Transform target, Action completed)
        {
            Vector3 originalScale = target.localScale;
            float halfDuration = duration / 2f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                float t = elapsed / halfDuration;
                target.localScale = originalScale * (1f + (scale - 1f) * t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            while (elapsed < duration)
            {
                float t = (elapsed - halfDuration) / halfDuration;
                target.localScale = originalScale * (scale - (scale - 1f) * t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            target.localScale = originalScale;
            completed?.Invoke();
        }

        /// <summary>
        /// 缓动函数库
        /// </summary>
        public static class Ease
        {
            public static float Linear(float t) => t;
            public static float EaseInQuad(float t) => t * t;
            public static float EaseOutQuad(float t) => t * (2 - t);
            public static float EaseInOutQuad(float t) => t < 0.5f ? 2 * t * t : -1 + (4 - 2 * t) * t;
            public static float EaseInCubic(float t) => t * t * t;
            public static float EaseOutCubic(float t) => (--t) * t * t + 1;
            public static float EaseInOutCubic(float t) => t < 0.5f ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;
            public static float EaseInSine(float t) => 1 - Mathf.Cos(t * Mathf.PI / 2);
            public static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.PI / 2);
            public static float EaseInOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1) / 2;
        }

        /// <summary>
        /// 内部协程运行器类
        /// </summary>
        private class CoroutineRunnerBehaviour : MonoBehaviour { }
    }
}