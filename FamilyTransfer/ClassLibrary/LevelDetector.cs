using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace VentilationEquipmentParser
{
    public class DensityAnalyzer
    {
        private Document _doc;
        private Dictionary<string, Level> _levels;

        public DensityAnalyzer(Document doc)
        {
            _doc = doc;
            _levels = CollectAllLevels(doc);
        }


        /// Собирает все уровни в документе        
        private Dictionary<string, Level> CollectAllLevels(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .ToDictionary(l => l.Name, l => l);
        }

        /// Находит область с максимальной плотностью элементов между двумя уровнями

        public DensityResult FindDensestArea(string levelName1, string levelName2,
                                            double gridSize = 1.0,
                                            BuiltInCategory category = BuiltInCategory.OST_MechanicalEquipment)
        {
            // Получаем уровни
            if (!_levels.TryGetValue(levelName1, out Level level1) ||
                !_levels.TryGetValue(levelName2, out Level level2))
            {
                throw new ArgumentException("Уровни не найдены");
            }

            // Определяем Z-границы (нижний и верхний уровень)
            double zMin = Math.Min(level1.Elevation, level2.Elevation);
            double zMax = Math.Max(level1.Elevation, level2.Elevation);

            // Собираем все элементы указанной категории
            var elements = new FilteredElementCollector(_doc)
                .OfCategory(category)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(e => IsElementBetweenLevels(e, zMin, zMax))
                .ToList();

            if (!elements.Any())
            {
                return new DensityResult { TotalElements = 0 };
            }

            // Определяем область поиска по X и Y
            BoundingBoxXYZ overallBBox = GetOverallBoundingBox(elements);

            // Разбиваем область на сетку и считаем плотность
            var gridCells = AnalyzeGridDensity(elements, overallBBox, gridSize);

            // Находим ячейку с максимальной плотностью
            var maxCell = gridCells.OrderByDescending(c => c.ElementCount).FirstOrDefault();

            return new DensityResult
            {
                TotalElements = elements.Count,
                DensestCell = maxCell,
                ZMin = zMin,
                ZMax = zMax,
                GridSize = gridSize,
                BoundingBox = overallBBox
            };
        }

        /// Проверяет, находится ли элемент между двумя уровнями по Z
        private bool IsElementBetweenLevels(Element element, double zMin, double zMax)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null) return false;

            double elementZMin = bbox.Min.Z;
            double elementZMax = bbox.Max.Z;

            // Элемент считается между уровнями, если его центр или большая часть находится в диапазоне
            double centerZ = (elementZMin + elementZMax) / 2;
            return centerZ >= zMin && centerZ <= zMax;
        }
        /// Получает общий габаритный контейнер для всех элементов
        private BoundingBoxXYZ GetOverallBoundingBox(List<Element> elements)
        {
            var bbox = new BoundingBoxXYZ();

            if (!elements.Any()) return bbox;

            // Инициализируем значения
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var element in elements)
            {
                var elementBBox = element.get_BoundingBox(null);
                if (elementBBox == null) continue;

                // Используем только координаты X и Y (план)
                minX = Math.Min(minX, elementBBox.Min.X);
                minY = Math.Min(minY, elementBBox.Min.Y);
                maxX = Math.Max(maxX, elementBBox.Max.X);
                maxY = Math.Max(maxY, elementBBox.Max.Y);
            }

            bbox.Min = new XYZ(minX, minY, 0);
            bbox.Max = new XYZ(maxX, maxY, 0);

            return bbox;
        }
        /// Анализирует плотность элементов по сетке
        private List<GridCell> AnalyzeGridDensity(List<Element> elements,
                                                  BoundingBoxXYZ overallBBox,
                                                  double gridSize)
        {
            var gridCells = new List<GridCell>();

            // Определяем размеры сетки в плане
            double xMin = overallBBox.Min.X;
            double xMax = overallBBox.Max.X;
            double yMin = overallBBox.Min.Y;
            double yMax = overallBBox.Max.Y;

            int xCells = (int)Math.Ceiling((xMax - xMin) / gridSize);
            int yCells = (int)Math.Ceiling((yMax - yMin) / gridSize);

            // Создаем сетку
            for (int i = 0; i < xCells; i++)
            {
                for (int j = 0; j < yCells; j++)
                {
                    double cellXMin = xMin + i * gridSize;
                    double cellXMax = xMin + (i + 1) * gridSize;
                    double cellYMin = yMin + j * gridSize;
                    double cellYMax = yMin + (j + 1) * gridSize;

                    var cell = new GridCell
                    {
                        XMin = cellXMin,
                        XMax = cellXMax,
                        YMin = cellYMin,
                        YMax = cellYMax,
                        CenterX = (cellXMin + cellXMax) / 2,
                        CenterY = (cellYMin + cellYMax) / 2,
                        Row = j,
                        Column = i
                    };

                    // Подсчитываем элементы в ячейке
                    foreach (var element in elements)
                    {
                        var elementBBox = element.get_BoundingBox(null);
                        if (elementBBox == null) continue;

                        // Проверяем пересечение (или попадание центра)
                        if (IsElementInCell(elementBBox, cell))
                        {
                            cell.ElementCount++;
                            cell.Elements.Add(element);
                        }
                    }

                    gridCells.Add(cell);
                }
            }

            return gridCells;                                                                                                                                                                                                                                      
        }
        /// Проверяет, находится ли элемент в ячейке
        private bool IsElementInCell(BoundingBoxXYZ elementBBox, GridCell cell)
        {
            // Проверяем пересечение габаритных контейнеров
            double elementCenterX = (elementBBox.Min.X + elementBBox.Max.X) / 2;
            double elementCenterY = (elementBBox.Min.Y + elementBBox.Max.Y) / 2;

            return elementCenterX >= cell.XMin && elementCenterX <= cell.XMax &&
                   elementCenterY >= cell.YMin && elementCenterY <= cell.YMax;
        }
    }
    /// Класс для хранения ячейки сетки
    public class GridCell
    {
        public double XMin { get; set; }
        public double XMax { get; set; }
        public double YMin { get; set; }
        public double YMax { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public int Row { get; set; }
        public int Column { get; set; }
        public int ElementCount { get; set; } = 0;
        public List<Element> Elements { get; set; } = new List<Element>();

        public XYZ Center => new XYZ(CenterX, CenterY, 0);
    }
    /// Класс для хранения результатов анализа
    public class DensityResult
    {
        public int TotalElements { get; set; }
        public GridCell DensestCell { get; set; }
        public double ZMin { get; set; }
        public double ZMax { get; set; }
        public double GridSize { get; set; }
        public BoundingBoxXYZ BoundingBox { get; set; }

        public bool HasResults => TotalElements > 0 && DensestCell != null;

        public override string ToString()
        {
            if (!HasResults) return "Элементы не найдены";

            return $"Найдено элементов: {TotalElements}\n" +
                   $"Диапазон высот: {ZMin:F2} - {ZMax:F2} м\n" +
                   $"Ячейка с макс. плотностью:\n" +
                   $"  - Позиция: X={DensestCell.CenterX:F2}, Y={DensestCell.CenterY:F2}\n" +
                   $"  - Количество элементов: {DensestCell.ElementCount}\n" +
                   $"  - Размер ячейки: {GridSize} м";
        }
    }
}