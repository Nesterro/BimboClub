using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimboClub
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ParamRulesCommand : IExternalCommand
    {
        private static bool _resolverRegistered = false;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RegisterAssemblyResolver();

            try
            {
                string ltoolsDllPath = GetLToolsDllPath();
                if (!File.Exists(ltoolsDllPath))
                {
                    Autodesk.Revit.UI.TaskDialog.Show("BimboClub | Ошибка", $"Файл LTools.dll не найден по пути:\n{ltoolsDllPath}\n\nУбедитесь, что LTools установлен.");
                    return Result.Failed;
                }

                Assembly ltoolsAsm = Assembly.LoadFrom(ltoolsDllPath);
                Type rulerType = ltoolsAsm.GetType("SAV.ParamRules.FrmRuler");

                if (rulerType == null)
                {
                    Autodesk.Revit.UI.TaskDialog.Show("BimboClub | Ошибка", "Не удалось найти тип SAV.ParamRules.FrmRuler в LTools.dll.");
                    return Result.Failed;
                }

                // Launch native LTools FrmRuler Form directly inside Revit
                System.Windows.Forms.Form rulerForm = (System.Windows.Forms.Form)Activator.CreateInstance(rulerType);
                IWin32Window revitWindow = new RevitWindowHandler(commandData.Application.MainWindowHandle);
                rulerForm.ShowDialog(revitWindow);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                Autodesk.Revit.UI.TaskDialog.Show("BimboClub | Ошибка LTools", $"Ошибка при запуске редактора правил LTools:\n{ex.ToString()}");
                return Result.Failed;
            }
        }

        private static void RegisterAssemblyResolver()
        {
            if (_resolverRegistered) return;
            _resolverRegistered = true;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    AssemblyName asmName = new AssemblyName(args.Name);
                    string ltoolsDir = Path.GetDirectoryName(GetLToolsDllPath());

                    if (!string.IsNullOrEmpty(ltoolsDir))
                    {
                        string candidatePath = Path.Combine(ltoolsDir, asmName.Name + ".dll");
                        if (File.Exists(candidatePath))
                        {
                            return Assembly.LoadFrom(candidatePath);
                        }
                    }
                }
                catch { }
                return null;
            };
        }

        private static string GetLToolsDllPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string userLTools = Path.Combine(appData, "Autodesk", "Revit", "Addins", "2024", "LTools", "LTools.dll");
            if (File.Exists(userLTools)) return userLTools;

            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string commonLTools = Path.Combine(programData, "Autodesk", "Revit", "Addins", "2024", "LTools", "LTools.dll");
            if (File.Exists(commonLTools)) return commonLTools;

            string asmLoc = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asmLoc))
            {
                string localLTools = Path.Combine(Path.GetDirectoryName(asmLoc), "LTools", "LTools.dll");
                if (File.Exists(localLTools)) return localLTools;
            }

            return @"C:\ProgramData\Autodesk\Revit\Addins\2024\LTools\LTools.dll";
        }
    }

    public class RevitWindowHandler : IWin32Window
    {
        public IntPtr Handle { get; }
        public RevitWindowHandler(IntPtr handle)
        {
            Handle = handle;
        }
    }
}
