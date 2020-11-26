using System;
using System.Collections.Generic;

#nullable disable

namespace ChatService.Entities
{
    public partial class GroupMember
    {
        public int Id { get; set; }
        public bool IsAdmin { get; set; }
        public int GroupRef { get; set; }
        public string Userid { get; set; }

        public virtual Group GroupRefNavigation { get; set; }
    }
}
