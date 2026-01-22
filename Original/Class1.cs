using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO.Pipelines;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Microsoft.Identity.Client;
using MySql.Data.MySqlClient;

namespace RevitToMySQL_2019
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExportDuctsToMySQL : IExternalCommand
    {
        FamilyInstance familyInstance;

        public class FamilyInfo
        {
            public string FamilyName { get; set; }
            public string DuckSize { get; set; }
            public string MepDiametr { get; set; }
            public string FuncGroup { get; set; }
            public string FuncTypes { get; set; }
            public double Length { get; set; }
            public string FuncGtoupType { get; set; }


        }


        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;


            ////----   БЛОК 0 приветственное слово   ----
            //string title = doc.Title;
            //Regex docname = new Regex(@"^\d{4}-Р-[A-Za-zА-Яа-я]{0,3}-.+-ОВ-(ОВ[1-3])_.+$");
            //if (!docname.IsMatch(title))
            //{
            //    TaskDialog.Show("Информация", "Похоже название вашего файла не соответсвует формуляру, программа продолжит работу");
            //}

            ////Match secmatch = Regex.Match(title, @"с(\d+)");
            ////string numbsection = secmatch.Success ? secmatch.Groups[1].Value : null;

            ////Match partmatch = Regex.Match(title, @"с(\d+)");
            ////string numbpart = partmatch.Success ? partmatch.Groups[1].Value : null;





















            //проверка названия открытой модели соответствует ли https://kb.bimteam.ru/pages/viewpage.action?pageId=65471646

            /*string title = doc.Title;
            Regex docname = new Regex(@"^\d{4}-Р-[A-Za-zА-Яа-я]{0,3}-.+-ОВ-(ОВ[1-3])_.+$");
            if (!docname.IsMatch(title))
            {
                TaskDialog.Show("Информация", "Похоже название вашего файла не соответсвует формуляру, программа продолжит работу");
            }
            */


            //получаем стадию/раздел из модели, проврека соотвтествия ОВ1/ОВ2/ОВ3 

            //обработка связей где совпадают стадия и раздел 

            //проверка того что параметры не нулевые 


            /* familyinstance
             * pipe
             * pipeinsulation
             * duct
             * ductinsulation


            */

            try
            {
                ////получаем все familyinstance
                //var familyinstance = new FilteredElementCollector(doc)
                //    .OfClass(typeof(FamilyInstance))
                //    .Cast<FamilyInstance>()
                //    .ToList();

                ////получаем все pipe
                //var pipes = new FilteredElementCollector(doc)
                //    .OfClass(typeof(Pipe))
                //    .Cast<Pipe>()
                //    .ToList();

                // получаем все воздуховоды
                var ducts = new FilteredElementCollector(doc)
                    .OfClass(typeof(Duct))
                    .Cast<Duct>()
                    .ToList();

                //проверка наличия семейств в проекте
                if (ducts.Count == 0)
                {
                    TaskDialog.Show("Информация", "В проекте нет воздуховодов для экспорта.");
                    return Result.Cancelled;
                }



                var ductData = new List<DuctInfo>();
                foreach (var duct in ducts)
                {
                    string familyName = duct.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "Не указано";
                    //string familyName = duct.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "Не указано";
                    string size = GetDuctSize(duct);
                    double length = UnitUtils.ConvertFromInternalUnits(
                        duct.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble(),
                        DisplayUnitType.DUT_METERS
                    );

                    ductData.Add(new DuctInfo
                    {
                        FamilyName = familyName,
                        Size = size,
                        Length = length
                    });
                }

                // Экспорт в MySQL 
                string connectionString = "Server=94.228.125.175;Database=testovk;Uid=zhukovk;Pwd=o?6JW83Xfu&pZ3;";
                ExportToMySQL(ductData, connectionString);

                TaskDialog.Show("Успех", $"Экспортировано воздуховодов: {ductData.Count}");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка экспорта: {ex.Message}");
                return Result.Failed;
            }
        }

        // Метод экспорта в MySQL (без транзакций для упрощения)
        private void ExportToMySQL(List<DuctInfo> ductData, string connectionString)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                string sql = @"
                    INSERT INTO ducts (family_name, size, length)
                    VALUES (@familyName, @size, @length);";

                foreach (var duct in ductData)
                {
                    using (MySqlCommand command = new MySqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("@familyName", duct.FamilyName);
                        command.Parameters.AddWithValue("@size", duct.Size);
                        command.Parameters.AddWithValue("@length", duct.Length);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
        // Метод для получения размера воздуховода (адаптирован для Revit 2019)
        private string GetDuctSize(Duct duct)
        {
            Parameter widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
            Parameter heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
            Parameter diameterParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

            string width = widthParam?.AsValueString();
            string height = heightParam?.AsValueString();
            string diameter = diameterParam?.AsValueString();

            if (!string.IsNullOrEmpty(width) && !string.IsNullOrEmpty(height))
                return $"{width} x {height}";

            return !string.IsNullOrEmpty(diameter) ? diameter : "N/A";
        }
    }

    // Класс для хранения данных о воздуховоде
    public class DuctInfo
    {
        public string FamilyName { get; set; }
        public string Size { get; set; }
        public double Length { get; set; }
    }
}