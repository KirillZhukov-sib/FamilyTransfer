using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyTransfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static RevitToMySQL.DatabaseCache;

namespace RevitToMySQL
{
    public class EquipmentDetector
    {
        public static List<FamilyInfo> Equip(
            List<Element> familyList,
            Document doc,
            DatabaseCache dbCache,
            SectionCache sectionCache,
            Dictionary<string, FamilyInfo> parentsData,
            string wipCode)
        {
            if (familyList == null || !familyList.Any() || dbCache == null || parentsData == null)
                return new List<FamilyInfo>();

            var result = new List<FamilyInfo>();
            try
            {
                foreach (Element e in familyList)
                {
                    Element familyType = null;
                    ElementId typeId = e.GetTypeId();
                    if (typeId != ElementId.InvalidElementId)
                    {
                        familyType = e.Document.GetElement(typeId);
                    }

                    string key = $"{e.Document.GetHashCode()}_{e.Id.IntegerValue}";

                    if (!parentsData.TryGetValue(key, out FamilyInfo pipe2))
                        continue;

                    string parentOrder = pipe2.Order;
                    int sorting = pipe2.SortingCode;
                    string systemName = pipe2.SystemName;
                    string section = pipe2.Section;
                    string part = pipe2.Part;
                    string ucode = familyType?.LookupParameter(ParametersInfo.UCode)?.AsString()?.Trim() ?? "-";
                    string modification = familyType?.LookupParameter(ParametersInfo.Modification)?.AsString()?.Trim() ?? "-";

                    var subEquipment = dbCache.GetSubEquipment(ucode, modification);
                    if (subEquipment == null || subEquipment.Count == 0) continue;

                    string familySize = ExtractSize(familyType);

                    foreach (var sub in subEquipment)
                    {
                        string orderSub = sub.EquipOrder;
                        string order = MakeOrder(e, parentsData, orderSub);

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
                                ID = e.Id.IntegerValue,
                                Section = section,
                                Part = part,
                                Order = order,
                                SortingCode = sorting,
                                WipCode = wipCode
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка в EquipmentDetector: {ex.Message}");
            }

            return result;
        }

        private static string MakeOrder(Element e, Dictionary<string, FamilyInfo> parentsData, string dirtyEquipOrder)
        {
            string key = $"{e.Document.GetHashCode()}_{e.Id.IntegerValue}";

            if (!parentsData.TryGetValue(key, out FamilyInfo family))
                return "-";

            string parentOrder = family.Order;

            if (dirtyEquipOrder.All(char.IsDigit) && dirtyEquipOrder.Length == 2)
            {
                string finalOrder = parentOrder.Substring(0, 4) + dirtyEquipOrder;
                return finalOrder;
            }

            if (dirtyEquipOrder.All(char.IsDigit) && dirtyEquipOrder.Length == 1)
            {
                string finalOrder = parentOrder.Substring(0, 4) + 0 + dirtyEquipOrder;
                return finalOrder;
            }
            return "-";
        }

        public static string ExtractSize(Element familyType)
        {
            if (familyType == null) return "-";

            string sizeDirty = familyType.LookupParameter(ParametersInfo.Diameter1)?.AsValueString()?.Trim() ?? "-";
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