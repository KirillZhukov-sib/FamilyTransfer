using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Diagnostics;
/*
Текущая скорость: 
Тест 1: 73 024 эл. за 1001 сек
Тест 2: 67 430 эл. за 817 сек
Тест 3: 110 600 эл. за 1418 сек
Тест 4: 172 581 эл. за 2472 сек 
Тест 5: 1269 эл. за 20 сек 

В инструкции указать об обязательности подгрузки КФ
*/


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
                */



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
                */


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
}
