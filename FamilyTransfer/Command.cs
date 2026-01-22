using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
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

            TaskDialog.Show("Программа запущена", "Программа запущена123");

            try
            {
                //---- БЛОК 0. Получение связанных моделей ОВ ---

                List<RevitLinkInstance> linksOV = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(link => System.Text.RegularExpressions.Regex.IsMatch(
                    link.Name, @"^\d{4}-Р-[A-Za-zА-Яа-я]{0,3}-.+-ОВ-(ОВ[1-3])_.+$"))
                .ToList();

                List<Document> linksdocument = new List<Document>();
                if (linksOV == null)
                {
                    TaskDialog.Show("Информация", "Связанные модели ОВ не найдены");
                }
                else
                {
                    foreach (RevitLinkInstance link in linksOV)
                    {
                        Document linkdoc = link.GetLinkDocument();
                        if (linkdoc != null)
                        {
                            linksdocument.Add(linkdoc);
                        }
                    }
                }
                //----   БЛОК 1. Получение всех элементов ----
                var pipeElements = ElementCollectorHelper.GetPipeElements(doc);
                var ductElements = ElementCollectorHelper.GetDuctElements(doc);

                if (pipeElements.Count == 0 && ductElements.Count == 0)
                {
                    TaskDialog.Show("Информация", "В проекте нет элементов для экспорта.");
                }
                //получение всех эелментов из связей

                //foreach (Document l in linksdocument)
                //{
                //    var pipeElemLink = ElementCollectorHelper.GetPipeElements(l);
                //    pipeElements.AddRange(pipeElemLink);


                //    var ductElemLink = ElementCollectorHelper.GetDuctElements(l);
                //}

                //----   БЛОК 2. ОБРАБОТКА ПАРАМЕТРОВ ----
                var pipeData = ParameterProcessor.GetParameters(pipeElements, doc);
                var ductData = ParameterProcessor.GetParameters(ductElements, doc);

                //обработка элементов связей 




                ////----   БЛОК 2.1. Сортировка  ----
                //ductData.OrderBy(x => x.SortingCode)
                //    .ThenBy(x => x.SystemName)
                //    .ThenBy(x => x.Order);

                //pipeData.OrderBy(x => x.SortingCode)
                //    .ThenBy(x => x.SystemName)
                //    .ThenBy(x => x.Order);









                //----   БЛОК 3. ЭКСПОРТ В MySQL ----
                try
                {
                    string connectionString = ConnectionStringHelper.GetConnectionStringFromSecretJson("testovk");
                    MySqlExporter.ExportToMySQL(pipeData, "pipetest", connectionString);
                    MySqlExporter.ExportToMySQL(ductData, "ducttest", connectionString);
                    TaskDialog.Show("Успех", $"Экспортировано элементов: {pipeElements.Count + ductElements.Count}");
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
    }
}