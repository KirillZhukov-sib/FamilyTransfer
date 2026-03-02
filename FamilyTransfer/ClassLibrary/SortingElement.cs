using Autodesk.Revit.DB;
using MySql.Data.MySqlClient;
using RevitToMySQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyTransfer.ClassLibrary
{
    internal class SortingElement
    {
        private static string GetSortingCode(Element element)
        {


            string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDCLC");
            string sysName = element.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsString() ?? "-";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string groupQuery = @"
                                    SELECT sorting_code 
                                    FROM default_register_OV2 
                                    WHERE family_system_name REGEXP CONCAT('(^|, )', @system_name, '(, |$)')";
                string groupOrder = "-";

                using (MySqlCommand groupCommand = new MySqlCommand(groupQuery, connection))
                {



                    groupCommand.Parameters.AddWithValue("@system_name", sysName.Replace("'", "''"));
                    object groupResult = groupCommand.ExecuteScalar();
                    groupOrder = groupResult?.ToString() ?? "-";
                }
                return $"{groupOrder}";
                
            }
        }
    }
}