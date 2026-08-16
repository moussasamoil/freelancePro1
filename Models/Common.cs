using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;

namespace lotus_blue.Models
{
    public class Common
    {
        public enum Countries
        {
            العراق = 1,
            الإمارات = 2,
            قطر = 3,
            ليبيا = 4,
            سلطنة_عمان = 5,
            فلسطين = 6,
            تركيا = 7,
            الأردن = 8,
            الكويت = 9,
            البحرين = 10,
            السعودية = 11,
            تونس = 12,
            المغرب = 13,
            الجزائر = 14,
            لبنان = 15,
            مصر = 16
        }

        public static Dictionary<Countries, List<string>> CitiesByCountry = new Dictionary<Countries, List<string>>
        {
            { Countries.العراق, new List<string> { "بغداد", "الموصل", "البصرة", "كربلاء", "النجف", "سماوه", "كوت", "الأنبار","السليمانية", "أربيل", "دهوك", "صلاح الدين","الديوانية","ميسان" ,"سامراء", "الرمادي", "الفلوجة", "الكوت","ديالي","ناصرية", "كركوك", "بابل", "حلبجة", "الخالص","ذي قار", "عفك", "تكريت", "بعقوبة" } },
            { Countries.الإمارات, new List<string> { "دبي", "أبو ظبي", "الشارقة", "عجمان", "رأس الخيمة", "كلباء","الرويس","الفجيرة", "أم القيوين", "العين", "جبل علي","خورفكان", "مدينة محمد بن راشد", "حتا", "دبا الفجيرة", "مدينة زايد","دبا الحصن" } },
            { Countries.قطر, new List<string> { "الدوحة", "الريان", "الخور", "الوكرة", "الشحانية", "السيلية", "الوجبة", "الغرافة", "الخريطيات", "الدخان", "الشمال", "المرخية", "أم صلال", "الزخرية", "الغوارية", "مسيعيد" } },
            { Countries.ليبيا, new List<string> {
                // مخزن بنغازي
                "الرويسات", "القوارشة", "شبنة", "فينيسيا", "حي السلام", "بوعطني", "بنغازي",
                "رقدالين", "بن جواد", "البيضاء", "المرج", "درنه", "طبرق", "بني وليد", "قمينس",
                // مخزن سوق الجمعه
                "مصراته", "مصراتة الكبري", "تازربو", "مساعد/امساعد", "زلتين", "طرابلس",
                "زناتة", "زناتة الجديدة", "الزاوية", "صبراته", "صرمان", "العجيلات", "الجميل",
                "زوارة", "القريولي", "قصر الاخيار", "الخمس", "سرت", "راس لانوف", "البريقة",
                "جالو", "اوجلة", "الابيار", "القبة/القبلة", "سبها", "الجفره", "الشويرف",
                "براك الشاطئ", "هون", "زلة", "سوكنة", "ودان", "اوباري", "مرزق", "اجدابيا",
                "غريان", "يفرن", "نالوت", "القلعة", "الزنتان", "الرحبيات", "ترهونه", "مسلاته",
                "عين زاره", "تاجوراء", "وادي الربيع", "الكريميه", "جنزور", "النجيلة", "زلطن",
                "شحات", "الاصابعه", "الابرق", "ورشفانه", "قصر بن غشير", "السواني", "ككلة",
                "تيجي", "كاباو", "الرياينة", "الرجبان", "القواليش", "الحوامد", "الرابطه",
                "العزيزيه", "الجوش", "طمزين", "بدر", "مزدة", "ام الارانب", "تراغن",
                "وادي عبته", "العلوص", "غات", "اجخرة", "خلة الفرجان", "سوسة", "اسبيعه",
                "القطرون", "توكره", "سلوق", "القريات", "جادو", "الساعدية", "الهيرة",
                "سوق الخميس-امسيحل", "مزدة", "تاورغاء", "الشقيقة", "طريق المطار",
                "بن عاشور", "سراج", "راس لأنوف",
                // غير متوفر توصيل
                "درج", "غدامس",
                // مدن إضافية موجودة في النظام
                "القويمات", "الغريفة", "الكفره", "الايبار", "سكته", "الرقيعات", "قصر الحاج",
                "شكشوك", "غدوة", "نازريو", "سوق الخميس", "امسيحل", "الوشكه", "سوق البنت",
                "زلة الجفره", "بير قدم", "جبل الغربي"
            }},
            { Countries.سلطنة_عمان, new List<string> { "مسقط", "صلالة", "صحار", "نزوى", "صور", "كبر", "البريمي", "الظاهرة", "الخابورة", "السويق", "إبراء", "المدية", "ظفار", "الرستاق", "صور","الداخلية","جنوب الباطنة","شمال الباطنية","شمال الشرقية","جنوب الشرقية ","مسندم","الوسطى" } },
            { Countries.تركيا, new List<string> { "اسطنبول", "أنقرة", "إزمير", "أيفون", "شرنق","كيلس", "كرشيهير ", " بالكسير ", "مانيسا ", "بوردور ", "افيون ", "أنطاليا", "كوتهايا", "إسبرطة", "اكسراي", "اردنة" , "اماسيا  " , "تشورلو", "جوورم", "شناق قلعة", "منيسيا ", "تشانكري ", "سيواس ", "كوجالي","بولو ", "الريحانية", "بيتلس", "اديامان", "كهرمان مرعش", "شانلي أورفا", "الانيا", "كارامان","بورصة","فان","أضنة", "قيصري", "مرسين","اسكي شاهير ", "ديار بكر","يوزغات", "اعزاز", "اديامان", "أزميت ", "بيتلس", "الايزاغ", "بيواجيك", "دوزجا", "نيفشهير ", "ماردين","بيلاجيك","كيركلاريلي","ملاطية","أوردو","قونيا", "تشوروم", "نيدا", "تكيرداغ ", "عثمانية","يالوفا","كارابوك","سكاريا","موغلا","دنيزلي", "غازي عنتاب","أنقرة", "أنطاكيا", "هاتاي","أديامان", "باتمان", "أفيون", "طرابزون ", "سامسون" ,"مجهول"} },
            { Countries.الأردن, new List<string> { "عمان", "الزرقاء", "إربد", "العقبة", "السلط", "معان", "جرش", "عجلون", "الكرك", "الطفيلة", "مادبا", "المفرق", "الرمثا", "البتراء" } },
            { Countries.البحرين, new List<string> { "المنامة", "المحرق", "الرفاع","سلمان" ,"عيسى", "الجنبيه", "عالي", "المالكية", "توبالي", "جدحفص", "الحد", "الحلة", "مدينة عيسى", "الدراز", "سار", "العدلية", "المقشع", "المحرق الكبرى", "مدينة حمد", "البديع", "السهلة" } },
            { Countries.السعودية, new List<string> { "الرياض", "جدة", "مكة المدينة", "المدينة المنورة", "الدمام", "الخبر", "الجبيل", "الأحساء", "الطائف", "بريدة", "تبوك", "الخرج", "حائل", "نجران", "جيزان", "عرعر", "القريات", "الباحة", "أبها", "الزلفي", "الرس", "سكاكا", "الظهران", "القطيف", "الخفجي", "عنيزة", "ينبع", "بيشة", "العرضيات", "تاروت", "الرميلة", "البدائع", "رفحاء", "الأفلاج", "العيينة", "الزلفي", "المجمعة", "عيينة", "الراشدية", "الشقيق", "شرورة", "الحفر", "الجوف", "الوجه", "الحريق", "أبو عريش", "رأس تنورة", "البكيرية", "الحلة", "الغاط", "السليل", "الحائط", "ضباء", "بارق", "بيش", "السليمانية", "الخرمة", "الخميس مشيط", "العلا", "سيهات", "صبيا", "عسير", "شقراء", "سلوى", "بلجرشي", "الريث", "الدوادمي", "شحات", "الدوادمي", "الدوادمي", "العيص", "قيا", "أحد رفيده", "المخواة", "البدائع", "الغريق", "عفيف", "رفحاء", "حوطة بني تميم", "الحسينية", "الزلفي", "الزلفي", "الخبر", "المدينة المنورة", "المدينة المنورة", "القنفذة", "عنيزة", "رياض الخبراء", "الدمام", "الدمام", "الرميلة", "الظهران", "الظهران", "القرين", "الجبيل", "الجبيل", "القطيف", "القطيف", "الباحة", "أبها", "جازان", "جيزان" } },
            { Countries.الكويت, new List<string> { "الكويت", "الأحمدي", "حولي", "الفروانية", "مبارك الكبير", "الجهراء" } },
            { Countries.تونس, new List<string> {
                "أريانة", "البجة", "بن عروس", "بنزرت", "قابس", "قفصة", "جندوبة", "القيروان",
                "القصرين", "قبلي", "الكاف", "المهدية", "منوبة", "مدنين", "المنستير", "نابل",
                "صفاقس", "سيدي بوزيد", "سليانة", "سوسة", "تطاوين", "توزر", "تونس", "زغوان"
              }},
              { Countries.فلسطين, new List<string> {
               "الضفة" ,"رام الله", "طولكرم","جنين","سلفيت",
                                 "طوباس" ,"نابلس", "بيت لحم","الخليل ","عناتا",
                                 "ابو ديس" ," العيزرية", "حزما","القدس","ابو غوش",
                                 "عين رافا" ,"الداخل اراضي 48",
              }},
            { Countries.مصر, new List<string> {
                "القاهرة", "الإسكندرية", "الجيزة", "القليوبية", "الشرقية", "الدقهلية",
                "دمياط", "الغربية", "المنوفية", "كفر الشيخ", "البحيرة", "مطروح",
                "بورسعيد", "السويس", "الإسماعيلية", "شمال سيناء", "جنوب سيناء",
                "الفيوم", "بني سويف", "المنيا", "سوهاج", "قنا", "الأقصر", "أسوان",
                "أسيوط", "الوادي الجديد", "البحر الأحمر"
            }}
        };

        public static Dictionary<Countries, string> CurrencyByCountry = new Dictionary<Countries, string>
        {
            { Countries.العراق, "IQD" },
            { Countries.الإمارات, "AED" },
            { Countries.قطر, "QAR" },
            { Countries.ليبيا, "LYD" },
            { Countries.سلطنة_عمان, "OMR" },
            { Countries.فلسطين, "ILS" }, // Palestine uses several currencies including the New Israeli Shekel
            { Countries.تركيا, "TRY" },
            { Countries.الأردن, "JOD" },
            { Countries.البحرين, "BHD" },
            { Countries.السعودية, "SAR" },
            { Countries.تونس, "TND" },
            {Countries.الكويت,"KWD" },
             {Countries.المغرب,"MAD" },
                        {Countries.الجزائر,"DZD" },
                                                {Countries.لبنان,"LBP" },
            { Countries.مصر, "EGP" }

        };

        // Bridge for the employee module's string-based country field
        // (Employee.Country, populated by EmployeeController.NormalizeEmployeeCountry).
        // Returns null for unmapped/empty values so callers fall back to USD.
        public static Countries? EmployeeCountryToEnum(string? country) => country?.Trim() switch
        {
            "Egypt"   => Countries.مصر,
            "Turkey"  => Countries.تركيا,
            "Iraq"    => Countries.العراق,
            "Jordan"  => Countries.الأردن,
            "Libya"   => Countries.ليبيا,
            "Kuwait"  => Countries.الكويت,
            "Qatar"   => Countries.قطر,
            "Oman"    => Countries.سلطنة_عمان,
            "Bahrain" => Countries.البحرين,
            "Tunisia" => Countries.تونس,
            _         => null,
        };

        public static Dictionary<Countries, string> PhoneNumberCodesByCountry = new Dictionary<Countries, string>
                {
                    { Countries.العراق, "+964" },
                    { Countries.الإمارات, "+971" },
                    { Countries.قطر, "+974" },
                    { Countries.ليبيا, "+218" },
                    { Countries.سلطنة_عمان, "+968" },
                    { Countries.فلسطين, "+970" },
                    { Countries.تركيا, "+90" },
                    { Countries.الأردن, "+962" },
                    { Countries.البحرين, "+973" },
                    { Countries.السعودية, "+966" },
                    { Countries.تونس, "+216" },
                    { Countries.الكويت, "+965" }
                };

        public static Dictionary<Countries, string> ImageUrlByCountry = new Dictionary<Countries, string>
        {
            { Countries.العراق, "/Countries/iraq.svg" },
            { Countries.الإمارات, "/Countries/emirates.svg" },
            { Countries.قطر, "/Countries/qatar.svg" },
            { Countries.ليبيا, "/Countries/libya.svg" },
            { Countries.سلطنة_عمان, "/Countries/oman.svg" },
            { Countries.فلسطين, "/Countries/palestine.svg" },
            { Countries.تركيا, "/Countries/turkey.svg" },
            { Countries.الأردن, "/Countries/jordan.svg" },
            { Countries.البحرين, "/Countries/bahrain.svg" },
            { Countries.السعودية, "/Countries/saudiarabia.svg" },
            { Countries.تونس, "/Countries/tunisia.svg" },
            { Countries.الكويت, "/Countries/kuwait.svg" },
            { Countries.الجزائر, "/Countries/algeria.svg" },
            { Countries.المغرب, "/Countries/morocco.svg" },
            { Countries.لبنان, "/Countries/lebanon.svg" },
            { Countries.مصر, "/Countries/egypt.svg" },
        };

        public static Dictionary<OrderSourceEnum, string> SocialMediaIconUrl = new Dictionary<OrderSourceEnum, string>
        {
            { OrderSourceEnum.فيسبوك, "/socialmediaicons/facebook.svg" },
            { OrderSourceEnum.انستغرام, "/socialmediaicons/instagram.svg" },
            { OrderSourceEnum.سناب_شات, "/socialmediaicons/snapchat.svg" },
            { OrderSourceEnum.واتساب, "/socialmediaicons/whatsapp.svg" },
            { OrderSourceEnum.تك_توك, "/socialmediaicons/tiktok.svg" },
            { OrderSourceEnum.توتير, "/socialmediaicons/twitter.svg" },
            { OrderSourceEnum.ويبسات, "/socialmediaicons/internet.svg" },
            { OrderSourceEnum.اتصال, "/socialmediaicons/phone.svg" },
            { OrderSourceEnum.تلغرام, "/socialmediaicons/telegram.svg" },
            { OrderSourceEnum.ميتا, "/socialmediaicons/meta.svg" },
        };

        public static Dictionary<OrderStatusEnum, string> StatusIconUrl = new Dictionary<OrderStatusEnum, string>
        {
            { OrderStatusEnum.طلب_جديد, "/static/blue.svg" },
            { OrderStatusEnum.تم_التجهيز, "/static/blue.svg" },
            { OrderStatusEnum.قيد_التوصيل, "/static/yellow.svg" },
            { OrderStatusEnum.تم_التسليم, "/static/green.svg" },
            { OrderStatusEnum.فشل_التسليم, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_1, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_2, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_3, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_4, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_5, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_6, "/static/red.svg" },
            { OrderStatusEnum.فشل_التسليم_7, "/static/red.svg" },
            { OrderStatusEnum.تم_المعالجة, "/static/yellow.svg" },
            { OrderStatusEnum.تم_التسليم_المؤقت, "/static/green.svg" },

            { OrderStatusEnum.الطلبات_المؤجلة, "/static/yellow.svg" },
            { OrderStatusEnum.تم_الإلغاء, "/static/red.svg" },
            { OrderStatusEnum.انتظار_المعالجة, "/static/red.svg" },
            { OrderStatusEnum.الطلبات_المرجعة, "/static/red.svg" },
            { OrderStatusEnum.أخطاء_الشركات_والمندوبين, "/static/red.svg" },
            { OrderStatusEnum.تم_تحديث_الرصيد, "/static/green.svg" },
            { OrderStatusEnum.تم_الدفع, "/static/green.svg" },
            { OrderStatusEnum.أرشيف_المرجع, "/static/red.svg" },
                        { OrderStatusEnum.الطلبات_المعلقة, "/static/red.svg" },
                                                { OrderStatusEnum.الطلبات_الغير_معرفة, "/static/red.svg" },
                                                                                                { OrderStatusEnum.الطلبات_الغير_مكتملة, "/static/white-blue.svg" },
            { OrderStatusEnum.الطلبات_الغير_مكتملة_1, "/static/white-blue.svg" },
            { OrderStatusEnum.الطلبات_الغير_مكتملة_2, "/static/white-blue.svg" },
            { OrderStatusEnum.الطلبات_الغير_مكتملة_3, "/static/white-blue.svg" },
            { OrderStatusEnum.الطلبات_الغير_مكتملة_4, "/static/white-blue.svg" },
            { OrderStatusEnum.الطلبات_الغير_مكتملة_5, "/static/white-blue.svg" },
            { OrderStatusEnum.الطلبات_الغير_مكتملة_6, "/static/white-blue.svg" },

        };


        private static Dictionary<OrderStatusesForDeliveryCompanyAndRepresentativeEnum, string> StatusIconUrlV2 = new Dictionary<OrderStatusesForDeliveryCompanyAndRepresentativeEnum, string>
        {
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.طلب_جديد, "/static/blue.svg" },
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_التجهيز, "/static/blue.svg" },
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.قيد_التوصيل, "/static/yellow.svg" },
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_التسليم, "/static/green.svg" },
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_المعالجة, "/static/yellow.svg" },
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.انتظار_المعالجة, "/static/red.svg" },
            { OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_تحديث_الرصيد, "/static/green.svg" },
        };

        private static Dictionary<OrderStatusesForEmployeesEnum, string> StatusIconUrlV3 = new Dictionary<OrderStatusesForEmployeesEnum, string>
        {
            { OrderStatusesForEmployeesEnum.فشل_التسليم, "/static/red.svg" },
            { OrderStatusesForEmployeesEnum.تم_المعالجة, "/static/yellow.svg" },
            { OrderStatusesForEmployeesEnum.تم_الإلغاء, "/static/red.svg" },
            { OrderStatusesForEmployeesEnum.انتظار_المعالجة, "/static/red.svg" },
            { OrderStatusesForEmployeesEnum.الطلبات_المرجعة, "/static/red.svg" },
            { OrderStatusesForEmployeesEnum.أخطاء_التوصيل, "/static/red.svg" },
            { OrderStatusesForEmployeesEnum.تم_التسليم_المؤقت, "/static/green.svg" },

        };




        private static Dictionary<OrderStatusesForFollowUpDepartmentEnum, string> StatusIconUrlV4 = new Dictionary<OrderStatusesForFollowUpDepartmentEnum, string>
        {
            { OrderStatusesForFollowUpDepartmentEnum.طلب_جديد, "/static/blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.تم_التجهيز, "/static/blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.قيد_التوصيل, "/static/yellow.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.فشل_التسليم, "/static/red.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.تم_المعالجة, "/static/yellow.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.تم_التسليم, "/static/green.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.تم_الإلغاء, "/static/red.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.انتظار_المعالجة, "/static/red.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_المرجعة, "/static/red.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.أخطاء_التوصيل, "/static/red.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.تم_التسليم_المؤقت, "/static/green.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.تم_تحديث_الرصيد, "/static/green.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_معرفة, "/static/red.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_مكتملة, "/static/white-blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_مكتملة_2, "/static/white-blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_مكتملة_3, "/static/white-blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_مكتملة_4, "/static/white-blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_مكتملة_5, "/static/white-blue.svg" },
            { OrderStatusesForFollowUpDepartmentEnum.الطلبات_الغير_مكتملة_6, "/static/white-blue.svg" },


        };








        public static Dictionary<string, (string HeaderText, string IconClass)> HeaderMappings = new Dictionary<string, (string, string)>
            {
                { "/Financial/OrderByCountry", ("حساباتي", "fa-light fa-coins") },
                { "/financial/OrderReports", ("كشوف الحسابات", "fa-light fa-coins") },
                { "/Financial/OrderByDeliveryCompany", ("حسابات شركات التوصيل", "fa-light fa-file-invoice-dollar") },
                { "/financial/Employees", ("حسابات الموظف", "fa-solid fa-user-gear") },
                { "/warehouse", ("ادارة المستودعات", "fa-light fa-store") },
                { "/Warehouse/Edit", ("  تعديل المستودع", "fa-duotone fa-rectangle-history-circle-user") },
                { "/Warehouse/create", ("  اضافة مستودع", "fa-light fa-rectangle-history-circle-plus") },
                { "/Warehouse/Details", ("تفاصيل المستودع  ", "fa-sharp fa-regular fa-rectangle-vertical-history") },
                { "/Employee", (" ادارة الموظفين", "fa-sharp fa-light fa-memo-circle-info") },
                { "/employee/create", (" اضافة الموظف ", "fa-sharp fa-light fa-user-plus") },
                { "/Employee/Edit", ("  تعديل موظف", "fa-light fa-user-pen") },
                { "/Employee/Details", ("  تفاصيل الموظف", "fa-light fa-file-circle-info") },
                { "/EmployeeTransactions/create", ("  اضافة مصروف ", "fa-light fa-pen-field") },
                { "/EmployeeTransactions", ("ادارة المصروفات  ", "fa-light fa-file-invoice-dollar") },
                { "/rating/employelist", ("تقييم الموظف  ", "fa-sharp fa-light fa-circle-star") },
                { "/rating/ManufactureCompanyDetails", ("التقييمات الكلية للموظفين   ", "fa-sharp fa-light fa-circle-star") },
                { "/DeliveryCompany/create", ("اضافة شركات التوصيل  ", "fa-light fa-light fa-shop") },
                { "/DeliveryCompany", ("ادارة شركات التوصيل", "fa-light fa-bars-progress") },
                { "/DeliveryCompanyPrice", ("ادارة اسعار التوصيل", "fa-sharp fa-light fa-backpack") },
                { "/DeliveryCompanyPrice/create", ("تعيين اسعار التوصيل  ", "fa-solid fa-square-dashed-circle-plus") },
                { "/DeliveryCompany/Edit", ("تعديل شركة التوصيل  ", "fa-light fa-pen-to-square") },
                { "/DeliveryCompany/Details", ("  معلومات شركة التوصيل", "fa-light fa-circle-info") },
                { "/ManufacturingCompany", ("  ادارة  المتاجر", "fa-light fa-bars-progress") },
                { "/ManufacturingCompany/create", (" اضافة  متجر ", "fa-light fa-light fa-shop") },
                { "/ExchangeRate/create", ("اضافة سعر تصريف", "fa-light fa-circle-dollar") },
                { "/ExchangeRate", ("  اسعار التصريف", "fa-light fa-chart-mixed-up-circle-dollar") },
                { "/Order", ("   طلبات التجهيز", "fa-light fa-chart-mixed-up-circle-dollar") },
                { "/Rating/EmployeOrdersDetails", ("تقييم الموظف", "fa-sharp fa-light fa-circle-star") },
                { "/rating/CityByCountryDetails", ("تقييم المدن", "fa-sharp fa-light fa-circle-star") },
                { "/Order/OrderFilteredByMultipleStatuses", ("   طلبات المنجزة", "fa-light fa-chart-mixed-up-circle-dollar") },

        };




        public static string GetOrderStatusClass(OrderStatusEnum orderStatus)
        {
            return orderStatus switch
            {
                OrderStatusEnum.تم_التسليم => "text-green-v2",
                OrderStatusEnum.تم_تحديث_الرصيد => "text-green-v2",
                OrderStatusEnum.تم_الدفع => "text-green-v2",
                OrderStatusEnum.تم_التسليم_المؤقت => "text-green-v2",

                OrderStatusEnum.فشل_التسليم => "text-red",
                OrderStatusEnum.فشل_التسليم_1 => "text-red",
                OrderStatusEnum.فشل_التسليم_2 => "text-red",
                OrderStatusEnum.فشل_التسليم_3 => "text-red",
                OrderStatusEnum.فشل_التسليم_4 => "text-red",
                OrderStatusEnum.فشل_التسليم_5 => "text-red",
                OrderStatusEnum.فشل_التسليم_6 => "text-red",
                OrderStatusEnum.فشل_التسليم_7 => "text-red",
                OrderStatusEnum.الطلبات_المرجعة => "text-red",
                OrderStatusEnum.أرشيف_المرجع => "text-red",
                OrderStatusEnum.أخطاء_الشركات_والمندوبين => "text-red",
                OrderStatusEnum.انتظار_المعالجة => "text-red",
                OrderStatusEnum.تم_الإلغاء => "text-red",

                OrderStatusEnum.طلب_جديد => "text-blue",
                OrderStatusEnum.تم_التجهيز => "text-blue",

                OrderStatusEnum.تم_المعالجة => "text-yellow-v2",
                OrderStatusEnum.قيد_التوصيل => "text-yellow-v2",
                OrderStatusEnum.الطلبات_المؤجلة => "text-yellow-v2",
                OrderStatusEnum.الطلبات_الغير_مكتملة => "text-blue",
                OrderStatusEnum.الطلبات_الغير_مكتملة_1 => "text-blue",
                OrderStatusEnum.الطلبات_الغير_معرفة => "text-red",

                _ => ""
            };
        }



        public static string GetPhoneNumberCodeByCountryStatus(string countryName)
        {

            // Try to parse the input string to the Countries enum
            if (Enum.TryParse(countryName.Replace(" ", "_"), out Countries countryEnum))
            {
                // Check if the country exists in the CurrencyByCountry dictionary
                if (PhoneNumberCodesByCountry.TryGetValue(countryEnum, out string phoneNumberCode))
                {
                    return phoneNumberCode;
                }
                else
                {
                    // Handle the case where the country is not found in the dictionary
                    return "Country not found";
                }
            }
            else
            {
                // Handle the case where the input string does not match any country in the enum
                return "Invalid country name";
            }

        }

        public static string GetImageUrlByCountryName(string countryName)
        {
            // Try to parse the input string to the Countries enum
            if (Enum.TryParse(countryName.Replace(" ", "_"), out Countries countryEnum))
            {
                // Check if the country exists in the ImageUrlByCountry dictionary
                if (ImageUrlByCountry.TryGetValue(countryEnum, out string imageUrl))
                {
                    return imageUrl;
                }
                else
                {
                    // Handle the case where the country is not found in the dictionary
                    return "Country not found";
                }
            }
            else
            {
                // Handle the case where the input string does not match any country in the enum
                return "Invalid country name";
            }
        }
        public static string GetCurrencyByCountryName(string countryName)
        {
            // Try to parse the input string to the Countries enum
            if (Enum.TryParse(countryName.Replace(" ", "_"), out Countries countryEnum))
            {
                // Check if the country exists in the CurrencyByCountry dictionary
                if (CurrencyByCountry.TryGetValue(countryEnum, out string currencyCode))
                {
                    return currencyCode;
                }
                else
                {
                    // Handle the case where the country is not found in the dictionary
                    return "Country not found";
                }
            }
            else
            {
                // Handle the case where the input string does not match any country in the enum
                return "Invalid country name";
            }
        }


        public static string GetSocialMediaIconUrl(OrderSourceEnum orderSource)
        {
            if (SocialMediaIconUrl.TryGetValue(orderSource, out string url))
            {
                return url;
            }
            else
            {
                return "/socialmediaicons/DefaultImage.svg";
            }
        }


        public static string GetStatusIconUrl(OrderStatusEnum orderStatus)
        {
            if (StatusIconUrl.TryGetValue(orderStatus, out string url))
            {
                return url;
            }
            else
            {
                return "static/DefaultImage.svg"; // Default URL
            }
        }




        public static (string HeaderText, string IconClass) GetHeaderContent(string url)
        {
            url ??= string.Empty;

            if (url.StartsWith("/ProductMinimumSellingPrices", StringComparison.OrdinalIgnoreCase)
                || url.Contains("ProductMinimumSellingPrices", StringComparison.OrdinalIgnoreCase))
            {
                return ("الحد الأدنى للبيع", "fa-light fa-tags");
            }

            foreach (var mapping in HeaderMappings)
            {
                if (url.Contains(mapping.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return mapping.Value;
                }
            }

            return ("Default Header", "default-icon-class");
        }





    }
}
