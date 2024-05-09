using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UserManagement.Data;
using UserManagement.Models;

namespace UserManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    static readonly string adminToken = "eyJhbGciOiJIUzI1NiJ9.eyJSb2xlIjoiQWRtaW4iLCJJc3N1ZXIiOiJGYXRpbWEiLCJVc2VybmFtZSI6IkFkbWluIiwiZXhwIjoxNzE1Mjk3NDcwLCJpYXQiOjE3MTUyODY2NzB9.k3qT8ko9Ccv4fyTK2c9uKec1WN0_08Yd-QjF4_v507w";
    
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public UserController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {  
            var unauthorizedObjectResult = AuthenticateRequestByAdminToken();
            if (unauthorizedObjectResult != null)
                return unauthorizedObjectResult;

            //Select specific fields of users
            var users = await _context.Users
                .Select(u => new { u.Id, u.Name, u.Email, u.PhoneNumber, u.DateOfBirth })
                .ToListAsync();  
            return Ok(users);
        }
        catch(Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPost("create")]
    public async Task<ActionResult<User>> CreateUser([FromBody] CreateUserModel model)
    {
        try
        {
            var unauthorizedObjectResult = AuthenticateRequestByAdminToken();
            if (unauthorizedObjectResult != null)
                return unauthorizedObjectResult;

            // Check if email already exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (existingUser != null)
            {
                return Conflict("Email address already exists.");
            }

            // Mapping model to user
            var user = new User
            {
                Name = model.Name,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth.Value,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password)
            };

            // Add User to the database
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            //Generate and Store token in cookies
            var token = GenerateJwtToken(user);
            Response.Cookies.Append("jwt", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict
            });

            return CreatedAtAction(nameof(CreateUser), new { user.Id, user.Name, user.Email, user.PhoneNumber, user.DateOfBirth });  //User added successfully
        }
        catch(Exception ex)
        {
            return BadRequest($"Failed to create user: {ex.Message}");
        }
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserModel model)
    {
        try
        {
            var unauthorizedObjectResult = AuthenticateRequestByAdminToken();
            if (unauthorizedObjectResult != null)
                return unauthorizedObjectResult;

            // Find user by id
            var user = await _context.Users.FindAsync(model.Id);
            if (user == null)
            {
                return NotFound($"User with Id '{model.Id}' is not found");
            }

            // Update user details
            user.Name = model.Name;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.DateOfBirth = model.DateOfBirth.Value;
            user.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);

            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Name, user.Email, user.PhoneNumber, user.DateOfBirth });  //User updated successfully
        }
        catch(Exception ex)
        {
            return BadRequest($"Failed to update user: {ex.Message}");
        }
    }

    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            var unauthorizedObjectResult = AuthenticateRequestByAdminToken();
            if (unauthorizedObjectResult != null)
                return unauthorizedObjectResult;

            // Find user by Id
            var user = await _context.Users.FindAsync(id);
            if (user == null) 
            {
                return NotFound($"User with Id '{id}' is not found");
            }
            
            // Remove user from the database
            _context.Users.Remove(user); 
            await _context.SaveChangesAsync();

            return NoContent(); // User deleted successfully
        }
        catch(Exception ex)
        {
            return BadRequest($"Failed to delete user: {ex.Message}");
        }
    }

    [HttpPost("addBalance/{userId}")]
    public async Task<IActionResult> AddBalance(int userId, [FromBody] decimal amount)
    {
        try
        {
            var unauthorizedObjectResult = AuthenticateRequestByAdminToken();
            if (unauthorizedObjectResult != null)
                return unauthorizedObjectResult;

            // Find user by Id
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound($"User with Id {userId} is not found");
            }

            // Add balance to user's account
            user.Balance += amount;
            await _context.SaveChangesAsync();

            return Ok($"Balance added successfully. New Balance: {user.Balance}."); // Balance added successfully
        }
        catch(Exception ex)
        {
            return BadRequest($"Failed to add the balance to user's account: {ex.Message}");
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] UserLoginModel model)
    {
        // Find user by email and verify the password
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == model.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.Password))
        {
            return Unauthorized("Incorrect email or password");
        }

        //Generate and store token in cookies
        var token = GenerateJwtToken(user);
        Response.Cookies.Append("jwt", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });

        return Ok(new { Token = token });
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> TransferFunds([FromBody] TransferFundsModel model)
    {
        // Get sender's email from token
        var senderEmail = User.Identity.Name;
        var sender = await _context.Users.SingleOrDefaultAsync(u => u.Email == senderEmail);
        if (sender == null)
        {
            return Unauthorized("Sender not found. Please make sure you are logged in.");
        }

        // Find receiver by Id
        var receiver = await _context.Users.FindAsync(model.ReceiverId);
        if (receiver == null)
        {
            return NotFound($"Receiver with Id '{model.ReceiverId}' not found");
        }

        // Check input amount greater than 0
        if (model.Amount <= 0)
        {
            return BadRequest("Amount must be greater than zero");
        }

        // Check sender has enough balance
        if (sender.Balance < model.Amount)
        {
            return BadRequest("Your balance is insufficient for this transfer"); 
        }

        // Perform the transfer
        sender.Balance -= model.Amount.Value;
        receiver.Balance += model.Amount.Value;

        await _context.SaveChangesAsync();

        return Ok($"Tranfer successful. Your new balance: {sender.Balance}."); // Transfer successful
    }

    private UnauthorizedObjectResult? AuthenticateRequestByAdminToken()
    {
        string token = Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        if (token != adminToken)
        {
            return Unauthorized("You are not authorized to perform this action");
        }
        return null;
    }

    private string GenerateJwtToken(User user)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var userClaims = new[]
        {
            new Claim(ClaimTypes.Name, user.Email),
        };
        var token = new JwtSecurityToken(
            _configuration["Jwt:Issuer"],
            _configuration["Jwt:Audience"],
            userClaims,
            expires: DateTime.Now.AddHours(1),
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}