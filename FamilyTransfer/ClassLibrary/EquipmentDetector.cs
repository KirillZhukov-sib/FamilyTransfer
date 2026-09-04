using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using FamilyTransfer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static RevitToMySQL.DatabaseCache;

namespace RevitToMySQL
{
    public class EquipmentDetector
    {
        /// <summary>
        /// Поиск и формирование комплектаций для переданных элементов.
        /// </summary>
        public static List<FamilyInfo> Equip(
            List<Element> familyList,
            Document doc,
            DatabaseCache dbCache,
            SectionCache sectionCache,
            Dictionary<string, FamilyInfo> parentsData,
            string wipCode)
        {
            if (familyList == null ||
                !familyList.Any() ||
                dbCache == null ||
                parentsData == null)
            {
                return new List<FamilyInfo>();
            }

            var result = new List<FamilyInfo>();

            // Контроль уникальности записей внутри текущего экспорта.
            // Ключ соответствует PRIMARY KEY таблицы:
            // ID + WipCode
            var uniqueKeys = new HashSet<string>(
                StringComparer.Ordinal);

            try
            {
                foreach (Element element in familyList)
                {
                    if (element == null)
                        continue;

                    // --------------------------------------------------
                    // Получаем тип элемента
                    // --------------------------------------------------

                    Element familyType = null;

                    ElementId typeId = element.GetTypeId();

                    if (typeId != null &&
                        typeId != ElementId.InvalidElementId)
                    {
                        familyType =
                            element.Document.GetElement(typeId);
                    }


                    // --------------------------------------------------
                    // Получаем родительскую запись
                    // --------------------------------------------------

                    string parentKey =
                        $"{element.Document.GetHashCode()}_" +
                        $"{element.Id.IntegerValue}";

                    if (!parentsData.TryGetValue(
                            parentKey,
                            out FamilyInfo parentInfo))
                    {
                        continue;
                    }


                    // --------------------------------------------------
                    // Наследуем данные от родителя
                    // --------------------------------------------------

                    int sorting =
                        parentInfo.SortingCode;

                    string systemName =
                        parentInfo.SystemName ?? "-";

                    string section =
                        parentInfo.Section ?? "-";

                    string part =
                        parentInfo.Part ?? "-";


                    // --------------------------------------------------
                    // Получаем Ucode и Modification родительского типа
                    // --------------------------------------------------

                    string ucode =
                        familyType?
                            .LookupParameter(
                                ParametersInfo.UCode)?
                            .AsString()?
                            .Trim()
                        ?? "-";

                    string modification =
                        familyType?
                            .LookupParameter(
                                ParametersInfo.Modification)?
                            .AsString()?
                            .Trim()
                        ?? "-";


                    // --------------------------------------------------
                    // Ищем комплектацию в DatabaseCache
                    // --------------------------------------------------

                    List<SubEquipment> subEquipment =
                        dbCache.GetSubEquipment(
                            ucode,
                            modification);

                    if (subEquipment == null ||
                        subEquipment.Count == 0)
                    {
                        continue;
                    }


                    // --------------------------------------------------
                    // Получаем размер родительского элемента
                    // --------------------------------------------------

                    string familySize =
                        ExtractSize(familyType);


                    // --------------------------------------------------
                    // Счётчик комплектаций
                    //
                    // Нужен для создания уникального ID,
                    // если у одного родителя несколько одинаковых
                    // комплектаций.
                    // --------------------------------------------------

                    int equipmentIndex = 0;


                    // --------------------------------------------------
                    // Обрабатываем каждую комплектацию
                    // --------------------------------------------------

                    foreach (SubEquipment sub in subEquipment)
                    {
                        if (sub == null)
                            continue;


                        // Проверяем условие TriggerInstruction
                        if (!ShouldInclude(
                                element,
                                familyType,
                                sub))
                        {
                            continue;
                        }


                        equipmentIndex++;


                        // --------------------------------------------------
                        // Формируем Order комплектации
                        // --------------------------------------------------

                        string orderSub =
                            sub.EquipOrder;

                        string order =
                            MakeOrder(
                                parentInfo.Order,
                                orderSub);


                        // --------------------------------------------------
                        // Формируем стабильный уникальный ID
                        //
                        // ВАЖНО:
                        // Нельзя использовать ID родительского Revit
                        // элемента, потому что PRIMARY KEY БД:
                        //
                        // ID + WipCode
                        //
                        // Родитель и комплектация должны иметь
                        // разные ID.
                        // --------------------------------------------------

                        int equipmentId =
                            CreateEquipmentId(
                                parentId: element.Id.IntegerValue,
                                parentWipCode: wipCode,
                                ucode: sub.Ucode,
                                modification: sub.Modification,
                                equipOrder: sub.EquipOrder,
                                index: equipmentIndex);


                        // --------------------------------------------------
                        // Создаём запись комплектации
                        // --------------------------------------------------

                        var equipItem = new FamilyInfo
                        {
                            FamilyName = "комплектация",

                            Ucode =
                                sub.Ucode ?? "",

                            Modification =
                                sub.Modification ?? "",

                            WorkPressure =
                                sub.WorkPressure ?? "",

                            Size =
                                GetFinalSize(
                                    sub.Size,
                                    familySize),

                            Amount =
                                sub.Amount ?? "-",

                            SystemName =
                                systemName,

                            // УНИКАЛЬНЫЙ виртуальный ID
                            ID =
                                equipmentId,

                            Section =
                                section,

                            Part =
                                part,

                            Order =
                                order,

                            SortingCode =
                                sorting,

                            WipCode =
                                wipCode
                        };


                        // --------------------------------------------------
                        // Вычисляем DataHash
                        // --------------------------------------------------

                        HashHelper.UpdateHash(equipItem);


                        // --------------------------------------------------
                        // Проверяем уникальность PRIMARY KEY
                        // --------------------------------------------------

                        string uniqueKey =
                            $"{equipItem.ID}|{equipItem.WipCode}";


                        if (!uniqueKeys.Add(uniqueKey))
                        {
                            System.Diagnostics.Debug.WriteLine(
                                "Обнаружен дубликат комплектации: " +
                                $"ID={equipItem.ID}, " +
                                $"WipCode={equipItem.WipCode}, " +
                                $"Ucode={equipItem.Ucode}, " +
                                $"Modification={equipItem.Modification}");

                            continue;
                        }


                        // --------------------------------------------------
                        // Добавляем комплектацию
                        // --------------------------------------------------

                        result.Add(equipItem);
                    }
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "Ошибка",
                    $"Ошибка в EquipmentDetector:\n\n{ex.Message}\n\n" +
                    $"StackTrace:\n{ex.StackTrace}");
            }

            return result;
        }


        // ==========================================================
        // СОЗДАНИЕ СТАБИЛЬНОГО УНИКАЛЬНОГО ID КОМПЛЕКТАЦИИ
        // ==========================================================

        /// <summary>
        /// Создаёт стабильный отрицательный ID для комплектации.
        ///
        /// Обычные элементы Revit имеют положительные ElementId.
        /// Комплектации получают отрицательные ID.
        ///
        /// Благодаря этому записи не конфликтуют по:
        /// PRIMARY KEY (ID, WipCode)
        /// </summary>
        private static int CreateEquipmentId(
            int parentId,
            string parentWipCode,
            string ucode,
            string modification,
            string equipOrder,
            int index)
        {
            string source =
                $"{parentId}|" +
                $"{parentWipCode ?? ""}|" +
                $"{ucode ?? ""}|" +
                $"{modification ?? ""}|" +
                $"{equipOrder ?? ""}|" +
                $"{index}";


            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes =
                    Encoding.UTF8.GetBytes(source);

                byte[] hash =
                    sha256.ComputeHash(bytes);


                // Берём первые 4 байта SHA256
                int hashValue =
                    BitConverter.ToInt32(hash, 0);


                // int.MinValue нельзя передавать в Math.Abs
                if (hashValue == int.MinValue)
                {
                    hashValue = int.MaxValue;
                }


                // Всегда отрицательный ID
                int equipmentId =
                    -Math.Abs(hashValue);


                // На всякий случай не допускаем 0
                if (equipmentId == 0)
                {
                    equipmentId = -1;
                }


                return equipmentId;
            }
        }


        // ==========================================================
        // ПРОВЕРКА НЕОБХОДИМОСТИ ДОБАВЛЕНИЯ КОМПЛЕКТАЦИИ
        // ==========================================================

        private static bool ShouldInclude(
            Element element,
            Element familyType,
            SubEquipment sub)
        {
            try
            {
                if (sub == null)
                    return false;


                string instruction =
                    sub.TriggerInstruction;


                // Если инструкция отсутствует,
                // комплектация добавляется всегда
                if (string.IsNullOrWhiteSpace(instruction))
                {
                    return true;
                }


                instruction =
                    instruction.Trim();


                // --------------------------------------------------
                // Получаем параметр
                // --------------------------------------------------

                Parameter parameter =
                    GetParameter(
                        element,
                        familyType,
                        instruction);


                // Если параметр не найден
                if (parameter == null)
                {
                    return false;
                }


                // Если параметр не имеет значения
                if (!parameter.HasValue)
                {
                    return false;
                }


                // --------------------------------------------------
                // Получаем значение параметра
                // --------------------------------------------------

                string value =
                    GetParameterValue(parameter);


                if (string.IsNullOrWhiteSpace(value))
                {
                    return false;
                }


                value = value.Trim();


                // --------------------------------------------------
                // TRUE
                // --------------------------------------------------

                if (value.Equals(
                        "1",
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(
                        "true",
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(
                        "да",
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(
                        "yes",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }


                // --------------------------------------------------
                // FALSE
                // --------------------------------------------------

                if (value.Equals(
                        "0",
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(
                        "false",
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(
                        "нет",
                        StringComparison.OrdinalIgnoreCase) ||
                    value.Equals(
                        "no",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }


                // Если значение существует и не является явно FALSE
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Ошибка ShouldInclude: {ex.Message}");

                return false;
            }
        }


        // ==========================================================
        // ПОИСК ПАРАМЕТРА
        // ==========================================================

        private static Parameter GetParameter(
            Element element,
            Element familyType,
            string parameterName)
        {
            if (string.IsNullOrWhiteSpace(parameterName))
                return null;


            // --------------------------------------------------
            // Сначала ищем у экземпляра
            // --------------------------------------------------

            try
            {
                Parameter parameter =
                    element?
                        .LookupParameter(parameterName);

                if (parameter != null)
                {
                    return parameter;
                }
            }
            catch
            {
                // Игнорируем и продолжаем поиск
            }


            // --------------------------------------------------
            // Затем ищем у типа
            // --------------------------------------------------

            try
            {
                Parameter parameter =
                    familyType?
                        .LookupParameter(parameterName);

                if (parameter != null)
                {
                    return parameter;
                }
            }
            catch
            {
                // Игнорируем
            }


            return null;
        }


        // ==========================================================
        // ПОЛУЧЕНИЕ ЗНАЧЕНИЯ ПАРАМЕТРА
        // ==========================================================

        private static string GetParameterValue(
            Parameter parameter)
        {
            if (parameter == null ||
                !parameter.HasValue)
            {
                return null;
            }


            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.String:

                        return parameter.AsString();


                    case StorageType.Integer:

                        return parameter
                            .AsInteger()
                            .ToString();


                    case StorageType.Double:

                        // Предпочитаем отображаемое значение
                        string valueString =
                            parameter.AsValueString();

                        if (!string.IsNullOrWhiteSpace(valueString))
                        {
                            return valueString;
                        }

                        return parameter
                            .AsDouble()
                            .ToString(
                                System.Globalization.CultureInfo.InvariantCulture);


                    case StorageType.ElementId:

                        return parameter
                            .AsElementId()
                            .IntegerValue
                            .ToString();


                    default:

                        return parameter.AsValueString();
                }
            }
            catch
            {
                return null;
            }
        }


        // ==========================================================
        // ФОРМИРОВАНИЕ ORDER КОМПЛЕКТАЦИИ
        // ==========================================================

        private static string MakeOrder(
            string parentOrder,
            string equipOrder)
        {
            if (string.IsNullOrEmpty(parentOrder) ||
                parentOrder.Length < 4)
            {
                return "-";
            }


            if (string.IsNullOrEmpty(equipOrder))
            {
                return "-";
            }


            equipOrder =
                equipOrder.Trim();


            // Если EquipOrder является числом
            if (equipOrder.All(char.IsDigit))
            {
                // Дополняем до 2 цифр
                string orderNum =
                    equipOrder.PadLeft(2, '0');


                // Первые 4 символа родительского Order
                return parentOrder.Substring(0, 4) +
                       orderNum;
            }


            return "-";
        }


        // ==========================================================
        // ИЗВЛЕЧЕНИЕ РАЗМЕРА СЕМЕЙСТВА
        // ==========================================================

        public static string ExtractSize(
            Element familyType)
        {
            if (familyType == null)
                return "-";


            try
            {
                string sizeDirty =
                    familyType
                        .LookupParameter(
                            ParametersInfo.Diameter1)?
                        .AsValueString()?
                        .Trim()
                    ?? "-";


                if (!string.IsNullOrWhiteSpace(sizeDirty) &&
                    sizeDirty != "-")
                {
                    return NormalizeSize(sizeDirty);
                }


                // --------------------------------------------------
                // Пробуем альтернативные параметры
                // --------------------------------------------------

                string[] parameterNames =
                {
                    ParametersInfo.Diameter1,
                    "Диаметр",
                    "Диаметр условный",
                    "Размер",
                    "Ширина",
                    "Высота"
                };


                foreach (string parameterName in parameterNames)
                {
                    Parameter parameter =
                        familyType.LookupParameter(parameterName);

                    if (parameter == null ||
                        !parameter.HasValue)
                    {
                        continue;
                    }


                    string value =
                        parameter.AsValueString() ??
                        parameter.AsString();


                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return NormalizeSize(
                            value.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Ошибка ExtractSize: {ex.Message}");
            }


            return "-";
        }


        // ==========================================================
        // ПОЛУЧЕНИЕ ФИНАЛЬНОГО РАЗМЕРА КОМПЛЕКТАЦИИ
        // ==========================================================

        private static string GetFinalSize(
            string equipmentSize,
            string familySize)
        {
            // Если размер комплектации пустой
            if (string.IsNullOrWhiteSpace(equipmentSize))
            {
                return familySize ?? "-";
            }


            equipmentSize =
                equipmentSize.Trim();


            // --------------------------------------------------
            // Подстановки размера родителя
            // --------------------------------------------------

            if (equipmentSize == "{SIZE}" ||
                equipmentSize == "[SIZE]" ||
                equipmentSize == "$SIZE$" ||
                equipmentSize.Equals(
                    "SIZE",
                    StringComparison.OrdinalIgnoreCase))
            {
                return familySize ?? "-";
            }


            // --------------------------------------------------
            // Если указан специальный символ "-"
            // --------------------------------------------------

            if (equipmentSize == "-")
            {
                return "-";
            }


            // --------------------------------------------------
            // Замена шаблонов
            // --------------------------------------------------

            string result =
                equipmentSize;

            result =
                result.Replace(
                    "{SIZE}",
                    familySize ?? "-");

            result =
                result.Replace(
                    "[SIZE]",
                    familySize ?? "-");

            result =
                result.Replace(
                    "$SIZE$",
                    familySize ?? "-");


            return NormalizeSize(result);
        }


        // ==========================================================
        // НОРМАЛИЗАЦИЯ РАЗМЕРА
        // ==========================================================

        private static string NormalizeSize(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "-";


            string result =
                value.Trim();


            // Приводим кириллическую x к латинской
            result =
                result.Replace("х", "x")
                      .Replace("Х", "x")
                      .Replace("X", "x");


            // Удаляем лишние пробелы вокруг x
            result =
                Regex.Replace(
                    result,
                    @"\s*x\s*",
                    "x");


            // Удаляем двойные пробелы
            result =
                Regex.Replace(
                    result,
                    @"\s+",
                    " ");


            return result.Trim();
        }
    }
}