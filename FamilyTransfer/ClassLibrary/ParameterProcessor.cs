using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using FamilyTransfer;
using FamilyTransfer.Cache;
using FamilyTransfer.Caching;
using RevitToMySQL.Caching;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitToMySQL
{
    public static class ParameterProcessor
    {
        public static List<FamilyInfo> GetParameters(
            List<Element> elements,
            Document mainDoc,
            DatabaseCache dbCache,
            SectionCache sectionCache,
            Dictionary<Document, ITypeParameterCacheProvider> typeCaches,
            string wipCode)
        {
            if (elements == null || !elements.Any())
                return new List<FamilyInfo>();

            var result = new List<FamilyInfo>(elements.Count);

            foreach (var e in elements)
            {
                Document elementDoc = e.Document;

                if (!typeCaches.TryGetValue(elementDoc, out var typeCache))
                {
                    typeCache = new TypeParameterCacheProvider(elementDoc);
                    typeCaches[elementDoc] = typeCache;
                }

                var instanceCache = new ParameterCache(e);
                var typeCacheForElement = typeCache.Get(e);
                var resolver = new ParameterResolver(instanceCache, typeCacheForElement);

                string familyName = GetFamilyName(e, resolver);
                string ucode = GetUcode(e, resolver);
                string funcGroup = GetFuncGroup(resolver);
                string funcType = GetFuncType(resolver);
                string modification = GetModification(resolver);
                string workPressure = GetWorkPressure(resolver);
                string ductEI = GetDuctEI(resolver);
                string systemName = GetSystemName(e, resolver);
                string size = GetElementSize(e, resolver);
                string amount = GetElementAmount(e, resolver);

                var sectionInfo = sectionCache.GetSectionAndPartForElement(e);
                int sortingCode = GetSortingCode(e, systemName, dbCache);
                string order = MakeOrder(funcGroup, funcType, dbCache);

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
                    SortingCode = sortingCode,
                    WipCode = wipCode
                });
            }

            return result;
        }
        private static string GetFamilyName(Element element, ParameterResolver resolver)
        {
            string value = resolver.GetString(ParametersInfo.FamilyName);
            if (!string.IsNullOrEmpty(value))
                return value;

            Parameter param = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM);
            if (param != null && param.HasValue)
                return param.AsValueString() ?? "Не указано";

            return "Не указано";
        }
        private static string GetUcode(Element element, ParameterResolver resolver)
        {
            if (element is Pipe)
            {
                Parameter segmentParam = element.get_Parameter(BuiltInParameter.RBS_PIPE_CLASS_PARAM);
                if (segmentParam != null && segmentParam.HasValue)
                {
                    string segmentValue = segmentParam.AsValueString();
                    if (!string.IsNullOrEmpty(segmentValue))
                    {
                        string[] parts = segmentValue.Split('/');
                        if (parts.Length > 0 && !string.IsNullOrEmpty(parts[parts.Length - 1]))
                            return parts[parts.Length - 1];
                    }
                }
            }
            string ucode = resolver.GetString(ParametersInfo.UCode);
            return !string.IsNullOrEmpty(ucode) ? ucode : "-";
        }

        private static string GetFuncGroup(ParameterResolver resolver)
        {
            string value = resolver.GetString(ParametersInfo.FuncGroup);
            return !string.IsNullOrEmpty(value) ? value : "-";
        }
        private static string GetFuncType(ParameterResolver resolver)
        {
            string value = resolver.GetString(ParametersInfo.FuncType);
            return !string.IsNullOrEmpty(value) ? value : "-";
        }
        private static string GetModification(ParameterResolver resolver)
        {
            string value = resolver.GetString(ParametersInfo.Modification);
            return !string.IsNullOrEmpty(value) ? value : "-";
        }
        private static string GetWorkPressure(ParameterResolver resolver)
        {
            string value = resolver.GetString(ParametersInfo.WorkPressureNominal);
            return !string.IsNullOrEmpty(value) ? value : "-";
        }
        private static string GetDuctEI(ParameterResolver resolver)
        {
            string value = resolver.GetString(ParametersInfo.FireResistance);
            return !string.IsNullOrEmpty(value) ? value : "-";
        }
        private static string GetSystemName(Element element, ParameterResolver resolver)
        {
            Parameter param = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param != null && param.HasValue)
            {
                string value = param.AsString()?.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    string[] parts = value.Split('.', ',');
                    if (parts.Length > 0)
                        return parts[0].Trim();
                }
            }
            string mepSystem = resolver.GetString(ParametersInfo.SystemNameMEP);
            if (!string.IsNullOrEmpty(mepSystem))
            {
                string[] parts = mepSystem.Split('.', ',');
                if (parts.Length > 0)
                    return parts[0].Trim();
            }
            return "-";
        }

        private static string GetElementSize(Element e, ParameterResolver resolver)
        {
            if (e == null) return "-";
            try
            {
                if (e is Pipe)
                {
                    var sizeParam = e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE);
                    string size = sizeParam?.AsString() ?? "-";

                    double? outerDiam = resolver.GetDouble(ParametersInfo.OuterDiameter);
                    double? innerDiam = resolver.GetDouble(ParametersInfo.InnerDiameter);

                    if (outerDiam.HasValue && innerDiam.HasValue)
                    {
                        double outerMm = UnitUtils.ConvertFromInternalUnits(outerDiam.Value, DisplayUnitType.DUT_MILLIMETERS);
                        double innerMm = UnitUtils.ConvertFromInternalUnits(innerDiam.Value, DisplayUnitType.DUT_MILLIMETERS);
                        double wallThickness = (outerMm - innerMm) / 2;
                        return $"{size}x{wallThickness:F1}";
                    }
                    return size;
                }
                if (e is Duct)
                {
                    Parameter diameterParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);
                    if (diameterParam != null && diameterParam.HasValue && diameterParam.AsDouble() > 0)
                        return diameterParam.AsValueString() ?? "-";

                    double? height = resolver.GetDouble(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM.ToString());
                    double? width = resolver.GetDouble(BuiltInParameter.RBS_CURVE_WIDTH_PARAM.ToString());

                    if (height.HasValue && width.HasValue)
                    {
                        double heightMm = UnitUtils.ConvertFromInternalUnits(height.Value, DisplayUnitType.DUT_MILLIMETERS);
                        double widthMm = UnitUtils.ConvertFromInternalUnits(width.Value, DisplayUnitType.DUT_MILLIMETERS);
                        return $"{Math.Max(heightMm, widthMm):F0}x{Math.Min(heightMm, widthMm):F0}";
                    }
                    return "-";
                }

                if (e.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_DuctAccessory)
                {
                    double? width = resolver.GetDouble(ParametersInfo.WidthMep);
                    double? height = resolver.GetDouble(ParametersInfo.HeightMep);

                    if (width.HasValue && height.HasValue)
                    {
                        double widthMm = UnitUtils.ConvertFromInternalUnits(width.Value, DisplayUnitType.DUT_MILLIMETERS);
                        double heightMm = UnitUtils.ConvertFromInternalUnits(height.Value, DisplayUnitType.DUT_MILLIMETERS);
                        return $"{Math.Max(widthMm, heightMm):F0}x{Math.Min(widthMm, heightMm):F0}";
                    }

                    double? diam = resolver.GetDouble(ParametersInfo.Diameter1);
                    if (diam.HasValue)
                    {
                        double diamMm = UnitUtils.ConvertFromInternalUnits(diam.Value, DisplayUnitType.DUT_MILLIMETERS);
                        return $"{diamMm:F0}";
                    }
                    return "-";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($" Ошибка получения размера: {ex.Message}");
            }
            return "-";
        }

        private static string GetElementAmount(Element e, ParameterResolver resolver)
        {
            if (e == null) return "-";

            try
            {
                if (e is Pipe || e is Duct)
                {
                    Parameter lengthParam = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                    if (lengthParam != null && lengthParam.HasValue && Math.Abs(lengthParam.AsDouble()) > 1e-6)
                    {
                        double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthParam.AsDouble(), DisplayUnitType.DUT_METERS);
                        return lengthInMeters.ToString("F2");
                    }
                    return "-";
                }

                if (e is PipeInsulation || e is DuctInsulation)
                {
                    Parameter areaParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
                    if (areaParam != null && areaParam.HasValue && Math.Abs(areaParam.AsDouble()) > 1e-6)
                    {
                        double areaInSquareMeters = UnitUtils.ConvertFromInternalUnits(areaParam.AsDouble(), DisplayUnitType.DUT_SQUARE_METERS);
                        return areaInSquareMeters.ToString("F2");
                    }
                    return "-";
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
            string order = "-";
            try
            {
                int? groupOrder = dbCache.GetFuncGroupOrder(funcGroup ?? "");
                int? typeOrder = dbCache.GetFuncTypeOrder(funcType ?? "");

                string groupOrderStr = groupOrder.HasValue ? groupOrder.Value.ToString("D2") : "00";
                string typeOrderStr = typeOrder.HasValue ? typeOrder.Value.ToString("D2") : "00";

                return $"{groupOrderStr}{typeOrderStr}00";
            }
            catch
            {
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
                if (!string.IsNullOrEmpty(systemName) && (systemName.Contains("Т") || systemName.Contains("T")))
                {
                    int? code = dbCache.GetSortingCodeOV1(systemName);
                    if (code.HasValue)
                        return code.Value;
                }
            }
            catch
            {
                return 0;
            }
            return 0;
        }
        private static bool IsDuctSystem(string systemName)
        {
            if (string.IsNullOrEmpty(systemName)) return false;
            return systemName.Contains("П") || systemName.Contains("В") ||
                   systemName.Contains("ДП") || systemName.Contains("ДВ") ||
                   systemName.Contains("ПЕ") || systemName.Contains("ВЕ") || systemName.Contains("ДПЕ");
        }
        private static string SystemTransformer(string origSystem)
        {
            if (string.IsNullOrEmpty(origSystem)) return origSystem ?? "";
            string[] parts = origSystem.Split('.');
            if (parts.Length >= 2)
            {
                string firstPart = parts[0] ?? "";
                if (origSystem.EndsWith("а") || origSystem.EndsWith("a"))
                    return firstPart + "а";
                return firstPart;
            }
            return origSystem;
        }
    }
}