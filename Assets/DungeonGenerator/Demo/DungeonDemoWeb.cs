using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

// Integración con el navegador para la demo web: semilla en la URL (?seed=1234)
// y copiar el enlace al portapapeles. Fuera de WebGL las llamadas son no-ops o usan
// el portapapeles del sistema.
public static class DungeonDemoWeb
{
    const string SeedParameter = "seed";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void DungeonDemo_ReplaceUrl(string url);

    [DllImport("__Internal")]
    static extern void DungeonDemo_CopyToClipboard(string text);
#endif

    public static bool HasUrl { get => !string.IsNullOrEmpty(Application.absoluteURL); }

    // Semilla indicada en la URL de la página, si la hay
    public static bool TryGetSeedFromUrl(out int seed)
    {
        seed = 0;
        if (!HasUrl)
            return false;

        SplitUrl(Application.absoluteURL, out _, out string query, out _);
        foreach (KeyValuePair<string, string> parameter in ParseQuery(query))
        {
            if (parameter.Key == SeedParameter)
                return int.TryParse(parameter.Value, out seed);
        }
        return false;
    }

    // URL de la página actual con ?seed=<seed>
    public static string GetShareUrl(int seed)
    {
        if (!HasUrl)
            return SeedParameter + "=" + seed;

        SplitUrl(Application.absoluteURL, out string path, out string query, out string fragment);

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, string> parameter in ParseQuery(query))
        {
            if (parameter.Key != SeedParameter)
                parts.Add(parameter.Value == null ? parameter.Key : parameter.Key + "=" + parameter.Value);
        }
        parts.Add(SeedParameter + "=" + seed);

        return path + "?" + string.Join("&", parts) + fragment;
    }

    // Refleja la semilla en la barra de direcciones para que se pueda compartir tal cual
    public static void UpdateUrl(int seed)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        DungeonDemo_ReplaceUrl(GetShareUrl(seed));
#endif
    }

    public static void CopyToClipboard(string text)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        DungeonDemo_CopyToClipboard(text);
#else
        GUIUtility.systemCopyBuffer = text;
#endif
    }

    static void SplitUrl(string url, out string path, out string query, out string fragment)
    {
        fragment = "";
        int hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            fragment = url.Substring(hashIndex);
            url = url.Substring(0, hashIndex);
        }

        query = "";
        int queryIndex = url.IndexOf('?');
        if (queryIndex >= 0)
        {
            query = url.Substring(queryIndex + 1);
            url = url.Substring(0, queryIndex);
        }

        path = url;
    }

    static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        foreach (string part in query.Split('&'))
        {
            if (part.Length == 0)
                continue;

            int equalsIndex = part.IndexOf('=');
            if (equalsIndex < 0)
                yield return new KeyValuePair<string, string>(part, null);
            else
                yield return new KeyValuePair<string, string>(part.Substring(0, equalsIndex), part.Substring(equalsIndex + 1));
        }
    }
}
