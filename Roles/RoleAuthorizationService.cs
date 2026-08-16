using lotus_blue.Models;
using lotus_blue.OrderStatus;

namespace lotus_blue.Roles
{
    public class RoleAuthorizationService
    {
        private static readonly Dictionary<OrderStatusEnum, List<string>> statusRolesMap = new Dictionary<OrderStatusEnum, List<string>>
    {
        { OrderStatusEnum.تم_التجهيز, new List<string> { "Admin", "DeliveryCompany", "DeliveryRepresentative", "ExecutiveDirector", "OrderPreparer", "WareHouse", "FollowUpDepartment" } },
        { OrderStatusEnum.قيد_التوصيل, new List<string> { "Admin", "DeliveryCompany", "DeliveryRepresentative", "WareHouse", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.تم_التسليم, new List<string> { "Admin", "DeliveryCompany", "DeliveryRepresentative", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.فشل_التسليم, new List<string> { "Admin", "DeliveryCompany", "DeliveryRepresentative", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.تم_المعالجة, new List<string> { "FollowUpDepartment", "CallCenter", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.أرشيف_المرجع, new List<string> { "FollowUpDepartment", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.الطلبات_المؤجلة, new List<string> { "CallCenter", "FollowUpDepartment", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.تم_الإلغاء, new List<string> { "CallCenter", "FollowUpDepartment", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.انتظار_المعالجة, new List<string> { "FollowUpDepartment", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.تم_التسليم_المؤقت, new List<string> { "CallCenter", "FollowUpDepartment" } },
        { OrderStatusEnum.الطلبات_المعلقة, new List<string> { "FollowUpDepartment", "Admin", "ExecutiveDirector" } },

        { OrderStatusEnum.الطلبات_المرجعة, new List<string> { "FollowUpDepartment", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.أخطاء_الشركات_والمندوبين, new List<string> { "FollowUpDepartment", "Admin", "ExecutiveDirector" } },
        { OrderStatusEnum.تم_تحديث_الرصيد, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.تم_الدفع, new List<string> { "Admin" } },
        { OrderStatusEnum.فشل_التسليم_2, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.فشل_التسليم_3, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.فشل_التسليم_4, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.فشل_التسليم_5, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.فشل_التسليم_6, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
        { OrderStatusEnum.فشل_التسليم_7, new List<string> { "Admin", "FollowUpDepartment", "ExecutiveDirector" } },
    };

        private static readonly HashSet<OrderStatusEnum> DeliveryAllowedTargetStatuses = new HashSet<OrderStatusEnum>
        {
            OrderStatusEnum.تم_التجهيز,
            OrderStatusEnum.قيد_التوصيل,
            OrderStatusEnum.تم_التسليم,
            OrderStatusEnum.فشل_التسليم,
        };

        public bool CanUpdateStatus(string roleName, OrderStatusEnum orderStatus)
        {
            return statusRolesMap.ContainsKey(orderStatus) && statusRolesMap[orderStatus].Contains(roleName);
        }

        /// <summary>
        /// Validates whether DeliveryCompany/DeliveryRepresentative can change an order from
        /// currentStatus to targetStatus, applying business rules:
        /// - Allowed target statuses: تم_التجهيز, قيد_التوصيل, تم_التسليم, فشل_التسليم
        /// - From any failure status (فشل_التسليم 1–7) they may also set تم_التسليم
        /// - Once تم_التسليم is reached, no changes allowed
        /// Returns null if allowed, or an Arabic error message if denied.
        /// </summary>
        public string? ValidateDeliveryRoleStatusChange(OrderStatusEnum currentStatus, OrderStatusEnum targetStatus)
        {
            if (currentStatus == OrderStatusEnum.تم_التسليم)
                return "لا يمكن تغيير حالة الطلب بعد وصوله إلى تم التسليم.";

            if (OrderStatusHelper.IsFailureStatus(currentStatus) && targetStatus == OrderStatusEnum.تم_التسليم)
                return null;

            if (!DeliveryAllowedTargetStatuses.Contains(targetStatus))
                return "غير مسموح لك بالتغيير إلى هذه الحالة.";

            return null;
        }
    }
}
