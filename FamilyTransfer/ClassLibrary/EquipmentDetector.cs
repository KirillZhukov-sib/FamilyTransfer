using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitToMySQL
{
    public class EquipmentDetector
    {
        public static List<FamilyInfo> Equip(List<Element> familyList, Document doc,
                                              DatabaseCache dbCache, SectionCache sectionCache, Dictionary<int, FamilyInfo>parentsData)
        {
            if (familyList == null || !familyList.Any() || dbCache == null)
                return new List<FamilyInfo>();

            var result = new List<FamilyInfo>();
                        
            try
            {
                foreach (Element e in familyList)
                {
                    try
                    {

                        Element familyType = null;
                        ElementId typeId = e.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            familyType = e.Document.GetElement(typeId);
                        }
                        int familyID = e.Id.IntegerValue;

                        //var parent = parentsData(typeId);

                        FamilyInfo pipe = new FamilyInfo();
                        FamilyInfo pipe2 = parentsData[familyID];


                        string order = pipe2.Order; //
                        string sorting = pipe2.SortingCode.ToString();//
                        string systemName = pipe2.SystemName;
                        string section = pipe2.Section;
                        string part = pipe2.Part;



                        string ucode = familyType?.LookupParameter("BS_Код изделия")?.AsString()?.Trim() ?? "-";
                        string modification = familyType?.LookupParameter("MEP_Модификация")?.AsString()?.Trim() ?? "-";

                        var subEquipment = dbCache.GetSubEquipment(ucode, modification);
                        if (subEquipment == null || subEquipment.Count == 0) continue;

                        string familySize = ExtractSize(familyType);
                        //string systemName = e.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsString() ?? "-";

                        //var sectionInfo = sectionCache.GetSectionAndPartForElement(e);
                        //string section = sectionInfo.section ?? "-";
                        //string part = sectionInfo.part ?? "-";


                        foreach (var sub in subEquipment)
                        {
                            if (ShouldInclude(e, familyType, sub))
                            {
                                result.Add(new FamilyInfo
                                {
                                    FamilyName = "комплектация тест",
                                    Ucode = sub.Ucode ?? "",
                                    Modification = sub.Modification ?? "",
                                    WorkPressure = sub.WorkPressure ?? "",
                                    Size = GetFinalSize(sub.Size, familySize),
                                    Amount = sub.Amount ?? "-",
                                    SystemName = systemName,
                                    ID = familyID,
                                    Section = section,
                                    Part = part,
                                    Order = order
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка обработки элемента: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка в EquipmentDetector: {ex.Message}");
            }

            return result;
        }

        private static string ExtractSize(Element familyType)
        {
            if (familyType == null) return "-";

            string sizeDirty = familyType.LookupParameter("MEP_Диаметр 1")?.AsValueString()?.Trim() ?? "-";
            Match match = Regex.Match(sizeDirty, @"[\d\.]+");
            return match.Success ? match.Value : "-";
        }

        private static string GetFinalSize(string dbSize, string familySize)
        {
            if (!string.IsNullOrEmpty(dbSize) &&
                dbSize.Trim().Equals("asparent", StringComparison.OrdinalIgnoreCase))
            {
                return familySize;
            }
            return dbSize ?? "-";
        }

        private static bool ShouldInclude(Element element, Element familyType, DatabaseCache.SubEquipment sub)
        {
            if (string.IsNullOrEmpty(sub.TriggerInstruction) || sub.TriggerInstruction == "нет")
                return true;

            if (!sub.TriggerInstruction.Contains("param"))
                return false;

            try
            {
                var (paramType, paramName, expectedValue) = ParseTrigger(sub.TriggerInstruction);
                string actualValue = GetParameterValue(element, familyType, paramType, paramName);

                return actualValue == expectedValue;
            }
            catch
            {
                return false;
            }
        }

        private static (string type, string name, string value) ParseTrigger(string trigger)
        {
            string[] parts = trigger.Split(':');
            if (parts.Length < 3)
                return ("", "", "");

            return (
                parts[0].Replace("param{", ""),
                parts[1],
                parts[2].Replace("{", "").Replace("}", "")
            );
        }


        private static string GetParameterValue(Element element, Element familyType, string paramType, string paramName)
        {
            try
            {
                switch (paramType)
                {
                    case "num":
                        string val = element?.LookupParameter(paramName)?.AsString()
                                   ?? familyType?.LookupParameter(paramName)?.AsString();
                        if (string.IsNullOrEmpty(val))
                        {
                            val = element?.LookupParameter(paramName)?.AsValueString()
                                ?? familyType?.LookupParameter(paramName)?.AsValueString();
                        }
                        return val?.Replace(" ", "")?.Replace("мм", "") ?? "";

                    case "bool":
                        int? boolVal = element?.LookupParameter(paramName)?.AsInteger()
                                     ?? familyType?.LookupParameter(paramName)?.AsInteger();
                        return boolVal == 1 ? "true" : "false";

                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }
    }
}