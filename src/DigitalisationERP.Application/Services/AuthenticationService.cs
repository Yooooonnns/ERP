using System;
using System.Threading.Tasks;
using DigitalisationERP.Domain.Entities;
using DigitalisationERP.Application.Interfaces;

namespace DigitalisationERP.Application.Services
{
    /// <summary>
    /// Implémentation du service d'authentification
    /// </summary>
    public class AuthenticationService : IAuthenticationService
    {
        // Mock pour démo - à remplacer par repository réel
        private static readonly Dictionary<string, User> _users = new();

        public async Task<Result<User>> RegisterAsync(string email, string password, 
            string firstName, string lastName)
        {
            await Task.Delay(100); // Simule I/O

            // Validation
            if (string.IsNullOrWhiteSpace(email))
                return Result<User>.Fail("Email is required");

            if (_users.ContainsKey(email))
                return Result<User>.Fail("Email already registered");

            if (password.Length < 8)
                return Result<User>.Fail("Password must be at least 8 characters");

            // Créer utilisateur
            var user = new User
            {
                UserId = _users.Count + 1,
                Email = email,
                PasswordHash = HashPassword(password),
                FirstName = firstName,
                LastName = lastName,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            _users[email] = user;
            return Result<User>.Ok(user, "User registered successfully");
        }

        public async Task<Result<User>> LoginAsync(string email, string password)
        {
            await Task.Delay(100); // Simule I/O

            if (!_users.TryGetValue(email, out var user))
                return Result<User>.Fail("Invalid email or password");

            if (!VerifyPassword(password, user.PasswordHash))
                return Result<User>.Fail("Invalid email or password");

            if (!user.IsActive)
                return Result<User>.Fail("Account is disabled");

            return Result<User>.Ok(user, "Login successful");
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            await Task.Delay(50);
            return !string.IsNullOrEmpty(token);
        }

        private string HashPassword(string password)
        {
            // Utiliser BCrypt en production
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }

        private bool VerifyPassword(string password, string hash)
        {
            var hashOfInput = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
            return hashOfInput == hash;
        }
    }
}
