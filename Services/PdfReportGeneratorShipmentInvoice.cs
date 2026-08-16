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
using lotus_blue.Models.ViewModel;
using System.Text;
using System.Security.Cryptography.X509Certificates;

public class PdfReportGeneratorShipmentInvoice
{
    private readonly IConverter _pdfConverter;
    private readonly IWebHostEnvironment _environment;
    private readonly DeliveryCompanyService _deliveryCompanyService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PdfReportGeneratorShipmentInvoice(IConverter pdfConverter, IWebHostEnvironment environment, DeliveryCompanyService deliveryCompanyService, IHttpContextAccessor httpContextAccessor)
    {
        _pdfConverter = pdfConverter ?? throw new ArgumentNullException(nameof(pdfConverter));
        _environment = environment;
        _deliveryCompanyService = deliveryCompanyService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<byte[]> CreatePdfReportAsync(
       string DeliveryCompanyName,
       string DeliveryCompanyAddress,
       string DeliveryCompanyPhoneNumber,
       string DeliveryCompanyEmail,
       string createdDate,
       string reportId,
       string totalAmount,
       List<WarehouseDetail> warehouseItems

        )
    {
        var htmlContent = await GenerateHtmlReportAsync(
            DeliveryCompanyName, DeliveryCompanyAddress, DeliveryCompanyPhoneNumber, DeliveryCompanyEmail, createdDate,
            reportId, totalAmount, warehouseItems);

        return await ConvertHtmlToPdfAsync(htmlContent);
    }

    public async Task<string> GenerateHtmlReportAsync(
      string DeliveryCompanyName,
      string DeliveryCompanyAddress,
      string DeliveryCompanyPhoneNumber,
      string DeliveryCompanyEmail,
      string createdDate,
      string reportId,
      string totalAmount,
      List<WarehouseDetail> warehouseItems
   )
    {
        var webRootPath = _environment.WebRootPath;
        var imageFilePath = Path.Combine(webRootPath, "static", "luxiralogojpeg.jpg");
        var imageAsBase64 = Convert.ToBase64String(File.ReadAllBytes(imageFilePath));
        var embeddedImageHtml = $"<img style=\"padding-bottom:15px\" src=\"data:image/jpeg;base64,{imageAsBase64}\" width=\"120\" height=\"60\" />";

        int itemsPerPage = 10;
        var pages = new List<List<WarehouseDetail>>();
        for (int i = 0; i < warehouseItems.Count; i += itemsPerPage)
        {
            pages.Add(warehouseItems.GetRange(i, Math.Min(itemsPerPage, warehouseItems.Count - i)));
        }

        decimal total = warehouseItems.Sum(item => item.Quantity * item.WarehousePrice);

       

        var htmlContentBuilder = new StringBuilder();
        int currentPageIndex = 0; // Start with the first page
        int lastIndex = pages.Count-1; // Index of the last page


        foreach (var page in pages)
        {

            // Dynamic warehouse items for the current page
            string warehouseItemsHtml = "";
            foreach (var item in page)
            {
                warehouseItemsHtml += $"<tr style=\" padding-right:8px  !important; padding-top:8px  !important; padding-bottom:8px  !important; border-top: 1px solid #dee2e6; font-size:16px\">" +
                $"<td style=\"color: #535971 !important; border: none; padding: 12px; vertical-align: top; text-align: right;\">" +
                    $"<p style=\"padding-right: 8px;\">{item.WarehouseName}</p>" +
                $"</td>" +
                $"<td style=\"color: #535971 !important; text-align: center; border: none; padding: 12px; vertical-align: top;\">" +
                    $"<p style=\"padding-right: 8px;\">{item.Quantity}</p>" +
                $"</td>" +
                $"<td style=\"color: #535971 !important; text-align: center; border: none; padding: 12px; vertical-align: top;\">" +
                    $"<p style=\"padding-right: 84px;\"> ${item.WarehousePrice}</p>" +
                $"</td>" +
                $"<td style=\"color: #535971 !important; text-align: left; border: none; padding: 12px; vertical-align: top;\">" +
                  $"<p style=\"padding-right: 8px;\"> {(item.Quantity * item.WarehousePrice):C2}</p>" +
               $"</td>" +
            $"</tr>"; // Your existing row generation logic here
            }

            if (currentPageIndex == lastIndex)
            {
                // Append the total amount HTML only on the last page
                warehouseItemsHtml += $"<tr style=\"border-top: 0.7px solid black; width: 80% !important; margin: 0 auto; text-align: center;\">" +
                   $"<td colspan=\"3\" style=\"border: none; padding: .75rem; vertical-align: top; text-align: right; font-size:16px;\">" +
                       $"<p style=\"padding-right:7px;\">السعر الأجمالي</p>" +
                   $"</td>" +
                   $"<td style=\"text-align: end; border: none; padding: .75rem; vertical-align: top; font-size:16px;\">" +
                       $"${totalAmount}" +
                   $"</td>" +
                   $"</tr>";
            }

            // Start of a new page
            htmlContentBuilder.Append($@"
<html>
<head>
    <meta charset='UTF-8'>
   <style>
@@font-face {{ font-family: 'Amiri'; src: url('fonts/Amiri-Regular.ttf'); }}
body {{font-family: 'Amiri', sans-serif;}}
p {{ font-size:16px !important; margin-bottom:0px; padding-bottom:0px; }}
table {{ border:1px solid rgba(0,0,0,.125) !important; }}
</style>
</head>
<body>
  <div id=""Print"" style=""direction: rtl; width: 100%; margin: 0 auto;"">
  <div>
    <div style=""width: 100%;  margin-bottom: 0; padding-bottom: 0px;"">
                    <div style=""width: 100%; table-layout: fixed; margin-bottom: 0;"">
                      <div style=""display: table; width: 100%;"">
                        <div style=""display: table-row;"">
                          <div style=""display: table-cell; text-align: right; "">
                        <p style=""font-size: calc(1.3rem + .2vw); padding-top: 28px; padding-bottom:0px"">فاتورة  #{reportId}</p>
                           </div>                         
                     <div style=""display: table-cell; text-align: left; vertical-align: top;"">
                      {embeddedImageHtml}
                          </div>
                        </div>
                      </div>
                    </div>


         <hr style=""margin-top: 0; margin-bottom: 20px; height: 0.7px;"">

            <div style=""width: 100%; table-layout: fixed;"">
                 <div style=""display: table; width: 100%;"">
                        <div style=""display: table-row;"">
                            <!-- Sender Company Information as the first cell -->
                            <div style=""display: table-cell; width: 50%; vertical-align: top;"">
                 <section>
                    <p>الشركة المرسلة :</p>
                    <span style=""color: #535971 !important;"">شركة Luxira التركية</span> <br>
                    <span style=""color: #535971 !important;"">اسطنبول , تركيا</span> <br>
                    <div style=""text-align: right; direction: ltr !important;"">
                        <span style="" color: #535971 !important;"">05312855286</span>
                    </div>
                    <span style=""color: #535971 !important;"">Luxiraholding@gmail.com</span>
                </section>
            </div>
            <!-- Receiver Company Information as the second cell -->
            <div style=""display: table-cell; width: 50%; vertical-align: top; text-align: left;"">
                <section>
                    <p>الشركة المستلمة :</p>
                    <span style=""color: #535971 !important;"">{DeliveryCompanyName}</span> <br>
                    <span style=""color: #535971 !important;"">{DeliveryCompanyAddress}</span><br>
                    <span style=""color: #535971 !important;"">{DeliveryCompanyPhoneNumber}</span><br>
                    <span style=""color: #535971 !important;"">{DeliveryCompanyEmail}</span>
                </section>

            </div>
        </div>
    </div>
</div>

  <div style=""width: 100%; table-layout: fixed; margin-bottom: 0; margin-top:25px;"">
                      <div style=""display: table; width: 100%;"">
                        <div style=""display: table-row;"">
                          <div style=""display: table-cell; text-align: right; "">
                              <section>
                                    <p>تاريخ الأرسال:</p>
                              <span style=""color: #535971 !important;"" >  {createdDate}</span>
                                </section>                           </div>                         
                     <div style=""display: table-cell; text-align: left; vertical-align: top;"">
                  <section>
    <span style=""margin-bottom: 0;"">Bank : KUVEYTTURK</span><br>
    <p style=""margin-top: 0; margin-bottom: 0; color: #535971 !important;"">IBAN: TR790020500009545077500001</p>
    <span style=""margin-top: 0; margin-bottom: 0; color: #535971 !important;"">NAME: AHMED FT SALEH</span><br>
</section>
</div>
                        </div>
                      </div>
                    </div>


                        
        </div>
    </div>

 <div style=""box-shadow: 1px solid  rgba(0, 0, 0,.125) !important; width: 100%; margin: 0 auto; padding-top:20px; "">
            <div style=""margin-top:0px; margin-bottom:0px; background-color: #f3f2f7; padding-top:6px; padding-bottom:3px; border-top-left-radius: .25rem; border-top-right-radius: .25rem; border: 1px solid rgba(0,0,0,.125) !important; border-bottom: none !important; "">
                <p style="" margin-bottom:0px; margin-top:0px; text-align: start; padding-bottom:2px;  padding-right:20px;"">تفاصيل الشحنة</p>
            </div>
  <table style=""width: 100% !important; margin-bottom: 0; border-collapse: collapse; font-size:16px !important; "">
         <thead style=""display: table-header-group;"">
            <tr>
                <th colspan=""4"" style=""padding-top: 20px;""></th>
            </tr>
             <tr>
            <th style=""vertical-align: middle; border-top: none; padding: 8px; text-align: right; font-size: 12px !important;"">
                <section><p style=""color:#000e16;  font-size: 12px !important; padding-right:14px; font-weight:400;"">اسم المنتج</p></section>
            </th>
            <th style=""vertical-align: middle; border-top: none; padding: 8px; text-align: center; font-size: 12px !important;"">
                <section><p style="" color:#000e16; font-size: 12px !important; padding-right:3px; font-weight:400;"">الكمية</p></section>
            </th>
            <th style=""vertical-align: middle; border-top: none; padding: 8px; text-align: center; font-size: 12px !important;"">
                <section><p style="" color:#000e16; font-size: 12px !important; padding-right:90px; font-weight:400;"">سعر القطعة</p></section>
            </th>

            <th style=""vertical-align: middle; border-top: none; padding: 8px; text-align: right; font-size: 12px !important; "">
                <section style="" color:#000e16;text-align: end;""><p style=""font-weight:400; font-size: 12px !important; padding-left:14px; "">المجموع</p></section>
            </th>

             </tr>
        </thead>
    <tbody>
         {warehouseItemsHtml} 
         
             </tbody>
          </table>
        </div>
 </div>

</div>
</body>
</html>");



            currentPageIndex++; // Move to the next page



            // Insert a page break if not the last page
            if (page != pages.Last())
            {
                htmlContentBuilder.Append("<div style=\"page-break-after: always;\"></div>");
            }
        }




        return htmlContentBuilder.ToString();





    }


    private async Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent)
    {
        var globalSettings = new GlobalSettings
        {
            ColorMode = ColorMode.Color,
            Orientation = Orientation.Portrait,
            PaperSize = DinkToPdf.PaperKind.A4, // Fully qualify PaperKind reference
            Margins = new MarginSettings { Top = 3, Bottom = 10, Left = 10, Right = 10 },
        };

        var objectSettings = new ObjectSettings
        {
            PagesCount = true,
            HtmlContent = htmlContent,
        };

        var pdfDocument = new HtmlToPdfDocument()
        {
            GlobalSettings = globalSettings,
            Objects = { objectSettings },
        };

        return _pdfConverter.Convert(pdfDocument);
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
