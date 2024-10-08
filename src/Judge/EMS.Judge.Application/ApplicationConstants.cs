using Core.Utilities;
using Core.Domain.State.Countries;
using System.Collections.Generic;
using System.Reflection;

namespace EMS.Judge.Application;

public static class ApplicationConstants
{
    public static Assembly[] Assemblies
    {
        get
        {
            var assemblies = ReflectionUtilities.GetAssemblies("EMS.Judge.Application");
            return assemblies;
        }
    }

    public const string STORAGE_FILE_NAME = "endurance-judge-data";

    public static class Api
    {
        public const string WITNESS = "witness";
        public const string STARTLIST = "startlist";
    }
    
    public static class FileExtensions
    {
        public const string Xml = ".xml";
        public const string SupportedExcel = ".xlsx";
    }

    public static class ExcelMaps
    {
        public static class ImportNational
        {
            public const int FIRST_ENTRY_ROW = 4;
            public const int FEI_ID_COLUMN = 27;
            public const int NAME_COLUMN = 2;
            public const int BREED_COLUMN = 22;
            public const int CLUB_COLUMN = 30;
        }
    }
    
    internal static class Countries
    {
        public static readonly List<Country> List = new()
        {
            new("AFG", "Afghanistan", 1),
            new("ALA", "Åland Islands", 2),
            new("ALB", "Albania", 3),
            new("DZA", "Algeria", 4),
            new("ASM", "American Samoa", 5),
            new("AND", "Andorra", 6),
            new("AGO", "Angola", 7),
            new("AIA", "Anguilla", 8),
            new("ATA", "Antarctica", 9),
            new("ATG", "Antigua and Barbuda", 10),
            new("ARG", "Argentina", 11),
            new("ARM", "Armenia", 12),
            new("ABW", "Aruba", 13),
            new("AUS", "Australia", 14),
            new("AUT", "Austria", 15),
            new("AZE", "Azerbaijan", 16),
            new("BHS", "Bahamas", 17),
            new("BHR", "Bahrain", 18),
            new("BGD", "Bangladesh", 19),
            new("BRB", "Barbados", 20),
            new("BLR", "Belarus", 21),
            new("BEL", "Belgium", 22),
            new("BLZ", "Belize", 23),
            new("BEN", "Benin", 24),
            new("BMU", "Bermuda", 25),
            new("BTN", "Bhutan", 26),
            new("BOL", "Bolivia, Plurinational State of", 27),
            new("BES", "Bonaire, Sint Eustatius and Saba", 28),
            new("BIH", "Bosnia and Herzegovina", 29),
            new("BWA", "Botswana", 30),
            new("BVT", "Bouvet Island", 31),
            new("BRA", "Brazil", 32),
            new("IOT", "British Indian Ocean Territory", 33),
            new("BRN", "Brunei Darussalam", 34),
            new("BUL", "Bulgaria", 35),
            new("BFA", "Burkina Faso", 36),
            new("BDI", "Burundi", 37),
            new("KHM", "Cambodia", 38),
            new("CMR", "Cameroon", 39),
            new("CAN", "Canada", 40),
            new("CPV", "Cape Verde", 41),
            new("CYM", "Cayman Islands", 42),
            new("CAF", "Central African Republic", 43),
            new("TCD", "Chad", 44),
            new("CHL", "Chile", 45),
            new("CHN", "China", 46),
            new("CXR", "Christmas Island", 47),
            new("CCK", "Cocos (Keeling) Islands", 48),
            new("COL", "Colombia", 49),
            new("COM", "Comoros", 50),
            new("COG", "Congo", 51),
            new("COD", "Congo, the Democratic Republic of the", 52),
            new("COK", "Cook Islands", 53),
            new("CRI", "Costa Rica", 54),
            new("CIV", "Côte d'Ivoire", 55),
            new("HRV", "Croatia", 56),
            new("CUB", "Cuba", 57),
            new("CUW", "Curaçao", 58),
            new("CYP", "Cyprus", 59),
            new("CZE", "Czech Republic", 60),
            new("DNK", "Denmark", 61),
            new("DJI", "Djibouti", 62),
            new("DMA", "Dominica", 63),
            new("DOM", "Dominican Republic", 64),
            new("ECU", "Ecuador", 65),
            new("EGY", "Egypt", 66),
            new("SLV", "El Salvador", 67),
            new("GNQ", "Equatorial Guinea", 68),
            new("ERI", "Eritrea", 69),
            new("EST", "Estonia", 70),
            new("ETH", "Ethiopia", 71),
            new("FLK", "Falkland Islands (Malvinas)", 72),
            new("FRO", "Faroe Islands", 73),
            new("FJI", "Fiji", 74),
            new("FIN", "Finland", 75),
            new("FRA", "France", 76),
            new("GUF", "French Guiana", 77),
            new("PYF", "French Polynesia", 78),
            new("ATF", "French Southern Territories", 79),
            new("GAB", "Gabon", 80),
            new("GMB", "Gambia", 81),
            new("GEO", "Georgia", 82),
            new("DEU", "Germany", 83),
            new("GHA", "Ghana", 84),
            new("GIB", "Gibraltar", 85),
            new("GRC", "Greece", 86),
            new("GRL", "Greenland", 87),
            new("GRD", "Grenada", 88),
            new("GLP", "Guadeloupe", 89),
            new("GUM", "Guam", 90),
            new("GTM", "Guatemala", 91),
            new("GGY", "Guernsey", 92),
            new("GIN", "Guinea", 93),
            new("GNB", "Guinea-Bissau", 94),
            new("GUY", "Guyana", 95),
            new("HTI", "Haiti", 96),
            new("HMD", "Heard Island and McDonald Islands", 97),
            new("VAT", "Holy See (Vatican City State)", 98),
            new("HND", "Honduras", 99),
            new("HKG", "Hong Kong", 100),
            new("HUN", "Hungary", 101),
            new("ISL", "Iceland", 102),
            new("IND", "India", 103),
            new("IDN", "Indonesia", 104),
            new("IRN", "Iran, Islamic Republic of", 105),
            new("IRQ", "Iraq", 106),
            new("IRL", "Ireland", 107),
            new("IMN", "Isle of Man", 108),
            new("ISR", "Israel", 109),
            new("ITA", "Italy", 110),
            new("JAM", "Jamaica", 111),
            new("JPN", "Japan", 112),
            new("JEY", "Jersey", 113),
            new("JOR", "Jordan", 114),
            new("KAZ", "Kazakhstan", 115),
            new("KEN", "Kenya", 116),
            new("KIR", "Kiribati", 117),
            new("PRK", "Korea, Democratic People's Republic of", 118),
            new("KOR", "Korea, Republic of", 119),
            new("KWT", "Kuwait", 120),
            new("KGZ", "Kyrgyzstan", 121),
            new("LAO", "Lao People's Democratic Republic", 122),
            new("LVA", "Latvia", 123),
            new("LBN", "Lebanon", 124),
            new("LSO", "Lesotho", 125),
            new("LBR", "Liberia", 126),
            new("LBY", "Libya", 127),
            new("LIE", "Liechtenstein", 128),
            new("LTU", "Lithuania", 129),
            new("LUX", "Luxembourg", 130),
            new("MAC", "Macao", 131),
            new("MKD", "Macedonia, the former Yugoslav Republic of", 132),
            new("MDG", "Madagascar", 133),
            new("MWI", "Malawi", 134),
            new("MYS", "Malaysia", 135),
            new("MDV", "Maldives", 136),
            new("MLI", "Mali", 137),
            new("MLT", "Malta", 138),
            new("MHL", "Marshall Islands", 139),
            new("MTQ", "Martinique", 140),
            new("MRT", "Mauritania", 141),
            new("MUS", "Mauritius", 142),
            new("MYT", "Mayotte", 143),
            new("MEX", "Mexico", 144),
            new("FSM", "Micronesia, Federated States of", 145),
            new("MDA", "Moldova, Republic of", 146),
            new("MCO", "Monaco", 147),
            new("MNG", "Mongolia", 148),
            new("MNE", "Montenegro", 149),
            new("MSR", "Montserrat", 150),
            new("MAR", "Morocco", 151),
            new("MOZ", "Mozambique", 152),
            new("MMR", "Myanmar", 153),
            new("NAM", "Namibia", 154),
            new("NRU", "Nauru", 155),
            new("NPL", "Nepal", 156),
            new("NLD", "Netherlands", 157),
            new("NCL", "New Caledonia", 158),
            new("NZL", "New Zealand", 159),
            new("NIC", "Nicaragua", 160),
            new("NER", "Niger", 161),
            new("NGA", "Nigeria", 162),
            new("NIU", "Niue", 163),
            new("NFK", "Norfolk Island", 164),
            new("MNP", "Northern Mariana Islands", 165),
            new("NOR", "Norway", 166),
            new("OMN", "Oman", 167),
            new("PAK", "Pakistan", 168),
            new("PLW", "Palau", 169),
            new("PSE", "Palestinian Territory, Occupied", 170),
            new("PAN", "Panama", 171),
            new("PNG", "Papua New Guinea", 172),
            new("PRY", "Paraguay", 173),
            new("PER", "Peru", 174),
            new("PHL", "Philippines", 175),
            new("PCN", "Pitcairn", 176),
            new("POL", "Poland", 177),
            new("PRT", "Portugal", 178),
            new("PRI", "Puerto Rico", 179),
            new("QAT", "Qatar", 180),
            new("REU", "Réunion", 181),
            new("ROU", "Romania", 182),
            new("RUS", "Russian Federation", 183),
            new("RWA", "Rwanda", 184),
            new("BLM", "Saint Barthélemy", 185),
            new("SHN", "Saint Helena, Ascension and Tristan da Cunha", 186),
            new("KNA", "Saint Kitts and Nevis", 187),
            new("LCA", "Saint Lucia", 188),
            new("MAF", "Saint Martin (French part)", 189),
            new("SPM", "Saint Pierre and Miquelon", 190),
            new("VCT", "Saint Vincent and the Grenadines", 191),
            new("WSM", "Samoa", 192),
            new("SMR", "San Marino", 193),
            new("STP", "Sao Tome and Principe", 194),
            new("SAU", "Saudi Arabia", 195),
            new("SEN", "Senegal", 196),
            new("SRB", "Serbia", 197),
            new("SYC", "Seychelles", 198),
            new("SLE", "Sierra Leone", 199),
            new("SGP", "Singapore", 200),
            new("SXM", "Sint Maarten (Dutch part)", 201),
            new("SVK", "Slovakia", 202),
            new("SVN", "Slovenia", 203),
            new("SLB", "Solomon Islands", 204),
            new("SOM", "Somalia", 205),
            new("ZAF", "South Africa", 206),
            new("SGS", "South Georgia and the South Sandwich Islands", 207),
            new("SSD", "South Sudan", 208),
            new("ESP", "Spain", 209),
            new("LKA", "Sri Lanka", 210),
            new("SDN", "Sudan", 211),
            new("SUR", "Suriname", 212),
            new("SJM", "Svalbard and Jan Mayen", 213),
            new("SWZ", "Swaziland", 214),
            new("SWE", "Sweden", 215),
            new("CHE", "Switzerland", 216),
            new("SYR", "Syrian Arab Republic", 217),
            new("TWN", "Taiwan, Province of China", 218),
            new("TJK", "Tajikistan", 219),
            new("TZA", "Tanzania, United Republic of", 220),
            new("THA", "Thailand", 221),
            new("TLS", "Timor-Leste", 222),
            new("TGO", "Togo", 223),
            new("TKL", "Tokelau", 224),
            new("TON", "Tonga", 225),
            new("TTO", "Trinidad and Tobago", 226),
            new("TUN", "Tunisia", 227),
            new("TUR", "Turkey", 228),
            new("TKM", "Turkmenistan", 229),
            new("TCA", "Turks and Caicos Islands", 230),
            new("TUV", "Tuvalu", 231),
            new("UGA", "Uganda", 232),
            new("UKR", "Ukraine", 233),
            new("ARE", "United Arab Emirates", 234),
            new("GBR", "United Kingdom", 235),
            new("USA", "United States", 236),
            new("UMI", "United States Minor Outlying Islands", 237),
            new("URY", "Uruguay", 238),
            new("UZB", "Uzbekistan", 239),
            new("VUT", "Vanuatu", 240),
            new("VEN", "Venezuela, Bolivarian Republic of", 241),
            new("VNM", "Viet Nam", 242),
            new("VGB", "Virgin Islands, British", 243),
            new("VIR", "Virgin Islands, U.S.", 244),
            new("WLF", "Wallis and Futuna", 245),
            new("ESH", "Western Sahara", 246),
            new("YEM", "Yemen", 247),
            new("ZMB", "Zambia", 248),
            new("ZWE", "Zimbabwe", 249),
        };
    }
}
