using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace RevitToMySQL
{
    public static class MySqlExporter
    {
        public static void ExportToMySQL(List<FamilyInfo> elements, string tableName, string connectionString)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string sql = tableName == "pipetest" ? GetPipeInsertSql() : GetDuctInsertSql();

                foreach (var e in elements)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection))
                    {
                        AddParameters(command, e, tableName);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
        private static string GetPipeInsertSql()
        {
            return @"INSERT INTO pipetest (FamilyName, Ucode, FuncGroup, FuncTypes, Modification, WorkPressure, `Order`, Size, SystemName, Amount, Section, Part, ID, Sorting)
                    VALUES (@familyname, @ucode, @funcgroup, @functypes, @modification, @workpressure, @order, @size, @systemname, @amount, @section, @part, @id, @sorting);";
        }
        private static string GetDuctInsertSql()

        {
            return @"INSERT INTO ducttest (FamilyName, Ucode, FuncGroup, FuncTypes, Modification, DuctEI, `Order`, Size, SystemName, Amount, Section, Part, ID, Sorting)
                    VALUES (@familyname, @ucode, @funcgroup, @functypes, @modification, @ductei, @order, @size, @systemname, @amount, @section, @part, @id, @sorting);";
        }
        private static void AddParameters(MySqlCommand command, FamilyInfo e, string tableName)
        {
            command.Parameters.AddWithValue("@familyname", e.FamilyName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ucode", e.Ucode ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@funcgroup", e.FuncGroup ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@functypes", e.FuncTypes ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@modification", e.Modification ?? (object)DBNull.Value);

            if (tableName == "pipetest")
            {
                command.Parameters.AddWithValue("@workpressure", e.WorkPressure ?? (object)DBNull.Value);
            }
            else
            {
                command.Parameters.AddWithValue("@ductei", e.DuctEI ?? (object)DBNull.Value);
            }

            command.Parameters.AddWithValue("@order", e.Order ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@size", e.Size ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@systemname", e.SystemName ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@amount", e.Amount ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@section", e.Section ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@part", e.Part ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@id", e.ID);
            command.Parameters.AddWithValue("@sorting", e.SortingCode);
        }
    }
}