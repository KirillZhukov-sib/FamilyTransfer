using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitToMySQL
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExportDuctsToMySQL : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                TaskDialog.Show("Программа запущена", "Начало экспорта данных...");

                // Шаг 1: Загружаем данные из БД
                DatabaseCache dbCache = DatabaseCache.LoadAllData();

                // Шаг 2: Строим кэш секций
                SectionCache sectionCache = new SectionCache(doc);

                // Шаг 3: Получаем связанные модели
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
                List<Element> ductElements = ElementCollectorHelper.GetDuctElements(doc);

                // Шаг 5: Собираем элементы из связанных моделей
                foreach (Document linkDoc in linkDocuments)
                {
                    try
                    {
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
                List<FamilyInfo> ductData = ParameterProcessor.GetParameters(ductElements, doc, dbCache, sectionCache);

                // Шаг 7: Получаем комплектации
                List<FamilyInfo> pipeEquipment = EquipmentDetector.Equip(pipeElements, doc, dbCache, sectionCache);
                List<FamilyInfo> ductEquipment = EquipmentDetector.Equip(ductElements, doc, dbCache, sectionCache);

                // Шаг 8: Сортировка
                pipeData = pipeData.OrderBy(x => x.SortingCode)
                                   .ThenBy(x => x.SystemName)
                                   .ThenBy(x => x.Order)
                                   .ToList();

                ductData = ductData.OrderBy(x => x.SortingCode)
                                    .ThenBy(x => x.SystemName)
                                    .ThenBy(x => x.Order)
                                    .ToList();

                // Шаг 9: Экспорт в MySQL
                string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("testovk");

                MySqlExporter.ExportToMySQL(pipeData, "pipetest", connectionString);
                MySqlExporter.ExportToMySQL(ductData, "ducttest", connectionString);
                MySqlExporter.ExportToMySQL(pipeEquipment, "pipetest", connectionString);
                MySqlExporter.ExportToMySQL(ductEquipment, "ducttest", connectionString);

                TaskDialog.Show("Успех", $"Экспортировано элементов:\n" +
                                         $"Трубы: {pipeData.Count}\n" +
                                         $"Воздуховоды: {ductData.Count}\n" +
                                         $"Комплектации труб: {pipeEquipment.Count}\n" +
                                         $"Комплектации воздуховодов: {ductEquipment.Count}");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", $"Ошибка экспорта: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}