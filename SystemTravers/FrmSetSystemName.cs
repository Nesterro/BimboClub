using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BimboClub
{
    public class FrmSetSystemName : Form
    {
        private ComboBox cmbParams;
        private ComboBox cmbApplyMode;
        private Button btnOk;
        private Button btnCancel;
        private GroupBox grpParam;
        private Label lblApplyMode;

        public string SelectedParameter => cmbParams.SelectedItem as string;
        public bool InFullDoc => cmbApplyMode.SelectedIndex == 0;

        public FrmSetSystemName(List<string> parameters)
        {
            InitializeComponent();
            
            cmbParams.DataSource = parameters;
            if (parameters.Contains("ADSK_Имя системы"))
            {
                cmbParams.SelectedItem = "ADSK_Имя системы";
            }
            else if (parameters.Contains("Имя системы"))
            {
                cmbParams.SelectedItem = "Имя системы";
            }

            cmbApplyMode.SelectedIndex = 0; // В рамках всего документа
        }

        private void InitializeComponent()
        {
            this.cmbParams = new ComboBox();
            this.cmbApplyMode = new ComboBox();
            this.btnOk = new Button();
            this.btnCancel = new Button();
            this.grpParam = new GroupBox();
            this.lblApplyMode = new Label();
            
            this.grpParam.SuspendLayout();
            this.SuspendLayout();

            // grpParam
            this.grpParam.Controls.Add(this.cmbParams);
            this.grpParam.Location = new Point(12, 12);
            this.grpParam.Size = new Size(320, 60);
            this.grpParam.Text = "Параметр для записи имени системы:";

            // cmbParams
            this.cmbParams.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbParams.FormattingEnabled = true;
            this.cmbParams.Location = new Point(10, 24);
            this.cmbParams.Size = new Size(300, 21);

            // lblApplyMode
            this.lblApplyMode.Location = new Point(12, 85);
            this.lblApplyMode.Size = new Size(130, 20);
            this.lblApplyMode.Text = "Область применения:";
            this.lblApplyMode.TextAlign = ContentAlignment.MiddleLeft;

            // cmbApplyMode
            this.cmbApplyMode.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbApplyMode.Items.AddRange(new object[] {
                "В рамках всего документа",
                "В рамках текущего вида"
            });
            this.cmbApplyMode.Location = new Point(145, 85);
            this.cmbApplyMode.Size = new Size(187, 21);

            // btnCancel
            this.btnCancel.DialogResult = DialogResult.Cancel;
            this.btnCancel.Location = new Point(160, 130);
            this.btnCancel.Size = new Size(80, 30);
            this.btnCancel.Text = "Отмена";
            this.btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // btnOk
            this.btnOk.Location = new Point(252, 130);
            this.btnOk.Size = new Size(80, 30);
            this.btnOk.Text = "Ок";
            this.btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };

            // Form
            this.AcceptButton = this.btnOk;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new Size(344, 175);
            this.Controls.Add(this.grpParam);
            this.Controls.Add(this.lblApplyMode);
            this.Controls.Add(this.cmbApplyMode);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Имя системы";
            
            this.grpParam.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
