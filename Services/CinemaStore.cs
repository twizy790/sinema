using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using CinemaSite.Models;

namespace CinemaSite.Services;

public sealed class CinemaData
{
    public SiteSettings Settings { get; set; } = new();
    public List<FaqItem> Faq { get; set; } = [];
    public List<AppUser> Users { get; set; } = [];
    public List<Film> Films { get; set; } = [];
    public List<Screening> Screenings { get; set; } = [];
    public List<BookingRecord> Bookings { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
    public List<ContactMessage> Messages { get; set; } = [];
}

public sealed class CinemaStore
{
    private readonly string _jsonFile;
    private readonly string _databaseFile;
    private readonly object _gate = new();
    private CinemaData? _cache;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public CinemaStore(IWebHostEnvironment environment)
    {
        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _jsonFile = Path.Combine(folder, "cinema.json");
        _databaseFile = Path.Combine(folder, "cinema.db");
        CreateSchema();
        if (HasData()) { var data = ReadDatabase(); if (UpdatePosterPaths(data)) Save(data); else _cache = data; }
        else
        {
            var data = File.Exists(_jsonFile) ? JsonSerializer.Deserialize<CinemaData>(File.ReadAllText(_jsonFile), JsonOptions) ?? Seed() : Seed();
            if (data.Users.Count == 0 && !File.Exists(_jsonFile))
            {
                data.Users.Add(new AppUser { Name = "Администратор", Email = "admin@cinema.local", PasswordHash = PasswordHasher.Hash("Admin123!"), Role = "Admin" });
                data.Users.Add(new AppUser { Name = "Менеджер", Email = "manager@cinema.local", PasswordHash = PasswordHasher.Hash("Manager123!"), Role = "Manager" });
            }
            if (data.Settings.CinemaName == "СИНЕМА") data.Settings.CinemaName = "Синема";
            if (data.Faq.Count == 0) data.Faq = Seed().Faq;
            Save(data);
        }
    }

    public T Read<T>(Func<CinemaData, T> read) { lock (_gate) return read(_cache!); }
    public T Update<T>(Func<CinemaData, T> update)
    {
        lock (_gate) { var result = update(_cache!); Save(_cache!); return result; }
    }

    private void CreateSchema()
    {
        using var db = new SqliteConnection(_databaseFile);
        db.Exec("PRAGMA journal_mode=WAL; CREATE TABLE IF NOT EXISTS Settings(Id INTEGER PRIMARY KEY CHECK(Id=1), Payload TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS Faq(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS Users(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS Films(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS Screenings(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS Bookings(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL); CREATE TABLE IF NOT EXISTS Reviews(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS Messages(Id TEXT PRIMARY KEY, Payload TEXT NOT NULL);");
    }
    private bool HasData() { using var db = new SqliteConnection(_databaseFile); return db.ScalarInt("SELECT COUNT(*) FROM Settings") > 0; }
    private CinemaData ReadDatabase()
    {
        using var db = new SqliteConnection(_databaseFile);
        var data = new CinemaData { Settings = JsonSerializer.Deserialize<SiteSettings>(db.ScalarText("SELECT Payload FROM Settings WHERE Id=1"), JsonOptions) ?? new() };
        data.Faq = ReadRows<FaqItem>(db, "Faq"); data.Users = ReadRows<AppUser>(db, "Users"); data.Films = ReadRows<Film>(db, "Films");
        data.Screenings = ReadRows<Screening>(db, "Screenings"); data.Bookings = ReadRows<BookingRecord>(db, "Bookings");
        data.Reviews = ReadRows<Review>(db, "Reviews"); data.Messages = ReadRows<ContactMessage>(db, "Messages"); return data;
    }
    private static List<T> ReadRows<T>(SqliteConnection db, string table)
    {
        var rows = new List<T>(); using var statement = db.Query($"SELECT Payload FROM {table}");
        while (statement.Step()) { var item = JsonSerializer.Deserialize<T>(statement.Text(0), JsonOptions); if (item is not null) rows.Add(item); }
        return rows;
    }
    private static bool UpdatePosterPaths(CinemaData data)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["photo-1509248961158-e54f6934749c"] = "poster-dune.jpg",
            ["photo-1489599849927-2ee91cede3ba"] = "poster-oppenheimer.avif",
            ["poster-oppenheimer.jpg"] = "poster-oppenheimer.avif",
            ["photo-1478720568477-152d9b164e26"] = "poster-panda.avif",
            ["photo-1485846234645-a62644f84728"] = "poster-poor-things.jpg"
        };
        var changed = false;
        foreach (var film in data.Films)
        {
            if (!map.TryGetValue(film.Poster, out var file)) continue;
            film.Poster = file;
            changed = true;
        }
        return changed;
    }
    private void Save(CinemaData data)
    {
        using var db = new SqliteConnection(_databaseFile); db.Exec("BEGIN IMMEDIATE;");
        try
        {
            db.Exec("DELETE FROM Settings; DELETE FROM Faq; DELETE FROM Users; DELETE FROM Films; DELETE FROM Screenings; DELETE FROM Bookings; DELETE FROM Reviews; DELETE FROM Messages;");
            db.InsertSetting(JsonSerializer.Serialize(data.Settings));
            InsertRows(db,"Faq",data.Faq); InsertRows(db,"Users",data.Users); InsertRows(db,"Films",data.Films); InsertRows(db,"Screenings",data.Screenings);
            InsertRows(db,"Bookings",data.Bookings); InsertRows(db,"Reviews",data.Reviews); InsertRows(db,"Messages",data.Messages); db.Exec("COMMIT;"); _cache = data;
        }
        catch { db.Exec("ROLLBACK;"); throw; }
    }
    private static void InsertRows<T>(SqliteConnection db,string table,List<T> rows)
    {
        foreach(var row in rows) { var id = (string?)typeof(T).GetProperty("Id")?.GetValue(row) ?? Guid.NewGuid().ToString("N"); db.Insert(table,id,JsonSerializer.Serialize(row)); }
    }

    private static CinemaData Seed()
    {
        var data = new CinemaData();
        data.Faq.AddRange(new[] { new FaqItem { Question="За сколько минут до начала лучше приходить?", Answer="Рекомендуем приходить за 15–20 минут, чтобы забрать заказ в кинобаре и спокойно найти зал.", SortOrder=1 }, new FaqItem { Question="Можно ли вернуть билет?", Answer="Возврат доступен до начала сеанса. Для оформления обратитесь в кассу.", SortOrder=2 }, new FaqItem { Question="Как выбрать места рядом?", Answer="На странице бронирования выберите сеанс и свободные кресла на схеме.", SortOrder=3 } });
        var films = new[] { new Film { Id="dune-2", Title="Дюна: Часть вторая", Genre="Фантастика", DurationMinutes=166, AgeRating="16+", Description="Пол Атрейдес объединяется с фременами и продолжает путь к своей судьбе.", Poster="poster-dune.jpg" }, new Film { Id="oppenheimer", Title="Оппенгеймер", Genre="Драма", DurationMinutes=180, AgeRating="18+", Description="История учёного, чьи открытия изменили ход истории.", Poster="poster-oppenheimer.jpg" }, new Film { Id="panda-4", Title="Кунг-фу Панда 4", Genre="Мультфильм", DurationMinutes=94, AgeRating="6+", Description="По отправляется навстречу новым приключениям.", Poster="poster-panda.avif" }, new Film { Id="poor-things", Title="Бедные-несчастные", Genre="Фэнтези", DurationMinutes=141, AgeRating="18+", Description="Необыкновенное путешествие Беллы Бакстер по большому миру.", Poster="poster-poor-things.jpg" } };
        data.Films.AddRange(films); var today = DateTime.Today; var times = new[] { (10,0),(14,0),(18,0),(22,0) };
        for(var day=0;day<30;day++) for(var slot=0;slot<times.Length;slot++) for(var fi=0;fi<films.Length;fi++) { var (h,m)=times[slot]; var film=films[fi]; data.Screenings.Add(new Screening { Id=Guid.NewGuid().ToString("N"), FilmId=film.Id, Hall=new[]{"Синий","Красный","Зелёный","Оранжевый"}[(day+slot+fi)%4], StartsAt=today.AddDays(day).AddHours(h).AddMinutes(m), TicketPrice=h<12?350:450+(h>19?100:0), Rows=5, SeatsPerRow=8 }); }
        return data;
    }

    private sealed class SqliteConnection : IDisposable
    {
        private IntPtr _db;
        public SqliteConnection(string path) { var rc=Native.open(path,out _db); if(rc!=0) throw new InvalidOperationException("Не удалось открыть SQLite: "+Error); }
        private string Error => Marshal.PtrToStringUTF8(Native.errmsg(_db)) ?? "SQLite error";
        public void Exec(string sql) { var rc=Native.exec(_db,sql,IntPtr.Zero,IntPtr.Zero,out var err); if(rc!=0){var msg=Marshal.PtrToStringUTF8(err)??Error; Native.free(err); throw new InvalidOperationException(msg);} }
        public int ScalarInt(string sql) { using var q=Query(sql); return q.Step()?q.Int(0):0; }
        public string ScalarText(string sql) { using var q=Query(sql); return q.Step()?q.Text(0):"{}"; }
        public SqliteStatement Query(string sql) { var rc=Native.prepare(_db,sql,-1,out var st,IntPtr.Zero); if(rc!=0) throw new InvalidOperationException(Error); return new SqliteStatement(st); }
        public void InsertSetting(string payload) { using var q=Query("INSERT INTO Settings(Id,Payload) VALUES(1,'"+payload.Replace("'","''")+"')"); q.Step(); }
        public void Insert(string table,string id,string payload) { using var q=Query("INSERT INTO "+table+"(Id,Payload) VALUES('"+id.Replace("'","''")+"','"+payload.Replace("'","''")+"')"); q.Step(); }
        public void Dispose(){if(_db!=IntPtr.Zero){Native.close(_db);_db=IntPtr.Zero;}}
        private static class Native
        {
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_open")] internal static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string filename,out IntPtr db);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_close")] internal static extern int close(IntPtr db);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_exec")] internal static extern int exec(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,IntPtr callback,IntPtr arg,out IntPtr errmsg);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_prepare_v2")] internal static extern int prepare(IntPtr db,[MarshalAs(UnmanagedType.LPUTF8Str)] string sql,int n,out IntPtr stmt,IntPtr tail);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_step")] internal static extern int step(IntPtr stmt);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_finalize")] internal static extern int finalize(IntPtr stmt);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_column_int64")] internal static extern long column_int64(IntPtr stmt,int col);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_column_text")] internal static extern IntPtr column_text(IntPtr stmt,int col);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_free")] internal static extern void free(IntPtr p);
            [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_errmsg")] internal static extern IntPtr errmsg(IntPtr db);
        }
        public sealed class SqliteStatement : IDisposable
        {
            private IntPtr _stmt; internal SqliteStatement(IntPtr stmt)=>_stmt=stmt;
            public bool Step(){var r=Native.step(_stmt);if(r==100)return true;if(r==101)return false;throw new InvalidOperationException("Ошибка SQLite запроса: "+r);}
            public int Int(int col)=>(int)Native.column_int64(_stmt,col);
            public string Text(int col)=>Marshal.PtrToStringUTF8(Native.column_text(_stmt,col))??"";
            public void Dispose(){if(_stmt!=IntPtr.Zero){Native.finalize(_stmt);_stmt=IntPtr.Zero;}}
        }
    }
}
