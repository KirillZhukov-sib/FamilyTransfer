using Autodesk.Revit.DB;
using FamilyTransfer.Cache;
using System.Collections.Generic;

namespace RevitToMySQL.Caching
{
    public sealed class TypeParameterCacheProvider : ITypeParameterCacheProvider
    {
        private readonly Document _document;
        private readonly Dictionary<ElementId, IParameterCache> _cache = new Dictionary<ElementId, IParameterCache>();

        public TypeParameterCacheProvider(Document document)
        {
            _document = document;
        }

        public IParameterCache Get(Element element)
        {
            if (element == null)
                return new ParameterCache(null);

            ElementId typeId = element.GetTypeId();
            if (typeId == null || typeId == ElementId.InvalidElementId)
                return new ParameterCache(null);

            if (_cache.TryGetValue(typeId, out var cache))
                return cache;

            Element type = _document.GetElement(typeId);
            if (type == null)
                return new ParameterCache(null);

            cache = new ParameterCache(type);

            if (!_cache.ContainsKey(typeId))
            {
                _cache.Add(typeId, cache);
            }
            else
            {
                return _cache[typeId];
            }

            return cache;
        }

        public void Clear()
        {
            _cache.Clear();
        }
    }
}