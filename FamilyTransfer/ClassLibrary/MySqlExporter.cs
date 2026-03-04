using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace RevitToMySQL
{
    public static class MySqlExporter
    {
        public static void ExportToMySQL(List<FamilyInfo> elements, string tableName, string connectionString)
        {
            if (elements == null || elements.Count == 0) return;

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (MySqlTransaction transaction = connection.BeginTransaction())
                {
                    string sql = tableName == "pipetest" ? GetPipeInsertSql() : GetDuctInsertSql();

                    using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
                    {
                        // Подготавливаем команду один раз
                        PrepareCommandParameters(command, tableName);
                        command.Prepare();

                        foreach (var e in elements)
                        {
                            // Заполняем параметры значениями
                            SetCommandParameters(command, e, tableName);
                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        private static void PrepareCommandParameters(MySqlCommand command, string tableName)
        {
            // Очищаем существующие параметры
            command.Parameters.Clear();

            // Добавляем все возможные параметры
            command.Parameters.Add("@familyname", MySqlDbType.VarChar);
            command.Parameters.Add("@ucode", MySqlDbType.VarChar);
            command.Parameters.Add("@funcgroup", MySqlDbType.VarChar);
            command.Parameters.Add("@functypes", MySqlDbType.VarChar);
            command.Parameters.Add("@modification", MySqlDbType.VarChar);

            if (tableName == "pipetest")
            {
                command.Parameters.Add("@workpressure", MySqlDbType.VarChar);
            }
            else
            {
                command.Parameters.Add("@ductei", MySqlDbType.VarChar);
            }

            command.Parameters.Add("@order", MySqlDbType.VarChar);
            command.Parameters.Add("@size", MySqlDbType.VarChar);
            command.Parameters.Add("@systemname", MySqlDbType.VarChar);
            command.Parameters.Add("@amount", MySqlDbType.VarChar);
            command.Parameters.Add("@section", MySqlDbType.VarChar);
            command.Parameters.Add("@part", MySqlDbType.VarChar);
            command.Parameters.Add("@id", MySqlDbType.Int32);
            command.Parameters.Add("@sorting", MySqlDbType.Int32);
        }

        private static void SetCommandParameters(MySqlCommand command, FamilyInfo e, string tableName)
        {
            command.Parameters["@familyname"].Value = e.FamilyName ?? (object)DBNull.Value;
            command.Parameters["@ucode"].Value = e.Ucode ?? (object)DBNull.Value;
            command.Parameters["@funcgroup"].Value = e.FuncGroup ?? (object)DBNull.Value;
            command.Parameters["@functypes"].Value = e.FuncTypes ?? (object)DBNull.Value;
            command.Parameters["@modification"].Value = e.Modification ?? (object)DBNull.Value;

            if (tableName == "pipetest")
            {
                command.Parameters["@workpressure"].Value = e.WorkPressure ?? (object)DBNull.Value;
            }
            else
            {
                command.Parameters["@ductei"].Value = e.DuctEI ?? (object)DBNull.Value;
            }

            command.Parameters["@order"].Value = e.Order ?? (object)DBNull.Value;
            command.Parameters["@size"].Value = e.Size ?? (object)DBNull.Value;
            command.Parameters["@systemname"].Value = e.SystemName ?? (object)DBNull.Value;
            command.Parameters["@amount"].Value = e.Amount ?? (object)DBNull.Value;
            command.Parameters["@section"].Value = e.Section ?? (object)DBNull.Value;
            command.Parameters["@part"].Value = e.Part ?? (object)DBNull.Value;
            command.Parameters["@id"].Value = e.ID;
            command.Parameters["@sorting"].Value = e.SortingCode;
        }

        private static string GetPipeInsertSql()
        {
            return @"INSERT INTO pipetest 
                    (FamilyName, Ucode, FuncGroup, FuncTypes, Modification, WorkPressure, 
                     `Order`, Size, SystemName, Amount, Section, Part, ID, Sorting)
                    VALUES 
                    (@familyname, @ucode, @funcgroup, @functypes, @modification, @workpressure,
                     @order, @size, @systemname, @amount, @section, @part, @id, @sorting);";
        }

        private static string GetDuctInsertSql()
        {
            return @"INSERT INTO ducttest 
                    (FamilyName, Ucode, FuncGroup, FuncTypes, Modification, DuctEI,
                     `Order`, Size, SystemName, Amount, Section, Part, ID, Sorting)
                    VALUES 
                    (@familyname, @ucode, @funcgroup, @functypes, @modification, @ductei,
                     @order, @size, @systemname, @amount, @section, @part, @id, @sorting);";
        }
    }
}