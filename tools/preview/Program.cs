using BookStore.Web.Data;
using BookStore.Web.Domain;
using BookStore.Web.Models;
using BookStore.Web.Tests;
using Microsoft.Extensions.DependencyInjection;

// Bản chạy thử tách biệt: SQLite in-memory, dữ liệu mẫu chỉ tồn tại trong tiến trình này.
// Tài khoản: reader / manager, mật khẩu "  test password  " (có khoảng trắng hai đầu).
using var factory = new BookStoreFactory();
factory.UseKestrel(options => options.ListenAnyIP(5090));
await factory.SeedAsync();
using (var scope = factory.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<IInventoryService>().SetThresholdAsync(new(), default);
    var db = scope.ServiceProvider.GetRequiredService<BookStoreDbContext>();
    var science = db.Categories.First();
    var novel = new Category { Name = "Văn học", Description = "Tiểu thuyết và truyện ngắn" };
    db.Categories.Add(novel);
    for (var i = 1; i <= 14; i++)
        db.Books.Add(new Book
        {
            Title = $"Tuyển tập số {i:00}", Author = i % 2 == 0 ? "Nguyễn Văn A" : "Trần Thị B",
            SKU = $"PV-{i:000}", Category = i % 3 == 0 ? science : novel, Price = 50000 + i * 10000,
            DiscountPrice = i % 4 == 0 ? 40000 + i * 10000 : null, PublishedYear = 2010 + i,
            Inventory = new Inventory { Quantity = i == 5 ? 0 : i % 7 == 0 ? 2 : 20 },
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        });
    await db.SaveChangesAsync();
}
Console.WriteLine("Isolated SQLite preview ready on port 5090.");
await Task.Delay(Timeout.Infinite);
