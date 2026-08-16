using lotus_blue.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Eventing.Reader;
using Z.Expressions.Compiler;
namespace lotus_blue.Models.ViewModel
{
    public class OrderViewModel : IValidatableObject
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "البلد")]
        public Common.Countries Country { get; set; }

        public int CountryId { get; set; }

        [Required]
        [Display(Name = "الولاية")]
        [StringLength(255)]
        public string State { get; set; }

        [Required]
        [Display(Name = "مصدر الطلب")]
        public OrderSourceEnum OrderSource { get; set; }

        [Required]
        [Display(Name = "اسم المصدر")]
        [StringLength(255)]
        public string SourceName { get; set; }

        [Required]
        [Display(Name = "شركة التصنيع")]
        public int? ManufacturingCompanyId { get; set; }

        public int? StoreId { get; set; }

        [Required]
        [Display(Name = "شركة التوصيل")]
        public int DeliveryCompanyId { get; set; }

        [Required]
        [Display(Name = "رقم الهاتف")]
        [StringLength(255)]
        public string TelephoneNumber { get; set; }

        [Display(Name = "رقم الهاتف الثاني")]
        [StringLength(255)]
        public string? SecondTelephoneNumber { get; set; }

        [Required]
        [Display(Name = "اسم العميل")]
        [StringLength(255)]
        public string CustomerName { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(255)]
        public string? Notes { get; set; }

        [Required]
        [Display(Name = "العنوان")]
        [StringLength(255)]
        public string Address { get; set; }


        [Required]
        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; }

        [Required]
        [Display(Name = "أخر تعديل")]
        public DateTime? LastEditedDate { get; set; }

        [Required]

        [Display(Name = "حالة الطلب")]
        public OrderStatusEnum OrderStatus { get; set; }

        [Required]
        [Display(Name = "المبلغ الإجمالي")]
        [DisplayFormat(DataFormatString = "{0:0.00}")]
        public decimal TotalPrice { get; set; }

        // warehouse for order
        [Required(ErrorMessage = "المستودع مطلوب")]
        [Display(Name = "مستودع")]
        public List<WarehouseAmountViewModel> SelectedWarehouses { get; set; }


        [Required]
        [Display(Name = "المستخدم")]
        public string ApplicationUserId { get; set; }


        [Required(ErrorMessage = "المستودع مطلوب")]
        [Display(Name = "مستودع")]
        public Dictionary<int, int> SelectedWarehouseIds { get; set; }


        public SelectList WarehouseList { get; set; }

        public DateTime? FixedOrderDate { get; set; }

        public ManufacturingCompanyViewModel ManufacturingCompany { get; set; }

        public DeliveryCompanyViewModel DeliveryCompany { get; set; }

        // choose more than 1 product


        // in order main page 
        [DisplayFormat(DataFormatString = "{0:0.00}")]
        public decimal DeliveryCost { get; set; } // Change data type to decimal

        // الادخالات

        public int Entries { get; set; }


        // order history 

        public List<OrderStatusHistoryModel> OrderStatusHistories { get; set; }

        public string LoggedInUserId { get; set; } // Add this property


        // order history reaosn فشل التسليم 

        public string CancelReasonForDeliveryFailure { get; set; }

        // order id from sbs 
        public int? ExternalOrderId { get; set; }


        //اخر معدل
        public string? LastEditedBy { get; set; }
        public string? LastEditedByImage { get; set; }


        //qr code 
        public string QRCodeImageBase64 { get; set; }

        // redirect link 
        public string QRCodeUrl { get; set; } // Add this line if you want to use the QR code as a link

        // employe name 
        public string EmployeeName { get; set; }

        public List<EmployeNameAndId> EmployeNameAndId { get; set; }

        public string ManufacturingCompanyName { get; set; }

        public string ManufacturingCompanylogo { get; set; }


        // 
        public string ExternalShipmentCode { get; set; }

        public bool FromComments { get; set; }


        public string EmployeeImage { get; set; }

        public string? lastEditedByImage { get; set; }

        public bool Gender { get; set; }

        public bool IsHidden { get; set; }

        public bool IsProcessed { get; set; }

        public bool IsPaid { get; set; }

        // fixed by name 
        public string? FixedbyEmployee { get; set; }
        public string? FixedbyEmployeeImage { get; set; }


        // fromcomments name 
        public string? FromCommentsEmployee { get; set; }
        public string? FromCommentsEmployeeImage { get; set; }


        public GetDataListViewModel Employee { get; set; }

        public bool IsDiscount { get; set; }

        public bool Employeebouns { get; set; }

        public int TotalAmountOfOrderWarehouses { get; set; }

        public bool IsFixedBefore { get; set; }

        public bool IsClientSpecial { get; set; }

        public bool HasWarehouseWithMoreThanOneItem { get; set; }
        public bool HasMoreThanOneWarehouse { get; set; }
        public decimal TotalProductsCount { get; set; }
        public bool IsComplaints { get; set; }


        /// life time 
        /// 
        public string? CountryImageUrl { get; set; } // Image URL for the country

        public string? OrderSourceImageUrl { get; set; } // Image URL for the order source

        public string? OrderStatusImageUrl { get; set; } // Image URL for the order status

        public string Currency { get; set; } // New property for the currency symbol

        public string CountryString { get; set; } // This will hold the string representation of the country enum
        public string OrderSourceString { get; set; }
        public string OrderStatusString { get; set; }


        public bool IsBonus { get; set; }

        public decimal DeliveryPrice { get; set; }

        public int FixedOrderTimes { get; set; }  // Count of تم_المعالجة
        public int FailedOrderTimes { get; set; } // Count of فشل_التسليم

        public string chatUrl { get; set; }

        public int? CampaignId { get; set; }

        // Pin state for Home/Index order pinning.
        public bool IsPinned { get; set; }
        public DateTime? PinnedAt { get; set; }
        public string? PinnedByUserId { get; set; }

        // Sales indicator shown on Home/Index after order creation.
        // Calculated from: (TotalPrice - DeliveryPrice) / total sold product quantity.
        public string? SalesIndicatorState { get; set; }
        public string? SalesIndicatorText { get; set; }
        public decimal? SalesIndicatorNetSellingPrice { get; set; }
        public decimal? SalesIndicatorAverageSellingPrice { get; set; }

        // How long it took the agent to fill the create-order form (seconds).
        // Captured from the floating timer at submit time and persisted on the Order.
        public int? CreationDurationSeconds { get; set; }

        public IFormFile? PhotoFile { get; set; }
        public string? PhotoUrl { get; set; }
        public string? ExistingPhotoUrl { get; set; }
        public IFormFile? PaymentReceiptFile { get; set; }
        public string? PaymentReceiptUrl { get; set; }
        public string? ExistingPaymentReceiptUrl { get; set; }
        public string? CampaignImageUrl { get; set; }

        public string FailureReasonDisplay { get; set; }

        // Populated only for Edit/Resend views — Create leaves these null and fetches via DataList AJAX endpoints.
        public List<StoreOptionVm>? AvailableStores { get; set; }
        public List<DeliveryCompanyOptionVm>? AvailableDeliveryParties { get; set; }
        public List<CampaignOptionVm>? AvailableCampaigns { get; set; }
        public List<WarehouseOptionVm>? AvailableWarehouses { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Chat URL is required for every source except WhatsApp, which doesn't carry one.
            if (OrderSource != OrderSourceEnum.واتساب && string.IsNullOrWhiteSpace(chatUrl))
            {
                yield return new ValidationResult(
                    "حقل رابط المحادثة مطلوب.",
                    new[] { nameof(chatUrl) });
            }

            // Turkey requires a full customer name (given + family name) for delivery paperwork,
            // and only Latin + Turkish letters are accepted.
            if (Country == Common.Countries.تركيا && !string.IsNullOrWhiteSpace(CustomerName))
            {
                var trimmed = CustomerName.Trim();
                var parts = trimmed.Split(
                    new[] { ' ', '\t' },
                    System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    yield return new ValidationResult(
                        "يرجى إدخال الاسم الكامل (الاسم واللقب) لطلبات تركيا.",
                        new[] { nameof(CustomerName) });
                }
                else if (!System.Text.RegularExpressions.Regex.IsMatch(
                    trimmed,
                    @"^[A-Za-zÇĞİIÖŞÜçğıiöşü\s]+$"))
                {
                    yield return new ValidationResult(
                        "يُسمح فقط بالأحرف اللاتينية والتركية في اسم العميل لطلبات تركيا.",
                        new[] { nameof(CustomerName) });
                }
            }
        }
    }

}
public class WarehouseAmountViewModel
{
    [Required]
    public int WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    [Required]
    public int Amount { get; set; }

    public int? RemainingAmount { get; set; }

    public string? Image { get; set; }

    public int? MainWarehouseId { get; set; }

}
public class OrderStatusHistoryModel
{
    public int Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public OrderStatusEnum? Status { get; set; }
    public string StatusApi { get; set; }  // Now as a string
    public string UserId { get; set; }
    public string? Reason { get; set; }
    public string? UserName { get; set; } // Name of the user who made the change
    public bool IsHidden { get; set; }
    public string? FailureReasonImageUrl { get; set; }
    public int OrderId { get; set; }

}


public class EmployeNameAndId
{
    public string Id { get; set; }
    public string Name { get; set; }
    // Other properties of Employee
}

public class StoreOptionVm
{
    public int id { get; set; }
    public string name { get; set; }
    public string logoUrl { get; set; }
    public int? mainWarehouseId { get; set; }
}

public class DeliveryCompanyOptionVm
{
    public int id { get; set; }
    public string name { get; set; }
    public string logoUrl { get; set; }
    public bool isRepresentative { get; set; }
}

public class CampaignOptionVm
{
    public int id { get; set; }
    public string name { get; set; }
    public string imageUrl { get; set; }
    public int? manufacturingCompanyId { get; set; }
}

public class WarehouseOptionVm
{
    public int id { get; set; }
    public string name { get; set; }
    public int amount { get; set; }
    public string productImage { get; set; }
    public int? mainWarehouseId { get; set; }
}

