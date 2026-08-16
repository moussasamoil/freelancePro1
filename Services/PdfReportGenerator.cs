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

public class PdfReportGenerator
{
    private readonly IConverter _pdfConverter;
    private readonly IWebHostEnvironment _environment;
    private readonly DeliveryCompanyService _deliveryCompanyService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PdfReportGenerator(IConverter pdfConverter, IWebHostEnvironment environment, DeliveryCompanyService deliveryCompanyService, IHttpContextAccessor httpContextAccessor)
    {
        _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
        _environment = environment;
        _deliveryCompanyService = deliveryCompanyService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<byte[]> CreatePdfReportAsync(
       IEnumerable<Order> orders,
       List<string> headers,
       List<Func<Order, string>> valueSelectors,
       string storeName,
       string storeAddress,
       string storePhoneNumber,
       string createdDate,
       string reportId,
       string totalAmount,
       string deliveryAmount,
       string remaningAmount,
       string totalOrderNumber,
       string countryName)
    {
        var htmlPages = await GenerateHtmlReportAsync(
            orders, headers, valueSelectors, storeName, storeAddress, storePhoneNumber,
            createdDate, reportId, totalAmount, deliveryAmount, remaningAmount, totalOrderNumber , countryName);

        return await ConvertHtmlPagesToPdfAsync(htmlPages);
    }





    public async Task<List<string>> GenerateHtmlReportAsync(
    IEnumerable<Order> orders,
    List<string> headers,
    List<Func<Order, string>> valueSelectors,

    string storeName,
    string storeAddress,
    string storePhoneNumber,
    string createdDate,
    string reportId,
    string totalAmount,
    string deliveryAmount,
    string RemaningAmount,
    string totalOrderNumber,
    string countryName)
    {
        var webRootPath = _environment.WebRootPath;
        var imageFilePath = Path.Combine(webRootPath, "static", "LuxiraRounded.jpg");
        var imageAsBase64 = Convert.ToBase64String(File.ReadAllBytes(imageFilePath));
        var embeddedImageHtml = $"<img style=\"padding-bottom:0px\" src=\"data:image/jpeg;base64,{imageAsBase64}\" width=\"55\" height=\"55\" />";


        var pages = new List<string>();
        var ordersPerPage =10;
        var ordersCount = orders.Count();
        var totalPages = Math.Ceiling((double)ordersCount / ordersPerPage);
        var status = _httpContextAccessor.HttpContext.Request.Query["status"].ToString();

        var orderIds = orders.Select(o => o.Id).ToList();
        var deliveryCompanyPrices = orders.Select(o => new { o.Id, o.DeliveryPrice }).ToDictionary(o => o.Id, o => o.DeliveryPrice);
        var firstOrder = orders.FirstOrDefault();
        var storename = firstOrder.ManufacturingCompany?.Name ?? "شركة luxira";
        var storephonenumber = firstOrder?.ManufacturingCompany?.PhoneNumber ?? "+90 552 652 44 92";
        for (int page = 0; page < totalPages; page++)
        {
            var startIndex = page * ordersPerPage;
            var endIndex = Math.Min(startIndex + ordersPerPage, ordersCount);

            // Example HTML content to be added on top (header)
            // Example HTML content to be added on top (header) with increased text size
            var headerHtml =
     "<div style=\"clear: both; margin-bottom: 20px; margin-right: px; margin-left: 20px; text-align: center;\">" +

            "<div style=\"float: left; width: 8%; margin-top:20px; box-sizing: border-box; margin-left:7px; text-align: end;\">" +

                                        $"{embeddedImageHtml}" +


            "</div>" +

                "<div style=\"float: left; width: 20%; margin-top:16px;   box-sizing: border-box;  text-align: start; \">" +
            "<p style=\"text-align: center; font-weight:;  margin:4px 8px;\">MAKEUP & COSMETICS</p>" +
            "<p style=\"text-align: center; font-weight:;  margin:4px 8px;\">MERKEZ MAH. MENEKŞE SK.</p>" +
            "<p style=\"text-align: center; font-weight:;  margin:4px 8px;\">NO: 4 A AVCILAR/</p>" +
            "<p style=\"text-align: center;  font-weight:; margin:4px 8px;\">İSTANBUL - TURKEY</p>" +

            "</div>" +

                "<div style=\"float: left; background-color:f0fffc; margin-top: 16px; width: 30%;  margin-left: 20px;  box-sizing: border-box; border: 1.5px solid #1d273b; border-radius: 8px;\">" +
                "<p style=\" margin:4px 8px; font-weight:; \">W E B: <a href=\"https://luxiraholdings.com\">https://luxiraholdings.com</a></p>" +
                "<p style=\" margin:4px 8px; font-weight:;\" >Mail:luxiraholding@gmail.com</p>" +
                "<p style=\" margin:4px 8px; font-weight:;\">T E L: +905312855286</p>" +
                "<p style=\" margin:4px 8px; font-weight:;\">instagram: Lotusbluecosmetics</p>" +

                "</div>" +

                "<div style=\"float: left; width: 30%; margin-top:16px; box-sizing: border-box;\">" +
                    "<p  style=\"margin:4px 8px 4px 8px; font-weight:;\">Bank : KUVEYT TURK</p>" +
                    "<p  style=\"margin:6px 8px 6px 8px;font-weight:;\">IBAN : TR790020500009545077500001</p>" +
                    "<p  style=\"margin:4px 8px 4px 8px;font-weight:;\">NAME : AHMED F T SALEH</p>" +
                    "</div>"+
                    
          "</div>" +

                                                                        "<br>" +
                                                            "<br>" +

                                                            "<br>" +
                                "<br>" +
                                                                "<br>" +

                                "<hr style=\"width: 95%; background-color: #1d273b !important; height: 4px;" +
                                "" +
                                "border:none; border-bottom:0.7px #05116e solid; height: 1px; " +
                                " box-shadow: none;\">\r\n" +


                      $"<div style=\"color: #1d273b !important; font-size:22px; text-align: center; \">فاتورة طلبات {status}</div>"+

                                       "<div style=\"width:100%; padding-top:8px; display: flex; justify-content: space-between;\">" +
                        "  <div style=\"float:right; width:50%\">" +
                        "    <div>" +
                         "      <div style=\"display: inline-block; margin:0px;  background-color: #f0f0f0; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "              border: 1px solid #6c757d;   border: 1px solid #6c757d; width:16%;\">" +
                        "        رقم الفاتورة" +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "              border: 1px solid #6c757d;  color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{reportId}"+
                        "      </div>" +
                         "       <div style=\"display: inline-block; margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px; border: 1px solid #6c757d; margin-top:2px;  width:16%;\">" +
                        "        التاريخ" +
                        "      </div>" +
                        "       <div style=\"display: inline-block; margin:0px; white-space: nowrap; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{createdDate}" +
                        "      </div>" + "      <div style=\"display: inline-block; white-space: nowrap; margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;  margin-top:2px;   border: 1px solid #6c757d; width:16%;\">" +
                        "        المجموع الكلي " +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; white-space: nowrap; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "             color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{totalAmount}" +
                        "      </div>" + "      <div style=\"display: inline-block; white-space: nowrap;  margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;  margin-top:2px;  border: 1px solid #6c757d; width:16%;\">" +
                        "        المبلغ المستقطع " +
                        "      </div>" +
                        "       <div style=\"display: inline-block; margin:0px; white-space: nowrap; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "              color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       "0" +
                        "      </div>" +
                        "    </div>" +
                        "    <div>" +
                        "      <div style=\"display: inline-block;  white-space: nowrap; background-color: #f0f0f0; margin:0px; border-radius: 8px;" +
                        "             padding: 6px 5px 6px 130px;  margin-top:2px;  border: 1px solid #6c757d;   width:16%;\">" +
                        "        اجمالي أجور التوصيل" +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; white-space: nowrap; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "             color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{deliveryAmount}" +
                        "      </div>" +
                          "      <div style=\"display: inline-block; margin:0px;  white-space: nowrap; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;   margin-top:2px;   border: 1px solid #6c757d; width:16%;\">" +
                        "        المبلغ المتبقي " +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; white-space: nowrap; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "              color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{RemaningAmount}" +
                        "      </div>" +
                          "      <div style=\"display: inline-block; white-space: nowrap;  margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;  margin-top:2px;  border: 1px solid #6c757d;   width:16%;\">" +
                        "        إجمالي عدد الطلبات " +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px;white-space: nowrap;  border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{totalOrderNumber}" +
                        "      </div>" +
                          "      <div style=\"display: inline-block; margin:0px;  white-space: nowrap; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;   margin-top:2px;  border: 1px solid #6c757d; width:16%;\">" +
                        "        عدد الصفحات " +
                        "      </div>" +
                        "       <div style=\"display: inline-block; margin:0px; border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{totalPages}" +
                        "      </div>" +
                        "    </div>" +

                        "  </div>" +
                        
                        "  <div style=\"float:left; width:50%\">" +
                        "    <div>" +
                          "      <div style=\"display: inline-block; margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;   border: 1px solid #6c757d;  width:16%;\">" +
                        "        اسم الشركة " +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px;  white-space: nowrap;  border-radius: 8px; padding: 6px 5px 6px 130px;" +
                        "              color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{storename}" +
                        "      </div>" +

                        "    </div>" +
                        "    <div>" +
                         "      <div style=\"display: inline-block; background-color: #f0f0f0; margin:0px; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;  margin-top:2px;  border: 1px solid #6c757d;  width:16%;\">" +
                        "        العنوان" +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; border-radius: 8px;  white-space: nowrap; padding: 6px 5px 6px 130px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       "TURKEY" +
                        "      </div>" +
                         "      <div style=\"display: inline-block; margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px; margin-top:2px;  border: 1px solid #6c757d; width:16%;\">" +
                        "        رقم الهاتف " +
                        "      </div>" +
                        "     <div style=\"display: inline-block; margin:0px; border-radius: 8px;  white-space: nowrap; padding: 6px 90px 6px 45px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%; direction: ltr;\">" +
                     $"{storephonenumber}" +
                        "      </div>" +
                          "      <div style=\"display: inline-block; white-space: nowrap; margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "             padding: 6px 5px 6px 130px;  margin-top:2px;  border: 1px solid #6c757d;  width:16%;\">" +
                        "        شركة التوصيل " +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; border-radius: 8px;  white-space: nowrap; padding: 6px 5px 6px 130px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{storeName}" +
                        "      </div>" +
                        "      <div style=\"display: inline-block; white-space: nowrap; margin:0px; background-color: #f0f0f0; border-radius: 8px;" +
                        "              padding: 6px 5px 6px 130px;  margin-top:2px;  border: 1px solid #6c757d;  width:16%;\">" +
                        "         الدولة " +
                        "      </div>" +
                        "      <div style=\"display: inline-block; margin:0px; border-radius: 8px;  white-space: nowrap; padding: 6px 5px 6px 130px;" +
                        "               color:#6c757d; border: 1px solid #6c757d; width:16%;\">" +
                       $"{countryName}" +
                        "      </div>" +
                        "    </div>" +
                        "  </div>" +
                                                "<br>"+
                                                "<br>" +
                                                                       "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                "<br>" +
                                                  "<br>" +
                                                "<br>" +
                                                                       "<br>" +
                                                "<br>" +
                                                                                              "<br>" +

                        "</div>";
                       








            var footerHtml =
                                "<br>" +


                "<p> .ملاحظة : يرجى التوقيع عند استسلام المبلغ المستحق وتعبتبر جميع وصولات الاستلام المذكورة في هذه الفاتورة غير نافذة*</p>";




            if (totalPages > 1)
            {
                footerHtml +=
                    "<br>" +
                    "<div style=\"text-align: center; font-size:12px !important; clear: both;\">" + // Use 'clear: both;' to place the page number at the end
                    $"<p style=\"margin-bottom: 0;\">Page {page + 1}/{totalPages}</p>" +
                    "</div>";
            }


            footerHtml += "</div>";

            var html = "<html dir=\"rtl\"><head><meta charset=\"UTF-8\"><style>" +
   "@@font-face { font-family: 'Amiri'; src: url('fonts/Amiri-Regular.ttf'); }" +
   "body { font-family: 'Amiri', sans-serif; font-size: 18px !important;margin-top:20px!important; }" + // Set the font size for the entire body
   "p, h1, h2,span { font-size: 12px !important;}" + // Set the font size for <p>, <h1>, and <h2> elements
   "table { width: 100%; font-weight:normal; text-align: center; font-size: 18px !important; }" + // Center the table horizontally and set text-align to center
   "table, th, td { border-collapse: color:#6c757d; font-weight:normal !importnat;  collapse; border: 0.7px solid black; font-size: 18px !important; }" + // Set font size for table elements
   "th { background-color: #f2f2f2; white-space: nowrap; padding: 8px; text-align: center; font-weight: normal;   border: 1px solid #6c757d; border-radius: 8px;}" + // Header cell styles
    "td { padding: 8px; text-align: center; white-space: nowrap; color:#6c757d; border :1px solid #6c757d ;border-radius: 8px; }" + // Data cell styles with no text wrap
    "tr { margin-bottom: 8px; white-space: nowrap; }" + // Add space between rows and prevent text wrap
   "span { font-size: 18px; }" + // Set the font size for the <span> elements
   "</style></head><body>";



            // Add the headerHtml on top of the table
            html += headerHtml;

            // Add a margin-top to the table by wrapping it in a <div>
            html += "<table>";

            // Add headers to the table
            html += "<tr>";
            html += "<th>#</th>"; // Header for the numbering column
            html += string.Join("", headers.Select(header => $"<th>{header}</th>"));
            html += "</tr>";

            // Add data rows to the table
            var tableRows = orders
                .Skip(startIndex)
                .Take(endIndex - startIndex)
                .Select((order, index) =>
                {
                    var orderNumber = startIndex + index + 1;
                    var rowHtml = $"<tr><td>{orderNumber}</td>";

                    for (int j = 0; j < valueSelectors.Count - 1; j++)
                    {
                        var cellValue = valueSelectors[j](order);
                        rowHtml += $"<td>{cellValue}</td>";
                    }

                    var totalPrice = valueSelectors.Last()(order);
                    var deliveryCompanyPrice = deliveryCompanyPrices.TryGetValue(order.Id, out var price) ? price : 0;
                    var netAmount = order.TotalPrice - deliveryCompanyPrice;

                    rowHtml += $"<td>{totalPrice}</td>"
                        + $"<td>{deliveryCompanyPrice}</td>"
                        + $"<td>{netAmount}</td>"
                        + "</tr>";

                    return rowHtml;
                });

            html += string.Join("", tableRows);

            html += "</table>";

            // Add the footerHtml under the table
            html += footerHtml;

            html += "</body></html>";

            pages.Add(html);

        }

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
