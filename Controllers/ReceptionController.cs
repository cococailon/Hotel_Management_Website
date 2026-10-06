using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[Authorize(Roles = AppRoles.Reception)]
[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public class ReceptionController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}