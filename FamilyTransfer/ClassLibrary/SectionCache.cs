using Autodesk.Revit.DB;
using FamilyTransfer;
using FamilyTransfer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitToMySQL
{
    public class SectionCache
    {
        private readonly List<SectionInfo> _sectionBounds;
        private readonly (string section, string part) _parkingDefault;
        private readonly List<SectionItem> _allSections;

        public class SectionInfo
        {
            public string Section { get; set; }
            public string Part { get; set; }
            public BoundingBoxXYZ Bounds { get; set; }
        }
        public SectionCache(Document doc)
        {
            _sectionBounds = new List<SectionInfo>();
            _allSections = new List<SectionItem>();  
            _parkingDefault = ("-", "-");
            
            if (doc == null) return;

            try
            {
                RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .FirstOrDefault(link => Regex.IsMatch(link.Name, @"^\d{4}-\d\.rvt"));

                if (linkInstance == null) return;

                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) return;

                List<Element> allForms = new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Mass)
                    .WhereElementIsNotElementType()
                    .ToList();

                if (allForms.Count == 0) return;

                List<Element> civilForms = new List<Element>();
                Element parkingForm = null;

                foreach (Element form in allForms)
                {
                    Parameter bdsParam = form.LookupParameter(ParametersInfo.Part);
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

                foreach (Element civil in civilForms)
                {
                    string section = civil.LookupParameter(ParametersInfo.Section)?.AsString() ?? "-";
                    string part = civil.LookupParameter(ParametersInfo.Part)?.AsString() ?? "-";
                    BoundingBoxXYZ bounds = civil.get_BoundingBox(null);

                    
                    if (bounds != null)
                    {
                        _sectionBounds.Add(new SectionInfo
                        {
                            Section = section,
                            Part = part,
                            Bounds = bounds
                        });

                        _allSections.Add(new SectionItem
                        {
                            Name = section,
                            Part = part,
                            IsSelected = true 
                        });
                    }
                }

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

                            string section = civilForms[i].LookupParameter(ParametersInfo.Section)?.AsString() ?? "-";
                            string part = civilForms[i].LookupParameter(ParametersInfo.Part)?.AsString() ?? "-";

                            _sectionBounds.Add(new SectionInfo
                            {
                                Section = section,
                                Part = part,
                                Bounds = bounds 
                            });

                            if (!_allSections.Any(s => s.Name == section && s.Part == part))
                            {
                                _allSections.Add(new SectionItem
                                {
                                    Name = section,
                                    Part = part,
                                    IsSelected = true
                                });
                            }
                        }

                        _parkingDefault = (
                            parkingForm.LookupParameter(ParametersInfo.Section)?.AsString() ?? "-",
                            parkingForm.LookupParameter(ParametersInfo.Part)?.AsString() ?? "-"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка инициализации SectionCache: {ex.Message}");
            }
        }

        // ========== ДОБАВЛЯЕМ МЕТОД GetAllSections ==========
        public List<SectionItem> GetAllSections()
        {
            return _allSections;
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

