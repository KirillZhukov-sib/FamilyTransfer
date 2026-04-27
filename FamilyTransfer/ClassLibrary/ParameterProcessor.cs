using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RevitToMySQL
{
    public static class ParameterProcessor
    {
        public static List<FamilyInfo> GetParameters(List<Element> elements, Document doc,
                                                      DatabaseCache dbCache, SectionCache sectionCache)
        {
            if (elements == null || !elements.Any())
                return new List<FamilyInfo>();

            var result = new List<FamilyInfo>(elements.Count);
            try
            {
                foreach (var e in elements)
                {

                    Element familyType = GetFamilyType(e);

                    string familyName = GetFamilyName(e) ?? "Не указано";
                    string ucode = GetUcode(e, familyType) ?? "-";
                    string funcGroup = GetFuncGroup(e, familyType) ?? "-";
                    string funcType = GetFuncType(e,familyType) ?? "-";
                    string modification = GetModification(e) ?? "-";
                    string workPressure = GetWorkPressure(e, familyType) ?? "-";
                    string ductEI = GetDuctEI(e) ?? "-";
                    string systemName = GetSystemName(e) ?? "-";
                    string size = GetElementSize(e) ?? "-";
                    string amount = GetElementAmount(e) ?? "-";
                    var sectionInfo = sectionCache.GetSectionAndPartForElement(e);
                    int sortingCode = GetSortingCode(e, systemName, dbCache);
                    string order = MakeOrder(funcGroup, funcType, dbCache) ?? "-";

                    result.Add(new FamilyInfo
                    {
                        FamilyName = familyName,
                        Ucode = ucode,
                        FuncGroup = funcGroup,
                        FuncTypes = funcType,
                        Modification = modification,
                        WorkPressure = workPressure,
                        DuctEI = ductEI,
                        Order = order,
                        Size = size,
                        SystemName = systemName,
                        Amount = amount,
                        Section = sectionInfo.section ?? "-",
                        Part = sectionInfo.part ?? "-",
                        ID = e.Id.IntegerValue,
                        SortingCode = sortingCode
                    });

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обработки элемента : {ex.Message}");
                //System.Diagnostics.Debug.WriteLine($"Ошибка обработки элемента {e.Id}: {ex.Message}");
            }
            return result;
        }

        private static Element GetFamilyType(Element element)
        {
            if (element == null) return null;

            ElementId typeId = element.GetTypeId();
            if (typeId != null && typeId != ElementId.InvalidElementId)
            {
                return element.Document.GetElement(typeId);
            }
            return null;
        }

        private static string GetFamilyName(Element element)
        {
            if (element == null) return "Не указано";

            Parameter param = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM);
            if (param != null && param.HasValue)
            {
                return param.AsValueString() ?? "Не указано";
            }
            return "Не указано";
        }

        private static string GetUcode(Element e, Element familyType)
        {
            if (e == null)
                return "-";

            if (e is Pipe)
            {
                try
                {
                    Parameter segmentParam = e.get_Parameter(BuiltInParameter.RBS_PIPE_CLASS_PARAM);
                    if (segmentParam != null && segmentParam.HasValue)
                    {
                        string segmentValue = segmentParam.AsValueString();
                        if (!string.IsNullOrEmpty(segmentValue))
                        {
                            string[] partsbscode = segmentValue.Split('/');
                            if (partsbscode != null && partsbscode.Length > 0)
                            {
                                string bscode = partsbscode[partsbscode.Length - 1];
                                if (!string.IsNullOrEmpty(bscode))
                                {
                                    return bscode;
                                }
                            }
                        }
                    }

                    if (familyType != null)
                    {
                        Parameter param = familyType.LookupParameter("BS_Код изделия");
                        if (param != null && param.HasValue)
                        {
                            return param.AsString()?.Trim() ?? "-";
                        }
                    }

                    return "-";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка получения Ucode для трубы {e.Id}: {ex.Message}");
                    return "-";
                }
            }

            Parameter instanceParam = e.LookupParameter("BS_Код изделия");
            if (instanceParam != null && instanceParam.HasValue)
            {
                string value = instanceParam.AsString()?.Trim();
                if (!string.IsNullOrEmpty(value) && value != "-")
                    return value;
            }
            else
            {
                if (familyType != null)
                {
                    Parameter typeParam = familyType.LookupParameter("BS_Код изделия");
                    if (typeParam != null && typeParam.HasValue)
                    {
                        string value = typeParam.AsString()?.Trim();
                        if (!string.IsNullOrEmpty(value) && value != "-")
                            return value;
                    }
                }
            }
            return "-";
        }
             
        private static string GetFuncGroup(Element element, Element familyType)
        {
            if (element != null)
            {
                Parameter param = element.LookupParameter("MEP_Функциональная группа");
                if (param != null && param.HasValue)
                {
                    string value = param.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            if (familyType != null)
            {
                Parameter param = familyType.LookupParameter("MEP_Функциональная группа");
                if (param != null && param.HasValue)
                {
                    string value = param.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            return "-";
        }

        private static string GetFuncType(Element element, Element familyType)
        {
            if (element != null)
            {
                Parameter param = element.LookupParameter("MEP_Функциональный тип");
                if (param != null && param.HasValue)
                {
                    string value = param.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            if (familyType != null)
            {
                Parameter param = familyType.LookupParameter("MEP_Функциональный тип");
                if (param != null && param.HasValue)
                {
                    string value = param.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }
            return "-";
        }

        private static string GetModification(Element element)
        {
            if (element == null) return "-";

            Parameter param = element.LookupParameter("MEP_Модификация");
            if (param != null && param.HasValue)
            {
                return param.AsString()?.Trim() ?? "-";
            }
            return "-";
        }


        private static string GetSystemName(Element element)
        {
            if (element == null) return "-";

            Parameter param = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param != null && param.HasValue)
            {
                string value = param.AsString()?.Trim();

                if (!string.IsNullOrEmpty(value))
                {
                    string[] separators = { ".", "," };
                    string[] parts = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length > 0)
                    {
                        return parts[0].Trim();
                    }
                }
            }

            Parameter mepSystemParam = element.LookupParameter("MEP_Система");
            if (mepSystemParam != null && mepSystemParam.HasValue)
            {
                string value = mepSystemParam.AsString()?.Trim();

                if (!string.IsNullOrEmpty(value))
                {
                    string[] separators = { ".", "," };
                    string[] parts = value.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length > 0)
                    {
                        return parts[0].Trim();
                    }
                }
            }


            return "-";
        }
        /*
        private static string GetSystemName(Element element)
        {
            if (element == null) return "-";

            Parameter param = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param != null && param.HasValue)
            {
                return param.AsString()?.Trim() ?? "-";
            }
            return "-";
        }
        */
        private static string GetDuctEI(Element element)
        {
            if (element == null) return "-";

            Parameter param = element.LookupParameter("Огнестойкость");
            if (param != null && param.HasValue)
            {
                return param.AsValueString()?.Trim() ?? "-";
            }
            return "-";
        }

        private static string GetWorkPressure(Element element, Element familyType)
        {
            const string parameterName = "MEP_Давление номинальное (текст)";

            if (element != null)
            {
                Parameter instanceParam = element.LookupParameter(parameterName);
                if (instanceParam != null && instanceParam.HasValue)
                {
                    string value = instanceParam.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(value) && value != "-")
                        return value;
                }
            }

            if (familyType != null)
            {
                Parameter typeParam = familyType.LookupParameter(parameterName);
                if (typeParam != null && typeParam.HasValue)
                {
                    string value = typeParam.AsString()?.Trim();
                    if (!string.IsNullOrEmpty(value) && value != "-")
                        return value;
                }
            }

            return "-";
        }

        private static string GetElementSize(Element e)
        {
            if (e == null) return "-";

            try
            {

                if (e is Pipe)
                {
                    double outerDiam = e.LookupParameter("Внешний диаметр")?.AsDouble() is double outerInFeet
                        ? UnitUtils.ConvertFromInternalUnits(outerInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    double innerDiam = e.LookupParameter("Внутренний диаметр")?.AsDouble() is double innerInFeet
                        ? UnitUtils.ConvertFromInternalUnits(innerInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    var size = e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";

                    double wallThickness = (outerDiam - innerDiam) / 2;
                    //if (outerDiam > 60)
                    //{
                    //    return $"{outerDiam}x{wallThickness}";

                    //}
                    /* else*/
                    return $"{size}x{wallThickness}";
                }

                if (e is Duct)
                {
                    //круглые
                    Parameter diameterParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    if (diameterParam != null && diameterParam.HasValue && diameterParam.AsDouble() > 0)
                    {
                        return diameterParam.AsValueString() ?? "-";
                    }

                    //прямоугольные
                    Parameter heightParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
                    Parameter widthParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
                    if (heightParam != null && widthParam != null &&
                        heightParam.HasValue && widthParam.HasValue)
                    {
                        double heightInMM = UnitUtils.ConvertFromInternalUnits(heightParam.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
                        double widthInMM = UnitUtils.ConvertFromInternalUnits(widthParam.AsDouble(), DisplayUnitType.DUT_MILLIMETERS);
                        return $"{Math.Max(heightInMM, widthInMM)}x{Math.Min(heightInMM, widthInMM)}";
                    }
                    return "-";
                }

                if (e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctAccessory)
                {
                    Parameter widthParam = e.LookupParameter("MEP_Ширина 1");
                    Parameter heightParam = e.LookupParameter("MEP_Высота 1");

                    if (widthParam != null && heightParam != null)
                    {
                        double width = widthParam.AsDouble() is double widthInFeet
                            ? UnitUtils.ConvertFromInternalUnits(widthInFeet, DisplayUnitType.DUT_MILLIMETERS)
                            : 0;

                        double height = heightParam.AsDouble() is double heightInFeet
                            ? UnitUtils.ConvertFromInternalUnits(heightInFeet, DisplayUnitType.DUT_MILLIMETERS)
                            : 0;

                        return $"{Math.Max(width, height)}x{Math.Min(width, height)}";
                    }

                    Parameter diamParam = e.LookupParameter("MEP_Диаметр 1");

                    if (diamParam != null)
                    {
                        double diam = diamParam.AsDouble() is double diamInFeet
                            ? UnitUtils.ConvertFromInternalUnits(diamInFeet, DisplayUnitType.DUT_MILLIMETERS)
                            : 0;

                        return $"{diam}";
                    }
                    return "-";
                }

                if (e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctTerminal)
                {
                    double widthNom = e.LookupParameter("MEP_Ширина_Номинальная")?.AsDouble() is double widthNomInFeet
                        ? UnitUtils.ConvertFromInternalUnits(widthNomInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    double heightNom = e.LookupParameter("MEP_Высота_Номинальная")?.AsDouble() is double heightNomInFeet
                        ? UnitUtils.ConvertFromInternalUnits(heightNomInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(widthNom) && !double.IsNaN(heightNom))
                    {
                        return $"{Math.Max(widthNom, heightNom):F0}x{Math.Min(widthNom, heightNom):F0}";
                    }

                    double width = e.LookupParameter("MEP_Ширина 1")?.AsDouble() is double widthInFeet
                        ? UnitUtils.ConvertFromInternalUnits(widthInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    double height = e.LookupParameter("MEP_Высота 1")?.AsDouble() is double heightInFeet
                        ? UnitUtils.ConvertFromInternalUnits(heightInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(width) && !double.IsNaN(height))
                    {
                        return $"{Math.Max(width, height):F0}x{Math.Min(width, height):F0}";
                    }

                    double diam = e.LookupParameter("MEP_Диаметр 1")?.AsDouble() is double diamInFeet
                        ? UnitUtils.ConvertFromInternalUnits(diamInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(diam))
                    {
                        return $"{diam}";
                    }
                    return "-";
                }

                if (e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeAccessory)
                {
                    double diam = e.LookupParameter("MEP_Диаметр 1")?.AsDouble() is double diamInFeet
                        ? UnitUtils.ConvertFromInternalUnits(diamInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(diam))
                    {
                        return $"{diam:F0}";
                    }

                    double pipeSize = e.get_Parameter(BuiltInParameter.RBS_PIPE_SIZE_MAXIMUM)?.AsDouble() is double sizeInFeet
                        ? UnitUtils.ConvertFromInternalUnits(sizeInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(pipeSize))
                    {
                        return $"{pipeSize:F0}";
                    }

                    return "-";
                }

                if (e.Category.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
                {
                    double maxSize = e.LookupParameter("Максимальный диаметр")?.AsDouble() is double maxInFeet
                        ? UnitUtils.ConvertFromInternalUnits(maxInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    double minSize = e.LookupParameter("Минимальный диаметр")?.AsDouble() is double minInFeet
                        ? UnitUtils.ConvertFromInternalUnits(minInFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(maxSize) && !double.IsNaN(minSize))
                    {
                        //return $"{Math.Max(maxSize, minSize)} - {Math.Min(maxSize, minSize)}";
                        return $"{Math.Max(maxSize, minSize)}";
                    }
                    //НО появись тройники с разными диамтрами в стволе и проходе!!!
                    double diamLeft = e.LookupParameter("Диаметр_слева")?.AsDouble() is double diamLeftFeet
                       ? UnitUtils.ConvertFromInternalUnits(diamLeftFeet, DisplayUnitType.DUT_MILLIMETERS)
                       : double.NaN;

                    double diamRight = e.LookupParameter("Диаметр_справа")?.AsDouble() is double diamRightFeet
                      ? UnitUtils.ConvertFromInternalUnits(diamRightFeet, DisplayUnitType.DUT_MILLIMETERS)
                      : double.NaN;

                    if (!double.IsNaN(diamLeft) && !double.IsNaN(diamRight) && diamLeft == diamRight)
                    {
                        return $"{diamLeft}";
                    }
                    double diam2 = e.LookupParameter("MEP_Диаметр 2")?.AsDouble() is double diam2InFeet
                        ? UnitUtils.ConvertFromInternalUnits(diam2InFeet, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    if (!double.IsNaN(diam2))
                    {
                        return $"{diam2}";
                    }

                    return "-";
                }

                if (e is FamilyInstance)
                {
                    //Parameter param = e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
                    Parameter param = e.LookupParameter("MEP_Диаметр 1");
                    return param?.AsString() ?? "-";
                }

                if (e is DuctInsulation || e is PipeInsulation)
                {
                    double thicknessInMm = e.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_DUCT)?.AsDouble() is double thickness
                        ? UnitUtils.ConvertFromInternalUnits(thickness, DisplayUnitType.DUT_MILLIMETERS)
                        : double.NaN;

                    return !double.IsNaN(thicknessInMm) ? $"{thicknessInMm}" : "-";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения размера: {ex.Message}");
            }

            return "-";
        }

        private static string GetElementAmount(Element e)
        {
            if (e == null) return "-";

            try
            {
                if (e is Pipe || e is Duct)
                {
                    Parameter lengthParam = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                    if (lengthParam == null || !lengthParam.HasValue)
                        return "-";

                    double lengthInFeet = lengthParam.AsDouble();
                    if (Math.Abs(lengthInFeet) < 1e-6)
                        return "-";

                    double lengthInMeters = UnitUtils.ConvertFromInternalUnits(
                        lengthInFeet, DisplayUnitType.DUT_METERS);
                    return lengthInMeters.ToString("F2");
                }

                if (e is PipeInsulation || e is DuctInsulation)
                {
                    Parameter areaParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);

                    if (areaParam == null || !areaParam.HasValue)
                        return "-";

                    double areaInFeet = areaParam.AsDouble();

                    if (Math.Abs(areaInFeet) < 1e-6)
                        return "-";

                    double areaInSquareMeters = UnitUtils.ConvertFromInternalUnits(
                        areaInFeet, DisplayUnitType.DUT_SQUARE_METERS);
                    return areaInSquareMeters.ToString("F2");
                }

                return "1";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения количества: {ex.Message}");
                return "-";
            }
        }

        private static string MakeOrder(string funcGroup, string funcType, DatabaseCache dbCache)
        {
            if (dbCache == null) return "-";

            try
            {
                int? groupOrder = dbCache.GetFuncGroupOrder(funcGroup ?? "");
                int? typeOrder = dbCache.GetFuncTypeOrder(funcType ?? "");

                string groupOrderStr = groupOrder.HasValue ? groupOrder.Value.ToString("D2") : "00";
                string typeOrderStr = typeOrder.HasValue ? typeOrder.Value.ToString("D2") : "00";

                return $"{groupOrderStr}{typeOrderStr}00";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка создания порядка: {ex.Message}");
                return "-";
            }
        }

        private static int GetSortingCode(Element element, string systemName, DatabaseCache dbCache)
        {
            if (element == null || dbCache == null) return 0;

            try
            {
                Parameter ductParam = element.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM);
                string sysDuctName = ductParam?.AsValueString() ?? "";

                if (!string.IsNullOrEmpty(sysDuctName) && IsDuctSystem(sysDuctName))
                {
                    string processedName = SystemTransformer(sysDuctName);
                    int? code = dbCache.GetSortingCodeOV2(processedName);
                    if (code.HasValue)
                        return code.Value;
                }

                if (!string.IsNullOrEmpty(systemName) &&
                    (systemName.Contains("Т") || systemName.Contains("T")))
                {
                    int? code = dbCache.GetSortingCodeOV1(systemName);
                    if (code.HasValue)
                        return code.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения кода сортировки: {ex.Message}");
            }

            return 0;
        }

        private static bool IsDuctSystem(string systemName)
        {
            if (string.IsNullOrEmpty(systemName))
                return false;

            return systemName.Contains("П") ||
                   systemName.Contains("В") ||
                   systemName.Contains("ДП") ||
                   systemName.Contains("ДВ") ||
                   systemName.Contains("ПЕ") ||
                   systemName.Contains("ВЕ") ||
                   systemName.Contains("ДПЕ");
        }

        private static string SystemTransformer(string origSystem)
        {
            if (string.IsNullOrEmpty(origSystem))
                return origSystem ?? "";

            string[] parts = origSystem.Split('.');
            if (parts.Length >= 2)
            {
                string firstPart = parts[0] ?? "";
                if (origSystem.EndsWith("а") || origSystem.EndsWith("a"))
                {
                    return firstPart + "а";
                }
                return firstPart;
            }

            return origSystem;
        }

        public static string GetSection(Element element, Document doc)
        {
            if (element == null || doc == null) return "-";

            var sectionCache = new SectionCache(doc);
            var sectionInfo = sectionCache.GetSectionAndPartForElement(element);
            return sectionInfo.section ?? "-";
        }

        public static string GetPart(Element element, Document doc)
        {
            if (element == null || doc == null) return "-";

            var sectionCache = new SectionCache(doc);
            var sectionInfo = sectionCache.GetSectionAndPartForElement(element);
            return sectionInfo.part ?? "-";
        }
    }
}