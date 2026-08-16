using Microsoft.AspNetCore.Identity;
using static lotus_blue.Models.Common;

namespace lotus_blue.Models
{
    public class ApplicationUser : IdentityUser
    {
        // to access his own pages related id
        public int AcessId { get; set; }

        public string? Name { get; set; }

        public Countries? Country { get; set; }
    }
}