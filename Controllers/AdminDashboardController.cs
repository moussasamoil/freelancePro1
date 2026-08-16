using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace lotus_blue.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        // TODO (user): implement — scan PotentialOrder.PhoneNumber rows, compute what
        // OrderController.NormalizePhone(po.PhoneNumber) would produce, and return a summary
        // (rows scanned, rows that would change, sample of before → after). DO NOT WRITE.
        [HttpPost]
        public IActionResult DryRunPoPhoneNormalization()
        {
            return new JsonResult(new
            {
                message = "TODO: implement dry-run logic for PotentialOrder.PhoneNumber normalization."
            });
        }

        // TODO (user): implement — same scan as the dry-run, but persist the normalized values
        // back to PotentialOrders. Should be idempotent (running twice does nothing new).
        [HttpPost]
        public IActionResult ApplyPoPhoneNormalization()
        {
            return new JsonResult(new
            {
                message = "TODO: implement apply logic for PotentialOrder.PhoneNumber normalization."
            });
        }
    }
}
