using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace TTIRC
{
    public static class Security
    {
        static Dictionary<Type, XmlSerializer> serializers;
        static Security() { serializers = new Dictionary<Type, XmlSerializer>(); }

        public static AesManaged GetAES(Type strong)
        {
            AesManaged aes = new AesManaged();
            PasswordDeriveBytes pdb = new PasswordDeriveBytes(strong.FullName, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xDE, 0xAD, 0xED, 0xEF });
            aes.Key = pdb.GetBytes(aes.KeySize / 8);
            aes.IV = pdb.GetBytes(aes.BlockSize / 8);
            return aes;
        }
        public static bool LoadEncryptedData<T>(out T value) where T : class
        {
            value = default(T);
            Type strong = typeof(T);
            string fileName = String.Format("./{0}.aes", strong.Name);
            AesManaged aes = GetAES(strong);

            MemoryStream xmlData = new MemoryStream();
            CryptoStream crypto = new CryptoStream(xmlData, aes.CreateDecryptor(), CryptoStreamMode.Write);
            FileStream encrypted = null;
            try
            {
                encrypted = File.OpenRead(fileName);
                encrypted.CopyTo(crypto);

                crypto.Flush();
                crypto.FlushFinalBlock();
                xmlData.Position = 0;

                if (!serializers.ContainsKey(strong))
                    serializers.Add(strong, new XmlSerializer(strong));

                if (xmlData.Length == 0) return false;
                value = serializers[strong].Deserialize(xmlData) as T;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
            finally
            {
                if (encrypted != null)
                    encrypted.Close();
            }
        }
        public static void SaveEncryptedData<T>(T value)
        {
            Type strong = typeof(T);
            string fileName = String.Format("./{0}.aes", strong.Name);
            AesManaged aes = GetAES(strong);

            FileStream encrypted = null;
            try
            {
                encrypted = File.Open(fileName, FileMode.OpenOrCreate);
                CryptoStream crypto = new CryptoStream(encrypted, aes.CreateEncryptor(), CryptoStreamMode.Write);
                MemoryStream xmlData = new MemoryStream();

                if (!serializers.ContainsKey(strong))
                    serializers.Add(strong, new XmlSerializer(strong));
                serializers[strong].Serialize(xmlData, value);

                xmlData.Position = 0;
                xmlData.CopyTo(crypto);

                crypto.Flush();
                crypto.FlushFinalBlock();
                encrypted.Flush();
                encrypted.Close();
            }
            catch (Exception)
            {

            }
            finally
            {
                if (encrypted != null)
                    encrypted.Close();
            }
        }
    }
}
