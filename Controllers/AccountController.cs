using System.Security.Claims;
using CinemaSite.Models;
using CinemaSite.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CinemaSite.Controllers;

public sealed class AccountController(CinemaStore store) : Controller
{
    [AllowAnonymous]
    public IActionResult Register() => View(new RegistrationInput());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegistrationInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var user = store.Update(data =>
        {
            if (data.Users.Any(x => x.Email.Equals(input.Email, StringComparison.OrdinalIgnoreCase))) return null;
            var created = new AppUser { Name = input.Name.Trim(), Email = input.Email.Trim(), PasswordHash = PasswordHasher.Hash(input.Password) };
            data.Users.Add(created);
            return created;
        });
        if (user is null) { ModelState.AddModelError(nameof(input.Email), "Этот адрес уже зарегистрирован."); return View(input); }
        await SignIn(user);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null) { ViewBag.ReturnUrl = returnUrl; return View(new LoginInput()); }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginInput input, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(input);
        var user = store.Read(data => data.Users.FirstOrDefault(x => x.IsActive && x.Email.Equals(input.Email, StringComparison.OrdinalIgnoreCase)));
        if (user is null || !PasswordHasher.Verify(input.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Неверная электронная почта или пароль.");
            return View(input);
        }
        await SignIn(user);
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public IActionResult Bookings()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = store.Read(data => data.Bookings.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt)
            .Select(x => { var screening = data.Screenings.FirstOrDefault(s => s.Id == x.ScreeningId); return new AccountBookingViewModel { Booking = x, Screening = screening, Film = screening is null ? null : data.Films.FirstOrDefault(f => f.Id == screening.FilmId) }; }).ToList());
        return View(result);
    }

    [Authorize]
    public IActionResult Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = store.Read(data => data.Users.FirstOrDefault(x => x.Id == userId));
        if (user is null) return NotFound();
        return View(new ProfileInput { Name = user.Name, Email = user.Email });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileInput input)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!ModelState.IsValid) return View(input);
        var result = store.Update(data =>
        {
            var user = data.Users.FirstOrDefault(x => x.Id == userId);
            if (user is null) return "Пользователь не найден.";
            if (data.Users.Any(x => x.Id != userId && x.Email.Equals(input.Email, StringComparison.OrdinalIgnoreCase))) return "Этот адрес уже занят.";
            if (!string.IsNullOrEmpty(input.NewPassword) && !PasswordHasher.Verify(input.CurrentPassword, user.PasswordHash)) return "Для смены пароля введите текущий пароль.";
            user.Name = input.Name.Trim(); user.Email = input.Email.Trim();
            if (!string.IsNullOrEmpty(input.NewPassword)) user.PasswordHash = PasswordHasher.Hash(input.NewPassword);
            return null;
        });
        if (result is not null) { ModelState.AddModelError(string.Empty, result); return View(input); }
        var userUpdated = store.Read(data => data.Users.First(x => x.Id == userId));
        await SignIn(userUpdated);
        TempData["Message"] = "Профиль сохранён.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult Cancel(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        store.Update(data => { var booking = data.Bookings.FirstOrDefault(x => x.Id == id && x.UserId == userId); if (booking is not null && booking.Status == "Ожидает оплаты") booking.Status = "Отменено"; return 0; });
        TempData["Message"] = "Бронирование отменено.";
        return RedirectToAction(nameof(Bookings));
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    private async Task SignIn(AppUser user)
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role) };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
