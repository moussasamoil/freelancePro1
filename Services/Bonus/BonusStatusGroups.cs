using lotus_blue.Models;

namespace lotus_blue.Services.Bonus
{
    public static class BonusStatusGroups
    {
        public static readonly OrderStatusEnum[] Success = new[]
        {
            OrderStatusEnum.تم_التسليم,            // 6
            OrderStatusEnum.تم_تحديث_الرصيد,        // 13
            OrderStatusEnum.تم_الدفع                // 14
        };

        public static readonly OrderStatusEnum[] Failure = new[]
        {
            OrderStatusEnum.فشل_التسليم,       // 7
            OrderStatusEnum.فشل_التسليم_1,     // 30
            OrderStatusEnum.فشل_التسليم_2,     // 19
            OrderStatusEnum.فشل_التسليم_3,     // 20
            OrderStatusEnum.فشل_التسليم_4,     // 21
            OrderStatusEnum.فشل_التسليم_5,     // 22
            OrderStatusEnum.فشل_التسليم_6,     // 23
            OrderStatusEnum.فشل_التسليم_7,     // 24
            OrderStatusEnum.انتظار_المعالجة,   // 8
            OrderStatusEnum.الطلبات_المرجعة,   // 9
            OrderStatusEnum.أرشيف_المرجع       // 15
        };

        public static readonly OrderStatusEnum[] Collection = new[]
        {
            OrderStatusEnum.طلب_جديد,           // 0
            OrderStatusEnum.تم_التجهيز,         // 2
            OrderStatusEnum.قيد_التوصيل,        // 4
            OrderStatusEnum.تم_التسليم_المؤقت   // 5
        };

        public static readonly OrderStatusEnum[] Incomplete = new[]
        {
            OrderStatusEnum.الطلبات_الغير_مكتملة,    // 18
            OrderStatusEnum.الطلبات_الغير_مكتملة_1,  // 31
            OrderStatusEnum.الطلبات_الغير_مكتملة_2,  // 25
            OrderStatusEnum.الطلبات_الغير_مكتملة_3,  // 26
            OrderStatusEnum.الطلبات_الغير_مكتملة_4,  // 27
            OrderStatusEnum.الطلبات_الغير_مكتملة_5,  // 28
            OrderStatusEnum.الطلبات_الغير_مكتملة_6   // 29
        };

        public static readonly OrderStatusEnum[] Delayed = new[]
        {
            OrderStatusEnum.الطلبات_المؤجلة     // 12
        };

        public static BonusStatClass? Classify(OrderStatusEnum status)
        {
            if (Array.IndexOf(Success, status) >= 0) return BonusStatClass.Success;
            if (Array.IndexOf(Failure, status) >= 0) return BonusStatClass.Failure;
            if (Array.IndexOf(Collection, status) >= 0) return BonusStatClass.Collection;
            if (Array.IndexOf(Incomplete, status) >= 0) return BonusStatClass.Incomplete;
            if (Array.IndexOf(Delayed, status) >= 0) return BonusStatClass.Delayed;
            return null;
        }

        public static bool IsSuccess(OrderStatusEnum status) => Array.IndexOf(Success, status) >= 0;

        // Maps the class name used in /Home/Index?bonusClass=… to its underlying status set.
        // "ProcessedForOthers" returns the Success array since the card narrows by Fixedby,
        // not by a distinct status group.
        public static OrderStatusEnum[] ResolveClassName(string name) => name switch
        {
            "Success" => Success,
            "Failure" => Failure,
            "Collection" => Collection,
            "Incomplete" => Incomplete,
            "Delayed" => Delayed,
            "ProcessedForOthers" => Success,
            _ => null
        };

        public static string ColorHex(BonusStatClass cls) => cls switch
        {
            BonusStatClass.Success => "#2ab149",
            BonusStatClass.Failure => "#FF0000",
            BonusStatClass.Collection => "#007bff",
            BonusStatClass.Incomplete => "#FF0000",
            BonusStatClass.Delayed => "#ffc107",
            _ => "#888888"
        };

        // Cyan — the "orders you processed for someone else" sub-class.
        // Derived: success-class order AND Fixedby != null AND Fixedby != ApplicationUserId.
        public const string ProcessedForOthersColor = "#45bbea";

        public static string ArabicLabel(BonusStatClass cls) => cls switch
        {
            BonusStatClass.Success => "الطلبات الناجحة",
            BonusStatClass.Failure => "فشل التسليم",
            BonusStatClass.Collection => "قيد التحصيل",
            BonusStatClass.Incomplete => "الطلبات غير المكتملة",
            BonusStatClass.Delayed => "الطلبات المؤجلة",
            _ => string.Empty
        };
    }
}
