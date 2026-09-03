/*
Текущая скорость: 
Тест 1: 73 024 эл. за 1001 сек
Тест 2: 67 430 эл. за 817 сек
Тест 3: 110 600 эл. за 1418 сек
Тест 4: 172 581 эл. за 2472 сек 
Тест 5: 1269 эл. за 20 сек 

Тест 2.1: 46098 эл. за 612,94
Тест 2.2: 1269 эл. за 24,41 сек - внедрение только интерфейса
Тест 2.3 1269 эл. за 3,94 сек - внедрение интерфейса и запросов  
Тест 2.4 41 588 эл. за 59,38 сек - стоянка байкальской 
Тест 2.5 76 147 эл. за 57.76 сек
Тест 2.6 41 796 эл. за 53.59 сек - стоянка байкальской через интерфейс
Тест 2.6  эл. за сек - стоянка байкальской через библиотеку классов


В инструкции указать об обязательности подгрузки КФ
*/

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyTransfer.Cache;
using FamilyTransfer.ViewModels;
using FamilyTransfer.Views;
using Microsoft.Identity.Client;
using RevitToMySQL.Caching;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace RevitToMySQL
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ExportDuctsToMySQL : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;

            if (uiDoc == null)
            {
                TaskDialog.Show("Ошибка", "Откройте проект Revit");
                return Result.Failed;
            }

            try
            {
                var viewModel = new ExportViewModel(uiDoc);
                var window = new ExportWindow
                {
                    DataContext = viewModel
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }
    }
}