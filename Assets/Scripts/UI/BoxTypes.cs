using System;

namespace XCon.UI.Boxes
{
    public enum BoxChannel
    {
        Info = 0,
        Thinking = 1,
    }

    public enum BoxSeverity
    {
        Info = 0,
        Warn = 1,
        Critical = 2,
    }

    [Serializable]
    public struct BoxMessage
    {
        public string TriggerKey;
        public BoxChannel Channel;
        public BoxSeverity Severity;
        public string SourceTag;
        public string Title;
        public string Body;

        public BoxMessage(
            string triggerKey,
            BoxChannel channel,
            BoxSeverity severity,
            string sourceTag,
            string title,
            string body)
        {
            TriggerKey = triggerKey;
            Channel = channel;
            Severity = severity;
            SourceTag = sourceTag;
            Title = title;
            Body = body;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(Body);
    }
}
