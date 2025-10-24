using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Globalization;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using danserdan.Models;
using danserdan.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.draw;
using PdfRectangle = iTextSharp.text.Rectangle;


namespace danserdan.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDBContext _context;

        public AccountController(ApplicationDBContext context)
        {
            _context = context;
        }

        public IActionResult GenerateCaptcha()
        {
            string captchaText = GenerateCaptchaText();
            HttpContext.Session.SetString("Captcha", captchaText);

            using (Bitmap bitmap = new Bitmap(120, 40))
            using (Graphics g = Graphics.FromImage(bitmap))
            using (MemoryStream ms = new MemoryStream())
            {
                g.Clear(Color.FromArgb(42, 45, 58));
                System.Drawing.Font font = new System.Drawing.Font("Arial", 24, FontStyle.Bold);
                Brush brush = new SolidBrush(Color.White);
                g.DrawString(captchaText, font, brush, 10, 5);

                // Add noise
                Random rand = new Random();
                for (int i = 0; i < 20; i++)
                {
                    g.DrawLine(new Pen(Color.Gray), rand.Next(120), rand.Next(40), rand.Next(120), rand.Next(40));
                }

                bitmap.Save(ms, ImageFormat.Png);
                return File(ms.ToArray(), "image/png");
            }
        }

        private string GenerateCaptchaText()
        {
            string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            Random rand = new Random();
            char[] captchaText = new char[4];
            for (int i = 0; i < captchaText.Length; i++)
            {
                captchaText[i] = characters[rand.Next(characters.Length)];
            }
            return new string(captchaText);
        }

        private bool IsValidPassword(string password)
        {
            return IsPasswordStrong(password);
        }

        [HttpGet]
        public IActionResult Signup()
        {
            return View();
        }

        [HttpPost]

        public async Task<IActionResult> Signup(string firstName, string lastName, string username, string email, string password, string captcha, bool isModal = false)
        {
            try
            {
                if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    ViewBag.Error = "Please fill in all fields.";
                    return isModal ? Json(new { success = false, message = "Please fill in all fields." }) : View();
                }

                if (!IsValidPassword(password))
                {
                    ViewBag.Error = "Password must be at least 8 characters long, contain at least one uppercase letter, one lowercase letter, one number, and one special character.";
                    return isModal ? Json(new { success = false, message = "Password must meet the required criteria." }) : View();
                }

                string sessionCaptcha = HttpContext.Session.GetString("Captcha");
                if (string.IsNullOrEmpty(captcha) || captcha != sessionCaptcha)
                {
                    ViewBag.Error = "Incorrect CAPTCHA. Please try again.";
                    return isModal ? Json(new { success = false, message = "Incorrect CAPTCHA. Please try again." }) : View();
                }

                if (await _context.Users.AnyAsync(u => u.username == username))
                {
                    ViewBag.Error = "Username is already taken.";
                    return isModal ? Json(new { success = false, message = "Username is already taken." }) : View();
                }

                if (await _context.Users.AnyAsync(u => u.email == email))
                {
                    ViewBag.Error = "Email is already taken.";
                    return isModal ? Json(new { success = false, message = "Email is already taken." }) : View();
                }

                var newUser = new Users
                {
                    firstName = firstName,
                    lastName = lastName,
                    username = username,
                    email = email,
                    password_hash = HashPassword(password),
                    balance = null,
                    created_at = DateTime.Now
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetString("UserEmail", newUser.email);
                HttpContext.Session.SetString("Username", newUser.username);

                return isModal ? Json(new { success = true }) : RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Signup: {ex.Message}");
                ViewBag.Error = "An error occurred during signup. Please try again.";
                return isModal ? Json(new { success = false, message = "An error occurred during signup. Please try again." }) : View();
            }
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool isModal = false)
        {
            try
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ViewBag.Error = "Please enter both username and password.";
                    return isModal ? Json(new { success = false, message = "Please enter both username and password." }) : View();
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => (u.email == username || u.username == username) && u.password_hash == HashPassword(password));
                if (user != null)
                {
                    if (string.IsNullOrEmpty(user.firstName)) user.firstName = "User";
                    if (string.IsNullOrEmpty(user.lastName)) user.lastName = "Account";
                    await _context.SaveChangesAsync();

                    HttpContext.Session.SetString("UserEmail", user.email);
                    HttpContext.Session.SetString("Username", user.username);

                    return isModal ? Json(new { success = true }) : RedirectToAction("Index", "Home");
                }

                ViewBag.Error = "Invalid username or password.";
                return isModal ? Json(new { success = false, message = "Invalid username or password." }) : View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Login: {ex.Message}");
                ViewBag.Error = "An error occurred during login. Please try again.";
                return isModal ? Json(new { success = false, message = "An error occurred during login. Please try again." }) : View();
            }
        }

        public async Task<IActionResult> Profile(int page = 1, int pageSize = 5)
        {
            string? userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail)) return RedirectToAction("Login");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.email == userEmail);
            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrEmpty(user.firstName)) user.firstName = "User";
            if (string.IsNullOrEmpty(user.lastName)) user.lastName = "Account";
            await _context.SaveChangesAsync();

            var totalTransactions = await _context.Transactions.CountAsync(t => t.user_id == user.user_id);
            var transactions = await _context.Transactions
                .Where(t => t.user_id == user.user_id)
                .OrderByDescending(t => t.TransactionTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalTrades = totalTransactions;
            var uniqueStocks = await _context.Transactions
                .Where(t => t.user_id == user.user_id)
                .Select(t => t.StockId)
                .Distinct()
                .CountAsync();

            decimal totalInvested = await _context.Transactions
                .Where(t => t.user_id == user.user_id && t.Price > 0)
                .SumAsync(t => t.Price * t.quantity);

            decimal totalSold = await _context.Transactions
                .Where(t => t.user_id == user.user_id && t.Price < 0)
                .SumAsync(t => t.Price * t.quantity);

            decimal totalReturn = totalSold - totalInvested;
            string returnPercentage = totalInvested > 0 ? $"{(totalReturn / totalInvested * 100):F2}%" : "0.00%";

            var stockIds = await _context.Transactions
                .Where(t => t.user_id == user.user_id)
                .Select(t => t.StockId)
                .Distinct()
                .ToListAsync();

            var stockHoldings = new List<StockHolding>();

            foreach (var stockId in stockIds)
            {
                if (!stockId.HasValue)
                {
                    continue;
                }

                var stock = await _context.Stocks.FindAsync(stockId.Value);
                if (stock == null) continue;

                var sharesBought = await _context.Transactions
                    .Where(t => t.user_id == user.user_id && t.StockId == stockId && t.Price > 0)
                    .SumAsync(t => t.quantity);

                var sharesSold = await _context.Transactions
                    .Where(t => t.user_id == user.user_id && t.StockId == stockId && t.Price < 0)
                    .SumAsync(t => t.quantity);

                var sharesOwned = sharesBought - sharesSold;
                if (sharesOwned <= 0) continue;

                var totalSpent = await _context.Transactions
                    .Where(t => t.user_id == user.user_id && t.StockId == stockId && t.Price > 0)
                    .SumAsync(t => t.Price * t.quantity);

                var avgPurchasePrice = totalSpent / sharesBought;
                var profitLoss = (stock.market_price - avgPurchasePrice) * sharesOwned;
                var profitLossPercentage = avgPurchasePrice > 0 ? $"{(stock.market_price - avgPurchasePrice) / avgPurchasePrice * 100:F2}%" : "0.00%";

                stockHoldings.Add(new StockHolding
                {
                    StockId = stockId ?? 0,
                    Symbol = stock.symbol ?? "",
                    CompanyName = stock.company_name ?? "",
                    Quantity = sharesOwned,
                    PurchasePrice = avgPurchasePrice,
                    CurrentPrice = stock.market_price,
                    ProfitLoss = profitLoss,
                    ProfitLossPercentage = profitLossPercentage
                });
            }

            var viewModel = new ProfileViewModel
            {
                User = user,
                Transactions = transactions,
                StockHoldings = stockHoldings,
                TotalTrades = totalTrades,
                UniqueStocks = uniqueStocks,
                TotalReturn = totalReturn,
                ReturnPercentage = returnPercentage,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalTransactions / (double)pageSize),
                PageSize = pageSize
            };

            if (TempData.ContainsKey("SuccessMessage"))
                ViewBag.SuccessMessage = TempData["SuccessMessage"];
            if (TempData.ContainsKey("ErrorMessage"))
                ViewBag.ErrorMessage = TempData["ErrorMessage"];

            return View("~/Views/Account/Profile.cshtml", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadReceipt(int transactionId)
        {
            string? userEmail = HttpContext.Session.GetString("UserEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                return RedirectToAction("Login");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.email == userEmail);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var transaction = await _context.Transactions.FirstOrDefaultAsync(t => t.transaction_id == transactionId && t.user_id == user.user_id);
            if (transaction == null)
            {
                return NotFound();
            }

            Stocks? stock = null;
            if (transaction.StockId.HasValue)
            {
                stock = await _context.Stocks.FirstOrDefaultAsync(s => s.stock_id == transaction.StockId.Value);
            }

            string stockName = stock?.company_name ?? (transaction.StockId.HasValue ? "Unknown Stock" : (transaction.Price >= 0 ? "Add Funds" : "Payout"));
            string transactionType = !transaction.StockId.HasValue
                ? (transaction.Price > 0 ? "Add Funds" : transaction.Price < 0 ? "Payout" : "Funds")
                : (transaction.Price >= 0 ? "Buy" : "Sell");

            var culture = CultureInfo.GetCultureInfo("en-US");
            decimal pricePerUnit = Math.Abs(transaction.Price);
            decimal totalAmount = Math.Abs(transaction.Price * transaction.quantity);
            string priceDisplay = pricePerUnit.ToString("C2", culture);
            string totalDisplay = totalAmount.ToString("C2", culture);
            string transactionDate = transaction.TransactionTime.ToLocalTime().ToString("MMMM dd, yyyy hh:mm tt");

            using var memoryStream = new MemoryStream();
            var document = new Document(PageSize.A4, 36f, 36f, 54f, 36f);
            var writer = PdfWriter.GetInstance(document, memoryStream);
            writer.CloseStream = false;

            document.Open();

            var primaryColor = new BaseColor(124, 58, 237);
            var secondaryColor = new BaseColor(236, 72, 153);
            var darkBackground = new BaseColor(26, 26, 36);
            var cardBackground = new BaseColor(33, 32, 48);
            var cardBorder = new BaseColor(78, 70, 110);
            var rowAlternateBackground = new BaseColor(40, 36, 62);
            var highlightBackground = new BaseColor(58, 36, 90);
            var textPrimary = new BaseColor(245, 245, 247);
            var textSecondary = new BaseColor(210, 210, 230);
            var textMuted = new BaseColor(170, 170, 190);

            var canvas = writer.DirectContentUnder;
            canvas.SaveState();
            canvas.SetColorFill(darkBackground);
            canvas.Rectangle(0, 0, PageSize.A4.Width, PageSize.A4.Height);
            canvas.Fill();
            canvas.RestoreState();

            var companyFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18f, textPrimary);
            var companyDetailsFont = FontFactory.GetFont(FontFactory.HELVETICA, 10f, textSecondary);
            var headerInfoFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12f, textPrimary);
            var sectionHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12f, textPrimary);
            var labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.5f, textSecondary);
            var highlightLabelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11f, textPrimary);
            var valueFont = FontFactory.GetFont(FontFactory.HELVETICA, 11f, textPrimary);
            var totalValueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11f, secondaryColor);
            var noteFont = FontFactory.GetFont(FontFactory.HELVETICA_OBLIQUE, 9f, textMuted);
            var closingFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11f, textPrimary);

            var headerGradient = new GradientCellEvent(writer, primaryColor, secondaryColor);

            var customerName = ($"{user.firstName} {user.lastName}").Trim();
            var displayCustomerName = string.IsNullOrWhiteSpace(customerName) ? user.username : customerName;

            var headerTable = new PdfPTable(2)
            {
                WidthPercentage = 100,
                SpacingAfter = 14f
            };
            headerTable.SetWidths(new float[] { 2f, 1f });

            var companyCell = new PdfPCell
            {
                Border = PdfRectangle.NO_BORDER,
                Padding = 20f
            };
            companyCell.CellEvent = headerGradient;
            var companyParagraph = new Paragraph
            {
                Alignment = Element.ALIGN_LEFT
            };
            companyParagraph.Add(new Phrase("TradeX Inc.\n", companyFont));
            companyParagraph.Add(new Phrase("Project It 15\nUM Davao, Davao City, PH 8000\nsupport@tradex.com | +63 2 555 0101", companyDetailsFont));
            companyCell.AddElement(companyParagraph);

            var receiptInfoCell = new PdfPCell
            {
                Border = PdfRectangle.NO_BORDER,
                Padding = 20f,
                HorizontalAlignment = Element.ALIGN_RIGHT
            };
            receiptInfoCell.CellEvent = headerGradient;
            var receiptTitle = new Paragraph("TRANSACTION RECEIPT", headerInfoFont)
            {
                Alignment = Element.ALIGN_RIGHT
            };
            var receiptDetails = new Paragraph($"Receipt No.: {transaction.transaction_id}\nIssued: {transactionDate}\nTotal Amount: {totalDisplay}", companyDetailsFont)
            {
                Alignment = Element.ALIGN_RIGHT,
                SpacingBefore = 6f
            };
            receiptInfoCell.AddElement(receiptTitle);
            receiptInfoCell.AddElement(receiptDetails);

            headerTable.AddCell(companyCell);
            headerTable.AddCell(receiptInfoCell);
            document.Add(headerTable);

            document.Add(new Paragraph(" "));
            var lineSeparator = new LineSeparator(1.2f, 100f, primaryColor, Element.ALIGN_CENTER, -2f);
            document.Add(new Chunk(lineSeparator));

            var customerTable = new PdfPTable(2)
            {
                WidthPercentage = 100,
                SpacingBefore = 18f,
                SpacingAfter = 8f
            };
            customerTable.SetWidths(new float[] { 1f, 1f });

            var billToCell = new PdfPCell
            {
                Border = PdfRectangle.NO_BORDER,
                Padding = 14f,
                BackgroundColor = cardBackground,
                BorderColorBottom = cardBorder,
                BorderWidthBottom = 0.75f
            };
            billToCell.AddElement(new Paragraph("BILL TO", labelFont));
            billToCell.AddElement(new Paragraph(displayCustomerName, valueFont));
            billToCell.AddElement(new Paragraph(user.email, valueFont));

            var accountDetailsCell = new PdfPCell
            {
                Border = PdfRectangle.NO_BORDER,
                Padding = 14f,
                BackgroundColor = cardBackground,
                BorderColorBottom = cardBorder,
                BorderWidthBottom = 0.75f
            };
            accountDetailsCell.AddElement(new Paragraph("ACCOUNT DETAILS", labelFont));
            accountDetailsCell.AddElement(new Paragraph($"Username: {user.username}", valueFont));
            accountDetailsCell.AddElement(new Paragraph($"Transaction Type: {transactionType}", valueFont));

            customerTable.AddCell(billToCell);
            customerTable.AddCell(accountDetailsCell);
            document.Add(customerTable);

            var summaryTable = new PdfPTable(2)
            {
                WidthPercentage = 100,
                SpacingBefore = 18f,
                SpacingAfter = 10f
            };
            summaryTable.SetWidths(new float[] { 1f, 1.5f });

            var summaryHeader = new PdfPCell(new Phrase("Transaction Summary", sectionHeaderFont))
            {
                Colspan = 2,
                BackgroundColor = primaryColor,
                Border = PdfRectangle.NO_BORDER,
                Padding = 12f
            };
            summaryHeader.CellEvent = headerGradient;
            summaryTable.AddCell(summaryHeader);

            bool alternateRow = false;

            void AddRow(string label, string value, bool emphasize = false)
            {
                var rowBackground = emphasize ? highlightBackground : (alternateRow ? rowAlternateBackground : cardBackground);
                var rowLabelFont = emphasize ? highlightLabelFont : labelFont;
                var rowValueFont = emphasize ? totalValueFont : valueFont;

                var labelCell = new PdfPCell(new Phrase(label, rowLabelFont))
                {
                    Border = PdfRectangle.NO_BORDER,
                    Padding = 10f,
                    BackgroundColor = rowBackground,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    BorderColorBottom = cardBorder,
                    BorderWidthBottom = emphasize ? 1.1f : 0.6f
                };
                labelCell.Border = PdfRectangle.BOTTOM_BORDER;

                var valueCell = new PdfPCell(new Phrase(value, rowValueFont))
                {
                    Border = PdfRectangle.NO_BORDER,
                    Padding = 10f,
                    BackgroundColor = rowBackground,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    BorderColorBottom = cardBorder,
                    BorderWidthBottom = emphasize ? 1.1f : 0.6f
                };
                valueCell.Border = PdfRectangle.BOTTOM_BORDER;

                summaryTable.AddCell(labelCell);
                summaryTable.AddCell(valueCell);

                if (!emphasize)
                {
                    alternateRow = !alternateRow;
                }
            }

            AddRow("Transaction ID", transaction.transaction_id.ToString());
            AddRow("Stock Name", stockName);
            AddRow("Quantity", transaction.quantity.ToString());
            AddRow("Price per Unit", priceDisplay);
            AddRow("Date", transactionDate);
            AddRow("Total Amount", totalDisplay, true);

            document.Add(summaryTable);

            var thanksParagraph = new Paragraph("Thank you for choosing TradeX Inc.", closingFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 18f,
                SpacingAfter = 6f
            };
            document.Add(thanksParagraph);

            var noteParagraph = new Paragraph("Note: Please retain this receipt for your records. For assistance, contact support@tradex.com within 7 days of the transaction.", noteFont)
            {
                Alignment = Element.ALIGN_CENTER,
                SpacingBefore = 4f
            };
            document.Add(noteParagraph);

            document.Close();

            memoryStream.Position = 0;
            var fileName = $"transaction-{transaction.transaction_id}.pdf";
            var pdfBytes = memoryStream.ToArray();

            return File(pdfBytes, "application/pdf", fileName);
        }


        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(string username, string firstName, string lastName, string email, string newPassword)
        {
            try
            {
                var userEmail = HttpContext.Session.GetString("UserEmail");
                if (string.IsNullOrEmpty(userEmail))
                    return Json(new { success = false, message = "You must be logged in to update your profile." });

                var user = await _context.Users.FirstOrDefaultAsync(u => u.email == userEmail);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                if (username != user.username && await _context.Users.AnyAsync(u => u.username == username && u.user_id != user.user_id))
                    return Json(new { success = false, message = "Username is already taken." });

                if (email != user.email && await _context.Users.AnyAsync(u => u.email == email && u.user_id != user.user_id))
                    return Json(new { success = false, message = "Email is already taken." });

                user.username = username;
                user.firstName = firstName;
                user.lastName = lastName;

                if (email != user.email)
                {
                    user.email = email;
                    HttpContext.Session.SetString("UserEmail", email);
                }

                if (!string.IsNullOrEmpty(newPassword))
                {
                    if (!IsPasswordStrong(newPassword))
                    {
                        return Json(new
                        {
                            success = false,
                            message = "Password must be at least 8 characters long and include an uppercase letter, lowercase letter, number,  and special character."
                        });
                    }

                    user.password_hash = HashPassword(newPassword);
                }

                await _context.SaveChangesAsync();
                HttpContext.Session.SetString("Username", username);

                return Json(new
                {
                    success = true,
                    message = "Profile updated successfully.",
                    username = user.username,
                    email = user.email
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateProfile: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while updating your profile. Please try again." });
            }
        }

        private bool IsPasswordStrong(string password)
        {
            var hasUpperCase = Regex.IsMatch(password, "[A-Z]");
            var hasLowerCase = Regex.IsMatch(password, "[a-z]");
            var hasDigit = Regex.IsMatch(password, "[0-9]");
            var hasSpecialChar = Regex.IsMatch(password, "[!@#$%^&*(),.?\":{}|<>]");

            return hasUpperCase && hasLowerCase && hasDigit && hasSpecialChar;
        }

        private class GradientCellEvent : IPdfPCellEvent
        {
            private readonly PdfWriter _writer;
            private readonly BaseColor _startColor;
            private readonly BaseColor _endColor;

            public GradientCellEvent(PdfWriter writer, BaseColor startColor, BaseColor endColor)
            {
                _writer = writer;
                _startColor = startColor;
                _endColor = endColor;
            }

            public void CellLayout(PdfPCell cell, PdfRectangle position, PdfContentByte[] canvases)
            {
                var background = canvases[PdfPTable.BACKGROUNDCANVAS];
                var shading = PdfShading.SimpleAxial(_writer, position.Left, position.Bottom, position.Right, position.Top, _startColor, _endColor);
                var pattern = new PdfShadingPattern(shading);

                background.SaveState();
                background.SetShadingFill(pattern);
                background.Rectangle(position.Left, position.Bottom, position.Width, position.Height);
                background.Fill();
                background.RestoreState();
            }
        }

        [HttpPost]
     
        private string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var hashBytes = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
