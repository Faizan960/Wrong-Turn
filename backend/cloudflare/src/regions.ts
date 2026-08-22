// ============================================================================
// Authoritative server-side region catalog (mirrors Unity RegionCatalog).
// The Worker validates country_code / city_id against THIS list and derives the
// display strings itself — a modified APK cannot invent fake regions or fake
// display names to fabricate/pollute a leaderboard scope.
// ============================================================================

export interface RegionCountry { display: string; cities: Record<string, string>; }

export const COUNTRIES: Record<string, RegionCountry> = {
  IN: { display: "India", cities: {
    mumbai_in: "Mumbai", delhi_in: "Delhi", bengaluru_in: "Bengaluru", hyderabad_in: "Hyderabad",
    ahmedabad_in: "Ahmedabad", chennai_in: "Chennai", kolkata_in: "Kolkata", pune_in: "Pune",
    jaipur_in: "Jaipur", lucknow_in: "Lucknow", surat_in: "Surat", kanpur_in: "Kanpur",
    nagpur_in: "Nagpur", indore_in: "Indore", bhopal_in: "Bhopal", patna_in: "Patna",
    solapur_in: "Solapur", kochi_in: "Kochi" } },
  US: { display: "United States", cities: {
    newyork_us: "New York", losangeles_us: "Los Angeles", chicago_us: "Chicago", houston_us: "Houston",
    phoenix_us: "Phoenix", philadelphia_us: "Philadelphia", sanantonio_us: "San Antonio",
    sandiego_us: "San Diego", dallas_us: "Dallas", sanjose_us: "San Jose", austin_us: "Austin",
    seattle_us: "Seattle", miami_us: "Miami", atlanta_us: "Atlanta", boston_us: "Boston" } },
  GB: { display: "United Kingdom", cities: {
    london_gb: "London", manchester_gb: "Manchester", birmingham_gb: "Birmingham", leeds_gb: "Leeds",
    glasgow_gb: "Glasgow", liverpool_gb: "Liverpool", bristol_gb: "Bristol", edinburgh_gb: "Edinburgh",
    cardiff_gb: "Cardiff" } },
  PK: { display: "Pakistan", cities: {
    karachi_pk: "Karachi", lahore_pk: "Lahore", islamabad_pk: "Islamabad", rawalpindi_pk: "Rawalpindi",
    faisalabad_pk: "Faisalabad", multan_pk: "Multan", peshawar_pk: "Peshawar", quetta_pk: "Quetta" } },
  BD: { display: "Bangladesh", cities: {
    dhaka_bd: "Dhaka", chittagong_bd: "Chittagong", khulna_bd: "Khulna", sylhet_bd: "Sylhet",
    rajshahi_bd: "Rajshahi" } },
  ID: { display: "Indonesia", cities: {
    jakarta_id: "Jakarta", surabaya_id: "Surabaya", bandung_id: "Bandung", medan_id: "Medan",
    semarang_id: "Semarang", makassar_id: "Makassar" } },
  PH: { display: "Philippines", cities: {
    manila_ph: "Manila", quezon_ph: "Quezon City", davao_ph: "Davao", cebu_ph: "Cebu", makati_ph: "Makati" } },
  BR: { display: "Brazil", cities: {
    saopaulo_br: "São Paulo", rio_br: "Rio de Janeiro", brasilia_br: "Brasília", salvador_br: "Salvador",
    fortaleza_br: "Fortaleza", recife_br: "Recife" } },
  NG: { display: "Nigeria", cities: {
    lagos_ng: "Lagos", abuja_ng: "Abuja", kano_ng: "Kano", ibadan_ng: "Ibadan", portharcourt_ng: "Port Harcourt" } },
  CA: { display: "Canada", cities: {
    toronto_ca: "Toronto", montreal_ca: "Montreal", vancouver_ca: "Vancouver", calgary_ca: "Calgary",
    ottawa_ca: "Ottawa" } },
  AU: { display: "Australia", cities: {
    sydney_au: "Sydney", melbourne_au: "Melbourne", brisbane_au: "Brisbane", perth_au: "Perth",
    adelaide_au: "Adelaide" } },
  DE: { display: "Germany", cities: {
    berlin_de: "Berlin", munich_de: "Munich", hamburg_de: "Hamburg", cologne_de: "Cologne",
    frankfurt_de: "Frankfurt" } },
  FR: { display: "France", cities: {
    paris_fr: "Paris", marseille_fr: "Marseille", lyon_fr: "Lyon", toulouse_fr: "Toulouse", nice_fr: "Nice" } },
  AE: { display: "United Arab Emirates", cities: {
    dubai_ae: "Dubai", abudhabi_ae: "Abu Dhabi", sharjah_ae: "Sharjah" } },
  SA: { display: "Saudi Arabia", cities: {
    riyadh_sa: "Riyadh", jeddah_sa: "Jeddah", mecca_sa: "Mecca", medina_sa: "Medina" } },
  ZA: { display: "South Africa", cities: {
    johannesburg_za: "Johannesburg", capetown_za: "Cape Town", durban_za: "Durban" } },
  EG: { display: "Egypt", cities: { cairo_eg: "Cairo", alexandria_eg: "Alexandria", giza_eg: "Giza" } },
  TR: { display: "Türkiye", cities: { istanbul_tr: "Istanbul", ankara_tr: "Ankara", izmir_tr: "Izmir" } },
  MX: { display: "Mexico", cities: {
    mexicocity_mx: "Mexico City", guadalajara_mx: "Guadalajara", monterrey_mx: "Monterrey" } },
  JP: { display: "Japan", cities: {
    tokyo_jp: "Tokyo", osaka_jp: "Osaka", yokohama_jp: "Yokohama", nagoya_jp: "Nagoya" } },
};

export interface ResolvedRegion {
  countryCode: string; countryDisplay: string;
  cityId: string | null; cityDisplay: string | null;
}

/**
 * Validate client-supplied region ids against the catalog and return the
 * authoritative (server-derived) display strings. City is optional. Throws on
 * an unknown country, or a city that doesn't belong to the country.
 */
export function resolveRegion(countryCode: string, cityId: string | null): ResolvedRegion {
  const cc = (countryCode || "").trim().toUpperCase();
  const country = COUNTRIES[cc];
  if (!country) throw new Error("BAD_COUNTRY");

  const ci = (cityId || "").trim().toLowerCase();
  if (!ci) return { countryCode: cc, countryDisplay: country.display, cityId: null, cityDisplay: null };

  const cityDisplay = country.cities[ci];
  if (!cityDisplay) throw new Error("BAD_CITY");
  return { countryCode: cc, countryDisplay: country.display, cityId: ci, cityDisplay };
}
