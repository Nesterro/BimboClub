// UI data-only contract, avoiding the WPF window in the API compile probe.
namespace BimboClub
{
    public class ViewParameterConfig
    {
        public bool IsEnabled { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
