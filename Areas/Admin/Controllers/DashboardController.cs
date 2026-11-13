using CinemaSite.Models;
using CinemaSite.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSite.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Admin,Manager")]
public sealed class DashboardController(CinemaStore store) : Controller
{
    public IActionResult Index()
    {
        var stats = store.Read(data => new AdminDashboardViewModel
        {
            Films = data.Films.Count(x => x.IsActive), Screenings = data.Screenings.Count(x => x.IsActive && x.StartsAt >= DateTime.Now), Bookings = data.Bookings.Count(x => x.Status != "Отменено"), Reviews = data.Reviews.Count(x => !x.IsApproved), Users = data.Users.Count,
            Items = data.Bookings.OrderByDescending(x => x.CreatedAt).Take(12).Select(b => { var screening = data.Screenings.FirstOrDefault(s => s.Id == b.ScreeningId); return new AdminBookingViewModel { Booking = b, User = data.Users.FirstOrDefault(u => u.Id == b.UserId), Screening = screening, Film = screening is null ? null : data.Films.FirstOrDefault(f => f.Id == screening.FilmId) }; }).ToList()
        });
        return View(stats);
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Films() => View(store.Read(data => data.Films.ToList()));

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult SaveFilm(Film input)
    {
        var invalidTrailer = !string.IsNullOrWhiteSpace(input.TrailerUrl) &&
            (!Uri.TryCreate(input.TrailerUrl, UriKind.Absolute, out var trailer) || trailer.Scheme != Uri.UriSchemeHttps);
        var invalidDuration = ModelState[nameof(Film.DurationMinutes)]?.Errors.Count > 0 || input.DurationMinutes is < 1 or > 500;
        if (string.IsNullOrWhiteSpace(input.Title) || invalidDuration || invalidTrailer)
        {
            TempData["Message"] = string.IsNullOrWhiteSpace(input.Title)
                ? "Заполните название фильма."
                : invalidDuration
                    ? "Укажите длительность фильма числом от 1 до 500 минут."
                    : invalidTrailer
                        ? "Ссылка на трейлер должна начинаться с https://. Поле можно оставить пустым."
                        : "Проверьте заполнение полей фильма.";
            TempData["FilmTitle"] = input.Title;
            TempData["FilmGenre"] = input.Genre;
            TempData["FilmDuration"] = input.DurationMinutes > 0 ? input.DurationMinutes.ToString() : "";
            TempData["FilmAgeRating"] = input.AgeRating;
            TempData["FilmPoster"] = input.Poster;
            TempData["FilmTrailerUrl"] = input.TrailerUrl;
            TempData["FilmDescription"] = input.Description;
            return RedirectToAction(nameof(Films));
        }

        input.Title = input.Title.Trim();
        input.Genre = input.Genre?.Trim() ?? "";
        input.AgeRating = input.AgeRating?.Trim() ?? "12+";
        input.Description = input.Description?.Trim() ?? "";
        input.Poster = input.Poster?.Trim() ?? "";
        input.TrailerUrl = input.TrailerUrl?.Trim() ?? "";
        store.Update(data =>
        {
            var film = data.Films.FirstOrDefault(x => x.Id == input.Id);
            if (film is null) data.Films.Add(input);
            else
            {
                film.Title = input.Title;
                film.Genre = input.Genre;
                film.DurationMinutes = input.DurationMinutes;
                film.AgeRating = input.AgeRating;
                film.Description = input.Description;
                film.Poster = input.Poster;
                film.TrailerUrl = input.TrailerUrl;
            }
            return 0;
        });
        TempData["Message"] = "Фильм добавлен в афишу.";
        return RedirectToAction(nameof(Films));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult DeleteFilm(string id)
    {
        store.Update(data => { var film = data.Films.FirstOrDefault(x => x.Id == id); if (film is not null) { film.IsActive = false; foreach (var screening in data.Screenings.Where(x => x.FilmId == film.Id)) screening.IsActive = false; } return 0; });
        TempData["Message"] = "Фильм скрыт из афиши. Его можно вернуть или удалить навсегда, если по его сеансам нет бронирований.";
        return RedirectToAction(nameof(Films));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult RestoreFilm(string id)
    {
        store.Update(data =>
        {
            var film = data.Films.FirstOrDefault(x => x.Id == id);
            if (film is null) return 0;
            film.IsActive = true;
            return 0;
        });
        TempData["Message"] = "Фильм возвращён в афишу. Чтобы он появился при выборе билетов, добавьте ему сеанс.";
        return RedirectToAction(nameof(Films));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult PermanentlyDeleteFilm(string id)
    {
        var result = store.Update(data =>
        {
            var film = data.Films.FirstOrDefault(x => x.Id == id);
            if (film is null) return 0;
            var screeningIds = data.Screenings.Where(x => x.FilmId == id).Select(x => x.Id).ToHashSet();
            if (data.Bookings.Any(x => screeningIds.Contains(x.ScreeningId))) return -1;
            data.Screenings.RemoveAll(x => screeningIds.Contains(x.Id));
            data.Films.Remove(film);
            return 1;
        });
        TempData["Message"] = result switch
        {
            1 => "Фильм и его сеансы удалены навсегда.",
            -1 => "Фильм нельзя удалить: по его сеансам уже есть бронирования.",
            _ => "Фильм не найден."
        };
        return RedirectToAction(nameof(Films));
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Screenings() => View(store.Read(data => new AdminScreeningsViewModel { Films = data.Films.Where(x => x.IsActive).ToList(), Screenings = data.Screenings.Where(x => x.StartsAt >= DateTime.Now).OrderBy(x => x.StartsAt).Take(1000).ToList() }));

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult SaveScreening(Screening input)
    {
        if (input.StartsAt == default || input.StartsAt <= DateTime.Now)
        {
            TempData["Message"] = "Выберите будущие дату и время сеанса.";
            return RedirectToAction(nameof(Screenings));
        }
        if (input.TicketPrice is < 1 or > 100_000 || string.IsNullOrWhiteSpace(input.Hall))
        {
            TempData["Message"] = "Укажите зал и цену билета от 1 до 100 000 ₽.";
            return RedirectToAction(nameof(Screenings));
        }
        var result = store.Update(data =>
        {
            var film = data.Films.FirstOrDefault(x => x.Id == input.FilmId && x.IsActive);
            if (film is null) return "film";
            input.StartsAt = DateTime.SpecifyKind(input.StartsAt, DateTimeKind.Local);
            var existing = data.Screenings.FirstOrDefault(x => x.Id == input.Id);
            var endsAt = input.StartsAt.AddMinutes(film.DurationMinutes);
            var overlaps = data.Screenings.Any(x =>
            {
                if (x.Id == input.Id || !x.IsActive || x.Hall != input.Hall || x.StartsAt >= endsAt) return false;
                var otherFilm = data.Films.FirstOrDefault(f => f.Id == x.FilmId);
                return otherFilm is not null && input.StartsAt < x.StartsAt.AddMinutes(otherFilm.DurationMinutes);
            });
            if (overlaps) return "overlap";
            if (existing is null) data.Screenings.Add(input);
            else { existing.FilmId = input.FilmId; existing.Hall = input.Hall; existing.StartsAt = input.StartsAt; existing.TicketPrice = input.TicketPrice; }
            return "saved";
        });
        TempData["Message"] = result switch
        {
            "film" => "Выбранный фильм скрыт или не найден. Верните его в афишу и попробуйте снова.",
            "overlap" => "В этом зале уже есть сеанс, который пересекается по времени. Выберите другое время или зал.",
            _ => "Сеанс сохранён."
        };
        return RedirectToAction(nameof(Screenings));
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Users() => View(store.Read(data => data.Users.OrderBy(x => x.Name).ToList()));

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult SetRole(string id, string role)
    {
        if (role is not ("Admin" or "Manager" or "User")) return BadRequest();
        if (id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value) { TempData["Message"] = "Нельзя изменить собственную роль."; return RedirectToAction(nameof(Users)); }
        store.Update(data => { var user = data.Users.FirstOrDefault(x => x.Id == id); if (user is not null) { if (user.Role == "Admin" && role != "Admin" && data.Users.Count(x => x.IsActive && x.Role == "Admin") <= 1) { TempData["Message"] = "Нельзя снять роль у последнего активного администратора."; return 0; } user.Role = role; } return 0; });
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult ToggleUser(string id)
    {
        if (id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value) { TempData["Message"] = "Нельзя отключить собственный аккаунт."; return RedirectToAction(nameof(Users)); }
        store.Update(data => { var user = data.Users.FirstOrDefault(x => x.Id == id); if (user is not null) { if (user.IsActive && user.Role == "Admin" && data.Users.Count(x => x.IsActive && x.Role == "Admin") <= 1) { TempData["Message"] = "Нельзя отключить последнего активного администратора."; return 0; } user.IsActive = !user.IsActive; } return 0; });
        return RedirectToAction(nameof(Users));
    }

    [Authorize(Roles = "Admin,Manager")]
    public IActionResult Reviews() => View(store.Read(data => data.Reviews.OrderByDescending(x => x.CreatedAt).ToList()));

    [Authorize(Roles = "Admin")]
    public IActionResult Messages() => View(store.Read(data => new AdminMessagesViewModel { Messages = data.Messages.OrderByDescending(x => x.CreatedAt).ToList(), Settings = data.Settings }));

    [Authorize(Roles = "Admin")]
    public IActionResult Faq() => View(store.Read(data => data.Faq.OrderBy(x => x.SortOrder).ToList()));

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult SaveFaq(FaqItem input)
    {
        if (string.IsNullOrWhiteSpace(input.Question) || string.IsNullOrWhiteSpace(input.Answer) || input.Question.Length > 250 || input.Answer.Length > 2000) { TempData["Message"] = "Заполните вопрос и ответ (до 250/2000 символов)."; return RedirectToAction(nameof(Faq)); }
        store.Update(data => { var item = data.Faq.FirstOrDefault(x => x.Id == input.Id); if (item is null) { input.SortOrder = data.Faq.Count + 1; data.Faq.Add(input); } else { item.Question = input.Question.Trim(); item.Answer = input.Answer.Trim(); } return 0; });
        return RedirectToAction(nameof(Faq));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult ArchiveFaq(string id)
    {
        store.Update(data => { var item = data.Faq.FirstOrDefault(x => x.Id == id); if (item is not null) item.IsActive = false; return 0; });
        return RedirectToAction(nameof(Faq));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult MarkMessageRead(string id)
    {
        store.Update(data => { var message = data.Messages.FirstOrDefault(x => x.Id == id); if (message is not null) message.IsRead = true; return 0; });
        return RedirectToAction(nameof(Messages));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult SaveSettings(SiteSettings input)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(input.CinemaName) || string.IsNullOrWhiteSpace(input.Address) || string.IsNullOrWhiteSpace(input.Phone) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.Email)) { TempData["Message"] = "Проверьте заполнение контактных настроек."; return RedirectToAction(nameof(Messages)); }
        store.Update(data => { data.Settings = new SiteSettings { CinemaName = input.CinemaName.Trim(), Address = input.Address.Trim(), Phone = input.Phone.Trim(), Email = input.Email.Trim(), OpeningHours = input.OpeningHours.Trim() }; return 0; });
        TempData["Message"] = "Контактная информация обновлена.";
        return RedirectToAction(nameof(Messages));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult ArchiveScreening(string id)
    {
        store.Update(data => { var screening = data.Screenings.FirstOrDefault(x => x.Id == id); if (screening is not null) screening.IsActive = false; return 0; });
        return RedirectToAction(nameof(Screenings));
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public IActionResult ModerateReview(string id, bool approve)
    {
        store.Update(data => { var review = data.Reviews.FirstOrDefault(x => x.Id == id); if (review is not null) review.IsApproved = approve; return 0; });
        return RedirectToAction(nameof(Reviews));
    }

    [HttpPost, Authorize(Roles = "Admin,Manager"), ValidateAntiForgeryToken]
    public IActionResult UpdateBooking(string id, string status)
    {
        if (status is not ("Оплачено" or "Ожидает оплаты" or "Отменено")) return BadRequest();
        store.Update(data => { var booking = data.Bookings.FirstOrDefault(x => x.Id == id); if (booking is not null) booking.Status = status; return 0; });
        return RedirectToAction(nameof(Index));
    }
}
