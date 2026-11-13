using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CinemaSite.Models;
using CinemaSite.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CinemaSite.Controllers;

public class HomeController(CinemaStore store) : Controller
{
    public IActionResult Index()
    {
        ViewBag.Films = store.Read(data => data.Films.Where(x => x.IsActive).ToList());
        ViewBag.Screenings = store.Read(data => data.Screenings.Where(x => x.IsActive && x.StartsAt >= DateTime.Now).OrderBy(x => x.StartsAt).ToList());
        ViewBag.Faq = store.Read(data => data.Faq.Where(x => x.IsActive).OrderBy(x => x.SortOrder).ToList());
        ViewBag.Settings = store.Read(data => data.Settings);
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult About() => View();
    public IActionResult Schedule(string? date)
    {
        var selectedDate = DateTime.TryParse(date, out var parsed) ? parsed.Date : DateTime.Today;
        ViewBag.SelectedDate = selectedDate;
        ViewBag.Films = store.Read(data => data.Films.Where(x => x.IsActive).ToList());
        ViewBag.Screenings = store.Read(data => data.Screenings.Where(x => x.IsActive && x.StartsAt.Date == selectedDate).OrderBy(x => x.StartsAt).ToList());
        return View();
    }
    public IActionResult Services() => View();
    public IActionResult Booking(string? screeningId)
    {
        var screenings = store.Read(data => data.Screenings.Where(x => x.IsActive && x.StartsAt >= DateTime.Now).OrderBy(x => x.StartsAt).Take(1000).ToList());
        var films = store.Read(data => data.Films.Where(x => x.IsActive).ToList());
        var selected = screenings.FirstOrDefault(x => x.Id == screeningId) ?? screenings.FirstOrDefault();
        ViewBag.Screenings = screenings;
        ViewBag.Films = films;
        ViewBag.SelectedScreening = selected;
        ViewBag.BookedSeats = selected is null ? new List<string>() : store.Read(data => data.Bookings.Where(x => x.ScreeningId == selected.Id && x.Status != "Отменено").SelectMany(x => x.Seats).ToList());
        return View();
    }
    public IActionResult Reviews() => View(store.Read(data => data.Reviews.Where(x => x.IsApproved).OrderByDescending(x => x.CreatedAt).ToList()));
    public IActionResult Contacts() => View(store.Read(data => data.Settings));
    public IActionResult Account() => View();
    public IActionResult Team() => View();

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult Booking(string screeningId, string[] seats)
    {
        if (seats is null || seats.Length == 0)
        {
            TempData["BookingMessage"] = "Выберите хотя бы одно свободное место.";
            return RedirectToAction(nameof(Booking), new { screeningId });
        }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var booking = store.Update(data =>
        {
            var screening = data.Screenings.FirstOrDefault(x => x.Id == screeningId && x.IsActive && x.StartsAt > DateTime.Now);
            if (screening is null) return (BookingRecord?)null;
            var validSeats = seats.Distinct().Where(seat => IsValidSeat(seat, screening) && !data.Bookings.Any(b => b.ScreeningId == screeningId && b.Status != "Отменено" && b.Seats.Contains(seat))).ToList();
            if (validSeats.Count != seats.Distinct().Count() || validSeats.Count == 0) return (BookingRecord?)null;
            var created = new BookingRecord { UserId = userId, ScreeningId = screening.Id, Seats = validSeats, Total = validSeats.Count * screening.TicketPrice };
            data.Bookings.Add(created);
            return created;
        });
        if (booking is null) { TempData["BookingMessage"] = "Одно из мест уже занято или сеанс недоступен. Проверьте выбор."; return RedirectToAction(nameof(Booking), new { screeningId }); }
        TempData["BookingMessage"] = $"Места забронированы. К оплате: {booking.Total} ₽. Онлайн-оплата не подключена, заказ сохранён как ожидающий оплаты.";
        return RedirectToAction("Bookings", "Account");
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public IActionResult Reviews(ReviewInput input)
    {
        if (!ModelState.IsValid) { TempData["Message"] = "Отзыв должен содержать от 5 до 1000 символов."; return RedirectToAction(nameof(Reviews)); }
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        store.Update(data => { data.Reviews.Add(new Review { UserId = userId, UserName = User.Identity?.Name ?? "Гость", Text = input.Text.Trim(), Rating = input.Rating }); return 0; });
        TempData["Message"] = "Спасибо! Отзыв появится после модерации.";
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Contacts(ContactInput input)
    {
        if (!ModelState.IsValid) { TempData["Message"] = "Заполните все поля корректно."; return RedirectToAction(nameof(Contacts)); }
        store.Update(data => { data.Messages.Add(new ContactMessage { Name = input.Name.Trim(), Email = input.Email.Trim(), Message = input.Message.Trim() }); return 0; });
        TempData["Message"] = "Спасибо, сообщение отправлено.";
        return RedirectToAction(nameof(Contacts));
    }

    private static bool IsValidSeat(string value, Screening screening)
    {
        var parts = value.Split('-', 2);
        return parts.Length == 2 && int.TryParse(parts[0], out var row) && int.TryParse(parts[1], out var seat) && row >= 1 && row <= screening.Rows && seat >= 1 && seat <= screening.SeatsPerRow;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
