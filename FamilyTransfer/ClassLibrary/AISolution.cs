using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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