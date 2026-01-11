#nullable enable
using CraftSharp.Control;
using CraftSharp.UI;

namespace CraftSharp.Event
{
    public record InteractionAddEvent : BaseEvent
    {
        public int InteractionId { get; }
        public bool AddAndSelect { get; }
        public bool UseProgress { get; }
        public bool ShowWarning { get; }
        public InteractionInfo Info { get; }

        public InteractionAddEvent(int id, bool addAndSelect, bool useProgress, bool showWarning, InteractionInfo info)
        {
            InteractionId = id;
            AddAndSelect = addAndSelect;
            UseProgress = useProgress;
            ShowWarning = showWarning;
            Info = info;
        }
    }
}