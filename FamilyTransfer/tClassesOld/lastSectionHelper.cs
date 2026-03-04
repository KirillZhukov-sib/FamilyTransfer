/*
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace RevitToMySQL
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class SectionCache
    {
        private readonly List<SectionInfo> _sectionBounds = new List<SectionInfo>();
        private readonly (string section, string part) _parkingDefault;

        public class SectionInfo
        {
            public string Section { get; set; }
            public string Part { get; set; }
            public BoundingBoxXYZ Bounds { get; set; }
        }

        public SectionCache(Document doc)
        {
            // Находим связанную модель
            var linkInstance = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(link => Regex.IsMatch(link.Name, @"^\d{4}-\d\.rvt"));

            if (linkInstance == null) return;

            var linkDoc = linkInstance.GetLinkDocument();
            if (linkDoc == null) return;

            // Получаем все формы одним проходом
            var allForms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Mass)
                .WhereElementIsNotElementType()
                .ToList();

            // Разделяем на жилье и паркинг
            var civilForms = allForms.Where(f =>
                !Regex.IsMatch(f.LookupParameter("BDS_Building")?.AsString() ?? "", @"[AaАа]")).ToList();

            var parkingForm = allForms.FirstOrDefault(f =>
                Regex.IsMatch(f.LookupParameter("BDS_Building")?.AsString() ?? "", @"[AaАа]"));

            // Строим кэш для жилых секций
            foreach (var form in civilForms)
            {
                _sectionBounds.Add(new SectionInfo
                {
                    Section = form.LookupParameter("BDS_Building")?.AsString() ?? "-",
                    Part = form.LookupParameter("BDS_BuildingPart")?.AsString() ?? "-",
                    Bounds = form.get_BoundingBox(null)
                });
            }

            // Если есть паркинг, режем его
            if (parkingForm != null && civilForms.Any())
            {
                var parkBounds = parkingForm.get_BoundingBox(null);
                if (parkBounds != null)
                {
                    double lengthX = (parkBounds.Max.X - parkBounds.Min.X) / civilForms.Count;

                    for (int i = 0; i < civilForms.Count; i++)
                    {
                        var bounds = new BoundingBoxXYZ
                        {
                            Min = new XYZ(parkBounds.Min.X + lengthX * i, parkBounds.Min.Y, parkBounds.Min.Z),
                            Max = new XYZ(parkBounds.Min.X + lengthX * (i + 1), parkBounds.Max.Y, parkBounds.Max.Z)
                        };

                        _sectionBounds.Add(new SectionInfo
                        {
                            Section = civilForms[i].LookupParameter("BDS_Building")?.AsString() ?? "-",
                            Part = civilForms[i].LookupParameter("BDS_BuildingPart")?.AsString() ?? "-",
                            Bounds = bounds
                        });
                    }

                    _parkingDefault = (
                        parkingForm.LookupParameter("BDS_Building")?.AsString() ?? "-",
                        parkingForm.LookupParameter("BDS_BuildingPart")?.AsString() ?? "-"
                    );
                }
            }
        }

        public (string section, string part) GetSectionForElement(Element element)
        {
            var elemBounds = element.get_BoundingBox(null);
            if (elemBounds == null) return ("-", "-");

            // Проверяем по кэшированным боксам
            foreach (var section in _sectionBounds)
            {
                if (Intersects(section.Bounds, elemBounds))
                    return (section.Section, section.Part);
            }

            // Если не нашли и элемент не жилой - возвращаем паркинг
            if (!IsCivilElement(element) && _parkingDefault != default)
                return _parkingDefault;

            return ("-", "-");
        }

        private static bool Intersects(BoundingBoxXYZ b1, BoundingBoxXYZ b2) =>
            b1.Min.X <= b2.Max.X && b1.Max.X >= b2.Min.X &&
            b1.Min.Y <= b2.Max.Y && b1.Max.Y >= b2.Min.Y &&
            b1.Min.Z <= b2.Max.Z && b1.Max.Z >= b2.Min.Z;

        private static bool IsCivilElement(Element elem)
        {
            var param = elem.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param == null) return false;

            string value = param.AsString() ?? "";
            return Regex.IsMatch(value, @"^[TТ](14|24|15|25)$") || Regex.IsMatch(value, @"[аАaA]");
        }
    }
}
*/