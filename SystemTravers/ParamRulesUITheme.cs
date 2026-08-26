using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SAV.ParamRules
{
	/// <summary>
	/// Обеспечивает единое современное оформление (Soft Modern / Fluent Aesthetic) для всех форм Редактора Правил.
	/// </summary>
	internal static class ParamRulesUITheme
	{
		public static readonly Font MainFont = new Font("Segoe UI", 9f, FontStyle.Regular);
		public static readonly Font HeaderFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);
		public static readonly Font SmallFont = new Font("Segoe UI", 8.25f, FontStyle.Regular);
		public static readonly Font TitleFont = new Font("Segoe UI", 11f, FontStyle.Bold);

		public static readonly Color BgColor = Color.FromArgb(248, 250, 252);        // #F8FAFC
		public static readonly Color CardBg = Color.White;                           // #FFFFFF
		public static readonly Color BorderColor = Color.FromArgb(226, 232, 240);    // #E2E8F0
		public static readonly Color BorderFocused = Color.FromArgb(99, 102, 241);   // #6366F1
		public static readonly Color TextDark = Color.FromArgb(15, 23, 42);          // #0F172A
		public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);      // #64748B

		public static readonly Color PrimaryBg = Color.FromArgb(79, 70, 229);        // #4F46E5 (Indigo)
		public static readonly Color PrimaryHover = Color.FromArgb(67, 56, 202);     // #4338CA
		public static readonly Color PrimaryText = Color.White;

		public static readonly Color SecondaryBg = Color.FromArgb(241, 245, 249);    // #F1F5F9
		public static readonly Color SecondaryHover = Color.FromArgb(226, 232, 240); // #E2E8F0
		public static readonly Color SecondaryBorder = Color.FromArgb(203, 213, 225);// #CBD5E1
		public static readonly Color SecondaryText = Color.FromArgb(51, 65, 85);     // #334155

		public static readonly Color SuccessBg = Color.FromArgb(16, 185, 129);       // #10B981 (Emerald)
		public static readonly Color SuccessHover = Color.FromArgb(5, 150, 105);     // #059669

		public static readonly Color DangerBg = Color.FromArgb(239, 68, 68);         // #EF4444
		public static readonly Color DangerHover = Color.FromArgb(220, 38, 38);

		public static readonly Color TableHeaderBg = Color.FromArgb(241, 245, 249);  // #F1F5F9
		public static readonly Color TableHeaderFg = Color.FromArgb(51, 65, 85);     // #334155
		public static readonly Color TableAltRow = Color.FromArgb(248, 250, 252);    // #F8FAFC
		public static readonly Color TableSelected = Color.FromArgb(224, 231, 255);  // #E0E7FF
		public static readonly Color TableGrid = Color.FromArgb(226, 232, 240);      // #E2E8F0

		/// <summary>
		/// Применяет тему ко всей форме и её дочерним элементам.
		/// </summary>
		public static void ApplyTheme(Form form)
		{
			if (form == null) return;

			form.Font = MainFont;
			form.BackColor = BgColor;
			form.ForeColor = TextDark;

			ApplyToControl(form);
		}

		private static void ApplyToControl(Control ctrl)
		{
			if (ctrl == null) return;

			if (ctrl is Button btn)
			{
				StyleButton(btn);
			}
			else if (ctrl is DataGridView dgv)
			{
				StyleDataGridView(dgv);
			}
			else if (ctrl is ToolStrip ts)
			{
				StyleToolStrip(ts);
			}
			else if (ctrl is GroupBox gb)
			{
				gb.Font = HeaderFont;
				gb.ForeColor = TextDark;
				gb.BackColor = Color.Transparent;
			}
			else if (ctrl is TextBox tb)
			{
				tb.Font = MainFont;
				tb.BorderStyle = BorderStyle.FixedSingle;
			}
			else if (ctrl is ComboBox cb)
			{
				cb.Font = MainFont;
				cb.FlatStyle = FlatStyle.Flat;
			}
			else if (ctrl is Label lbl)
			{
				lbl.Font = MainFont;
				lbl.ForeColor = TextDark;
			}
			else if (ctrl is CheckBox chk)
			{
				chk.Font = MainFont;
				chk.ForeColor = TextDark;
			}
			else if (ctrl is RadioButton rb)
			{
				rb.Font = MainFont;
				rb.ForeColor = TextDark;
			}
			else if (ctrl is Panel pnl)
			{
				if (pnl.BackColor == SystemColors.Control)
				{
					pnl.BackColor = BgColor;
				}
			}

			// Рекурсивно для всех вложенных контролов
			foreach (Control child in ctrl.Controls)
			{
				ApplyToControl(child);
			}
		}

		public static void StyleButton(Button btn)
		{
			btn.Font = HeaderFont;
			btn.Cursor = Cursors.Hand;

			// Если кнопка только с иконкой (или фоновым изображением)
			if (btn.BackgroundImage != null || btn.Image != null || string.IsNullOrWhiteSpace(btn.Text))
			{
				btn.FlatStyle = FlatStyle.Flat;
				btn.FlatAppearance.BorderSize = 0;
				btn.BackColor = Color.Transparent;
				btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
				btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(203, 213, 225);
				return;
			}

			btn.FlatStyle = FlatStyle.Flat;
			btn.FlatAppearance.BorderSize = 1;

			string txt = (btn.Text ?? "").ToLower();

			if (txt.Contains("применить") || txt.Contains("сохранить") || txt.Contains("ок") || txt.Contains("добавить"))
			{
				btn.BackColor = PrimaryBg;
				btn.ForeColor = PrimaryText;
				btn.FlatAppearance.BorderColor = PrimaryBg;
				btn.FlatAppearance.MouseOverBackColor = PrimaryHover;
				btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 48, 163);
			}
			else if (txt.Contains("отмена") || txt.Contains("закрыть") || txt.Contains("выход"))
			{
				btn.BackColor = SecondaryBg;
				btn.ForeColor = SecondaryText;
				btn.FlatAppearance.BorderColor = SecondaryBorder;
				btn.FlatAppearance.MouseOverBackColor = SecondaryHover;
				btn.FlatAppearance.MouseDownBackColor = SecondaryBorder;
			}
			else if (txt.Contains("удалить"))
			{
				btn.BackColor = Color.FromArgb(254, 242, 242);
				btn.ForeColor = Color.FromArgb(185, 28, 28);
				btn.FlatAppearance.BorderColor = Color.FromArgb(254, 202, 202);
				btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(254, 226, 226);
			}
			else
			{
				btn.BackColor = SecondaryBg;
				btn.ForeColor = SecondaryText;
				btn.FlatAppearance.BorderColor = SecondaryBorder;
				btn.FlatAppearance.MouseOverBackColor = SecondaryHover;
				btn.FlatAppearance.MouseDownBackColor = SecondaryBorder;
			}
		}

		public static void StyleDataGridView(DataGridView dgv)
		{
			dgv.BackgroundColor = CardBg;
			dgv.BorderStyle = BorderStyle.None;
			dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
			dgv.GridColor = TableGrid;
			dgv.EnableHeadersVisualStyles = false;
			dgv.RowHeadersVisible = false;
			dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			dgv.MultiSelect = true;

			// Очистка коричневого цвета в колонках-кнопках
			foreach (DataGridViewColumn col in dgv.Columns)
			{
				if (col is DataGridViewImageColumn imgCol)
				{
					imgCol.DefaultCellStyle.BackColor = CardBg;
					imgCol.DefaultCellStyle.SelectionBackColor = TableSelected;
					imgCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
					imgCol.DefaultCellStyle.NullValue = null;
				}
			}

			dgv.CellFormatting += (s, e) =>
			{
				if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && e.ColumnIndex < dgv.Columns.Count)
				{
					if (dgv.Columns[e.ColumnIndex] is DataGridViewImageColumn)
					{
						if (dgv.Rows[e.RowIndex].Selected)
						{
							e.CellStyle.SelectionBackColor = TableSelected;
						}
						else
						{
							e.CellStyle.BackColor = (e.RowIndex % 2 == 1) ? TableAltRow : CardBg;
						}
					}
				}
			};

			// Заголовки колонок
			dgv.ColumnHeadersHeight = 32;
			dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
			dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
			dgv.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = TableHeaderBg,
				ForeColor = TableHeaderFg,
				Font = HeaderFont,
				Alignment = DataGridViewContentAlignment.MiddleLeft,
				Padding = new Padding(6, 4, 6, 4)
			};

			// Строки по умолчанию
			dgv.RowTemplate.Height = 28;
			dgv.DefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = CardBg,
				ForeColor = TextDark,
				Font = MainFont,
				SelectionBackColor = TableSelected,
				SelectionForeColor = TextDark,
				Padding = new Padding(4, 2, 4, 2)
			};

			// Чередующиеся строки
			dgv.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
			{
				BackColor = TableAltRow,
				ForeColor = TextDark,
				Font = MainFont,
				SelectionBackColor = TableSelected,
				SelectionForeColor = TextDark,
				Padding = new Padding(4, 2, 4, 2)
			};
		}

		public static void StyleToolStrip(ToolStrip ts)
		{
			ts.Font = MainFont;
			ts.BackColor = CardBg;
			ts.ForeColor = TextDark;
			ts.GripStyle = ToolStripGripStyle.Hidden;
			ts.RenderMode = ToolStripRenderMode.Professional;
			ts.Renderer = new ModernToolStripRenderer();
		}

		/// <summary>
		/// Современный чистый рендерер без ретро-градиентов.
		/// </summary>
		private class ModernToolStripRenderer : ToolStripProfessionalRenderer
		{
			public ModernToolStripRenderer() : base(new ModernColorTable())
			{
				this.RoundedEdges = true;
			}

			protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
			{
				using (Pen pen = new Pen(BorderColor, 1))
				{
					e.Graphics.DrawLine(pen, 0, e.ToolStrip.Height - 1, e.ToolStrip.Width, e.ToolStrip.Height - 1);
				}
			}

			protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
			{
				if (e.Item.Selected || e.Item.Pressed)
				{
					Rectangle rc = new Rectangle(2, 2, e.Item.Width - 4, e.Item.Height - 4);
					using (SolidBrush brush = new SolidBrush(SecondaryHover))
					{
						e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
						e.Graphics.FillRectangle(brush, rc);
					}
				}
			}
		}

		private class ModernColorTable : ProfessionalColorTable
		{
			public override Color ToolStripGradientBegin => CardBg;
			public override Color ToolStripGradientMiddle => CardBg;
			public override Color ToolStripGradientEnd => CardBg;
			public override Color ToolStripBorder => BorderColor;
			public override Color MenuBorder => BorderColor;
			public override Color MenuItemBorder => Color.Transparent;
			public override Color MenuItemSelected => SecondaryHover;
			public override Color MenuItemSelectedGradientBegin => SecondaryHover;
			public override Color MenuItemSelectedGradientEnd => SecondaryHover;
			public override Color MenuItemPressedGradientBegin => SecondaryBorder;
			public override Color MenuItemPressedGradientEnd => SecondaryBorder;
			public override Color ToolStripDropDownBackground => CardBg;
			public override Color ImageMarginGradientBegin => CardBg;
			public override Color ImageMarginGradientMiddle => CardBg;
			public override Color ImageMarginGradientEnd => CardBg;
			public override Color SeparatorDark => BorderColor;
			public override Color SeparatorLight => Color.Transparent;
		}
	}
}
