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
        public event EventHandler<IdlingEventArgs> Idling;
        public Action<string> OnLoad { get; set; }
        public RibbonPanel CreateRibbonPanel(string tab, string name) => new RibbonPanel();
        public void LoadAddIn(string path) => OnLoad(path);
        public void RaiseIdling() => Idling?.Invoke(this, new IdlingEventArgs());
    }
}

namespace BimboClub
{
    internal static class Logger
    {
        internal static void LogError(string message, Exception error) => Console.WriteLine($"{message}: {error.Message}");
    }
}
