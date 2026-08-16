using lotus_blue.Data;
using lotus_blue.Models;
using lotus_blue.Models.ViewModel;
using lotus_blue.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Security.Claims;

namespace lotus_blue.Controllers
{
    public class ProductShipmentInvoiceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GetCurrentTimeInIstanbul _timeService;
        private readonly PdfReportGeneratorShipmentInvoice _pdfReportGeneratorShipmentInvoice;

        public ProductShipmentInvoiceController(ApplicationDbContext context, GetCurrentTimeInIstanbul timeService, PdfReportGeneratorShipmentInvoice pdfReportGeneratorShipmentInvoice)
        {
            _context = context;
            _timeService = timeService;
            _pdfReportGeneratorShipmentInvoice = pdfReportGeneratorShipmentInvoice;
        }



        [HttpGet]
        public IActionResult CreatePriceOffer()
        {
            var model = new PriceOfferViewModel();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePriceOffer(PriceOfferViewModel model)
        {
            model.TotalPriceOfAllProducts = model.Products.Sum(p => p.TotalPrice);
            model.CreatedDate = DateTime.Now;
            Random random = new Random();
            model.InvoiceId = random.Next(1000, 10000);

            // Format the total amount as currency
            string formattedTotalAmount = model.TotalPriceOfAllProducts.ToString("N2");

            var pdfBytes = await _pdfReportGeneratorShipmentInvoice.CreatePdfReportAsync(
                model.DeliveryCompanyName,
                model.DeliveryCompanyAddress,
                model.DeliveryCompanyPhoneNumber,
                model.DeliveryCompanyEmail,
                model.CreatedDate.ToString("yyyy-MM-dd"),
                model.InvoiceId.ToString(),
                formattedTotalAmount,  // Use formatted currency here
                model.Products.Select(p => new WarehouseDetail
                {
                    WarehouseName = p.Name,
                    WarehousePrice = p.Price,
                    Quantity = p.Amount
                }).ToList()
            );

            Response.Headers.Add("Content-Disposition", "inline; filename=OrdersReport.pdf");
            return File(pdfBytes, "application/pdf");
        }




    }
}
