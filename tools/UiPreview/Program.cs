using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using BimboClub;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        string repo = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        var app = new Application();
        VerifyControls();
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var events = new HashSet<string> { "Click", "Loaded", "Unloaded", "Closing", "Closed",
            "SelectionChanged", "TextChanged", "Checked", "Unchecked", "KeyDown", "PreviewKeyDown",
            "MouseLeftButtonDown", "MouseDoubleClick", "PreviewMouseLeftButtonDown", "SizeChanged",
            "SelectedItemChanged", "GotFocus", "LostFocus", "CellEditEnding", "PreviewTextInput" };
        int rendered = 0;
        int failed = 0;
        foreach (string path in Directory.GetFiles(repo, "*.xaml", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                        !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)))
        {
            var document = XDocument.Load(path);
            if (document.Root.Name.LocalName != "Window") continue;
            try
            {
                app.Resources.MergedDictionaries.Clear();
                string project = Path.GetRelativePath(repo, path).Split(Path.DirectorySeparatorChar)[0];
                string styles = Path.Combine(repo, project, "Resources", "Styles.xaml");
                if (File.Exists(styles))
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(styles) });
                if (project == "RevitServerManagerInstaller")
                {
                    var resources = XDocument.Load(Path.Combine(repo, project, "App.xaml")).Root.Element(presentation + "Application.Resources");
                    app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(
                        new XElement(presentation + "ResourceDictionary", new XAttribute(XNamespace.Xmlns + "x", x), resources.Elements()).ToString()));
                }
                document.Root.Attribute(x + "Class")?.Remove();
                int templateKey = 0;
                foreach (var element in document.Descendants().ToList())
                {
                    if (element.Name.LocalName.EndsWith(".DataContext") || element.Name.LocalName == "EventSetter")
                    { element.Remove(); continue; }
                    foreach (var attr in element.Attributes().ToList())
                        if (events.Contains(attr.Name.LocalName) || attr.Name.LocalName == "Icon" ||
                            (attr.Name.LocalName == "DataType" && (attr.Value.Contains("local:") || attr.Value.Contains("models:"))))
                        {
                            if (attr.Name.LocalName == "DataType" && element.Parent?.Name.LocalName.EndsWith("Resources") == true)
                                element.SetAttributeValue(x + "Key", "PreviewTemplate" + templateKey++);
                            attr.Remove();
                        }
                    if (element.Name.LocalName == "Image" && element.Attribute("Source") is XAttribute src && !src.Value.StartsWith("{"))
                        src.Value = Path.Combine(Path.GetDirectoryName(path), src.Value);
                }
                var context = new ParserContext { BaseUri = new Uri(path) };
                var window = (Window)XamlReader.Parse(document.ToString(), context);
                if (project == "BimboClubManager") window.DataContext = new ManagerPreview();
                if (project == "SystemTravers") UiThemeHelper.ApplyNeutralTheme(window);
                // Render source markup with no event handlers and no live Revit document.
                var content = (FrameworkElement)window.Content;
                if (content is Panel panel && panel.Background == null) panel.Background = window.Background;
                double width = double.IsNaN(window.Width) ? 1000 : window.Width;
                double height = double.IsNaN(window.Height) ? 700 : window.Height;
                content.Measure(new Size(width, height));
                content.Arrange(new Rect(0, 0, width, height));
                content.UpdateLayout();
                Save(content, (int)width, (int)height, Path.Combine(output, project + "-" + Path.GetFileNameWithoutExtension(path) + ".png"));
                rendered++;
                window.Close();
            }
            catch (Exception ex) { failed++; Console.WriteLine("FAIL " + Path.GetRelativePath(repo, path) + ": " + ex.GetBaseException().Message); }
        }

        var gallery = new WrapPanel { Width = 660, Background = Brushes.White };
        foreach (string key in new[] {"3d","tags","wall","floor","copy","print","router","filters","rename","network","json","excel","riser","connect","crop","param","specs","sizer","heat","rules","fill_params"})
        {
            var cell = new StackPanel { Width = 110, Height = 95, Margin = new Thickness(0, 6, 0, 6) };
            cell.Children.Add(new Image { Source = RibbonIcons.FromFileName("icon_" + key + ".png"), Width = 32, Height = 32 });
            cell.Children.Add(new TextBlock { Text = key, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 6) });
            cell.Children.Add(new Image { Source = RibbonIcons.FromFileName("icon_" + key + "_16.png"), Width = 16, Height = 16 });
            gallery.Children.Add(cell);
        }
        gallery.Measure(new Size(660, 450));
        gallery.Arrange(new Rect(0, 0, 660, 450));
        Save(gallery, 660, 450, Path.Combine(output, "ribbon-icons.png"));
        Console.WriteLine($"Rendered {rendered} windows and 21 icon pairs.");
        if (failed > 0) throw new InvalidOperationException($"{failed} interface previews failed.");
    }

    private static void VerifyControls()
    {
        var window = new Window();
        UiThemeHelper.ApplyNeutralTheme(window);
        var combo = new ComboBox { IsEditable = true, Text = "Текст фильтра", Width = 260 };
        window.Content = combo;
        combo.Style = (Style)window.Resources[typeof(ComboBox)];
        combo.ApplyTemplate();
        var editor = combo.Template.FindName("PART_EditableTextBox", combo) as TextBox;
        if (editor == null || editor.Visibility != Visibility.Visible || editor.Text != combo.Text)
            throw new InvalidOperationException("Editable ComboBox must expose its bound text editor.");
        if (combo.MaxDropDownHeight != 320) throw new InvalidOperationException("Dropdown height must be bounded.");
        var icon = RibbonIcons.FromFileName("icon_heat_16.png");
        if (!icon.IsFrozen || icon.PixelWidth != 16 || !ReferenceEquals(icon, RibbonIcons.FromFileName("icon_heat_16.png")))
            throw new InvalidOperationException("Ribbon icons must be frozen, cached, and match the requested size.");
        window.Close();
        Console.WriteLine("PASS: editable controls, bounded dropdowns, and icon cache.");
    }

    private sealed class ManagerPreview
    {
        public bool IsLoading => false;
        public bool ShowRevitWarning => false;
        public bool IsPluginsTabActive => true;
        public bool IsSettingsTabActive => false;
        public bool IsAboutTabActive => false;
        public string CurrentTab => "plugins";
        public string ClientVersion => "2.4.23";
        public bool HasUpdatesAvailable => true;
        public string LatestReleaseVersion => "2.4.23";
        public string LatestReleaseName => "BimboClub Tools 2.4.23";
        public string LatestReleaseBody => "Проверка версии пакета перед установкой.\nНейтральные интерфейсы и единые иконки.";
        public string UpdateAllButtonText => "Обновить все";
        public object[] RevitVersions => new[] { "2024", "2025", "2026" }.Select(year => (object)new {
            Year = year, DisplayName = "Autodesk Revit " + year,
            IsRevitInstalled = true, IsPluginInstalled = true, IsUpdateAvailable = year == "2024",
            IsAvailableVersionUpToDate = year != "2024",
            InstalledVersionDisplay = year == "2024" ? "v2.4.20" : "v2.4.23",
            AvailableVersionDisplay = year == "2024" ? "v2.4.23" : "Актуально",
            StatusDescription = year == "2024" ? "Доступно обновление" : "Установлена актуальная версия"
        }).ToArray();
    }

    private static void Save(FrameworkElement element, int width, int height, string path)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
