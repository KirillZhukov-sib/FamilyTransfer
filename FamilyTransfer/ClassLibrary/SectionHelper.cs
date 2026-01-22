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
    public static class SectionHelper
    {
        public static (string section, string part) GetBuildingSectionForElement(Document doc, Element element)
        {
            // Находим связанную модель с формами
            RevitLinkInstance linkInstance = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(link => System.Text.RegularExpressions.Regex.IsMatch(
                    link.Name, @"^\d{4}-\d\.rvt"));
            if (linkInstance == null)
            {
                TaskDialog.Show("Информация", "Связанная модель КФ-файла' не найдена");
                return ("-", "-");
            }

            Document linkDoc = linkInstance.GetLinkDocument();
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
                    Parameter bds = t.LookupParameter("BDS_Building");
                    string bdss = bds?.AsString() ?? "";
                    return !System.Text.RegularExpressions.Regex.IsMatch(
                    bdss, @"[AaАа]");
                })
                .ToList();

            double countSectionCivil = sectionCivil.Count;
            if (sectionCivil.Count == 0)
            {
                TaskDialog.Show("Информация", "Секции жилья не найдены");
                return ("-", "-");
            }

            //создадим коллекцию боксов с параметрами, чтоб потом объединить с новыми боксами стоянки
            List<(string section, string part, BoundingBoxXYZ form)> civilbounds = new List<(string, string, BoundingBoxXYZ)>();
            foreach (Element civ in sectionCivil)
            {
                string s = civ.LookupParameter("BDS_Building")?.AsString() ?? "-";
                string p = civ.LookupParameter("BDS_BuildingPart")?.AsString() ?? "-";
                BoundingBoxXYZ b = civ.get_BoundingBox(null);
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
                    Parameter bds = t.LookupParameter("BDS_Building");
                    string bdss = bds?.AsString() ?? "";
                    return System.Text.RegularExpressions.Regex.IsMatch(bdss, @"[AaАа]");
                });

            if (sectionParking == null)
            {
                foreach (var bb in civilbounds)
                {
                    BoundingBoxXYZ elemBox = element.get_BoundingBox(null);
                    if (elemBox != null && Intersection(bb.form, elemBox))
                    {
                        return (bb.section, bb.part);
                    }
                }
                return ("-", "-");
            }

            BoundingBoxXYZ parkform = sectionParking.get_BoundingBox(null);
            if (parkform == null) 
            {
                TaskDialog.Show("Ошибка", "Не удалось получить границы формы стоянки");
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
                string parksecname = sectionCivil[i - 1].LookupParameter("BDS_Building")?.AsString() ?? "";
                string parkpartame = sectionCivil[i - 1].LookupParameter("BDS_BuildingPart")?.AsString() ?? "";

                bounds.Add((parksecname, parkpartame, b));
            }
            string section = sectionParking.LookupParameter("BDS_Building")?.AsString() ?? ""; //секция у изначальной формы до деления ее записываем в элементы АС
            string part = sectionParking.LookupParameter("BDS_BuildingPart")?.AsString() ?? ""; //корпус у изначальной формы до деления его записываем в элементы АС

            // объединяем 2 коллекции: боксы жилья и боксы порезанного паркинга 
            civilbounds.AddRange(bounds);

            // ---
            //1 Этап. Работаем в каждом боксе секции жильяч 
            foreach (var bb in civilbounds)
            {   
                BoundingBoxXYZ elem = element.get_BoundingBox(null);
                BoundingBoxXYZ form = bb.form;
                if (Intersection(form, elem))
                {
                    string sectionnubmer = bb.section;
                    string partnumber = bb.part;
                    return (sectionnubmer, partnumber);
                }
            }
            //2 Этап. Занесение в элементы стоянки параметров "секции" & "корпуса"
            if (!IsElementCivil(element))
            {
                return (section, part);
            }

            return ("-", "-");
        }

        private static bool IsCivil(Element elem)
        {
            Parameter param = elem.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
            if (param == null) return false;

            string value = param.AsString() ?? "";

            bool hasSystem = System.Text.RegularExpressions.Regex.IsMatch(value, @"^[TТ](14|24|15|25)$");
            bool hasLetterA = System.Text.RegularExpressions.Regex.IsMatch(value, @"[аАaA]");

            return hasSystem || hasLetterA;
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


        //private static bool IsElemenTPakring(Element elem)
        //{
        //    Parameter param = elem.get_Parameter(BuiltInParameter.RBS_DUCT_PIPE_SYSTEM_ABBREVIATION_PARAM);
        //    if (param == null) return false;

        //    string value = param.AsString() ?? "";

        //    bool hasSystem = System.Text.RegularExpressions.Regex.IsMatch(value, @"^[TТ](14|24|15|25)$");
        //    bool hasLetterA = System.Text.RegularExpressions.Regex.IsMatch(value, @"[аАaA]");

        //    return hasSystem || hasLetterA;
        //}

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
