using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB;

namespace RevitToMySQL
{
    public class FamilyInfo
    {
        public string FamilyName { get; set; }
        public string Ucode { get; set; }
        public string FuncGroup { get; set; }
        public string FuncTypes { get; set; }
        public string Modification { get; set; }
        public string WorkPressure { get; set; }
        public string DuctEI { get; set; }
        public string Order { get; set; }
        public string Size { get; set; }
        public string SystemName { get; set; }
        public string Amount { get; set; }
        public string Section { get; set; }
        public string Part { get; set; }
        public int ID { get; set; }
        public int SortingCode { get; set; }
        public string WipCode { get; set; }
        public string Level { get; set; }
        public string DataHash { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime? LastModified { get; set; }
    }
}

