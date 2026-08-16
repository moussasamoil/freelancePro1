using System.ComponentModel.DataAnnotations;
using Z.EntityFramework.Extensions;

namespace lotus_blue.Models.AppViewModel
{
    public class AppEmployeeViewmodel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Cv { get; set; }

        public string Img { get; set; }

        public string JobTitle { get; set; }

        public string IdNumber { get; set; }

        public string AcademicLevel { get; set; }

        public string Nationality { get; set; }


        public string Salary { get; set; }

        public string Address { get; set; }
        public string PhoneNumber { get; set; }

        public string Email { get; set; }

        public bool Gender { get; set; }

        public string DateOfBirth { get; set; }

        public string AddedDate { get; set; }

        public string LastEditedDay { get; set; }
        public bool IsActive { get; set; }   

        public bool IsShown { get; set; }

        public string DeliveryCompanyName { get; set; } 

    }




    public class AppCreateEmployeeViewmodel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; }



        [Required(ErrorMessage = "Job title is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Job title must be between 2 and 100 characters")]
        public string JobTitle { get; set; }

        [Required(ErrorMessage = "Academic level is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Academic level must be between 2 and 50 characters")]
        public string AcademicLevel { get; set; }

        [Required(ErrorMessage = "Nationality is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Nationality must be between 2 and 50 characters")]
        public string Nationality { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Address must be between 2 and 200 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Phone number must be between 10 and 15 characters")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "Email must be between 5 and 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public bool Gender { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "ID number is required")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "ID number must be between 2 and 20 characters")]
        public string IdNumber { get; set; }

        public string? LastEditedDay { get; set; }

        [Required(ErrorMessage = "Active status is required")]
        public bool IsActive { get; set; }

        [Required(ErrorMessage = "Show status is required")]
        public bool IsShown { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Role is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Role must be between 2 and 50 characters")]
        public string Role { get; set; }

        [Required(ErrorMessage = "Salary is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Salary must be a non-negative value")]
        public decimal Salary { get; set; }

        public int? DeliveryCompanyId { get; set; }
    }
    public class AppEditEmployeeViewmodel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 100 characters")]
        public string Name { get; set; }



        [Required(ErrorMessage = "Job title is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Job title must be between 2 and 100 characters")]
        public string JobTitle { get; set; }

        [Required(ErrorMessage = "Academic level is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Academic level must be between 2 and 50 characters")]
        public string AcademicLevel { get; set; }

        [Required(ErrorMessage = "Nationality is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Nationality must be between 2 and 50 characters")]
        public string Nationality { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Address must be between 2 and 200 characters")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Phone number must be between 10 and 15 characters")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "Email must be between 5 and 100 characters")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        public bool Gender { get; set; }

        [Required(ErrorMessage = "Date of birth is required")]
        public DateTime DateOfBirth { get; set; }

        [Required(ErrorMessage = "ID number is required")]
        [StringLength(20, MinimumLength = 2, ErrorMessage = "ID number must be between 2 and 20 characters")]
        public string IdNumber { get; set; }

        public string? LastEditedDay { get; set; }

        [Required(ErrorMessage = "Active status is required")]
        public bool IsActive { get; set; }

        [Required(ErrorMessage = "Show status is required")]
        public bool IsShown { get; set; }

        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
        public string Password { get; set; }

        [StringLength(50, MinimumLength = 2, ErrorMessage = "Role must be between 2 and 50 characters")]
        public string Role { get; set; }

        [Required(ErrorMessage = "Salary is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Salary must be a non-negative value")]
        public decimal Salary { get; set; }

        public int? DeliveryCompanyId { get; set; }
    }

}
