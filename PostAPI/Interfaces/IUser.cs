﻿using PostAPI.Dto;
using PostAPI.Models;

namespace PostAPI.Interfaces
{
    public interface IUser
    {
        Task<List<User>> GetUsers();
        Task<bool> CreateUser(User user);
        Task<bool> UpdateUser(UserUpdateDto user);
        Task<bool> DeleteUser(User user);
        Task<string?> UploadProfilePicture(string path);
        Task<User> GetUser(string username);
        Task<User> GetUserById(int userId);
        Task<bool> UserExists(string username);
        Task<bool> UserIdExists(int userId);
        Task<bool> EmailExists(string email);
        string JwtTokenGenerator(User user);
        Task<string> GetHashedPassword(string username);
        Task<bool> CheckAdminStatus();
        Task<string> GetToken();
        Task<(int id, string role)> DecodeHS512(string token);
    }
}