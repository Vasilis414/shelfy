# Shelfy

Shelfy is a library management website built with ASP.NET Core MVC and MySQL. It has separate pages for members and administrators.

Members can browse books, see their borrow history, pay demo fines and enable email two-factor authentication. Administrators can manage books, members, borrows and fine rules.

## Info

- C# and .NET 9
- ASP.NET Core MVC and Razor views
- MySQL with `MySqlConnector`
- Entity Framework Core for the application context and model setup
- Bootstrap, JavaScript and SweetAlert2

## Project structure

- `Controllers` handles page requests and form actions.
- `Models` contains the objects used by the application and the forms.
- `Data/ApplicationDbContext.cs` contains the database queries.
- `Services` contains password hashing and email sending.
- `Views` contains the Razor pages and their page-specific JavaScript.
- `wwwroot` contains CSS, JavaScript, images and uploaded book covers.
