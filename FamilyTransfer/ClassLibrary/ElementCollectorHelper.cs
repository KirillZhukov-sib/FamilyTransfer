using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace RevitToMySQL

{
    public static class ElementCollectorHelper
    {
        public static List<Element> GetPipeElements(Document doc)
        {
            return new FilteredElementCollector(doc)
                .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {

                    new ElementCategoryFilter(BuiltInCategory.OST_PipeFitting),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory),
                    new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeInsulations),
                }))
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent()
                .OrderBy(x => x.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsValueString() ?? "")
                .ToList();
        }         
        public static List<Element> GetDuctElements(Document doc)
        {
            return new FilteredElementCollector(doc)
                .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctTerminal),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory),
                    new ElementCategoryFilter(BuiltInCategory.OST_DuctInsulations),
                }))
                .WhereElementIsNotElementType()
                //.WhereElementIsViewIndependent()
                .ToList();
        }
    }
}
