using FamilyTransfer.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FamilyTransfer.Caching
{
    public sealed class ParameterResolver
    {
        private readonly IParameterCache _instance;
        private readonly IParameterCache _type;

        public ParameterResolver(IParameterCache instance, IParameterCache type)
        {
            _instance = instance;
            _type = type;
        }

        public string GetString(string parameterName)
        {
            string value = _instance.GetString(parameterName);
            if (!string.IsNullOrEmpty(value))
                return value;

            return _type.GetString(parameterName);
        }


        public double? GetDouble(string parameterName)
        {
            double? value = _instance.GetDouble(parameterName);
            if (value.HasValue)
                return value;

            return _type.GetDouble(parameterName);
        }

        public int? GetInt(string parameterName)
        {
            int? value = _instance.GetInt(parameterName);
            if (value.HasValue)
                return value;

            return _type.GetInt(parameterName);
        }
    }
}