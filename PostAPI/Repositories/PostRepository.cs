﻿using Microsoft.EntityFrameworkCore;
using PostAPI.Interfaces;
using PostAPI.Models;

namespace PostAPI.Repositories
{
    public class PostRepository : IPost
    {
        private readonly AppDbContext _context;
        private readonly IUser _userService;
        private readonly IToken _tokenService;
        private readonly IComment _commentService;

        public PostRepository(AppDbContext context, IUser userService, IToken tokenService, IComment commentService)
        {
            _context = context;
            _userService = userService;
            _tokenService = tokenService;
            _commentService = commentService;
        }

        public async Task<bool> IdExists(int id)
        {
            return await _context.Posts.AnyAsync(i => i.Post_Id == id);
        }

        public async Task<int> CreatePost(Post post)
        {
            int userId = await _tokenService.ExtractIdFromToken();

            var newPost = new Post()
            {
                User_Id = userId,
                Created = DateTime.UtcNow,
                //Modified = DateTime.UtcNow, No modified date.
                Content = post.Content
            };

            _context.Add(newPost);
            _ = await _context.SaveChangesAsync() > 0;

            return newPost.Post_Id;
        }

        public async Task<bool> DeletePost(Post post)
        {
            var toDelete = await _context.Posts.FindAsync(post.Post_Id);

            var comparasion = await CompareTokenPostId(post.Post_Id);
            var admin = await _userService.CheckAdminStatus();

            var comments = await _commentService.GetComments(post.Post_Id);

            if (comparasion == true || admin)
            {
                _context.Posts.Remove(toDelete);
                // * This will delete all the comments of the post
                if(comments.Count > 0)
                {
                    foreach (var comment in comments)
                    {
                        _context.Comments.Remove(comment);
                    }
                }

                return await _context.SaveChangesAsync() > 0;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> UpdatePost(int id, Post post)
        {
            var comparasion = await CompareTokenPostId(id);
            var admin = await _userService.CheckAdminStatus();

            if (comparasion || admin)
            {
                var existingPost = await _context.Posts.FindAsync(id);

                if (existingPost == null)
                    return false;

                existingPost.Content = post.Content;
                existingPost.Modified = DateTime.UtcNow;

                _context.Update(existingPost);
                return await _context.SaveChangesAsync() > 0;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> CompareTokenPostId(int postId)
        {
            var post = await _context.Posts.FindAsync(postId);

            var idFromToken = await _tokenService.ExtractIdFromToken();
            var idFromPost = post.User_Id;

            return idFromToken == idFromPost;
        }

        public async Task<PostView> GetPostViewById(int id)
        {
            var post = await _context.Posts.GroupJoin(
                _context.Users,
                post => post.User_Id,
                user => user.User_Id,
                (posts, users) => new { posts, users })
                .SelectMany(
                x => x.users.DefaultIfEmpty(),
                (post, user) => new PostView
                {
                    Post_Id = post.posts.Post_Id,
                    Author = user.Username,
                    First_Name = user.First_Name,
                    Last_Name = user.Last_Name,
                    Created = post.posts.Created,
                    Modified = post.posts.Modified,
                    Content = post.posts.Content,
                    Profile_Picture = user != null ? user.Profile_Picture : "No picture",
                }
                ).FirstOrDefaultAsync(p => p.Post_Id == id);

            return post;
        }

        public async Task<List<PostView>> GetPosts(int page, int pageSize)
        {
            // * Some pagination logic and left join of tables to get the profile picture and first and last name per post
            int postsToSkip = (page - 1) * pageSize;
            var posts = await _context.Posts.GroupJoin(
                _context.Users,
                post => post.User_Id,
                user => user.User_Id,
                (posts, users) => new { posts, users })
                .SelectMany(
                x => x.users.DefaultIfEmpty(),
                (post, user) => new PostView
                {
                    Post_Id = post.posts.Post_Id,
                    Author = user.Username,
                    First_Name = user.First_Name,
                    Last_Name = user.Last_Name,
                    Created = post.posts.Created,
                    Modified = post.posts.Modified,
                    Content = post.posts.Content,
                    Profile_Picture = user != null ? user.Profile_Picture : "No picture",
                }
                ).OrderByDescending(p => p.Created).Skip(postsToSkip).Take(pageSize).ToListAsync();
            return posts;
        }

        public async Task<Post> GetPostById(int id)
        {
            return await _context.Posts.FirstOrDefaultAsync(p => p.Post_Id == id);
        }

        public async Task<List<PostView>> GetUserPostsByUsername(string username)
        {
            return await _context.Posts.GroupJoin(
                _context.Users,
                post => post.User_Id,
                user => user.User_Id,
                (posts, users) => new { posts, users })
                .SelectMany(
                x => x.users.DefaultIfEmpty(),
                (post, user) => new PostView
                {
                    Post_Id = post.posts.Post_Id,
                    User_Id = post.posts.User_Id,
                    Author = user.Username,
                    First_Name = user.First_Name,
                    Last_Name = user.Last_Name,
                    Created = post.posts.Created,
                    Modified = post.posts.Modified,
                    Content = post.posts.Content,
                    Profile_Picture = user != null ? user.Profile_Picture : "No picture"
                }
                )
                .OrderByDescending(p => p.Created)
                .Where(p => p.Author == username)
                .ToListAsync();
        }
    }
}
