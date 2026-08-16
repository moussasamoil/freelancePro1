using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using static lotus_blue.Models.Common;

namespace lotus_blue.ViewModels
{
    public class CampaignViewModel
    {
        public int Id { get; set; }

        [Display(Name = "رابط الصورة")]
        public string? ImageUrl { get; set; }

        [Display(Name = "صورة الحملة")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "صور الحملة")]
        public List<IFormFile> ImageFiles { get; set; } = new();

        // Used for edit (single country)
        [Display(Name = "البلد")]
        public Countries Country { get; set; }

        // Used for create (multiple countries) and edit (pre-populated)
        [Display(Name = "البلدان")]
        public List<Countries> SelectedCountries { get; set; } = new();

        [Required]
        [Display(Name = "المنتج الرئيسي")]
        public int MainWarehouseId { get; set; }

        public string? WarehouseName { get; set; }

        [Display(Name = "المتجر")]
        public int? ManufacturingCompanyId { get; set; }

        public string? ManufacturingCompanyName { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;
    }
}