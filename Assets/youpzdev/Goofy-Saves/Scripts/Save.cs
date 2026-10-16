using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public static class Save
{
    private static readonly string filePath = Path.Combine(Application.persistentDataPath, "save.dat");
    private static Dictionary<string, string> data = new();
    private static string customKey;
    private static bool loaded;

    public static bool UseEncryption = true;

    public static void SetKey(string key)
    {
        if (key.Length != 32) { Debug.LogError("Save: key must be exactly 32 characters"); return; }
        customKey = key;
    }

    public static void Set<T>(string k, T value) { EnsureLoaded(); data[k] = JsonConvert.SerializeObject(value); Flush(); }
    public static T Get<T>(string k, T defaultValue = default)
    {
        EnsureLoaded();
        if (!data.TryGetValue(k, out var raw)) return defaultValue;
        try { return JsonConvert.DeserializeObject<T>(raw); }
        catch { return defaultValue; }
    }
    public static bool Has(string k) { EnsureLoaded(); return data.ContainsKey(k); }
    public static void Delete(string k) { EnsureLoaded(); data.Remove(k); Flush(); }
    public static void DeleteAll() { data.Clear(); Flush(); }

    private static string GetKey()
    {
        if (customKey != null) return customKey;
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(SystemInfo.deviceUniqueIdentifier + "goofy-save"));
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 32).ToLower();
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        if (!File.Exists(filePath)) return;
        try { var raw = File.ReadAllText(filePath); data = JsonConvert.DeserializeObject<Dictionary<string, string>>(UseEncryption ? Decrypt(raw) : raw); }
        catch { data = new(); }
    }

    private static void Flush()
    {
        var json = JsonConvert.SerializeObject(data);
        File.WriteAllText(filePath, UseEncryption ? Encrypt(json) : json);
    }

    private static string Encrypt(string plain)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(GetKey());
        aes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(aes.IV, 0, aes.IV.Length);
        using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs)) sw.Write(plain);
        return Convert.ToBase64String(ms.ToArray());
    }

    private static string Decrypt(string cipher)
    {
        var bytes = System.Convert.FromBase64String(cipher);
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(GetKey());
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(bytes, iv, iv.Length);
        aes.IV = iv;
        using var ms = new MemoryStream(bytes, iv.Length, bytes.Length - iv.Length);
        using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}