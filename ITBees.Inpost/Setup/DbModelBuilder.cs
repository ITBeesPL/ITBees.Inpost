using ITBees.Inpost.Entities;
using Microsoft.EntityFrameworkCore;

namespace ITBees.Inpost.Setup;

public static class DbModelBuilder
{
    /// <summary>
    /// Rejestruje encje biblioteki w DbContext aplikacji hosta -
    /// wywołaj w OnModelCreating.
    /// </summary>
    public static void Register(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InpostIntegrationSettings>().HasKey(x => x.Id);
    }
}
