using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevitToMySQL
{
    public class EquipmentDetector
    {
        public static List<FamilyInfo> Equip(List<Element> familyList)
        {
            List<FamilyInfo> familyEquipment = new List<FamilyInfo>();

            if (familyList == null || !familyList.Any())
                return familyEquipment;

            string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDEQU");

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string query = @"SELECT subeq_pipes_sub_ucode, 
                                        subeq_pipes_sub_modification,
                                        subeq_pipes_sub_workpressure,
                                        subeq_pipes_sub_size,
                                        subeq_pipes_formula
                                 FROM Equip_subequipment_pipes 
                                 WHERE subeq_pipes_prnt_ucode = @ucode 
                                   AND subeq_pipes_prnt_modification = @modification";

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    command.Parameters.Add("@ucode", MySqlDbType.VarChar);
                    command.Parameters.Add("@modification", MySqlDbType.VarChar);

                    foreach (Element e in familyList)
                    {
                        ElementId typeId = e.GetTypeId();
                        Element familyType = null;

                        if (typeId != ElementId.InvalidElementId)
                        {
                            familyType = e.Document.GetElement(typeId);
                        }

                        string familyUcode = familyType?.LookupParameter("BS_Код изделия")?.AsString()?.Trim() ?? "-";
                        string familyModification = familyType?.LookupParameter("MEP_Модификация")?.AsString()?.Trim() ?? "-";

                        string familySizeDirty = familyType?.LookupParameter("MEP_Диаметр 1")?.AsValueString()?.Trim() ?? "-";
                        string familySize = Regex.Match(familySizeDirty, @"[\d\.]+").Value;
                        int familyID = e.Id.IntegerValue;


                        if (string.IsNullOrEmpty(familyUcode) || familyUcode == "-")
                            continue;

                        command.Parameters["@ucode"].Value = familyUcode;
                        command.Parameters["@modification"].Value = familyModification;

                        using (MySqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                string dbSizeValue = reader.IsDBNull(3) ? "" : reader.GetString(3);

                                string finalSize;
                                if (!string.IsNullOrEmpty(dbSizeValue) &&
                                    dbSizeValue.Trim().Equals("asparent", StringComparison.OrdinalIgnoreCase))
                                {
                                    finalSize = familySize;
                                }
                                else
                                {
                                    finalSize = dbSizeValue;
                                }

                                familyEquipment.Add(new FamilyInfo
                                {
                                    FamilyName = "комплектация тест",
                                    Ucode = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Modification = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    WorkPressure = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Size = finalSize,
                                    Amount = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    ID = familyID
                                });
                            }
                        }
                    }
                }
            }

            return familyEquipment;
        }
    }
}