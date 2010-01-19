﻿using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Orchard.Comments.Models;
using Orchard.ContentManagement.Aspects;
using Orchard.Data;
using Orchard.Logging;
using Orchard.ContentManagement;
using Orchard.Security;
using Orchard.Settings;
using Orchard.UI.Notify;

namespace Orchard.Comments.Services {
    public interface ICommentService : IDependency {
        IEnumerable<Comment> GetComments();
        IEnumerable<Comment> GetComments(CommentStatus status);
        IEnumerable<Comment> GetCommentsForCommentedContent(int id);
        IEnumerable<Comment> GetCommentsForCommentedContent(int id, CommentStatus status);
        Comment GetComment(int id);
        ContentItemMetadata GetDisplayForCommentedContent(int id);
        void CreateComment(Comment comment);
        void UpdateComment(int id, string name, string email, string siteName, string commentText, CommentStatus status);
        void ApproveComment(int commentId);
        void PendComment(int commentId);
        void MarkCommentAsSpam(int commentId);
        void DeleteComment(int commentId);
        bool CommentsClosedForCommentedContent(int id);
        void CloseCommentsForCommentedContent(int id);
        void EnableCommentsForCommentedContent(int id);
    }

    public class CommentService : ICommentService {
        private readonly IRepository<Comment> _commentRepository;
        private readonly IRepository<ClosedComments> _closedCommentsRepository;
        private readonly IRepository<HasCommentsRecord> _hasCommentsRepository;
        private readonly ICommentValidator _commentValidator;
        private readonly IContentManager _contentManager;
        private readonly IAuthorizer _authorizer;
        private readonly INotifier _notifier;

        public CommentService(IRepository<Comment> commentRepository,
                              IRepository<ClosedComments> closedCommentsRepository,
                              IRepository<HasCommentsRecord> hasCommentsRepository,
                              ICommentValidator commentValidator,
                              IContentManager contentManager,
                              IAuthorizer authorizer,
                              INotifier notifier) {
            _commentRepository = commentRepository;
            _closedCommentsRepository = closedCommentsRepository;
            _hasCommentsRepository = hasCommentsRepository;
            _commentValidator = commentValidator;
            _contentManager = contentManager;
            _authorizer = authorizer;
            _notifier = notifier;
            Logger = NullLogger.Instance;
        }

        public ILogger Logger { get; set; }
        protected virtual ISite CurrentSite { get; [UsedImplicitly] private set; }


        #region Implementation of ICommentService

        public IEnumerable<Comment> GetComments() {
            return from comment in _commentRepository.Table.ToList() select comment;
        }

        public IEnumerable<Comment> GetComments(CommentStatus status) {
            return from comment in _commentRepository.Table.ToList() where comment.Status == status select comment;
        }

        public IEnumerable<Comment> GetCommentsForCommentedContent(int id) {
            return from comment in _commentRepository.Table.ToList() where comment.CommentedOn == id select comment;
        }

        public IEnumerable<Comment> GetCommentsForCommentedContent(int id, CommentStatus status) {
            return from comment in _commentRepository.Table.ToList() where comment.CommentedOn == id && comment.Status == status select comment;
        }

        public Comment GetComment(int id) {
            return _commentRepository.Get(id);
        }

        public ContentItemMetadata GetDisplayForCommentedContent(int id) {
            var content = _contentManager.Get(id);
            if (content == null)
                return null;
            return _contentManager.GetItemMetadata(content);
        }

        public void CreateComment(Comment comment) {
            comment.Status = _commentValidator.ValidateComment(comment) ? CommentStatus.Pending : CommentStatus.Spam;

            // store id of the next layer for large-grained operations, e.g. rss on blog
            var commentedOn = _contentManager.Get<ICommonAspect>(comment.CommentedOn);
            if (commentedOn != null && commentedOn.Container != null) {
                comment.CommentedOnContainer = commentedOn.Container.ContentItem.Id;
            }

            _commentRepository.Create(comment);
        }

        public void UpdateComment(int id, string name, string email, string siteName, string commentText, CommentStatus status) {
            Comment comment = GetComment(id);
            comment.Author = name;
            comment.Email = email;
            comment.SiteName = siteName;
            comment.CommentText = commentText;
            comment.Status = status;
        }

        public void ApproveComment(int commentId) {
            Comment comment = GetComment(commentId);
            comment.Status = CommentStatus.Approved;
        }

        public void PendComment(int commentId) {
            Comment comment = GetComment(commentId);
            comment.Status = CommentStatus.Pending;
        }

        public void MarkCommentAsSpam(int commentId) {
            Comment comment = GetComment(commentId);
            comment.Status = CommentStatus.Spam;
        }

        public void DeleteComment(int commentId) {
            _commentRepository.Delete(GetComment(commentId));
        }

        public bool CommentsClosedForCommentedContent(int id) {
            return _closedCommentsRepository.Get(x => x.ContentItemId == id) == null ? false : true;
        }

        public void CloseCommentsForCommentedContent(int id) {
            _closedCommentsRepository.Create(new ClosedComments { ContentItemId = id });
        }

        public void EnableCommentsForCommentedContent(int id) {
            ClosedComments closedComments = _closedCommentsRepository.Get(x => x.ContentItemId == id);
            if (closedComments != null) {
                _closedCommentsRepository.Delete(closedComments);
            }
        }

        #endregion
    }
}
