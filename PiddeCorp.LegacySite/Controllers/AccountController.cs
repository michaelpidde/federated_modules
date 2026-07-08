using Microsoft.AspNetCore.Mvc;

namespace PiddeCorp.LegacySite.Controllers;

public class AccountController : Controller {
    [HttpGet]
    public IActionResult Login() {
        return View();
    }
    
    [HttpPost]
    public IActionResult Login(string username, string password) {
        if(string.IsNullOrWhiteSpace(username)) {
            ModelState.AddModelError(string.Empty, "Enter a username - password can be anything, this is fake.");
            return View();
        }
        
        HttpContext.Session.SetString("LegacyUser", username);
        return RedirectToAction("Tile", "Portal");
    }
    
    public IActionResult Logout() {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }
}
