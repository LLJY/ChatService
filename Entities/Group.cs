using System;
using System.Collections.Generic;

#nullable disable

namespace ChatService.Entities
{
    public partial class Group
    {
        public Group()
        {
            Messages = new HashSet<Message>();
        }

        public int Id { get; set; }
        public Guid Uuid { get; set; }
        public string Title { get; set; }
        public int? PhotoRef { get; set; }

        public virtual Media PhotoRefNavigation { get; set; }
        public virtual GroupMember GroupMember { get; set; }
        public virtual ICollection<Message> Messages { get; set; }
    }
}
