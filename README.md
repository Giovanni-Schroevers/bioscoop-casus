# Bioscoop Casus

Welcome to the Bioscoop Casus project! This documentation explains how to start the environment and outlines some useful developer commands.

## Getting Started

### 1. Start the Database (Docker)

We use a SQL Server 2022 container for our database to keep everything uniform across all computers.

1. Ensure you have **Docker Desktop** installed and running.
2. Open a terminal in this folder (the project root).
3. Start the database in the background by running:
   ```powershell
   docker compose up -d
   ```
   _(To stop the database later, you can run `docker compose down`)_

### 2. Run the API

The backend interacts with the database using Entity Framework Core.
When you run the API, **it automatically creates all the necessary SQL tables and runs a Seeder** to populate mock movies, showing times, and the 6 rooms.

To run it from the terminal:

```powershell
dotnet run --project BioscoopCasus.API
```

_(Or simply press the Play/Run button in JetBrains Rider or Visual Studio)._

---

## Useful Architecture & Database Commands

If you change the C# classes in `BioscoopCasus.Models` (e.g., adding a new property to `Movie`), you need to generate a new "Migration" to let the SQL database know about the changes.

> **Note:** These commands require the EF Core CLI tools. If you get an error that `dotnet ef` doesn't exist, install it globally first:
> `dotnet tool install --global dotnet-ef`

### Create a new Database Migration

When you change a model, run this to generate the SQL update script:

```powershell
dotnet ef migrations add NameOfYourChange --project BioscoopCasus.API
```

_(Example: `dotnet ef migrations add AddTrailerUrlToMovie --project BioscoopCasus.API`)_

### Apply Migrations Manually

Our application is setup to automatically update the database when it starts (in `Program.cs`), but if you ever need to apply them manually via CLI:

```powershell
dotnet ef database update --project BioscoopCasus.API
```

### Undo / Remove the Last Migration

If you made a mistake in your C# model _after_ generating a migration, but haven't applied/pushed it yet, you can remove it:

```powershell
dotnet ef migrations remove --project BioscoopCasus.API
```

## Dropping / Resetting the whole database

If you severely messed up the database or want to re-run the `BioscoopDbSeeder` from scratch to get fresh data:

1. Delete the volume in docker: `docker compose down -v`
2. Start it back up: `docker compose up -d`
3. Run the API. The tables will be regenerated automatically!
