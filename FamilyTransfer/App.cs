using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;

namespace FamilyTransfer
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class App : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
        public Result OnStartup(UIControlledApplication application)
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            application.CreateRibbonTab("SpecTransfer");
            RibbonPanel panel = application.CreateRibbonPanel("SpecTransfer", "Панелька");
            PushButtonData pbd = new PushButtonData("WriteFamSpec", "Выгрузка спеки", assemblyPath, "FamilyTransfer.Command");

            panel.AddItem(pbd);

            return Result.Succeeded;
        }
    }
}
