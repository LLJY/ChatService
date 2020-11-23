using System;
using System.Collections.Generic;

#nullable disable

namespace ChatService.Entities
{
    public partial class Media
    {
        public Media()
        {
            Groups = new HashSet<Group>();
            Messages = new HashSet<Message>();
        }

        public int Id { get; set; }
        public Guid Uuid { get; set; }
        public string Url { get; set; }
        public string MimeType { get; set; }
        public int Size { get; set; }

        public virtual ICollection<Group> Groups { get; set; }
        public virtual ICollection<Message> Messages { get; set; }
    }
}
