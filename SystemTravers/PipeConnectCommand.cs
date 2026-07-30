using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace BimboClub
{
	[Transaction(TransactionMode.Manual)]
	public class PipeConnectCommand : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			UIApplication application = commandData.Application;
			UIDocument activeUIDocument = application.ActiveUIDocument;
			Document document = activeUIDocument.Document;
			Result result;
			try
			{
				Logger.Log("Запуск подключения двух труб.", "INFO");
				IList<Reference> list;
				try
				{
					list = activeUIDocument.Selection.PickObjects(ObjectType.Element, new PipeSelectionFilter(), "Выберите ровно 2 трубы для подключения (затем нажмите Finish)");
				}
				catch (Autodesk.Revit.Exceptions.OperationCanceledException)
				{
					result = Result.Cancelled;
					return result;
				}
				if (list == null || list.Count < 2)
				{
					TaskDialog.Show("Подключение труб", "Необходимо выбрать ровно 2 трубы.");
					result = Result.Cancelled;
				}
				else
				{
					Pipe pipe = document.GetElement(list[0]) as Pipe;
					Pipe pipe2 = document.GetElement(list[1]) as Pipe;
					if (pipe == null || pipe2 == null)
					{
						TaskDialog.Show("Подключение труб", "Выбранные элементы должны быть трубами.");
						result = Result.Cancelled;
					}
					else
					{
						XYZ pipeDirection = this.GetPipeDirection(pipe);
						XYZ pipeDirection2 = this.GetPipeDirection(pipe2);
						if (pipeDirection == null || pipeDirection2 == null)
						{
							TaskDialog.Show("Подключение труб", "Не удалось получить направление труб.");
							result = Result.Failed;
						}
						else
						{
							PipeType pipeType = document.GetElement(pipe.GetTypeId()) as PipeType;
							PipeConnectWindow pipeConnectWindow = new PipeConnectWindow(pipe, pipe2, pipeDirection, pipeDirection2);
							new WindowInteropHelper(pipeConnectWindow).Owner = application.MainWindowHandle;
							if (!pipeConnectWindow.ShowDialog().GetValueOrDefault())
							{
								result = Result.Cancelled;
							}
							else
							{
								using (Transaction transaction = new Transaction(document, "Подключение труб"))
								{
									transaction.Start();
									this.ConnectPipes(document, pipe2, pipe, pipeDirection2, pipeDirection, pipeType, pipeConnectWindow.ConnectionFromTop, pipeConnectWindow.ElbowAngle, pipeConnectWindow.HeightOffsetMm, pipeConnectWindow.RiseToConnectionMm);
									transaction.Commit();
								}
								Logger.Log("Подключение труб выполнено успешно.", "INFO");
								result = Result.Succeeded;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				Logger.LogError("Ошибка при подключении труб", ex);
				message = ex.Message + "\n" + ex.StackTrace;
				result = Result.Failed;
			}
			return result;
		}
		private XYZ GetPipeDirection(Pipe pipe)
		{
			LocationCurve locationCurve = pipe.Location as LocationCurve;
			if (locationCurve == null)
			{
				return null;
			}
			Line line = locationCurve.Curve as Line;
			if (line == null)
			{
				return null;
			}
			return line.Direction.Normalize();
		}
		private void ConnectPipes(Document doc, Pipe pipe1, Pipe pipe2, XYZ dir1, XYZ dir2, PipeType pipeType, bool fromTop, int elbowAngle, double heightOffsetMm, double riseToConnectionMm)
		{
			double num = heightOffsetMm / 304.8;
			double value = riseToConnectionMm / 304.8;
			double num2 = fromTop ? 1.0 : -1.0;
			LocationCurve locationCurve = pipe1.Location as LocationCurve;
			LocationCurve locationCurve2 = pipe2.Location as LocationCurve;
			if (locationCurve == null || locationCurve2 == null)
			{
				return;
			}
			Line line = locationCurve.Curve as Line;
			Line line2 = locationCurve2.Curve as Line;
			if (line == null || line2 == null)
			{
				return;
			}
			XYZ endPoint = line.GetEndPoint(0);
			XYZ endPoint2 = line.GetEndPoint(1);
			XYZ endPoint3 = line2.GetEndPoint(0);
			XYZ endPoint4 = line2.GetEndPoint(1);
			XYZ xYZ = new XYZ(endPoint2.X - endPoint.X, endPoint2.Y - endPoint.Y, 0.0).Normalize();
			XYZ xYZ2 = new XYZ(endPoint4.X - endPoint3.X, endPoint4.Y - endPoint3.Y, 0.0).Normalize();
			XYZ xYZ3 = new XYZ(endPoint.X, endPoint.Y, 0.0);
			XYZ xYZ4 = new XYZ(endPoint3.X, endPoint3.Y, 0.0);
			XYZ xYZ5 = new XYZ(endPoint4.X, endPoint4.Y, 0.0);
			double arg_198_0 = (xYZ4 - xYZ3).CrossProduct(xYZ).GetLength();
			double length = (xYZ5 - xYZ3).CrossProduct(xYZ).GetLength();
			XYZ xYZ6 = (arg_198_0 < length) ? xYZ4 : xYZ5;
			XYZ xYZ7 = (arg_198_0 < length) ? xYZ5 : xYZ4;
			XYZ xYZ8 = (arg_198_0 < length) ? endPoint4 : endPoint3;
			double value2 = (xYZ6 - xYZ3).DotProduct(xYZ);
			XYZ xYZ9 = xYZ3 + value2 * xYZ;
			XYZ expr_1EB = xYZ9 - xYZ6;
			XYZ left = expr_1EB.Normalize();
			if (expr_1EB.GetLength() < 0.001)
			{
				left = new XYZ(-xYZ.Y, xYZ.X, 0.0);
			}
			XYZ xYZ10 = xYZ9 - left * value;
			double value3 = (xYZ10 - xYZ7).DotProduct(xYZ2);
			XYZ xYZ11 = xYZ7 + value3 * xYZ2;
			XYZ xYZ12 = new XYZ(xYZ11.X, xYZ11.Y, xYZ8.Z);
			XYZ xYZ13 = new XYZ(xYZ10.X, xYZ10.Y, xYZ8.Z);
			double num3 = 0.5 * (endPoint.Z + endPoint2.Z);
			XYZ xYZ14 = new XYZ(xYZ10.X, xYZ10.Y, num3 + num2 * num);
			XYZ xYZ15 = new XYZ(xYZ9.X, xYZ9.Y, num3 + num2 * num);
			XYZ xYZ16 = new XYZ(xYZ9.X, xYZ9.Y, num3);
			if (xYZ8.DistanceTo(xYZ12) > 0.01)
			{
				try
				{
					locationCurve2.Curve = Line.CreateBound(xYZ8, xYZ12);
				}
				catch
				{
				}
			}
			ElementId pipeSystemTypeId = this.GetPipeSystemTypeId(pipe2);
			Pipe pipe3 = this.CreatePipeSegment(doc, xYZ12, xYZ13, pipe2, pipeSystemTypeId);
			Pipe pipe4 = this.CreatePipeSegment(doc, xYZ13, xYZ14, pipe2, pipeSystemTypeId);
			Pipe pipe5 = this.CreatePipeSegment(doc, xYZ14, xYZ15, pipe2, pipeSystemTypeId);
			Pipe pipe6 = this.CreatePipeSegment(doc, xYZ15, xYZ16, pipe2, pipeSystemTypeId);
			List<Pipe> list = new List<Pipe>();
			list.Add(pipe2);
			if (pipe3 != null)
			{
				list.Add(pipe3);
			}
			if (pipe4 != null)
			{
				list.Add(pipe4);
			}
			if (pipe5 != null)
			{
				list.Add(pipe5);
			}
			if (pipe6 != null)
			{
				list.Add(pipe6);
			}
			for (int i = 0; i < list.Count - 1; i++)
			{
				MEPCurve arg_3DB_0 = list[i];
				Pipe pipe7 = list[i + 1];
				Connector connector = null;
				Connector connector2 = null;
				double num4 = 1.7976931348623157E+308;
				foreach (Connector connector3 in arg_3DB_0.ConnectorManager.Connectors)
				{
					foreach (Connector connector4 in pipe7.ConnectorManager.Connectors)
					{
						double num5 = connector3.Origin.DistanceTo(connector4.Origin);
						if (num5 < num4)
						{
							num4 = num5;
							connector = connector3;
							connector2 = connector4;
						}
					}
				}
				if (connector != null && connector2 != null && num4 < 0.1)
				{
					try
					{
						doc.Create.NewElbowFitting(connector, connector2);
					}
					catch (Exception ex)
					{
						Logger.Log("Не удалось создать отвод (уголок): " + ex.Message, "WARNING");
					}
				}
			}
			if (list.Count > 0)
			{
				Pipe pipe8 = list.Last<Pipe>();
				Connector connector5 = this.FindConnector(pipe8, xYZ16);
				if (connector5 != null)
				{
					Pipe pipe9 = null;
					try
					{
						ElementId elementId = PlumbingUtils.BreakCurve(doc, pipe1.Id, xYZ16);
						if (elementId != ElementId.InvalidElementId)
						{
							pipe9 = (doc.GetElement(elementId) as Pipe);
						}
					}
					catch (Exception ex2)
					{
						Logger.Log("Не удалось разрезать магистральную трубу: " + ex2.Message, "WARNING");
					}
					if (pipe9 != null)
					{
						Connector connector6 = this.FindConnector(pipe1, xYZ16);
						Connector connector7 = this.FindConnector(pipe9, xYZ16);
						if (connector6 == null || connector7 == null)
						{
							return;
						}
						try
						{
							doc.Create.NewTeeFitting(connector6, connector7, connector5);
							return;
						}
						catch
						{
							try
							{
								doc.Create.NewTakeoffFitting(connector5, pipe1);
							}
							catch (Exception ex3)
							{
								Logger.Log("Не удалось создать тройник/врезку: " + ex3.Message, "WARNING");
							}
							return;
						}
					}
					try
					{
						doc.Create.NewTakeoffFitting(connector5, pipe1);
					}
					catch (Exception ex4)
					{
						Logger.Log("Не удалось сделать врезку в магистраль: " + ex4.Message, "WARNING");
					}
				}
			}
		}
		private XYZ Intersect2D(XYZ p1, XYZ d1, XYZ p2, XYZ d2)
		{
			double num = d1.X * d2.Y - d1.Y * d2.X;
			if (Math.Abs(num) < 1E-05)
			{
				return null;
			}
			double value = ((p2.X - p1.X) * d2.Y - (p2.Y - p1.Y) * d2.X) / num;
			return p1 + value * d1;
		}
		private Connector FindConnector(Pipe pipe, XYZ point)
		{
			if (pipe == null)
			{
				return null;
			}
			ConnectorManager connectorManager = pipe.ConnectorManager;
			if (connectorManager == null)
			{
				return null;
			}
			Connector result = null;
			double num = 1.7976931348623157E+308;
			foreach (Connector connector in connectorManager.Connectors)
			{
				double num2 = connector.Origin.DistanceTo(point);
				if (num2 < num)
				{
					num = num2;
					result = connector;
				}
			}
			return result;
		}
		private Pipe CreatePipeSegment(Document doc, XYZ start, XYZ end, Pipe referencePipe, ElementId sysTypeId)
		{
			if (start.DistanceTo(end) < 0.005)
			{
				return null;
			}
			Parameter expr_21 = referencePipe.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
			ElementId levelId = ((expr_21 != null) ? expr_21.AsElementId() : null) ?? ElementId.InvalidElementId;
			ElementId typeId = referencePipe.GetTypeId();
			Pipe result;
			try
			{
				Pipe pipe = Pipe.Create(doc, sysTypeId, typeId, levelId, start, end);
				if (pipe != null)
				{
					double diameter = referencePipe.Diameter;
					Parameter parameter = pipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
					if (parameter != null && !parameter.IsReadOnly)
					{
						parameter.Set(diameter);
					}
				}
				result = pipe;
			}
			catch (Exception ex)
			{
				Logger.Log("Не удалось создать сегмент трубы: " + ex.Message, "WARNING");
				result = null;
			}
			return result;
		}
		private ElementId GetPipeSystemTypeId(Pipe pipe)
		{
			try
			{
				ConnectorManager connectorManager = pipe.ConnectorManager;
				if (connectorManager != null)
				{
					foreach (Connector connector in connectorManager.Connectors)
					{
						if (connector.MEPSystem != null)
						{
							PipingSystem pipingSystem = connector.MEPSystem as PipingSystem;
							if (pipingSystem != null)
							{
								ElementId typeId = pipingSystem.GetTypeId();
								if (typeId != ElementId.InvalidElementId)
								{
									ElementId result = typeId;
									return result;
								}
							}
						}
					}
				}
			}
			catch
			{
			}
			try
			{
				Element element = new FilteredElementCollector(pipe.Document).OfClass(typeof(PipingSystemType)).FirstOrDefault<Element>();
				if (element != null)
				{
					ElementId result = element.Id;
					return result;
				}
			}
			catch
			{
			}
			return ElementId.InvalidElementId;
		}
	}
	public class PipeSelectionFilter : ISelectionFilter
	{
		public bool AllowElement(Element elem)
		{
			return elem is Pipe;
		}
		public bool AllowReference(Reference reference, XYZ position)
		{
			return true;
		}
	}
	public class PipeConnectWindow : Window
	{
		private RadioButton _rbTop;
		private RadioButton _rbBottom;
		private RadioButton _rb90;
		private RadioButton _rb45;
		private System.Windows.Controls.TextBox _txtHeightOffset;
		private System.Windows.Controls.TextBox _txtRiseToConnection;
		private Button _btnOk;
		private Button _btnCancel;
		public bool ConnectionFromTop
		{
			get
			{
				return this._rbTop.IsChecked.GetValueOrDefault();
			}
		}
		public int ElbowAngle
		{
			get
			{
				if (!this._rb45.IsChecked.GetValueOrDefault())
				{
					return 90;
				}
				return 45;
			}
		}
		public double HeightOffsetMm
		{
			get
			{
				double result;
				if (!double.TryParse(this._txtHeightOffset.Text, out result))
				{
					return 150.0;
				}
				return result;
			}
		}
		public double RiseToConnectionMm
		{
			get
			{
				double result;
				if (!double.TryParse(this._txtRiseToConnection.Text, out result))
				{
					return 200.0;
				}
				return result;
			}
		}
		public PipeConnectWindow(Pipe pipe1, Pipe pipe2, XYZ dir1, XYZ dir2)
		{
			this.InitializeComponent(pipe1, pipe2, dir1, dir2);
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
		private void InitializeComponent(Pipe pipe1, Pipe pipe2, XYZ dir1, XYZ dir2)
		{
			base.Title = "Подключение труб — BimboClub Tools";
			base.Width = 440.0;
			base.SizeToContent = SizeToContent.Height;
			base.ResizeMode = ResizeMode.NoResize;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 24, 28));
			base.FontFamily = new FontFamily("Segoe UI");
			base.FontSize = 13.0;
			SolidColorBrush background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 38));
			SolidColorBrush solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 48, 56));
			SolidColorBrush accentBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));
			SolidColorBrush cancelNormal = new SolidColorBrush(System.Windows.Media.Color.FromRgb(58, 58, 66));
			SolidColorBrush cancelHover = new SolidColorBrush(System.Windows.Media.Color.FromRgb(78, 78, 88));
			Style roundedButtonStyle = this.GetRoundedButtonStyle(6.0);
			Style style = new Style(typeof(RadioButton));
			style.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 16.0, 0.0)));
			style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
			style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
			Style style2 = new Style(typeof(TextBlock));
			style2.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 200))));
			style2.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0.0, 0.0, 8.0, 0.0)));
			style2.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
			Style style3 = new Style(typeof(System.Windows.Controls.TextBox));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53))));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.ForegroundProperty, Brushes.White));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.BorderBrushProperty, solidColorBrush));
			style3.Setters.Add(new Setter(System.Windows.Controls.Control.PaddingProperty, new Thickness(6.0, 3.0, 6.0, 3.0)));
			style3.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical
			};
			base.Content = stackPanel;
			Border border = new Border
			{
				Background = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(18, 18, 20), System.Windows.Media.Color.FromRgb(179, 14, 45), 0.0),
				Padding = new Thickness(20.0, 14.0, 20.0, 14.0)
			};
			stackPanel.Children.Add(border);
			StackPanel stackPanel2 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				VerticalAlignment = VerticalAlignment.Center
			};
			border.Child = stackPanel2;
			string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon32.png");
			if (File.Exists(text))
			{
				Image element = new Image
				{
					Source = new BitmapImage(new Uri(text)),
					Width = 24.0,
					Height = 24.0,
					Margin = new Thickness(0.0, 0.0, 12.0, 0.0),
					VerticalAlignment = VerticalAlignment.Center
				};
				stackPanel2.Children.Add(element);
			}
			StackPanel stackPanel3 = new StackPanel
			{
				Orientation = Orientation.Vertical,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel2.Children.Add(stackPanel3);
			stackPanel3.Children.Add(new TextBlock
			{
				Text = "Подключение труб",
				FontSize = 18.0,
				FontWeight = FontWeights.Bold,
				Foreground = Brushes.White
			});
			stackPanel3.Children.Add(new TextBlock
			{
				Text = "Подключаемая: " + this.GetPipeInfo(pipe1) + "  •  Магистраль: " + this.GetPipeInfo(pipe2),
				FontSize = 11.0,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(200, 255, 255, 255)),
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			});
			Border border2 = new Border
			{
				Background = background,
				Padding = new Thickness(20.0, 14.0, 20.0, 14.0)
			};
			stackPanel.Children.Add(border2);
			StackPanel stackPanel4 = new StackPanel
			{
				Orientation = Orientation.Vertical
			};
			border2.Child = stackPanel4;
			stackPanel4.Children.Add(new TextBlock
			{
				Text = "Направление подключения",
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			});
			StackPanel stackPanel5 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
			};
			stackPanel4.Children.Add(stackPanel5);
			this._rbTop = new RadioButton
			{
				Content = "⬆ Сверху",
				IsChecked = new bool?(false),
				Style = style
			};
			this._rbBottom = new RadioButton
			{
				Content = "⬇ Снизу",
				IsChecked = new bool?(true),
				Style = style
			};
			stackPanel5.Children.Add(this._rbTop);
			stackPanel5.Children.Add(this._rbBottom);
			stackPanel4.Children.Add(new TextBlock
			{
				Text = "Тип отводов",
				Foreground = Brushes.White,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
			});
			StackPanel stackPanel6 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
			};
			stackPanel4.Children.Add(stackPanel6);
			this._rb90 = new RadioButton
			{
				Content = "90° (прямой)",
				IsChecked = new bool?(true),
				Style = style
			};
			this._rb45 = new RadioButton
			{
				Content = "45° (диагональный)",
				IsChecked = new bool?(false),
				Style = style
			};
			stackPanel6.Children.Add(this._rb90);
			stackPanel6.Children.Add(this._rb45);
			stackPanel4.Children.Add(new Border
			{
				Height = 1.0,
				Background = solidColorBrush,
				Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
			});
			System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(80.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(10.0)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(80.0)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			stackPanel4.Children.Add(grid);
			TextBlock element2 = new TextBlock
			{
				Text = "Отступ по высоте (мм):",
				Style = style2
			};
			System.Windows.Controls.Grid.SetColumn(element2, 0);
			System.Windows.Controls.Grid.SetRow(element2, 0);
			grid.Children.Add(element2);
			this._txtHeightOffset = new System.Windows.Controls.TextBox
			{
				Text = "150",
				Style = style3
			};
			System.Windows.Controls.Grid.SetColumn(this._txtHeightOffset, 1);
			System.Windows.Controls.Grid.SetRow(this._txtHeightOffset, 0);
			grid.Children.Add(this._txtHeightOffset);
			TextBlock element3 = new TextBlock
			{
				Text = "Подъём → подключение (мм):",
				Style = style2
			};
			System.Windows.Controls.Grid.SetColumn(element3, 3);
			System.Windows.Controls.Grid.SetRow(element3, 0);
			grid.Children.Add(element3);
			this._txtRiseToConnection = new System.Windows.Controls.TextBox
			{
				Text = "200",
				Style = style3
			};
			System.Windows.Controls.Grid.SetColumn(this._txtRiseToConnection, 4);
			System.Windows.Controls.Grid.SetRow(this._txtRiseToConnection, 0);
			grid.Children.Add(this._txtRiseToConnection);
			stackPanel4.Children.Add(new TextBlock
			{
				Text = "Труба 1 (Подключаемая) — ответвление, от которой строится подключение. Труба 2 (Магистраль) — труба, к которой подключаемся.",
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(130, 130, 155)),
				FontSize = 11.0,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
			});
			Border border3 = new Border
			{
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 28, 33)),
				BorderBrush = solidColorBrush,
				BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
				Padding = new Thickness(20.0, 12.0, 20.0, 12.0)
			};
			stackPanel.Children.Add(border3);
			StackPanel stackPanel7 = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right
			};
			border3.Child = stackPanel7;
			this._btnCancel = new Button
			{
				Content = "Отмена",
				Foreground = Brushes.White,
				Background = cancelNormal,
				BorderThickness = new Thickness(0.0),
				Width = 120.0,
				Height = 34.0,
				Cursor = Cursors.Hand,
				Style = roundedButtonStyle,
				Margin = new Thickness(0.0, 0.0, 10.0, 0.0)
			};
			this._btnCancel.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				this._btnCancel.Background = cancelHover;
			};
			this._btnCancel.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				this._btnCancel.Background = cancelNormal;
			};
			this._btnCancel.Click += delegate(object s, RoutedEventArgs e)
			{
				this.DialogResult = new bool?(false);
				this.Close();
			};
			stackPanel7.Children.Add(this._btnCancel);
			this._btnOk = new Button
			{
				Content = "Подключить",
				Foreground = Brushes.White,
				Background = accentBrush,
				BorderThickness = new Thickness(0.0),
				Width = 140.0,
				Height = 34.0,
				Cursor = Cursors.Hand,
				Style = roundedButtonStyle
			};
			this._btnOk.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				this._btnOk.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 20, 50));
			};
			this._btnOk.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				this._btnOk.Background = accentBrush;
			};
			this._btnOk.Click += delegate(object s, RoutedEventArgs e)
			{
				double num;
				if (!double.TryParse(this._txtHeightOffset.Text, out num) || num < 0.0)
				{
					MessageBox.Show("Введите корректное значение отступа по высоте (мм).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}
				double num2;
				if (!double.TryParse(this._txtRiseToConnection.Text, out num2) || num2 < 0.0)
				{
					MessageBox.Show("Введите корректное значение отступа подъём→подключение (мм).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Exclamation);
					return;
				}
				this.DialogResult = new bool?(true);
				this.Close();
			};
			stackPanel7.Children.Add(this._btnOk);
		}
		private string GetPipeInfo(Pipe pipe)
		{
			string result;
			try
			{
				int num = (int)Math.Round(pipe.Diameter * 304.8);
				Parameter expr_23 = pipe.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
				string text = ((expr_23 != null) ? expr_23.AsString() : null) ?? "";
				result = string.Format("DN{0}", num) + (string.IsNullOrEmpty(text) ? "" : (" [" + text + "]"));
			}
			catch
			{
				result = "труба";
			}
			return result;
		}
	}
}
