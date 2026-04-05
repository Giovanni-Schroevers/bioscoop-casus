# Bioscoop Casus

A cinema ticketing web application built with ASP.NET Core (REST API) and Blazor WebAssembly (frontend). The app lets visitors browse movies, select seats, order popcorn, and pay, and gives staff access to a management dashboard with revenue and occupancy analytics.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Getting the Source Code](#2-getting-the-source-code)
3. [Project Structure](#3-project-structure)
4. [Running the Application](#4-running-the-application)
5. [Using the Application](#5-using-the-application)
6. [Running Without an IDE (Acceptance Environment)](#6-running-without-an-ide-acceptance-environment)
7. [Database & Migrations](#7-database--migrations)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Prerequisites

Before you begin, make sure the following is installed on your machine:

| Tool | Version | Download |
| --- | --- | --- |
| **.NET SDK** | 10.0 or higher | <https://dotnet.microsoft.com/download> |
| **Git** | any recent | <https://git-scm.com/downloads> |
| **A modern browser** | Chrome, Firefox, Edge, Safari | — |

You do **not** need to install a local database. The application is pre-configured to connect to a hosted MySQL database (filess.io) via the connection string in `BioscoopCasus.API/appsettings.json`.

> Optional: **JetBrains Rider** or **Visual Studio 2022+** if you prefer working in an IDE. Not required.

Verify your .NET installation:

```bash
dotnet --version
```

You should see `10.0.x` or higher.

---

## 2. Getting the Source Code

Clone the repository and navigate into the project folder:

```bash
git clone https://github.com/Giovanni-Schroevers/bioscoop-casus.git
cd bioscoop-casus
```

Restore the NuGet packages for all projects:

```bash
dotnet restore BioscoopCasus.slnx
```

---

## 3. Project Structure

The solution is split into three projects:

```
bioscoop-casus/
├── BioscoopCasus.API/         # ASP.NET Core backend (REST API + EF Core)
├── BioscoopCasus.Models/      # Shared DTOs, data models and helpers
├── BioscoopCasus.Web/         # Blazor WebAssembly frontend (MudBlazor)
└── BioscoopCasus.slnx         # Solution file linking the three projects
```

- `BioscoopCasus.API` runs the REST API and talks to the MySQL database via Entity Framework Core.
- `BioscoopCasus.Web` is the Blazor WebAssembly client that calls the API.
- `BioscoopCasus.Models` contains DTOs and data models shared between the two.

---

## 4. Running the Application

The app consists of **two services** that must both be running: the API and the Web frontend. Open **two terminals**, one for each.

> **Important:** Always start the **API first**. The Web frontend contacts the API on startup to load its ticket pricing configuration, and will fail to launch if the API is not reachable.

### Terminal 1 — Start the API

```bash
dotnet run --project BioscoopCasus.API
```

On first start the API will automatically:
- Apply any pending Entity Framework Core migrations
- Run the `BioscoopDbSeeder` to populate the database with 6 rooms, mock movies and showtimes (only if empty)

The API will be available at: **<http://localhost:5064>**

### Terminal 2 — Start the Web Frontend

```bash
dotnet run --project BioscoopCasus.Web
```

The frontend will be available at: **<http://localhost:5158>**

Your browser should open automatically. If it doesn't, navigate to <http://localhost:5158> manually.

> Both services need to stay running. Stop them with `Ctrl+C` in each terminal.

---

## 5. Using the Application

Once both services are running, you can use the following parts of the app:

### Customer flow

| Page | URL |
| --- | --- |
| Home page | <http://localhost:5158> |
| Movies overview | <http://localhost:5158/movies> |
| Films overview | <http://localhost:5158/films> |
| Ticket printing terminal | <http://localhost:5158/TicketPrint> |
| Mystery movie | <http://localhost:5158/mystery> |

A typical visitor flow:
1. Browse movies from the home page or `/movies`.
2. Click a movie to view details and showtimes.
3. Pick a showtime → select seats → add popcorn → pay (simulated PIN or QR).
4. Receive a ticket with a QR code.

### Management dashboard

The management pages are available at:

| Page | URL |
| --- | --- |
| Login | <http://localhost:5158/login> |
| Management home | <http://localhost:5158/management> |
| Movie management | <http://localhost:5158/management/movies> |
| Room management | <http://localhost:5158/management/rooms> |
| Occupancy analytics | <http://localhost:5158/management/analytics/occupancy> |
| Revenue analytics | <http://localhost:5158/management/analytics/revenue> |
| Email template editor | <http://localhost:5158/management/email-editor> |

Authentication is handled via JWT tokens. Log in at `/login` first to access the management pages.

---

## 6. Running Without an IDE (Acceptance Environment)

For a demo or acceptance environment, you don't need Visual Studio or Rider. You can publish both projects into self-contained folders and run them from the command line.

### Step 1 — Publish both projects

From the project root:

```bash
dotnet publish BioscoopCasus.API -c Release -o ./publish/api
dotnet publish BioscoopCasus.Web -c Release -o ./publish/web
```

This produces two ready-to-run folders under `./publish/`.

### Step 2 — Run the API (start this first)

```bash
dotnet ./publish/api/BioscoopCasus.API.dll --urls "http://localhost:5064"
```

Leave this terminal running.

### Step 3 — Serve the Web frontend

Blazor WebAssembly compiles into a static site. After publishing, the complete static site lives in:

```
./publish/web/wwwroot/
```

Host that folder behind any static web server (nginx, IIS, Apache, `dotnet serve`, etc.), making sure it is served on **port 5158** so it matches the API CORS configuration and the URLs documented above.

A quick way to serve it for a demo, using the `dotnet serve` global tool:

```bash
dotnet tool install --global dotnet-serve
dotnet serve --directory ./publish/web/wwwroot --port 5158
```

The app is now fully running without any IDE involvement.

---

## 7. Database & Migrations

The application uses **Entity Framework Core** with a hosted **MySQL 8.0** database. The connection string lives in `BioscoopCasus.API/appsettings.json`.

### Migrations run automatically

When the API starts, it automatically applies any pending migrations. You normally do not need to do anything.

### Developer commands (only if you modify entities)

These commands require the EF Core CLI tool. Install it once globally:

```bash
dotnet tool install --global dotnet-ef
```

**Create a new migration** after changing an entity in `BioscoopCasus.API/Entities/`:

```bash
dotnet ef migrations add NameOfYourChange --project BioscoopCasus.API
```

**Apply migrations manually:**

```bash
dotnet ef database update --project BioscoopCasus.API
```

**Remove the last migration** (only if it hasn't been applied yet):

```bash
dotnet ef migrations remove --project BioscoopCasus.API
```

---

## 8. Troubleshooting

**`dotnet: command not found`**
Install the .NET 10 SDK from <https://dotnet.microsoft.com/download> and restart your terminal.

**`dotnet ef` is not recognized**
Install the EF Core CLI tool: `dotnet tool install --global dotnet-ef`.

**Port 5064 or 5158 is already in use**
Another process is using the port. Either stop that process, or change the URL:
`dotnet run --project BioscoopCasus.API --urls "http://localhost:5065"`
(If you change the API port, also update `BioscoopCasus.Web/Program.cs` where the API base address is configured.)

**The frontend loads but no movies appear**
The API is probably not running, or it's running on a different port than the frontend expects. Check that the API is up at <http://localhost:5064> and that both terminals are running.

**Database connection errors on API startup**
The hosted database may be temporarily unreachable. Check your internet connection and that the connection string in `BioscoopCasus.API/appsettings.json` is intact.
