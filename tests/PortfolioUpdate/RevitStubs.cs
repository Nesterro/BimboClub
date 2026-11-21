namespace Autodesk.Revit.UI.Events
{
    public sealed class IdlingEventArgs : EventArgs { }
}

namespace Autodesk.Revit.UI
{
    using Events;
    public sealed class RibbonPanel
    {
        public string Title { get; set; }
        public bool Visible { get; set; } = true;
    }

    public sealed class UIControlledApplication
    {
        private EventHandler<IdlingEventArgs> _idling;
        private bool _raisingIdling;
        public event EventHandler<IdlingEventArgs> Idling
        {
            add
            {
                if (_raisingIdling) throw new InvalidOperationException("Cannot subscribe during Idling.");
                _idling += value;
            }
            remove
            {
                if (_raisingIdling) throw new InvalidOperationException("Cannot unsubscribe during Idling.");
                _idling -= value;
            }
        }
        public Action<string> OnLoad { get; set; }
        public RibbonPanel CreateRibbonPanel(string tab, string name) => new RibbonPanel();
        public void LoadAddIn(string path) => OnLoad(path);
        public void RaiseIdling()
        {
            _raisingIdling = true;
            try { _idling?.Invoke(this, new IdlingEventArgs()); }
            finally { _raisingIdling = false; }
        }
    }
}

namespace BimboClub
{
    internal static class Logger
    {
        internal static void LogError(string message, Exception error) => Console.WriteLine($"{message}: {error.Message}");
    }
}
