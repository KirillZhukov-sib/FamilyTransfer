using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using MySql.Data.MySqlClient;
using System.Data.SqlClient;
using System.Data;

namespace VentilationExporter
{
    [Transaction(TransactionMode.Manual)]
    public class ExportVentilationToMySQL : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                // 1. Получаем все элементы вентиляции (включая арматуру)
                var ventilationElements = GetVentilationElements(doc);

                // 2. Группируем элементы по семейству и размеру
                var groupedElements = GroupElements(ventilationElements);

                // 3. Экспортируем в MySQL
                ExportToMySQL(groupedElements, doc);

                TaskDialog.Show("Экспорт завершен",
                    $"Успешно экспортировано {groupedElements.Sum(x => x.Count)} элементов вентиляции.");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Произошла ошибка: {ex.Message}");
                return Result.Failed;
            }
        }

        private List<Element> GetVentilationElements(Document doc)
        {
            // Фильтр по дисциплине "Механизмы" (Mechanical)
            BuiltInCategory[] ventilationCategories = new BuiltInCategory[]
            {
                BuiltInCategory.OST_DuctCurves,       // Воздуховоды
                BuiltInCategory.OST_DuctFittings,     // Фасонные части
                BuiltInCategory.OST_DuctAccessories,  // Арматура (заслонки, клапаны и т. д.)
                BuiltInCategory.OST_DuctTerminal,    // Решетки/диффузоры
                BuiltInCategory.OST_MechanicalEquipment // Оборудование
            };

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(
                    ventilationCategories.Select(cat =>
                        new ElementCategoryFilter(cat) as ElementFilter).ToList()));

            return collector.ToList();
        }

        private List<VentilationElement> GroupElements(List<Element> elements)
        {
            var result = new List<VentilationElement>();

            foreach (var element in elements)
            {
                string familyName = element.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsString() ?? "Не определено";
                string size = GetElementSize(element);
                string typeName = element.get_Parameter(BuiltInParameter.SYMBOL_NAME_PARAM)?.AsString() ?? "Не определено";

                // Для арматуры учитываем тип (например, "Задвижка DN150")
                if (element.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_DuctAccessories)
                {
                    size = GetAccessorySize(element) ?? size;
                    familyName = $"{familyName} ({typeName})";
                }

                var existingItem = result.FirstOrDefault(x =>
                    x.FamilyName == familyName && x.Size == size);

                if (existingItem != null)
                {
                    existingItem.Count++;
                }
                else
                {
                    result.Add(new VentilationElement
                    {
                        FamilyName = familyName,
                        Size = size,
                        Count = 1,
                        // Для арматуры длина не учитывается (можно заменить на количество)
                        TotalLength = element is Duct ? GetElementLength(element) : 0
                    });
                }
            }

            return result;
        }

        private string GetElementSize(Element element)
        {
            if (element is Duct duct)
            {
                if (duct.Shape == ConnectorProfileType.Rectangular)
                {
                    double width = UnitUtils.ConvertFromInternalUnits(
                        duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM).AsDouble(),
                        DisplayUnitType.DUT_MILLIMETERS);

                    double height = UnitUtils.ConvertFromInternalUnits(
                        duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM).AsDouble(),
                        DisplayUnitType.DUT_MILLIMETERS);

                    return $"{width:0}×{height:0} мм";
                }
                else if (duct.Shape == ConnectorProfileType.Round)
                {
                    double diameter = UnitUtils.ConvertFromInternalUnits(
                        duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM).AsDouble(),
                        DisplayUnitType.DUT_MILLIMETERS);

                    return $"{diameter:0} мм";
                }
            }

            return "Не применимо";
        }

        // Получаем размер арматуры (например, "DN150")
        private string GetAccessorySize(Element element)
        {
            // Пробуем получить параметр "Размер" или "Диаметр"
            Parameter sizeParam = element.LookupParameter("Размер")
                              ?? element.LookupParameter("Диаметр")
                              ?? element.LookupParameter("Size");

            if (sizeParam != null && sizeParam.HasValue)
            {
                return sizeParam.AsValueString() ?? sizeParam.AsString();
            }

            // Если параметр не найден, пытаемся получить диаметр из подключений
            if (element is FamilyInstance familyInstance)
            {
                var connectors = familyInstance.MEPModel?.ConnectorManager?.Connectors;
                if (connectors != null)
                {
                    foreach (Connector connector in connectors)
                    {
                        if (connector.Shape == ConnectorProfileType.Round)
                        {
                            double diameter = UnitUtils.ConvertFromInternalUnits(
                                connector.Radius * 2,
                                DisplayUnitType.DUT_MILLIMETERS);

                            return $"DN {diameter:0} мм";
                        }
                    }
                }
            }

            return "Не определено";
        }

        private double GetElementLength(Element element)
        {
            if (element is Duct duct)
            {
                return UnitUtils.ConvertFromInternalUnits(
                    duct.get_Parameter(BuiltInParameter.RBS_CURVE_LENGTH_PARAM).AsDouble(),
                    DisplayUnitType.DUT_METERS);
            }

            return 0;
        }

        private void ExportToMySQL(List<VentilationElement> elements, Document doc)
        {
            string connectionString = "server=localhost;database=revit_db;user=username;password=password;";

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                // Очищаем таблицу перед экспортом
                using (MySqlCommand clearCmd = new MySqlCommand(
                    "TRUNCATE TABLE ventilation_elements", connection))
                {
                    clearCmd.ExecuteNonQuery();
                }

                // Команда для вставки данных
                string insertQuery = @"INSERT INTO ventilation_elements 
                    (project_name, family_name, size, count, total_length, export_date) 
                    VALUES (@project, @family, @size, @count, @length, @date)"
                ;

                using (MySqlCommand cmd = new MySqlCommand(insertQuery, connection))
                {
                    cmd.Parameters.Add("@project", MySqlDbType.VarChar);
                    cmd.Parameters.Add("@family", MySqlDbType.VarChar);
                    cmd.Parameters.Add("@size", MySqlDbType.VarChar);
                    cmd.Parameters.Add("@count", MySqlDbType.Int32);
                    cmd.Parameters.Add("@length", MySqlDbType.Double);
                    cmd.Parameters.Add("@date", MySqlDbType.DateTime);

                    // Получаем имя проекта
                    string projectName = doc.Title;

                    foreach (var item in elements)
                    {
                        cmd.Parameters["@project"].Value = projectName;
                        cmd.Parameters["@family"].Value = item.FamilyName;
                        cmd.Parameters["@size"].Value = item.Size;
                        cmd.Parameters["@count"].Value = item.Count;
                        cmd.Parameters["@length"].Value = item.TotalLength;
                        cmd.Parameters["@date"].Value = DateTime.Now;

                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }

    public class VentilationElement
    {
        public string FamilyName { get; set; }
        public string Size { get; set; }
        public int Count { get; set; }
        public double TotalLength { get; set; }
    }
}