using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitToMySQL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FamilyTransfer.ClassLibrary
{
    internal class FindingLevels
    {
        private Document doc;
        private Dictionary<string, Level> _levels;

        public class LevelElements
        {
            public string Name { get; set; }
            public string Max { get; set; }
            public string Min { get; set; }

        }
        //public Dictionary(string name1, Level) CollectAllLevels(Document doc)
        //{
        //    return new FilteredElementCollector(doc)
        //        .OfClass(typeof(Level))
        //        .Cast<Level>()
        //        .ToDictionary(l => l.Name, l => l);
        //}

        public Dictionary<string, Level> CollectAllLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToDictionary(l => l.Name, l => l);
        }

        //public Dictionary<string, LevelElements> CreateLevelBox(Document doc, SectionCache cc)
        //{
            
            
        //}

        // --- ПОЛУЧИЛИ ВСЕ УРОВНИ ИЗ КФ/ИЗ СВЯЗАННОЙ МОДЕЛИ --- //

        // --- ПОЛУЧИЛИ ВСЕ ФОРМЫ ИЗ КФ/ИЗ СВЯЗАННОЙ МОДЕЛИ --- //

        // --- СОЗДАЛИ КЛЕТКИ ОГРАНИЧЕННЫЕ ФОРМОЙ КФ И УРОВНЯМИ --- //

        // --- ПРОВЕРИЛИ ОТНОСИТСЯ ЛИ ЭЛЕМЕНТ(БОЛЬШАЯ ЧАСТЬ) К КЛЕТКЕ
        // - считываем клетки с низу, если в клетке боле 100элементов. считаем каждый пока не дойдем до относительно пустого, это будут подвал(нет) 1,2 этаж(бкт)
        // - считываем клетки вверх до первой попавшейся
        // 














    }





}
