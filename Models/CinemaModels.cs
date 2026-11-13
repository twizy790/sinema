namespace CinemaSite.Models;

public sealed class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Film
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string AgeRating { get; set; } = "12+";
    public string Description { get; set; } = "";
    public string Poster { get; set; } = "";
    public string TrailerUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class Screening
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilmId { get; set; } = "";
    public string Hall { get; set; } = "Синий";
    public DateTime StartsAt { get; set; }
    public int TicketPrice { get; set; } = 450;
    public int Rows { get; set; } = 5;
    public int SeatsPerRow { get; set; } = 8;
    public bool IsActive { get; set; } = true;
}

public sealed class BookingRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public string ScreeningId { get; set; } = "";
    public List<string> Seats { get; set; } = [];
    public int Total { get; set; }
    public string Status { get; set; } = "Ожидает оплаты";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Review
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public string UserName { get; set; } = "";
    public string Text { get; set; } = "";
    public int Rating { get; set; } = 5;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsApproved { get; set; }
}

public sealed class ContactMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

public sealed class SiteSettings
{
    public string CinemaName { get; set; } = "Синема";
    public string Address { get; set; } = "Казань, ул. Баумана, 15";
    public string Phone { get; set; } = "+7 (843) 200-00-00";
    public string Email { get; set; } = "hello@cinema.example";
    public string OpeningHours { get; set; } = "Ежедневно, 10:00–02:00";
}

public sealed class FaqItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AccountBookingViewModel
{
    public BookingRecord Booking { get; set; } = new();
    public Screening? Screening { get; set; }
    public Film? Film { get; set; }
}

public sealed class AdminBookingViewModel
{
    public BookingRecord Booking { get; set; } = new();
    public AppUser? User { get; set; }
    public Screening? Screening { get; set; }
    public Film? Film { get; set; }
}

public sealed class AdminDashboardViewModel
{
    public int Films { get; set; }
    public int Screenings { get; set; }
    public int Bookings { get; set; }
    public int Reviews { get; set; }
    public int Users { get; set; }
    public List<AdminBookingViewModel> Items { get; set; } = [];
}

public sealed class AdminScreeningsViewModel
{
    public List<Film> Films { get; set; } = [];
    public List<Screening> Screenings { get; set; } = [];
}

public sealed class AdminMessagesViewModel
{
    public List<ContactMessage> Messages { get; set; } = [];
    public SiteSettings Settings { get; set; } = new();
}

public sealed class RegistrationInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(200)]
    public string Email { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";
}

public sealed class LoginInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required]
    public string Password { get; set; } = "";
}

public sealed class ReviewInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(1000, MinimumLength = 5)]
    public string Text { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Range(1, 5)]
    public int Rating { get; set; } = 5;
}

public sealed class ContactInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80)]
    public string Name { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress]
    public string Email { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(2000, MinimumLength = 5)]
    public string Message { get; set; } = "";
}

public sealed class ProfileInput
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(80, MinimumLength = 2)]
    public string Name { get; set; } = "";
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(200)]
    public string Email { get; set; } = "";
    public string CurrentPassword { get; set; } = "";
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string? NewPassword { get; set; }
}
