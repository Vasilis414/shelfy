using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Shelfy.Models;

namespace Shelfy.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly string _connectionString;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IConfiguration configuration
        ) : base(options)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string not found");
        }

        public DbSet<User> Accounts => Set<User>();
        public DbSet<Book> Books => Set<Book>();
        public DbSet<Borrow> Borrows => Set<Borrow>();
        public DbSet<Media> Media => Set<Media>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>().ToTable("Accounts").HasKey(u => u.UserId);
            modelBuilder.Entity<User>().Property(u => u.UserId).ValueGeneratedOnAdd();

            modelBuilder.Entity<Book>().HasKey(b => b.BookId);
            modelBuilder.Entity<Book>().Property(b => b.BookId).ValueGeneratedOnAdd();

            modelBuilder.Entity<Borrow>().HasKey(b => b.BorrowId);
            modelBuilder.Entity<Borrow>().Property(b => b.BorrowId).ValueGeneratedOnAdd();

            modelBuilder
                .Entity<Borrow>()
                .HasOne(b => b.User)
                .WithMany(u => u.Borrows)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<Borrow>()
                .HasOne(b => b.Book)
                .WithMany(bk => bk.Borrows)
                .HasForeignKey(b => b.BookId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Media>().HasKey(m => m.MediaId);
            modelBuilder.Entity<Media>().Property(m => m.MediaId).ValueGeneratedOnAdd();

            modelBuilder
                .Entity<Media>()
                .HasOne(m => m.Book)
                .WithMany(b => b.Media)
                .HasForeignKey(m => m.BookId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public async Task<decimal> GetUserFinesAsync(int userId, int borrowId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                if (borrowId > 0)
                {
                    var sql = "SELECT SUM(fine) FROM Borrows WHERE borrowid = @BorrowId";
                    using var command = new MySqlCommand(sql, connection);
                    command.Parameters.AddWithValue("@BorrowId", borrowId);
                    var result = await command.ExecuteScalarAsync();
                    return result == DBNull.Value || result == null ? 0 : Convert.ToDecimal(result);
                }

                var userSql = "SELECT SUM(fine) FROM Borrows WHERE userid = @UserId";
                using var userCommand = new MySqlCommand(userSql, connection);
                userCommand.Parameters.AddWithValue("@UserId", userId);
                var userResult = await userCommand.ExecuteScalarAsync();
                return userResult == DBNull.Value || userResult == null
                  ? 0
                  : Convert.ToDecimal(userResult);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve fines for user {userId}", ex);
            }
        }

        public async Task<bool> RegisterUserAsync(
            string username,
            string email,
            string password,
            int admin
        )
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var checkSql = "SELECT COUNT(*) FROM Accounts WHERE email = @Email";
                using var checkCmd = new MySqlCommand(checkSql, connection);
                checkCmd.Parameters.AddWithValue("@Email", email);
                var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

                if (count > 0)
                    return false;

                var sql =
                    @"INSERT INTO Accounts (username, email, password, admin) 
                            VALUES (@UserName, @Email, @Password, @Admin)";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserName", username);
                command.Parameters.AddWithValue("@Email", email);
                command.Parameters.AddWithValue("@Password", password);
                command.Parameters.AddWithValue("@Admin", admin);

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<User?> LoginUserAsync(string email, string password)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT userid, username, email, password, admin, TWO_FACTOR_AUTH FROM Accounts WHERE email = @Email AND password = @Password";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Email", email);
                command.Parameters.AddWithValue("@Password", password);

                using var reader = await command.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SingleRow
                );
                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        UserId = reader.GetInt32("userid"),
                        Username = reader.GetString("username"),
                        Email = reader.GetString("email"),
                        Password = reader.GetString("password"),
                        Admin = reader.GetInt32("admin"),
                        TwoFactorAuth = reader.IsDBNull(reader.GetOrdinal("TWO_FACTOR_AUTH"))
                            ? false
                            : reader.GetBoolean("TWO_FACTOR_AUTH")
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return null;
            }
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT userid, username, email, password, admin, TWO_FACTOR_AUTH FROM Accounts WHERE email = @Email";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Email", email);

                using var reader = await command.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SingleRow
                );
                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        UserId = reader.GetInt32("userid"),
                        Username = reader.GetString("username"),
                        Email = reader.GetString("email"),
                        Password = reader.GetString("password"),
                        Admin = reader.GetInt32("admin"),
                        TwoFactorAuth = reader.IsDBNull(reader.GetOrdinal("TWO_FACTOR_AUTH"))
                            ? false
                            : reader.GetBoolean("TWO_FACTOR_AUTH")
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetUserByEmailAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT userid, username, email, password, admin, TWO_FACTOR_AUTH FROM Accounts WHERE userid = @UserId";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", userId);

                using var reader = await command.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SingleRow
                );
                if (await reader.ReadAsync())
                {
                    return new User
                    {
                        UserId = reader.GetInt32("userid"),
                        Username = reader.GetString("username"),
                        Email = reader.GetString("email"),
                        Password = reader.GetString("password"),
                        Admin = reader.GetInt32("admin"),
                        TwoFactorAuth = reader.IsDBNull(reader.GetOrdinal("TWO_FACTOR_AUTH"))
                            ? false
                            : reader.GetBoolean("TWO_FACTOR_AUTH")
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetUserByIdAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> ToggleTwoFactorAsync(int userId, bool enabled)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "UPDATE Accounts SET TWO_FACTOR_AUTH = @Enabled WHERE userid = @UserId";
                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Enabled", enabled);
                command.Parameters.AddWithValue("@UserId", userId);

                return await command.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ToggleTwoFactorAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ClearUserFinesAsync(int userId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "UPDATE Borrows SET fine = 0, FineCalculatedUntil = CURDATE() WHERE userid = @UserId";
                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", userId);

                return await command.ExecuteNonQueryAsync() >= 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ClearUserFinesAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdatePasswordAsync(int userId, string password)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "UPDATE Accounts SET password = @Password WHERE userid = @UserId";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Password", password);
                command.Parameters.AddWithValue("@UserId", userId);

                return await command.ExecuteNonQueryAsync() > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdatePasswordAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> InsertBookAsync(Book book)
        {
            try
            {
                
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"INSERT INTO Books (isbn, title, author, publisher, quantity, description, notes, bookFineRule, created_at) 
                            VALUES (@Isbn, @Title, @Author, @Publisher, @Quantity, @Description, @Notes, @BookFineRule, NOW())";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Isbn", (object?)book.Isbn ?? DBNull.Value);
                command.Parameters.AddWithValue("@Title", book.Title);
                command.Parameters.AddWithValue("@Author", book.Author);
                command.Parameters.AddWithValue(
                    "@Publisher",
                    (object?)book.Publisher ?? DBNull.Value
                );
                command.Parameters.AddWithValue("@Quantity", book.Quantity);
                command.Parameters.AddWithValue(
                    "@Description",
                    (object?)book.Description ?? DBNull.Value
                );
                command.Parameters.AddWithValue("@Notes", (object?)book.Notes ?? DBNull.Value);
                command.Parameters.AddWithValue("@BookFineRule", book.BookFineRule);




                await command.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InsertBookAsync error: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public async Task<bool> InsertBorrowAsync(Borrow borrow)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"INSERT INTO Borrows (userid, bookid, status, fine, borrowRule, borrowed_at, borrow_returndate, actualreturn) 
                            VALUES (@UserId, @BookId, @Status, 0, @BorrowRule, @BorrowedAt, @BorrowReturnDate, @ActualReturn)";



                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", borrow.UserId);
                command.Parameters.AddWithValue("@BookId", borrow.BookId);
                command.Parameters.AddWithValue("@Status", borrow.Status);
                command.Parameters.AddWithValue("@BorrowedAt", borrow.BorrowedAt);
                command.Parameters.AddWithValue(
                    "@BorrowReturnDate",
                    borrow.BorrowReturnDate.HasValue
                      ? (object)borrow.BorrowReturnDate.Value
                      : DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@ActualReturn",
                    borrow.ActualReturn.HasValue ? (object)borrow.ActualReturn.Value : DBNull.Value
                );
                command.Parameters.AddWithValue("@BorrowRule", borrow.BorrowRule);




                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task RefreshBorrowStatusesAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"
                UPDATE Borrows b
                LEFT JOIN FineRules f ON b.borrowRule = f.ruleid
                SET
                    b.status = CASE
                        WHEN b.actualreturn IS NOT NULL AND b.borrow_returndate IS NOT NULL AND DATE(b.actualreturn) > DATE(b.borrow_returndate) THEN 3
                        WHEN b.actualreturn IS NOT NULL THEN 1
                        WHEN b.borrow_returndate IS NOT NULL AND DATE(b.borrow_returndate) < CURDATE() THEN 2
                        ELSE 0
                    END,
                    b.fine = CASE
                        WHEN b.actualreturn IS NOT NULL AND b.borrow_returndate IS NOT NULL AND DATE(b.actualreturn) > DATE(b.borrow_returndate) THEN
                            FLOOR(DATEDIFF(b.actualreturn, b.borrow_returndate) / IFNULL(f.fee_interval, 1)) * IFNULL(f.fee, 0)
                        WHEN b.actualreturn IS NULL AND b.borrow_returndate IS NOT NULL AND DATE(b.borrow_returndate) < CURDATE() THEN
                            b.fine + (
                                CASE
                                    WHEN b.FineCalculatedUntil IS NOT NULL AND DATE(CURDATE()) > DATE(b.FineCalculatedUntil) THEN
                                        FLOOR(DATEDIFF(CURDATE(), b.FineCalculatedUntil) / IFNULL(f.fee_interval, 1)) * IFNULL(f.fee, 0)
                                    WHEN b.FineCalculatedUntil IS NULL AND DATE(b.borrow_returndate) < CURDATE() THEN
                                        FLOOR(DATEDIFF(CURDATE(), b.borrow_returndate) / IFNULL(f.fee_interval, 1)) * IFNULL(f.fee, 0)
                                    ELSE 0
                                END
                            )
                        ELSE b.fine
                    END,
                    b.FineCalculatedUntil = CASE
                        WHEN b.actualreturn IS NOT NULL AND b.borrow_returndate IS NOT NULL AND DATE(b.actualreturn) > DATE(b.borrow_returndate) THEN
                            b.actualreturn
                        WHEN b.actualreturn IS NULL AND b.borrow_returndate IS NOT NULL AND DATE(b.borrow_returndate) < CURDATE() THEN
                            CURDATE()
                        ELSE b.FineCalculatedUntil
                    END
                WHERE
                    b.status IN (0, 2)
                    OR (b.status = 3 AND b.actualreturn IS NOT NULL AND DATE(b.actualreturn) > DATE(b.borrow_returndate));
                ";

                using var command = new MySqlCommand(sql, connection);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RefreshBorrowStatusesAsync error: {ex.Message}");
            }
        }

        public async Task<List<Borrow>> GetAllBorrowsAsync()
        {
            try
            {
                await RefreshBorrowStatusesAsync();

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT borrowid, userid, bookid, status, borrowed_at, borrow_returndate, actualreturn, borrowRule, FineCalculatedUntil FROM Borrows ORDER BY borrowid DESC";

                using var command = new MySqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var borrows = new List<Borrow>();
                while (await reader.ReadAsync())
                {
                    var returnDateOrdinal = reader.GetOrdinal("borrow_returndate");
                    var actualReturnOrdinal = reader.GetOrdinal("actualreturn");
                    var fineCalculatedUntilOrdinal = reader.GetOrdinal("FineCalculatedUntil");

                    borrows.Add(
                        new Borrow
                        {
                            BorrowId = reader.GetInt32("borrowid"),
                            UserId = reader.GetInt32("userid"),
                            BookId = reader.GetInt32("bookid"),
                            Status = reader.GetInt32("status"),
                            BorrowedAt = reader.GetDateTime("borrowed_at"),
                            BorrowReturnDate = reader.IsDBNull(returnDateOrdinal)
                                ? null
                                : reader.GetDateTime(returnDateOrdinal),
                            ActualReturn = reader.IsDBNull(actualReturnOrdinal)
                                ? null
                                : reader.GetDateTime(actualReturnOrdinal),
                            FineCalculatedUntil = reader.IsDBNull(fineCalculatedUntilOrdinal)
                                ? null
                                : reader.GetDateTime(fineCalculatedUntilOrdinal),
                            BorrowRule = reader.GetInt32("borrowRule")
                        }
                    );
                }
                return borrows;
            }
            catch
            {
                return new List<Borrow>();
            }
        }

        public async Task<Borrow?> GetBorrowByIdAsync(int borrowId)
        {
            try
            {


                await RefreshBorrowStatusesAsync();

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT borrowid, userid, bookid, status, borrowed_at, borrow_returndate, actualreturn, borrowRule, FineCalculatedUntil FROM Borrows WHERE borrowid = @BorrowId";


                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@BorrowId", borrowId);

                using var reader = await command.ExecuteReaderAsync(
                    System.Data.CommandBehavior.SingleRow
                );


                if (await reader.ReadAsync())
                {
                    var returnDateOrdinal = reader.GetOrdinal("borrow_returndate");
                    var actualReturnOrdinal = reader.GetOrdinal("actualreturn");
                    var fineCalculatedUntilOrdinal = reader.GetOrdinal("FineCalculatedUntil");

                    return new Borrow
                    {

                        BorrowId = reader.GetInt32("borrowid"),
                        UserId = reader.GetInt32("userid"),
                        BookId = reader.GetInt32("bookid"),
                        Status = reader.GetInt32("status"),
                        BorrowedAt = reader.GetDateTime("borrowed_at"),
                        BorrowReturnDate = reader.IsDBNull(returnDateOrdinal)
                            ? null
                            : reader.GetDateTime(returnDateOrdinal),
                        ActualReturn = reader.IsDBNull(actualReturnOrdinal)
                            ? null
                            : reader.GetDateTime(actualReturnOrdinal),
                        FineCalculatedUntil = reader.IsDBNull(fineCalculatedUntilOrdinal)
                            ? null
                            : reader.GetDateTime(fineCalculatedUntilOrdinal),
                        BorrowRule = reader.GetInt32("borrowRule")
                    };


                }
                return null;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"GetBorrowByIdAsync error: {ex.Message}");
                return null;
            }
        }




        public async Task<int> GetActiveBorrowCountForBookAsync(int bookId)
        {
            try
            {

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"SELECT COUNT(*) FROM Borrows 
                            WHERE bookid = @BookId AND status IN (0, 2)";



                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@BookId", bookId);


                var count = Convert.ToInt32(await command.ExecuteScalarAsync());
                return count;
            }
            catch
            {
                return 0;
            }
        }

        public async Task<bool> UpdateBookAsync(Book book)
        {

            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"UPDATE Books SET isbn = @Isbn, title = @Title, author = @Author, publisher = @Publisher, quantity = @Quantity, description = @Description, notes = @Notes, bookFineRule = @BookFineRule 
                            WHERE bookid = @BookId";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Isbn", (object?)book.Isbn ?? DBNull.Value);
                command.Parameters.AddWithValue("@Title", book.Title);
                command.Parameters.AddWithValue("@Author", book.Author);
                command.Parameters.AddWithValue(
                    "@Publisher",
                    (object?)book.Publisher ?? DBNull.Value
                );
                command.Parameters.AddWithValue("@Quantity", book.Quantity);
                command.Parameters.AddWithValue(
                    "@Description",
                    (object?)book.Description ?? DBNull.Value
                );
                command.Parameters.AddWithValue("@Notes", (object?)book.Notes ?? DBNull.Value);
                command.Parameters.AddWithValue("@BookFineRule", book.BookFineRule);
                command.Parameters.AddWithValue("@BookId", book.BookId);

                var rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"UpdateBookAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateBorrowAsync(Borrow borrow)
        {
            try
            {

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"UPDATE Borrows SET userid = @UserId, bookid = @BookId, status = @Status, fine = @Fine, borrowRule = @BorrowRule, borrowed_at = @BorrowedAt, borrow_returndate = @BorrowReturnDate, actualreturn = @ActualReturn 
                            WHERE borrowid = @BorrowId";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserId", borrow.UserId);
                command.Parameters.AddWithValue("@BookId", borrow.BookId);
                command.Parameters.AddWithValue("@Status", borrow.Status);
                command.Parameters.AddWithValue("@BorrowedAt", borrow.BorrowedAt);
                command.Parameters.AddWithValue(
                    "@BorrowReturnDate",
                    borrow.BorrowReturnDate.HasValue
                      ? (object)borrow.BorrowReturnDate.Value
                      : DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@ActualReturn",
                    borrow.ActualReturn.HasValue ? (object)borrow.ActualReturn.Value : DBNull.Value
                );
                command.Parameters.AddWithValue("@Fine", borrow.Fine);
                command.Parameters.AddWithValue("@BorrowRule", borrow.BorrowRule);
                command.Parameters.AddWithValue("@BorrowId", borrow.BorrowId);

                var rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"UpdateBorrowAsync error: {ex.Message}");
                return false;
            }
        }


        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"UPDATE Accounts SET username = @UserName, email = @Email, admin = @Admin 
                            WHERE userid = @UserId";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@UserName", user.Username);
                command.Parameters.AddWithValue("@Email", user.Email);
                command.Parameters.AddWithValue("@Admin", user.Admin);
                command.Parameters.AddWithValue("@UserId", user.UserId);

                var rows = await command.ExecuteNonQueryAsync();
                return rows > 0;
            }
            catch (Exception ex)
            {

                Console.WriteLine($"UpdateUserAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<Borrow>> GetOverdueBorrowsAsync()
        {
            try
            {
                await RefreshBorrowStatusesAsync();

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"SELECT borrowid, userid, bookid, status, fine, borrowed_at, borrow_returndate, actualreturn, FineCalculatedUntil 
                            FROM Borrows 
                            WHERE status = 2 
                            ORDER BY borrowed_at DESC LIMIT 5";


                using var command = new MySqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var borrows = new List<Borrow>();
                var returnDateOrdinal = reader.GetOrdinal("borrow_returndate");
                var actualReturnOrdinal = reader.GetOrdinal("actualreturn");
                var fineCalculatedUntilOrdinal = reader.GetOrdinal("FineCalculatedUntil");


                while (await reader.ReadAsync())
                {
                    borrows.Add(
                        new Borrow
                        {

                            BorrowId = reader.GetInt32("borrowid"),
                            UserId = reader.GetInt32("userid"),
                            BookId = reader.GetInt32("bookid"),
                            Status = reader.GetInt32("status"),
                            Fine = reader.GetDecimal("fine"),
                            BorrowedAt = reader.GetDateTime("borrowed_at"),
                            BorrowReturnDate = reader.IsDBNull(returnDateOrdinal)
                                ? null
                                : reader.GetDateTime(returnDateOrdinal),
                            ActualReturn = reader.IsDBNull(actualReturnOrdinal)
                                ? null
                                : reader.GetDateTime(actualReturnOrdinal),
                            FineCalculatedUntil = reader.IsDBNull(fineCalculatedUntilOrdinal)
                                ? null
                                : reader.GetDateTime(fineCalculatedUntilOrdinal)
                        }
                    );
                }
                return borrows;
            }
            catch
            {
                return new List<Borrow>();
            }
        }


        public async Task<List<User>> GetAllUsersAsync()
        {


            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT userid, username, email, password, admin FROM Accounts ORDER BY userid DESC";


                using var command = new MySqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();


                var users = new List<User>();
                while (await reader.ReadAsync())
                {
                    users.Add(
                        new User
                        {
                            UserId = reader.GetInt32("userid"),
                            Username = reader.GetString("username"),
                            Email = reader.GetString("email"),
                            Password = reader.GetString("password"),
                            Admin = reader.GetInt32("admin")
                        }
                    );
                }
                return users;
            }
            catch
            {

                return new List<User>();
            }
        }

        public async Task<List<Book>> GetAllBooksAsync()
        {
            try
            {

                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "SELECT bookid, isbn, title, author, publisher, quantity, description, notes, bookFineRule FROM Books ORDER BY bookid DESC";

                using var command = new MySqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();

                var books = new List<Book>();
                while (await reader.ReadAsync())
                {

                    books.Add(
                        new Book
                        {
                            BookId = reader.GetInt32("bookid"),
                            Isbn = reader.GetString("isbn"),
                            Title = reader.GetString("title"),
                            Author = reader.GetString("author"),
                            Publisher = reader.IsDBNull(reader.GetOrdinal("publisher"))
                                ? string.Empty
                                : reader.GetString("publisher"),
                            Quantity = reader.GetInt32("quantity"),
                            Description = reader.IsDBNull(reader.GetOrdinal("description"))
                                ? null
                                : reader.GetString("description"),
                            Notes = reader.IsDBNull(reader.GetOrdinal("notes"))
                                ? null
                                : reader.GetString("notes"),
                            BookFineRule = reader.GetInt32("bookFineRule")
                        }
                                    
                    );
                }
                return books;
            }
            catch
            {
                return new List<Book>();
            }
        } 



        public async Task<List<FineRule>> GetAllFineRulesAsync()
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var sql = "SELECT ruleid, title, fee, fee_interval FROM FineRules";
                using var command = new MySqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync();
                var rules = new List<FineRule>();
                while (await reader.ReadAsync())
                {
                    rules.Add(
                        new FineRule
                        {
                            RuleId = reader.GetInt32("ruleid"),
                            Title = reader.GetString("title"),
                            Fee = reader.GetFloat("fee"),
                            FeeInterval = reader.GetInt32("fee_interval")
                        }
                    );
                }
                return rules;
            }
            catch
            {
                return new List<FineRule>();
            }
        }

        public async Task<bool> InsertFineRuleAsync(string title, float fee, int interval)
        {
            try
            {


                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    "INSERT INTO FineRules (title, fee, fee_interval) VALUES (@Title, @Fee, @Interval)";
                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Title", title);
                command.Parameters.AddWithValue("@Fee", fee);
                command.Parameters.AddWithValue("@Interval", interval);
                return await command.ExecuteNonQueryAsync() > 0;
            }
            catch
            {

                return false;
            }
        }

        public async Task<bool> UpdateFineRuleAsync(
            int ruleId,
            string title,
            float fee,
            int interval
        )
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();
                var sql =
                    "UPDATE FineRules SET title = @Title, fee = @Fee, fee_interval = @Interval WHERE ruleid = @RuleId";
                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@Title", title);
                command.Parameters.AddWithValue("@Fee", fee);
                command.Parameters.AddWithValue("@Interval", interval);
                command.Parameters.AddWithValue("@RuleId", ruleId);
                return await command.ExecuteNonQueryAsync() > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> InsertMediaAsync(Media media)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql =
                    @"INSERT INTO Media (bookid, path) 
                            VALUES (@BookId, @Path)";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@BookId", media.BookId);
                command.Parameters.AddWithValue(
                    "@Path",
                    media.Path != null ? media.Path : DBNull.Value
                );

                await command.ExecuteNonQueryAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"InsertMediaAsync error: {ex.Message}");
                return false;
            }
        }

        public async Task<Media?> GetMediaByBookIdAsync(int bookId)
        {
            try
            {
                using var connection = new MySqlConnection(_connectionString);
                await connection.OpenAsync();

                var sql = "SELECT mediaid, bookid, path FROM Media WHERE bookid = @BookId LIMIT 1";

                using var command = new MySqlCommand(sql, connection);
                command.Parameters.AddWithValue("@BookId", bookId);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var pathString = reader.IsDBNull(2) ? null : reader.GetString(2);
                    return new Media
                    {
                        MediaId = reader.GetInt32("mediaid"),
                        BookId = reader.GetInt32("bookid"),
                        Path = pathString
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetMediaByBookIdAsync error: {ex.Message}");
                return null;
            }
        }
    }
}
