using System.Collections.Generic;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Curated country/city list for the region picker. Coarse regions only — no
    /// GPS, no lat/long (§6). City is optional (country-only players still get a
    /// country board). cityId = "&lt;cityslug&gt;_&lt;cc&gt;" is stable and unique.
    /// Extend freely; the backend treats these as opaque strings.
    /// </summary>
    public static class RegionCatalog
    {
        public sealed class Country
        {
            public string code;      // ISO-3166-1 alpha-2
            public string display;
            public List<City> cities;
        }

        public sealed class City
        {
            public string id;        // e.g. "mumbai_in"
            public string display;   // "Mumbai"
        }

        private static City C(string id, string display) => new City { id = id, display = display };

        public static readonly List<Country> Countries = new List<Country>
        {
            new Country { code = "IN", display = "India", cities = new List<City>
            {
                C("mumbai_in","Mumbai"), C("delhi_in","Delhi"), C("bengaluru_in","Bengaluru"),
                C("hyderabad_in","Hyderabad"), C("ahmedabad_in","Ahmedabad"), C("chennai_in","Chennai"),
                C("kolkata_in","Kolkata"), C("pune_in","Pune"), C("jaipur_in","Jaipur"),
                C("lucknow_in","Lucknow"), C("surat_in","Surat"), C("kanpur_in","Kanpur"),
                C("nagpur_in","Nagpur"), C("indore_in","Indore"), C("bhopal_in","Bhopal"),
                C("patna_in","Patna"), C("solapur_in","Solapur"), C("kochi_in","Kochi"),
            }},
            new Country { code = "US", display = "United States", cities = new List<City>
            {
                C("newyork_us","New York"), C("losangeles_us","Los Angeles"), C("chicago_us","Chicago"),
                C("houston_us","Houston"), C("phoenix_us","Phoenix"), C("philadelphia_us","Philadelphia"),
                C("sanantonio_us","San Antonio"), C("sandiego_us","San Diego"), C("dallas_us","Dallas"),
                C("sanjose_us","San Jose"), C("austin_us","Austin"), C("seattle_us","Seattle"),
                C("miami_us","Miami"), C("atlanta_us","Atlanta"), C("boston_us","Boston"),
            }},
            new Country { code = "GB", display = "United Kingdom", cities = new List<City>
            {
                C("london_gb","London"), C("manchester_gb","Manchester"), C("birmingham_gb","Birmingham"),
                C("leeds_gb","Leeds"), C("glasgow_gb","Glasgow"), C("liverpool_gb","Liverpool"),
                C("bristol_gb","Bristol"), C("edinburgh_gb","Edinburgh"), C("cardiff_gb","Cardiff"),
            }},
            new Country { code = "PK", display = "Pakistan", cities = new List<City>
            {
                C("karachi_pk","Karachi"), C("lahore_pk","Lahore"), C("islamabad_pk","Islamabad"),
                C("rawalpindi_pk","Rawalpindi"), C("faisalabad_pk","Faisalabad"), C("multan_pk","Multan"),
                C("peshawar_pk","Peshawar"), C("quetta_pk","Quetta"),
            }},
            new Country { code = "BD", display = "Bangladesh", cities = new List<City>
            {
                C("dhaka_bd","Dhaka"), C("chittagong_bd","Chittagong"), C("khulna_bd","Khulna"),
                C("sylhet_bd","Sylhet"), C("rajshahi_bd","Rajshahi"),
            }},
            new Country { code = "ID", display = "Indonesia", cities = new List<City>
            {
                C("jakarta_id","Jakarta"), C("surabaya_id","Surabaya"), C("bandung_id","Bandung"),
                C("medan_id","Medan"), C("semarang_id","Semarang"), C("makassar_id","Makassar"),
            }},
            new Country { code = "PH", display = "Philippines", cities = new List<City>
            {
                C("manila_ph","Manila"), C("quezon_ph","Quezon City"), C("davao_ph","Davao"),
                C("cebu_ph","Cebu"), C("makati_ph","Makati"),
            }},
            new Country { code = "BR", display = "Brazil", cities = new List<City>
            {
                C("saopaulo_br","São Paulo"), C("rio_br","Rio de Janeiro"), C("brasilia_br","Brasília"),
                C("salvador_br","Salvador"), C("fortaleza_br","Fortaleza"), C("recife_br","Recife"),
            }},
            new Country { code = "NG", display = "Nigeria", cities = new List<City>
            {
                C("lagos_ng","Lagos"), C("abuja_ng","Abuja"), C("kano_ng","Kano"),
                C("ibadan_ng","Ibadan"), C("portharcourt_ng","Port Harcourt"),
            }},
            new Country { code = "CA", display = "Canada", cities = new List<City>
            {
                C("toronto_ca","Toronto"), C("montreal_ca","Montreal"), C("vancouver_ca","Vancouver"),
                C("calgary_ca","Calgary"), C("ottawa_ca","Ottawa"),
            }},
            new Country { code = "AU", display = "Australia", cities = new List<City>
            {
                C("sydney_au","Sydney"), C("melbourne_au","Melbourne"), C("brisbane_au","Brisbane"),
                C("perth_au","Perth"), C("adelaide_au","Adelaide"),
            }},
            new Country { code = "DE", display = "Germany", cities = new List<City>
            {
                C("berlin_de","Berlin"), C("munich_de","Munich"), C("hamburg_de","Hamburg"),
                C("cologne_de","Cologne"), C("frankfurt_de","Frankfurt"),
            }},
            new Country { code = "FR", display = "France", cities = new List<City>
            {
                C("paris_fr","Paris"), C("marseille_fr","Marseille"), C("lyon_fr","Lyon"),
                C("toulouse_fr","Toulouse"), C("nice_fr","Nice"),
            }},
            new Country { code = "AE", display = "United Arab Emirates", cities = new List<City>
            {
                C("dubai_ae","Dubai"), C("abudhabi_ae","Abu Dhabi"), C("sharjah_ae","Sharjah"),
            }},
            new Country { code = "SA", display = "Saudi Arabia", cities = new List<City>
            {
                C("riyadh_sa","Riyadh"), C("jeddah_sa","Jeddah"), C("mecca_sa","Mecca"), C("medina_sa","Medina"),
            }},
            new Country { code = "ZA", display = "South Africa", cities = new List<City>
            {
                C("johannesburg_za","Johannesburg"), C("capetown_za","Cape Town"), C("durban_za","Durban"),
            }},
            new Country { code = "EG", display = "Egypt", cities = new List<City>
            {
                C("cairo_eg","Cairo"), C("alexandria_eg","Alexandria"), C("giza_eg","Giza"),
            }},
            new Country { code = "TR", display = "Türkiye", cities = new List<City>
            {
                C("istanbul_tr","Istanbul"), C("ankara_tr","Ankara"), C("izmir_tr","Izmir"),
            }},
            new Country { code = "MX", display = "Mexico", cities = new List<City>
            {
                C("mexicocity_mx","Mexico City"), C("guadalajara_mx","Guadalajara"), C("monterrey_mx","Monterrey"),
            }},
            new Country { code = "JP", display = "Japan", cities = new List<City>
            {
                C("tokyo_jp","Tokyo"), C("osaka_jp","Osaka"), C("yokohama_jp","Yokohama"), C("nagoya_jp","Nagoya"),
            }},
        };

        public static Country FindCountry(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var c in Countries) if (c.code == code) return c;
            return null;
        }
    }
}
