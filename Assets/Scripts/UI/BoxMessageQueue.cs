using System;
using System.Collections.Generic;
using UnityEngine;

namespace XCon.UI.Boxes
{
    public sealed class BoxMessageQueue : MonoBehaviour
    {
        public static BoxMessageQueue Instance { get; private set; }

        public event Action<BoxMessage?> CurrentMessageChanged;

        [Header("Behavior")]
        [SerializeField] private float duplicateSuppressSeconds = 3.0f;

        private readonly List<BoxMessage> queue = new();
        private readonly Dictionary<string, float> lastPublishedAtByKey = new();

        private BoxMessage? current;

        public BoxMessage? Current => current;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Publish(BoxMessage message)
        {
            if (!message.IsValid)
            {
                return;
            }

            var key = string.IsNullOrWhiteSpace(message.TriggerKey)
                ? $"{message.Channel}:{message.Severity}:{message.SourceTag}:{message.Title}"
                : message.TriggerKey;

            var now = Time.unscaledTime;

            if (lastPublishedAtByKey.TryGetValue(key, out var lastTime) && now - lastTime < duplicateSuppressSeconds)
            {
                return;
            }

            lastPublishedAtByKey[key] = now;

            message.TriggerKey = key;

            // Coalesce repeated triggers: keep only the latest per key.
            for (var i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i].TriggerKey == key)
                {
                    queue.RemoveAt(i);
                    break;
                }
            }

            if (current.HasValue && current.Value.TriggerKey == key)
            {
                current = message;
                CurrentMessageChanged?.Invoke(current);
                return;
            }

            queue.Add(message);

            EvaluatePreemption(message);
        }

        public void DismissCurrent()
        {
            if (!current.HasValue)
            {
                return;
            }

            current = null;
            CurrentMessageChanged?.Invoke(null);
            ShowNextIfAny();
        }

        private void EvaluatePreemption(BoxMessage incoming)
        {
            if (!current.HasValue)
            {
                ShowNextIfAny();
                return;
            }

            var incomingPriority = GetPriority(incoming);
            var currentPriority = GetPriority(current.Value);

            if (incomingPriority > currentPriority)
            {
                // Preempt: push current back into queue.
                queue.Add(current.Value);
                current = null;
                CurrentMessageChanged?.Invoke(null);
                ShowNextIfAny();
            }
        }

        private void ShowNextIfAny()
        {
            if (current.HasValue)
            {
                return;
            }

            if (queue.Count == 0)
            {
                return;
            }

            var bestIndex = 0;
            var bestPriority = int.MinValue;

            for (var i = 0; i < queue.Count; i++)
            {
                var p = GetPriority(queue[i]);
                if (p > bestPriority)
                {
                    bestPriority = p;
                    bestIndex = i;
                }
            }

            var next = queue[bestIndex];
            queue.RemoveAt(bestIndex);
            current = next;
            CurrentMessageChanged?.Invoke(current);
        }

        private static int GetPriority(BoxMessage message)
        {
            // Matches the spec in the GDD:
            // Info-Critical > Info-Warn > Thinking > Info-Info
            if (message.Channel == BoxChannel.Info)
            {
                return message.Severity switch
                {
                    BoxSeverity.Critical => 400,
                    BoxSeverity.Warn => 300,
                    _ => 100,
                };
            }

            return 200;
        }
    }
}
