﻿using CloudinaryDotNet.Actions;
using CloudinaryDotNet;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PostAPI.Interfaces;
using PostAPI.Models;
using PostAPI.OptionsSetup;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PostAPI.Dto;

namespace PostAPI.Repositories
{
    public class UserRepository : IUser
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _http;
        private readonly Cloudinary _cloudinary;
        private readonly IToken _tokenService;
        private readonly JwtOptions _options;

        public UserRepository(AppDbContext context, IHttpContextAccessor http, IOptions<JwtOptions> options, Cloudinary cloudinary)
        {
            _context = context;
            _http = http;
            _cloudinary = cloudinary;
            _options = options.Value;
        }

        public async Task<bool> CheckAdminStatus()
        {
            var token = await GetToken();
            var decoded = await DecodeHS512(token);
            var role = decoded.role;

            return role == "admin";
        }

        public async Task<bool> CreateUser(User user)
        {
            string salt = BCrypt.Net.BCrypt.GenerateSalt();
            string hashedPw = BCrypt.Net.BCrypt.HashPassword(user.Password, salt);

            // * Get the value of the Profile_Picture field from the payload. Upload to Cloudinary...
            string? profilePictureUrl = await UploadProfilePicture(user.Profile_Picture);

            var newUser = new User()
            {
                Username = user.Username.ToLower(), // * Required
                First_Name = user.First_Name,
                Last_Name = user.Last_Name,
                Created = DateTime.Now,
                Email = user.Email.ToLower(), // * Required
                Role = user.Role.ToLower(), // * Required
                Password = hashedPw, // * Required
                Gender = user.Gender,
                Birthday = user.Birthday,
                Profile_Picture = profilePictureUrl, // * And save the URL on the database
                Status = user.Status,
                Last_Login = user.Last_Login,
            };

            _context.Add(newUser);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<(int id, string role)> DecodeHS512(string token)
        {
            JwtSecurityTokenHandler tokenHandler = new();
            var jwtToken = tokenHandler.ReadJwtToken(token);

            string idString = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
            _ = int.TryParse(idString, out int id);

            string role = jwtToken.Claims.FirstOrDefault(r => r.Type == "role")?.Value;

            return (id, role);
        }

        public async Task<bool> DeleteUser(User user)
        {
            // Injecting tokenService causes circular dependency issue
            var token = await GetToken();
            if (token == null) return false;

            var (id, _) = await DecodeHS512(token);
            if(id == user.User_Id)
            {
                _context.Users.Remove(user);
                return await _context.SaveChangesAsync() > 0;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> EmailExists(string email)
        {
            return await _context.Users.AnyAsync(e => e.Email == email);
        }

        public async Task<string> GetHashedPassword(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(p => p.Username == username);
            return user?.Password;
        }

        public async Task<string?> GetToken()
        {
            string token = _http.HttpContext.Request.Headers.Authorization.ToString();
            return token.Replace("Bearer", "").Trim();
        }

        public async Task<User> GetUser(string username)
        {
            return await _context.Users.Where(u => u.Username == username).FirstOrDefaultAsync();
        }

        public async Task<User> GetUserById(int userId)
        {
            return await _context.Users.Where(u => u.User_Id == userId).FirstOrDefaultAsync();
        }

        public async Task<List<User>> GetUsers()
        {
            return await _context.Users.OrderBy(user => user.User_Id).ToListAsync();
        }

        public string JwtTokenGenerator(User user)
        {
            // * Set up the JWT payload
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.User_Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddDays(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)), SecurityAlgorithms.HmacSha512Signature)
            };

            // * Generate the JWT Token
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        public async Task<bool> UpdateUser(UserUpdateDto user)
        {
            var token = await GetToken();
            var (id, _) = await DecodeHS512(token);
            string? hashedPw = null;
            string? profilePictureUrl = null;

            if (!string.IsNullOrEmpty(user.Password))
            {
                string salt = BCrypt.Net.BCrypt.GenerateSalt();
                hashedPw = BCrypt.Net.BCrypt.HashPassword(user.Password, salt);
            }

            if(!string.IsNullOrEmpty(user.Profile_Picture))
                profilePictureUrl = await UploadProfilePicture(user.Profile_Picture);

            var existingUser = _context.Users.Find(id);

            if (existingUser != null)
            {
                // * Might use AutoMapper for this
                existingUser.First_Name = user.First_Name ?? existingUser.First_Name;
                existingUser.Last_Name = user.Last_Name ?? existingUser.Last_Name;
                existingUser.Email = user.Email ?? existingUser.Email;
                existingUser.Password = hashedPw ?? existingUser.Password;
                existingUser.Gender = user.Gender ?? existingUser.Gender;
                existingUser.Birthday = user.Birthday ?? existingUser.Birthday;
                existingUser.Profile_Picture = profilePictureUrl ?? existingUser.Profile_Picture;
                existingUser.Bio = user.Bio ?? existingUser.Bio;
                existingUser.Status = user.Status ?? existingUser.Status;

                _context.Update(existingUser);
                return await _context.SaveChangesAsync() > 0;
            }
            else
            {
                return false;
            }
        }

        public async Task<string?> UploadProfilePicture(string path)
        {
            if(string.IsNullOrEmpty(path)) return null; // * Profile picture its opional.

            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(@path),
                Transformation = new Transformation().Width(300).Height(300), // * Image resize to 300x300 before uploading
                UseFilename = true,
                UniqueFilename = true,
                Overwrite = true
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            var url = uploadResult.SecureUrl;

            return url.OriginalString; // * Return the Cloudinary URL
        }

        public async Task<bool> UserExists(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task<bool> UserIdExists(int userId)
        {
            return await _context.Users.AnyAsync(i => i.User_Id == userId);
        }
    }
}