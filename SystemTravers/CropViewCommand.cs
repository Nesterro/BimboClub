using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.Exceptions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace BimboClub
{
	[Transaction(TransactionMode.Manual)]
	public class CropViewCommand : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			UIApplication application = commandData.Application;
			UIDocument activeUIDocument = application.ActiveUIDocument;
			Document document = activeUIDocument.Document;
			View activeView = document.ActiveView;
			if (activeView == null)
			{
				message = "Нет активного вида.";
				return Result.Failed;
			}
			if (activeView is ViewSheet || activeView is ViewSchedule || activeView.ViewType == ViewType.DraftingView)
			{
				TaskDialog.Show("Внимание", "Подрезка не поддерживается для данного типа вида.");
				return Result.Failed;
			}
			Result result;
			try
			{
				IEnumerable<ElementId> arg_67_0 = activeUIDocument.Selection.GetElementIds();
				List<Element> list = new List<Element>();
				foreach (ElementId current in arg_67_0)
				{
					Element element = document.GetElement(current);
					if (element != null)
					{
						list.Add(element);
					}
				}
				if (list.Count == 0)
				{
					try
					{
						foreach (Reference current2 in activeUIDocument.Selection.PickObjects(ObjectType.Element, "Выберите элементы для подрезки вида и нажмите 'Готово' в левом верхнем углу"))
						{
							Element element2 = document.GetElement(current2);
							if (element2 != null)
							{
								list.Add(element2);
							}
						}
					}
					catch (Autodesk.Revit.Exceptions.OperationCanceledException)
					{
						result = Result.Cancelled;
						return result;
					}
				}
				List<Element> list2 = new List<Element>();
				foreach (Element current3 in list)
				{
					if (current3.get_BoundingBox(null) != null)
					{
						list2.Add(current3);
					}
				}
				if (list2.Count == 0)
				{
					TaskDialog.Show("Внимание", "Ни один из выбранных элементов не имеет трехмерных габаритов.");
					result = Result.Failed;
				}
				else
				{
					CropOffsetWindow cropOffsetWindow = new CropOffsetWindow();
					new WindowInteropHelper(cropOffsetWindow).Owner = application.MainWindowHandle;
					cropOffsetWindow.ShowDialog();
					if (!cropOffsetWindow.IsSuccess)
					{
						result = Result.Cancelled;
					}
					else
					{
						double num = cropOffsetWindow.OffsetMm / 304.8;
						double num2 = 1.7976931348623157E+308;
						double num3 = -1.7976931348623157E+308;
						double num4 = 1.7976931348623157E+308;
						double num5 = -1.7976931348623157E+308;
						double num6 = 1.7976931348623157E+308;
						double num7 = -1.7976931348623157E+308;
						using (Transaction transaction = new Transaction(document, "Подрезка вида"))
						{
							transaction.Start();
							View3D view3D = activeView as View3D;
							if (view3D != null)
							{
								using (List<Element>.Enumerator enumerator3 = list2.GetEnumerator())
								{
									while (enumerator3.MoveNext())
									{
										BoundingBoxXYZ boundingBoxXYZ = enumerator3.Current.get_BoundingBox(null);
										if (boundingBoxXYZ != null)
										{
											num2 = Math.Min(num2, boundingBoxXYZ.Min.X);
											num3 = Math.Max(num3, boundingBoxXYZ.Max.X);
											num4 = Math.Min(num4, boundingBoxXYZ.Min.Y);
											num5 = Math.Max(num5, boundingBoxXYZ.Max.Y);
											num6 = Math.Min(num6, boundingBoxXYZ.Min.Z);
											num7 = Math.Max(num7, boundingBoxXYZ.Max.Z);
										}
									}
								}
								view3D.SetSectionBox(new BoundingBoxXYZ
								{
									Min = new XYZ(num2 - num, num4 - num, num6 - num),
									Max = new XYZ(num3 + num, num5 + num, num7 + num)
								});
								view3D.IsSectionBoxActive = true;
							}
							else
							{
								BoundingBoxXYZ cropBox = activeView.CropBox;
								Autodesk.Revit.DB.Transform transform = cropBox.Transform;
								Autodesk.Revit.DB.Transform inverse = transform.Inverse;
								using (List<Element>.Enumerator enumerator3 = list2.GetEnumerator())
								{
									while (enumerator3.MoveNext())
									{
										BoundingBoxXYZ boundingBoxXYZ2 = enumerator3.Current.get_BoundingBox(null);
										if (boundingBoxXYZ2 != null)
										{
											XYZ[] array = new XYZ[]
											{
												new XYZ(boundingBoxXYZ2.Min.X, boundingBoxXYZ2.Min.Y, boundingBoxXYZ2.Min.Z),
												new XYZ(boundingBoxXYZ2.Max.X, boundingBoxXYZ2.Min.Y, boundingBoxXYZ2.Min.Z),
												new XYZ(boundingBoxXYZ2.Min.X, boundingBoxXYZ2.Max.Y, boundingBoxXYZ2.Min.Z),
												new XYZ(boundingBoxXYZ2.Max.X, boundingBoxXYZ2.Max.Y, boundingBoxXYZ2.Min.Z),
												new XYZ(boundingBoxXYZ2.Min.X, boundingBoxXYZ2.Min.Y, boundingBoxXYZ2.Max.Z),
												new XYZ(boundingBoxXYZ2.Max.X, boundingBoxXYZ2.Min.Y, boundingBoxXYZ2.Max.Z),
												new XYZ(boundingBoxXYZ2.Min.X, boundingBoxXYZ2.Max.Y, boundingBoxXYZ2.Max.Z),
												new XYZ(boundingBoxXYZ2.Max.X, boundingBoxXYZ2.Max.Y, boundingBoxXYZ2.Max.Z)
											};
											for (int i = 0; i < array.Length; i++)
											{
												XYZ point = array[i];
												XYZ xYZ = inverse.OfPoint(point);
												num2 = Math.Min(num2, xYZ.X);
												num3 = Math.Max(num3, xYZ.X);
												num4 = Math.Min(num4, xYZ.Y);
												num5 = Math.Max(num5, xYZ.Y);
											}
										}
									}
								}
								BoundingBoxXYZ boundingBoxXYZ3 = new BoundingBoxXYZ();
								boundingBoxXYZ3.Transform = transform;
								boundingBoxXYZ3.Min = new XYZ(num2 - num, num4 - num, cropBox.Min.Z);
								boundingBoxXYZ3.Max = new XYZ(num3 + num, num5 + num, cropBox.Max.Z);
								activeView.CropBoxActive = true;
								activeView.CropBox = boundingBoxXYZ3;
								activeView.CropBoxVisible = true;
							}
							transaction.Commit();
						}
						result = Result.Succeeded;
					}
				}
			}
			catch (Exception ex)
			{
				message = ex.Message;
				Logger.LogError("Ошибка при подрезке вида", ex);
				result = Result.Failed;
			}
			return result;
		}
	}
	public class CropOffsetWindow : Window
	{

		private System.Windows.Controls.TextBox _txtOffset;
		public double OffsetMm
		{
			get;
			private set;
		}
		public bool IsSuccess
		{
			get;
			private set;
		}
		public CropOffsetWindow()
		{
			this.OffsetMm = 500.0;
			base.Title = "Область подрезки";
			base.Width = 320.0;
			base.Height = 180.0;
			base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			base.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 34));
			base.Foreground = Brushes.White;
			base.ResizeMode = ResizeMode.NoResize;
			base.WindowStyle = WindowStyle.ToolWindow;
			base.FontFamily = new FontFamily("Segoe UI");
			try
			{
				string text = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon32.png");
				if (File.Exists(text))
				{
					base.Icon = new BitmapImage(new Uri(text));
				}
			}
			catch
			{
			}
			System.Windows.Controls.Grid grid = new System.Windows.Controls.Grid();
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = new GridLength(1.0, GridUnitType.Star)
			});
			grid.RowDefinitions.Add(new RowDefinition
			{
				Height = GridLength.Auto
			});
			grid.Margin = new Thickness(15.0);
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Vertical
			};
			Label element = new Label
			{
				Content = "Отступ от границ элементов (мм):",
				Foreground = Brushes.LightGray,
				FontSize = 12.0,
				Margin = new Thickness(0.0, 0.0, 0.0, 5.0)
			};
			stackPanel.Children.Add(element);
			this._txtOffset = new System.Windows.Controls.TextBox
			{
				Text = "500",
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 53)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 102)),
				CaretBrush = Brushes.White,
				FontSize = 13.0,
				Padding = new Thickness(6.0, 6.0, 6.0, 6.0),
				Height = 32.0,
				VerticalContentAlignment = VerticalAlignment.Center
			};
			this._txtOffset.PreviewTextInput += (s, ev) =>
			{
				int val;
				ev.Handled = !int.TryParse(ev.Text, out val);
			};
			stackPanel.Children.Add(this._txtOffset);
			System.Windows.Controls.Grid.SetRow(stackPanel, 0);
			grid.Children.Add(stackPanel);
			System.Windows.Controls.Grid grid2 = new System.Windows.Controls.Grid
			{
				Margin = new Thickness(0.0, 15.0, 0.0, 0.0)
			};
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
			Button btnOk = new Button
			{
				Content = "Обрезать",
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45)),
				Foreground = Brushes.White,
				BorderThickness = new Thickness(0.0),
				Height = 28.0,
				Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
				Cursor = Cursors.Hand,
				IsDefault = true
			};
			btnOk.Click += new RoutedEventHandler(this.BtnOk_Click);
			btnOk.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				btnOk.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 20, 50));
			};
			btnOk.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				btnOk.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(179, 14, 45));
			};
			System.Windows.Controls.Grid.SetColumn(btnOk, 0);
			grid2.Children.Add(btnOk);
			Button btnCancel = new Button
			{
				Content = "Отмена",
				Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 80)),
				Foreground = Brushes.White,
				BorderThickness = new Thickness(0.0),
				Height = 28.0,
				Margin = new Thickness(5.0, 0.0, 0.0, 0.0),
				Cursor = Cursors.Hand,
				IsCancel = true
			};
			btnCancel.Click += delegate(object s, RoutedEventArgs e)
			{
				base.Close();
			};
			btnCancel.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				btnCancel.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 102));
			};
			btnCancel.MouseLeave += delegate(object s, MouseEventArgs e)
			{
				btnCancel.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 80));
			};
			System.Windows.Controls.Grid.SetColumn(btnCancel, 1);
			grid2.Children.Add(btnCancel);
			System.Windows.Controls.Grid.SetRow(grid2, 1);
			grid.Children.Add(grid2);
			base.Content = grid;
		}
		private void BtnOk_Click(object sender, RoutedEventArgs e)
		{
			double num;
			if (double.TryParse(this._txtOffset.Text, out num) && num >= 0.0)
			{
				this.OffsetMm = num;
				this.IsSuccess = true;
				base.Close();
				return;
			}
			MessageBox.Show("Пожалуйста, введите корректное положительное число.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}
}
