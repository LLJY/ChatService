using System;
using System.Collections.Generic;

#nullable disable

namespace ChatService.Entities
{
    public partial class UsersRead
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public int MessageStatus { get; set; }
        public int MessageRef { get; set; }

        public virtual Message MessageRefNavigation { get; set; }
    }
}
