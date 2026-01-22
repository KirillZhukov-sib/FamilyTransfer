using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using MySql.Data.MySqlClient;
using static Google.Protobuf.Reflection.UninterpretedOption.Types;


// Метод для получения размера воздуховода 
//private string GetDuctSize(Duct duct)
//{
//    Parameter widthParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_WIDTH_PARAM);
//    Parameter heightParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_HEIGHT_PARAM);
//    Parameter diameterParam = duct.get_Parameter(BuiltInParameter.RBS_CURVE_DIAMETER_PARAM);

//    string width = widthParam?.AsValueString();
//    string height = heightParam?.AsValueString();
//    string diameter = diameterParam?.AsValueString();

//    if (!string.IsNullOrEmpty(width) && !string.IsNullOrEmpty(height))
//        return $"{width} x {height}";

//    return !string.IsNullOrEmpty(diameter) ? diameter : "N/A";
//}




/*
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

namespace RevitToMySQL
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExportDuctsToMySQL : IExternalCommand
    {
        public class FamilyInfo
        {
            public string Ucode { get; set; }
            public string WorkPressure { get; set; }    
            public string DuctEI { get; set; } 
            public string Modification { get; set; }  
            public string FuncGroup { get; set; }
            public string FuncTypes { get; set; }
            public string Order { get; set; } 
            public string Size { get; set; }  
            public string SystemName { get; set; }
            public string Length { get; set; } // длинаЮ площадь, количество 
            public string Section { get; set; } // номер секции
            public string Part { get; set; } // раздел 
            
            public int ID { get; set; }            
                      
        }



        public class FamilyDuct
        {
            public string FamilyName { get; set; }
            public string Bscode { get; set; }
            public string FuncGroup { get; set; }
            public string FuncTypes { get; set; }
            public string FuncGroupType { get; set; } //order
            public string Length { get; set; }
            public string Section { get; set; } // номер секции
            public string Part { get; set; } // раздел 
            public string SystemName { get; set; }
            public string DuctSize { get; set; }
            public string DuctEI { get; set; }
            public string ThicknessDuckInsolation { get; set; }
            public int ID { get; set; }
        }
        public class FamilyPipe
        {
            public string FamilyName { get; set; }
            public string Bscode { get; set; }
            public string FuncGroup { get; set; }
            public string FuncTypes { get; set; }
            public string FuncGroupType { get; set; } //order
            public string Length { get; set; }
            public string Section { get; set; } // номер секции
            public string Part { get; set; } // раздел 
            public string SystemName { get; set; }
            public string MepDiameter1 { get; set; }
            public string PipeSize { get; set; }
            public string ThicknessPipeInsolation { get; set; }
            public int ID { get; set; }
            //давление
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            //БЛОК 0 Проверка модели
            //проверка названия открытой модели соответствует ли https://kb.bimteam.ru/pages/viewpage.action?pageId=65471646

            string title = doc.Title;
            Regex docname = new Regex(@"^\d{4}-Р-[A-Za-zА-Яа-я]{0,3}-.+-ОВ-(ОВ[1-3])_.+$");
            if (!docname.IsMatch(title))
            {
                TaskDialog.Show("Информация", "Похоже название вашего файла не соответсвует формуляру, программа продолжит работу");
                //return Result.Failed;
            }
            //секция
            Match secmatch = Regex.Match(title, @"с(\d+)");
            string numbsection = secmatch.Success ? secmatch.Groups[1].Value : null;
            //раздел
            Match partmatch = Regex.Match(title, @"с(\d+)");
            string numbpart = partmatch.Success ? partmatch.Groups[1].Value : null;

            /*
            идеи касаемо сортировки:
            1. Деление только по имени системы:
               В папку pipe отправляются все элементы с типом системы (Т1/Т2...., К1??)
               В папку ducts отправлюся все элементы с типос системы (В.., П.., ДП.., ДВ..,)
            2. Деление по раб. набором? 
            3.

            что делать с обобщенными моделями(виртуальными элементами(детали крепления ОП, краска))
            что делать с вложенными семействами(шкаф
            */


/*

try
{
    // БЛОК 1. Получение всех элементов размещенных в моделе
    var elemPipe = new FilteredElementCollector(doc)
                    .WherePasses(new LogicalOrFilter(
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
                    new ElementCategoryFilter(BuiltInCategory.OST_PipeInsulations)))
                    .WhereElementIsNotElementType()
                    .ToList();








    //Pipes
    var pipeLine = new FilteredElementCollector(doc)
    .WherePasses(new LogicalOrFilter(
    new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves),
    new ElementCategoryFilter(BuiltInCategory.OST_PipeInsulations)))
    .WhereElementIsNotElementType()
    .ToList();

    var pipeelements = new FilteredElementCollector(doc)
    .WherePasses(new LogicalOrFilter(new List<ElementFilter>
    {
                new ElementCategoryFilter(BuiltInCategory.OST_PipeFitting),
                new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory),
                new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures)
    }))
    .WhereElementIsNotElementType()
    .OfType<FamilyInstance>()
    .ToList();

    var test = new FilteredElementCollector(doc)
   .WherePasses(new LogicalOrFilter(new List<ElementFilter>
   {
                new ElementCategoryFilter(BuiltInCategory.OST_PipeFitting),
                new ElementCategoryFilter(BuiltInCategory.OST_PipeAccessory),
                new ElementCategoryFilter(BuiltInCategory.OST_PlumbingFixtures)
   }))
   .WhereElementIsNotElementType()
   .OfType<FamilyInstance>()
   .ToList();

    //Duct
    var ductline = new FilteredElementCollector(doc)
    .WherePasses(new LogicalOrFilter(
    new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves),
    new ElementCategoryFilter(BuiltInCategory.OST_DuctInsulations)))
    .WhereElementIsNotElementType()
    .OrderBy(x => x.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "")
    .ThenBy(x => x.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "")
    .ToList();

    var ductelements = new FilteredElementCollector(doc)
    .WherePasses(new LogicalOrFilter(new List<ElementFilter>
    {
                new ElementCategoryFilter(BuiltInCategory.OST_DuctFitting),
                new ElementCategoryFilter(BuiltInCategory.OST_DuctAccessory),
                new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment)
    }))
    .WhereElementIsNotElementType()
    .OfType<FamilyInstance>()
    .OrderBy(x => x.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "")
    .ThenBy(x => x.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "")
    .ToList();

    //проверка наличия семейств в проекте и завершения программы
    if (pipeLine.Count == 0 && pipeelements.Count == 0 && ductline.Count == 0 && ductelements.Count == 0)
    {
        TaskDialog.Show("Информация", "В проекте нет воздуховодов для экспорта.");
        //return Result.Cancelled;
    }


    //БЛОК 2.занесение параметров в каждый элемент

    var FamilyDuctInfo = new List<FamilyDuct>();
    foreach (var ductl in ductline)
    {

        string familyName = ductl.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "-";
        string bscode = ductl.LookupParameter("BS_Код изделия")?.AsValueString() ?? "-";
        string functype = ductl.LookupParameter("MEP_Функциональный тип")?.AsValueString() ?? "-";
        string funcgroup = ductl.LookupParameter("MEP_Функциональная группа")?.AsValueString() ?? "-";
        string length = ductl.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsValueString() ?? "-";
        string size = ductl.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
        string ductEI = ductl.LookupParameter("Огнестойкость")?.AsValueString() ?? "-"; //тут изменить 
        string insulationDuctThickness = ductl.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_DUCT)?.AsValueString() ?? "-";
        string systemname = ductl.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "-";

        FamilyDuctInfo.Add(new FamilyDuct
        {
            FamilyName = familyName,
            Bscode = bscode,
            FuncGroup = funcgroup,
            FuncTypes = functype,
            Length = length,
            Section = numbsection,
            Part = numbpart,
            SystemName = systemname,
            DuctSize = size,
            DuctEI = ductEI,
            ThicknessDuckInsolation = insulationDuctThickness,
            ID = ductl.Id.IntegerValue
        });
    }
    var FamilyDuctInstInfo = new List<FamilyDuct>();
    foreach (var ductl in ductelements)
    {
        string familyName = ductl.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "-";
        string bscode = ductl.LookupParameter("BS_Код изделия")?.AsValueString() ?? "-";
        string functype = ductl.LookupParameter("MEP_Функциональный тип")?.AsValueString() ?? "-";
        string funcgroup = ductl.LookupParameter("MEP_Функциональная группа")?.AsValueString() ?? "-";
        string size = ductl.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
        string systemname = ductl.get_Parameter(BuiltInParameter.RBS_DUCT_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "-";

        FamilyDuctInfo.Add(new FamilyDuct
        {
            FamilyName = familyName,
            Bscode = bscode,
            FuncGroup = funcgroup,
            FuncTypes = functype,
            DuctSize = size,
            SystemName = systemname,
            ID = ductl.Id.IntegerValue
        });
    }
    var FamilyPipeInfo = new List<FamilyPipe>();
    foreach (var pipel in pipeLine)
    {
        string familyName = pipel.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "-";
        string bscode = pipel.LookupParameter("BS_Код изделия")?.AsValueString() ?? "-";
        string functype = pipel.LookupParameter("MEP_Функциональный тип")?.AsValueString() ?? "-";
        string funcgroup = pipel.LookupParameter("MEP_Функциональная группа")?.AsValueString() ?? "-";
        string length = pipel.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsValueString() ?? "-";
        string systemname = pipel.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "-";
        string mepdiameter1 = pipel.LookupParameter("MEP_Диаметр 1")?.AsValueString() ?? "-";
        string size = pipel.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
        string insulationPipeThickness = pipel.get_Parameter(BuiltInParameter.RBS_INSULATION_THICKNESS_FOR_PIPE)?.AsValueString() ?? "-";

        FamilyPipeInfo.Add(new FamilyPipe
        {
            FamilyName = familyName,
            Bscode = bscode,
            FuncGroup = funcgroup,
            FuncTypes = functype,
            Length = length,
            Section = numbsection,
            Part = numbpart,
            SystemName = systemname,
            MepDiameter1 = mepdiameter1,
            PipeSize = size,
            ThicknessPipeInsolation = insulationPipeThickness,
            ID = pipel.Id.IntegerValue
        });
    }
    var FamilyPipeInstInfo = new List<FamilyPipe>();
    foreach (var pipel in pipeelements)
    {
        string familyName = pipel.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM)?.AsValueString() ?? "-";
        string bscode = pipel.LookupParameter("BS_Код изделия")?.AsValueString() ?? "-";
        string functype = pipel.LookupParameter("MEP_Функциональный тип")?.AsValueString() ?? "-";


        string size;
        if (pipel.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_PipeFitting)
        {
            // Если это фитинг — берём из RBS_CALCULATED_SIZE
            size = pipel.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE)?.AsString() ?? "-";
        }
        else
        {
            // Если это НЕ фитинг — берём из параметра "Размер"
            size = pipel.LookupParameter("Размер")?.AsValueString() ?? "-";
        }


















        string funcgroup = pipel.LookupParameter("MEP_Функциональная группа")?.AsValueString() ?? "-";
        string length = pipel.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH)?.AsValueString() ?? "-";
        string systemname = pipel.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "-";
        string mepdiameter1 = pipel.LookupParameter("MEP_Диаметр 1")?.AsValueString() ?? "-";

        FamilyPipeInfo.Add(new FamilyPipe
        {
            FamilyName = familyName,
            Bscode = bscode,
            FuncGroup = funcgroup,
            FuncTypes = functype,
            Length = length,
            Section = numbsection,
            Part = numbpart,
            SystemName = systemname,
            MepDiameter1 = mepdiameter1,
            ID = pipel.Id.IntegerValue
        });
    }

    // БЛОК 3. Экспорт в MySQL 
    //string connectionString = "Server=94.228.125.175;Database=testovk;Uid=zhukovk;Pwd=o?6JW83Xfu&pZ3;";
    //ExportToMySQL(ductData, connectionString);
    try
    {
        // строка подключения из secret.json
        string connectionString = GetConnectionStringFromSecretJson();
        ExportToMySQL1(FamilyDuctInfo, connectionString);
        ExportToMySQL2(FamilyPipeInfo, connectionString);
        TaskDialog.Show("Успех", $"Экспортировано элементов': {FamilyDuctInfo.Count + FamilyPipeInfo.Count}");
    }
    catch (Exception ex)
    {
        TaskDialog.Show("Ошибка", $"Не удалось экспортировать данные: {ex.Message}");
        return Result.Failed;
    }
    return Result.Succeeded;
}
catch (Exception ex)
{
    message = ex.Message;
    TaskDialog.Show("Ошибка", $"Ошибка экспорта: {ex.Message}");
    return Result.Failed;
}
        }


        public List<Element> FamilyCollctor()
{
    return
        }




// Метод экспорта в MySQL 
// получаение секретов из json
public string GetConnectionStringFromSecretJson()
{
    try
    {
        string secretPath = @"\\picompany.ru\pikp\Dep\DR\DEV_DLLS\Новая папка\secrets.json";


        if (!File.Exists(secretPath))
            throw new FileNotFoundException("Файл secrets.json не найден по указанному пути");

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddJsonFile(secretPath, optional: false)
            .Build();

        string connectionString = configuration.GetConnectionString("MySQLConnection");

        if (string.IsNullOrEmpty(connectionString))
            throw new Exception("Строка подключения не найдена в secrets.json");

        return connectionString;
    }
    catch (Exception ex)
    {
        throw new Exception($"Ошибка при чтении secrets.json: {ex.Message}");
    }
}
private void ExportToMySQL1(List<FamilyDuct> ductData, string connectionString)
{
    using (MySqlConnection connection = new MySqlConnection(connectionString))
    {
        connection.Open();
        string sql = @"INSERT INTO ducttest (FamilyName, Bscode, FuncGroup, FuncTypes, FuncGroupType, Length, Section, Part, SystemName, DuctSize, DuctEI, ThicknessDuckInsolation, ID)
                    VALUES (@familyName, @bscode, @funcgroup ,@functypes ,@funcgrouptype ,@length ,@section ,@part ,@systemname ,@ductsize ,@ductei, @thicknessduckinsolation, @id);";

        foreach (var duct in ductData)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@familyName", duct.FamilyName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@bscode", duct.Bscode ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@funcgroup", duct.FuncGroup ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@functypes", duct.FuncTypes ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@funcgrouptype", duct.FuncGroupType ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@length", duct.Length);
                command.Parameters.AddWithValue("@section", duct.Section ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@part", duct.Part ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@systemname", duct.SystemName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ductsize", duct.DuctSize ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@ductei", duct.DuctEI ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@thicknessduckinsolation", duct.ThicknessDuckInsolation);
                command.Parameters.AddWithValue("@id", duct.ID);
                command.ExecuteNonQuery();
            }
        }
    }
}
private void ExportToMySQL2(List<FamilyPipe> pipeData, string connectionString)
{
    using (MySqlConnection connection = new MySqlConnection(connectionString))
    {
        connection.Open();
        string sql = @"INSERT INTO pipetest (FamilyName, Bscode, FuncGroup, FuncTypes, FuncGroupType, PipeSize, MepDiameter1, SystemName, Length, Section, Part, ThicknessPipeInsolation, ID)
                    VALUES (@familyname, @bscode, @funcgroup, @functypes, @funcgrouptype, @pipesize, @mepdiameter1, @systemname, @length, @section, @part, @thicknesspipeinsolation, @id);";

        foreach (var pipe in pipeData)
        {
            using (MySqlCommand command = new MySqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@familyName", pipe.FamilyName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@bscode", pipe.Bscode ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@funcgroup", pipe.FuncGroup ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@functypes", pipe.FuncTypes ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@funcgrouptype", pipe.FuncGroupType ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@length", pipe.Length);
                command.Parameters.AddWithValue("@section", pipe.Section ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@part", pipe.Part ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@systemname", pipe.SystemName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@pipesize", pipe.PipeSize ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@mepdiameter1", pipe.MepDiameter1 ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@thicknesspipeinsolation", pipe.ThicknessPipeInsolation);
                command.Parameters.AddWithValue("@id", pipe.ID);
                command.ExecuteNonQuery();
            }
        }
    }
}
    }
}
 OST_PipeFitting
                    OST_PipeAccessory
                    OST_PlumbingFixtures
                    OST_PipeCurves
                    OST_PipeInsulations
                OST_DuctCurves
                    OST_MechanicalEquipment
                    OST_DuctAccessory
                    OST_DuctInsulations














 */ 