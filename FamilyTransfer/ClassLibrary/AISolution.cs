using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace RevitToMySQL
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public static class AISolution
    {
        public static (string section, string part) GetBuildingSectionForElement(Document doc, Element element)
        {
            // ДОБАВИЛ: Проверка входных параметров
            if (doc == null || element == null)
            {
                TaskDialog.Show("Ошибка", "Документ или элемент не заданы");
                return ("-", "-");
            }

            // Находим связанную модель с формами
            RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(link => System.Text.RegularExpressions.Regex.IsMatch(
                    link.Name, @"^\d{4}-\d\.rvt"));

            // ДОБАВИЛ: Проверка на наличие связанной модели
            if (linkInstance == null)
            {
                TaskDialog.Show("Информация", "Связанная модель КФ-файла не найдена");
                return ("-", "-");
            }

            Document linkDoc = linkInstance.GetLinkDocument();

            // ДОБАВИЛ: Проверка что связанный документ доступен
            if (linkDoc == null)
            {
                TaskDialog.Show("Ошибка", "Не удалось получить связанный документ");
                return ("-", "-");
            }

            // --- Работаем с формами жилья
            // Получаем форм секции ЖИЛЬЯ
            List<Element> sectionCivil = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Mass)
                .WhereElementIsNotElementType()
                .Where(t =>
                {
                    // ИСПРАВИЛ: Добавил проверку на null для параметра
                    Parameter bds = t.LookupParameter("BDS_Building");
                    if (bds == null) return false; // ДОБАВИЛ: проверка
                    string bdss = bds.AsString() ?? "";
                    return !System.Text.RegularExpressions.Regex.IsMatch(bdss, @"[AaАа]");
                })
                .ToList();

            // ДОБАВИЛ: Проверка что есть секции жилья
            if (sectionCivil.Count == 0)
            {
                TaskDialog.Show("Информация", "Секции жилья не найдены");
                return ("-", "-");
            }

            double countSectionCivil = sectionCivil.Count;

            //создадим коллекцию боксов с параметрами, чтоб потом объединить с новыми боксами стоянки
            List<(string section, string part, BoundingBoxXYZ form)> civilbounds = new List<(string, string, BoundingBoxXYZ)>();
            foreach (Element civ in sectionCivil)
            {
                // ИСПРАВИЛ: Добавил проверки на null для параметров
                string s = civ.LookupParameter("BDS_Building")?.AsString() ?? "-";
                string p = civ.LookupParameter("BDS_BuildingPart")?.AsString() ?? "-";

                // ДОБАВИЛ: Проверка что BoundingBox существует
                BoundingBoxXYZ b = civ.get_BoundingBox(null);
                if (b == null) continue; // Пропускаем элементы без BoundingBox

                civilbounds.Add((s, p, b));
            }

            // ---

            // --- Работаем с формой стоянки
            // Получаем форм секцию СТОЯНКИ
            Element sectionParking = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Mass)
                .WhereElementIsNotElementType()
                .FirstOrDefault(t =>
                {
                    // ИСПРАВИЛ: Добавил проверку на null для параметра
                    Parameter bds = t.LookupParameter("BDS_Building");
                    if (bds == null) return false; // ДОБАВИЛ: проверка
                    string bdss = bds.AsString() ?? "";
                    return System.Text.RegularExpressions.Regex.IsMatch(bdss, @"[AaАа]");
                });

            // ДОБАВИЛ: Проверка что форма стоянки найдена
            if (sectionParking == null)
            {
                TaskDialog.Show("Информация", "Форма стоянки не найдена");
                // Продолжаем работу только с жильем
            }
            else
            {
                // ДОБАВИЛ: Проверка что BoundingBox парковки существует
                BoundingBoxXYZ parkform = sectionParking.get_BoundingBox(null);
                if (parkform == null)
                {
                    TaskDialog.Show("Ошибка", "У формы стоянки нет BoundingBox");
                }
                else
                {
                    // ИСПРАВИЛ: Добавил проверку деления на ноль
                    if (countSectionCivil == 0)
                    {
                        TaskDialog.Show("Ошибка", "Нет секций для разделения парковки");
                        return ("-", "-");
                    }



                    double lenghtX = (parkform.Max.X - parkform.Min.X) / countSectionCivil;

                    //начинаем резать форму стоянки на несколько боксов, по числу секций в корпусе, создаем коллекцию этих форм
                    List<(string section, string part, BoundingBoxXYZ form)> bounds = new List<(string, string, BoundingBoxXYZ)>();

                    for (int i = 1; i <= countSectionCivil; i++)
                    {
                        BoundingBoxXYZ b = new BoundingBoxXYZ();
                        b.Max = new XYZ(parkform.Min.X + lenghtX * i, parkform.Max.Y, parkform.Max.Z);
                        b.Min = new XYZ(parkform.Min.X + lenghtX * (i - 1), parkform.Min.Y, parkform.Min.Z);

                        // ИСПРАВИЛ: Проверка индекса в пределах массива
                        if (i - 1 < sectionCivil.Count)
                        {
                            string parksecname = sectionCivil[i - 1].LookupParameter("BDS_Building")?.AsString() ?? "";
                            string parkpartame = sectionCivil[i - 1].LookupParameter("BDS_BuildingPart")?.AsString() ?? "";
                            bounds.Add((parksecname, parkpartame, b));
                        }
                    }

                    // ИСПРАВИЛ: Добавил проверки на null для параметров парковки
                    string section = sectionParking.LookupParameter("BDS_Building")?.AsString() ?? "";
                    string part = sectionParking.LookupParameter("BDS_BuildingPart")?.AsString() ?? "";

                    // объединяем 2 коллекции: боксы жилья и боксы порезанного паркинга 
                    civilbounds.AddRange(bounds);
                }
            }
            // ---

            // ДОБАВИЛ: Проверка что есть bounding box'ы для проверки
            if (civilbounds.Count == 0)
            {
                TaskDialog.Show("Информация", "Нет bounding box'ов для проверки пересечений");
                return ("-", "-");
            }

            // ДОБАВИЛ: Проверка что у элемента есть BoundingBox
            BoundingBoxXYZ elemBox = element.get_BoundingBox(null);
            if (elemBox == null)
            {
                TaskDialog.Show("Информация", "У элемента нет BoundingBox");
                return ("-", "-");
            }

            //1 Этап. Проверка пересечений
            foreach (var bb in civilbounds)
            {
                if (bb.form == null) continue; // ДОБАВИЛ: Пропускаем null bounding boxes

                if (Intersection(bb.form, elemBox))
                {
                    string sectionnubmer = bb.section;
                    string partnumber = bb.part;
                    return (sectionnubmer, partnumber);
                }
            }

            //2 Этап. Занесение в элементы стоянки параметров "секции" & "корпуса"
            // ИСПРАВИЛ: Убрал отрицание для правильной логики
            if (IsElementCivil(element))
            {
                // Для гражданских элементов возвращаем значения из парковки если они есть
                if (sectionParking != null)
                {
                    string section = sectionParking.LookupParameter("BDS_Building")?.AsString() ?? "";
                    string part = sectionParking.LookupParameter("BDS_BuildingPart")?.AsString() ?? "";
                    return (section, part);
                }
            }

            return ("-", "-");
        }

        private static bool IsElementCivil(Element elem)
        {
            Parameter param = elem.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param == null) return false;

            string value = param.AsString() ?? "";

            bool hasSystem = System.Text.RegularExpressions.Regex.IsMatch(value, @"^[TТ](14|24|15|25)$");
            bool hasLetterA = System.Text.RegularExpressions.Regex.IsMatch(value, @"[аАaA]");

            return hasSystem || hasLetterA;
        }

        private static bool Intersection(BoundingBoxXYZ bbf1, BoundingBoxXYZ bbe2)
        {
            if (bbf1 == null || bbe2 == null) return false;

            bool intersectX = (bbf1.Min.X <= bbe2.Max.X) && (bbf1.Max.X >= bbe2.Min.X);
            bool intersectY = (bbf1.Min.Y <= bbe2.Max.Y) && (bbf1.Max.Y >= bbe2.Min.Y);
            bool intersectZ = (bbf1.Min.Z <= bbe2.Max.Z) && (bbf1.Max.Z >= bbe2.Min.Z);

            return intersectX && intersectY && intersectZ;
        }
    }
}










/*
 * 
 * using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.UI;
using FamilyTransfer;
using MySql.Data.MySqlClient;
using Mysqlx.Crud;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Mysqlx.Datatypes.Scalar.Types;
using static System.Collections.Specialized.BitVector32;

namespace RevitToMySQL
{
    public class EquipmentDetector
    {
        public static List<FamilyInfo> Equip(List<Element> familyList)
        {
            List<FamilyInfo> familyEquipment = new List<FamilyInfo>();

            if (familyList == null || !familyList.Any())
                return familyEquipment;

            string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("DBEDEQU");

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {


                connection.Open();
                string groupQuery = @"SELECT subeq_pipes_sub_ucode, 
                                                 subeq_pipes_sub_modification,
                                                 subeq_pipes_sub_workpressure,
                                                 subeq_pipes_sub_size,
                                                 subeq_pipes_formula
                                        FROM Equip_subequipment_pipes 
                                        WHERE subeq_pipes_prnt_ucode = @гсщ"
                ;

                using (MySqlCommand groupCommand = new MySqlCommand(groupQuery, connection))
                {
                    groupCommand.Parameters.Add("@equip_name", MySqlDbType.VarChar);
                    foreach (Element e in familyList)
                    {


                        ElementId typeId = e.GetTypeId();
                        Element familyType = null;

                        if (typeId != ElementId.InvalidElementId)
                        {
                            familyType = e.Document.GetElement(typeId);
                        }

                        string familyname = familyType?.LookupParameter("BS_Код изделия")?.AsString() ?? "-";
                        string familyModification = familyType?.LookupParameter("MEP_Модификация")?.AsString() ?? "-";

                        if (string.IsNullOrEmpty(familyname) || familyname == "-")
                            continue;



                        groupCommand.Parameters["@equip_name"].Value = familyname.Trim();
                        object bscodeResult = groupCommand.ExecuteScalar();

                        if (bscodeResult != null && bscodeResult != DBNull.Value)
                        {
                            string bscode = bscodeResult.ToString();
                            familyEquipment.Add(new FamilyInfo
                            {
                                FamilyName = "комплектация тест",
                                Ucode = bscode,
                                Modification = "2",
                                WorkPressure = "2",
                                Size = "2",
                                Amount = "2"
                            });
                        }



                    }
                }


            }
            return familyEquipment;
        }
    }
}






 * 
 * 
 * 
 * 
 * 
 * 
 * */
/*
 * using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static RevitToMySQL.DatabaseCache;

namespace RevitToMySQL
{
    public class EquipmentDetector
    {
        public static List<FamilyInfo> Equip(List<Element> familyList, Document doc,
                                              DatabaseCache dbCache, SectionCache sectionCache, Dictionary<int, FamilyInfo> parentsData)
        {
            // ========== ДИАГНОСТИКА ==========
            string logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"EquipDebug_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            System.IO.File.AppendAllText(logPath, $"=== НАЧАЛО ДИАГНОСТИКИ {DateTime.Now} ===\n");
            System.IO.File.AppendAllText(logPath, $"familyList.Count = {familyList?.Count ?? 0}\n");
            System.IO.File.AppendAllText(logPath, $"parentsData?.Count = {parentsData?.Count ?? 0}\n");
            System.IO.File.AppendAllText(logPath, $"dbCache = {(dbCache == null ? "null" : "ok")}\n");
            // =================================


            if (familyList == null || !familyList.Any() || dbCache == null)
                return new List<FamilyInfo>();

            var result = new List<FamilyInfo>();

            //// ДОБАВЛЕНО: счетчики для статистики
            int processedCount = 0;
            int foundParentCount = 0;
            int foundSubEquipmentCount = 0;
            //// КОНЕЦ ДОБАВЛЕНИЯ


            try
            {
                foreach (Element e in familyList)
                {
                    processedCount++;  //// ДОБАВЛЕНО

                    try
                    {

                        Element familyType = null;
                        ElementId typeId = e.GetTypeId();
                        if (typeId != ElementId.InvalidElementId)
                        {
                            familyType = e.Document.GetElement(typeId);
                        }
                        int familyID = e.Id.IntegerValue;

                        //var parent = parentsData(typeId);

                        FamilyInfo pipe = new FamilyInfo();
                        FamilyInfo pipe2 = parentsData[familyID];


                        //string parentOrder = pipe2.Order; //
                        



                        string sorting = pipe2.SortingCode.ToString();//
                        string systemName = pipe2.SystemName;
                        string section = pipe2.Section;
                        string part = pipe2.Part;
                        string ucode = familyType?.LookupParameter("BS_Код изделия")?.AsString()?.Trim() ?? "-";
                        string modification = familyType?.LookupParameter("MEP_Модификация")?.AsString()?.Trim() ?? "-";

                        var subEquipment = dbCache.GetSubEquipment(ucode, modification);
                        if (subEquipment == null || subEquipment.Count == 0) continue;

                        string familySize = ExtractSize(familyType);
                        //string systemName = e.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM)?.AsString() ?? "-";

                        //var sectionInfo = sectionCache.GetSectionAndPartForElement(e);
                        //string section = sectionInfo.section ?? "-";
                        //string part = sectionInfo.part ?? "-";

                        foreach (var sub in subEquipment)
                        {
                            //string orderSub = sub.OrderEquip;                            
                            //string order = MakeOrder(e, parentsData, orderSub);


                            if (ShouldInclude(e, familyType, sub))
                            {
                                result.Add(new FamilyInfo
                                {
                                    FamilyName = "комплектация тест",
                                    Ucode = sub.Ucode ?? "",
                                    Modification = sub.Modification ?? "",
                                    WorkPressure = sub.WorkPressure ?? "",
                                    Size = GetFinalSize(sub.Size, familySize),
                                    Amount = sub.Amount ?? "-",
                                    SystemName = systemName,
                                    ID = familyID,
                                    Section = section,
                                    Part = part,
                                    //Order=sub.OrderEquip
                                    //Order = order
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка обработки элемента: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", $"Ошибка в EquipmentDetector: {ex.Message}");
            }

            return result;
        }
        private static string MakeOrder(Element e, Dictionary<int, FamilyInfo> parentsData, string dirtyEquipOrder)
        {
            FamilyInfo family = parentsData[e.Id.IntegerValue];
            string parentOrder = family.Order;
            

            if (dirtyEquipOrder.All(char.IsDigit) && dirtyEquipOrder.Length == 2)
            {
                string finalOrder = parentOrder.Substring(0, 4) + dirtyEquipOrder;
                return finalOrder;
            }

            if (dirtyEquipOrder.All(char.IsDigit) && dirtyEquipOrder.Length == 1)
            {
                string finalOrder = parentOrder.Substring(0, 4) + 0 + dirtyEquipOrder;
                return finalOrder;
            }
             return "-";
        }



        private static string ExtractSize(Element familyType)
        {
            if (familyType == null) return "-";

            string sizeDirty = familyType.LookupParameter("MEP_Диаметр 1")?.AsValueString()?.Trim() ?? "-";
            Match match = Regex.Match(sizeDirty, @"[\d\.]+");
            return match.Success ? match.Value : "-";
        }

        private static string GetFinalSize(string dbSize, string familySize)
        {
            if (!string.IsNullOrEmpty(dbSize) &&
                dbSize.Trim().Equals("asparent", StringComparison.OrdinalIgnoreCase))
            {
                return familySize;
            }
            return dbSize ?? "-";
        }

        private static bool ShouldInclude(Element element, Element familyType, DatabaseCache.SubEquipment sub)
        {
            if (string.IsNullOrEmpty(sub.TriggerInstruction) || sub.TriggerInstruction == "нет")
                return true;

            if (!sub.TriggerInstruction.Contains("param"))
                return false;

            try
            {
                var (paramType, paramName, expectedValue) = ParseTrigger(sub.TriggerInstruction);
                string actualValue = GetParameterValue(element, familyType, paramType, paramName);

                return actualValue == expectedValue;
            }
            catch
            {
                return false;
            }
        }

        private static (string type, string name, string value) ParseTrigger(string trigger)
        {
            string[] parts = trigger.Split(':');
            if (parts.Length < 3)
                return ("", "", "");

            return (
                parts[0].Replace("param{", ""),
                parts[1],
                parts[2].Replace("{", "").Replace("}", "")
            );
        }


        private static string GetParameterValue(Element element, Element familyType, string paramType, string paramName)
        {
            try
            {
                switch (paramType)
                {
                    case "num":
                        string val = element?.LookupParameter(paramName)?.AsString()
                                   ?? familyType?.LookupParameter(paramName)?.AsString();
                        if (string.IsNullOrEmpty(val))
                        {
                            val = element?.LookupParameter(paramName)?.AsValueString()
                                ?? familyType?.LookupParameter(paramName)?.AsValueString();
                        }
                        return val?.Replace(" ", "")?.Replace("мм", "") ?? "";

                    case "bool":
                        int? boolVal = element?.LookupParameter(paramName)?.AsInteger()
                                     ?? familyType?.LookupParameter(paramName)?.AsInteger();
                        return boolVal == 1 ? "true" : "false";

                    default:
                        return "";
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
*/

/*using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
using System.IO;

/*
Текущая скорость: 
Тест 1: 73 024 эл. за 1001 сек
Тест 2: 67 430 эл. за 817 сек
Тест 3: 110 600 эл. за 1418 сек
Тест 4: 172 581 эл. за 2472 сек 
Тест 5: 1269 эл. за 20 сек 

В инструкции указать об обязательности подгрузки КФ
*/

/*
namespace RevitToMySQL
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExportDuctsToMySQL : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {

                TaskDialog.Show("Программа запущена", "Начало экспорта данных...");

                // БЛОК 1: Загрузка данные из БД MySQL
                DatabaseCache dbCache = DatabaseCache.LoadAllData();

                // БЛОК 2: Кэш секций
                SectionCache sectionCache = new SectionCache(doc);

                // БЛОК 3: Получение связанные модели




                // Находим связанную модель с формами

                /*
                RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .FirstOrDefault(link => System.Text.RegularExpressions.Regex.IsMatch(
                        link.Name, @"^\d{4}-\d\.rvt"));
                



                List<RevitLinkInstance> links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .ToList();

                List<Document> linkDocuments = new List<Document>();
                foreach (RevitLinkInstance link in links)
                {
                    Document linkDoc = link.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        linkDocuments.Add(linkDoc);
                    }
                }




                // Шаг 4: Собираем элементы из основного документа
                List<Element> pipeElements = ElementCollectorHelper.GetPipeElements(doc);
                //List<Element> ductElements = ElementCollectorHelper.GetDuctElements(doc);


                /*
                // Шаг 5: Собираем элементы из связанных моделей
                foreach (Document linkDoc in linkDocuments)
                {
                    try
                    {

                // вот здесь 
                        pipeElements.AddRange(ElementCollectorHelper.GetPipeElements(linkDoc));
                        ductElements.AddRange(ElementCollectorHelper.GetDuctElements(linkDoc));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка обработки связанной модели: {ex.Message}");
                    }
                }
                


                // Шаг 6: Обрабатываем параметры
                List<FamilyInfo> pipeData = ParameterProcessor.GetParameters(pipeElements, doc, dbCache, sectionCache);
                //List<FamilyInfo> ductData = ParameterProcessor.GetParameters(ductElements, doc, dbCache, sectionCache);

                //------ВАРИАНТ С РОДИТЕЛЯМИ---------------
                var pipeParentData = pipeData.ToDictionary(x => x.ID, x => x);
                //var ductParentData = ductData.ToDictionary(x => x.ID, x => x);


                // Шаг 7: Получаем комплектации
                List<FamilyInfo> pipeEquipment = EquipmentDetector.Equip(pipeElements, doc, dbCache, sectionCache, pipeParentData);
                //List<FamilyInfo> ductEquipment = EquipmentDetector.Equip(ductElements, doc, dbCache, sectionCache);

                // Шаг 8: Сортировка
                pipeData = pipeData.OrderBy(x => x.SortingCode)
                                   .ThenBy(x => x.SystemName)
                                   .ThenBy(x => x.Order)
                                   .ToList();

                //ductData = ductData.OrderBy(x => x.SortingCode)
                //                    .ThenBy(x => x.SystemName)
                //                    .ThenBy(x => x.Order)
                //                    .ToList();

                // Шаг 9: Экспорт в MySQL
                string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("testovk");

                MySqlExporter.ExportToMySQL(pipeData, "pipetest", connectionString);
                //MySqlExporter.ExportToMySQL(ductData, "ducttest", connectionString);
                MySqlExporter.ExportToMySQL(pipeEquipment, "pipetest", connectionString);
                //MySqlExporter.ExportToMySQL(ductEquipment, "ducttest", connectionString);

                stopwatch.Stop();
                TimeSpan ts = stopwatch.Elapsed;

                TaskDialog.Show("Успех", $"Экспортировано элементов:\n" +
                                         $"Трубы: {pipeData.Count}\n" +
                                         //$"Воздуховоды: {ductData.Count}\n" +
                                         $"Комплектации труб: {pipeEquipment.Count}\n" +
                                         //$"Комплектации воздуховодов: {ductEquipment.Count}\n"+
                                         //$"Экспортировано элементов: {pipeData.Count + ductData.Count}\n"+
                                         $"Экспорт выполнен за {ts.TotalSeconds:F2} секунд");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                message = ex.Message;
                TaskDialog.Show("Ошибка",
            $"Ошибка через {stopwatch.Elapsed.TotalSeconds:F2} сек: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}*/






/*
 * using System;
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
            //public string OrderEquip { get; set; }
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
                                subeq_pipes_triggers, subeq_pipes_formula
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
                            Ucode = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Modification = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            WorkPressure = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            Size = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Amount = reader.IsDBNull(6) ? null : reader.GetString(6),
                            TriggerInstruction = reader.IsDBNull(7) ? null : reader.GetString(7),
                            //OrderEquip = reader.IsDBNull(8) ? "00" : reader.GetString(8)
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
*/






