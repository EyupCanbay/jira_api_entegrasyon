using System.Security.Cryptography;
using System.Text;

public static class SecurityHelper
{
    //AES ŞİFRELEME 
    public static string Sifrele(string plainText, string key)
    {
        if (key.Length != 32) throw new Exception("Key tam 32 karakter olmalı!");
        
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.GenerateIV();
            
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] encryptedBytes;
            
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs)) { sw.Write(plainText); }
                encryptedBytes = ms.ToArray();
            }
            
            // IV + Şifreli Veri birleştiriliyor
            var combined = new byte[aes.IV.Length + encryptedBytes.Length];
            Array.Copy(aes.IV, 0, combined, 0, aes.IV.Length);
            Array.Copy(encryptedBytes, 0, combined, aes.IV.Length, encryptedBytes.Length);
            
            return Convert.ToBase64String(combined);
        }
    }

    public static string SifreyiCoz(string cipherText, string key)
    {
        if (key.Length != 32) throw new Exception("Key tam 32 karakter olmalı!");
        
        var fullCipher = Convert.FromBase64String(cipherText);
        using (Aes aes = Aes.Create())
        {
            byte[] iv = new byte[16];
            Array.Copy(fullCipher, 0, iv, 0, iv.Length);
            
            byte[] cipher = new byte[fullCipher.Length - iv.Length];
            Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);
            
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = iv;
            
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            
            using (var ms = new MemoryStream(cipher))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var sr = new StreamReader(cs)) { return sr.ReadToEnd(); }
        }
    }

    // Bu, verinin değiştirilmediğini ve anahtara sahip biri tarafından gönderildiğini kanıtlar.
    public static string HMACImzaOlustur(string timestamp, string nonce, string secretKey)
    {
        // İmza içeriği: ZamanDamgası + RastgeleSayı
        var message = $"{timestamp}:{nonce}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using (var hmac = new HMACSHA256(keyBytes))
        {
            var hashBytes = hmac.ComputeHash(messageBytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}