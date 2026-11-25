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
        [SerializeField] private float infoAutoDismissSeconds = 4.0f;
        [SerializeField] private float warnAutoDismissSeconds = 7.0f;
        [SerializeField] private float thinkingAutoDismissSeconds = 8.5f;
        [SerializeField] private int longMessageCharThreshold = 180;
        [SerializeField] private float longMessageExtraSeconds = 2.5f;
        [SerializeField] private int veryLongMessageManualDismissThreshold = 320;
        [Tooltip("Very long Info/Warn/Thinking messages close after this many extra seconds instead of waiting for Esc forever.")]
        [SerializeField] private float veryLongMessageExtraSeconds = 12.0f;

        [Header("Staleness")]
        [Tooltip("Queued Info/Thinking messages older than this are dropped instead of shown late.")]
        [SerializeField] private float maxQueuedAgeSeconds = 45.0f;
        [Tooltip("Queued Warn messages older than this are dropped instead of shown late.")]
        [SerializeField] private float maxQueuedWarnAgeSeconds = 75.0f;

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
            message.PublishedAt = now;

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

        /// <summary>Frame on which a key press (Esc / `) last dismissed a message.</summary>
        public static int LastKeyDismissFrame { get; private set; } = -1;

        /// <summary>
        /// Dismiss the current message in response to a key press. Several components listen for
        /// the dismiss key, so only the first call per frame acts; the rest are ignored. Returns
        /// true if a message was dismissed this frame, so other Esc handlers (the game menu) can
        /// leave the key press alone.
        /// </summary>
        public bool DismissCurrentFromKeyPress()
        {
            if (LastKeyDismissFrame == Time.frameCount)
            {
                return true;
            }

            // While the game menu is open, Esc closes the menu and leaves messages alone.
            if (GameMenuController.IsMenuOpen)
            {
                return false;
            }

            if (!current.HasValue)
            {
                return false;
            }

            // Response choices must be answered (1-4), never skipped with Esc.
            var key = current.Value.TriggerKey;
            if (!string.IsNullOrWhiteSpace(key) && key.StartsWith("ufo/response_choice/", StringComparison.Ordinal))
            {
                return false;
            }

            LastKeyDismissFrame = Time.frameCount;
            DismissCurrent();
            return true;
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

            var preferIncoming = incomingPriority > currentPriority
                || (incomingPriority == currentPriority
                    && IsResponseChoiceMessage(incoming)
                    && !IsResponseChoiceMessage(current.Value));

            if (preferIncoming)
            {
                // Preempt: push current back into queue.
                queue.Add(current.Value);
                current = null;
                SetCurrent(DequeueBestOrNull());
            }
        }

        /// <summary>
        /// Drop queued messages that waited too long: by the time they'd show, the contact or numbers
        /// they describe are out of date. Critical messages and response choices are never dropped.
        /// </summary>
        private void DropStaleQueued(float now)
        {
            for (var i = queue.Count - 1; i >= 0; i--)
            {
                var m = queue[i];
                if (IsResponseChoiceMessage(m) || (m.Channel == BoxChannel.Info && m.Severity == BoxSeverity.Critical))
                {
                    continue;
                }

                var maxAge = m.Channel == BoxChannel.Info && m.Severity == BoxSeverity.Warn
                    ? maxQueuedWarnAgeSeconds
                    : maxQueuedAgeSeconds;

                if (maxAge > 0f && now - m.PublishedAt > maxAge)
                {
                    queue.RemoveAt(i);
                }
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
            DropStaleQueued(Time.unscaledTime);

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
                else if (p == bestPriority
                    && IsResponseChoiceMessage(queue[i])
                    && !IsResponseChoiceMessage(queue[bestIndex]))
                {
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

            var contentLength = GetMessageContentLength(message);
            float? baseSeconds;

            if (message.Channel != BoxChannel.Info)
            {
                baseSeconds = message.Channel == BoxChannel.Thinking
                    ? thinkingAutoDismissSeconds
                    : null;
            }
            else
            {
                baseSeconds = message.Severity switch
                {
                    BoxSeverity.Info => infoAutoDismissSeconds,
                    BoxSeverity.Warn => warnAutoDismissSeconds,
                    _ => null,
                };
            }

            if (!baseSeconds.HasValue)
            {
                return null;
            }

            if (contentLength >= veryLongMessageManualDismissThreshold)
            {
                return baseSeconds.Value + Mathf.Max(0f, longMessageExtraSeconds) + Mathf.Max(0f, veryLongMessageExtraSeconds);
            }

            if (contentLength >= longMessageCharThreshold)
            {
                return baseSeconds.Value + Mathf.Max(0f, longMessageExtraSeconds);
            }

            return baseSeconds;
        }

        private static int GetMessageContentLength(BoxMessage message)
        {
            var titleLength = string.IsNullOrEmpty(message.Title) ? 0 : message.Title.Length;
            var bodyLength = string.IsNullOrEmpty(message.Body) ? 0 : message.Body.Length;
            return titleLength + bodyLength;
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

        private static bool IsResponseChoiceMessage(BoxMessage message)
        {
            return !string.IsNullOrWhiteSpace(message.TriggerKey)
                && message.TriggerKey.StartsWith("ufo/response_choice/", StringComparison.Ordinal);
        }
    }
}
