using System;
using System.Security.Cryptography;
using System.Text;

namespace RevitToMySQL
{
    public static class HashHelper
    {
        public static string ComputeDataHash(FamilyInfo item)
        {
            if (item == null)
                return null;

            string data = string.Join("|",
                item.FamilyName ?? "",
                item.Ucode ?? "",
                item.FuncGroup ?? "",
                item.FuncTypes ?? "",
                item.Modification ?? "",
                item.WorkPressure ?? "",
                item.DuctEI ?? "",
                item.Order ?? "",
                item.Size ?? "",
                item.SystemName ?? "",
                item.Amount ?? "",
                item.Section ?? "",
                item.Part ?? "",
                item.ID.ToString(),
                item.SortingCode.ToString(),
                item.WipCode ?? "",
                item.Level ?? ""
            );

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                byte[] hash = sha256.ComputeHash(bytes);

                return Convert.ToBase64String(hash);
            }
        }

        public static void UpdateHash(FamilyInfo item)
        {
            if (item == null)
                return;

            item.DataHash = ComputeDataHash(item);
        }
    }
}
