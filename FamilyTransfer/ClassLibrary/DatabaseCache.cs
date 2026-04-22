using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;

namespace RevitToMySQL
{
    public class DatabaseCache
    {
        private Dictionary<string, int> _funcGroupOrders;
        private Dictionary<string, int> _funcTypeOrders;
        private Dictionary<string, int> _sortingCodesOV1;
        private Dictionary<string, int> _sortingCodesOV2;
        private Dictionary<(string ucode, string modification), List<SubEquipment>> _subEquipmentMap;

        public class SubEquipment
        {
            public string Ucode { get; set; }
            public string Modification { get; set; }
            public string WorkPressure { get; set; }
            public string Size { get; set; }
            public string Amount { get; set; }
            public string TriggerInstruction { get; set; }
            public string EquipOrder { get; set; }

        }

        public static DatabaseCache LoadAllData()
        {
            var cache = new DatabaseCache();

            try
            {
                string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDEQU");

                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    cache._funcGroupOrders = LoadSimpleDictionary(connection,
                        "SELECT funcgroup_name, funcgroup_order FROM Equip_funcgroups");

                    cache._funcTypeOrders = LoadSimpleDictionary(connection,
                        "SELECT functype_name, functype_order FROM Equip_functypes");

                    cache._subEquipmentMap = LoadSubEquipment(connection);
                }

                string sortingConnection = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDCLC");

                using (var connection = new MySqlConnection(sortingConnection))
                {
                    connection.Open();

                    cache._sortingCodesOV1 = LoadDictionaryWithSplitting(connection,
                        "SELECT family_system_name, sorting_code FROM default_register_OV1");

                    cache._sortingCodesOV2 = LoadDictionaryWithSplitting(connection,
                        "SELECT family_system_name, sorting_code FROM default_register_OV2");
                }
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки DatabaseCache: {ex.Message}");
                cache._funcGroupOrders = new Dictionary<string, int>();
                cache._funcTypeOrders = new Dictionary<string, int>();
                cache._sortingCodesOV1 = new Dictionary<string, int>();
                cache._sortingCodesOV2 = new Dictionary<string, int>();
                cache._subEquipmentMap = new Dictionary<(string, string), List<SubEquipment>>();
            }

            return cache;
        }


        private static Dictionary<string, int> LoadSimpleDictionary(MySqlConnection connection, string query)
        {
            var dict = new Dictionary<string, int>();

            try
            {
                using (var cmd = new MySqlCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string key = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        if (!string.IsNullOrEmpty(key) && !reader.IsDBNull(1))
                        {
                            int value = reader.GetInt32(1);

                            // Просто добавляем без разбивки
                            if (!dict.ContainsKey(key))
                            {
                                dict[key] = value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки простого словаря: {ex.Message}");
            }

            return dict;
        }


        private static Dictionary<string, int> LoadDictionaryWithSplitting(MySqlConnection connection, string query)
        {
            var dict = new Dictionary<string, int>();

            try
            {
                using (var cmd = new MySqlCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string rawValue = reader.IsDBNull(0) ? "" : reader.GetString(0);
                        if (!string.IsNullOrEmpty(rawValue) && !reader.IsDBNull(1))
                        {
                            int code = reader.GetInt32(1);

                            if (rawValue.Contains(","))
                            {

                                string[] systems = rawValue.Split(new[] { ", ", "," }, StringSplitOptions.RemoveEmptyEntries);

                                foreach (string system in systems)
                                {
                                    string trimmedSystem = system.Trim();
                                    if (!dict.ContainsKey(trimmedSystem))
                                    {
                                        dict[trimmedSystem] = code;
                                    }
                                }
                            }
                            else
                            {
                                if (!dict.ContainsKey(rawValue))
                                {
                                    dict[rawValue] = code;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки словаря с разбивкой: {ex.Message}");
            }

            return dict;
        }
        private static Dictionary<(string, string), List<SubEquipment>> LoadSubEquipment(MySqlConnection connection)
        {
            var map = new Dictionary<(string, string), List<SubEquipment>>();

            try
            {
                string query = @"SELECT subeq_pipes_prnt_ucode, subeq_pipes_prnt_modification,
                                        subeq_pipes_sub_ucode, subeq_pipes_sub_modification,
                                        subeq_pipes_sub_workpressure, subeq_pipes_sub_size,
                                        subeq_pipes_formula, subeq_pipes_triggers, subeq_pipes_sub_order 
                                 FROM Equip_subequipment_pipes";

                using (var cmd = new MySqlCommand(query, connection))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;

                        var key = (reader.GetString(0), reader.GetString(1));

                        if (!map.ContainsKey(key))
                            map[key] = new List<SubEquipment>();

                        var subEq = new SubEquipment
                        {
                            Ucode = reader.GetString(2),
                            Modification = reader.GetString(3),
                            WorkPressure = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Size = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Amount = reader.IsDBNull(6) ? null : reader.GetString(6),
                            TriggerInstruction = reader.IsDBNull(7) ? null : reader.GetString(7),
                            EquipOrder = reader.GetInt64(8).ToString()  
                            
                        };

                        map[key].Add(subEq);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки подоборудования: {ex.Message}");
            }

            return map;
        }
        public int? GetFuncGroupOrder(string groupName)
        {
            if (string.IsNullOrEmpty(groupName) || _funcGroupOrders == null)
                return null;

            return _funcGroupOrders.TryGetValue(groupName, out var val) ? val : (int?)null;
        }
        public int? GetFuncTypeOrder(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || _funcTypeOrders == null)
                return null;

            return _funcTypeOrders.TryGetValue(typeName, out var val) ? val : (int?)null;
        }
        public int? GetSortingCodeOV1(string systemName)
        {
            if (string.IsNullOrEmpty(systemName) || _sortingCodesOV1 == null)
                return null;

            return _sortingCodesOV1.TryGetValue(systemName, out var val) ? val : (int?)null;
        }
        public int? GetSortingCodeOV2(string systemName)
        {
            if (string.IsNullOrEmpty(systemName) || _sortingCodesOV2 == null)
                return null;

            return _sortingCodesOV2.TryGetValue(systemName, out var val) ? val : (int?)null;
        }
        public List<SubEquipment> GetSubEquipment(string ucode, string modification)
        {
            if (string.IsNullOrEmpty(ucode) || string.IsNullOrEmpty(modification) || _subEquipmentMap == null)
                return null;

            var key = (ucode, modification);
            return _subEquipmentMap.TryGetValue(key, out var list) ? list : null;
        }
    }
}