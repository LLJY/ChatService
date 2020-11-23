using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable

namespace ChatService.Entities
{
    public partial class Message
    {
        public Message()
        {
            InverseReplyMessageRefNavigation = new HashSet<Message>();
            UsersReads = new HashSet<UsersRead>();
        }
        /**
         * Converts a protobuf contract message to a database storable message
         */
        public static Message CreateMessageFromRequest(Protos.Message message, string userId, McsvChatDbContext db)
        {
            // if message type is a media message, attatch a media to it
            if (message.Media != null)
            {
                var messageMedia = new Media
                {
                    MimeType = message.Media.MediaUrl,
                    Url = message.Media.MediaUrl,
                    Uuid = new Guid(),
                    Size = (int) message.Media.SizeBytes
                };
                if (message.GroupId != null)
                {
                    return new Message
                    {
                        Dateposted = DateTime.Now,
                        Text = message.Message_,
                        Uuid = new Guid(),
                        AuthorId = userId,
                        IsForward = message.IsForward,
                        GroupRefNavigation =
                            db.Groups.First(x => x.Uuid == Guid.Parse((ReadOnlySpan<char>) message.GroupId)),
                        MediaRefNavigation = messageMedia
                    };
                }

                return new Message
                {
                    Dateposted = DateTime.Now,
                    Text = message.Message_,
                    Uuid = new Guid(),
                    AuthorId = userId,
                    IsForward = message.IsForward,
                    GroupRefNavigation =
                        db.Groups.First(x => x.Uuid == Guid.Parse((ReadOnlySpan<char>) message.GroupId)),
                    MediaRefNavigation = messageMedia
                };
            }
            // if not media source, the rest is normal.
            if (message.GroupId != null)
            {
                return new Message
                {
                    Dateposted = DateTime.Now,
                    Text = message.Message_,
                    Uuid = new Guid(),
                    AuthorId = userId,
                    IsForward = message.IsForward,
                    GroupRefNavigation =
                        db.Groups.First(x => x.Uuid == Guid.Parse((ReadOnlySpan<char>) message.GroupId)),
                };
            }

            return new Message
            {
                Dateposted = DateTime.Now,
                Text = message.Message_,
                Uuid = new Guid(),
                AuthorId = userId,
                IsForward = message.IsForward,
            };
        }

        public int Id { get; set; }
        public Guid Uuid { get; set; }
        public int? MediaRef { get; set; }
        public string Text { get; set; }
        public int? ReplyMessageRef { get; set; }
        public bool IsForward { get; set; }
        public string AuthorId { get; set; }
        public int? GroupRef { get; set; }
        public DateTime Dateposted { get; set; }
        public string RecieverId { get; set; }

        public virtual Group GroupRefNavigation { get; set; }
        public virtual Media MediaRefNavigation { get; set; }
        public virtual Message ReplyMessageRefNavigation { get; set; }
        public virtual ICollection<Message> InverseReplyMessageRefNavigation { get; set; }
        public virtual ICollection<UsersRead> UsersReads { get; set; }
    }
}
