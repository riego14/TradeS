# TradeS (Project IT 15)

A modern-themed stock trading web application built with ASP.NET Core MVC. TradeS simulates a trading platform where users can manage their portfolio, execute trades, add or withdraw funds, and download stylized PDF receipts. The system integrates third-party services for live market data and payment processing while providing administrative tooling for managing users, stocks, and transactions.

## Key Features

- **Account & Security**
  - Email-based sign up/login with captcha verification and password strength checks.
  - Session-backed authentication with configurable timeouts.
- **Portfolio & Trading**
  - Dashboard with real-time-inspired market data supplied through the AlphaVantage API and cached via `IMemoryCache`.
  - Background `StockPriceUpdateService` to keep simulated prices fresh.
  - Buy/Sell workflows plus balance management (add funds / cash out).
- **Payments & Receipts**
  - Stripe-powered add-funds experience using test keys (replace with production keys before launch).
  - Themed PDF transaction receipts generated with iTextSharp and downloadable from the profile page.
- **Administration**
  - Management views for users, transactions, and stock listings.
  - Tools to audit activity and ensure platform consistency.
- **UI/UX**
  - Customized dark theme defined in `wwwroot/css/modern-theme.css` with gradient accents and responsive layouts.

## Technology Stack

- **Framework**: ASP.NET Core 8 (MVC pattern)
- **Language**: C#
- **Data Access**: Entity Framework Core 8 with SQL Server backend
- **Services**:
  - AlphaVantage API (market data)
  - Stripe.NET (payments)
  - iTextSharp.LGPLv2.Core (PDF generation)
- **Infrastructure**: Hosted services, memory caching, session state, dependency injection
- **Frontend**: Razor views, Bootstrap, and custom CSS theme assets

## Project Structure

```
TradeS/
├── danserdan/
│   ├── Controllers/           # MVC controllers (Account, Home, Payment, Admin, etc.)
│   ├── Models/                # EF Core models and view models
│   ├── Services/              # AlphaVantage, Stripe, Currency, background services
│   ├── Views/                 # Razor UI for user and admin flows
│   ├── wwwroot/               # Static assets including the modern theme
│   ├── Migrations/            # Entity Framework Core migration history
│   ├── appsettings*.json      # Environment-specific configuration
│   └── Program.cs             # App bootstrapping and middleware pipeline
└── README.md
```

## Getting Started

### Prerequisites

- [.NET SDK 8.0](https://dotnet.microsoft.com/en-us/download)
- SQL Server instance (local or hosted). The sample connection string targets an Azure SQL database.
- AlphaVantage API key (free tier available).
- Stripe test keys for payment flows.

### Configuration

1. Copy `appsettings.json` to `appsettings.Development.json` (if not already present).
2. Update connection strings under `ConnectionStrings:DefaultConnection`.
3. Provide secrets:
   ```json
   "AlphaVantage": {
     "ApiKey": "YOUR_ALPHA_VANTAGE_KEY"
   },
   "Stripe": {
     "SecretKey": "sk_test_xxx",
     "PublicKey": "pk_test_xxx"
   }
   ```
4. For production environments, store sensitive values using [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) or a secrets manager rather than committing them to source control.

### Database Setup

If you need to initialize the database schema locally:

```bash
dotnet tool install --global dotnet-ef  # if not installed
dotnet ef database update
```

This runs the EF Core migrations contained in the `Migrations` folder.

### Run the Application

From the project directory (`danserdan/danserdan/danserdan4/danserdan`):

```bash
dotnet restore
dotnet run
```

The site will bind to the configured Kestrel port (typically `https://localhost:5001` or `https://localhost:7214`).

### Stripe Webhooks (Optional)

For end-to-end payment testing, configure Stripe CLI/webhooks to forward events to your local environment. Update controller logic if you introduce webhook handling.

## Usage Tips

- **Trading & Receipts**: After completing a transaction, visit the profile page and use the **Download Receipt** button to generate a PDF themed to match the web UI.
- **Admin Tools**: Accessible through `/Admin/Index` for users with the proper role assignment.
- **Caching Behavior**: Market data requests are cached to reduce API usage; adjust cache durations in the `AlphaVantageService` as needed.
- **Session Timeout**: Default session timeout is 30 minutes. Modify in `Program.cs` to fit deployment needs.

## Customization

- Modify `wwwroot/css/modern-theme.css` to tweak colors, gradients, and typography.
- Update `Controllers/AccountController.cs` for PDF styling changes—gradient helpers, color palette, and branding text are all centralized in the `DownloadReceipt` action.
- Replace placeholder company details in the receipt header with your organization’s information.

## Known Considerations

- Ensure secrets in `appsettings.json` are removed before committing to public repositories.
- Production deployments should offload session state to a distributed cache (e.g., Redis) and replace the simulated stock updater with live feeds.

## License

This project currently does not specify a license. Add one (e.g., MIT, Apache 2.0) if you plan to open-source or distribute the application.
