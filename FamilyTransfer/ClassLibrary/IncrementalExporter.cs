using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace RevitToMySQL
{
    public static class IncrementalExporter
    {
        private const int BatchSize = 500;

        public static void ExportIncremental(
            List<FamilyInfo> currentElements,
            string tableName,
            string connectionString)
        {
            ValidateTableName(tableName);

            if (currentElements == null)
            {
                throw new ArgumentNullException(nameof(currentElements));
            }

            Console.WriteLine(
                $"[{tableName}] Начало инкрементального экспорта. " +
                $"Получено элементов: {currentElements.Count}");

            // --------------------------------------------------
            // 1. Проверяем и создаём словарь текущих элементов
            // --------------------------------------------------

            Dictionary<string, FamilyInfo> currentDictionary =
                BuildCurrentDictionary(currentElements, tableName);

            Console.WriteLine(
                $"[{tableName}] Уникальных элементов: " +
                $"{currentDictionary.Count}");

            using (MySqlConnection connection =
                   new MySqlConnection(connectionString))
            {
                connection.Open();

                // --------------------------------------------------
                // 2. Получаем текущие данные из БД
                // --------------------------------------------------

                Dictionary<string, ExistingRecord> existingDictionary =
                    GetExistingElements(connection, tableName);

                Console.WriteLine(
                    $"[{tableName}] Элементов в БД: " +
                    $"{existingDictionary.Count}");

                // --------------------------------------------------
                // 3. Сравниваем данные
                // --------------------------------------------------

                List<FamilyInfo> toInsert =
                    new List<FamilyInfo>();

                List<FamilyInfo> toUpdate =
                    new List<FamilyInfo>();

                HashSet<string> existingKeys =
                    new HashSet<string>(
                        existingDictionary.Keys,
                        StringComparer.Ordinal);

                foreach (KeyValuePair<string, FamilyInfo> pair
                         in currentDictionary)
                {
                    string key = pair.Key;
                    FamilyInfo current = pair.Value;

                    if (existingDictionary.TryGetValue(
                            key,
                            out ExistingRecord existing))
                    {
                        // Элемент уже существует

                        if (!string.Equals(
                                existing.DataHash,
                                current.DataHash,
                                StringComparison.Ordinal))
                        {
                            toUpdate.Add(current);
                        }

                        // Убираем ключ из списка кандидатов на удаление
                        existingKeys.Remove(key);
                    }
                    else
                    {
                        // Новый элемент
                        toInsert.Add(current);
                    }
                }

                List<ExistingRecord> toDelete =
                    existingKeys
                        .Select(key => existingDictionary[key])
                        .ToList();

                Console.WriteLine(
                    $"[{tableName}] " +
                    $"INSERT: {toInsert.Count}, " +
                    $"UPDATE: {toUpdate.Count}, " +
                    $"DELETE: {toDelete.Count}");

                // --------------------------------------------------
                // Если изменений нет
                // --------------------------------------------------

                if (toInsert.Count == 0 &&
                    toUpdate.Count == 0 &&
                    toDelete.Count == 0)
                {
                    Console.WriteLine(
                        $"[{tableName}] Изменений не обнаружено.");

                    return;
                }

                // --------------------------------------------------
                // 4. Выполняем транзакцию
                // --------------------------------------------------

                using (MySqlTransaction transaction =
                       connection.BeginTransaction())
                {
                    try
                    {
                        InsertElements(
                            connection,
                            transaction,
                            toInsert,
                            tableName);

                        UpdateElements(
                            connection,
                            transaction,
                            toUpdate,
                            tableName);

                        DeleteElements(
                            connection,
                            transaction,
                            toDelete,
                            tableName);

                        transaction.Commit();

                        Console.WriteLine(
                            $"[{tableName}] Экспорт успешно завершён.");
                    }
                    catch
                    {
                        transaction.Rollback();

                        throw;
                    }
                }
            }
        }


        // ==========================================================
        // ПРОВЕРКА И ПОСТРОЕНИЕ СЛОВАРЯ
        // ==========================================================

        private static Dictionary<string, FamilyInfo>
            BuildCurrentDictionary(
                IEnumerable<FamilyInfo> elements,
                string tableName)
        {
            Dictionary<string, FamilyInfo> dictionary =
                new Dictionary<string, FamilyInfo>(
                    StringComparer.Ordinal);

            foreach (FamilyInfo element in elements)
            {
                if (element == null)
                    continue;

                if (string.IsNullOrWhiteSpace(element.WipCode))
                {
                    throw new InvalidOperationException(
                        $"[{tableName}] Элемент ID={element.ID} " +
                        $"не содержит WipCode.");
                }

                // Пересчитываем Hash
                HashHelper.UpdateHash(element);

                string key =
                    ExportKeyHelper.GetKey(element);

                if (dictionary.ContainsKey(key))
                {
                    FamilyInfo duplicate =
                        dictionary[key];

                    throw new InvalidOperationException(
                        $"[{tableName}] Обнаружен дубликат PRIMARY KEY.\n" +
                        $"ID: {element.ID}\n" +
                        $"WipCode: {element.WipCode}\n\n" +

                        $"Первый элемент:\n" +
                        $"FamilyName={duplicate.FamilyName}\n" +
                        $"Ucode={duplicate.Ucode}\n" +
                        $"Modification={duplicate.Modification}\n\n" +

                        $"Дубликат:\n" +
                        $"FamilyName={element.FamilyName}\n" +
                        $"Ucode={element.Ucode}\n" +
                        $"Modification={element.Modification}");
                }

                dictionary.Add(key, element);
            }

            return dictionary;
        }


        // ==========================================================
        // ПОЛУЧЕНИЕ ДАННЫХ ИЗ MYSQL
        // ==========================================================

        private static Dictionary<string, ExistingRecord>
            GetExistingElements(
                MySqlConnection connection,
                string tableName)
        {
            Dictionary<string, ExistingRecord> result =
                new Dictionary<string, ExistingRecord>(
                    StringComparer.Ordinal);

            string sql = $@"
                SELECT
                    ID,
                    WipCode,
                    DataHash
                FROM `{tableName}`";

            using (MySqlCommand command =
                   new MySqlCommand(sql, connection))
            using (MySqlDataReader reader =
                   command.ExecuteReader())
            {
                while (reader.Read())
                {
                    ExistingRecord record =
                        new ExistingRecord
                        {
                            ID = reader.GetInt32("ID"),

                            WipCode =
                                reader.GetString("WipCode"),

                            DataHash =
                                reader.IsDBNull(
                                    reader.GetOrdinal("DataHash"))
                                    ? null
                                    : reader.GetString("DataHash")
                        };

                    string key =
                        $"{record.ID}|{record.WipCode}";

                    if (result.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            $"[{tableName}] В БД обнаружен " +
                            $"дубликат PRIMARY KEY: {key}");
                    }

                    result.Add(key, record);
                }
            }

            return result;
        }


        // ==========================================================
        // INSERT
        // ==========================================================

        private static void InsertElements(
            MySqlConnection connection,
            MySqlTransaction transaction,
            List<FamilyInfo> elements,
            string tableName)
        {
            if (elements == null || elements.Count == 0)
                return;

            foreach (List<FamilyInfo> batch
                     in SplitIntoBatches(elements, BatchSize))
            {
                InsertBatch(
                    connection,
                    transaction,
                    batch,
                    tableName);
            }
        }


        private static void InsertBatch(
            MySqlConnection connection,
            MySqlTransaction transaction,
            List<FamilyInfo> batch,
            string tableName)
        {
            StringBuilder sql = new StringBuilder();

            if (tableName == "OV1")
            {
                sql.Append(@"
                    INSERT INTO `OV1`
                    (
                        FamilyName,
                        Ucode,
                        FuncGroup,
                        FuncTypes,
                        Modification,
                        WorkPressure,
                        `Order`,
                        Size,
                        SystemName,
                        Amount,
                        Section,
                        Part,
                        ID,
                        Sorting,
                        WipCode,
                        `Level`,
                        DataHash,
                        LastModified,
                        LastModifiedBy
                    )
                    VALUES ");
            }
            else
            {
                sql.Append(@"
                    INSERT INTO `OV2`
                    (
                        FamilyName,
                        Ucode,
                        FuncGroup,
                        FuncTypes,
                        Modification,
                        DuctEI,
                        `Order`,
                        Size,
                        SystemName,
                        Amount,
                        Section,
                        Part,
                        ID,
                        Sorting,
                        WipCode,
                        `Level`,
                        DataHash,
                        LastModified,
                        LastModifiedBy
                    )
                    VALUES ");
            }

            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0)
                    sql.Append(",");

                sql.Append($"({BuildParameterList(i, tableName)})");
            }

            using (MySqlCommand command =
                   new MySqlCommand(
                       sql.ToString(),
                       connection,
                       transaction))
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    AddParameters(
                        command,
                        batch[i],
                        i,
                        tableName);
                }

                command.ExecuteNonQuery();
            }
        }


        // ==========================================================
        // UPDATE
        // ==========================================================

        private static void UpdateElements(
            MySqlConnection connection,
            MySqlTransaction transaction,
            List<FamilyInfo> elements,
            string tableName)
        {
            if (elements == null || elements.Count == 0)
                return;

            foreach (FamilyInfo element in elements)
            {
                UpdateSingleElement(
                    connection,
                    transaction,
                    element,
                    tableName);
            }
        }


        private static void UpdateSingleElement(
            MySqlConnection connection,
            MySqlTransaction transaction,
            FamilyInfo element,
            string tableName)
        {
            string sql;

            if (tableName == "OV1")
            {
                sql = @"
                    UPDATE `OV1`
                    SET
                        FamilyName = @FamilyName,
                        Ucode = @Ucode,
                        FuncGroup = @FuncGroup,
                        FuncTypes = @FuncTypes,
                        Modification = @Modification,
                        WorkPressure = @WorkPressure,
                        `Order` = @Order,
                        Size = @Size,
                        SystemName = @SystemName,
                        Amount = @Amount,
                        Section = @Section,
                        Part = @Part,
                        Sorting = @Sorting,
                        `Level` = @Level,
                        DataHash = @DataHash,
                        LastModified = @LastModified,
                        LastModifiedBy = @LastModifiedBy
                    WHERE
                        ID = @ID
                        AND WipCode = @WipCode";
            }
            else
            {
                sql = @"
                    UPDATE `OV2`
                    SET
                        FamilyName = @FamilyName,
                        Ucode = @Ucode,
                        FuncGroup = @FuncGroup,
                        FuncTypes = @FuncTypes,
                        Modification = @Modification,
                        DuctEI = @DuctEI,
                        `Order` = @Order,
                        Size = @Size,
                        SystemName = @SystemName,
                        Amount = @Amount,
                        Section = @Section,
                        Part = @Part,
                        Sorting = @Sorting,
                        `Level` = @Level,
                        DataHash = @DataHash,
                        LastModified = @LastModified,
                        LastModifiedBy = @LastModifiedBy
                    WHERE
                        ID = @ID
                        AND WipCode = @WipCode";
            }

            using (MySqlCommand command =
                   new MySqlCommand(
                       sql,
                       connection,
                       transaction))
            {
                AddSingleParameters(
                    command,
                    element,
                    tableName);

                int affectedRows =
                    command.ExecuteNonQuery();

                if (affectedRows != 1)
                {
                    throw new InvalidOperationException(
                        $"[{tableName}] UPDATE не затронул " +
                        $"ровно одну запись. " +
                        $"ID={element.ID}, " +
                        $"WipCode={element.WipCode}");
                }
            }
        }


        // ==========================================================
        // DELETE
        // ==========================================================

        private static void DeleteElements(
            MySqlConnection connection,
            MySqlTransaction transaction,
            List<ExistingRecord> elements,
            string tableName)
        {
            if (elements == null || elements.Count == 0)
                return;

            foreach (List<ExistingRecord> batch
                     in SplitIntoBatches(elements, BatchSize))
            {
                DeleteBatch(
                    connection,
                    transaction,
                    batch,
                    tableName);
            }
        }


        private static void DeleteBatch(
            MySqlConnection connection,
            MySqlTransaction transaction,
            List<ExistingRecord> batch,
            string tableName)
        {
            StringBuilder sql = new StringBuilder();

            sql.Append($"DELETE FROM `{tableName}` WHERE ");

            for (int i = 0; i < batch.Count; i++)
            {
                if (i > 0)
                    sql.Append(" OR ");

                sql.Append(
                    $"(ID = @id{i} AND WipCode = @wip{i})");
            }

            using (MySqlCommand command =
                   new MySqlCommand(
                       sql.ToString(),
                       connection,
                       transaction))
            {
                for (int i = 0; i < batch.Count; i++)
                {
                    command.Parameters.AddWithValue(
                        $"@id{i}",
                        batch[i].ID);

                    command.Parameters.AddWithValue(
                        $"@wip{i}",
                        batch[i].WipCode);
                }

                command.ExecuteNonQuery();
            }
        }


        // ==========================================================
        // ПАРАМЕТРЫ INSERT
        // ==========================================================

        private static string BuildParameterList(
            int index,
            string tableName)
        {
            if (tableName == "OV1")
            {
                return string.Join(", ",
                    $"@FamilyName{index}",
                    $"@Ucode{index}",
                    $"@FuncGroup{index}",
                    $"@FuncTypes{index}",
                    $"@Modification{index}",
                    $"@WorkPressure{index}",
                    $"@Order{index}",
                    $"@Size{index}",
                    $"@SystemName{index}",
                    $"@Amount{index}",
                    $"@Section{index}",
                    $"@Part{index}",
                    $"@ID{index}",
                    $"@Sorting{index}",
                    $"@WipCode{index}",
                    $"@Level{index}",
                    $"@DataHash{index}",
                    $"@LastModified{index}",
                    $"@LastModifiedBy{index}");
            }

            return string.Join(", ",
                $"@FamilyName{index}",
                $"@Ucode{index}",
                $"@FuncGroup{index}",
                $"@FuncTypes{index}",
                $"@Modification{index}",
                $"@DuctEI{index}",
                $"@Order{index}",
                $"@Size{index}",
                $"@SystemName{index}",
                $"@Amount{index}",
                $"@Section{index}",
                $"@Part{index}",
                $"@ID{index}",
                $"@Sorting{index}",
                $"@WipCode{index}",
                $"@Level{index}",
                $"@DataHash{index}",
                $"@LastModified{index}",
                $"@LastModifiedBy{index}");
        }


        private static void AddParameters(
            MySqlCommand command,
            FamilyInfo element,
            int index,
            string tableName)
        {
            command.Parameters.AddWithValue(
                $"@FamilyName{index}",
                ToDbValue(element.FamilyName));

            command.Parameters.AddWithValue(
                $"@Ucode{index}",
                ToDbValue(element.Ucode));

            command.Parameters.AddWithValue(
                $"@FuncGroup{index}",
                ToDbValue(element.FuncGroup));

            command.Parameters.AddWithValue(
                $"@FuncTypes{index}",
                ToDbValue(element.FuncTypes));

            command.Parameters.AddWithValue(
                $"@Modification{index}",
                ToDbValue(element.Modification));

            if (tableName == "OV1")
            {
                command.Parameters.AddWithValue(
                    $"@WorkPressure{index}",
                    ToDbValue(element.WorkPressure));
            }
            else
            {
                command.Parameters.AddWithValue(
                    $"@DuctEI{index}",
                    ToDbValue(element.DuctEI));
            }

            command.Parameters.AddWithValue(
                $"@Order{index}",
                ToDbValue(element.Order));

            command.Parameters.AddWithValue(
                $"@Size{index}",
                ToDbValue(element.Size));

            command.Parameters.AddWithValue(
                $"@SystemName{index}",
                ToDbValue(element.SystemName));

            command.Parameters.AddWithValue(
                $"@Amount{index}",
                ToDbValue(element.Amount));

            command.Parameters.AddWithValue(
                $"@Section{index}",
                ToDbValue(element.Section));

            command.Parameters.AddWithValue(
                $"@Part{index}",
                ToDbValue(element.Part));

            command.Parameters.AddWithValue(
                $"@ID{index}",
                element.ID);

            command.Parameters.AddWithValue(
                $"@Sorting{index}",
                element.SortingCode);

            command.Parameters.AddWithValue(
                $"@WipCode{index}",
                element.WipCode);

            command.Parameters.AddWithValue(
                $"@Level{index}",
                ToDbValue(element.Level));

            command.Parameters.AddWithValue(
                $"@DataHash{index}",
                ToDbValue(element.DataHash));

            command.Parameters.AddWithValue(
                $"@LastModified{index}",
                DateTime.Now);

            command.Parameters.AddWithValue(
                $"@LastModifiedBy{index}",
                Environment.UserName);
        }


        private static void AddSingleParameters(
            MySqlCommand command,
            FamilyInfo element,
            string tableName)
        {
            command.Parameters.AddWithValue(
                "@FamilyName",
                ToDbValue(element.FamilyName));

            command.Parameters.AddWithValue(
                "@Ucode",
                ToDbValue(element.Ucode));

            command.Parameters.AddWithValue(
                "@FuncGroup",
                ToDbValue(element.FuncGroup));

            command.Parameters.AddWithValue(
                "@FuncTypes",
                ToDbValue(element.FuncTypes));

            command.Parameters.AddWithValue(
                "@Modification",
                ToDbValue(element.Modification));

            if (tableName == "OV1")
            {
                command.Parameters.AddWithValue(
                    "@WorkPressure",
                    ToDbValue(element.WorkPressure));
            }
            else
            {
                command.Parameters.AddWithValue(
                    "@DuctEI",
                    ToDbValue(element.DuctEI));
            }

            command.Parameters.AddWithValue(
                "@Order",
                ToDbValue(element.Order));

            command.Parameters.AddWithValue(
                "@Size",
                ToDbValue(element.Size));

            command.Parameters.AddWithValue(
                "@SystemName",
                ToDbValue(element.SystemName));

            command.Parameters.AddWithValue(
                "@Amount",
                ToDbValue(element.Amount));

            command.Parameters.AddWithValue(
                "@Section",
                ToDbValue(element.Section));

            command.Parameters.AddWithValue(
                "@Part",
                ToDbValue(element.Part));

            command.Parameters.AddWithValue(
                "@Sorting",
                element.SortingCode);

            command.Parameters.AddWithValue(
                "@Level",
                ToDbValue(element.Level));

            command.Parameters.AddWithValue(
                "@DataHash",
                ToDbValue(element.DataHash));

            command.Parameters.AddWithValue(
                "@LastModified",
                DateTime.Now);

            command.Parameters.AddWithValue(
                "@LastModifiedBy",
                Environment.UserName);

            command.Parameters.AddWithValue(
                "@ID",
                element.ID);

            command.Parameters.AddWithValue(
                "@WipCode",
                element.WipCode);
        }


        // ==========================================================
        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
        // ==========================================================

        private static object ToDbValue(string value)
        {
            return string.IsNullOrEmpty(value)
                ? (object)DBNull.Value
                : value;
        }


        private static IEnumerable<List<T>> SplitIntoBatches<T>(
            List<T> source,
            int batchSize)
        {
            for (int i = 0; i < source.Count; i += batchSize)
            {
                yield return source
                    .Skip(i)
                    .Take(batchSize)
                    .ToList();
            }
        }


        private static void ValidateTableName(string tableName)
        {
            if (tableName != "OV1" &&
                tableName != "OV2")
            {
                throw new ArgumentException(
                    $"Недопустимая таблица: {tableName}",
                    nameof(tableName));
            }
        }


        // ==========================================================
        // ВНУТРЕННЯЯ МОДЕЛЬ ЗАПИСИ БД
        // ==========================================================

        private class ExistingRecord
        {
            public int ID { get; set; }

            public string WipCode { get; set; }

            public string DataHash { get; set; }
        }
    }
}