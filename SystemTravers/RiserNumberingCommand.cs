using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace BimboClub
{
	[Transaction(TransactionMode.Manual)]
	public class RiserNumberingCommand : IExternalCommand
	{

		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			UIApplication application = commandData.Application;
			Document document = application.ActiveUIDocument.Document;
			View activeView = document.ActiveView;
			if (activeView == null)
			{
				TaskDialog.Show("Ошибка", "Не найден активный вид.");
				return Result.Failed;
			}
			Result result;
			try
			{
				Logger.Log("Запуск нумерации стояков.", "INFO");
				List<ValueTuple<double, double>> list = this.CollectFloorZRanges(document, null);
				foreach (RevitLinkInstance current in new FilteredElementCollector(document).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>().ToList<RevitLinkInstance>())
				{
					Document linkDocument = current.GetLinkDocument();
					if (linkDocument != null)
					{
						Autodesk.Revit.DB.Transform totalTransform = current.GetTotalTransform();
						List<ValueTuple<double, double>> linkFloors = this.CollectFloorZRanges(linkDocument, totalTransform);
						list.AddRange(linkFloors);
					}
				}
				List<RiserPipeItem> list2 = new List<RiserPipeItem>();
				this.CollectRiserPipesFromDoc(document, activeView, list, list2, null, null);
				if (list2.Count == 0)
				{
					TaskDialog.Show("Нумерация стояков", "На текущем виде не найдено вертикальных труб, пересекающих перекрытия.\n\nУбедитесь, что на виде видны вертикальные трубы (стояки) в зоне перекрытий.");
					result = Result.Cancelled;
				}
				else
				{
					List<string> stringParameters = this.GetStringParameters(list2.First<RiserPipeItem>());
					RiserNumberingWindow riserNumberingWindow = new RiserNumberingWindow(list2, stringParameters);
					new WindowInteropHelper(riserNumberingWindow).Owner = application.MainWindowHandle;
					if (!riserNumberingWindow.ShowDialog().GetValueOrDefault())
					{
						result = Result.Cancelled;
					}
					else
					{
						using (Transaction transaction = new Transaction(document, "Нумерация стояков"))
						{
							transaction.Start();
							string selectedParamName = riserNumberingWindow.SelectedParamName;
							foreach (RiserPipeItem current2 in list2.Where(p => p.IsChecked && !string.IsNullOrEmpty(p.AssignedNumber)))
							{
								if (!current2.IsFromLinkedModel)
								{
									Parameter parameter = current2.PipeElement.LookupParameter(selectedParamName);
									if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.String)
									{
										parameter.Set(current2.AssignedNumber);
									}
								}
							}
							transaction.Commit();
						}
						int num = list2.Count(p => p.IsChecked && !string.IsNullOrEmpty(p.AssignedNumber) && !p.IsFromLinkedModel);
						Logger.Log(string.Format("Нумерация стояков завершена. Записано номеров: {0}.", num), "INFO");
						TaskDialog.Show("Нумерация стояков", string.Format("Готово! Присвоено номеров: {0}", num));
						result = Result.Succeeded;
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при нумерации стояков", ex);
				message = ex.Message + "\n" + ex.StackTrace;
				result = Result.Failed;
			}
			return result;
		}

		private List<ValueTuple<double, double>> CollectFloorZRanges(Document doc, Autodesk.Revit.DB.Transform transform)
		{
			List<ValueTuple<double, double>> list = new List<ValueTuple<double, double>>();
			try
			{
				using (List<Element>.Enumerator enumerator = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType().ToList<Element>().GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						BoundingBoxXYZ boundingBoxXYZ = enumerator.Current.get_BoundingBox(null);
						if (boundingBoxXYZ != null)
						{
							XYZ minPoint = boundingBoxXYZ.Min;
							XYZ maxPoint = boundingBoxXYZ.Max;
							if (transform != null && !transform.IsIdentity)
							{
								minPoint = transform.OfPoint(minPoint);
								maxPoint = transform.OfPoint(maxPoint);
							}
							double minZ = Math.Min(minPoint.Z, maxPoint.Z);
							double maxZ = Math.Max(minPoint.Z, maxPoint.Z);
							list.Add(new ValueTuple<double, double>(minZ, maxZ));
						}
					}
				}
			}
			catch
			{
			}
			return list;
		}
		private void CollectRiserPipesFromDoc(Document doc, View activeView, List<ValueTuple<double, double>> floorRanges, List<RiserPipeItem> result, string linkName, Autodesk.Revit.DB.Transform linkTransform)
		{
			try
			{
				IList<Element> list;
				if (activeView != null)
				{
					list = new FilteredElementCollector(doc, activeView.Id).OfClass(typeof(Pipe)).WhereElementIsNotElementType().ToList<Element>();
				}
				else
				{
					list = new FilteredElementCollector(doc).OfClass(typeof(Pipe)).WhereElementIsNotElementType().ToList<Element>();
				}
				foreach (Element arg_63_0 in list)
				{
					Pipe pipe = arg_63_0 as Pipe;
					if (pipe != null)
					{
						LocationCurve locationCurve = pipe.Location as LocationCurve;
						if (locationCurve != null)
						{
							Line line = locationCurve.Curve as Line;
							if (!(line == null) && Math.Abs(line.Direction.Z) >= 0.7)
							{
								XYZ xYZ = line.GetEndPoint(0);
								XYZ xYZ2 = line.GetEndPoint(1);
								if (linkTransform != null)
								{
									xYZ = linkTransform.OfPoint(xYZ);
									xYZ2 = linkTransform.OfPoint(xYZ2);
								}
								double pipeZMin = Math.Min(xYZ.Z, xYZ2.Z);
								double pipeZMax = Math.Max(xYZ.Z, xYZ2.Z);
								if (floorRanges.Any(fr => pipeZMin < fr.Item2 && pipeZMax > fr.Item1))
								{
									string systemName = "";
									try
									{
										Parameter parameter = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
										if (parameter != null)
										{
											systemName = (parameter.AsString() ?? "");
										}
									}
									catch
									{
									}
									string diameter = "";
									try
									{
										double diameter2 = pipe.Diameter;
										diameter = string.Format("DN{0}", (int)Math.Round(diameter2 * 304.8));
									}
									catch
									{
									}
									string currentNumber = "";
									try
									{
										Parameter parameter2 = pipe.LookupParameter("ADSK_Номер_стояка") ?? pipe.LookupParameter("ADSK_Номер стояка");
										if (parameter2 != null)
										{
											currentNumber = (parameter2.AsString() ?? "");
										}
									}
									catch
									{
									}
									string level = "";
									try
									{
										Parameter expr_1EC = pipe.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
										ElementId elementId = (expr_1EC != null) ? expr_1EC.AsElementId() : null;
										if (elementId != null && elementId != ElementId.InvalidElementId)
										{
											Element element = doc.GetElement(elementId);
											if (element != null)
											{
												level = element.Name;
											}
										}
									}
									catch
									{
									}
									result.Add(new RiserPipeItem
									{
										PipeElement = pipe,
										SystemName = systemName,
										Diameter = diameter,
										Level = level,
										CurrentNumber = currentNumber,
										IsFromLinkedModel = linkName != null,
										LinkedModelName = linkName ?? "",
										CenterX = (xYZ.X + xYZ2.X) / 2.0,
										CenterY = (xYZ.Y + xYZ2.Y) / 2.0,
										IsChecked = true
									});
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}
		private List<string> GetStringParameters(RiserPipeItem item)
		{
			List<string> list = new List<string>();
			list.Add("ADSK_Номер_стояка");
			if (((item != null) ? item.PipeElement : null) == null)
			{
				return list;
			}
			try
			{
				foreach (Parameter parameter in item.PipeElement.Parameters)
				{
					if (parameter.StorageType == StorageType.String && !parameter.IsReadOnly)
					{
						Definition expr_58 = parameter.Definition;
						string text = (expr_58 != null) ? expr_58.Name : null;
						if (!string.IsNullOrEmpty(text) && !list.Contains(text))
						{
							list.Add(text);
						}
					}
				}
			}
			catch
			{
			}
			return list.OrderBy(x => x).ToList();
		}
	}
	public class RiserPipeItem : INotifyPropertyChanged
	{
		private bool _isChecked = true;
		private string _assignedNumber = "";
		public event PropertyChangedEventHandler PropertyChanged;
		public Element PipeElement
		{
			get;
			set;
		}
		public string SystemName
		{
			get;
			set;
		}
		public string Diameter
		{
			get;
			set;
		}
		public string Level
		{
			get;
			set;
		}
		public string CurrentNumber
		{
			get;
			set;
		}
		public bool IsFromLinkedModel
		{
			get;
			set;
		}
		public string LinkedModelName
		{
			get;
			set;
		}
		public double CenterX
		{
			get;
			set;
		}
		public double CenterY
		{
			get;
			set;
		}
		public bool IsChecked
		{
			get
			{
				return this._isChecked;
			}
			set
			{
				this._isChecked = value;
				this.OnPropertyChanged("IsChecked");
			}
		}
		public string AssignedNumber
		{
			get
			{
				return this._assignedNumber;
			}
			set
			{
				this._assignedNumber = value;
				this.OnPropertyChanged("AssignedNumber");
			}
		}
		public string DisplaySource
		{
			get
			{
				if (!this.IsFromLinkedModel)
				{
					return "Основной";
				}
				return "[" + this.LinkedModelName + "]";
			}
		}
		protected void OnPropertyChanged(string name)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
	public class RiserNumberingWindow : Window
	{

		private readonly List<RiserPipeItem> _pipes;
		private readonly List<string> _availableParams;
		private DataGrid _dataGrid;
		private System.Windows.Controls.ComboBox _cbParam;
		private System.Windows.Controls.TextBox _txtPrefix;
		private System.Windows.Controls.TextBox _txtStartFrom;
		private System.Windows.Controls.ComboBox _cbSortMode;
		private Button _btnPreview;
		private Button _btnNumber;
		public string SelectedParamName
		{
			get;
			private set;
		}
		public RiserNumberingWindow(List<RiserPipeItem> pipes, List<string> availableParams)
		{
			this.SelectedParamName = "ADSK_Номер_стояка";
			this._pipes = pipes;
			this._availableParams = availableParams;
			this.InitializeComponent();
		}
		private Style GetRoundedButtonStyle(double radius)
		{
			Style arg_E5_0 = new Style(typeof(Button));
			ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
			FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
			frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
			frameworkElementFactory.SetValue(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
			{
				RelativeSource = RelativeSource.TemplatedParent
			});
			frameworkElementFactory.SetValue(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush")
			{
				RelativeSource = RelativeSource.TemplatedParent
			});
			frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness")
			{
				RelativeSource = RelativeSource.TemplatedParent
			});
			FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
			frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
			frameworkElementFactory.AppendChild(frameworkElementFactory2);
			controlTemplate.VisualTree = frameworkElementFactory;
			arg_E5_0.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty, controlTemplate));
			return arg_E5_0;
		}
		private void InitializeComponent()
		{
			base.Title = "Нумерация стояков — BimboClub Tools";
			base.Width = 860.0;
			base.Height = 600.0;
			base.MinWidth = 700.0;
			base.MinHeight = 450.0;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28));
			base.FontFamily = new FontFamily("Segoe UI");
			base.FontSize = 13.0;
			SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38));
			SolidColorBrush solidColorBrush2 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 56));
			SolidColorBrush accentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));
			SolidColorBrush cancelNormal = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 66));
			SolidColorBrush cancelHover = new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 78, 88));
			Style roundedButtonStyle = this.GetRoundedButtonStyle(6.0);
			System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(56.0)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(100.0)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(54.0)
			});
			base.Content = grid;
			Border border = new Border
			{
				Background = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(18, 18, 20), System.Windows.Media.Color.FromRgb(179, 14, 45), 0.0),
				Padding = new Thickness(20.0, 0.0, 20.0, 0.0)
			};
			System.Windows.Controls.Grid.SetRow(border, 0);
			grid.Children.Add(border);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			border.Child = stackPanel;
			string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon32.png");
			if (File.Exists(text))
			{
				Image element = new Image
				{
					Source = new BitmapImage(new Uri(text)),
					Width = 24.0,
					Height = 24.0,
					Margin = new Thickness(0.0, 0.0, 10.0, 0.0),
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel.Children.Add(element);
			}
			TextBlock element2 = new TextBlock
			{
				Text = "Нумерация стояков",
				FontSize = 20.0,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(element2);
			TextBlock element3 = new TextBlock
			{
				Text = string.Format("  •  найдено {0} стояков", this._pipes.Count),
				FontSize = 13.0,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
			};
			stackPanel.Children.Add(element3);
			Border border2 = new Border
			{
				Background = solidColorBrush,
				BorderBrush = solidColorBrush2,
				BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				Padding = new Thickness(16.0, 10.0, 16.0, 10.0)
			};
			System.Windows.Controls.Grid.SetRow(border2, 1);
			grid.Children.Add(border2);
			System.Windows.Controls.Grid grid2 = new System.Windows.Controls.Grid();
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(220.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(16.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(100.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(16.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(80.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(16.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(80.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(16.0)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(160.0)
			});
			grid2.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid2.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(6.0)
			});
			grid2.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			border2.Child = grid2;
			Style style = new Style(typeof(TextBlock));
			style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 200))));
			style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
			style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 6.0, 0.0)));
			Style style2 = new Style(typeof(System.Windows.Controls.TextBox));
			style2.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53))));
			style2.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style2.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, solidColorBrush2));
			style2.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(5.0, 3.0, 5.0, 3.0)));
			style2.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
			this.AddLabel(grid2, "Параметр:", 0, 0, 0, style);
			this._cbParam = new System.Windows.Controls.ComboBox
			{
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
				Foreground = Brushes.White,
				BorderBrush = solidColorBrush2
			};
			foreach (string current in this._availableParams)
			{
				this._cbParam.Items.Add(current);
			}
			this._cbParam.SelectedIndex = 0;
			System.Windows.Controls.Grid.SetRow(this._cbParam, 0);
			System.Windows.Controls.Grid.SetColumn(this._cbParam, 1);
			grid2.Children.Add(this._cbParam);
			UiThemeHelper.StyleComboBoxDark(this._cbParam, solidColorBrush, solidColorBrush2);
			this.AddLabel(grid2, "Префикс:", 2, 3, 0, style);
			this._txtPrefix = new System.Windows.Controls.TextBox
			{
				Text = "Ст-",
				Width = 80.0,
				Style = style2
			};
			System.Windows.Controls.Grid.SetRow(this._txtPrefix, 2);
			System.Windows.Controls.Grid.SetColumn(this._txtPrefix, 4);
			grid2.Children.Add(this._txtPrefix);
			this.AddLabel(grid2, "Начать с:", 2, 6, 0, style);
			this._txtStartFrom = new System.Windows.Controls.TextBox
			{
				Text = "1",
				Width = 60.0,
				Style = style2
			};
			System.Windows.Controls.Grid.SetRow(this._txtStartFrom, 2);
			System.Windows.Controls.Grid.SetColumn(this._txtStartFrom, 7);
			grid2.Children.Add(this._txtStartFrom);
			this.AddLabel(grid2, "Сортировка:", 2, 9, 0, style);
			this._cbSortMode = new System.Windows.Controls.ComboBox
			{
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
				Foreground = Brushes.White,
				BorderBrush = solidColorBrush2,
				Width = 140.0
			};
			this._cbSortMode.Items.Add("Слева направо (X)");
			this._cbSortMode.Items.Add("Снизу вверх (Y)");
			this._cbSortMode.Items.Add("По системе");
			this._cbSortMode.SelectedIndex = 0;
			System.Windows.Controls.Grid.SetRow(this._cbSortMode, 2);
			System.Windows.Controls.Grid.SetColumn(this._cbSortMode, 10);
			grid2.Children.Add(this._cbSortMode);
			UiThemeHelper.StyleComboBoxDark(this._cbSortMode, solidColorBrush, solidColorBrush2);
			this._btnPreview = new Button
			{
				Content = "Предпросмотр",
				Foreground = Brushes.White,
				Background = accentBrush,
				BorderThickness = new Thickness(0.0),
				Padding = new Thickness(14.0, 4.0, 14.0, 4.0),
				Cursor = Cursors.Hand,
				Style = roundedButtonStyle,
				Margin = new Thickness(16.0, 0.0, 0.0, 0.0)
			};
			this._btnPreview.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				this._btnPreview.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 20, 50));
			};
			this._btnPreview.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				this._btnPreview.Background = accentBrush;
			};
			System.Windows.Controls.Grid.SetRow(this._btnPreview, 2);
			System.Windows.Controls.Grid.SetColumn(this._btnPreview, 12);
			grid2.Children.Add(this._btnPreview);
			this._btnPreview.Click += new RoutedEventHandler(this.BtnPreview_Click);
			Border border3 = new Border
			{
				Background = solidColorBrush,
				Margin = new Thickness(12.0, 0.0, 12.0, 0.0)
			};
			System.Windows.Controls.Grid.SetRow(border3, 2);
			grid.Children.Add(border3);
			this._dataGrid = new DataGrid
			{
				AutoGenerateColumns = false,
				CanUserAddRows = false,
				CanUserDeleteRows = false,
				Background = Brushes.Transparent,
				Foreground = Brushes.White,
				BorderThickness = new Thickness(0.0),
				RowBackground = solidColorBrush,
				AlternatingRowBackground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(36, 36, 42)),
				GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
				HorizontalGridLinesBrush = solidColorBrush2,
				HeadersVisibility = DataGridHeadersVisibility.Column,
				SelectionMode = DataGridSelectionMode.Extended,
				IsReadOnly = false,
				Margin = new Thickness(0.0)
			};
			Style style3 = new Style(typeof(DataGridCell));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, Brushes.Transparent));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0.0)));
			style3.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
			Trigger trigger = new Trigger
			{
				Property = DataGridCell.IsSelectedProperty,
				Value = true
			};
			trigger.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45))));
			trigger.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style3.Triggers.Add(trigger);
			this._dataGrid.CellStyle = style3;
			this._dataGrid.Resources.Add(SystemColors.HighlightBrushKey, new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45)));
			this._dataGrid.Resources.Add(SystemColors.HighlightTextBrushKey, Brushes.White);
			this._dataGrid.Resources.Add(SystemColors.InactiveSelectionHighlightBrushKey, new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 66)));
			this._dataGrid.Resources.Add(SystemColors.InactiveSelectionHighlightTextBrushKey, Brushes.White);
			Style style4 = new Style(typeof(System.Windows.Controls.TextBox));
			style4.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 48))));
			style4.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style4.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, Brushes.White));
			style4.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45))));
			style4.Setters.Add(new Setter(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(1.0)));
			style4.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(2.0)));
			this._dataGrid.Resources.Add(typeof(System.Windows.Controls.TextBox), style4);
			Style style5 = new Style(typeof(CheckBox));
			style5.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center));
			style5.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
			this._dataGrid.Resources.Add(typeof(CheckBox), style5);
			Style style6 = new Style(typeof(DataGridColumnHeader));
			style6.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 40, 50))));
			style6.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style6.Setters.Add(new Setter(System.Windows.Controls.Control.FontWeightProperty, FontWeights.SemiBold));
			style6.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(8.0, 4.0, 8.0, 4.0)));
			this._dataGrid.ColumnHeaderStyle = style6;
			Style style7 = new Style(typeof(DataGridRow));
			style7.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			Trigger trigger2 = new Trigger
			{
				Property = DataGridRow.IsSelectedProperty,
				Value = true
			};
			trigger2.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45))));
			trigger2.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style7.Triggers.Add(trigger2);
			this._dataGrid.RowStyle = style7;
			this._dataGrid.Columns.Add(new DataGridCheckBoxColumn
			{
				Header = "✓",
				Binding = new System.Windows.Data.Binding("IsChecked")
				{
					Mode = BindingMode.TwoWay,
					UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
				},
				Width = 36.0
			});
			this._dataGrid.Columns.Add(new DataGridTextColumn
			{
				Header = "Система",
				Binding = new System.Windows.Data.Binding("SystemName"),
				Width = new DataGridLength(1.0, DataGridLengthUnitType.Star),
				IsReadOnly = true
			});
			this._dataGrid.Columns.Add(new DataGridTextColumn
			{
				Header = "Диаметр",
				Binding = new System.Windows.Data.Binding("Diameter"),
				Width = 80.0,
				IsReadOnly = true
			});
			this._dataGrid.Columns.Add(new DataGridTextColumn
			{
				Header = "Уровень",
				Binding = new System.Windows.Data.Binding("Level"),
				Width = 100.0,
				IsReadOnly = true
			});
			this._dataGrid.Columns.Add(new DataGridTextColumn
			{
				Header = "Тек. номер",
				Binding = new System.Windows.Data.Binding("CurrentNumber"),
				Width = 100.0,
				IsReadOnly = true
			});
			this._dataGrid.Columns.Add(new DataGridTextColumn
			{
				Header = "Новый номер",
				Binding = new System.Windows.Data.Binding("AssignedNumber")
				{
					Mode = BindingMode.TwoWay,
					UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
				},
				Width = 120.0
			});
			this._dataGrid.Columns.Add(new DataGridTextColumn
			{
				Header = "Источник",
				Binding = new System.Windows.Data.Binding("DisplaySource"),
				Width = 120.0,
				IsReadOnly = true
			});
			this._dataGrid.ItemsSource = this._pipes;
			border3.Child = this._dataGrid;
			Border border4 = new Border
			{
				Background = solidColorBrush,
				BorderBrush = solidColorBrush2,
				BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
				Padding = new Thickness(16.0, 0.0, 16.0, 0.0)
			};
			System.Windows.Controls.Grid.SetRow(border4, 3);
			grid.Children.Add(border4);
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center
			};
			border4.Child = stackPanel2;
			Button btnCancel = new Button
			{
				Content = "Отмена",
				Foreground = Brushes.White,
				Background = cancelNormal,
				BorderThickness = new Thickness(0.0),
				Width = 140.0,
				Height = 36.0,
				Cursor = Cursors.Hand,
				Style = roundedButtonStyle,
				Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
			};
			btnCancel.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				btnCancel.Background = cancelHover;
			};
			btnCancel.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				btnCancel.Background = cancelNormal;
			};
			btnCancel.Click += delegate(object s, RoutedEventArgs e)
			{
				this.DialogResult = new bool?(false);
				this.Close();
			};
			stackPanel2.Children.Add(btnCancel);
			this._btnNumber = new Button
			{
				Content = "Пронумеровать",
				Foreground = Brushes.White,
				Background = accentBrush,
				BorderThickness = new Thickness(0.0),
				Width = 180.0,
				Height = 36.0,
				Cursor = Cursors.Hand,
				Style = roundedButtonStyle
			};
			this._btnNumber.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				this._btnNumber.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 20, 50));
			};
			this._btnNumber.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				this._btnNumber.Background = accentBrush;
			};
			this._btnNumber.Click += new RoutedEventHandler(this.BtnNumber_Click);
			stackPanel2.Children.Add(this._btnNumber);
			base.Loaded += delegate(object s, RoutedEventArgs e)
			{
				this.BtnPreview_Click(null, null);
			};
		}

		private void AddLabel(System.Windows.Controls.Grid grid, string text, int row, int col, int rowSpan, Style style)
		{
			TextBlock element = new TextBlock
			{
				Text = text,
				Style = style
			};
			System.Windows.Controls.Grid.SetRow(element, row);
			System.Windows.Controls.Grid.SetColumn(element, col);
			grid.Children.Add(element);
		}
		private void BtnPreview_Click(object sender, RoutedEventArgs e)
		{
			string arg = this._txtPrefix.Text ?? "";
			int num2;
			int num = int.TryParse(this._txtStartFrom.Text, out num2) ? num2 : 1;
			int selectedIndex = this._cbSortMode.SelectedIndex;
			IEnumerable<RiserPipeItem> enumerable;
			if (selectedIndex == 1)
			{
				enumerable = this._pipes.OrderBy(p => p.CenterY).ThenBy(p => p.CenterX);
			}
			else if (selectedIndex == 2)
			{
				enumerable = this._pipes.OrderBy(p => p.SystemName).ThenBy(p => p.CenterX);
			}
			else
			{
				enumerable = this._pipes.OrderBy(p => p.CenterX).ThenBy(p => p.CenterY);
			}
			int num3 = num;
			foreach (RiserPipeItem current in enumerable)
			{
				if (current.IsChecked)
				{
					current.AssignedNumber = string.Format("{0}{1}", arg, num3);
					num3++;
				}
				else
				{
					current.AssignedNumber = "";
				}
			}
			this._dataGrid.Items.Refresh();
		}
		private void BtnNumber_Click(object sender, RoutedEventArgs e)
		{
			this.BtnPreview_Click(null, null);
			this.SelectedParamName = ((this._cbParam.SelectedItem as string) ?? "ADSK_Номер_стояка");
			if (this._pipes.Count(p => p.IsChecked && !string.IsNullOrEmpty(p.AssignedNumber) && !p.IsFromLinkedModel) == 0)
			{
				MessageBox.Show("Нет стояков для нумерации.\n(Стояки из связанных файлов не могут быть пронумерованы — они только для просмотра.)", "Нумерация стояков", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			base.DialogResult = new bool?(true);
			base.Close();
		}
	}
}
