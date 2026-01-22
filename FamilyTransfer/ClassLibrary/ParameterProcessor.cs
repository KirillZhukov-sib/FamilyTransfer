using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Net.Sockets;

namespace RevitToMySQL
{
    public static class ParameterProcessor
    {
        public static List<FamilyInfo> GetParameters(List<Element> elements, Document doc)
        {
            var result = new List<FamilyInfo>();

            foreach (var e in elements)
            {
                string familyname = e.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "Не указано";

                // ---
                ElementId typeId = e.GetTypeId();
                Element familyType = null;
                if (typeId != ElementId.InvalidElementId)
                {
                    familyType = e.Document.GetElement(typeId);
                }
                string bscode = familyType?.LookupParameter("BS_Код изделия")?.AsString() ?? "-";
                // ---

                string funcgroup = familyType?.LookupParameter("MEP_Функциональная группа")?.AsString() ?? "-";
                string functype = familyType?.LookupParameter("MEP_Функциональный тип")?.AsString() ?? "-";
                string modification = e.LookupParameter("MEP_Модификация")?.AsString() ?? "-";
                string workpressure = GetWorkPressure(e);
                string ductEI = e.LookupParameter("Огнестойкость")?.AsValueString() ?? "-";
                string order = MakeOrder(e);
                //string size = GetElementSize(e);
                string size = "-";
                string systemname = e.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsString() ?? "-";
                string amount = GetElementAmount(e);
                string section = GetSection(e, doc);
                string part = GetPart(e, doc);
                int sorting = GetSortingCode(e);

                result.Add(new FamilyInfo
                {
                    FamilyName = familyname,
                    Ucode = bscode,
                    FuncGroup = funcgroup,
                    FuncTypes = functype,
                    Modification = modification,
                    WorkPressure = workpressure,
                    DuctEI = ductEI,
                    Order = order,
                    Size = size,
                    SystemName = systemname,
                    Amount = amount,
                    Section = section,
                    Part = part,
                    ID = e.Id.IntegerValue,
                    SortingCode = sorting
                });
            }
            return result;
        }

        private static string GetSection(Element element, Document doc)
        {
            var (section, _) = SectionHelper.GetBuildingSectionForElement(doc, element);
            return section;
        }

        private static string GetPart(Element element, Document doc)
        {
            var (_, part) = SectionHelper.GetBuildingSectionForElement(doc, element);
            return part;
        }

        private static string GetElementSize(Element e)
        {


            if (e is Pipe)
            {
                return e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
            }
            else if (e is Duct)
            {
                if (e.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.AsDouble() > 0)
                {
                    return e.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM)?.AsValueString() ?? "-";
                }
                else
                {
                    return e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
                }
            }
            else if (e is FamilyInstance)
            {
                return e.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
            }
            else if (e is DuctInsulation || e is PipeInsulation)
            {
                return e.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_DUCT)?.AsString() ?? "-";
            }
            return "-";
        }
        private static string GetElementAmount(Element e)
        {
            if (e is Pipe || e is Duct)
            {
                Parameter lengthParam = e.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                if (lengthParam == null || !lengthParam.HasValue || lengthParam.AsDouble() == 0)
                    return "-";
                double lengthInFeet = lengthParam.AsDouble();
                double lengthInMeters = UnitUtils.ConvertFromInternalUnits(lengthInFeet, DisplayUnitType.DUT_METERS);
                return lengthInMeters.ToString("F2");
            }
            else if (e is PipeInsulation || e is DuctInsulation)
            {
                Parameter areaParam = e.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);

                if (areaParam?.HasValue != true)
                    return "-";

                double areaInFeet = areaParam.AsDouble();

                if (areaInFeet == 0)
                    return "-";

                double areaInSquaremeters = UnitUtils.ConvertFromInternalUnits(areaInFeet, DisplayUnitType.DUT_SQUARE_METERS);
                return areaInSquaremeters.ToString("F2");
            }
            return "1";  
        }

        private static string GetWorkPressure(Element element)
        {
            string parameterName = "MEP_Давление номинальное (текст)";
            Parameter instanceParam = element.LookupParameter(parameterName);
            if (instanceParam != null && instanceParam.HasValue)
            {
                string value = instanceParam.AsString();
                if (!string.IsNullOrEmpty(value) && value != "-")
                    return value;
            }
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element familyType = element.Document.GetElement(typeId);
                Parameter typeParam = familyType?.LookupParameter(parameterName);
                if (typeParam != null && typeParam.HasValue)
                {
                    string value = typeParam.AsString();
                    if (!string.IsNullOrEmpty(value) && value != "-")
                        return value;
                }
            }
            return "-";
        }

        private static string Systemtransformer(string origsystem)
        {
            string final = origsystem;
            string[] parts = origsystem.Split('.');

            if (parts.Length >= 2)
            {
                string firstpart = parts[0];
                char lastchar = origsystem.LastOrDefault();

                if (lastchar == 'а' || lastchar == 'a')
                {
                    final = firstpart + "а";
                }
                else { final = firstpart; }

            }
            else
            {
                final = origsystem;
            }
            return final;
        }


        private static int GetSortingCode(Element element)
        {
            string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDCLC");
            string sysPipeName = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsString() ?? "-";
            string sysDuctName = element.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "-";

            string final = Systemtransformer(sysDuctName);            

            if (sysDuctName.Contains("П") ||
                sysDuctName.Contains("В") ||
                sysDuctName.Contains("ДП") ||
                sysDuctName.Contains("ДВ") ||
                sysDuctName.Contains("ПЕ") ||
                sysDuctName.Contains("ВЕ") ||
                sysDuctName.Contains("ДПЕ"))
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    string groupQuery = @"SELECT sorting_code 
                                        FROM default_register_OV2 
                                        WHERE family_system_name REGEXP CONCAT('(^|, )', @system_name, '(, |$)')";

                    using (MySqlCommand groupCommand = new MySqlCommand(groupQuery, connection))
                    {
                        groupCommand.Parameters.AddWithValue("@system_name", final.Replace("'", "''"));
                        object result = groupCommand.ExecuteScalar();

                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);  
                        }
                        else
                        {
                            return 0;
                        }
                    }
                }
            }
            //if (sysPipeName.Contains("Т"))
            //{
            //    using (MySqlConnection connection = new MySqlConnection(connectionString))
            //    {
            //        connection.Open();

            //        string groupQuery = @"SELECT sorting_code 
            //                            FROM default_register_OV1 
            //                            WHERE family_system_name REGEXP CONCAT('(^|, )', @system_name, '(, |$)')";

            //        using (MySqlCommand groupCommand = new MySqlCommand(groupQuery, connection))
            //        {

            //            groupCommand.Parameters.AddWithValue("@system_name", sysPipeName.Replace("'", "''"));
            //            object result = groupCommand.ExecuteScalar();

            //            if (result != null && result != DBNull.Value)
            //            {
            //                return Convert.ToInt32(result);
            //            }
            //            else
            //            {
            //                return 0;
            //            }
            //        }
            //    }
            //}
            return 0;
        } 

        private static string MakeOrder(Element element)
        {
            ElementId typeId = element.GetTypeId();
            Element familyType = null;
            if (typeId != ElementId.InvalidElementId)
            {
                familyType = element.Document.GetElement(typeId);
            }
            string funcgroup = familyType?.LookupParameter("MEP_Функциональная группа")?.AsString() ?? "-";
            string functype = familyType?.LookupParameter("MEP_Функциональный тип")?.AsString() ?? "-";

            string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDEQU");

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string groupQuery = "SELECT funcgroup_order FROM Equip_funcgroups WHERE funcgroup_name = @funcgroupName";
                string groupOrder = "-";


                using (MySqlCommand groupCommand = new MySqlCommand(groupQuery, connection))
                {
                    groupCommand.Parameters.AddWithValue("@funcgroupName", funcgroup);
                    object groupResult = groupCommand.ExecuteScalar();
                    groupOrder = groupResult?.ToString() ?? "-";
                }
                 
                string typeQuery = "SELECT functype_order FROM Equip_functypes WHERE functype_name = @functypeName";
                string typeOrder = "-";
                
                using (MySqlCommand typeCommand = new MySqlCommand(typeQuery, connection))
                {
                    typeCommand.Parameters.AddWithValue("@functypeName", functype);
                    object typeResult = typeCommand.ExecuteScalar();
                    typeOrder = typeResult?.ToString() ?? "-";
                }
                return $"{groupOrder}{typeOrder}00";
            }
        }
    }
}
