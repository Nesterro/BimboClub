using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Form = System.Windows.Forms.Form;
using Button = System.Windows.Forms.Button;
using CheckBox = System.Windows.Forms.CheckBox;
using ComboBox = System.Windows.Forms.ComboBox;
using GroupBox = System.Windows.Forms.GroupBox;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;
using Point = System.Drawing.Point;
using Control = System.Windows.Forms.Control;

namespace BimboClub
{
    public class FrmCreateSystemsViews : Form
    {
        private UIDocument _uidoc;
        private Document _doc;
        private List<MEPSystem> _systems;
        private List<View3D> _views3D;
        private List<ViewSchedule> _schedules;
        private List<FamilySymbol> _titleBlocks;
        private List<string> _parameters;

        private TabControl tabMain;
        private TabPage tpSystems;
        private TabPage tpSettings;

        // Systems Tab Controls
        private DataGridView dgvSystems;
        private DataGridViewCheckBoxColumn colCheck;
        private DataGridViewTextBoxColumn colName;
        private DataGridViewTextBoxColumn colCount;
        private Button btnCreateViews;
        private Button btnCreateSchedules;
        private Button btnCreateSheets;
        private Button btnCancel;

        // Settings Tab Controls
        private GroupBox grpViews;
        private ComboBox cmbViewTemplate;
        private TextBox txtViewPrefix;
        private TextBox txtFilterPrefix;
        private ComboBox cmbSysParam;

        private GroupBox grpSchedules;
        private ComboBox cmbScheduleTemplate;
        private TextBox txtSchedulePrefix;

        private GroupBox grpSheets;
        private ComboBox cmbTitleBlock;
        private RadioButton rdoAlbum;
        private RadioButton rdoPortrait;
        private CheckBox chkActivateSheet;

        public FrmCreateSystemsViews(UIDocument uidoc, List<MEPSystem> systems, List<View3D> views3D, List<ViewSchedule> schedules, List<FamilySymbol> titleBlocks, List<string> parameters)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;
            _systems = systems;
            _views3D = views3D;
            _schedules = schedules;
            _titleBlocks = titleBlocks;
            _parameters = parameters;

            InitializeComponent();
            PopulateControls();
        }

        private void PopulateControls()
        {
            // Populate Systems Grid
            foreach (var sys in _systems)
            {
                dgvSystems.Rows.Add(true, sys.Name, sys.Elements.Size);
            }

            // Populate View Templates
            cmbViewTemplate.DataSource = _views3D;
            cmbViewTemplate.DisplayMember = "Name";

            // Populate Schedule Templates
            cmbScheduleTemplate.DataSource = _schedules;
            cmbScheduleTemplate.DisplayMember = "Name";

            // Populate Title Blocks
            cmbTitleBlock.DataSource = _titleBlocks;
            cmbTitleBlock.DisplayMember = "Name";

            // Populate Parameters list
            cmbSysParam.DataSource = _parameters;
            if (_parameters.Contains("ADSK_Имя системы"))
                cmbSysParam.SelectedItem = "ADSK_Имя системы";
            else if (_parameters.Contains("Имя системы"))
                cmbSysParam.SelectedItem = "Имя системы";
        }

        private void InitializeComponent()
        {
            this.tabMain = new TabControl();
            this.tpSystems = new TabPage();
            this.tpSettings = new TabPage();
            
            this.dgvSystems = new DataGridView();
            this.colCheck = new DataGridViewCheckBoxColumn();
            this.colName = new DataGridViewTextBoxColumn();
            this.colCount = new DataGridViewTextBoxColumn();
            
            this.btnCreateViews = new Button();
            this.btnCreateSchedules = new Button();
            this.btnCreateSheets = new Button();
            this.btnCancel = new Button();

            this.grpViews = new GroupBox();
            this.cmbViewTemplate = new ComboBox();
            this.txtViewPrefix = new TextBox();
            this.txtFilterPrefix = new TextBox();
            this.cmbSysParam = new ComboBox();

            this.grpSchedules = new GroupBox();
            this.cmbScheduleTemplate = new ComboBox();
            this.txtSchedulePrefix = new TextBox();

            this.grpSheets = new GroupBox();
            this.cmbTitleBlock = new ComboBox();
            this.rdoAlbum = new RadioButton();
            this.rdoPortrait = new RadioButton();
            this.chkActivateSheet = new CheckBox();

            this.tabMain.SuspendLayout();
            this.tpSystems.SuspendLayout();
            this.tpSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvSystems)).BeginInit();
            this.grpViews.SuspendLayout();
            this.grpSchedules.SuspendLayout();
            this.grpSheets.SuspendLayout();
            this.SuspendLayout();

            // tabMain
            this.tabMain.Controls.Add(this.tpSystems);
            this.tabMain.Controls.Add(this.tpSettings);
            this.tabMain.Dock = DockStyle.Fill;
            this.tabMain.Location = new Point(0, 0);
            this.tabMain.Size = new Size(580, 520);

            // tpSystems
            this.tpSystems.Controls.Add(this.dgvSystems);
            this.tpSystems.Controls.Add(this.btnCreateViews);
            this.tpSystems.Controls.Add(this.btnCreateSchedules);
            this.tpSystems.Controls.Add(this.btnCreateSheets);
            this.tpSystems.Controls.Add(this.btnCancel);
            this.tpSystems.Text = "Системы";
            this.tpSystems.UseVisualStyleBackColor = true;

            // dgvSystems
            this.dgvSystems.AllowUserToAddRows = false;
            this.dgvSystems.AllowUserToDeleteRows = false;
            this.dgvSystems.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvSystems.Columns.AddRange(new DataGridViewColumn[] { this.colCheck, this.colName, this.colCount });
            this.dgvSystems.Location = new Point(10, 10);
            this.dgvSystems.Size = new Size(550, 410);
            
            // colCheck
            this.colCheck.HeaderText = "Выбор";
            this.colCheck.Width = 60;
            
            // colName
            this.colName.HeaderText = "Имя системы";
            this.colName.ReadOnly = true;
            this.colName.Width = 320;
            
            // colCount
            this.colCount.HeaderText = "Элементов";
            this.colCount.ReadOnly = true;
            this.colCount.Width = 100;

            // Buttons placement
            this.btnCancel.Location = new Point(10, 435);
            this.btnCancel.Size = new Size(100, 35);
            this.btnCancel.Text = "Отмена";
            this.btnCancel.Click += (s, e) => this.Close();

            this.btnCreateViews.Location = new Point(230, 435);
            this.btnCreateViews.Size = new Size(100, 35);
            this.btnCreateViews.Text = "Создать виды";
            this.btnCreateViews.Click += btnCreateViews_Click;

            this.btnCreateSchedules.Location = new Point(340, 435);
            this.btnCreateSchedules.Size = new Size(110, 35);
            this.btnCreateSchedules.Text = "Создать специф.";
            this.btnCreateSchedules.Click += btnCreateSchedules_Click;

            this.btnCreateSheets.Location = new Point(460, 435);
            this.btnCreateSheets.Size = new Size(100, 35);
            this.btnCreateSheets.Text = "Создать листы";
            this.btnCreateSheets.Click += btnCreateSheets_Click;

            // tpSettings
            this.tpSettings.Controls.Add(this.grpViews);
            this.tpSettings.Controls.Add(this.grpSchedules);
            this.tpSettings.Controls.Add(this.grpSheets);
            this.tpSettings.Text = "Настройки";
            this.tpSettings.UseVisualStyleBackColor = true;

            // grpViews Settings
            this.grpViews.Location = new Point(10, 10);
            this.grpViews.Size = new Size(550, 170);
            this.grpViews.Text = "3D Виды и Фильтры";
            
            Label lblTemplate = new Label() { Text = "Шаблонный 3D вид:", Location = new Point(15, 25), Size = new Size(150, 20) };
            this.cmbViewTemplate.Location = new Point(175, 22);
            this.cmbViewTemplate.Size = new Size(350, 21);
            this.cmbViewTemplate.DropDownStyle = ComboBoxStyle.DropDownList;

            Label lblViewPrefix = new Label() { Text = "Префикс имени вида:", Location = new Point(15, 60), Size = new Size(150, 20) };
            this.txtViewPrefix.Location = new Point(175, 57);
            this.txtViewPrefix.Size = new Size(350, 20);
            this.txtViewPrefix.Text = "3D_";

            Label lblFilterPrefix = new Label() { Text = "Префикс имени фильтра:", Location = new Point(15, 95), Size = new Size(150, 20) };
            this.txtFilterPrefix.Location = new Point(175, 92);
            this.txtFilterPrefix.Size = new Size(350, 20);
            this.txtFilterPrefix.Text = "Ф_";

            Label lblSysParam = new Label() { Text = "Параметр имени системы:", Location = new Point(15, 130), Size = new Size(150, 20) };
            this.cmbSysParam.Location = new Point(175, 127);
            this.cmbSysParam.Size = new Size(350, 21);
            this.cmbSysParam.DropDownStyle = ComboBoxStyle.DropDownList;

            this.grpViews.Controls.AddRange(new Control[] { lblTemplate, this.cmbViewTemplate, lblViewPrefix, this.txtViewPrefix, lblFilterPrefix, this.txtFilterPrefix, lblSysParam, this.cmbSysParam });

            // grpSchedules Settings
            this.grpSchedules.Location = new Point(10, 190);
            this.grpSchedules.Size = new Size(550, 100);
            this.grpSchedules.Text = "Спецификации";
            
            Label lblSchedTemplate = new Label() { Text = "Шаблон спецификации:", Location = new Point(15, 25), Size = new Size(150, 20) };
            this.cmbScheduleTemplate.Location = new Point(175, 22);
            this.cmbScheduleTemplate.Size = new Size(350, 21);
            this.cmbScheduleTemplate.DropDownStyle = ComboBoxStyle.DropDownList;

            Label lblSchedPrefix = new Label() { Text = "Префикс спецификации:", Location = new Point(15, 60), Size = new Size(150, 20) };
            this.txtSchedulePrefix.Location = new Point(175, 57);
            this.txtSchedulePrefix.Size = new Size(350, 20);
            this.txtSchedulePrefix.Text = "Спецификация_";

            this.grpSchedules.Controls.AddRange(new Control[] { lblSchedTemplate, this.cmbScheduleTemplate, lblSchedPrefix, this.txtSchedulePrefix });

            // grpSheets Settings
            this.grpSheets.Location = new Point(10, 300);
            this.grpSheets.Size = new Size(550, 160);
            this.grpSheets.Text = "Листы";

            Label lblTitleBlock = new Label() { Text = "Основная надпись:", Location = new Point(15, 25), Size = new Size(150, 20) };
            this.cmbTitleBlock.Location = new Point(175, 22);
            this.cmbTitleBlock.Size = new Size(350, 21);
            this.cmbTitleBlock.DropDownStyle = ComboBoxStyle.DropDownList;

            this.rdoAlbum.Location = new Point(20, 65);
            this.rdoAlbum.Size = new Size(150, 20);
            this.rdoAlbum.Text = "Альбомная";
            this.rdoAlbum.Checked = true;

            this.rdoPortrait.Location = new Point(180, 65);
            this.rdoPortrait.Size = new Size(150, 20);
            this.rdoPortrait.Text = "Книжная";

            this.chkActivateSheet.Location = new Point(20, 100);
            this.chkActivateSheet.Size = new Size(300, 20);
            this.chkActivateSheet.Text = "Активировать лист после создания";
            this.chkActivateSheet.Checked = true;

            this.grpSheets.Controls.AddRange(new Control[] { lblTitleBlock, this.cmbTitleBlock, this.rdoAlbum, this.rdoPortrait, this.chkActivateSheet });

            // Form
            this.ClientSize = new Size(580, 520);
            this.Controls.Add(this.tabMain);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Text = "Создание видов, спецификаций и листов для систем";

            this.tabMain.ResumeLayout(false);
            this.tpSystems.ResumeLayout(false);
            this.tpSettings.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvSystems)).EndInit();
            this.grpViews.ResumeLayout(false);
            this.grpViews.PerformLayout();
            this.grpSchedules.ResumeLayout(false);
            this.grpSchedules.PerformLayout();
            this.grpSheets.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        private List<MEPSystem> GetCheckedSystems()
        {
            List<MEPSystem> list = new List<MEPSystem>();
            for (int i = 0; i < dgvSystems.Rows.Count; i++)
            {
                bool isChecked = (bool)dgvSystems.Rows[i].Cells[0].Value;
                if (isChecked)
                {
                    string name = (string)dgvSystems.Rows[i].Cells[1].Value;
                    MEPSystem sys = _systems.FirstOrDefault(s => s.Name == name);
                    if (sys != null)
                    {
                        list.Add(sys);
                    }
                }
            }
            return list;
        }

        private void btnCreateViews_Click(object sender, EventArgs e)
        {
            var selectedSystems = GetCheckedSystems();
            if (selectedSystems.Count == 0)
            {
                MessageBox.Show("Не выбрано ни одной системы.");
                return;
            }

            View3D templateView = cmbViewTemplate.SelectedItem as View3D;
            if (templateView == null)
            {
                MessageBox.Show("Не выбран шаблонный 3D вид в настройках.");
                return;
            }

            string paramName = cmbSysParam.SelectedItem as string;
            if (string.IsNullOrEmpty(paramName))
            {
                MessageBox.Show("Не выбран параметр имени системы.");
                return;
            }

            string prefix = txtViewPrefix.Text;
            string filterPrefix = txtFilterPrefix.Text;

            int count = 0;
            using (Transaction t = new Transaction(_doc, "Создать 3D виды для систем"))
            {
                t.Start();
                foreach (var sys in selectedSystems)
                {
                    try
                    {
                        string name = prefix + sys.Name;
                        if (templateView.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                        {
                            ElementId newId = templateView.Duplicate(ViewDuplicateOption.Duplicate);
                            View3D newView = _doc.GetElement(newId) as View3D;
                            newView.Name = GetUniqueViewName(name);

                            // Создаем фильтр для изоляции системы
                            var catIds = GetMepCategoryIds(_doc);
                            var filter = CreateFilterForSystem(_doc, sys, paramName, filterPrefix + sys.Name, catIds);
                            if (filter != null)
                            {
                                newView.AddFilter(filter.Id);
                                newView.SetFilterVisibility(filter.Id, false); // скрыть все кроме нашей системы
                            }
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Не удалось создать вид для " + sys.Name, ex);
                    }
                }
                t.Commit();
            }

            MessageBox.Show($"Успешно создано 3D видов: {count}.");
            this.Close();
        }

        private void btnCreateSchedules_Click(object sender, EventArgs e)
        {
            var selectedSystems = GetCheckedSystems();
            if (selectedSystems.Count == 0)
            {
                MessageBox.Show("Не выбрано ни одной системы.");
                return;
            }

            ViewSchedule templateSchedule = cmbScheduleTemplate.SelectedItem as ViewSchedule;
            if (templateSchedule == null)
            {
                MessageBox.Show("Не выбран шаблон спецификации.");
                return;
            }

            string paramName = cmbSysParam.SelectedItem as string;
            if (string.IsNullOrEmpty(paramName))
            {
                MessageBox.Show("Не выбран параметр имени системы.");
                return;
            }

            string prefix = txtSchedulePrefix.Text;
            int count = 0;

            using (Transaction t = new Transaction(_doc, "Создать спецификации для систем"))
            {
                t.Start();
                foreach (var sys in selectedSystems)
                {
                    try
                    {
                        string name = prefix + sys.Name;
                        ElementId newId = templateSchedule.Duplicate(ViewDuplicateOption.Duplicate);
                        ViewSchedule newSched = _doc.GetElement(newId) as ViewSchedule;
                        newSched.Name = GetUniqueScheduleName(name);

                        // Находим поле по параметру
                        ScheduleField field = FindScheduleField(newSched, paramName);
                        if (field != null)
                        {
                            ScheduleFilter filter = new ScheduleFilter(field.FieldId, ScheduleFilterType.Equal, sys.Name);
                            newSched.Definition.AddFilter(filter);
                        }
                        count++;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Не удалось создать спецификацию для " + sys.Name, ex);
                    }
                }
                t.Commit();
            }

            MessageBox.Show($"Успешно создано спецификаций: {count}.");
            this.Close();
        }

        private void btnCreateSheets_Click(object sender, EventArgs e)
        {
            var selectedSystems = GetCheckedSystems();
            if (selectedSystems.Count == 0)
            {
                MessageBox.Show("Не выбрано ни одной системы.");
                return;
            }

            View3D templateView = cmbViewTemplate.SelectedItem as View3D;
            ViewSchedule templateSchedule = cmbScheduleTemplate.SelectedItem as ViewSchedule;
            FamilySymbol titleBlock = cmbTitleBlock.SelectedItem as FamilySymbol;

            if (templateView == null || templateSchedule == null || titleBlock == null)
            {
                MessageBox.Show("Для создания листов необходимо выбрать шаблон вида, спецификации и основную надпись в настройках.");
                return;
            }

            string paramName = cmbSysParam.SelectedItem as string;
            string viewPrefix = txtViewPrefix.Text;
            string filterPrefix = txtFilterPrefix.Text;
            string schedPrefix = txtSchedulePrefix.Text;

            int count = 0;
            ViewSheet lastCreatedSheet = null;

            using (TransactionGroup tg = new TransactionGroup(_doc, "Создание комплекта листов систем"))
            {
                tg.Start();

                foreach (var sys in selectedSystems)
                {
                    View3D newView = null;
                    ViewSchedule newSched = null;
                    ViewSheet newSheet = null;

                    using (Transaction t = new Transaction(_doc, "Создание листа и элементов для " + sys.Name))
                    {
                        t.Start();
                        try
                        {
                            // 1. Создать 3D вид
                            if (templateView.CanViewBeDuplicated(ViewDuplicateOption.Duplicate))
                            {
                                ElementId viewId = templateView.Duplicate(ViewDuplicateOption.Duplicate);
                                newView = _doc.GetElement(viewId) as View3D;
                                newView.Name = GetUniqueViewName(viewPrefix + sys.Name);
                                var catIds = GetMepCategoryIds(_doc);
                                var filter = CreateFilterForSystem(_doc, sys, paramName, filterPrefix + sys.Name, catIds);
                                if (filter != null)
                                {
                                    newView.AddFilter(filter.Id);
                                    newView.SetFilterVisibility(filter.Id, false);
                                }
                            }

                            // 2. Создать спецификацию
                            ElementId schedId = templateSchedule.Duplicate(ViewDuplicateOption.Duplicate);
                            newSched = _doc.GetElement(schedId) as ViewSchedule;
                            newSched.Name = GetUniqueScheduleName(schedPrefix + sys.Name);
                            ScheduleField field = FindScheduleField(newSched, paramName);
                            if (field != null)
                            {
                                ScheduleFilter filter = new ScheduleFilter(field.FieldId, ScheduleFilterType.Equal, sys.Name);
                                newSched.Definition.AddFilter(filter);
                            }

                            // 3. Создать лист
                            newSheet = ViewSheet.Create(_doc, titleBlock.Id);
                            newSheet.Name = sys.Name;
                            lastCreatedSheet = newSheet;

                            // Задать параметры рамки
                            Element tbInstance = new FilteredElementCollector(_doc, newSheet.Id)
                                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                                .FirstOrDefault();

                            if (tbInstance != null)
                            {
                                // Формат и ориентация
                                SafeSetParameter(tbInstance, "ADSK_Формат", rdoAlbum.Checked ? "A3" : "A4");
                                SafeSetParameter(tbInstance, "ADSK_Ориентация", rdoAlbum.Checked ? "Альбомная" : "Книжная");
                            }

                            t.Commit();
                        }
                        catch (Exception ex)
                        {
                            t.RollBack();
                            Logger.LogError("Ошибка при создании листа для " + sys.Name, ex);
                            continue;
                        }
                    }

                    // 4. Размещение на листе
                    if (newSheet != null && newView != null && newSched != null)
                    {
                        using (Transaction t2 = new Transaction(_doc, "Размещение на листе " + sys.Name))
                        {
                            t2.Start();
                            try
                            {
                                // Размещаем вид в центре листа
                                Viewport vp = Viewport.Create(_doc, newSheet.Id, newView.Id, new XYZ(1.5, 1.0, 0.0));
                                
                                // Размещаем спецификацию в верхнем левом углу
                                ScheduleSheetInstance.Create(_doc, newSheet.Id, newSched.Id, new XYZ(0.1, 1.8, 0.0));

                                count++;
                                t2.Commit();
                            }
                            catch (Exception ex)
                            {
                                t2.RollBack();
                                Logger.LogError("Ошибка при размещении видов на листе " + sys.Name, ex);
                            }
                        }
                    }
                }

                tg.Assimilate();
            }

            if (chkActivateSheet.Checked && lastCreatedSheet != null)
            {
                _uidoc.ActiveView = lastCreatedSheet;
            }

            MessageBox.Show($"Успешно создано комплектов листов: {count}.");
            this.Close();
        }

        private string GetUniqueViewName(string baseName)
        {
            string name = baseName;
            int idx = 1;
            while (new FilteredElementCollector(_doc).OfClass(typeof(View3D)).Cast<View3D>().Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                name = baseName + " " + idx;
                idx++;
            }
            return name;
        }

        private string GetUniqueScheduleName(string baseName)
        {
            string name = baseName;
            int idx = 1;
            while (new FilteredElementCollector(_doc).OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>().Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                name = baseName + " " + idx;
                idx++;
            }
            return name;
        }

        private List<ElementId> GetMepCategoryIds(Document doc)
        {
            var categories = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_DuctAccessory,
                BuiltInCategory.OST_DuctTerminal,
                BuiltInCategory.OST_DuctInsulations,
                BuiltInCategory.OST_DuctLinings,
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_PipeAccessory,
                BuiltInCategory.OST_PipeInsulations,
                BuiltInCategory.OST_FlexPipeCurves,
                BuiltInCategory.OST_FlexDuctCurves,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_CableTrayFitting,
                BuiltInCategory.OST_Conduit,
                BuiltInCategory.OST_ConduitFitting,
                BuiltInCategory.OST_MechanicalEquipment,
                BuiltInCategory.OST_PlumbingFixtures,
                BuiltInCategory.OST_SpecialityEquipment,
                BuiltInCategory.OST_Sprinklers
            };

            List<ElementId> ids = new List<ElementId>();
            foreach (var cat in categories)
            {
                try
                {
                    Category c = Category.GetCategory(doc, cat);
                    if (c != null) ids.Add(c.Id);
                }
                catch { }
            }
            return ids;
        }

        private ParameterFilterElement CreateFilterForSystem(Document doc, MEPSystem system, string paramName, string filterName, List<ElementId> catIds)
        {
            try
            {
                var existingFilters = new FilteredElementCollector(doc)
                    .OfClass(typeof(ParameterFilterElement))
                    .Cast<ParameterFilterElement>()
                    .FirstOrDefault(f => f.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase));
                if (existingFilters != null)
                {
                    return existingFilters;
                }

                // Находим параметр у любого MEP элемента в системе
                Parameter testParam = null;
                foreach (Element el in system.Elements)
                {
                    testParam = el.LookupParameter(paramName);
                    if (testParam == null)
                    {
                        testParam = el.Parameters.Cast<Parameter>().FirstOrDefault(p => p.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
                    }
                    if (testParam != null) break;
                }

                if (testParam != null)
                {
                    ElementId paramId = testParam.Id;
                    FilterRule rule = ParameterFilterRuleFactory.CreateNotEqualsRule(paramId, system.Name, true);
                    ParameterFilterElement filter = ParameterFilterElement.Create(doc, filterName, catIds);
                    filter.SetElementFilter(new ElementParameterFilter(new List<FilterRule> { rule }));
                    return filter;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Ошибка создания фильтра для " + system.Name, ex);
            }
            return null;
        }

        private ScheduleField FindScheduleField(ViewSchedule schedule, string paramName)
        {
            var def = schedule.Definition;
            for (int i = 0; i < def.GetFieldCount(); i++)
            {
                ScheduleField field = def.GetField(i);
                if (field.GetName().Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return field;
                }
            }
            
            // Если поля нет, попробуем добавить
            foreach (SchedulableField sf in def.GetSchedulableFields())
            {
                if (sf.GetName(_doc).Equals(paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return def.AddField(sf);
                }
            }
            return null;
        }

        private void SafeSetParameter(Element el, string paramName, string value)
        {
            if (el == null || string.IsNullOrEmpty(paramName)) return;
            Parameter p = el.LookupParameter(paramName);
            if (p == null)
            {
                p = el.Parameters.Cast<Parameter>().FirstOrDefault(x => x.Definition.Name.Equals(paramName, StringComparison.OrdinalIgnoreCase));
            }
            if (p != null && !p.IsReadOnly)
            {
                p.Set(value);
            }
        }
    }
}
