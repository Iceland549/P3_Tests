using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using P3AddNewFunctionalityDotNetCore.Models.ViewModels;

namespace P3AddNewFunctionalityDotNetCore.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        public AccountController(UserManager<IdentityUser> userMgr,
        SignInManager<IdentityUser> signInMgr)
        {
            _userManager = userMgr;
            _signInManager = signInMgr;
        }

        [AllowAnonymous]
        public ViewResult Login(string returnUrl)
        {
            return View(new LoginModel
            {
                ReturnUrl = returnUrl
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel loginModel)
        {
            if (ModelState.IsValid)
            {
                IdentityUser user =
                await _userManager.FindByNameAsync(loginModel.Name);
                if (user != null)
                {
                    await _signInManager.SignOutAsync();
                    if ((await _signInManager.PasswordSignInAsync(user,
                    loginModel.Password, false, false)).Succeeded)
                    {
                        return Redirect(loginModel.ReturnUrl ?? "/Admin/Index");                       
                    }
                }
            }
            ModelState.AddModelError("", "Invalid name or password");
            return View(loginModel);
        }

        public async Task<RedirectResult> Logout(string returnUrl = "/")
        {
            await _signInManager.SignOutAsync();
            return Redirect(returnUrl);
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> LoginForTesting([FromForm] string username, [FromForm] string password)
        {
            Console.WriteLine($"LoginForTesting called with username: {username}");
            var currentEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            Console.WriteLine($"Current environment in LoginForTesting: {currentEnv}");

            Console.WriteLine($"Current environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}");
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Testing")
            {
                Console.WriteLine("Not in Development environment");
                return BadRequest("Not in Development environment");
            }

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                Console.WriteLine("User not found");
                return BadRequest("User not found");
            }

            var result = await _signInManager.PasswordSignInAsync(username, password, false, false);
            if (result.Succeeded)
            {
                Console.WriteLine("Login succeeded");
                return Ok();
            }

            Console.WriteLine($"LoginForTesting called with username: {username}");
            Console.WriteLine("User not found");
            Console.WriteLine($"Login failed: {result}");
            Console.WriteLine("Login succeeded");
            return BadRequest($"Login failed: {result}");
        }
    }
}