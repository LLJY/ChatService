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
 public static Message CreateMessageFromRequest(Protos.Message message, string userId, McsvChatDbContext db)
        {
            // if message type is a media message, attatch a media to it
            if (message.Media != null)
            {
                var messageMedia = new Media
                {
                    MimeType = message.Media.MediaUrl,
                    Url = message.Media.MediaUrl,
                    Uuid = Guid.NewGuid(),
                    Size = (int) message.Media.SizeBytes
                };
                if (Guid.TryParse( message.GroupId, out var guid1))
                {
                    return new Message
                    {
                        Dateposted = DateTime.Now,
                        Text = message.Message_,
                        Uuid = Guid.NewGuid(),
                        AuthorId = userId,
                        IsForward = message.IsForward,
                        ReceiverId = message.ReceiverUserId,
                        GroupRefNavigation =
                            db.Groups.First(x => x.Uuid == guid1),
                        MediaRefNavigation = messageMedia
                    };
                    
                }

                return new Message
                {
                    Dateposted = DateTime.Now,
                    Text = message.Message_,
                    Uuid = Guid.NewGuid(),
                    AuthorId = userId,
                    IsForward = message.IsForward,
                    ReceiverId = message.ReceiverUserId,
                    GroupRefNavigation =
                        db.Groups.First(x => x.Uuid == Guid.Parse((ReadOnlySpan<char>) message.GroupId)),
                    MediaRefNavigation = messageMedia
                };
            }
            // if not media source, the rest is normal.
            Console.WriteLine($"Message groupId = {message.GroupId==null}");
            var guid = Guid.Empty;
            if (Guid.TryParse( message.GroupId, out guid))
            {
                return new Message
                {
                    Dateposted = DateTime.Now,
                    Text = message.Message_,
                    Uuid = Guid.NewGuid(),
                    AuthorId = userId,
                    IsForward = message.IsForward,
                    ReceiverId = message.ReceiverUserId,
                    GroupRefNavigation =
                        db.Groups.First(x => x.Uuid == guid),
                };
            }

            return new Message
            {
                Dateposted = DateTime.Now,
                Text = message.Message_,
                Uuid = Guid.NewGuid(),
                AuthorId = userId,
                ReceiverId = message.ReceiverUserId,
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
        public string ReceiverId { get; set; }

        public virtual Group GroupRefNavigation { get; set; }
        public virtual Media MediaRefNavigation { get; set; }
        public virtual Message ReplyMessageRefNavigation { get; set; }
        public virtual ICollection<Message> InverseReplyMessageRefNavigation { get; set; }
        public virtual ICollection<UsersRead> UsersReads { get; set; }
    }
}
