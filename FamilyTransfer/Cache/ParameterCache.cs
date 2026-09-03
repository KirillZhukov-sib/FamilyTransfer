using Autodesk.Revit.DB;
using FamilyTransfer.Cache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitToMySQL.Caching
{
    public sealed class ParameterCache : IParameterCache
    {
        private readonly Dictionary<string, Parameter> _parameters;

        public ParameterCache(Element element)
        {
            if (element == null)
            {
                _parameters = new Dictionary<string, Parameter>(StringComparer.OrdinalIgnoreCase);
                return;
            }
            try
            {
                // Безопасное создание словаря
                _parameters = new Dictionary<string, Parameter>(StringComparer.OrdinalIgnoreCase);

                foreach (Parameter param in element.Parameters)
                {
                    if (param?.Definition == null) continue;

                    string paramName = param.Definition.Name;
                    if (string.IsNullOrEmpty(paramName)) continue;

                    // Если ключ уже есть - пропускаем, сохраняем первый
                    if (!_parameters.ContainsKey(paramName))
                    {
                        _parameters.Add(paramName, param);
                    }
                }
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка создания кэша параметров: {ex.Message}");
                _parameters = new Dictionary<string, Parameter>(StringComparer.OrdinalIgnoreCase);
            }
        }
        public bool TryGet(string parameterName, out Parameter parameter)
        {
            return _parameters.TryGetValue(parameterName, out parameter);
        }
        public string GetString(string parameterName)
        {
            if (!_parameters.TryGetValue(parameterName, out var parameter))
                return string.Empty;

            if (!parameter.HasValue)
                return string.Empty;

            return parameter.AsString() ?? string.Empty;
        }
        public double? GetDouble(string parameterName)
        {
            if (!_parameters.TryGetValue(parameterName, out var parameter))
                return null;

            if (!parameter.HasValue)
                return null;

            return parameter.AsDouble();
        }
        public int? GetInt(string parameterName)
        {
            if (!_parameters.TryGetValue(parameterName, out var parameter))
                return null;

            if (!parameter.HasValue)
                return null;

            return parameter.AsInteger();
        }
        public bool HasValue(string parameterName)
        {
            if (!_parameters.TryGetValue(parameterName, out var parameter))
                return false;

            return parameter.HasValue;
        }
    }
}