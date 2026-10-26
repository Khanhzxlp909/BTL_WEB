using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BookStore.Web.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BookStoreDbContext>
{
    public BookStoreDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<BookStoreDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("ConnectionStrings__BookStore")
            ?? "Server=localhost;Database=BookStore;Integrated Security=true;TrustServerCertificate=false")
        .Options);
}
