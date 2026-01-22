using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using Microsoft.Identity.Client;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Mysqlx.Crud;

namespace RevitToMySQL
{
    public static class ConnectionStringHelper
    {
        public static string GetConnectionStringFromSecretJson(string DBName)
        {
            try
            {
                string secretPath = @"\\picompany.ru\pikp\Dep\DR\DEV_DLLS\Новая папка\secrets.json";

                if (!File.Exists(secretPath))
                    throw new FileNotFoundException("Файл secrets.json не найден по указанному пути");

                var configuration = new ConfigurationBuilder()
                    .AddJsonFile(secretPath, optional: false)
                    .Build();

                string connectionString = configuration.GetConnectionString(DBName);

                if (string.IsNullOrEmpty(connectionString))
                    throw new Exception($"Строка подключения для таблицы {DBName} не найдена в secrets.json");

                return connectionString;
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при чтении secrets.json для таблицы '{DBName}':{ex.Message}");
            }
        }
    }
}
