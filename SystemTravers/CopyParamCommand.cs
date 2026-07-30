using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Control = System.Windows.Forms.Control;

namespace BimboClub
{
	[Regeneration(RegenerationOption.Manual), Transaction(TransactionMode.Manual)]
	public class CopyParamCommand : IExternalCommand
	{
		public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
		{
			UIDocument activeUIDocument = commandData.Application.ActiveUIDocument;
			Document document = activeUIDocument.Document;
			Result result;
			try
			{
				// 1. Собираем системы воздуховодов и их параметры
				FilteredElementCollector ductSource = new FilteredElementCollector(document).OfClass(typeof(MechanicalSystem));
				List<MechanicalSystem> ductSystems = ductSource.Cast<MechanicalSystem>()
					.Where(s => s.SystemType == DuctSystemType.SupplyAir || s.SystemType == DuctSystemType.ReturnAir || s.SystemType == DuctSystemType.ExhaustAir || s.SystemType == DuctSystemType.OtherAir)
					.ToList();

				List<string> ductSourceParams = new List<string>();
				List<string> ductTargetParams = new List<string>();

				if (ductSystems.Count > 0)
				{
					foreach (Parameter parameter in ductSystems[0].Parameters)
					{
						if (!ductSourceParams.Contains(parameter.Definition.Name))
						{
							ductSourceParams.Add(parameter.Definition.Name);
						}
					}

					ElementId typeId = ductSystems[0].GetTypeId();
					if (typeId != ElementId.InvalidElementId)
					{
						Element typeElem = document.GetElement(typeId);
						if (typeElem != null)
						{
							foreach (Parameter parameter in typeElem.Parameters)
							{
								if (!ductSourceParams.Contains(parameter.Definition.Name))
								{
									ductSourceParams.Add(parameter.Definition.Name);
								}
							}
						}
					}
					ductSourceParams.Sort();

					foreach (MechanicalSystem sys in ductSystems)
					{
						if (sys.DuctNetwork.Size > 0)
						{
							foreach (Element el in sys.DuctNetwork)
							{
								foreach (Parameter p in el.Parameters)
								{
									if (!p.IsReadOnly && !ductTargetParams.Contains(p.Definition.Name))
									{
										ductTargetParams.Add(p.Definition.Name);
									}
								}
							}
							break;
						}
					}
					ductTargetParams.Sort();
				}

				// 2. Собираем системы трубопроводов и их параметры
				FilteredElementCollector pipeSource = new FilteredElementCollector(document).OfClass(typeof(PipingSystem));
				List<PipingSystem> pipeSystems = pipeSource.Cast<PipingSystem>().ToList();

				List<string> pipeSourceParams = new List<string>();
				List<string> pipeTargetParams = new List<string>();

				if (pipeSystems.Count > 0)
				{
					foreach (Parameter parameter in pipeSystems[0].Parameters)
					{
						if (!pipeSourceParams.Contains(parameter.Definition.Name))
						{
							pipeSourceParams.Add(parameter.Definition.Name);
						}
					}

					ElementId typeId = pipeSystems[0].GetTypeId();
					if (typeId != ElementId.InvalidElementId)
					{
						Element typeElem = document.GetElement(typeId);
						if (typeElem != null)
						{
							foreach (Parameter parameter in typeElem.Parameters)
							{
								if (!pipeSourceParams.Contains(parameter.Definition.Name))
								{
									pipeSourceParams.Add(parameter.Definition.Name);
								}
							}
						}
					}
					pipeSourceParams.Sort();

					foreach (PipingSystem sys in pipeSystems)
					{
						if (sys.PipingNetwork.Size > 0)
						{
							foreach (Element el in sys.PipingNetwork)
							{
								foreach (Parameter p in el.Parameters)
								{
									if (!p.IsReadOnly && !pipeTargetParams.Contains(p.Definition.Name))
									{
										pipeTargetParams.Add(p.Definition.Name);
									}
								}
							}
							break;
						}
					}
					pipeTargetParams.Sort();
				}

				// 3. Открываем форму
				using (ParamSelectionForm form = new ParamSelectionForm(ductSourceParams, ductTargetParams, pipeSourceParams, pipeTargetParams))
				{
					form.StartPosition = FormStartPosition.CenterScreen;
					if (form.ShowDialog() != DialogResult.OK)
					{
						result = Result.Cancelled;
						return result;
					}

					string sourceParameter = form.SourceParameter;
					string targetParameter = form.TargetParameter;
					int selectedTab = form.SelectedTab;

					if (string.IsNullOrEmpty(sourceParameter) || string.IsNullOrEmpty(targetParameter))
					{
						Autodesk.Revit.UI.TaskDialog.Show("Внимание", "Параметры не выбраны.");
						return Result.Cancelled;
					}

					int updatedCount = 0;
					int skippedCount = 0;

					using (Transaction transaction = new Transaction(document, "Копировать параметры систем"))
					{
						transaction.Start();

						if (selectedTab == 0) // Воздуховоды
						{
							foreach (MechanicalSystem current2 in ductSystems)
							{
								Parameter parameter3 = current2.LookupParameter(sourceParameter);
								if (parameter3 == null || !parameter3.HasValue)
								{
									ElementId typeId = current2.GetTypeId();
									if (typeId != ElementId.InvalidElementId)
									{
										Element typeElem = document.GetElement(typeId);
										if (typeElem != null)
										{
											parameter3 = typeElem.LookupParameter(sourceParameter);
										}
									}
								}

								if (parameter3 != null && parameter3.HasValue)
								{
									string parameterValueAsString = this.GetParameterValueAsString(parameter3);
									if (!string.IsNullOrEmpty(parameterValueAsString))
									{
										foreach (Element element2 in current2.DuctNetwork)
										{
											Parameter parameter4 = element2.LookupParameter(targetParameter);
											if (parameter4 == null || parameter4.IsReadOnly)
											{
												skippedCount++;
											}
											else
											{
												if (this.SetParameterValueFromString(parameter4, parameterValueAsString))
												{
													updatedCount++;
												}
												else
												{
													skippedCount++;
												}
											}
										}
									}
								}
							}
						}
						else // Трубопроводы
						{
							foreach (PipingSystem current2 in pipeSystems)
							{
								Parameter parameter3 = current2.LookupParameter(sourceParameter);
								if (parameter3 == null || !parameter3.HasValue)
								{
									ElementId typeId = current2.GetTypeId();
									if (typeId != ElementId.InvalidElementId)
									{
										Element typeElem = document.GetElement(typeId);
										if (typeElem != null)
										{
											parameter3 = typeElem.LookupParameter(sourceParameter);
										}
									}
								}

								if (parameter3 != null && parameter3.HasValue)
								{
									string parameterValueAsString = this.GetParameterValueAsString(parameter3);
									if (!string.IsNullOrEmpty(parameterValueAsString))
									{
										foreach (Element element2 in current2.PipingNetwork)
										{
											Parameter parameter4 = element2.LookupParameter(targetParameter);
											if (parameter4 == null || parameter4.IsReadOnly)
											{
												skippedCount++;
											}
											else
											{
												if (this.SetParameterValueFromString(parameter4, parameterValueAsString))
												{
													updatedCount++;
												}
												else
												{
													skippedCount++;
												}
											}
										}
									}
								}
							}
						}

						transaction.Commit();
					}

					Autodesk.Revit.UI.TaskDialog.Show("Готово", $"Успешно обновлено элементов: {updatedCount}\nПропущено: {skippedCount}");
				}
				result = Result.Succeeded;
			}
			catch (Exception ex)
			{
				message = ex.Message;
				result = Result.Failed;
			}
			return result;
		}

		private string GetParameterValueAsString(Parameter p)
		{
			if (p == null || !p.HasValue)
			{
				return string.Empty;
			}
			switch (p.StorageType)
			{
				case StorageType.Integer:
					return p.AsInteger().ToString();
				case StorageType.Double:
					return p.AsDouble().ToString();
				case StorageType.String:
					return p.AsString() ?? string.Empty;
				case StorageType.ElementId:
					#if REVIT2024 || REVIT2025 || !NET48
					return p.AsValueString() ?? p.AsElementId().Value.ToString();
					#else
					return p.AsValueString() ?? p.AsElementId().IntegerValue.ToString();
					#endif
				default:
					return p.AsValueString() ?? string.Empty;
			}
		}

		private bool SetParameterValueFromString(Parameter p, string value)
		{
			bool result;
			try
			{
				switch (p.StorageType)
				{
					case StorageType.Integer:
						{
							int value2;
							if (int.TryParse(value, out value2))
							{
								result = p.Set(value2);
							}
							else
							{
								result = false;
							}
							break;
						}
					case StorageType.Double:
						{
							double value3;
							if (double.TryParse(value, out value3))
							{
								result = p.Set(value3);
							}
							else
							{
								result = false;
							}
							break;
						}
					case StorageType.String:
						result = p.Set(value);
						break;
					default:
						result = p.SetValueString(value);
						break;
				}
			}
			catch
			{
				result = false;
			}
			return result;
		}
	}

	public class ParamSelectionForm : System.Windows.Forms.Form
	{
		private TabControl tabCtrl;
		private TabPage tpDucts;
		private TabPage tpPipes;

		private System.Windows.Forms.ComboBox cmbDuctSource;
		private System.Windows.Forms.ComboBox cmbDuctTarget;

		private System.Windows.Forms.ComboBox cmbPipeSource;
		private System.Windows.Forms.ComboBox cmbPipeTarget;

		private System.Windows.Forms.Button btnOk;
		private System.Windows.Forms.Button btnCancel;

		public int SelectedTab => tabCtrl.SelectedIndex;

		public string SourceParameter
		{
			get
			{
				if (SelectedTab == 0)
				{
					return cmbDuctSource.SelectedItem?.ToString();
				}
				else
				{
					return cmbPipeSource.SelectedItem?.ToString();
				}
			}
		}

		public string TargetParameter
		{
			get
			{
				if (SelectedTab == 0)
				{
					return cmbDuctTarget.SelectedItem?.ToString();
				}
				else
				{
					return cmbPipeTarget.SelectedItem?.ToString();
				}
			}
		}

		public ParamSelectionForm(List<string> ductSourceParams, List<string> ductTargetParams, List<string> pipeSourceParams, List<string> pipeTargetParams)
		{
			this.Text = "Копировать параметры систем - BimboClub Tools";
			base.Size = new Size(560, 380);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.StartPosition = FormStartPosition.CenterScreen;
			base.MaximizeBox = false;
			base.MinimizeBox = false;

			// Настройка темы оформления BimboClub (Темная тема)
			base.BackColor = System.Drawing.Color.FromArgb(24, 24, 28); // #18181C
			base.ForeColor = System.Drawing.Color.White;
			base.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Regular);

			// Инициализация вкладок
			this.tabCtrl = new TabControl();
			this.tabCtrl.Left = 15;
			this.tabCtrl.Top = 15;
			this.tabCtrl.Width = 515;
			this.tabCtrl.Height = 250;

			// ------------------ Tab 1: Ducts ------------------
			this.tpDucts = new TabPage("Системы воздуховодов");
			this.tpDucts.BackColor = System.Drawing.Color.FromArgb(32, 32, 38);
			this.tpDucts.ForeColor = System.Drawing.Color.White;

			System.Windows.Forms.Label lblDuctSource = new System.Windows.Forms.Label();
			lblDuctSource.Text = "Параметр СИСТЕМЫ (источник):";
			lblDuctSource.Left = 20;
			lblDuctSource.Top = 20;
			lblDuctSource.Width = 475;
			lblDuctSource.Height = 25;

			this.cmbDuctSource = new System.Windows.Forms.ComboBox();
			this.cmbDuctSource.Left = 20;
			this.cmbDuctSource.Top = 50;
			this.cmbDuctSource.Width = 470;
			this.cmbDuctSource.DropDownStyle = ComboBoxStyle.DropDownList;
			this.cmbDuctSource.BackColor = System.Drawing.Color.FromArgb(48, 48, 56);
			this.cmbDuctSource.ForeColor = System.Drawing.Color.White;
			this.cmbDuctSource.FlatStyle = FlatStyle.Flat;
			this.cmbDuctSource.Items.AddRange(ductSourceParams.ToArray());
			if (this.cmbDuctSource.Items.Count > 0) this.cmbDuctSource.SelectedIndex = 0;

			System.Windows.Forms.Label lblDuctTarget = new System.Windows.Forms.Label();
			lblDuctTarget.Text = "Параметр ЭЛЕМЕНТА (приемник):";
			lblDuctTarget.Left = 20;
			lblDuctTarget.Top = 110;
			lblDuctTarget.Width = 475;
			lblDuctTarget.Height = 25;

			this.cmbDuctTarget = new System.Windows.Forms.ComboBox();
			this.cmbDuctTarget.Left = 20;
			this.cmbDuctTarget.Top = 140;
			this.cmbDuctTarget.Width = 470;
			this.cmbDuctTarget.DropDownStyle = ComboBoxStyle.DropDownList;
			this.cmbDuctTarget.BackColor = System.Drawing.Color.FromArgb(48, 48, 56);
			this.cmbDuctTarget.ForeColor = System.Drawing.Color.White;
			this.cmbDuctTarget.FlatStyle = FlatStyle.Flat;
			this.cmbDuctTarget.Items.AddRange(ductTargetParams.ToArray());
			if (this.cmbDuctTarget.Items.Count > 0) this.cmbDuctTarget.SelectedIndex = 0;

			this.tpDucts.Controls.AddRange(new Control[] { lblDuctSource, this.cmbDuctSource, lblDuctTarget, this.cmbDuctTarget });

			// ------------------ Tab 2: Pipes ------------------
			this.tpPipes = new TabPage("Системы трубопроводов");
			this.tpPipes.BackColor = System.Drawing.Color.FromArgb(32, 32, 38);
			this.tpPipes.ForeColor = System.Drawing.Color.White;

			System.Windows.Forms.Label lblPipeSource = new System.Windows.Forms.Label();
			lblPipeSource.Text = "Параметр СИСТЕМЫ (источник):";
			lblPipeSource.Left = 20;
			lblPipeSource.Top = 20;
			lblPipeSource.Width = 475;
			lblPipeSource.Height = 25;

			this.cmbPipeSource = new System.Windows.Forms.ComboBox();
			this.cmbPipeSource.Left = 20;
			this.cmbPipeSource.Top = 50;
			this.cmbPipeSource.Width = 470;
			this.cmbPipeSource.DropDownStyle = ComboBoxStyle.DropDownList;
			this.cmbPipeSource.BackColor = System.Drawing.Color.FromArgb(48, 48, 56);
			this.cmbPipeSource.ForeColor = System.Drawing.Color.White;
			this.cmbPipeSource.FlatStyle = FlatStyle.Flat;
			this.cmbPipeSource.Items.AddRange(pipeSourceParams.ToArray());
			if (this.cmbPipeSource.Items.Count > 0) this.cmbPipeSource.SelectedIndex = 0;

			System.Windows.Forms.Label lblPipeTarget = new System.Windows.Forms.Label();
			lblPipeTarget.Text = "Параметр ЭЛЕМЕНТА (приемник):";
			lblPipeTarget.Left = 20;
			lblPipeTarget.Top = 110;
			lblPipeTarget.Width = 475;
			lblPipeTarget.Height = 25;

			this.cmbPipeTarget = new System.Windows.Forms.ComboBox();
			this.cmbPipeTarget.Left = 20;
			this.cmbPipeTarget.Top = 140;
			this.cmbPipeTarget.Width = 470;
			this.cmbPipeTarget.DropDownStyle = ComboBoxStyle.DropDownList;
			this.cmbPipeTarget.BackColor = System.Drawing.Color.FromArgb(48, 48, 56);
			this.cmbPipeTarget.ForeColor = System.Drawing.Color.White;
			this.cmbPipeTarget.FlatStyle = FlatStyle.Flat;
			this.cmbPipeTarget.Items.AddRange(pipeTargetParams.ToArray());
			if (this.cmbPipeTarget.Items.Count > 0) this.cmbPipeTarget.SelectedIndex = 0;

			this.tpPipes.Controls.AddRange(new Control[] { lblPipeSource, this.cmbPipeSource, lblPipeTarget, this.cmbPipeTarget });

			this.tabCtrl.Controls.AddRange(new TabPage[] { this.tpDucts, this.tpPipes });

			// Кнопки формы
			this.btnOk = new System.Windows.Forms.Button();
			this.btnOk.Text = "OK";
			this.btnOk.Left = 310;
			this.btnOk.Top = 285;
			this.btnOk.Width = 110;
			this.btnOk.Height = 35;
			this.btnOk.DialogResult = DialogResult.OK;
			this.btnOk.BackColor = System.Drawing.Color.FromArgb(179, 14, 45); // #B30E2D
			this.btnOk.ForeColor = System.Drawing.Color.White;
			this.btnOk.FlatStyle = FlatStyle.Flat;
			this.btnOk.FlatAppearance.BorderSize = 0;
			this.btnOk.Cursor = System.Windows.Forms.Cursors.Hand;
			this.btnOk.Font = new System.Drawing.Font("Segoe UI", 9.75f, System.Drawing.FontStyle.Bold);

			this.btnCancel = new System.Windows.Forms.Button();
			this.btnCancel.Text = "Отмена";
			this.btnCancel.Left = 430;
			this.btnCancel.Top = 285;
			this.btnCancel.Width = 110;
			this.btnCancel.Height = 35;
			this.btnCancel.DialogResult = DialogResult.Cancel;
			this.btnCancel.BackColor = System.Drawing.Color.FromArgb(58, 58, 66); // #3A3A42
			this.btnCancel.ForeColor = System.Drawing.Color.White;
			this.btnCancel.FlatStyle = FlatStyle.Flat;
			this.btnCancel.FlatAppearance.BorderSize = 0;
			this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;

			base.Controls.AddRange(new Control[] { this.tabCtrl, this.btnOk, this.btnCancel });
			base.AcceptButton = this.btnOk;
			base.CancelButton = this.btnCancel;
		}
	}
}
