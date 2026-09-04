using System;

namespace RevitToMySQL
{
    public static class ExportKeyHelper
    {
        public static string GetKey(FamilyInfo item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (string.IsNullOrWhiteSpace(item.WipCode))
            {
                throw new InvalidOperationException(
                    $"У элемента ID={item.ID} отсутствует WipCode.");
            }

            return $"{item.ID}|{item.WipCode}";
        }
    }
}