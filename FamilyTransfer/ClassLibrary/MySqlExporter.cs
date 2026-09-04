using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitToMySQL
{
    public static class MySqlExporter
    {
        private const int BATCH_SIZE = 1000;

        // ========== ЕДИНСТВЕННЫЙ МЕТОД (полный экспорт) ==========
        public static void ExportToMySQL(List<FamilyInfo> elements, string tableName, string connectionString)
        {
            if (elements == null || elements.Count == 0) return;

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                using (MySqlTransaction transaction = connection.BeginTransaction())
                {
                    for (int i = 0; i < elements.Count; i += BATCH_SIZE)
                    {
                        var batch = elements.Skip(i).Take(BATCH_SIZE).ToList();
                        InsertBatch(connection, transaction, batch, tableName);
                    }

                    transaction.Commit();
                }
            }
        }
        // ========== СТАРЫЙ МЕТОД InsertBatch ==========
        private static void InsertBatch(
            MySqlConnection connection,
            MySqlTransaction transaction,
            List<FamilyInfo> batch,
            string tableName)
        {
            string sql = BuildInsertSql(batch, tableName);

            using (MySqlCommand command = new MySqlCommand(sql, connection, transaction))
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    var e = batch[i];
                    string suffix = $"_{i}";

                    command.Parameters.AddWithValue($"@familyname{suffix}", e.FamilyName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@ucode{suffix}", e.Ucode ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@funcgroup{suffix}", e.FuncGroup ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@functypes{suffix}", e.FuncTypes ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@modification{suffix}", e.Modification ?? (object)DBNull.Value);

                    if (tableName == "OV1")
                    {
                        command.Parameters.AddWithValue($"@workpressure{suffix}", e.WorkPressure ?? (object)DBNull.Value);
                    }
                    else
                    {
                        command.Parameters.AddWithValue($"@ductei{suffix}", e.DuctEI ?? (object)DBNull.Value);
                    }

                    command.Parameters.AddWithValue($"@order{suffix}", e.Order ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@size{suffix}", e.Size ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@systemname{suffix}", e.SystemName ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@amount{suffix}", e.Amount ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@section{suffix}", e.Section ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@part{suffix}", e.Part ?? (object)DBNull.Value);
                    command.Parameters.AddWithValue($"@id{suffix}", e.ID);
                    command.Parameters.AddWithValue($"@sorting{suffix}", e.SortingCode);
                    command.Parameters.AddWithValue($"@wipcode{suffix}", e.WipCode ?? (object)DBNull.Value);

                    // ❌ НЕ БЫЛО! Эти параметры добавили позже:
                    // command.Parameters.AddWithValue($"@hash{suffix}", ComputeHash(e));
                    // command.Parameters.AddWithValue($"@lastmodified_{suffix}", DateTime.Now);
                    // command.Parameters.AddWithValue($"@lastmodifiedby_{suffix}", Environment.UserName);
                }

                command.ExecuteNonQuery();
            }
        }

        // ========== СТАРЫЙ BuildInsertSql (БЕЗ DataHash, LastModified, LastModifiedBy) ==========
        private static string BuildInsertSql(List<FamilyInfo> batch, string tableName)
        {
            var sb = new StringBuilder();

            if (tableName == "OV1")
            {
                sb.AppendLine("INSERT INTO OV1 (FamilyName, Ucode, FuncGroup, FuncTypes, Modification, WorkPressure, `Order`, Size, SystemName, Amount, Section, Part, ID, Sorting, WipCode) VALUES ");
            }
            else
            {
                sb.AppendLine("INSERT INTO OV2 (FamilyName, Ucode, FuncGroup, FuncTypes, Modification, DuctEI, `Order`, Size, SystemName, Amount, Section, Part, ID, Sorting, WipCode) VALUES ");
            }

            for (int i = 0; i < batch.Count; i++)
            {
                sb.Append($"(@familyname_{i}, @ucode_{i}, @funcgroup_{i}, @functypes_{i}, @modification_{i}, ");

                if (tableName == "OV1")
                {
                    sb.Append($"@workpressure_{i}, ");
                }
                else
                {
                    sb.Append($"@ductei_{i}, ");
                }

                sb.Append($"@order_{i}, @size_{i}, @systemname_{i}, @amount_{i}, @section_{i}, @part_{i}, @id_{i}, @sorting_{i}, @wipcode_{i})");

                if (i < batch.Count - 1)
                    sb.Append(", ");
                else
                    sb.Append(";");
            }

            return sb.ToString();
        }
    }
}