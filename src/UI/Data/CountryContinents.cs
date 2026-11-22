namespace MusicForge.UI.Data;

/// <summary>
/// Maps ISO 3166-1 alpha-2 country codes to continents.
/// The Radio Browser API has no continent field, so the directory
/// hierarchy (continent -> country -> stations) is derived from this table.
/// </summary>
public static class CountryContinents
{
    public const string Africa = "Africa";
    public const string Asia = "Asia";
    public const string Europe = "Europe";
    public const string NorthAmerica = "North America";
    public const string SouthAmerica = "South America";
    public const string Oceania = "Oceania";
    public const string Antarctica = "Antarctica";
    public const string Other = "Other";

    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Africa
        ["DZ"] = Africa, ["AO"] = Africa, ["BJ"] = Africa, ["BW"] = Africa, ["BF"] = Africa,
        ["BI"] = Africa, ["CV"] = Africa, ["CM"] = Africa, ["CF"] = Africa, ["TD"] = Africa,
        ["KM"] = Africa, ["CG"] = Africa, ["CD"] = Africa, ["CI"] = Africa, ["DJ"] = Africa,
        ["EG"] = Africa, ["GQ"] = Africa, ["ER"] = Africa, ["SZ"] = Africa, ["ET"] = Africa,
        ["GA"] = Africa, ["GM"] = Africa, ["GH"] = Africa, ["GN"] = Africa, ["GW"] = Africa,
        ["KE"] = Africa, ["LS"] = Africa, ["LR"] = Africa, ["LY"] = Africa, ["MG"] = Africa,
        ["MW"] = Africa, ["ML"] = Africa, ["MR"] = Africa, ["MU"] = Africa, ["MA"] = Africa,
        ["MZ"] = Africa, ["NA"] = Africa, ["NE"] = Africa, ["NG"] = Africa, ["RW"] = Africa,
        ["ST"] = Africa, ["SN"] = Africa, ["SC"] = Africa, ["SL"] = Africa, ["SO"] = Africa,
        ["ZA"] = Africa, ["SS"] = Africa, ["SD"] = Africa, ["TZ"] = Africa, ["TG"] = Africa,
        ["TN"] = Africa, ["UG"] = Africa, ["ZM"] = Africa, ["ZW"] = Africa, ["EH"] = Africa,
        // Asia
        ["AF"] = Asia, ["AM"] = Asia, ["AZ"] = Asia, ["BH"] = Asia, ["BD"] = Asia,
        ["BT"] = Asia, ["BN"] = Asia, ["KH"] = Asia, ["CN"] = Asia, ["CY"] = Asia,
        ["GE"] = Asia, ["IN"] = Asia, ["ID"] = Asia, ["IR"] = Asia, ["IQ"] = Asia,
        ["IL"] = Asia, ["JP"] = Asia, ["JO"] = Asia, ["KZ"] = Asia, ["KW"] = Asia,
        ["KG"] = Asia, ["LA"] = Asia, ["LB"] = Asia, ["MY"] = Asia, ["MV"] = Asia,
        ["MN"] = Asia, ["MM"] = Asia, ["NP"] = Asia, ["KP"] = Asia, ["OM"] = Asia,
        ["PK"] = Asia, ["PS"] = Asia, ["PH"] = Asia, ["QA"] = Asia, ["SA"] = Asia,
        ["SG"] = Asia, ["KR"] = Asia, ["LK"] = Asia, ["SY"] = Asia, ["TW"] = Asia,
        ["TJ"] = Asia, ["TH"] = Asia, ["TL"] = Asia, ["TR"] = Asia, ["TM"] = Asia,
        ["AE"] = Asia, ["UZ"] = Asia, ["VN"] = Asia, ["YE"] = Asia,
        // Europe
        ["AL"] = Europe, ["AD"] = Europe, ["AT"] = Europe, ["BY"] = Europe, ["BE"] = Europe,
        ["BA"] = Europe, ["BG"] = Europe, ["HR"] = Europe, ["CZ"] = Europe, ["DK"] = Europe,
        ["EE"] = Europe, ["FO"] = Europe, ["FI"] = Europe, ["FR"] = Europe, ["DE"] = Europe,
        ["GI"] = Europe, ["GR"] = Europe, ["GG"] = Europe, ["HU"] = Europe, ["IS"] = Europe,
        ["IE"] = Europe, ["IM"] = Europe, ["IT"] = Europe, ["JE"] = Europe, ["XK"] = Europe,
        ["LV"] = Europe, ["LI"] = Europe, ["LT"] = Europe, ["LU"] = Europe, ["MT"] = Europe,
        ["MD"] = Europe, ["MC"] = Europe, ["ME"] = Europe, ["NL"] = Europe, ["MK"] = Europe,
        ["NO"] = Europe, ["PL"] = Europe, ["PT"] = Europe, ["RO"] = Europe, ["RU"] = Europe,
        ["SM"] = Europe, ["RS"] = Europe, ["SK"] = Europe, ["SI"] = Europe, ["ES"] = Europe,
        ["SE"] = Europe, ["CH"] = Europe, ["UA"] = Europe, ["GB"] = Europe, ["VA"] = Europe,
        // North America
        ["AG"] = NorthAmerica, ["BS"] = NorthAmerica, ["BB"] = NorthAmerica, ["BZ"] = NorthAmerica,
        ["CA"] = NorthAmerica, ["CR"] = NorthAmerica, ["CU"] = NorthAmerica, ["DM"] = NorthAmerica,
        ["DO"] = NorthAmerica, ["SV"] = NorthAmerica, ["GD"] = NorthAmerica, ["GT"] = NorthAmerica,
        ["HT"] = NorthAmerica, ["HN"] = NorthAmerica, ["JM"] = NorthAmerica, ["MX"] = NorthAmerica,
        ["NI"] = NorthAmerica, ["PA"] = NorthAmerica, ["KN"] = NorthAmerica, ["LC"] = NorthAmerica,
        ["VC"] = NorthAmerica, ["TT"] = NorthAmerica, ["US"] = NorthAmerica,
        // South America
        ["AR"] = SouthAmerica, ["BO"] = SouthAmerica, ["BR"] = SouthAmerica, ["CL"] = SouthAmerica,
        ["CO"] = SouthAmerica, ["EC"] = SouthAmerica, ["GY"] = SouthAmerica, ["PY"] = SouthAmerica,
        ["PE"] = SouthAmerica, ["SR"] = SouthAmerica, ["UY"] = SouthAmerica, ["VE"] = SouthAmerica,
        // Oceania
        ["AS"] = Oceania, ["AU"] = Oceania, ["FJ"] = Oceania, ["PF"] = Oceania, ["GU"] = Oceania,
        ["KI"] = Oceania, ["MH"] = Oceania, ["FM"] = Oceania, ["NR"] = Oceania, ["NC"] = Oceania,
        ["NZ"] = Oceania, ["NU"] = Oceania, ["MP"] = Oceania, ["PW"] = Oceania, ["PG"] = Oceania,
        ["PN"] = Oceania, ["WS"] = Oceania, ["SB"] = Oceania, ["TO"] = Oceania, ["TV"] = Oceania,
        ["VU"] = Oceania, ["WF"] = Oceania,
        // Antarctica & territories
        ["AQ"] = Antarctica, ["BV"] = Antarctica, ["HM"] = Antarctica, ["TF"] = Antarctica,
        ["GS"] = Antarctica
    };

    /// <summary>Returns the continent for an ISO alpha-2 country code, or <see cref="Other"/>.</summary>
    public static string ForCountryCode(string? countryCode)
    {
        if (!string.IsNullOrWhiteSpace(countryCode) && Map.TryGetValue(countryCode.Trim(), out var c))
            return c;
        return Other;
    }
}
