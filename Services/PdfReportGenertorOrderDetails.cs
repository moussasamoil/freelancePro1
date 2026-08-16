using DinkToPdf;
using DinkToPdf.Contracts;
using lotus_blue.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PdfiumViewer;
using Microsoft.AspNetCore.Hosting;
using lotus_blue.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

public class PdfReportGenertorOrderDetails
{
    private readonly IConverter _pdfConverter;
    private readonly IWebHostEnvironment _environment;
    private readonly DeliveryCompanyService _deliveryCompanyService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PdfReportGenertorOrderDetails(IConverter pdfConverter, IWebHostEnvironment environment, DeliveryCompanyService deliveryCompanyService, IHttpContextAccessor httpContextAccessor)
    {
        _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
        _environment = environment;
        _deliveryCompanyService = deliveryCompanyService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<byte[]> CreatePdfReportAsync(
     Order order,
     string storeName,
     string customerAddress,
     string customerPhoneNumber,
     string createdDate,
     string reportId,
     string totalAmount,
     string qrCodeUrl,
     List<string> warehouseDetails)
    {
        var htmlPages = await GenerateHtmlReportAsync(
            order, storeName, customerAddress, customerPhoneNumber,
            createdDate, reportId, totalAmount, qrCodeUrl, warehouseDetails);

        return await ConvertHtmlPagesToPdfAsync(htmlPages);
    }





    public async Task<List<string>> GenerateHtmlReportAsync(
      Order order,
      string storeName,
      string customerAddress,
      string customerPhoneNumber,
      string createdDate,
      string reportId,
      string totalAmount,
      string qrCodeUrl,
      List<string> warehouseDetails)
    {
        var pages = new List<string>();
        var path = Path.Combine(_environment.WebRootPath, "static", "MoonLight.png.png");

        var stringimage = "http://localhost:7079/static/MoonLight.png";
        var htmlContent = new StringBuilder();

        // HTML start with CSS styles
        htmlContent.Append("<html dir=\"rtl\"><head><meta charset=\"UTF-8\"><style>");
        htmlContent.Append("@@font-face { font-family: 'Amiri'; src: url('fonts/Amiri-Regular.ttf'); }");
        htmlContent.Append("body { font-family: 'Amiri', sans-serif; font-size: 18px !important; margin-top: 20px !important; }");
        // Add more CSS styles as needed

        htmlContent.Append("</style></head><body>");

        //// HTML content
        //htmlContent.Append($"<h1>{storeName}</h1>");
        //htmlContent.Append($"<p>Address: {customerAddress}</p>");
        //htmlContent.Append($"<p>Phone: {customerPhoneNumber}</p>");
        //htmlContent.Append($"<p>Created Date: {createdDate}</p>");
        //htmlContent.Append($"<p>Report ID: {reportId}</p>");
        //htmlContent.Append($"<p>Total Amount: {totalAmount}</p>");
        //htmlContent.Append($"<img src='{stringimage}' alt='QR Code'/>");
        //htmlContent.Append($"<img src='{path}' alt='QR Code'/>");

        //htmlContent.Append($"<p>Created Date: {path}</p>");


        foreach (var detail in warehouseDetails)
        {
            htmlContent.Append($"<p>{detail}</p>");
        }

        htmlContent.Append("</body></html>");

        pages.Add(htmlContent.ToString());

        return pages;
    }



    private async Task<byte[]> ConvertHtmlPagesToPdfAsync(List<string> htmlPages)
    {
        // Example: Assuming PechkinPaperSize expects dimensions in the format "210mm" and "297mm"
        var a4PaperSize = new PechkinPaperSize("210mm", "297mm"); // Width and height for A4 paper size as strings

        var doc = new HtmlToPdfDocument()
        {
            GlobalSettings = {
            ColorMode = ColorMode.Color,
            PaperSize = a4PaperSize, // Set the custom A4 paper size
             Margins = new MarginSettings { Top = 10,  Left = 10, Right = 10 }


        }
        };

        foreach (var htmlPage in htmlPages)
        {
            doc.Objects.Add(new ObjectSettings
            {
                HtmlContent = htmlPage,
            });
        }

        return _pdfConverter.Convert(doc);
    }

    public async Task SendPdfForPrinting(IEnumerable<Order> orders, List<string> headers, List<Func<Order, string>> valueSelectors)
    {
        // Path to the existing PDF file in wwwroot
        string pdfFilePath = Path.Combine(_environment.WebRootPath, "CustomPdf", "FaturaFinancial.pdf");

        if (!File.Exists(pdfFilePath))
        {
            // Handle the case where the PDF file is not found
            Console.WriteLine("Custom PDF file not found: " + pdfFilePath);
            Console.WriteLine("Custom PDF file not found: " + pdfFilePath);
            Console.WriteLine("Custom PDF file not found: " + pdfFilePath);
            Console.WriteLine("Custom PDF file not found: " + pdfFilePath);
            Console.WriteLine("Custom PDF file not found: " + pdfFilePath);
            Console.WriteLine("HII");
            Console.WriteLine("HII");
            Console.WriteLine("HII");
            Console.WriteLine("HII");
            Console.WriteLine("HII");
            Console.WriteLine("HII");
            Console.WriteLine("HII");
            Console.WriteLine("HII");

            return; // You can also throw an exception or handle it differently
        }

        using (var document = PdfiumViewer.PdfDocument.Load(pdfFilePath))
        {
            using (var printDocument = new PrintDocument())
            {
                printDocument.PrintPage += (sender, e) =>
                {
                    try
                    {
                        var pageNumber = e.PageSettings.PrinterSettings.FromPage;
                        // Increase resolution for better quality
                        using (var image = document.Render(pageNumber - 1, 300, 300, PdfRenderFlags.Annotations))
                        {
                            // Adjust scaling to fit the page
                            var rect = new System.Drawing.Rectangle(0, 0, e.PageBounds.Width, e.PageBounds.Height);
                            e.Graphics.DrawImage(image, rect);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception or handle it as needed
                        Console.WriteLine("Error during PDF rendering: " + ex.Message);
                    }
                };

                printDocument.PrinterSettings.FromPage = 1;
                printDocument.PrinterSettings.ToPage = document.PageCount;
                printDocument.PrinterSettings.PrintRange = PrintRange.SomePages;
                printDocument.Print();
            }
        }
    }
}
