using System;
using System.Collections.Generic;
using UnityEngine;

namespace XCon.UI.Boxes
{
    public sealed class BoxMessageQueue : MonoBehaviour
    {
        public static BoxMessageQueue Instance { get; private set; }

        public event Action<BoxMessage?> CurrentMessageChanged;
        public event Action StateChanged;

        [Header("Behavior")]
        [SerializeField] private float duplicateSuppressSeconds = 3.0f;
        [SerializeField] private int maxDuplicateKeys = 512;

        [Header("Auto Dismiss")]
        [SerializeField] private float infoAutoDismissSeconds = 3.0f;
        [SerializeField] private float warnAutoDismissSeconds = 6.0f;

        private readonly List<BoxMessage> queue = new();
        private readonly Dictionary<string, float> lastPublishedAtByKey = new();

        private BoxMessage? current;

        private Coroutine autoDismissRoutine;
        private int autoDismissToken;

        public BoxMessage? Current => current;

        public void CopyQueuedMessages(List<BoxMessage> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            buffer.AddRange(queue);
        }

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

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
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

            PruneDuplicateKeys(now);

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
                }
            }

            if (current.HasValue && current.Value.TriggerKey == key)
            {
                SetCurrent(message);
                return;
            }

            queue.Add(message);

            EvaluatePreemption(message);

            // If this didn't change the current message, the UI still needs to know the queue changed.
            StateChanged?.Invoke();
        }

        public void DismissCurrent()
        {
            if (!current.HasValue)
            {
                return;
            }

            current = null;
            SetCurrent(DequeueBestOrNull());
        }

        public bool DismissIfCurrent(string triggerKey)
        {
            if (string.IsNullOrWhiteSpace(triggerKey))
            {
                return false;
            }

            if (!current.HasValue)
            {
                return false;
            }

            if (current.Value.TriggerKey != triggerKey)
            {
                return false;
            }

            DismissCurrent();
            return true;
        }

        public int RemoveQueued(string triggerKey)
        {
            if (string.IsNullOrWhiteSpace(triggerKey) || queue.Count == 0)
            {
                return 0;
            }

            var removed = 0;
            for (var i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i].TriggerKey == triggerKey)
                {
                    queue.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                StateChanged?.Invoke();
            }

            return removed;
        }

        public int UpdateBody(string triggerKey, string newBody)
        {
            if (string.IsNullOrWhiteSpace(triggerKey) || string.IsNullOrWhiteSpace(newBody))
            {
                return 0;
            }

            var updated = 0;
            var updatedQueued = false;

            if (current.HasValue && current.Value.TriggerKey == triggerKey)
            {
                var m = current.Value;
                if (m.Body != newBody)
                {
                    m.Body = newBody;
                    SetCurrent(m);
                    updated++;
                }
            }

            if (queue.Count > 0)
            {
                for (var i = 0; i < queue.Count; i++)
                {
                    if (queue[i].TriggerKey != triggerKey)
                    {
                        continue;
                    }

                    if (queue[i].Body == newBody)
                    {
                        continue;
                    }

                    var m = queue[i];
                    m.Body = newBody;
                    queue[i] = m;
                    updated++;
                    updatedQueued = true;
                }
            }

            if (updatedQueued)
            {
                StateChanged?.Invoke();
            }

            return updated;
        }

        private void EvaluatePreemption(BoxMessage incoming)
        {
            if (!current.HasValue)
            {
                EnsureCurrent();
                return;
            }

            var incomingPriority = GetPriority(incoming);
            var currentPriority = GetPriority(current.Value);

            if (incomingPriority > currentPriority)
            {
                // Preempt: push current back into queue.
                queue.Add(current.Value);
                current = null;
                SetCurrent(DequeueBestOrNull());
            }
        }

        private void EnsureCurrent()
        {
            if (current.HasValue)
            {
                return;
            }

            SetCurrent(DequeueBestOrNull());
        }

        private BoxMessage? DequeueBestOrNull()
        {
            if (queue.Count == 0)
            {
                return null;
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
            return next;
        }

        private void SetCurrent(BoxMessage? message)
        {
            if (current.HasValue && message.HasValue && current.Value.Equals(message.Value))
            {
                return;
            }

            if (!current.HasValue && !message.HasValue)
            {
                return;
            }

            current = message;
            CurrentMessageChanged?.Invoke(current);
            StateChanged?.Invoke();

            RestartAutoDismissIfNeeded(message);
        }

        private void RestartAutoDismissIfNeeded(BoxMessage? message)
        {
            autoDismissToken++;

            if (autoDismissRoutine != null)
            {
                StopCoroutine(autoDismissRoutine);
                autoDismissRoutine = null;
            }

            if (!message.HasValue)
            {
                return;
            }

            var m = message.Value;
            var seconds = GetAutoDismissSecondsOrNull(m);
            if (!seconds.HasValue)
            {
                return;
            }

            if (seconds.Value <= 0.0f)
            {
                return;
            }

            var token = autoDismissToken;
            var triggerKey = m.TriggerKey;
            autoDismissRoutine = StartCoroutine(AutoDismissAfterDelay(seconds.Value, token, triggerKey));
        }

        private System.Collections.IEnumerator AutoDismissAfterDelay(float seconds, int token, string triggerKey)
        {
            yield return new WaitForSecondsRealtime(seconds);

            if (token != autoDismissToken)
            {
                yield break;
            }

            if (!current.HasValue)
            {
                yield break;
            }

            if (current.Value.TriggerKey != triggerKey)
            {
                yield break;
            }

            DismissCurrent();
        }

        private float? GetAutoDismissSecondsOrNull(BoxMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.TriggerKey)
                && message.TriggerKey.StartsWith("ufo/response_choice/", StringComparison.Ordinal))
            {
                return null;
            }

            if (message.Channel != BoxChannel.Info)
            {
                return null;
            }

            return message.Severity switch
            {
                BoxSeverity.Info => infoAutoDismissSeconds,
                BoxSeverity.Warn => warnAutoDismissSeconds,
                _ => null,
            };
        }

        private void PruneDuplicateKeys(float now)
        {
            if (lastPublishedAtByKey.Count == 0)
            {
                return;
            }

            List<string> keysToRemove = null;
            foreach (var kv in lastPublishedAtByKey)
            {
                if (now - kv.Value >= duplicateSuppressSeconds)
                {
                    keysToRemove ??= new List<string>();
                    keysToRemove.Add(kv.Key);
                }
            }

            if (keysToRemove != null)
            {
                for (var i = 0; i < keysToRemove.Count; i++)
                {
                    lastPublishedAtByKey.Remove(keysToRemove[i]);
                }
            }

            if (maxDuplicateKeys > 0 && lastPublishedAtByKey.Count > maxDuplicateKeys)
            {
                // Still too big: drop oldest entries until within limit.
                var entries = new List<KeyValuePair<string, float>>(lastPublishedAtByKey);
                entries.Sort((a, b) => a.Value.CompareTo(b.Value));

                var toDrop = lastPublishedAtByKey.Count - maxDuplicateKeys;
                for (var i = 0; i < toDrop && i < entries.Count; i++)
                {
                    lastPublishedAtByKey.Remove(entries[i].Key);
                }
            }
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
