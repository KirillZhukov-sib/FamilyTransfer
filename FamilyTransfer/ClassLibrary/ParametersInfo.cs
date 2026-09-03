using Autodesk.Revit.DB;
using RevitToMySQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyTransfer
{
    public class ParametersInfo
    {
        public const string UCode = "BS_Код изделия";
        public const string Modification = "MEP_Модификация";
        public const string FuncGroup = "MEP_Функциональная группа";
        public const string FuncType = "MEP_Функциональный тип";
        public const string WorkPressure = "MEP_Давление номинальное (текст)";
        public const string FireResistance = "Огнестойкость";
        public const string Diameter1 = "MEP_Диаметр 1";
        public const string Diameter2 = "MEP_Диаметр 2";
        public const string WorkPressureNominal = "MEP_Давление номинальное (текст)";
        public const string OuterDiameter = "Внешний диаметр";
        public const string InnerDiameter = "Внутренний диаметр";
        public const string HeightMep = "MEP_Высота 1";
        public const string WidthMep = "MEP_Ширина 1";
        public const string DiameterMax = "Максимальный диаметр";
        public const string DiameterMin = "Минимальный диаметр";
        public const string WidthMepNom = "MEP_Ширина_Номинальная";
        public const string HeightMepNom = "MEP_Высота_Номинальная";
        public const string DiameterLeft = "Диаметр_слева";
        public const string DiameterRight = "Диаметр_справа";
        public const string SystemNameMEP = "MEP_Система";
        public const string FamilyName = "Имя семейства";
        public const string Section = "BDS_BuildingPart"; 
        public const string Part = "BDS_Building"; //корпус 

    }
}
