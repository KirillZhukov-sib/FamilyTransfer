using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;



namespace FamilyTransfer.Cache
{

    public interface IParameterCache
    {
        bool TryGet(string parameterName, out Parameter parameter);
        string GetString(string parameterName);
        double? GetDouble(string parameterName);
        int? GetInt(string parameterName);
        bool HasValue(string parameterName);   
    }

    
}
