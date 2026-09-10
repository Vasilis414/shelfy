using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shelfy.Data;
using Shelfy.Models;
using Shelfy.Services;

namespace Shelfy.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context; //get database connection
    }

    private bool IsAdmin()
    {
        var adminLevel = HttpContext.Session.GetInt32("AdminLevel");
        return adminLevel == 1 || adminLevel == 2;
    }


    // website checks
    private bool IsSuperAdmin()
    {
        var adminLevel = HttpContext.Session.GetInt32("AdminLevel");
        return adminLevel == 2;
    }

    private int CurrentUserId => HttpContext.Session.GetInt32("UserId") ?? 0;

    private static int CalculateBorrowStatus(DateTime? expectedReturnDate, DateTime? actualReturnDate)
    {

        if (actualReturnDate.HasValue)
        {
            if (expectedReturnDate.HasValue && actualReturnDate.Value.Date > expectedReturnDate.Value.Date)
            {
                return 3;
            }
            return 1;
        }

        if (expectedReturnDate.HasValue && expectedReturnDate.Value.Date < DateTime.Today)
        {
            return 2;
        }
        return 0;
    }

    private static bool IsOpenBorrow(Borrow borrow)
    {
        return borrow.Status == 0 || borrow.Status == 2;
    }




//here the backend for the pages start
    public async Task<IActionResult> Index()
    {
        var userName = HttpContext.Session.GetString("UserName");
        if (string.IsNullOrEmpty(userName))
        {
            return RedirectToAction("Login", "Account");
        }

        if (!IsAdmin())
        {
            return RedirectToAction("OPAC");
        }

        ViewBag.UserName = userName;

        var books = await _context.GetAllBooksAsync();
        var users = await _context.GetAllUsersAsync();
        var borrows = await _context.GetAllBorrowsAsync();
        var overdueBooks = await _context.GetOverdueBorrowsAsync();

        foreach (var book in books)
        {
            book.Available = book.Quantity - borrows.Count(
                b => b.BookId == book.BookId && IsOpenBorrow(b)
                );
        }

        var model = new DashboardViewModel
        {
            TotalBooks = books.Count,
            TotalMembers = users.Count,
            TotalBorrows = borrows.Count,
            ActiveBorrows = borrows.Count(b => b.Status == 0),
            OverdueBorrows = borrows.Count(b => b.Status == 2),
            OverdueBorrowRecords = overdueBooks
        };

        return View(model);
    }

    public async Task<IActionResult> Books()
    {
        if (!IsAdmin())
        {
            return RedirectToAction("OPAC");

        }

        var books = await _context.GetAllBooksAsync();
        var borrows = await _context.GetAllBorrowsAsync();

        ViewBag.FineRules = await _context.GetAllFineRulesAsync();

        foreach (var book in books)
        {
            book.Available = book.Quantity - borrows.Count(b => b.BookId == book.BookId && IsOpenBorrow(b));
        }

        return View(books);
    }

    public async Task<IActionResult> FineRules()
    {
        if (!IsAdmin())

            {
            return RedirectToAction("OPAC");
        }

        var rules = await _context.GetAllFineRulesAsync();
        return View(rules);
    }

    [HttpPost]
    public async Task<IActionResult> AddFineRule(string title, float fee, int interval)
    {
        if (!IsSuperAdmin())
        {
            return Json(new { 
            success = false, 
                message = "Only Super Admins can manage rules." 
                });
        }

        var success = await _context.InsertFineRuleAsync(title, fee, interval);
        return Json(new { 
            success, 
            message = success ? "Rule added" : "Failed to add rule" 
            
        });
    }
    [HttpPost]
    public async Task<IActionResult> EditFineRule(int ruleId, string title, float fee, int interval)
    {
        if (!IsSuperAdmin())
        {
            return Json(new { success = false, message = "Only Super Admins can manage rules." });
        }

        var success = await _context.UpdateFineRuleAsync(ruleId, title, fee, interval);
        return Json(new { success, message = success ? "Rule updated" : "Failed to update rule" });
    }


    public async Task<IActionResult> Borrows()
    {
        if (!IsAdmin())
        {
            return RedirectToAction("OPAC");
        }

        var borrows = await _context.GetAllBorrowsAsync();
        ViewBag.FineRules = await _context.GetAllFineRulesAsync();
        return View(borrows);
    }

    public async Task<IActionResult> OPAC()
    {
        if (IsAdmin())
        {
            return RedirectToAction("Index");
        }

        var userId = CurrentUserId;
        if (userId == 0)
        {
            return RedirectToAction("Login", "Account");
        }

        var books = await _context.GetAllBooksAsync();
        var borrows = await _context.GetAllBorrowsAsync();
        ViewBag.TotalFines = await _context.GetUserFinesAsync(userId, 0);

        foreach (var book in books)
        {
            book.Available = book.Quantity - borrows.Count(
                b => b.BookId == book.BookId && IsOpenBorrow(b));




            book.Notes = null;

            var media = await _context.GetMediaByBookIdAsync(book.BookId);
            if (media != null)
            {
                book.Media.Add(media);
            }
        }

        var model = new OpacViewModel
        {
            Books = books,
            BorrowHistory = borrows.Where(b => b.UserId == userId).OrderByDescending(b => b.BorrowedAt).ToList()
        };

        return View(model);
    }






    [HttpPost]
    public async Task<IActionResult> AddBook(string isbn, string title, string author, string publisher, int quantity, string? description, string? notes, int bookFineRule, IFormFile coverFile, string coverUrl)
    {
        if (!IsAdmin())
        {
            return Json(new { 
                success = false, 
                message = "Unauthorized" 
            });
        }

        var book = new Book
        {
            Isbn = isbn,
            Title = title,
            Author = author,
            Publisher = publisher,
            Quantity = quantity,
            Description = description,
            Notes = notes,
            BookFineRule = bookFineRule
        };

        var (success, errorMessage) = await _context.InsertBookAsync(book);
        if (success)
        {
            var books = await _context.GetAllBooksAsync();
            var newBook = books.OrderByDescending(b => b.BookId).FirstOrDefault();

            if (newBook != null)
            {
                string? mediaPath = null;



                if (coverFile != null && coverFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var fileExtension = Path.GetExtension(coverFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return Json(new { success = false, message = "Invalid file type. Allowed: JPG, PNG, GIF" });
                 }

                    if (coverFile.Length > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "File size exceeds 5MB limit" });
                    }

                    var uploadsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "books");
                    if (!Directory.Exists(uploadsDirectory))
                    {
                        Directory.CreateDirectory(uploadsDirectory);}

                    var fileName = $"{newBook.BookId}_{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsDirectory, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await coverFile.CopyToAsync(stream);
                    }

                    mediaPath = $"/uploads/books/{fileName}";
                }
                else if (!string.IsNullOrEmpty(coverUrl))
                {
                    if (!coverUrl.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) &&
                        !coverUrl.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                        !coverUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                        !coverUrl.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    {
                        return Json(new { success = false, message = "URL must end with .jpg, .jpeg, .png, or .gif" });
                    }

                    mediaPath = coverUrl;
                }

                if (!string.IsNullOrEmpty(mediaPath))
                {
                    var media = new Media
                    {
                        BookId = newBook.BookId,
                        Path = mediaPath
                    };

                    await _context.InsertMediaAsync(media);
                }
            }

            return Json(new { success = true, message = "Book added successfully" });
        }

        return Json(new { success = false, message = "Failed to add book", error = errorMessage });
    }

    [HttpPost]
    public async Task<IActionResult> AddBorrow(int userId, int bookId, int status, string? borrowedAt, string? borrowReturnDate, string? actualReturn, int borrowRule)
    {
        if (!IsAdmin())
        {
            return Json(new { success = false, message = "Unauthorized" });
        }
        var fines = await _context.GetUserFinesAsync(userId, 0);
        if (fines > 0)
        {
            return Json(new { success = false, message = "The user has active fines, thus he can not borrow any books until they are paid." });
        }


        var parsedBorrowedAt = DateTime.TryParse(borrowedAt, out var borrowDate) ? borrowDate : DateTime.Now;
        var parsedReturnDate = DateTime.TryParse(borrowReturnDate, out var returnDate) ? returnDate : (DateTime?)null;
        var parsedActualReturn = DateTime.TryParse(actualReturn, out var actualReturnDate) ? actualReturnDate : (DateTime?)null;

        var borrow = new Borrow
        {
            UserId = userId,
            BookId = bookId,
            Status = CalculateBorrowStatus(parsedReturnDate, parsedActualReturn),
            BorrowedAt = parsedBorrowedAt,
            BorrowReturnDate = parsedReturnDate,
            ActualReturn = parsedActualReturn,
            FineCalculatedUntil = parsedReturnDate,
            BorrowRule = borrowRule
        };

        var result = await _context.InsertBorrowAsync(borrow);
        if (result)
        {
            return Json(new { success = true, message = "Borrow record added successfully" });
        }
        return Json(new { success = false, message = "Failed to add borrow record" });
    }

    [HttpPost]
    public async Task<IActionResult> EditBorrow(int borrowId, int userId, int bookId, int status, string? borrowedAt, string? borrowReturnDate, string? actualReturn, int borrowRule)
    {
        if (!IsAdmin())
        {
            return Json(new { success = false, message = "Unauthorized" });
        }

        var existingBorrow = await _context.GetBorrowByIdAsync(borrowId);
        if (existingBorrow == null)
        {
            return Json(new { 
                success = false, 
        message = "Borrow record not found" });
        }





        var parsedBorrowedAt = DateTime.TryParse(borrowedAt, out var borrowDate) ? borrowDate : existingBorrow.BorrowedAt;
        var parsedReturnDate = DateTime.TryParse(borrowReturnDate, out var returnDate) ? returnDate : (DateTime?)null;
        var parsedActualReturn = DateTime.TryParse(actualReturn, out var actualReturnDate) ? actualReturnDate : (DateTime?)null;



        var borrow = new Borrow
        {
            BorrowId = borrowId,
            UserId = userId,
            BookId = bookId,
            Status = CalculateBorrowStatus(parsedReturnDate, parsedActualReturn),
            BorrowedAt = parsedBorrowedAt,
            BorrowReturnDate = parsedReturnDate,
            ActualReturn = parsedActualReturn,
            FineCalculatedUntil = existingBorrow.FineCalculatedUntil,
            BorrowRule = borrowRule
        };


        var success = await _context.UpdateBorrowAsync(borrow);

        return Json(new { success, message = success ? "Borrow updated successfully" : "Failed to update borrow record" });
}

    [HttpPost]
    public async Task<IActionResult> ReturnBorrow(int borrowId)
    {
        if (!IsAdmin())
        {
            return Json(
                new { 
                success = false, message = "Unauthorized" }
            );
        }


        var fines = await _context.GetUserFinesAsync(CurrentUserId, borrowId);

        Console.WriteLine($"Fines: {fines}");

        if (fines > 0)
        {
            return Json(new
            {
                success = false,
            message = "The user has active fines in the particular book, thus he can not return the book until the fines are paid."
            });
        }

        var borrow = await _context.GetBorrowByIdAsync(borrowId);
        
        if (borrow == null)
        {
            return Json(new { 
                success = false, 
                message = "Borrow record not found" });
        }

        if (borrow.ActualReturn.HasValue)
        {
            return Json(new { 
                success = false, 
            message = "This borrow has already been returned" });
        }

        borrow.ActualReturn = DateTime.Today;
        borrow.Status = CalculateBorrowStatus(borrow.BorrowReturnDate, borrow.ActualReturn);

        var success = await _context.UpdateBorrowAsync(borrow);
        return Json(new { 
            success, 
        message = success ? "Book returned successfully" : "Failed to return book" 
        });
    }

    [HttpGet]
    public async Task<IActionResult> SearchBorrow(int? borrowId, int? userId, int? bookId)
    {
        if (!IsAdmin())
        {
            return Json(new { 
                success = false, 
                message = "Unauthorized" 
            });
        }

        var borrows = await _context.GetAllBorrowsAsync();
        IEnumerable<Borrow> matches = borrows;

        if (borrowId.HasValue)
        {
            matches = matches.Where(b => b.BorrowId == borrowId.Value);
        }
        if (userId.HasValue)
        {
            matches = matches.Where(b => b.UserId == userId.Value);
        }
        if (bookId.HasValue)
        {
            matches = matches.Where(b => b.BookId == bookId.Value);
        }

        var results = matches.Select(b => new
        {
            b.BorrowId,
            b.UserId,
            b.BookId,
            b.Status,
            StatusText = b.StatusText,
            BorrowedAt = b.BorrowedAt.ToString("yyyy-MM-dd"),
            BorrowReturnDate = b.BorrowReturnDate?.ToString("yyyy-MM-dd"),
            ActualReturn = b.ActualReturn?.ToString("yyyy-MM-dd")
        }
        ).ToList();

        return Json(new { 
            
            success = true, 
            results 
        });
    }



    [HttpPost]
    public async Task<IActionResult> EditBook(int bookId, string isbn, string title, string author, string publisher, int quantity, string? description, string? notes, int bookFineRule)
    {
        if (!IsAdmin() )
        {
            return Json(new { 
                success = false, 
                message = "Unauthorized"});
        }

        var book = new Book
        {
            BookId = bookId,
            Isbn = isbn,
            Title = title,
            Author = author,
            Publisher = publisher,
            Quantity = quantity,
            Description = description,
            Notes = notes,
            BookFineRule = bookFineRule
        };

        var success = await _context.UpdateBookAsync(book);
        return Json(new { 
            success, 
            message = success ? "Book updated successfully" : "Failed to update book" 
        });
    }

    [HttpPost]
    public async Task<IActionResult> EditMember(int userId, string username, string email, int admin)
    {
        var currentAdmin = HttpContext.Session.GetInt32("AdminLevel");
        if (currentAdmin != 1 && currentAdmin != 2)
        {
            return Json(new { success = false, message = "Unauthorized" });
        }

        if (admin < 0 || admin > 2)
        {
            return Json(new { success = false, message = "Invalid admin level" });
        }

        var existingUser = await _context.GetUserByIdAsync(userId);
        if (existingUser == null)
        {
            return Json(new { success = false, message = "Member not found" });
        }

        if (currentAdmin == 1 && existingUser.Admin > 0)
        {
            return Json(new { success = false, message = "Regular admins cannot edit admin accounts" });
        }

        if (currentAdmin == 1 && admin > 0)
        {
            return Json(new { success = false, message = "You can only promote to basic members" });
        }

        var user = new User
        {
            UserId = userId,
            Username = username,
            Email = email,
            Admin = admin
        };

        var success = await _context.UpdateUserAsync(user);
        return Json(new { success, message = success ? "Member updated successfully" : "Failed to update member" });
    }

    public async Task<IActionResult> Members()
    {
        var adminLevel = HttpContext.Session.GetInt32("AdminLevel");
        if (adminLevel != 1 && adminLevel != 2)
        {
            return RedirectToAction("Index");
        }

        var users = await _context.GetAllUsersAsync();
        return View(users);
    }

    [HttpPost]
    public async Task<IActionResult> AddMember(string username, string email, string password, int admin)
    {
        var adminLevel = HttpContext.Session.GetInt32("AdminLevel");
        if (adminLevel != 1 && adminLevel != 2)
        {
            return Json(new { 
                success = false, 

                message = "Unauthorized" 
            });
        }

        if (adminLevel == 1 && admin > 0)
        {
            return Json(new { 
                success = false, 
            message = "You can only create members" });
        }

        var hashedPassword = HashingAlgorithm.ComputeSha256(password);
        var result = await _context.RegisterUserAsync(username, email, hashedPassword, admin);


        if (result)

        {
            return Json(new { 
                success = true,
            message = "Member added successfully" 
        });
        }
        return Json(new 
        {success = false, 
        message = "Failed to add member" });
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { 
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    public IActionResult Loading()
    {
        return View();
    }
}
