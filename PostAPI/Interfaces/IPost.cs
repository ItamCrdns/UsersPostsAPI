﻿using PostAPI.Models;

namespace PostAPI.Interfaces
{
    public interface IPost
    {
        Task DeleteRecursiveComments(Comment comment);
        Task AddImagesToPost(List<IFormFile> files, int posttId);
        IQueryable<PostView> PostJoinQuery();
        Task<List<PostView>> GetPosts(int page, int pageSize);
        Task<List<PostView>> GetPostsByGroupId(int groupId);
        Task<bool> IdExists(int id);
        Task<PostView> GetPostViewById(int id);
        Task<List<PostView>> GetUserPostsByUsername(string username);
        Task<Post> GetPostById(int id);
        //Task<bool> CompareTokenPostId(int  postId);
        Task<int> CreatePost(List<IFormFile> files, Post post, int? groupId);
        Task<bool> DeletePost(Post post);
        Task<bool> UpdatePost(List<IFormFile> files, int postId, Post post);
        Task<bool> AddImageToPost(IFormFile file, int postId);
    }
}
