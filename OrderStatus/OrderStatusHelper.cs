using lotus_blue.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace lotus_blue.OrderStatus
{
    public static class OrderStatusHelper
    {
        // Cached set of valid failure reason Display Names for server-side validation
        private static readonly HashSet<string> ValidFailureReasonNames = new HashSet<string>(
            Enum.GetValues(typeof(FailureReasonEnum))
                .Cast<FailureReasonEnum>()
                .Select(r => typeof(FailureReasonEnum)
                    .GetMember(r.ToString())[0]
                    .GetCustomAttribute<DisplayAttribute>()?.Name ?? r.ToString())
        );

        public static bool IsValidFailureReason(string? reason)
        {
            return !string.IsNullOrEmpty(reason) && ValidFailureReasonNames.Contains(reason);
        }

        public static bool IsFailureStatus(OrderStatusEnum status)
        {
            return status == OrderStatusEnum.فشل_التسليم
                || status == OrderStatusEnum.فشل_التسليم_1
                || status == OrderStatusEnum.فشل_التسليم_2
                || status == OrderStatusEnum.فشل_التسليم_3
                || status == OrderStatusEnum.فشل_التسليم_4
                || status == OrderStatusEnum.فشل_التسليم_5
                || status == OrderStatusEnum.فشل_التسليم_6
                || status == OrderStatusEnum.فشل_التسليم_7;
        }

        public static bool IsIncompleteStatus(OrderStatusEnum status)
        {
            return status == OrderStatusEnum.الطلبات_الغير_مكتملة
                || status == OrderStatusEnum.الطلبات_الغير_مكتملة_1
                || status == OrderStatusEnum.الطلبات_الغير_مكتملة_2
                || status == OrderStatusEnum.الطلبات_الغير_مكتملة_3
                || status == OrderStatusEnum.الطلبات_الغير_مكتملة_4
                || status == OrderStatusEnum.الطلبات_الغير_مكتملة_5
                || status == OrderStatusEnum.الطلبات_الغير_مكتملة_6;
        }

        public static bool RequiresLegalFailureReason(OrderStatusEnum status)
        {
            return IsFailureStatus(status)
                || status == OrderStatusEnum.انتظار_المعالجة
                || status == OrderStatusEnum.الطلبات_المرجعة
                || status == OrderStatusEnum.أرشيف_المرجع;
        }

        public static bool IsFailedOrder(OrderStatusEnum status)
        {
            return IsFailureStatus(status)
                || status == OrderStatusEnum.انتظار_المعالجة
                || status == OrderStatusEnum.الطلبات_المرجعة
                || status == OrderStatusEnum.تم_الإلغاء;
        }

        public static bool IsSuccessfulOrder(OrderStatusEnum status)
        {
            return status == OrderStatusEnum.تم_التسليم
                || status == OrderStatusEnum.تم_تحديث_الرصيد
                || status == OrderStatusEnum.تم_الدفع;
        }

        // Simplified to directly check the specific status
        public static bool ShouldRefundToWarehouse(OrderStatusEnum orderStatus)
        {
            return orderStatus == OrderStatusEnum.فشل_التسليم || orderStatus == OrderStatusEnum.تم_الإلغاء;
        }

        // Method to get valid next statuses
        public static List<OrderStatusEnum> GetValidNextStatuses(OrderStatusEnum currentStatus)
        {
            switch (currentStatus)
            {
                case OrderStatusEnum.طلب_جديد:
                    return new List<OrderStatusEnum> { OrderStatusEnum.تم_التجهيز, OrderStatusEnum.تم_الإلغاء , OrderStatusEnum.الطلبات_الغير_مكتملة};

                case OrderStatusEnum.تم_التجهيز:
                    return new List<OrderStatusEnum> { OrderStatusEnum.قيد_التوصيل, OrderStatusEnum.الطلبات_الغير_مكتملة };

                case OrderStatusEnum.قيد_التوصيل:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم, OrderStatusEnum.تم_التسليم,  };

             
                case OrderStatusEnum.فشل_التسليم:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_1 };

                case OrderStatusEnum.فشل_التسليم_1:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_2 };

                case OrderStatusEnum.فشل_التسليم_2:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_3 };

                case OrderStatusEnum.فشل_التسليم_3:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_4 };

                case OrderStatusEnum.فشل_التسليم_4:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_5 };

                case OrderStatusEnum.فشل_التسليم_5:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_6 };

                case OrderStatusEnum.فشل_التسليم_6:
                    return new List<OrderStatusEnum> { OrderStatusEnum.فشل_التسليم_7 };

                case OrderStatusEnum.فشل_التسليم_7:
                    return new List<OrderStatusEnum> { OrderStatusEnum.انتظار_المعالجة };

                case OrderStatusEnum.أخطاء_الشركات_والمندوبين:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_المرجعة, OrderStatusEnum.تم_المعالجة };

                case OrderStatusEnum.تم_التسليم:
                    return new List<OrderStatusEnum> { OrderStatusEnum.تم_تحديث_الرصيد, };

                case OrderStatusEnum.تم_التسليم_المؤقت:
                    return new List<OrderStatusEnum> { OrderStatusEnum.تم_التسليم, };


                case OrderStatusEnum.انتظار_المعالجة:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_المرجعة };

                case OrderStatusEnum.الطلبات_المرجعة:
                    return new List<OrderStatusEnum> { OrderStatusEnum.أرشيف_المرجع };

                case OrderStatusEnum.الطلبات_المؤجلة:
                    return new List<OrderStatusEnum> { OrderStatusEnum.تم_الإلغاء };

                case OrderStatusEnum.تم_المعالجة:
                    return new List<OrderStatusEnum> { OrderStatusEnum.قيد_التوصيل };

                case OrderStatusEnum.الطلبات_الغير_مكتملة:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_الغير_مكتملة_1 };

                case OrderStatusEnum.الطلبات_الغير_مكتملة_1:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_الغير_مكتملة_2 };

                case OrderStatusEnum.الطلبات_الغير_مكتملة_2:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_الغير_مكتملة_3 };

                case OrderStatusEnum.الطلبات_الغير_مكتملة_3:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_الغير_مكتملة_4 };

                case OrderStatusEnum.الطلبات_الغير_مكتملة_4:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_الغير_مكتملة_5 };

                case OrderStatusEnum.الطلبات_الغير_مكتملة_5:
                    return new List<OrderStatusEnum> { OrderStatusEnum.الطلبات_الغير_مكتملة_6 };

                case OrderStatusEnum.الطلبات_الغير_مكتملة_6:
                    return new List<OrderStatusEnum> { OrderStatusEnum.طلب_جديد };

                default:
                    return new List<OrderStatusEnum>(); // Empty list means no valid next statuses
            }
        }

        public static Dictionary<OrderStatusEnum, string> StatusColorMapping = new Dictionary<OrderStatusEnum, string>
                {
                { OrderStatusEnum.طلب_جديد, "color: #007bff;" }, // Blue
                { OrderStatusEnum.تم_التجهيز, "color: #007bff" }, // Blue
                { OrderStatusEnum.قيد_التوصيل, "color: #ffc107;" }, // Yellow
                { OrderStatusEnum.تم_التسليم, "color: #2ab149;" }, // green
                { OrderStatusEnum.فشل_التسليم, "color: #FF0000;" }, // Red
                { OrderStatusEnum.فشل_التسليم_1, "color: #FF0000;" }, // Red
                { OrderStatusEnum.تم_المعالجة, "color: #45bbea;" }, // Yellow
                { OrderStatusEnum.أرشيف_المرجع, "color: #FF0000;" }, // Green  
                { OrderStatusEnum.تم_التسليم_المؤقت, "color: #2ab149;" }, // Green  
                { OrderStatusEnum.انتظار_المعالجة, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_المرجعة, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_المعلقة, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_الغير_معرفة, "color: #FF0000;" }, // Red
                                { OrderStatusEnum.الطلبات_الغير_مكتملة, "color: #FF0000;" }, // Red
                                { OrderStatusEnum.الطلبات_الغير_مكتملة_1, "color: #FF0000;" }, // Red
                                { OrderStatusEnum.تم_الإلغاء, "color: #FF0000;" }, // Red

                { OrderStatusEnum.الطلبات_المؤجلة, "color: #ffc107;" }, // Red
                { OrderStatusEnum.أخطاء_الشركات_والمندوبين, "color: #FF0000;" }, // Red
                { OrderStatusEnum.تم_تحديث_الرصيد, "color: #2ab149;" }, // Green
                { OrderStatusEnum.تم_الدفع, "color: #2ab149;" }, // Green
                { OrderStatusEnum.فشل_التسليم_2, "color: #FF0000;" }, // Red
                { OrderStatusEnum.فشل_التسليم_3, "color: #FF0000;" }, // Red
                { OrderStatusEnum.فشل_التسليم_4, "color: #FF0000;" }, // Red
                { OrderStatusEnum.فشل_التسليم_5, "color: #FF0000;" }, // Red
                { OrderStatusEnum.فشل_التسليم_6, "color: #FF0000;" }, // Red
                { OrderStatusEnum.فشل_التسليم_7, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_الغير_مكتملة_2, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_الغير_مكتملة_3, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_الغير_مكتملة_4, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_الغير_مكتملة_5, "color: #FF0000;" }, // Red
                { OrderStatusEnum.الطلبات_الغير_مكتملة_6, "color: #FF0000;" }, // Red
                };

        public static List<OrderStatusEnum> OrderedStatuses = new List<OrderStatusEnum>
                {
                OrderStatusEnum.طلب_جديد,
                OrderStatusEnum.تم_التجهيز,
                OrderStatusEnum.قيد_التوصيل,
                OrderStatusEnum.تم_التسليم,
                OrderStatusEnum.فشل_التسليم,
                OrderStatusEnum.فشل_التسليم_1,
                OrderStatusEnum.فشل_التسليم_2,
                OrderStatusEnum.فشل_التسليم_3,
                OrderStatusEnum.فشل_التسليم_4,
                OrderStatusEnum.فشل_التسليم_5,
                OrderStatusEnum.فشل_التسليم_6,
                OrderStatusEnum.فشل_التسليم_7,
                OrderStatusEnum.انتظار_المعالجة,
                OrderStatusEnum.الطلبات_المعلقة,
                OrderStatusEnum.الطلبات_المرجعة,
                OrderStatusEnum.أخطاء_الشركات_والمندوبين,
                OrderStatusEnum.أرشيف_المرجع,
                OrderStatusEnum.الطلبات_الغير_معرفة,
                OrderStatusEnum.الطلبات_الغير_مكتملة,
                OrderStatusEnum.الطلبات_الغير_مكتملة_1,
                OrderStatusEnum.الطلبات_الغير_مكتملة_2,
                OrderStatusEnum.الطلبات_الغير_مكتملة_3,
                OrderStatusEnum.الطلبات_الغير_مكتملة_4,
                OrderStatusEnum.الطلبات_الغير_مكتملة_5,
                OrderStatusEnum.الطلبات_الغير_مكتملة_6,

                OrderStatusEnum.تم_التسليم_المؤقت,

                OrderStatusEnum.تم_تحديث_الرصيد,
        };
        public static List<OrderStatusesForDeliveryCompanyAndRepresentativeEnum> OrderedStatusessForDeliveryCompanyAndRepresentative = new List<OrderStatusesForDeliveryCompanyAndRepresentativeEnum>
                {
                OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_التجهيز,
                OrderStatusesForDeliveryCompanyAndRepresentativeEnum.قيد_التوصيل,
                OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_التسليم,
                OrderStatusesForDeliveryCompanyAndRepresentativeEnum.تم_تحديث_الرصيد,
        };

        // Find the index of the current status
        public static int GetCurrentStatusIndex(OrderStatusEnum currentStatus)
        {
            return OrderedStatuses.IndexOf(currentStatus);
        }
        public static int GetCurrentStatusIndexV2(OrderStatusesForDeliveryCompanyAndRepresentativeEnum currentStatus)
        {
            return OrderedStatusessForDeliveryCompanyAndRepresentative.IndexOf(currentStatus);
        }

        public static Dictionary<string, string> StatusColorMappingString = new Dictionary<string, string>
            {
                { "طلب_جديد", "color: #007bff;" }, // Blue
               { "انتظار_التجهيز", "color: #007bff;" }, // Blue

                { "تم_التجهيز", "color: #007bff;" }, // Blue (assuming the same color as "انتظار_التجهيز")
                { "قيد_التوصيل", "color: #ffc107;" }, // Yellow
                { "تم_التسليم", "color: #2ab149;" }, // Green
                { "فشل_التسليم", "color: #FF0000;" }, // Red
                { "مؤجلات", "color: #ffc107;" }, // Yellow
                { "انتظار_المعالجة", "color: #FF0000;" }, // Red
                { "تم_الإلغاء", "color: #FF0000;" }, // Red
                { "الطلبات_المرجعة", "color: #FF0000;" }, // Red
                 { "تم_المعالجة", "color: #ffc107;" }, // Red
                { "اخطاء_مندوبين_ومتابعة", "color: #FF0000;" }, // Red
                { "تم_تحديث_الرصيد", "color: #2ab149;" }, // Green
                { "الطلبات_المؤجلة", "color: #ffc107;" }, // Green
               { "أرشيف_المرجع", "color: #ffc107;" }, // Green

                { "تم_الدفع", "color: #2ab149;" }, // Green

            };


        public static Dictionary<OrderStatusEnum, string> StatusPhraseMapping = new Dictionary<OrderStatusEnum, string>
            {
                { OrderStatusEnum.طلب_جديد, "الطلب قيد الانتظار للتجهيز" },
                { OrderStatusEnum.تم_التجهيز, "تم الأنتهاء من تجهيز الطلب" },
                { OrderStatusEnum.قيد_التوصيل, "جاري العمل على توصيل الطلب" },
                { OrderStatusEnum.تم_التسليم, "تم توصيل الطلب بنجاح" },
                { OrderStatusEnum.فشل_التسليم, "جرت محاولة فاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_1, "المحاولة الأولى الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_2, "المحاولة الثانية الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_3, "المحاولة الثالثة الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_4, "المحاولة الرابعة الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_5, "المحاولة الخامسة الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_6, "المحاولة السادسة الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.فشل_التسليم_7, "المحاولة السابعة والأخيرة الفاشلة لتسليم الطلب" },
                { OrderStatusEnum.تم_المعالجة, "تم معالحة الطلب بنجاح" },
                { OrderStatusEnum.انتظار_المعالجة, "في النتظار الرد من العميل " },
                { OrderStatusEnum.الطلبات_المرجعة, "تم إعادة الطلب الي المستودعات" },
                { OrderStatusEnum.أخطاء_الشركات_والمندوبين, "هناك خطأ من قبل المندوب" },
                { OrderStatusEnum.تم_تحديث_الرصيد, "تم إيداع مبلغ الطلب في الحساب الجاري " },
                { OrderStatusEnum.أرشيف_المرجع, " الطلب في حالة أرشيف المرج" },
                 { OrderStatusEnum.الطلبات_المؤجلة, "  الطلب مؤجل" },
                  { OrderStatusEnum.تم_التسليم_المؤقت, "تم تسليم الطلب مؤقتا " },
                                    { OrderStatusEnum.الطلبات_الغير_معرفة, "طلب غير معرف " },
                { OrderStatusEnum.الطلبات_الغير_مكتملة, "طلب غير مكتمل " },
                { OrderStatusEnum.الطلبات_الغير_مكتملة_1, "المحاولة الأولى للطلب الغير مكتمل" },
                { OrderStatusEnum.الطلبات_الغير_مكتملة_2, "المحاولة الثانية للطلب الغير مكتمل" },
                { OrderStatusEnum.الطلبات_الغير_مكتملة_3, "المحاولة الثالثة للطلب الغير مكتمل" },
                { OrderStatusEnum.الطلبات_الغير_مكتملة_4, "المحاولة الرابعة للطلب الغير مكتمل" },
                { OrderStatusEnum.الطلبات_الغير_مكتملة_5, "المحاولة الخامسة للطلب الغير مكتمل" },
                { OrderStatusEnum.الطلبات_الغير_مكتملة_6, "المحاولة السادسة والأخيرة للطلب الغير مكتمل" },

                { OrderStatusEnum.تم_الدفع, "تم دفع مبلغ الطلب في  " },

            };

        // Method to get only the status phrase
        public static string GetOrderStatusPhrase(OrderStatusEnum orderStatus)
        {
            return StatusPhraseMapping.TryGetValue(orderStatus, out var phrase) ? phrase : "Unknown Status";
        }


        public static Dictionary<string, string> StatusPhraseMappingString = new Dictionary<string, string>
        {
            { "طلب_جديد", "الطلب قيد الانتظار للتجهيز" },
            { "تم_التجهيز", "تم الأنتهاء من تجهيز الطلب" },
            { "قيد_التوصيل", "جاري العمل على توصيل الطلب" },
            { "تم_التسليم", "تم توصيل الطلب بنجاح" },
            { "فشل_التسليم", "جرت محاولة فاشلة لتسليم الطلب" },
            { "انتظار_المعالجة", "في النتظار الرد من العميل" },
            { "تم_المعالجة", "تم معالحة الطلب بنجاح" },
            {"أرشيف_المرجع", "الطلب في حالة أرشيف المرجع" },
            {"مؤجلات", "تم تأجيل توصيل الطلب" },
            { "الطلبات_المرجعة", "تم إعادة الطلب الى المستودعات" },
            { "اخطاء_مندوبين_ومتابعة", "هناك خطأ من قبل المندوب" },
            { "تم_تحديث_الرصيد", "تم إيداع مبلغ الطلب في الحساب الجاري" },
            { "الطلبات_المؤجلة", "الطلب مرجل" },
            { "تم_الدفع", " تم_الدفع" },

        };
        // Method to get only the status phrase
        public static string GetOrderStatusPhraseStrong(string orderStatus)
        {
            return StatusPhraseMappingString.TryGetValue(orderStatus, out var phrase) ? phrase : "Unknown Status";
        }
    }
}
