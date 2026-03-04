using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace RevitToMySQL
{
    public class SectionCache
    {
        private readonly List<SectionInfo> _sectionBounds;
        private readonly (string section, string part) _parkingDefault;

        public class SectionInfo
        {
            public string Section { get; set; }
            public string Part { get; set; }
            public BoundingBoxXYZ Bounds { get; set; }
        }

        public SectionCache(Document doc)
        {
            _sectionBounds = new List<SectionInfo>();
            _parkingDefault = ("-", "-");

            if (doc == null) return;

            try
            {
                // Находим связанную модель КФ
                RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .FirstOrDefault(link => Regex.IsMatch(link.Name, @"^\d{4}-\d\.rvt"));

                if (linkInstance == null) return;

                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) return;

                // Получаем все формы одним проходом
                List<Element> allForms = new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Mass)
                    .WhereElementIsNotElementType()
                    .ToList();

                if (allForms.Count == 0) return;

                // Разделяем на жилье и паркинг
                List<Element> civilForms = new List<Element>();
                Element parkingForm = null;

                foreach (Element form in allForms)
                {
                    Parameter bdsParam = form.LookupParameter("BDS_Building");
                    string bdsValue = bdsParam?.AsString() ?? "";

                    if (Regex.IsMatch(bdsValue, @"[AaАа]"))
                    {
                        parkingForm = form;
                    }
                    else
                    {
                        civilForms.Add(form);
                    }
                }

                if (civilForms.Count == 0) return;

                // Добавляем жилые секции
                foreach (Element civil in civilForms)
                {
                    string section = civil.LookupParameter("BDS_Building")?.AsString() ?? "-";
                    string part = civil.LookupParameter("BDS_BuildingPart")?.AsString() ?? "-";
                    BoundingBoxXYZ bounds = civil.get_BoundingBox(null);

                    if (bounds != null)
                    {
                        _sectionBounds.Add(new SectionInfo
                        {
                            Section = section,
                            Part = part,
                            Bounds = bounds
                        });
                    }
                }

                // Обрабатываем паркинг, если есть
                if (parkingForm != null)
                {
                    BoundingBoxXYZ parkBounds = parkingForm.get_BoundingBox(null);
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

                            string section = civilForms[i].LookupParameter("BDS_Building")?.AsString() ?? "-";
                            string part = civilForms[i].LookupParameter("BDS_BuildingPart")?.AsString() ?? "-";

                            _sectionBounds.Add(new SectionInfo
                            {
                                Section = section,
                                Part = part,
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации SectionCache: {ex.Message}");
            }
        }

        public (string section, string part) GetSectionAndPartForElement(Element element)
        {
            if (element == null) return ("-", "-");

            try
            {
                BoundingBoxXYZ elemBounds = element.get_BoundingBox(null);
                if (elemBounds == null) return ("-", "-");

                foreach (var section in _sectionBounds)
                {
                    if (section.Bounds != null && Intersects(section.Bounds, elemBounds))
                        return (section.Section ?? "-", section.Part ?? "-");
                }

                if (!IsCivilElement(element) && _parkingDefault != default)
                    return _parkingDefault;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка GetSectionAndPartForElement: {ex.Message}");
            }

            return ("-", "-");
        }

        private static bool Intersects(BoundingBoxXYZ b1, BoundingBoxXYZ b2)
        {
            if (b1 == null || b2 == null) return false;

            return b1.Min.X <= b2.Max.X && b1.Max.X >= b2.Min.X &&
                   b1.Min.Y <= b2.Max.Y && b1.Max.Y >= b2.Min.Y &&
                   b1.Min.Z <= b2.Max.Z && b1.Max.Z >= b2.Min.Z;
        }

        private static bool IsCivilElement(Element elem)
        {
            if (elem == null) return false;

            Parameter param = elem.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param == null || !param.HasValue) return false;

            string value = param.AsString() ?? "";
            return Regex.IsMatch(value, @"^[TТ](14|24|15|25)$") || Regex.IsMatch(value, @"[аАaA]");
        }
    }
}