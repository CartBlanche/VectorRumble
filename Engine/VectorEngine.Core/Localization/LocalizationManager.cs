using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Threading;

namespace VectorEngine.Localization;

public class LocalizationManager
{
    /// <summary>The cultures a game has translations for.</summary>
    /// <param name="resourceManager">The game's own resources (its Resources.ResourceManager).</param>
    public static List<CultureInfo> GetSupportedCultures(ResourceManager resourceManager)
    {
        // Create a list to hold supported cultures
        List<CultureInfo> supportedCultures = new List<CultureInfo>();

        // Get all cultures defined in the satellite assemblies
        CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

        foreach (CultureInfo culture in cultures)
        {
            try
            {
                // Try to get the resource set for this culture
                var resourceSet = resourceManager.GetResourceSet(culture, true, false);
                if (resourceSet != null)
                {
                    supportedCultures.Add(culture);
                }
            }
            catch (MissingManifestResourceException)
            {
                // This exception is thrown when there is no .resx for the culture, ignore it
            }
        }

        // Always add the default (invariant) culture - the base .resx file
        supportedCultures.Add(CultureInfo.InvariantCulture);

        return supportedCultures;
    }

    /// <summary>The device's own language, taken once before the game first sets its culture (SetCulture changes the thread's, so asking later would give the game's).</summary>
    private static readonly CultureInfo DeviceCulture = CultureInfo.CurrentUICulture;

    /// <summary>
    /// The language to start in when the player has not chosen one: the device's language if the game has it ("de-DE" exactly, or any German for
    /// the German file, Portuguese from Portugal for the Brazilian one), otherwise English.
    /// </summary>
    public static CultureInfo FromDevice(List<CultureInfo> supported)
    {
        foreach (var culture in supported)
            if (culture.Name.Length > 0 && string.Equals(culture.Name, DeviceCulture.Name, System.StringComparison.OrdinalIgnoreCase))
                return culture;
        foreach (var culture in supported)
            if (culture.Name.Length > 0 && culture.TwoLetterISOLanguageName == DeviceCulture.TwoLetterISOLanguageName)
                return culture;
        return CultureInfo.InvariantCulture;
    }

    /// <summary>The language for a saved <paramref name="code"/>: the player's choice if there is one (and the game still has it), else the device's.</summary>
    public static CultureInfo Resolve(string code, List<CultureInfo> supported)
    {
        if (code is null)
            return FromDevice(supported);
        foreach (var culture in supported)
            if (string.Equals(culture.Name, code, System.StringComparison.OrdinalIgnoreCase))
                return culture;
        return CultureInfo.InvariantCulture;
    }

    public static void SetCulture(string cultureCode)
    {
        CultureInfo culture = new CultureInfo(cultureCode);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
