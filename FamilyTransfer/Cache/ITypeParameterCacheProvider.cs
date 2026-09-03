using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;


namespace FamilyTransfer.Cache
{
    public interface ITypeParameterCacheProvider
    {
        IParameterCache Get(Element element);
    }
}
